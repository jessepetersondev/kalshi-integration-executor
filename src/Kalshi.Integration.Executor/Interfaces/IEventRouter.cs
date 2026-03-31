namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Maps an inbound application event envelope to the executor route that should handle it.
/// </summary>
public interface IEventRouter
{
    ExecutorRoutingResult Route(string payload);
}