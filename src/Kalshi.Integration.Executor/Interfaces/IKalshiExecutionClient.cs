using Kalshi.Integration.Executor.KalshiApi;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Provides access to i kalshi execution.
/// </summary>


public interface IKalshiExecutionClient
{
    Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default);
    Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default);
}