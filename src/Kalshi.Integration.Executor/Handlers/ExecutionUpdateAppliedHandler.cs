using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

/// <summary>
/// Handles execution update applied events.
/// </summary>
public sealed class ExecutionUpdateAppliedHandler
{
    private readonly IKalshiExecutionClient _kalshiExecutionClient;
    private readonly IResultEventPublisher _resultEventPublisher;
    private readonly IConsumedEventStore _consumedEventStore;
    private readonly IExecutionRecordStore _executionRecordStore;
    private readonly ExecutionReliabilityPolicy _executionReliabilityPolicy;

    public ExecutionUpdateAppliedHandler(
        IKalshiExecutionClient kalshiExecutionClient,
        IResultEventPublisher resultEventPublisher,
        IConsumedEventStore consumedEventStore,
        IExecutionRecordStore executionRecordStore,
        ExecutionReliabilityPolicy executionReliabilityPolicy)
    {
        _kalshiExecutionClient = kalshiExecutionClient;
        _resultEventPublisher = resultEventPublisher;
        _consumedEventStore = consumedEventStore;
        _executionRecordStore = executionRecordStore;
        _executionReliabilityPolicy = executionReliabilityPolicy;
    }

    public async Task HandleAsync(ApplicationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var eventKey = envelope.IdempotencyKey ?? envelope.Id.ToString();
        if (await _consumedEventStore.HasProcessedAsync(eventKey, cancellationToken))
        {
            return;
        }

        var externalOrderId = envelope.Attributes.TryGetValue("externalOrderId", out var externalOrderValue)
            ? externalOrderValue ?? envelope.ResourceId ?? string.Empty
            : envelope.ResourceId ?? string.Empty;

        await _executionReliabilityPolicy.ExecuteAsync(
            envelope,
            deadLetterQueue: "kalshi.integration.executor.dlq",
            async (_attemptCount, token) =>
            {
                try
                {
                    var rawStatus = await _kalshiExecutionClient.GetOrderStatusAsync(externalOrderId, token);
                    var snapshot = KalshiOrderResponseParser.Parse(rawStatus, envelope.ResourceId ?? externalOrderId);

                    var existingRecord = await _executionRecordStore.GetByExternalOrderIdAsync(snapshot.OrderId, token);
                    await _executionRecordStore.UpsertAsync(
                        new ExecutionRecord(
                            snapshot.OrderId,
                            snapshot.ClientOrderId,
                            envelope.ResourceId,
                            envelope.CorrelationId,
                            envelope.Id.ToString(),
                            envelope.Attributes.TryGetValue("actionType", out var actionType) ? actionType : null,
                            envelope.Attributes.TryGetValue("tradeIntentId", out var tradeIntentId) ? tradeIntentId : null,
                            envelope.Attributes.TryGetValue("publisherOrderId", out var publisherOrderId) ? publisherOrderId : envelope.ResourceId,
                            snapshot.Ticker,
                            snapshot.Side,
                            snapshot.Action,
                            envelope.Attributes.TryGetValue("targetPublisherOrderId", out var targetPublisherOrderId) ? targetPublisherOrderId : null,
                            envelope.Attributes.TryGetValue("targetClientOrderId", out var targetClientOrderId) ? targetClientOrderId : null,
                            envelope.Attributes.TryGetValue("targetExternalOrderId", out var targetExternalOrderId) ? targetExternalOrderId : null,
                            snapshot.Status,
                            existingRecord?.Quantity,
                            existingRecord?.LimitPriceDollars,
                            existingRecord?.NotionalDollars,
                            snapshot.RawBody,
                            DateTimeOffset.UtcNow),
                        token);

                    var successEvent = new ApplicationEventEnvelope(
                        Guid.NewGuid(),
                        "executor",
                        "execution-update.reconciled",
                        envelope.ResourceId,
                        envelope.CorrelationId,
                        envelope.IdempotencyKey,
                        new Dictionary<string, string?>
                        {
                            ["externalOrderId"] = snapshot.OrderId,
                            ["clientOrderId"] = snapshot.ClientOrderId,
                            ["ticker"] = snapshot.Ticker,
                            ["side"] = snapshot.Side,
                            ["action"] = snapshot.Action,
                            ["status"] = snapshot.Status,
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
                        "execution-update.reconciliation_failed",
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
