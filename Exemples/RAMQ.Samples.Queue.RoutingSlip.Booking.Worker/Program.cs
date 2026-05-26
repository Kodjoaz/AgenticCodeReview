extern alias AzureIdentity;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Activities;
using RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.Services;
using RAMQ.Samples.RoutingSlip.Booking.Message;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ── OpenTelemetry : traces distribuées ────────────────────────────────────────
        // Enregistre deux sources :
        //   • EMTInstrumentation.SourceName ("RAMQ.COM.EnterpriseMessageTransit") :
        //     spans messaging.send, messaging.consume, routing_slip.step émis par la librairie EMT
        //   • BookingTelemetry.SourceName ("RAMQ.Samples.RoutingSlip.Booking") :
        //     spans métier booking.car.reserve, booking.hotel.reserve, booking.flight.reserve, booking.compensate
        //
        // Exporteurs configurés selon l'environnement :
        //   • OTLP → Jaeger en développement local (docker-compose, port 4317)
        //   • Azure Monitor → Application Insights en staging/production (détection automatique via APPLICATIONINSIGHTS_CONNECTION_STRING + host.json)
        var otlpEndpoint = ctx.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var appInsightsConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        var telemetryBuilder = services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(EMTInstrumentation.SourceName);   // spans EMT (routing_slip.step, messaging.*)
                t.AddSource(BookingTelemetry.SourceName);     // spans métier (booking.*.reserve)
                t.AddHttpClientInstrumentation();             // spans sortants HTTP (appels API externes)

                // Exporteur OTLP (Jaeger local - dev)
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .UseFunctionsWorkerDefaults();   // Configure Azure Functions + capte Application Insights automatiquement

        // Export vers Azure Monitor (prod - seulement si APPLICATIONINSIGHTS_CONNECTION_STRING est configurée)
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            telemetryBuilder.UseAzureMonitorExporter();
        }

        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // Configuration consumer (utilisée par IRoutingSlipExecutor pour setter + compléter)
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());

        // ── Service de compensation ────────────────────────────────────────────────────────────────
        // Scoped : une instance par invocation Function (même durée de vie que les activités).
        services.AddScoped<IBookingCompensationService, BookingCompensationService>();

        // ── Activités Routing Slip ──────────────────────────────────────────────────────────────────
        // stepName == Target dans AppSettings.Endpoints (doit correspondre aux noms
        // de l'activateur et aux noms des triggers Service Bus ci-dessous).
        services.AddRoutingSlipActivity<BookCarActivity,    BookCarArgs>  ("ReserverVoiture");
        services.AddRoutingSlipActivity<BookHotelActivity,  BookHotelArgs>("ReserverHotel");
        services.AddRoutingSlipActivity<BookFlightActivity, BookFlightArgs>("ReserverVol");

        services.ConfigureAzureProviders(new AzureIdentity::Azure.Identity.VisualStudioCredential());
    });

builder.Build().Run();
