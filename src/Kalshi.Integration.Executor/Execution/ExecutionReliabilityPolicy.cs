using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;


namespace Kalshi.Integration.Executor.Execution;

public sealed class ExecutionReliabilityPolicy
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly FailureHandlingOptions _options;
    private readonly IDeadLetterEventPublisher _deadLetterEventPublisher;
    private readonly IDeadLetterRecordStore _deadLetterRecordStore;

    public ExecutionReliabilityPolicy(
        IOptions<FailureHandlingOptions> options,
        IDeadLetterEventPublisher deadLetterEventPublisher,
        IDeadLetterRecordStore deadLetterRecordStore)
    {
        _options = options.Value;
        _deadLetterEventPublisher = deadLetterEventPublisher;
        _deadLetterRecordStore = deadLetterRecordStore;
    }

    public async Task ExecuteAsync(
        ApplicationEventEnvelope envelope,
        string deadLetterQueue,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        var attemptCount = 0;

        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            attemptCount = attempt + 1;

            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                lastException = exception;

                if (attempt == _options.MaxRetryAttempts)
                {
                    break;
                }

                var delay = TimeSpan.FromMilliseconds(_options.BaseDelayMilliseconds * Math.Max(1, attempt + 1));
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                break;
            }
        }

        var deadLetterRecord = new DeadLetterRecord(
            Guid.NewGuid(),
            envelope.Id,
            envelope.Category,
            envelope.Name,
            envelope.ResourceId,
            envelope.CorrelationId,
            envelope.IdempotencyKey,
            deadLetterQueue,
            attemptCount,
            lastException?.GetType().Name,
            lastException?.Message,
            JsonSerializer.Serialize(envelope, SerializerOptions),
            DateTimeOffset.UtcNow,
            null,
            0);

        await _deadLetterRecordStore.AddAsync(deadLetterRecord, cancellationToken);

        var deadLetterEnvelope = new ApplicationEventEnvelope(
            deadLetterRecord.Id,
            "executor",
            $"{envelope.Name}.dead_lettered",
            envelope.ResourceId,
            envelope.CorrelationId,
            envelope.IdempotencyKey,
            new Dictionary<string, string?>
            {
                ["deadLetterRecordId"] = deadLetterRecord.Id.ToString(),
                ["sourceEventId"] = envelope.Id.ToString(),
                ["sourceCategory"] = envelope.Category,
                ["sourceEvent"] = envelope.Name,
                ["deadLetterQueue"] = deadLetterQueue,
                ["attemptCount"] = attemptCount.ToString(CultureInfo.InvariantCulture),
                ["errorType"] = lastException?.GetType().Name,
                ["errorMessage"] = lastException?.Message,
            },
            DateTimeOffset.UtcNow);

        await _deadLetterEventPublisher.PublishAsync(deadLetterEnvelope, deadLetterQueue, cancellationToken);
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or TimeoutException;
    }
}
