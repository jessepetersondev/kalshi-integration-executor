namespace Kalshi.Integration.Executor.KalshiApi;

public sealed record KalshiOrderSnapshot(
    string OrderId,
    string ClientOrderId,
    string? Ticker,
    string? Side,
    string? Action,
    string? Status,
    string RawBody);