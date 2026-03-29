
namespace Kalshi.Integration.Executor.Messaging;

public interface IDeadLetterEventPublisher
{
    Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default);
}