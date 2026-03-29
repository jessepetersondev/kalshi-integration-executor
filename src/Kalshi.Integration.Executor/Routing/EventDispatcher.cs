using Kalshi.Integration.Executor.Handlers;

namespace Kalshi.Integration.Executor.Routing;

public sealed class EventDispatcher : IEventDispatcher
{
    private readonly OrderCreatedHandler _orderCreatedHandler;
    private readonly TradeIntentCreatedHandler _tradeIntentCreatedHandler;
    private readonly ExecutionUpdateAppliedHandler _executionUpdateAppliedHandler;

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
