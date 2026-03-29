namespace Kalshi.Integration.Executor.Logging;

internal static partial class ExecutorLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Starting {ServiceName}. Mode={Mode}. Environment={EnvironmentName}")]
    public static partial void Startup(ILogger logger, string serviceName, string mode, string environmentName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Kalshi Integration Executor worker started. RabbitMQ consumption and Kalshi execution handlers are not wired yet.")]
    public static partial void WorkerStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "RabbitMQ topology ensured. Exchange={Exchange}. ExecutorQueue={ExecutorQueue}. ResultsQueue={ResultsQueue}")]
    public static partial void TopologyReady(ILogger logger, string exchange, string executorQueue, string resultsQueue);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Kalshi API call {Operation} returned statusCode={StatusCode} in {DurationMs} ms.")]
    public static partial void KalshiApiCallSucceeded(ILogger logger, string operation, int statusCode, double durationMs);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Kalshi API call {Operation} failed in {DurationMs} ms. Error={ErrorMessage}")]
    public static partial void KalshiApiCallFailed(ILogger logger, string operation, double durationMs, string errorMessage);
}
