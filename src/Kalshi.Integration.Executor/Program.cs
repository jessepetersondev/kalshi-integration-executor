using Kalshi.Integration.Executor;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Diagnostics;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Logging;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Kalshi.Integration.Executor.Routing;
using Microsoft.Extensions.Options;

// Wire the executor as a generic host so the same process can run either as
// the long-lived RabbitMQ worker or as the DLQ diagnostics CLI.
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<ExecutorOptions>()
    .Bind(builder.Configuration.GetSection(ExecutorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<KalshiApiOptions>()
    .Bind(builder.Configuration.GetSection(KalshiApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IPostConfigureOptions<KalshiApiOptions>, KalshiApiOptionsPostConfigure>();
builder.Services.AddSingleton<IValidateOptions<KalshiApiOptions>, KalshiApiOptionsValidator>();

builder.Services
    .AddOptions<FailureHandlingOptions>()
    .Bind(builder.Configuration.GetSection(FailureHandlingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<PersistenceOptions>()
    .Bind(builder.Configuration.GetSection(PersistenceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RiskControlsOptions>()
    .Bind(builder.Configuration.GetSection(RiskControlsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var kalshiApiOptions = builder.Configuration.GetSection(KalshiApiOptions.SectionName).Get<KalshiApiOptions>() ?? new KalshiApiOptions();

builder.Services.AddSingleton<RabbitMqConnectionFactoryFactory>();
builder.Services.AddSingleton<RabbitMqTopologyBootstrapper>();
builder.Services.AddSingleton<IEventRouter, EventRouter>();
builder.Services.AddSingleton<IEventDispatcher, EventDispatcher>();
builder.Services.AddSingleton<IInboundEventPublisher, RabbitMqInboundEventPublisher>();
builder.Services.AddSingleton<IResultEventPublisher, RabbitMqResultEventPublisher>();
builder.Services.AddSingleton<IConsumedEventStore, SqliteConsumedEventStore>();
builder.Services.AddSingleton<IExecutionRecordStore, SqliteExecutionRecordStore>();
builder.Services.AddSingleton<IDeadLetterRecordStore, SqliteDeadLetterRecordStore>();
builder.Services.AddSingleton<IExecutionRiskGuard, ExecutionRiskGuard>();
builder.Services.AddSingleton<IDeadLetterEventPublisher, DeadLetterEventPublisher>();
builder.Services.AddSingleton<ExecutionReliabilityPolicy>();
builder.Services.AddSingleton<DeadLetterReplayService>();
builder.Services.AddTransient<OrderCreatedHandler>();
builder.Services.AddTransient<TradeIntentCreatedHandler>();
builder.Services.AddTransient<ExecutionUpdateAppliedHandler>();
builder.Services.AddHttpClient<IKalshiExecutionClient, KalshiExecutionClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<KalshiApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddStandardResilienceHandler(resilienceOptions =>
    {
        resilienceOptions.Retry.MaxRetryAttempts = kalshiApiOptions.RetryAttempts;
        resilienceOptions.AttemptTimeout.Timeout = TimeSpan.FromSeconds(kalshiApiOptions.TimeoutSeconds);
        resilienceOptions.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(Math.Max(kalshiApiOptions.TimeoutSeconds * Math.Max(1, kalshiApiOptions.RetryAttempts + 1), kalshiApiOptions.TimeoutSeconds));
    });

builder.Services.AddHostedService<RabbitMqEventConsumer>();

using var host = builder.Build();

if (await ExecutorCliRunner.TryRunAsync(args, host.Services, Console.Out, CancellationToken.None))
{
    return;
}

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var options = host.Services.GetRequiredService<IOptions<ExecutorOptions>>().Value;
ExecutorLogMessages.Startup(
    logger,
    options.ServiceName,
    options.Mode,
    host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

await host.RunAsync();
