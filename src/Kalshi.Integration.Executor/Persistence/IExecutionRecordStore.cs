namespace Kalshi.Integration.Executor.Persistence;

public interface IExecutionRecordStore
{
    Task UpsertAsync(ExecutionRecord record, CancellationToken cancellationToken = default);
    Task<ExecutionRecord?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
}
