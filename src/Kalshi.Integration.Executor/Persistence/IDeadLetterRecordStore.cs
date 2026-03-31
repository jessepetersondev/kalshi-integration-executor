namespace Kalshi.Integration.Executor.Persistence;
/// <summary>
/// Provides storage operations for i dead letter record.
/// </summary>


public interface IDeadLetterRecordStore
{
    Task AddAsync(DeadLetterRecord record, CancellationToken cancellationToken = default);
    Task<DeadLetterRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task MarkReplayedAsync(Guid id, DateTimeOffset replayedAtUtc, CancellationToken cancellationToken = default);
}
