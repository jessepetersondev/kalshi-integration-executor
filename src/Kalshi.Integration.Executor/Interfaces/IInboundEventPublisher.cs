namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes i inbound event.
/// </summary>
public interface IInboundEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default);
}
