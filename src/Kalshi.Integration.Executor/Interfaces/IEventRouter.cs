
namespace Kalshi.Integration.Executor.Routing;
/// <summary>
/// Defines the contract for event router.
/// </summary>


public interface IEventRouter
{
    ExecutorRoutingResult Route(string payload);
}