namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Defines the supported routing targets for executor.
/// </summary>


public enum ExecutorRoute
{
    TradeIntentCreated,
    OrderCreated,
    ExecutionUpdateApplied,
}