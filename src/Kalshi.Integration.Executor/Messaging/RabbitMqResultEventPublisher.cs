using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using RabbitMQ.Client;


namespace Kalshi.Integration.Executor.Messaging;

public sealed class RabbitMqResultEventPublisher : IResultEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;

    public RabbitMqResultEventPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var routingKey = BuildRoutingKey(applicationEvent);
        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);

        using var connection = factory.CreateConnection(_options.ClientProvidedName);
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: true, autoDelete: false, arguments: null);

        var properties = channel.CreateBasicProperties();
        properties.AppId = _options.ClientProvidedName;
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.MessageId = applicationEvent.Id.ToString();
        properties.CorrelationId = applicationEvent.CorrelationId ?? applicationEvent.Id.ToString();
        properties.Type = applicationEvent.Name;

        channel.BasicPublish(_options.Exchange, routingKey, mandatory: false, basicProperties: properties, body: body);
        return Task.CompletedTask;
    }

    private static string BuildRoutingKey(ApplicationEventEnvelope applicationEvent)
    {
        var name = applicationEvent.Name.Trim().ToLowerInvariant().Replace('-', '_').Replace('.', '_');
        return $"kalshi.integration.results.{name}";
    }
}