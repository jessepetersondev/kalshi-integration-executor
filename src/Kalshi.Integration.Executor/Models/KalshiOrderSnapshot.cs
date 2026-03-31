namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Represents a snapshot of kalshi order state.
/// </summary>
public sealed record KalshiOrderSnapshot(
    string OrderId,
    string ClientOrderId,
    string? Ticker,
    string? Side,
    string? Action,
    string? Status,
    string RawBody);