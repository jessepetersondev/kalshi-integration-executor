using Kalshi.Integration.Executor.KalshiApi;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Defines the outbound Kalshi order-management operations required by the executor.
/// </summary>
public interface IKalshiExecutionClient
{
    Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default);
    Task<KalshiOrderResponse> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default);
}
