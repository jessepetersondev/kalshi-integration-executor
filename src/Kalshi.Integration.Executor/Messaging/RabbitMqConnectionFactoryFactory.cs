using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using RabbitMQ.Client;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Creates rabbit mq connection factory instances.
/// </summary>


public sealed class RabbitMqConnectionFactoryFactory
{
    private readonly RabbitMqOptions _options;

    public RabbitMqConnectionFactoryFactory(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public ConnectionFactory Create()
    {
        return new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            ClientProvidedName = _options.ClientProvidedName,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };
    }
}
