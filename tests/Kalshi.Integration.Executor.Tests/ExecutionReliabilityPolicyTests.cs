using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutionReliabilityPolicyTests
{
    [Fact]
    public async Task ExecuteAsyncShouldRetryTransientFailureAndEventuallySucceed()
    {
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var policy = new ExecutionReliabilityPolicy(
            Options.Create(new FailureHandlingOptions
            {
                MaxRetryAttempts = 2,
                BaseDelayMilliseconds = 1,
            }),
            deadLetterPublisher,
            deadLetterStore);

        var attempts = 0;
        await policy.ExecuteAsync(
            CreateEnvelope(),
            "kalshi.integration.executor.dlq",
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new HttpRequestException("transient");
                }

                return Task.CompletedTask;
            });

        Assert.Equal(2, attempts);
        Assert.Empty(deadLetterPublisher.PublishedEvents);
        Assert.Empty(await deadLetterStore.ListRecentAsync());
    }

    [Fact]
    public async Task ExecuteAsyncShouldDeadLetterAfterRetryExhaustion()
    {
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var deadLetterStore = new InMemoryDeadLetterRecordStore();
        var policy = new ExecutionReliabilityPolicy(
            Options.Create(new FailureHandlingOptions
            {
                MaxRetryAttempts = 1,
                BaseDelayMilliseconds = 1,
            }),
            deadLetterPublisher,
            deadLetterStore);

        var attempts = 0;
        await policy.ExecuteAsync(
            CreateEnvelope(),
            "kalshi.integration.executor.dlq",
            _ =>
            {
                attempts++;
                throw new HttpRequestException("still transient");
            });

        Assert.Equal(2, attempts);
        var deadLetter = Assert.Single(deadLetterPublisher.PublishedEvents);
        Assert.Equal("order.created.dead_lettered", deadLetter.Name);
        Assert.Equal("kalshi.integration.executor.dlq", deadLetter.Attributes["deadLetterQueue"]);
        Assert.Equal("2", deadLetter.Attributes["attemptCount"]);
        var persisted = Assert.Single(await deadLetterStore.ListRecentAsync());
        Assert.Equal("order.created", persisted.SourceEventName);
        Assert.Equal(2, persisted.AttemptCount);
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
            new Dictionary<string, string?>(),
            DateTimeOffset.UtcNow);
    }

    private sealed class RecordingDeadLetterPublisher : IDeadLetterEventPublisher
    {
        public List<ApplicationEventEnvelope> PublishedEvents { get; } = [];

        public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(applicationEvent with
            {
                Attributes = new Dictionary<string, string?>(applicationEvent.Attributes)
                {
                    ["deadLetterQueue"] = deadLetterQueue,
                },
            });

            return Task.CompletedTask;
        }
    }
}
