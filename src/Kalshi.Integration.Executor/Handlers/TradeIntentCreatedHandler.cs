using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Handlers;

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

        var ticker = envelope.Attributes.TryGetValue("ticker", out var tickerValue) ? tickerValue ?? string.Empty : string.Empty;

        await _executionReliabilityPolicy.ExecuteAsync(
            envelope,
            deadLetterQueue: "kalshi.integration.executor.dlq",
            async token =>
            {
                try
                {
                    var market = await _kalshiExecutionClient.GetMarketAsync(ticker, token);
                    var successEvent = new ApplicationEventEnvelope(
                        Guid.NewGuid(),
                        "executor",
                        "trade-intent.executed",
                        envelope.ResourceId,
                        envelope.CorrelationId,
                        envelope.IdempotencyKey,
                        new Dictionary<string, string?>
                        {
                            ["ticker"] = ticker,
                            ["market"] = market,
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
                        "trade-intent.failed",
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
