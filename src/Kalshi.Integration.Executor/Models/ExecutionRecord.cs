namespace Kalshi.Integration.Executor.Persistence;

public sealed record ExecutionRecord(
    string ExternalOrderId,
    string ClientOrderId,
    string? ResourceId,
    string? CorrelationId,
    string? Ticker,
    string? Side,
    string? Action,
    string? Status,
    int? Quantity,
    decimal? LimitPriceDollars,
    decimal? NotionalDollars,
    string RawResponse,
    DateTimeOffset RecordedAtUtc);