using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

public sealed class OrderCreatedHandler
{
    private readonly IKalshiExecutionClient _kalshiExecutionClient;
    private readonly IResultEventPublisher _resultEventPublisher;
    private readonly IConsumedEventStore _consumedEventStore;
    private readonly ExecutionReliabilityPolicy _executionReliabilityPolicy;

    public OrderCreatedHandler(
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

        var request = new KalshiOrderRequest(
            MarketTicker: envelope.Attributes.TryGetValue("ticker", out var ticker) ? ticker ?? string.Empty : string.Empty,
            Side: envelope.Attributes.TryGetValue("side", out var side) ? side ?? string.Empty : string.Empty,
            Quantity: envelope.Attributes.TryGetValue("quantity", out var quantity) && int.TryParse(quantity, out var parsedQuantity) ? parsedQuantity : 0,
            LimitPrice: envelope.Attributes.TryGetValue("limitPrice", out var limitPrice) && decimal.TryParse(limitPrice, out var parsedLimitPrice) ? parsedLimitPrice : 0m,
            ClientOrderId: envelope.ResourceId ?? envelope.Id.ToString());

        await _executionReliabilityPolicy.ExecuteAsync(
            envelope,
            deadLetterQueue: "kalshi.integration.executor.dlq",
            async token =>
            {
                try
                {
                    var response = await _kalshiExecutionClient.PlaceOrderAsync(request, token);
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
