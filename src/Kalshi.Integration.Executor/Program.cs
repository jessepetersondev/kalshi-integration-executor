using Kalshi.Integration.Executor;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Logging;
using Kalshi.Integration.Executor.Messaging;
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
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{KalshiApiOptions.SectionName}:BaseUrl must be an absolute URL.")
    .ValidateOnStart();

var kalshiApiOptions = builder.Configuration.GetSection(KalshiApiOptions.SectionName).Get<KalshiApiOptions>() ?? new KalshiApiOptions();

builder.Services.AddSingleton<RabbitMqTopologyBootstrapper>();
builder.Services.AddSingleton<IEventRouter, EventRouter>();
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

builder.Services.AddHostedService<ExecutorWorker>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var options = host.Services.GetRequiredService<IOptions<ExecutorOptions>>().Value;
ExecutorLogMessages.Startup(
    logger,
    options.ServiceName,
    options.Mode,
    host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

host.Run();
