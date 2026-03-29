using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Logging;
using RabbitMQ.Client;


namespace Kalshi.Integration.Executor.Messaging;

public sealed class RabbitMqTopologyBootstrapper
{
    private readonly RabbitMqOptions _options;

    public RabbitMqTopologyBootstrapper(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public void EnsureTopology(ILogger logger)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            ClientProvidedName = _options.ClientProvidedName,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
        };

        using var connection = factory.CreateConnection(_options.ClientProvidedName);
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

        channel.QueueBind(_options.ExecutorQueue, _options.Exchange, _options.RoutingKeyBinding);
        channel.QueueBind(_options.ExecutorResultsQueue, _options.Exchange, _options.ResultsRoutingKeyBinding);

        ExecutorLogMessages.TopologyReady(logger, _options.Exchange, _options.ExecutorQueue, _options.ExecutorResultsQueue);
    }
}