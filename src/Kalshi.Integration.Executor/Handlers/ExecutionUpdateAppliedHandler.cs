using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

public sealed class ExecutionUpdateAppliedHandler
{
    private readonly IKalshiExecutionClient _kalshiExecutionClient;
    private readonly IResultEventPublisher _resultEventPublisher;
    private readonly IConsumedEventStore _consumedEventStore;

    public ExecutionUpdateAppliedHandler(
        IKalshiExecutionClient kalshiExecutionClient,
        IResultEventPublisher resultEventPublisher,
        IConsumedEventStore consumedEventStore)
    {
        _kalshiExecutionClient = kalshiExecutionClient;
        _resultEventPublisher = resultEventPublisher;
        _consumedEventStore = consumedEventStore;
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

        try
        {
            var status = await _kalshiExecutionClient.GetOrderStatusAsync(externalOrderId, cancellationToken);
            var successEvent = new ApplicationEventEnvelope(
                Guid.NewGuid(),
                "executor",
                "execution-update.reconciled",
                envelope.ResourceId,
                envelope.CorrelationId,
                envelope.IdempotencyKey,
                new Dictionary<string, string?>
                {
                    ["externalOrderId"] = externalOrderId,
                    ["status"] = status,
                    ["sourceEvent"] = envelope.Name,
                },
                DateTimeOffset.UtcNow);

            await _resultEventPublisher.PublishAsync(successEvent, cancellationToken);
            await _consumedEventStore.RecordProcessedAsync(eventKey, envelope.Name, envelope.ResourceId, cancellationToken);
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

            await _resultEventPublisher.PublishAsync(failureEvent, cancellationToken);
        }
    }
}
