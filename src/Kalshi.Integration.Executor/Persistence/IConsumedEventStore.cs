namespace Kalshi.Integration.Executor.Persistence;

public interface IConsumedEventStore
{
    Task<bool> HasProcessedAsync(string eventKey, CancellationToken cancellationToken = default);
    Task RecordProcessedAsync(string eventKey, string eventName, string? resourceId, CancellationToken cancellationToken = default);
}
