using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutionUpdateAppliedHandlerTests
{
    [Fact]
    public async Task HandleAsyncShouldPublishReconciledEventWhenStatusLookupSucceeds()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient("filled", null);
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, store);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("execution-update.reconciled", published.Name);
        Assert.Equal("filled", published.Attributes["status"]);
    }

    [Fact]
    public async Task HandleAsyncShouldPublishFailureEventWhenStatusLookupFails()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient(null, new InvalidOperationException("status failed"));
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, store);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("execution-update.reconciliation_failed", published.Name);
        Assert.Equal("InvalidOperationException", published.Attributes["errorType"]);
    }

    [Fact]
    public async Task HandleAsyncShouldNotReprocessDuplicateExecutionUpdateEvent()
    {
        var publisher = new InMemoryResultEventPublisher();
        var store = new InMemoryConsumedEventStore();
        var client = new StubKalshiExecutionClient("filled", null);
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, store);

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
            "execution-update.applied",
            "order-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["externalOrderId"] = "ext-123",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class StubKalshiExecutionClient : IKalshiExecutionClient
    {
        private readonly string? _orderStatus;
        private readonly Exception? _exception;

        public StubKalshiExecutionClient(string? orderStatus, Exception? exception)
        {
            _orderStatus = orderStatus;
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

            return Task.FromResult(_orderStatus ?? "pending");
        }

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
            => Task.FromResult("market");
    }
}
