using Kalshi.Integration.Executor;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.Handlers;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Logging;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;
using Kalshi.Integration.Executor.Routing;
using Microsoft.Extensions.Options;

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
    .PostConfigure(options =>
    {
        if (string.IsNullOrWhiteSpace(options.PrivateKeyPem) && !string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            var fullPath = Path.IsPathRooted(options.PrivateKeyPath)
                ? options.PrivateKeyPath
                : Path.Combine(builder.Environment.ContentRootPath, options.PrivateKeyPath);
            if (File.Exists(fullPath))
            {
                options.PrivateKeyPem = File.ReadAllText(fullPath);
            }
        }
    })
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{KalshiApiOptions.SectionName}:BaseUrl must be an absolute URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.PrivateKeyPem), $"{KalshiApiOptions.SectionName}:PrivateKeyPem or readable PrivateKeyPath must be configured.")
    .ValidateOnStart();

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

var kalshiApiOptions = builder.Configuration.GetSection(KalshiApiOptions.SectionName).Get<KalshiApiOptions>() ?? new KalshiApiOptions();

builder.Services.AddSingleton<RabbitMqTopologyBootstrapper>();
builder.Services.AddSingleton<IEventRouter, EventRouter>();
builder.Services.AddSingleton<IEventDispatcher, EventDispatcher>();
builder.Services.AddSingleton<IResultEventPublisher, RabbitMqResultEventPublisher>();
builder.Services.AddSingleton<IConsumedEventStore, SqliteConsumedEventStore>();
builder.Services.AddSingleton<IDeadLetterEventPublisher, DeadLetterEventPublisher>();
builder.Services.AddSingleton<ExecutionReliabilityPolicy>();
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

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var options = host.Services.GetRequiredService<IOptions<ExecutorOptions>>().Value;
ExecutorLogMessages.Startup(
    logger,
    options.ServiceName,
    options.Mode,
    host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

host.Run();
