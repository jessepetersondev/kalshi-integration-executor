namespace Kalshi.Integration.Executor.KalshiApi;

public sealed record KalshiOrderResponse(
    string ExternalOrderId,
    string ClientOrderId,
    string? Ticker,
    string? Side,
    string? Action,
    string? Status,
    string RawBody);