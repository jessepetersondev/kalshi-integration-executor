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

        var request = new KalshiOrderRequest(
            MarketTicker: envelope.Attributes.TryGetValue("ticker", out var ticker) ? ticker ?? string.Empty : string.Empty,
            Side: envelope.Attributes.TryGetValue("side", out var side) ? side ?? string.Empty : string.Empty,
            Quantity: envelope.Attributes.TryGetValue("quantity", out var quantity) && int.TryParse(quantity, out var parsedQuantity) ? parsedQuantity : 0,
            LimitPrice: envelope.Attributes.TryGetValue("limitPrice", out var limitPrice) && decimal.TryParse(limitPrice, out var parsedLimitPrice) ? parsedLimitPrice : 0m,
            ClientOrderId: envelope.ResourceId ?? envelope.Id.ToString());

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
                new Dictionary<string, string?>
                {
                    ["ticker"] = request.MarketTicker,
                    ["side"] = request.Side,
                    ["quantity"] = request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["limitPrice"] = request.LimitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["blockCode"] = riskDecision.Code,
                    ["blockReason"] = riskDecision.Reason,
                    ["sourceEvent"] = envelope.Name,
                },
                DateTimeOffset.UtcNow);

            await _resultEventPublisher.PublishAsync(blockedEvent, cancellationToken);
            await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, cancellationToken);
            return;
        }

        await _executionReliabilityPolicy.ExecuteAsync(
            envelope,
            deadLetterQueue: "kalshi.integration.executor.dlq",
            async token =>
            {
                try
                {
                    var response = await _kalshiExecutionClient.PlaceOrderAsync(request, token);
                    await _executionRecordStore.UpsertAsync(
                        new ExecutionRecord(
                            response.ExternalOrderId,
                            response.ClientOrderId,
                            envelope.ResourceId,
                            envelope.CorrelationId,
                            response.Ticker ?? request.MarketTicker,
                            response.Side ?? request.Side,
                            response.Action ?? request.Action,
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
                        new Dictionary<string, string?>
                        {
                            ["externalOrderId"] = response.ExternalOrderId,
                            ["clientOrderId"] = response.ClientOrderId,
                            ["ticker"] = response.Ticker ?? request.MarketTicker,
                            ["side"] = response.Side ?? request.Side,
                            ["action"] = response.Action ?? request.Action,
                            ["status"] = response.Status,
                            ["sourceEvent"] = envelope.Name,
                        },
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
                        new Dictionary<string, string?>
                        {
                            ["errorType"] = exception.GetType().Name,
                            ["errorMessage"] = exception.Message,
                            ["sourceEvent"] = envelope.Name,
                        },
                        DateTimeOffset.UtcNow);

                    await _resultEventPublisher.PublishAsync(failureEvent, token);
                }
            },
            cancellationToken);
    }
}