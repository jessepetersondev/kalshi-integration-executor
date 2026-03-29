namespace Kalshi.Integration.Executor.KalshiApi;

public sealed record KalshiOrderRequest(
    string MarketTicker,
    string Side,
    int Quantity,
    decimal LimitPrice,
    string ClientOrderId,
    string Action = "buy",
    bool ReduceOnly = false);
