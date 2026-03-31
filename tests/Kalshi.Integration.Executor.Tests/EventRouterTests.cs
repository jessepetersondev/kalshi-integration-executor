using System.Text.Json;
using Kalshi.Integration.Executor.Routing;

namespace Kalshi.Integration.Executor.Tests;

public sealed class EventRouterTests
{
    private readonly EventRouter _router = new();

    [Fact]
    public void RouteShouldMapOrderCreatedEvent()
    {
        var payload = CreatePayload("order.created");

        var result = _router.Route(payload);

        Assert.Equal(ExecutorRoute.OrderCreated, result.Route);
        Assert.Equal("order.created", result.Envelope.Name);
        Assert.Equal("trading", result.Envelope.Category);
    }

    [Fact]
    public void RouteShouldMapTradeIntentCreatedEvent()
    {
        var payload = CreatePayload("trade-intent.created");

        var result = _router.Route(payload);

        Assert.Equal(ExecutorRoute.TradeIntentCreated, result.Route);
    }

    [Fact]
    public void RouteShouldMapExecutionUpdateAppliedEvent()
    {
        var payload = CreatePayload("execution-update.applied");

        var result = _router.Route(payload);

        Assert.Equal(ExecutorRoute.ExecutionUpdateApplied, result.Route);
    }

    [Fact]
    public void RouteShouldThrowUnsupportedEventExceptionForUnsupportedEventName()
    {
        var payload = CreatePayload("position.closed");

        var exception = Assert.Throws<UnsupportedEventException>(() => _router.Route(payload));

        Assert.Equal("position.closed", exception.EventName);
    }

    [Fact]
    public void RouteShouldThrowArgumentExceptionForEmptyPayload()
    {
        Assert.Throws<ArgumentException>(() => _router.Route(string.Empty));
    }

    private static string CreatePayload(string eventName)
    {
        return JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            category = "trading",
            name = eventName,
            resourceId = "resource-1",
            correlationId = "corr-1",
            idempotencyKey = "idem-1",
            attributes = new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC",
            },
            occurredAt = DateTimeOffset.UtcNow,
        });
    }
}