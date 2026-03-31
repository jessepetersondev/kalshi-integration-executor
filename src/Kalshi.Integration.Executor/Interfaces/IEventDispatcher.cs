namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Defines the contract for event dispatcher.
/// </summary>


public interface IEventDispatcher
{
    Task DispatchAsync(ExecutorRoutingResult routingResult, CancellationToken cancellationToken = default);
}