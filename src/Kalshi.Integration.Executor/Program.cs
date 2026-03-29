using Kalshi.Integration.Executor;
using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Logging;
using Kalshi.Integration.Executor.Messaging;
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

builder.Services.AddSingleton<RabbitMqTopologyBootstrapper>();
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
