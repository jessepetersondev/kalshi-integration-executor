namespace Kalshi.Integration.Executor.Persistence;

public sealed class InMemoryDeadLetterRecordStore : IDeadLetterRecordStore
{
    private readonly Dictionary<Guid, DeadLetterRecord> _records = [];
    private readonly object _lock = new();

    public Task AddAsync(DeadLetterRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _records[record.Id] = record;
        }

        return Task.CompletedTask;
    }

    public Task<DeadLetterRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _records.TryGetValue(id, out var record);
            return Task.FromResult(record);
        }
    }

    public Task<IReadOnlyList<DeadLetterRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<DeadLetterRecord>>(
                _records.Values
                    .OrderByDescending(x => x.DeadLetteredAtUtc)
                    .Take(limit)
                    .ToArray());
        }
    }

    public Task MarkReplayedAsync(Guid id, DateTimeOffset replayedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_records.TryGetValue(id, out var record))
            {
                _records[id] = record with
                {
                    LastReplayedAtUtc = replayedAtUtc,
                    ReplayCount = record.ReplayCount + 1,
                };
            }
        }

        return Task.CompletedTask;
    }
}
