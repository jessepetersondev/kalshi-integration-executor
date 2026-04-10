using System.Globalization;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

/// <summary>
/// Handles order created events.
/// </summary>
public sealed class OrderCreatedHandler
{
    private readonly IKalshiExecutionClient _kalshiExecutionClient;
    private readonly IResultEventPublisher _resultEventPublisher;
    private readonly IConsumedEventStore _consumedEventStore;
    private readonly IExecutionRecordStore _executionRecordStore;
    private readonly IExecutionRiskGuard _executionRiskGuard;
    private readonly ExecutionReliabilityPolicy _executionReliabilityPolicy;

    public OrderCreatedHandler(
        IKalshiExecutionClient kalshiExecutionClient,
        IResultEventPublisher resultEventPublisher,
        IConsumedEventStore consumedEventStore,
        IExecutionRecordStore executionRecordStore,
        IExecutionRiskGuard executionRiskGuard,
        ExecutionReliabilityPolicy executionReliabilityPolicy)
    {
        _kalshiExecutionClient = kalshiExecutionClient;
        _resultEventPublisher = resultEventPublisher;
        _consumedEventStore = consumedEventStore;
        _executionRecordStore = executionRecordStore;
        _executionRiskGuard = executionRiskGuard;
        _executionReliabilityPolicy = executionReliabilityPolicy;
    }

    public async Task HandleAsync(ApplicationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var eventKey = envelope.IdempotencyKey ?? envelope.Id.ToString();
        if (await _consumedEventStore.HasProcessedAsync(eventKey, cancellationToken))
        {
            return;
        }

        var actionType = GetAttribute(envelope, "actionType") ?? "entry";
        var request = new KalshiOrderRequest(
            MarketTicker: GetAttribute(envelope, "ticker") ?? string.Empty,
            Side: GetAttribute(envelope, "side") ?? string.Empty,
            Quantity: TryGetInt(envelope, "quantity") ?? 0,
            LimitPrice: TryGetDecimal(envelope, "limitPrice") ?? 0m,
            ClientOrderId: envelope.ResourceId ?? envelope.Id.ToString(),
            Action: actionType == "cancel" ? "cancel" : actionType == "exit" ? "sell" : "buy",
            ReduceOnly: actionType == "exit",
            ActionType: actionType,
            TargetPublisherOrderId: GetAttribute(envelope, "targetPublisherOrderId"),
            TargetClientOrderId: GetAttribute(envelope, "targetClientOrderId"),
            TargetExternalOrderId: GetAttribute(envelope, "targetExternalOrderId"));

        var riskDecision = await _executionRiskGuard.EvaluateAsync(request, cancellationToken);
        if (!riskDecision.IsAllowed)
        {
            var blockedEvent = new ApplicationEventEnvelope(
                Guid.NewGuid(),
                "executor",
                "order.execution_blocked",
                envelope.ResourceId,
                envelope.CorrelationId,
                envelope.IdempotencyKey,
                BuildResultAttributes(
                    envelope,
                    actionType,
                    attemptCount: 1,
                    additionalAttributes: new Dictionary<string, string?>
                    {
                        ["blockCode"] = riskDecision.Code,
                        ["blockReason"] = riskDecision.Reason,
                    },
                    normalizedStatus: "execution_blocked"),
                DateTimeOffset.UtcNow);

            await _resultEventPublisher.PublishAsync(blockedEvent, cancellationToken);
            await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, cancellationToken);
            return;
        }

