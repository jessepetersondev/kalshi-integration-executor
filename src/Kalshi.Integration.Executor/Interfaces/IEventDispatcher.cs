namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Dispatches a routed envelope to the handler responsible for that executor route.
/// </summary>
public interface IEventDispatcher
{
    Task DispatchAsync(ExecutorRoutingResult routingResult, CancellationToken cancellationToken = default);
}
