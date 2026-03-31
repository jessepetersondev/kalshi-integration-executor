using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.Configuration;

/// <summary>
/// Configures the RabbitMQ exchanges, queues, and connection settings used by the executor.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string UserName { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";

    [Required]
    public string Exchange { get; set; } = "kalshi.integration.events";

    [Required]
    public string ExchangeType { get; set; } = "topic";

    [Required]
    public string ExecutorQueue { get; set; } = "kalshi.integration.executor";

    [Required]
    public string ExecutorResultsQueue { get; set; } = "kalshi.integration.executor.results";

    [Required]
    public string ExecutorDeadLetterQueue { get; set; } = "kalshi.integration.executor.dlq";

    [Required]
    public string ExecutorResultsDeadLetterQueue { get; set; } = "kalshi.integration.executor.results.dlq";

    [Required]
    public string RoutingKeyBinding { get; set; } = "kalshi.integration.trading.#";

    [Required]
    public string ResultsRoutingKeyBinding { get; set; } = "kalshi.integration.results.#";

    [Required]
    public string ClientProvidedName { get; set; } = "kalshi-integration-executor";
}