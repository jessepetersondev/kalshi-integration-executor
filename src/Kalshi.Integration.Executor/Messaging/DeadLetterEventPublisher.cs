using System.Text;
using System.Text.Json;
using Kalshi.Integration.Executor.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Kalshi.Integration.Executor.Messaging;

public sealed class DeadLetterEventPublisher : IDeadLetterEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;

    public DeadLetterEventPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public Task PublishAsync(ApplicationEventEnvelope applicationEvent, string deadLetterQueue, CancellationToken cancellationToken = default)
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

        var payload = JsonSerializer.Serialize(applicationEvent, SerializerOptions);
        var body = Encoding.UTF8.GetBytes(payload);

        using var connection = factory.CreateConnection(_options.ClientProvidedName);
        using var channel = connection.CreateModel();
        channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.BasicPublish(exchange: string.Empty, routingKey: deadLetterQueue, basicProperties: null, body: body);
        return Task.CompletedTask;
    }
}
