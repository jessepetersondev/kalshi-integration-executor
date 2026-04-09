namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Provides storage operations for in memory execution record.
/// </summary>
public sealed class InMemoryExecutionRecordStore : IExecutionRecordStore
{
    private readonly Dictionary<string, ExecutionRecord> _records = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public Task UpsertAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _records[record.ExternalOrderId] = record;
        }

        return Task.CompletedTask;
    }

    public Task<ExecutionRecord?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _records.TryGetValue(externalOrderId, out var record);
            return Task.FromResult(record);
        }
    }

    public Task<ExecutionRecord?> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(x => string.Equals(x.ResourceId, resourceId, StringComparison.Ordinal)));
        }
    }

    public Task<ExecutionRecord?> GetByClientOrderIdAsync(string clientOrderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(x => string.Equals(x.ClientOrderId, clientOrderId, StringComparison.Ordinal)));
        }
    }

    public Task<IReadOnlyList<ExecutionRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ExecutionRecord>>(_records.Values.OrderByDescending(x => x.RecordedAtUtc).Take(limit).ToArray());
        }
    }
}
