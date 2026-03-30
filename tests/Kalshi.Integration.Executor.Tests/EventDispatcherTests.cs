using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Kalshi.Integration.Executor.Routing;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class EventDispatcherTests
{
    [Fact]
    public async Task DispatchAsyncShouldInvokeOrderCreatedHandler()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var riskGuard = new ExecutionRiskGuard(Options.Create(new RiskControlsOptions { LiveExecutionEnabled = true }), executionStore);
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher);
        var client = new StubKalshiExecutionClient();
        var dispatcher = new EventDispatcher(
            new OrderCreatedHandler(client, publisher, consumedStore, executionStore, riskGuard, policy),
            new TradeIntentCreatedHandler(client, publisher, consumedStore, policy),
            new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy));

        await dispatcher.DispatchAsync(new ExecutorRoutingResult(ExecutorRoute.OrderCreated, CreateEnvelope("order.created")));

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("order.execution_succeeded", published.Name);
    }

    [Fact]
    public async Task DispatchAsyncShouldInvokeTradeIntentHandler()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var riskGuard = new ExecutionRiskGuard(Options.Create(new RiskControlsOptions { LiveExecutionEnabled = true }), executionStore);
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher);
        var client = new StubKalshiExecutionClient();
        var dispatcher = new EventDispatcher(
            new OrderCreatedHandler(client, publisher, consumedStore, executionStore, riskGuard, policy),
            new TradeIntentCreatedHandler(client, publisher, consumedStore, policy),
            new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy));

        await dispatcher.DispatchAsync(new ExecutorRoutingResult(ExecutorRoute.TradeIntentCreated, CreateEnvelope("trade-intent.created")));

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("trade-intent.executed", published.Name);
    }

    [Fact]
    public async Task DispatchAsyncShouldInvokeExecutionUpdateHandler()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var riskGuard = new ExecutionRiskGuard(Options.Create(new RiskControlsOptions { LiveExecutionEnabled = true }), executionStore);
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher);
        var client = new StubKalshiExecutionClient();
        var dispatcher = new EventDispatcher(
            new OrderCreatedHandler(client, publisher, consumedStore, executionStore, riskGuard, policy),
            new TradeIntentCreatedHandler(client, publisher, consumedStore, policy),
            new ExecutionUpdateAppliedHandler(client, publisher, consumedStore, executionStore, policy));

        await dispatcher.DispatchAsync(new ExecutorRoutingResult(ExecutorRoute.ExecutionUpdateApplied, CreateEnvelope("execution-update.applied")));

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("execution-update.reconciled", published.Name);
    }

    private static ApplicationEventEnvelope CreateEnvelope(string eventName)
    {
        return new ApplicationEventEnvelope(
            Guid.NewGuid(),
            "trading",
            eventName,
            "resource-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC",
                ["side"] = "yes",
                ["quantity"] = "2",
                ["limitPrice"] = "0.42",
                ["externalOrderId"] = "ext-123",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class RecordingDeadLetterPublisher : IDeadLetterEventPublisher
    {
        public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubKalshiExecutionClient : IKalshiExecutionClient
    {
        public Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new KalshiOrderResponse("ext-123", "client-123", "KXBTC", "yes", "buy", "accepted", "{}"));

        public Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("cancelled");

        public Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"order\":{\"order_id\":\"ext-123\",\"client_order_id\":\"client-123\",\"ticker\":\"KXBTC\",\"side\":\"yes\",\"action\":\"buy\",\"status\":\"filled\"}}");

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"ticker\":\"KXBTC\"}");
    }
}
