using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Logging;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Represents rabbit mq topology bootstrapper.
/// </summary>


public sealed class RabbitMqTopologyBootstrapper
{
    private readonly RabbitMqConnectionFactoryFactory _connectionFactoryFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqTopologyBootstrapper(RabbitMqConnectionFactoryFactory connectionFactoryFactory, IOptions<RabbitMqOptions> options)
    {
        _connectionFactoryFactory = connectionFactoryFactory;
        _options = options.Value;
    }

    public void EnsureTopology(ILogger logger)
    {
        using var connection = _connectionFactoryFactory.Create().CreateConnection(_options.ClientProvidedName);
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: true, autoDelete: false, arguments: null);

        channel.QueueDeclare(_options.ExecutorDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueDeclare(_options.ExecutorResultsDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

        channel.QueueDeclare(
            _options.ExecutorQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _options.ExecutorDeadLetterQueue,
            });

        channel.QueueDeclare(
            _options.ExecutorResultsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _options.ExecutorResultsDeadLetterQueue,
            });

        try
        {
            channel.QueueUnbind(_options.ExecutorQueue, _options.Exchange, "kalshi.integration.#", arguments: null);
        }
        catch
        {
            // Ignore if the legacy binding is absent.
        }

        channel.QueueBind(_options.ExecutorQueue, _options.Exchange, _options.RoutingKeyBinding, arguments: null);
        channel.QueueBind(_options.ExecutorResultsQueue, _options.Exchange, _options.ResultsRoutingKeyBinding, arguments: null);

        ExecutorLogMessages.TopologyReady(logger, _options.Exchange, _options.ExecutorQueue, _options.ExecutorResultsQueue);
    }
}
