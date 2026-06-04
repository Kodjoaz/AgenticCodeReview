extern alias AzureIdentity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;

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
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        // Lire la connection string depuis la config ou l'env var directement.
        var appInsightsCs =
            ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? string.Empty;

        var credential = new AzureIdentity::Azure.Identity.VisualStudioCredential();

        if (!string.IsNullOrWhiteSpace(appInsightsCs))
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
            services.AddApplicationInsightsTelemetryProcessor<AppInsightsNoiseFilter>();
        }

        var telemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(EMTInstrumentation.SourceName);
                t.AddSource(BookingTelemetry.SourceName);
            })
            .WithMetrics(m =>
            {
                m.AddMeter(EMTInstrumentation.SourceName);
            })
            .UseFunctionsWorkerDefaults();

        if (!string.IsNullOrWhiteSpace(appInsightsCs))
            telemetryBuilder.UseAzureMonitorExporter(o =>
            {
                o.ConnectionString = appInsightsCs;
                o.Credential = credential;
            });

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleConsumerDefaults(ctx.Configuration, credential);

        services.AddScoped<IBookingCompensationService, BookingCompensationService>();

        services.AddRoutingSlipActivity<BookCarActivity,    BookCarArgs>  ("ReserverVoiture");
        services.AddRoutingSlipActivity<BookHotelActivity,  BookHotelArgs>("ReserverHotel");
        services.AddRoutingSlipActivity<BookFlightActivity, BookFlightArgs>("ReserverVol");

    });

builder.Build().Run();

// Filtre les dépendances capturées automatiquement par le SDK AppInsights (bruit infra).
// DependencyTrackingTelemetryModule intercepte TOUS les HttpClient — y compris les appels
// de l'OTel exporter vers /v2/track et le canal gRPC FunctionRpc.
internal sealed class AppInsightsNoiseFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dep)
        {
            var data = dep.Data ?? string.Empty;
            if (data.Contains("applicationinsights.azure.com") ||
                data.Contains("livediagnostics.monitor.azure.com") ||
                data.Contains("FunctionRpc") ||
                data.Contains("/v2/track") ||
                data.Contains("/v2.1/track"))
                return;
        }
        next.Process(item);
    }
}





