using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Messaging;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class RabbitMqConnectionFactoryFactoryTests
{
    [Fact]
    public void CreateShouldEnableAutomaticAndTopologyRecovery()
    {
        var factoryFactory = new RabbitMqConnectionFactoryFactory(Options.Create(new RabbitMqOptions
        {
            HostName = "rabbitmq",
            Port = 5672,
            VirtualHost = "/",
            UserName = "guest",
            Password = "guest",
            ClientProvidedName = "executor-tests",
        }));

        var connectionFactory = factoryFactory.Create();

        Assert.True(connectionFactory.AutomaticRecoveryEnabled);
        Assert.True(connectionFactory.TopologyRecoveryEnabled);
        Assert.True(connectionFactory.DispatchConsumersAsync);
        Assert.Equal(TimeSpan.FromSeconds(5), connectionFactory.NetworkRecoveryInterval);
        Assert.Equal("executor-tests", connectionFactory.ClientProvidedName);
    }
}
