using System.Text.Json;
using Kalshi.Integration.Executor.Diagnostics;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutorCliRunnerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TryRunAsyncShouldReturnFalseWhenNoCliCommandIsProvided()
    {
        var provider = BuildServiceProvider(new InMemoryDeadLetterRecordStore(), new InMemoryInboundEventPublisher());
        var output = new StringWriter();

        var handled = await ExecutorCliRunner.TryRunAsync([], provider, output, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task TryRunAsyncShouldInspectDeadLetterRecords()
    {
        var store = new InMemoryDeadLetterRecordStore();
        var envelope = CreateEnvelope();
        await store.AddAsync(new DeadLetterRecord(
            Guid.NewGuid(),
            envelope.Id,
            envelope.Category,
            envelope.Name,
            envelope.ResourceId,
            envelope.CorrelationId,
            envelope.IdempotencyKey,
            "kalshi.integration.executor.dlq",
            2,
            "HttpRequestException",
            "transient failure",
            JsonSerializer.Serialize(envelope, SerializerOptions),
            DateTimeOffset.UtcNow,
            null,
            0));
        var provider = BuildServiceProvider(store, new InMemoryInboundEventPublisher());
        var output = new StringWriter();

        var handled = await ExecutorCliRunner.TryRunAsync(["dlq", "inspect", "--limit", "10"], provider, output, CancellationToken.None);

        Assert.True(handled);
        Assert.Contains("order.created", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("HttpRequestException", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryRunAsyncShouldReplayRequestedDeadLetterRecord()
    {
        var store = new InMemoryDeadLetterRecordStore();
        var inboundPublisher = new InMemoryInboundEventPublisher();
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
            2,
            "HttpRequestException",
            "transient failure",
            JsonSerializer.Serialize(envelope, SerializerOptions),
            DateTimeOffset.UtcNow,
            null,
            0);
        await store.AddAsync(record);
        var provider = BuildServiceProvider(store, inboundPublisher);
        var output = new StringWriter();

        var handled = await ExecutorCliRunner.TryRunAsync(["dlq", "replay", "--id", record.Id.ToString()], provider, output, CancellationToken.None);

        Assert.True(handled);
        Assert.Contains(record.Id.ToString(), output.ToString(), StringComparison.Ordinal);
        Assert.Single(inboundPublisher.PublishedEvents);
    }

    private static ServiceProvider BuildServiceProvider(IDeadLetterRecordStore store, IInboundEventPublisher inboundEventPublisher)
        => new ServiceCollection()
            .AddSingleton(store)
            .AddSingleton(inboundEventPublisher)
            .AddSingleton<DeadLetterReplayService>()
            .BuildServiceProvider();

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
