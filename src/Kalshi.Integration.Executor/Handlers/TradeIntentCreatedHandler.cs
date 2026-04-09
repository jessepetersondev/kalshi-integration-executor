using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

/// <summary>
/// Handles trade intent created events.
/// </summary>
public sealed class TradeIntentCreatedHandler
{
    private readonly IKalshiExecutionClient _kalshiExecutionClient;
    private readonly IResultEventPublisher _resultEventPublisher;
    private readonly IConsumedEventStore _consumedEventStore;
    private readonly ExecutionReliabilityPolicy _executionReliabilityPolicy;

    public TradeIntentCreatedHandler(
        IKalshiExecutionClient kalshiExecutionClient,
        IResultEventPublisher resultEventPublisher,
        IConsumedEventStore consumedEventStore,
        ExecutionReliabilityPolicy executionReliabilityPolicy)
    {
        _kalshiExecutionClient = kalshiExecutionClient;
        _resultEventPublisher = resultEventPublisher;
        _consumedEventStore = consumedEventStore;
        _executionReliabilityPolicy = executionReliabilityPolicy;
    }

    public async Task HandleAsync(ApplicationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var eventKey = envelope.IdempotencyKey ?? envelope.Id.ToString();
        if (await _consumedEventStore.HasProcessedAsync(eventKey, cancellationToken))
        {
            return;
        }
        await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, cancellationToken);
    }
}
