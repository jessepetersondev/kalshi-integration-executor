using System.Text;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Kalshi.Integration.Executor.Routing;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Consumes inbound executor work from RabbitMQ, routes envelopes to the matching
/// handler, and acknowledges only after the dispatch pipeline completes.
/// </summary>
public sealed class RabbitMqEventConsumer : BackgroundService
{
    private readonly ILogger<RabbitMqEventConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqTopologyBootstrapper _topologyBootstrapper;
    private readonly RabbitMqConnectionFactoryFactory _connectionFactoryFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqEventConsumer(
        ILogger<RabbitMqEventConsumer> logger,
        IServiceScopeFactory scopeFactory,
        RabbitMqTopologyBootstrapper topologyBootstrapper,
        RabbitMqConnectionFactoryFactory connectionFactoryFactory,
        IOptions<RabbitMqOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _topologyBootstrapper = topologyBootstrapper;
        _connectionFactoryFactory = connectionFactoryFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _topologyBootstrapper.EnsureTopology(_logger);
        ExecutorLogMessages.WorkerStarted(_logger);

        using var connection = _connectionFactoryFactory.Create().CreateConnection(_options.ClientProvidedName);
        using var channel = connection.CreateModel();
        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) =>
        {
            var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            using var scope = _scopeFactory.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IEventRouter>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

            try
            {
                var routingResult = router.Route(payload);
                await dispatcher.DispatchAsync(routingResult, stoppingToken);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (UnsupportedEventException)
            {
                channel.BasicReject(eventArgs.DeliveryTag, requeue: false);
            }
            catch (Exception)
            {
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(queue: _options.ExecutorQueue, autoAck: false, consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
