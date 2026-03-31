using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Configuration;
using RabbitMQ.Client;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes rabbit mq inbound event.
/// </summary>


public sealed class RabbitMqInboundEventPublisher : IInboundEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConnectionFactoryFactory _connectionFactoryFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqInboundEventPublisher(RabbitMqConnectionFactoryFactory connectionFactoryFactory, IOptions<RabbitMqOptions> options)
    {
        _connectionFactoryFactory = connectionFactoryFactory;
        _options = options.Value;
    }

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);
        var routingKey = BuildRoutingKey(applicationEvent);

        using var connection = _connectionFactoryFactory.Create().CreateConnection(_options.ClientProvidedName);
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
        static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('-', '_').Replace('.', '_');
        return $"kalshi.integration.{Normalize(applicationEvent.Category)}.{Normalize(applicationEvent.Name)}";
    }
}
