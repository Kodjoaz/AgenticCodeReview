using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Diagnostics;
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
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddSimpleConsole(opts => { opts.IncludeScopes = false; opts.TimestampFormat = "HH:mm:ss.fff "; });
        logging.AddFilter("Azure",     LogLevel.Warning);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System",    LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITelemetryInitializer, ConsumerTelemetryInitializer>();
        var appInsightsCs =
            ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(appInsightsCs))
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
            services.AddApplicationInsightsTelemetryProcessor<AppInsightsNoiseFilter>();
            services.AddSingleton<ITelemetryInitializer, ServiceBusCorrelationInitializer>();
        }
        // AppInsights injecte opts.MinLevel = Warning ET des règles Warning.
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

internal sealed class ServiceBusCorrelationInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        var traceparent = Activity.Current?.GetTagItem("messaging.source.traceparent") as string;
        if (traceparent == null) return;
        var parts = traceparent.Split('-');
        if (parts.Length < 4 || parts[1].Length != 32) return;
        telemetry.Context.Operation.Id       = parts[1];
        telemetry.Context.Operation.ParentId = parts[2];
    }
}

internal sealed class AppInsightsNoiseFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dep)
        {
            var data = dep.Data ?? string.Empty;
            var type = dep.Type ?? string.Empty;
            if (data.Contains("applicationinsights.azure.com") ||
                data.Contains("livediagnostics.monitor.azure.com") ||
                data.Contains("login.microsoftonline.com") ||
                data.Contains("FunctionRpc") ||
                data.Contains("/v2/track") ||
                data.Contains("/v2.1/track") ||
                data.Contains("/Settlement/") ||
                type.Contains("Microsoft.AAD") ||
                type.Contains("Microsoft.Tables") ||
                type.StartsWith("Azure Service Bus", StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith("Azure table", StringComparison.OrdinalIgnoreCase) ||
                type == "InProc")
                return;
        }
        next.Process(item);
    }
}