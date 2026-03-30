using Kalshi.Integration.Executor.Logging;
using Kalshi.Integration.Executor.Messaging;


namespace Kalshi.Integration.Executor;

/// <summary>
/// Minimal hosted worker used to keep the process alive after bootstrapping topology.
/// The actual message consumption path lives in <see cref="RabbitMqEventConsumer"/>.
/// </summary>
public sealed class ExecutorWorker : BackgroundService
{
    private readonly ILogger<ExecutorWorker> _logger;
    private readonly RabbitMqTopologyBootstrapper _topologyBootstrapper;

    public ExecutorWorker(ILogger<ExecutorWorker> logger, RabbitMqTopologyBootstrapper topologyBootstrapper)
    {
        _logger = logger;
        _topologyBootstrapper = topologyBootstrapper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _topologyBootstrapper.EnsureTopology(_logger);
        ExecutorLogMessages.WorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}