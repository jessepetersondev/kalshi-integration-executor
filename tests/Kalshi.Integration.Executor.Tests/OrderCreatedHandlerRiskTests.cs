using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class OrderCreatedHandlerRiskTests
{
    [Fact]
    public async Task HandleAsyncShouldPublishBlockedEventAndSkipKalshiCallWhenRiskGuardBlocks()
    {
        var publisher = new InMemoryResultEventPublisher();
        var consumedStore = new InMemoryConsumedEventStore();
        var executionStore = new InMemoryExecutionRecordStore();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var policy = new ExecutionReliabilityPolicy(Options.Create(new FailureHandlingOptions()), deadLetterPublisher, deadLetterStore);
        var client = new CountingKalshiExecutionClient();
        var riskGuard = new ExecutionRiskGuard(
            Options.Create(new RiskControlsOptions
            {
                LiveExecutionEnabled = true,
                KillSwitchEnabled = true,
            }),
            executionStore);
        var handler = new OrderCreatedHandler(client, publisher, consumedStore, executionStore, riskGuard, policy);

        await handler.HandleAsync(CreateEnvelope());

        var published = Assert.Single(publisher.PublishedEvents);
        Assert.Equal("order.execution_blocked", published.Name);
        Assert.Equal("kill_switch_enabled", published.Attributes["blockCode"]);
        Assert.Equal(0, client.PlaceOrderCallCount);
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
                ["ticker"] = "KXBTC-26MAR2915-B74950",
                ["side"] = "yes",
                ["quantity"] = "1",
                ["limitPrice"] = "0.42",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class RecordingDeadLetterPublisher : IDeadLetterEventPublisher
    {
        public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CountingKalshiExecutionClient : IKalshiExecutionClient
    {
        public int PlaceOrderCallCount { get; private set; }

        public Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
        {
            PlaceOrderCallCount++;
            return Task.FromResult(new KalshiOrderResponse("ext-1", "client-1", request.MarketTicker, request.Side, request.Action, "accepted", "{}"));
        }

        public Task<KalshiOrderResponse> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(new KalshiOrderResponse(externalOrderId, externalOrderId, null, null, "cancel", "canceled", "{}"));

        public Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("status");

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
            => Task.FromResult("market");
    }
}
