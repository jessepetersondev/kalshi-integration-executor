namespace Kalshi.Integration.Executor.Messaging;

public interface IInboundEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default);
}
