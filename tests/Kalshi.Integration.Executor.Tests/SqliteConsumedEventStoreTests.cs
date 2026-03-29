using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class SqliteConsumedEventStoreTests
{
    [Fact]
    public async Task SqliteConsumedEventStoreShouldPersistProcessedEventsAcrossInstances()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kalshi-executor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "consumed-events.db");
        var options = Options.Create(new PersistenceOptions
        {
            ConnectionString = $"Data Source={databasePath}",
        });

        try
        {
            var store = new SqliteConsumedEventStore(options);
            await store.RecordProcessedAsync("event-1", "order.created", "resource-1");

            var restartedStore = new SqliteConsumedEventStore(options);
            var wasProcessed = await restartedStore.HasProcessedAsync("event-1");
            var recent = await restartedStore.ListRecentAsync();

            Assert.True(wasProcessed);
            var record = Assert.Single(recent);
            Assert.Equal("event-1", record.EventKey);
            Assert.Equal("order.created", record.EventName);
            Assert.Equal("resource-1", record.ResourceId);
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
