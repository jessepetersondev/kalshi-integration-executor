using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Tests;

public sealed class OrderCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsyncShouldPublishSuccessEventWhenKalshiOrderSucceeds()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(
            new KalshiOrderResponse("ext-123", "Accepted", "{\"ok\":true}"),
            null);
        var handler = new OrderCreatedHandler(client, publisher, store);

        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope);

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("order.execution_succeeded", published.Name);
        Assert.Equal("corr-1", published.CorrelationId);
        Assert.Equal("idem-1", published.IdempotencyKey);
        Assert.Equal("ext-123", published.Attributes["externalOrderId"]);
    }

    [Fact]
    public async Task HandleAsyncShouldPublishFailureEventWhenKalshiOrderFails()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(null, new InvalidOperationException("boom"));
        var handler = new OrderCreatedHandler(client, publisher, store);

        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope);

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("order.execution_failed", published.Name);
        Assert.Equal("InvalidOperationException", published.Attributes["errorType"]);
        Assert.Equal("boom", published.Attributes["errorMessage"]);
    }

    [Fact]
    public async Task HandleAsyncShouldNotReprocessDuplicateEvent()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(
            new KalshiOrderResponse("ext-123", "Accepted", "{\"ok\":true}"),
            null);
        var handler = new OrderCreatedHandler(client, publisher, store);

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
            "order.created",
            "order-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC",
                ["side"] = "yes",
                ["quantity"] = "2",
                ["limitPrice"] = "0.42",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class StubKalshiExecutionClient : IKalshiExecutionClient
    {
        private readonly KalshiOrderResponse? _response;
        private readonly Exception? _exception;

        public StubKalshiExecutionClient(KalshiOrderResponse? response, Exception? exception)
        {
            _response = response;
            _exception = exception;
        }

        public Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_response!);
        }

        public Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("cancelled");

        public Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("status");

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
            => Task.FromResult("market");
    }
}
