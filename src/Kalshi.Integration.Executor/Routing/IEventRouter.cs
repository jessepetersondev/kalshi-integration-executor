namespace Kalshi.Integration.Executor.Routing;

public interface IEventRouter
{
    ExecutorRoutingResult Route(string payload);
}
