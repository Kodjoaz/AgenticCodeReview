extern alias AzureIdentity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.RoutingSlip.Booking.Message;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
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
        var appInsightsConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        // AppInsights enregistré seulement si la connection string est présente.
        // Sans AppInsights (local) : relay gRPC natif → couleurs func CLI correctes.
        // Avec AppInsights (production) : logs envoyés à Azure Monitor.
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
        }

        var telemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(EMTInstrumentation.SourceName);
                t.AddSource(BookingTelemetry.SourceName);
                t.AddHttpClientInstrumentation();
            })
            .WithMetrics(m =>
            {
                m.AddMeter(EMTInstrumentation.SourceName);
            })
            .UseFunctionsWorkerDefaults();

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
            telemetryBuilder.UseAzureMonitorExporter();

        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleProducerDefaults(ctx.Configuration, new AzureIdentity::Azure.Identity.VisualStudioCredential());

        services.AddProducer<SlipEnvelope>("ReserverVoiture");

        services.AddSingleton<IEndpointResolver, EndpointResolver>();
        services.AddTransient<RoutingSlipBuilder>(sp =>
            new RoutingSlipBuilder("Booking", sp.GetRequiredService<IEndpointResolver>()));
    });

builder.Build().Run();
