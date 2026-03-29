namespace Kalshi.Integration.Executor.Persistence;

public sealed record ConsumedEventRecord(string EventKey, string EventName, string? ResourceId, DateTimeOffset RecordedAt);
