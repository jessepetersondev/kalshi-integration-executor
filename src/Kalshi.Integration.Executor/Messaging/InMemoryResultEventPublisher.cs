
namespace Kalshi.Integration.Executor.Messaging;
/// <summary>
/// Publishes in memory result event.
/// </summary>


public sealed class InMemoryResultEventPublisher : IResultEventPublisher
{
    private readonly List<ApplicationEventEnvelope> _publishedEvents = [];

    public IReadOnlyList<ApplicationEventEnvelope> PublishedEvents => _publishedEvents;

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _publishedEvents.Add(applicationEvent);
        return Task.CompletedTask;
    }
}