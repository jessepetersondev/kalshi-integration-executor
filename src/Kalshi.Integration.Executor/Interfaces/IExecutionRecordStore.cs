namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Persists execution outcomes for diagnostics and risk evaluation.
/// </summary>
public interface IExecutionRecordStore
{
    Task UpsertAsync(ExecutionRecord record, CancellationToken cancellationToken = default);
    Task<ExecutionRecord?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
}