namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Represents a recorded dead letter entry.
/// </summary>


public sealed record DeadLetterRecord(
    Guid Id,
    Guid SourceEventId,
    string SourceCategory,
    string SourceEventName,
    string? ResourceId,
    string? CorrelationId,
    string? IdempotencyKey,
    string DeadLetterQueue,
    int AttemptCount,
    string? ErrorType,
    string? ErrorMessage,
    string OriginalPayload,
    DateTimeOffset DeadLetteredAtUtc,
    DateTimeOffset? LastReplayedAtUtc,
    int ReplayCount);
