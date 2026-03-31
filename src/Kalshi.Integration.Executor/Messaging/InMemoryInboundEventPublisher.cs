using System.Text.Json;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes in memory inbound event.
/// </summary>


public sealed class InMemoryInboundEventPublisher : IInboundEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public List<ApplicationEventEnvelope> PublishedEvents { get; } = [];

    public List<string> PublishedPayloads { get; } = [];

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PublishedEvents.Add(applicationEvent);
        PublishedPayloads.Add(JsonSerializer.Serialize(applicationEvent, SerializerOptions));
        return Task.CompletedTask;
    }
}
