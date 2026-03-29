
namespace Kalshi.Integration.Executor.Routing;

public interface IEventDispatcher
{
    Task DispatchAsync(ExecutorRoutingResult routingResult, CancellationToken cancellationToken = default);
}