namespace Kalshi.Integration.Executor.KalshiApi;

public sealed record KalshiOrderResponse(
    string ExternalOrderId,
    string Status,
    string RawBody);
