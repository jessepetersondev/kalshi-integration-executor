namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes i result event.
/// </summary>
public interface IResultEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default);
}