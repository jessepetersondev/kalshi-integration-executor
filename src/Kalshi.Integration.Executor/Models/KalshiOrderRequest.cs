namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Represents a request payload for kalshi order.
/// </summary>
public sealed record KalshiOrderRequest(
    string MarketTicker,
    string Side,
    int Quantity,
    decimal LimitPrice,
    string ClientOrderId,
    string Action = "buy",
    bool ReduceOnly = false,
    string? ActionType = null,
    string? TargetPublisherOrderId = null,
    string? TargetClientOrderId = null,
    string? TargetExternalOrderId = null);
