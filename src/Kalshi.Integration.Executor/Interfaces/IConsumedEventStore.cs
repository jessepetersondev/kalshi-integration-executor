
namespace Kalshi.Integration.Executor.Persistence;
/// <summary>
/// Provides storage operations for i consumed event.
/// </summary>


public interface IConsumedEventStore
{
    Task<bool> HasProcessedAsync(string eventKey, CancellationToken cancellationToken = default);
    Task RecordProcessedAsync(string eventKey, string eventName, string? resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumedEventRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
}