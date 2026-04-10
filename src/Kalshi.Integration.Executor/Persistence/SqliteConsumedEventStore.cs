using Kalshi.Integration.Executor.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Provides storage operations for sqlite consumed event.
/// </summary>
public sealed class SqliteConsumedEventStore : IConsumedEventStore
{
    private readonly string _connectionString;

    public SqliteConsumedEventStore(IOptions<PersistenceOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
        EnsureDatabase();
    }

    public async Task<bool> HasProcessedAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM consumed_events WHERE event_key = $eventKey LIMIT 1;";
        command.Parameters.AddWithValue("$eventKey", eventKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public async Task RecordProcessedAsync(string eventKey, string eventName, string? resourceId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR IGNORE INTO consumed_events (event_key, event_name, resource_id, recorded_at_utc)
VALUES ($eventKey, $eventName, $resourceId, $recordedAtUtc);";
        command.Parameters.AddWithValue("$eventKey", eventKey);
        command.Parameters.AddWithValue("$eventName", eventName);
        command.Parameters.AddWithValue("$resourceId", (object?)resourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$recordedAtUtc", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsumedEventRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT event_key, event_name, resource_id, recorded_at_utc
FROM consumed_events
ORDER BY recorded_at_utc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<ConsumedEventRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ConsumedEventRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return results;
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private void EnsureDatabase()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource))
        {
            var fullPath = Path.GetFullPath(builder.DataSource);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS consumed_events (
    event_key TEXT PRIMARY KEY,
    event_name TEXT NOT NULL,
    resource_id TEXT NULL,
    recorded_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_consumed_events_recorded_at_utc ON consumed_events(recorded_at_utc DESC);";
        command.ExecuteNonQuery();
    }
}
