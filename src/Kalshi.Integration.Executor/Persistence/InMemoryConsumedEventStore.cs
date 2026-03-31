namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Provides storage operations for in memory consumed event.
/// </summary>


public sealed class InMemoryConsumedEventStore : IConsumedEventStore
{
    private readonly HashSet<string> _processedKeys = [];
    private readonly List<ConsumedEventRecord> _records = [];
    private readonly object _lock = new();

    public IReadOnlyList<ConsumedEventRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToArray();
            }
        }
    }

    public Task<bool> HasProcessedAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(_processedKeys.Contains(eventKey));
        }
    }

    public Task RecordProcessedAsync(string eventKey, string eventName, string? resourceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _processedKeys.Add(eventKey);
            _records.Add(new ConsumedEventRecord(eventKey, eventName, resourceId, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConsumedEventRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConsumedEventRecord>>(_records.TakeLast(limit).ToArray());
        }
    }
}