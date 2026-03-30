using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Persistence;
using Kalshi.Integration.Executor.Messaging;



namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutionUpdateAppliedHandlerTests
{
    [Fact]
    public async Task HandleAsyncShouldPublishReconciledEventWhenStatusLookupSucceeds()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher, deadLetterStore);
        var client = new StubKalshiExecutionClient("{\"order\":{\"order_id\":\"ext-123\",\"client_order_id\":\"client-123\",\"ticker\":\"KXBTC\",\"side\":\"yes\",\"action\":\"buy\",\"status\":\"filled\"}}", null);
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("execution-update.reconciled", published.Name);
        Assert.Equal("filled", published.Attributes["status"]);
        Assert.Equal("client-123", published.Attributes["clientOrderId"]);
        var record = await executionStore.GetByExternalOrderIdAsync("ext-123");
        Assert.NotNull(record);
        Assert.Equal("filled", record!.Status);
        Assert.Empty(deadLetterPublisher.PublishedEvents);
    }

    [Fact]
    public async Task HandleAsyncShouldPublishFailureEventWhenStatusLookupFails()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher, deadLetterStore);
        var client = new StubKalshiExecutionClient(null, new InvalidOperationException("status failed"));
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("execution-update.reconciliation_failed", published.Name);
        Assert.Equal("InvalidOperationException", published.Attributes["errorType"]);
        var record = await executionStore.GetByExternalOrderIdAsync("ext-123");
        Assert.Null(record);
    }

    [Fact]
    public async Task HandleAsyncShouldNotReprocessDuplicateExecutionUpdateEvent()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher, deadLetterStore);
        var client = new StubKalshiExecutionClient("{\"order\":{\"order_id\":\"ext-123\",\"client_order_id\":\"client-123\",\"ticker\":\"KXBTC\",\"side\":\"yes\",\"action\":\"buy\",\"status\":\"filled\"}}", null);
        var handler = new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy);

        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Single(publisher.PublishedEvents);
        Assert.Single(consumedStore.Records);
        var records = await executionStore.ListRecentAsync();
        Assert.Single(records);
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

    private sealed class RecordingDeadLetterPublisher : IDeadLetterEventPublisher
    {
        public List<ApplicationEventEnvelope> PublishedEvents { get; } = [];

        public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(applicationEvent);
            return Task.CompletedTask;
        }
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
            => Task.FromResult(new KalshiOrderResponse("ext-1", "client-1", "KXBTC", "yes", "buy", "accepted", "{}"));

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