        await _executionReliabilityPolicy.ExecuteAsync(
            envelope,
            deadLetterQueue: "kalshi.integration.executor.dlq",
            async (attemptCount, token) =>
            {
                try
                {
                    var response = actionType == "cancel"
                        ? await _kalshiExecutionClient.CancelOrderAsync(await ResolveCancelTargetExternalOrderIdAsync(request, token), token)
                        : await _kalshiExecutionClient.PlaceOrderAsync(request, token);

                    await _executionRecordStore.UpsertAsync(
                        new ExecutionRecord(
                            response.ExternalOrderId,
                            response.ClientOrderId,
                            envelope.ResourceId,
                            envelope.CorrelationId,
                            envelope.Id.ToString(),
                            actionType,
                            GetAttribute(envelope, "tradeIntentId"),
                            GetAttribute(envelope, "publisherOrderId") ?? envelope.ResourceId,
                            response.Ticker ?? request.MarketTicker,
                            response.Side ?? request.Side,
                            response.Action ?? request.Action,
                            request.TargetPublisherOrderId,
                            request.TargetClientOrderId,
                            request.TargetExternalOrderId,
                            response.Status,
                            request.Quantity,
                            request.LimitPrice,
                            request.Quantity * request.LimitPrice,
                            response.RawBody,
                            DateTimeOffset.UtcNow),
                        token);

                    var successEvent = new ApplicationEventEnvelope(
                        Guid.NewGuid(),
                        "executor",
                        "order.execution_succeeded",
                        envelope.ResourceId,
                        envelope.CorrelationId,
                        envelope.IdempotencyKey,
                        BuildResultAttributes(
                            envelope,
                            actionType,
                            attemptCount,
                            normalizedStatus: "execution_succeeded",
                            additionalAttributes: new Dictionary<string, string?>
                            {
                                ["externalOrderId"] = response.ExternalOrderId,
                                ["clientOrderId"] = response.ClientOrderId,
                                ["ticker"] = response.Ticker ?? request.MarketTicker,
                                ["side"] = response.Side ?? request.Side,
                                ["action"] = response.Action ?? request.Action,
                                ["orderStatus"] = response.Status,
                            }),
                        DateTimeOffset.UtcNow);

                    await _resultEventPublisher.PublishAsync(successEvent, token);
                    await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, token);
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var failureEvent = new ApplicationEventEnvelope(
                        Guid.NewGuid(),
                        "executor",
                        "order.execution_failed",
                        envelope.ResourceId,
                        envelope.CorrelationId,
                        envelope.IdempotencyKey,
                        BuildResultAttributes(
                            envelope,
                            actionType,
                            attemptCount,
                            normalizedStatus: "execution_failed",
                            additionalAttributes: new Dictionary<string, string?>
                            {
                                ["errorType"] = exception.GetType().Name,
                                ["errorMessage"] = exception.Message,
                            }),
                        DateTimeOffset.UtcNow);

                    await _resultEventPublisher.PublishAsync(failureEvent, token);
                    await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, token);
                }
            },
            cancellationToken);
    }

    private async Task<string> ResolveCancelTargetExternalOrderIdAsync(KalshiOrderRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.TargetExternalOrderId))
        {
            return request.TargetExternalOrderId;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetClientOrderId))
        {
            var byClientOrderId = await _executionRecordStore.GetByClientOrderIdAsync(request.TargetClientOrderId, cancellationToken);
            if (byClientOrderId is not null)
            {
                return byClientOrderId.ExternalOrderId;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.TargetPublisherOrderId))
        {
            var byPublisherOrderId = await _executionRecordStore.GetByResourceIdAsync(request.TargetPublisherOrderId, cancellationToken);
            if (byPublisherOrderId is not null)
            {
                return byPublisherOrderId.ExternalOrderId;
            }
        }

        throw new InvalidOperationException("Cancel command is missing a resolvable external order target.");
    }

    private static Dictionary<string, string?> BuildResultAttributes(
        ApplicationEventEnvelope envelope,
        string actionType,
        int attemptCount,
        string normalizedStatus,
        IReadOnlyDictionary<string, string?>? additionalAttributes = null)
    {
        var attributes = new Dictionary<string, string?>
        {
            ["resultSchemaVersion"] = "weather-quant-result.v1",
            ["sourceEvent"] = envelope.Name,
            ["commandEventId"] = envelope.Id.ToString(),
            ["tradeIntentId"] = GetAttribute(envelope, "tradeIntentId"),
            ["publisherOrderId"] = GetAttribute(envelope, "publisherOrderId") ?? envelope.ResourceId,
            ["actionType"] = actionType,
            ["status"] = normalizedStatus,
            ["attemptCount"] = attemptCount.ToString(CultureInfo.InvariantCulture),
            ["targetPublisherOrderId"] = GetAttribute(envelope, "targetPublisherOrderId"),
            ["targetClientOrderId"] = GetAttribute(envelope, "targetClientOrderId"),
            ["targetExternalOrderId"] = GetAttribute(envelope, "targetExternalOrderId"),
        };

        if (additionalAttributes is not null)
        {
            foreach (var item in additionalAttributes)
            {
                attributes[item.Key] = item.Value;
            }
        }

        return attributes;
    }

    private static string? GetAttribute(ApplicationEventEnvelope envelope, string key)
        => envelope.Attributes.TryGetValue(key, out var value) ? value : null;

    private static int? TryGetInt(ApplicationEventEnvelope envelope, string key)
        => envelope.Attributes.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : null;

    private static decimal? TryGetDecimal(ApplicationEventEnvelope envelope, string key)
        => envelope.Attributes.TryGetValue(key, out var value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
