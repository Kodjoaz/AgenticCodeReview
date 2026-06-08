using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Topic.PubSub.Consumer;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddSimpleConsole(opts => { opts.IncludeScopes = false; opts.TimestampFormat = "HH:mm:ss.fff "; });
        logging.AddFilter("Azure",     LogLevel.Warning);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System",    LogLevel.Warning);
    })
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
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
        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(ctx.Configuration, new VisualStudioCredential());

        services.AddConsumer<CarBookingConsumer>("CarBooking");
        services.AddConsumer<CarCancellationConsumer>("CarCancellation");
        services.AddConsumer<HotelBookingConsumer>("HotelBooking");
        services.AddConsumer<HotelCancellationConsumer>("HotelCancellation");
        services.AddConsumer<FlightBookingConsumer>("FlightBooking");
        services.AddConsumer<FlightCancellationConsumer>("FlightCancellation");
    });

await builder.Build().RunAsync();

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
                type.StartsWith("Azure table", StringComparison.OrdinalIgnoreCase))
                return;
        }
        next.Process(item);
    }
}