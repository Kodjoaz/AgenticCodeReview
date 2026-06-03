using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refit;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.HOA5.Consumer;
using RAMQ.Samples.Queue.HOA5.Consumer.Telemetry;
using RAMQ.Samples.Queue.TDF.Integration.Consumer.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        // Le relay gRPC worker→host ne fonctionne pas avec Functions.Worker 2.51.0/net8.
        // AddSimpleConsole écrit directement sur stdout du worker, pipé au terminal func CLI.
        // Les filtres provider-spécifiques ne peuvent pas être overridés par AppInsights.
        logging.SetMinimumLevel(LogLevel.None);
        logging.AddSimpleConsole(opts =>
        {
            opts.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
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
        services.AddSingleton<ITelemetryInitializer, ConsumerTelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();


        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // Client HTTP Refit vers HOA5.Backend — requis par CorrelationResultConsumer
        var hoa5Url = ctx.Configuration["Hoa5BackendUrl"] ?? "http://localhost:7072";
        services.AddRefitClient<IHoa5BackendApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(hoa5Url));

        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());

        services.AddConsumer<CorrelationResultConsumer>();

        services.ConfigureAzureProviders(new VisualStudioCredential());
    })
    .Build();

await host.RunAsync();








