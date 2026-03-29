using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Tests;

public sealed class TradeIntentCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsyncShouldPublishExecutedEventWhenMarketLookupSucceeds()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(marketResponse: "{\"ticker\":\"KXBTC\"}", orderStatusResponse: null, exception: null);
        var handler = new TradeIntentCreatedHandler(client, publisher, store);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("trade-intent.executed", published.Name);
        Assert.Equal("corr-1", published.CorrelationId);
        Assert.Contains("KXBTC", published.Attributes["market"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsyncShouldPublishFailedEventWhenMarketLookupFails()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(null, null, new InvalidOperationException("lookup failed"));
        var handler = new TradeIntentCreatedHandler(client, publisher, store);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("trade-intent.failed", published.Name);
        Assert.Equal("InvalidOperationException", published.Attributes["errorType"]);
    }

    [Fact]
    public async Task HandleAsyncShouldNotReprocessDuplicateTradeIntentEvent()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(marketResponse: "{\"ticker\":\"KXBTC\"}", orderStatusResponse: null, exception: null);
        var handler = new TradeIntentCreatedHandler(client, publisher, store);

        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Single(publisher.PublishedEvents);
        Assert.Single(store.Records);
    }

    private static ApplicationEventEnvelope CreateEnvelope()
    {
        return new ApplicationEventEnvelope(
            Guid.NewGuid(),
            "trading",
            "trade-intent.created",
            "trade-intent-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class StubKalshiExecutionClient : IKalshiExecutionClient
    {
        private readonly string? _marketResponse;
        private readonly string? _orderStatusResponse;
        private readonly Exception? _exception;

        public StubKalshiExecutionClient(string? marketResponse, string? orderStatusResponse, Exception? exception)
        {
            _marketResponse = marketResponse;
            _orderStatusResponse = orderStatusResponse;
            _exception = exception;
        }

        public Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new KalshiOrderResponse("ext-1", "Accepted", "{}"));

        public Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("cancelled");

        public Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_orderStatusResponse ?? "status");
        }

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_marketResponse ?? "market");
        }
    }
}
