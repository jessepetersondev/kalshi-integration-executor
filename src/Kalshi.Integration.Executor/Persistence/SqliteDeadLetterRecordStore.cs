using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;

namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Provides storage operations for sqlite dead letter record.
/// </summary>


public sealed class SqliteDeadLetterRecordStore : IDeadLetterRecordStore
{
    private readonly string _connectionString;

    public SqliteDeadLetterRecordStore(IOptions<PersistenceOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
        EnsureDatabase();
    }

    public async Task AddAsync(DeadLetterRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR REPLACE INTO dead_letter_records (
    id,
    source_event_id,
    source_category,
    source_event_name,
    resource_id,
    correlation_id,
    idempotency_key,
    dead_letter_queue,
    attempt_count,
    error_type,
    error_message,
    original_payload,
    dead_lettered_at_utc,
    last_replayed_at_utc,
    replay_count
)
VALUES (
    $id,
    $source_event_id,
    $source_category,
    $source_event_name,
    $resource_id,
    $correlation_id,
    $idempotency_key,
    $dead_letter_queue,
    $attempt_count,
    $error_type,
    $error_message,
    $original_payload,
    $dead_lettered_at_utc,
    $last_replayed_at_utc,
    $replay_count
);";
        command.Parameters.AddWithValue("$id", record.Id.ToString());
        command.Parameters.AddWithValue("$source_event_id", record.SourceEventId.ToString());
        command.Parameters.AddWithValue("$source_category", record.SourceCategory);
        command.Parameters.AddWithValue("$source_event_name", record.SourceEventName);
        command.Parameters.AddWithValue("$resource_id", (object?)record.ResourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$correlation_id", (object?)record.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$idempotency_key", (object?)record.IdempotencyKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$dead_letter_queue", record.DeadLetterQueue);
        command.Parameters.AddWithValue("$attempt_count", record.AttemptCount);
        command.Parameters.AddWithValue("$error_type", (object?)record.ErrorType ?? DBNull.Value);
        command.Parameters.AddWithValue("$error_message", (object?)record.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$original_payload", record.OriginalPayload);
        command.Parameters.AddWithValue("$dead_lettered_at_utc", record.DeadLetteredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$last_replayed_at_utc", record.LastReplayedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$replay_count", record.ReplayCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DeadLetterRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, source_event_id, source_category, source_event_name, resource_id, correlation_id, idempotency_key, dead_letter_queue, attempt_count, error_type, error_message, original_payload, dead_lettered_at_utc, last_replayed_at_utc, replay_count
FROM dead_letter_records
WHERE id = $id
LIMIT 1;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRecord(reader)
            : null;
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, source_event_id, source_category, source_event_name, resource_id, correlation_id, idempotency_key, dead_letter_queue, attempt_count, error_type, error_message, original_payload, dead_lettered_at_utc, last_replayed_at_utc, replay_count
FROM dead_letter_records
ORDER BY dead_lettered_at_utc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<DeadLetterRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    public async Task MarkReplayedAsync(Guid id, DateTimeOffset replayedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE dead_letter_records
SET last_replayed_at_utc = $last_replayed_at_utc,
    replay_count = replay_count + 1
WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$last_replayed_at_utc", replayedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DeadLetterRecord ReadRecord(SqliteDataReader reader)
    {
        return new DeadLetterRecord(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetString(11),
            DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture),
            reader.IsDBNull(13) ? null : DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture),
            reader.GetInt32(14));
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
CREATE TABLE IF NOT EXISTS dead_letter_records (
    id TEXT PRIMARY KEY,
    source_event_id TEXT NOT NULL,
    source_category TEXT NOT NULL,
    source_event_name TEXT NOT NULL,
    resource_id TEXT NULL,
    correlation_id TEXT NULL,
    idempotency_key TEXT NULL,
    dead_letter_queue TEXT NOT NULL,
    attempt_count INTEGER NOT NULL,
    error_type TEXT NULL,
    error_message TEXT NULL,
    original_payload TEXT NOT NULL,
    dead_lettered_at_utc TEXT NOT NULL,
    last_replayed_at_utc TEXT NULL,
    replay_count INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_dead_letter_records_dead_lettered_at_utc ON dead_letter_records(dead_lettered_at_utc DESC);";
        command.ExecuteNonQuery();
    }
}
