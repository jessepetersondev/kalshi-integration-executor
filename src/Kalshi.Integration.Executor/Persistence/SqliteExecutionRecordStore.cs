using System.Globalization;
using Kalshi.Integration.Executor.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Persistence;

/// <summary>
/// Provides storage operations for sqlite execution record.
/// </summary>
public sealed class SqliteExecutionRecordStore : IExecutionRecordStore
{
    private readonly string _connectionString;

    public SqliteExecutionRecordStore(IOptions<PersistenceOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
        EnsureDatabase();
    }

    public async Task UpsertAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO execution_records (
    external_order_id,
    client_order_id,
    resource_id,
    correlation_id,
    command_event_id,
    action_type,
    trade_intent_id,
    publisher_order_id,
    ticker,
    side,
    action,
    target_publisher_order_id,
    target_client_order_id,
    target_external_order_id,
    status,
    quantity,
    limit_price_dollars,
    notional_dollars,
    raw_response,
    recorded_at_utc
)
VALUES (
    $external_order_id,
    $client_order_id,
    $resource_id,
    $correlation_id,
    $command_event_id,
    $action_type,
    $trade_intent_id,
    $publisher_order_id,
    $ticker,
    $side,
    $action,
    $target_publisher_order_id,
    $target_client_order_id,
    $target_external_order_id,
    $status,
    $quantity,
    $limit_price_dollars,
    $notional_dollars,
    $raw_response,
    $recorded_at_utc
)
ON CONFLICT(external_order_id) DO UPDATE SET
    client_order_id = excluded.client_order_id,
    resource_id = excluded.resource_id,
    correlation_id = excluded.correlation_id,
    command_event_id = excluded.command_event_id,
    action_type = excluded.action_type,
    trade_intent_id = excluded.trade_intent_id,
    publisher_order_id = excluded.publisher_order_id,
    ticker = excluded.ticker,
    side = excluded.side,
    action = excluded.action,
    target_publisher_order_id = excluded.target_publisher_order_id,
    target_client_order_id = excluded.target_client_order_id,
    target_external_order_id = excluded.target_external_order_id,
    status = excluded.status,
    quantity = COALESCE(excluded.quantity, execution_records.quantity),
    limit_price_dollars = COALESCE(excluded.limit_price_dollars, execution_records.limit_price_dollars),
    notional_dollars = COALESCE(excluded.notional_dollars, execution_records.notional_dollars),
    raw_response = excluded.raw_response,
    recorded_at_utc = excluded.recorded_at_utc;";
        command.Parameters.AddWithValue("$external_order_id", record.ExternalOrderId);
        command.Parameters.AddWithValue("$client_order_id", record.ClientOrderId);
        command.Parameters.AddWithValue("$resource_id", (object?)record.ResourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$correlation_id", (object?)record.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$command_event_id", (object?)record.CommandEventId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action_type", (object?)record.ActionType ?? DBNull.Value);
        command.Parameters.AddWithValue("$trade_intent_id", (object?)record.TradeIntentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$publisher_order_id", (object?)record.PublisherOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$ticker", (object?)record.Ticker ?? DBNull.Value);
        command.Parameters.AddWithValue("$side", (object?)record.Side ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", (object?)record.Action ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_publisher_order_id", (object?)record.TargetPublisherOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_client_order_id", (object?)record.TargetClientOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$target_external_order_id", (object?)record.TargetExternalOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (object?)record.Status ?? DBNull.Value);
        command.Parameters.AddWithValue("$quantity", (object?)record.Quantity ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit_price_dollars", (object?)record.LimitPriceDollars ?? DBNull.Value);
        command.Parameters.AddWithValue("$notional_dollars", (object?)record.NotionalDollars ?? DBNull.Value);
        command.Parameters.AddWithValue("$raw_response", record.RawResponse);
        command.Parameters.AddWithValue("$recorded_at_utc", record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ExecutionRecord?> GetByExternalOrderIdAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT external_order_id, client_order_id, resource_id, correlation_id, command_event_id, action_type, trade_intent_id, publisher_order_id, ticker, side, action, target_publisher_order_id, target_client_order_id, target_external_order_id, status, quantity, limit_price_dollars, notional_dollars, raw_response, recorded_at_utc
FROM execution_records
WHERE external_order_id = $external_order_id
LIMIT 1;";
        command.Parameters.AddWithValue("$external_order_id", externalOrderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRecord(reader);
    }

    public async Task<ExecutionRecord?> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT external_order_id, client_order_id, resource_id, correlation_id, command_event_id, action_type, trade_intent_id, publisher_order_id, ticker, side, action, target_publisher_order_id, target_client_order_id, target_external_order_id, status, quantity, limit_price_dollars, notional_dollars, raw_response, recorded_at_utc
FROM execution_records
WHERE resource_id = $resource_id
ORDER BY recorded_at_utc DESC
LIMIT 1;";
        command.Parameters.AddWithValue("$resource_id", resourceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRecord(reader);
    }

    public async Task<ExecutionRecord?> GetByClientOrderIdAsync(string clientOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT external_order_id, client_order_id, resource_id, correlation_id, command_event_id, action_type, trade_intent_id, publisher_order_id, ticker, side, action, target_publisher_order_id, target_client_order_id, target_external_order_id, status, quantity, limit_price_dollars, notional_dollars, raw_response, recorded_at_utc
FROM execution_records
WHERE client_order_id = $client_order_id
ORDER BY recorded_at_utc DESC
LIMIT 1;";
        command.Parameters.AddWithValue("$client_order_id", clientOrderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT external_order_id, client_order_id, resource_id, correlation_id, command_event_id, action_type, trade_intent_id, publisher_order_id, ticker, side, action, target_publisher_order_id, target_client_order_id, target_external_order_id, status, quantity, limit_price_dollars, notional_dollars, raw_response, recorded_at_utc
FROM execution_records
ORDER BY recorded_at_utc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        var records = new List<ExecutionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    private static ExecutionRecord ReadRecord(SqliteDataReader reader)
    {
        return new ExecutionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetInt32(15),
            reader.IsDBNull(16) ? null : reader.GetDecimal(16),
            reader.IsDBNull(17) ? null : reader.GetDecimal(17),
            reader.GetString(18),
            DateTimeOffset.Parse(reader.GetString(19), CultureInfo.InvariantCulture));
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private static void TryAddColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Ignore when the column already exists.
        }
    }

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
CREATE TABLE IF NOT EXISTS execution_records (
    external_order_id TEXT PRIMARY KEY,
    client_order_id TEXT NOT NULL,
    resource_id TEXT NULL,
    correlation_id TEXT NULL,
    command_event_id TEXT NULL,
    action_type TEXT NULL,
    trade_intent_id TEXT NULL,
    publisher_order_id TEXT NULL,
    ticker TEXT NULL,
    side TEXT NULL,
    action TEXT NULL,
    target_publisher_order_id TEXT NULL,
    target_client_order_id TEXT NULL,
    target_external_order_id TEXT NULL,
    status TEXT NULL,
    quantity INTEGER NULL,
    limit_price_dollars REAL NULL,
    notional_dollars REAL NULL,
    raw_response TEXT NOT NULL,
    recorded_at_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_execution_records_recorded_at_utc ON execution_records(recorded_at_utc DESC);";
        command.ExecuteNonQuery();
        TryAddColumn(connection, "execution_records", "quantity", "INTEGER NULL");
        TryAddColumn(connection, "execution_records", "limit_price_dollars", "REAL NULL");
        TryAddColumn(connection, "execution_records", "notional_dollars", "REAL NULL");
        TryAddColumn(connection, "execution_records", "command_event_id", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "action_type", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "trade_intent_id", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "publisher_order_id", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "target_publisher_order_id", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "target_client_order_id", "TEXT NULL");
        TryAddColumn(connection, "execution_records", "target_external_order_id", "TEXT NULL");
    }
}
