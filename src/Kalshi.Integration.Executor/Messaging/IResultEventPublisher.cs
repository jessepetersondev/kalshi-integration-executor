namespace Kalshi.Integration.Executor.Messaging;

public interface IResultEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default);
}
