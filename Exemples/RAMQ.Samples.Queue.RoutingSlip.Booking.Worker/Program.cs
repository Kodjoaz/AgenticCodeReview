extern alias AzureIdentity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Diagnostics;
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
            services.AddSingleton<ITelemetryInitializer, ServiceBusCorrelationInitializer>();
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
// Restaure le même operation_Id (TraceId W3C) que l'activateur sur toute la télémétrie
// du worker — y compris le RequestTelemetry de l'invocation Azure Functions.
// En dotnet-isolated, le Service Bus trigger crée une nouvelle Activity racine (TraceId ≠),
// ce qui empêche la corrélation automatique. Ce initializer lit le tag posé par
// BaseConsumer.DeserializeMessageAsync et aligne l'operation_Id sur celui du producteur.
internal sealed class ServiceBusCorrelationInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        var traceparent = Activity.Current?.GetTagItem("messaging.source.traceparent") as string;
        if (traceparent == null) return;
        // Format W3C : "00-{traceId:32hex}-{spanId:16hex}-{flags:2hex}"
        var parts = traceparent.Split('-');
        if (parts.Length < 4 || parts[1].Length != 32) return;
        telemetry.Context.Operation.Id       = parts[1]; // TraceId du producteur
        telemetry.Context.Operation.ParentId = parts[2]; // SpanId du messaging.send
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
                type.Contains("Microsoft.AAD") ||     // VisualStudioCredential.GetToken
                type.Contains("Microsoft.Tables"))    // TableClient.AddEntity (journal infra)
                return;
        }
        next.Process(item);
    }
}





