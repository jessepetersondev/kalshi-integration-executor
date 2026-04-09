namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Represents a recorded execution entry.
/// </summary>
public sealed record ExecutionRecord(
    string ExternalOrderId,
    string ClientOrderId,
    string? ResourceId,
    string? CorrelationId,
    string? CommandEventId,
    string? ActionType,
    string? TradeIntentId,
    string? PublisherOrderId,
    string? Ticker,
    string? Side,
    string? Action,
    string? TargetPublisherOrderId,
    string? TargetClientOrderId,
    string? TargetExternalOrderId,
    string? Status,
    int? Quantity,
    decimal? LimitPriceDollars,
    decimal? NotionalDollars,
    string RawResponse,
    DateTimeOffset RecordedAtUtc)
{
    public ExecutionRecord(
        string externalOrderId,
        string clientOrderId,
        string? resourceId,
        string? correlationId,
        string? ticker,
        string? side,
        string? action,
        string? status,
        int? quantity,
        decimal? limitPriceDollars,
        decimal? notionalDollars,
        string rawResponse,
        DateTimeOffset recordedAtUtc)
        : this(
            externalOrderId,
            clientOrderId,
            resourceId,
            correlationId,
            null,
            null,
            null,
            resourceId,
            ticker,
            side,
            action,
            null,
            null,
            null,
            status,
            quantity,
            limitPriceDollars,
            notionalDollars,
            rawResponse,
            recordedAtUtc)
    {
    }
}
