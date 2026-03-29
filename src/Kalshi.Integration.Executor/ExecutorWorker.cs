using Kalshi.Integration.Executor.Logging;

namespace Kalshi.Integration.Executor;

public sealed class ExecutorWorker : BackgroundService
{
    private readonly ILogger<ExecutorWorker> _logger;

    public ExecutorWorker(ILogger<ExecutorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ExecutorLogMessages.WorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
