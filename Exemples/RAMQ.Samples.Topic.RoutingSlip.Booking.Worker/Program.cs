extern alias AzureIdentity;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker;
using Azure.Identity;
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
using RAMQ.Samples.RoutingSlip.Booking.Message;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Activities;
using RAMQ.Samples.Topic.RoutingSlip.Booking.Worker.Services;

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
        // Sources : EMT (routing_slip.step, messaging.*) + Booking (booking.*.reserve, booking.compensate)
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
                t.AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .UseFunctionsWorkerDefaults();    // Configure Azure Functions + capte Application Insights automatiquement

        // Export vers Azure Monitor (prod - seulement si APPLICATIONINSIGHTS_CONNECTION_STRING est configurée)
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            telemetryBuilder.UseAzureMonitorExporter();
        }

        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());

        // ── Service de compensation ────────────────────────────────────────────────────────────────
        services.AddScoped<IBookingCompensationService, BookingCompensationService>();

        // ── Activités Routing Slip Topic ──────────────────────────────────────────────────────────
        // AddRoutingSlipActivityForTopic : utilise ExecuteAsync (Topic) au lieu de ProcessAsync (Queue).
        // stepName == Target dans AppSettings.Endpoints ET dans le filtre SQL de l'abonnement.
        services.AddRoutingSlipActivityForTopic<BookCarActivity,    BookCarArgs>  ("ReserverVoiture");
        services.AddRoutingSlipActivityForTopic<BookHotelActivity,  BookHotelArgs>("ReserverHotel");
        services.AddRoutingSlipActivityForTopic<BookFlightActivity, BookFlightArgs>("ReserverVol");

        // Enregistrement centralisé des providers Azure.
        // VisualStudioCredential pour le développement local (conforme à l'activateur).
        services.ConfigureAzureProviders(new AzureIdentity::Azure.Identity.VisualStudioCredential());
    });

builder.Build().Run();
