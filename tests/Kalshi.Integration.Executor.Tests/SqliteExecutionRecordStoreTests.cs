using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class SqliteExecutionRecordStoreTests
{
    [Fact]
    public async Task SqliteExecutionRecordStoreShouldPersistExecutionRecordsAcrossInstances()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kalshi-execution-record-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "execution-records.db");
        var options = Options.Create(new PersistenceOptions
        {
            ConnectionString = $"Data Source={databasePath}",
        });

        try
        {
            var store = new SqliteExecutionRecordStore(options);
            await store.UpsertAsync(new ExecutionRecord(
                "ext-123",
                "client-123",
                "resource-1",
                "corr-1",
                "KXBTC",
                "yes",
                "buy",
                "filled",
                "{\"order\":{}}",
                DateTimeOffset.UtcNow));

            var restartedStore = new SqliteExecutionRecordStore(options);
            var record = await restartedStore.GetByExternalOrderIdAsync("ext-123");
            var recent = await restartedStore.ListRecentAsync();

            Assert.NotNull(record);
            Assert.Equal("client-123", record!.ClientOrderId);
            Assert.Equal("filled", record.Status);
            Assert.Single(recent);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
