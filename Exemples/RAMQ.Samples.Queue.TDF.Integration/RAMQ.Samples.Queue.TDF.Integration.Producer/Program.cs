using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Services;
using RAMQ.Samples.Queue.TDF.Integration.Consumer.Messages;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Telemetry;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.None);
        logging.AddSimpleConsole(opts =>
        {
            opts.ColorBehavior  = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            opts.IncludeScopes  = false;
            opts.TimestampFormat = "HH:mm:ss.fff ";
        });
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("RAMQ",       LogLevel.Information);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("Azure",      LogLevel.Warning);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("Microsoft",  LogLevel.Warning);
        logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>("System",     LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITelemetryInitializer, ProducerTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
        // PostConfigure reset les deux → logs RAMQ.* passent via relay gRPC → host (couleurs natives func CLI).
        services.PostConfigure<LoggerFilterOptions>(opts =>
        {
            opts.MinLevel = LogLevel.None;
            opts.Rules.Add(new LoggerFilterRule(null, "RAMQ",      LogLevel.Information, null));
            opts.Rules.Add(new LoggerFilterRule(null, "Azure",     LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "Microsoft", LogLevel.Warning,     null));
            opts.Rules.Add(new LoggerFilterRule(null, "System",    LogLevel.Warning,     null));
        });


        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));

        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());

        services.AddProducer<TdfTransactionCommand>();

        services.AddSingleton<ITdfProducerService, TdfProducerService>();

        services.ConfigureAzureProviders(new VisualStudioCredential());
    })
    .Build();

await host.RunAsync();











