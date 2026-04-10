namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Represents a recorded consumed event entry.
/// </summary>
public sealed record ConsumedEventRecord(string EventKey, string EventName, string? ResourceId, DateTimeOffset RecordedAt);
