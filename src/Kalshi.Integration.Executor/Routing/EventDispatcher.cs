using Kalshi.Integration.Executor.Handlers;

namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Dispatches routed application events to the concrete executor handler responsible for each route.
/// </summary>
public sealed class EventDispatcher : IEventDispatcher
{
    private readonly OrderCreatedHandler _orderCreatedHandler;
    private readonly TradeIntentCreatedHandler _tradeIntentCreatedHandler;
    private readonly ExecutionUpdateAppliedHandler _executionUpdateAppliedHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventDispatcher"/> class.
    /// </summary>
    /// <param name="orderCreatedHandler">The handler for order-created events.</param>
    /// <param name="tradeIntentCreatedHandler">The handler for trade-intent-created events.</param>
    /// <param name="executionUpdateAppliedHandler">The handler for execution-update-applied events.</param>
    public EventDispatcher(
        OrderCreatedHandler orderCreatedHandler,
        TradeIntentCreatedHandler tradeIntentCreatedHandler,
        ExecutionUpdateAppliedHandler executionUpdateAppliedHandler)
    {
        _orderCreatedHandler = orderCreatedHandler;
        _tradeIntentCreatedHandler = tradeIntentCreatedHandler;
        _executionUpdateAppliedHandler = executionUpdateAppliedHandler;
    }

    public Task DispatchAsync(ExecutorRoutingResult routingResult, CancellationToken cancellationToken = default)
    {
        return routingResult.Route switch
        {
            ExecutorRoute.OrderCreated => _orderCreatedHandler.HandleAsync(routingResult.Envelope, cancellationToken),
            ExecutorRoute.TradeIntentCreated => _tradeIntentCreatedHandler.HandleAsync(routingResult.Envelope, cancellationToken),
            ExecutorRoute.ExecutionUpdateApplied => _executionUpdateAppliedHandler.HandleAsync(routingResult.Envelope, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported route '{routingResult.Route}'."),
        };
    }
}
