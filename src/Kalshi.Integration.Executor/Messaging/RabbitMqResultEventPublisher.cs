using System.Text;
using System.Text.Json;
using Kalshi.Integration.Executor.Configuration;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes rabbit mq result event.
/// </summary>
public sealed class RabbitMqResultEventPublisher : IResultEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConnectionFactoryFactory _connectionFactoryFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqResultEventPublisher(RabbitMqConnectionFactoryFactory connectionFactoryFactory, Microsoft.Extensions.Options.IOptions<RabbitMqOptions> options)
    {
        _connectionFactoryFactory = connectionFactoryFactory;
        _options = options.Value;
    }

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routingKey = BuildRoutingKey(applicationEvent);
        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);

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
        var name = applicationEvent.Name.Trim().ToLowerInvariant().Replace('-', '_').Replace('.', '_');
        return $"kalshi.integration.results.{name}";
    }
}
