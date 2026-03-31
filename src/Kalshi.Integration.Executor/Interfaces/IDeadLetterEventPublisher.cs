namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes i dead letter event.
/// </summary>


public interface IDeadLetterEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default);
}