using System.Text.Json;
using Kalshi.Integration.Executor.Diagnostics;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Tests;

public sealed class DeadLetterReplayServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ReplayAsyncShouldRepublishOriginalEnvelopeAndMarkRecordReplayed()
    {
        var store = new InMemoryDeadLetterRecordStore();
        var inboundPublisher = new InMemoryInboundEventPublisher();
        var service = new DeadLetterReplayService(store, inboundPublisher);
        var envelope = CreateEnvelope();
        var record = new DeadLetterRecord(
            Guid.NewGuid(),
            envelope.Id,
            envelope.Category,
            envelope.Name,
            envelope.ResourceId,
            envelope.CorrelationId,
            envelope.IdempotencyKey,
            "kalshi.integration.executor.dlq",
            3,
            "HttpRequestException",
            "transient failure",
            JsonSerializer.Serialize(envelope, SerializerOptions),
            DateTimeOffset.UtcNow,
            null,
            0);

        await store.AddAsync(record);
        await service.ReplayAsync(record.Id);

        var replayed = Assert.Single(inboundPublisher.PublishedEvents);
        Assert.Equal(envelope.Id, replayed.Id);
        Assert.Equal(envelope.Name, replayed.Name);

        var updated = await store.GetByIdAsync(record.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated!.ReplayCount);
        Assert.NotNull(updated.LastReplayedAtUtc);
    }

    [Fact]
    public async Task ReplayAsyncShouldThrowForUnknownDeadLetterRecord()
    {
        var service = new DeadLetterReplayService(new InMemoryDeadLetterRecordStore(), new InMemoryInboundEventPublisher());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplayAsync(Guid.NewGuid()));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    private static ApplicationEventEnvelope CreateEnvelope()
    {
        return new ApplicationEventEnvelope(
            Guid.NewGuid(),
            "trading",
            "order.created",
            "order-1",
            "corr-1",
            "idem-1",
            new Dictionary<string, string?>
            {
                ["ticker"] = "KXBTC-26MAR2919-T65999.99",
                ["side"] = "no",
                ["quantity"] = "1",
                ["limitPrice"] = "0.75",
            },
            DateTimeOffset.UtcNow);
    }
}
