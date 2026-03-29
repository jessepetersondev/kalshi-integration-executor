using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Messaging;


namespace Kalshi.Integration.Executor.Execution;

public sealed class ExecutionReliabilityPolicy
{
    private readonly FailureHandlingOptions _options;
    private readonly IDeadLetterEventPublisher _deadLetterEventPublisher;

    public ExecutionReliabilityPolicy(IOptions<FailureHandlingOptions> options, IDeadLetterEventPublisher deadLetterEventPublisher)
    {
        _options = options.Value;
        _deadLetterEventPublisher = deadLetterEventPublisher;
    }

    public async Task ExecuteAsync(
        ApplicationEventEnvelope envelope,
        string deadLetterQueue,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
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

        var deadLetterEnvelope = new ApplicationEventEnvelope(
            Guid.NewGuid(),
            "executor",
            $"{envelope.Name}.dead_lettered",
            envelope.ResourceId,
            envelope.CorrelationId,
            envelope.IdempotencyKey,
            new Dictionary<string, string?>
            {
                ["sourceEvent"] = envelope.Name,
                ["deadLetterQueue"] = deadLetterQueue,
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