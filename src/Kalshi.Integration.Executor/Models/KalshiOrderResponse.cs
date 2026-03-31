namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Represents a response payload for kalshi order.
/// </summary>
public sealed record KalshiOrderResponse(
    string ExternalOrderId,
    string ClientOrderId,
    string? Ticker,
    string? Side,
    string? Action,
    string? Status,
    string RawBody);