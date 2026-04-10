using System.Text.Json;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Diagnostics;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Kalshi.Integration.Executor.Routing;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutorEndToEndResilienceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task OrderCreatedFlowShouldPersistSuccessAndSuppressDuplicateAfterRestart()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kalshi-executor-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "executor-e2e.db");
        var envelope = CreateOrderCreatedEnvelope();
        var payload = JsonSerializer.Serialize(envelope, SerializerOptions);

        try
        {
            var initialClient = new StubKalshiExecutionClient();
            var initialHarness = CreateHarness(databasePath, initialClient);
            await initialHarness.DispatchAsync(payload);

            var initialResult = Assert.Single(initialHarness.ResultPublisher.PublishedEvents);
            Assert.Equal("order.execution_succeeded", initialResult.Name);
            Assert.Equal(1, initialClient.PlaceOrderCallCount);
            Assert.Single(await initialHarness.ExecutionRecordStore.ListRecentAsync());
            Assert.Single(await initialHarness.ConsumedEventStore.ListRecentAsync());

            var restartedClient = new StubKalshiExecutionClient();
            var restartedHarness = CreateHarness(databasePath, restartedClient);
            await restartedHarness.DispatchAsync(payload);

            Assert.Empty(restartedHarness.ResultPublisher.PublishedEvents);
            Assert.Equal(0, restartedClient.PlaceOrderCallCount);
            Assert.Single(await restartedHarness.ExecutionRecordStore.ListRecentAsync());
            Assert.Single(await restartedHarness.ConsumedEventStore.ListRecentAsync());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DeadLetterRecordShouldBeReplayableIntoSuccessfulEndToEndFlow()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kalshi-executor-dlq-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "executor-dlq.db");
        var envelope = CreateOrderCreatedEnvelope();
        var payload = JsonSerializer.Serialize(envelope, SerializerOptions);

        try
        {
            var failingHarness = CreateHarness(databasePath, new StubKalshiExecutionClient(httpException: new HttpRequestException("simulated transient outage")));
            await failingHarness.DispatchAsync(payload);

            var failureEvent = Assert.Single(failingHarness.ResultPublisher.PublishedEvents);
            Assert.Equal("order.execution_failed", failureEvent.Name);
            var deadLetterEvent = Assert.Single(failingHarness.DeadLetterPublisher.PublishedEvents);
            Assert.Equal("order.created.dead_lettered", deadLetterEvent.Name);

            var deadLetterRecord = Assert.Single(await failingHarness.DeadLetterRecordStore.ListRecentAsync());
            Assert.Equal("order.created", deadLetterRecord.SourceEventName);
            Assert.Equal(2, deadLetterRecord.AttemptCount);
            Assert.Empty(await failingHarness.ConsumedEventStore.ListRecentAsync());

            var inboundReplayPublisher = new InMemoryInboundEventPublisher();
            var replayService = new DeadLetterReplayService(failingHarness.DeadLetterRecordStore, inboundReplayPublisher);
            await replayService.ReplayAsync(deadLetterRecord.Id);

            var replayedPayload = Assert.Single(inboundReplayPublisher.PublishedPayloads);
            var successfulHarness = CreateHarness(databasePath, new StubKalshiExecutionClient());
            await successfulHarness.DispatchAsync(replayedPayload);

            var successEvent = Assert.Single(successfulHarness.ResultPublisher.PublishedEvents);
            Assert.Equal("order.execution_succeeded", successEvent.Name);
            Assert.Single(await successfulHarness.ExecutionRecordStore.ListRecentAsync());
            Assert.Single(await successfulHarness.ConsumedEventStore.ListRecentAsync());

            var updatedDeadLetterRecord = await successfulHarness.DeadLetterRecordStore.GetByIdAsync(deadLetterRecord.Id);
            Assert.NotNull(updatedDeadLetterRecord);
            Assert.Equal(1, updatedDeadLetterRecord!.ReplayCount);
            Assert.NotNull(updatedDeadLetterRecord.LastReplayedAtUtc);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static TestHarness CreateHarness(string databasePath, StubKalshiExecutionClient client)
    {
        var persistenceOptions = Options.Create(new PersistenceOptions
        {
            ConnectionString = $"Data Source={databasePath}",
        });
        var consumedEventStore = new SqliteConsumedEventStore(persistenceOptions);
        var executionRecordStore = new SqliteExecutionRecordStore(persistenceOptions);
        var deadLetterRecordStore = new SqliteDeadLetterRecordStore(persistenceOptions);
        var resultPublisher = new InMemoryResultEventPublisher();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var riskGuard = new ExecutionRiskGuard(
            Options.Create(new RiskControlsOptions
            {
                LiveExecutionEnabled = true,
                AllowedTickerPrefixes = ["KXBTC-"],
                MaxOrderQuantity = 5,
                MaxOrderNotionalDollars = 5m,
                MaxDailyNotionalDollars = 25m,
            }),
            executionRecordStore);
        var reliabilityPolicy = new ExecutionReliabilityPolicy(
            Options.Create(new FailureHandlingOptions
            {
                MaxRetryAttempts = 1,
                BaseDelayMilliseconds = 1,
            }),
            deadLetterPublisher,
            deadLetterRecordStore);
        var dispatcher = new EventDispatcher(
            new OrderCreatedHandler(client, resultPublisher, consumedEventStore, executionRecordStore, riskGuard, reliabilityPolicy),
            new TradeIntentCreatedHandler(client, resultPublisher, consumedEventStore, reliabilityPolicy),
            new ExecutionUpdateAppliedHandler(client, resultPublisher, consumedEventStore, executionRecordStore, reliabilityPolicy));

        return new TestHarness(new EventRouter(), dispatcher, resultPublisher, deadLetterPublisher, consumedEventStore, executionRecordStore, deadLetterRecordStore);
    }

    private static ApplicationEventEnvelope CreateOrderCreatedEnvelope()
    {
        return new ApplicationEventEnvelope(
            Guid.Parse("4f275cec-a2aa-4d3e-9e8d-4f8d4f3f6217"),
            "trading",
            "order.created",
            "order-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC-26MAR2920-T65899.99",
                ["side"] = "no",
                ["quantity"] = "1",
                ["limitPrice"] = "0.33",
            },
            DateTimeOffset.UtcNow);
    }

    private sealed class TestHarness
    {
        public TestHarness(
            EventRouter router,
            EventDispatcher dispatcher,
            InMemoryResultEventPublisher resultPublisher,
            RecordingDeadLetterPublisher deadLetterPublisher,
            SqliteConsumedEventStore consumedEventStore,
            SqliteExecutionRecordStore executionRecordStore,
            SqliteDeadLetterRecordStore deadLetterRecordStore)
        {
            Router = router;
            Dispatcher = dispatcher;
            ResultPublisher = resultPublisher;
            DeadLetterPublisher = deadLetterPublisher;
            ConsumedEventStore = consumedEventStore;
            ExecutionRecordStore = executionRecordStore;
            DeadLetterRecordStore = deadLetterRecordStore;
        }

        public EventRouter Router { get; }

        public EventDispatcher Dispatcher { get; }

        public InMemoryResultEventPublisher ResultPublisher { get; }

        public RecordingDeadLetterPublisher DeadLetterPublisher { get; }

        public SqliteConsumedEventStore ConsumedEventStore { get; }

        public SqliteExecutionRecordStore ExecutionRecordStore { get; }

        public SqliteDeadLetterRecordStore DeadLetterRecordStore { get; }

        public Task DispatchAsync(string payload)
        {
            var routingResult = Router.Route(payload);
            return Dispatcher.DispatchAsync(routingResult);
        }
    }

    private sealed class RecordingDeadLetterPublisher : IDeadLetterEventPublisher
    {
        public List<ApplicationEventEnvelope> PublishedEvents { get; } = [];

        public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(applicationEvent with
            {
                Attributes = new Dictionary<string, string?>(applicationEvent.Attributes)
                {
                    ["deadLetterQueue"] = deadLetterQueue,
                },
            });
            return Task.CompletedTask;
        }
    }

    private sealed class StubKalshiExecutionClient : IKalshiExecutionClient
    {
        private readonly HttpRequestException? _httpException;

        public StubKalshiExecutionClient(HttpRequestException? httpException = null)
        {
            _httpException = httpException;
        }

        public int PlaceOrderCallCount { get; private set; }

        public Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
        {
            PlaceOrderCallCount++;

            if (_httpException is not null)
            {
                throw _httpException;
            }

            return Task.FromResult(new KalshiOrderResponse(
                "ext-123",
                request.ClientOrderId,
                request.MarketTicker,
                request.Side,
                request.Action,
                "accepted",
                "{\"status\":\"accepted\"}"));
        }

        public Task<KalshiOrderResponse> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(new KalshiOrderResponse(externalOrderId, externalOrderId, null, null, "cancel", "canceled", "{}"));

        public Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"order\":{\"order_id\":\"ext-123\",\"client_order_id\":\"client-1\",\"ticker\":\"KXBTC\",\"side\":\"no\",\"action\":\"buy\",\"status\":\"filled\"}}");

        public Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
            => Task.FromResult($"{{\"ticker\":\"{marketTicker}\"}}");
    }
}
