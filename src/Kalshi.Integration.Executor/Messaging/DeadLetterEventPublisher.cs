using System.Text;
using System.Text.Json;
using Kalshi.Integration.Executor.Configuration;

namespace Kalshi.Integration.Executor.Messaging;

/// <summary>
/// Publishes dead letter event.
/// </summary>


public sealed class DeadLetterEventPublisher : IDeadLetterEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConnectionFactoryFactory _connectionFactoryFactory;
    private readonly RabbitMqOptions _options;

    public DeadLetterEventPublisher(RabbitMqConnectionFactoryFactory connectionFactoryFactory, Microsoft.Extensions.Options.IOptions<RabbitMqOptions> options)
    {
        _connectionFactoryFactory = connectionFactoryFactory;
        _options = options.Value;
    }

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);

        using var connection = _connectionFactoryFactory.Create().CreateConnection(_options.ClientProvidedName);
        using var channel = connection.CreateModel();
        channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.BasicPublish(exchange: string.Empty, routingKey: deadLetterQueue, mandatory: false, basicProperties: null, body: body);
        return Task.CompletedTask;
    }
}
