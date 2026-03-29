using System.Text.Json;
using Kalshi.Integration.Executor.Messaging;

namespace Kalshi.Integration.Executor.Routing;

public sealed class EventRouter : IEventRouter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public ExecutorRoutingResult Route(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var envelope = JsonSerializer.Deserialize<ApplicationEventEnvelope>(payload, SerializerOptions)
            ?? throw new InvalidOperationException("Payload could not be deserialized into ApplicationEventEnvelope.");

        var route = envelope.Name switch
        {
            "trade-intent.created" => ExecutorRoute.TradeIntentCreated,
            "order.created" => ExecutorRoute.OrderCreated,
            "execution-update.applied" => ExecutorRoute.ExecutionUpdateApplied,
            _ => throw new UnsupportedEventException(envelope.Name),
        };

        return new ExecutorRoutingResult(route, envelope);
    }
}
