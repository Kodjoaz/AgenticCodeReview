using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.CircuitBreaker.Message;
using RAMQ.Samples.Queue.CircuitBreaker.Worker;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .ConfigureServices((ctx, services) =>
    {
        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleProducerDefaults(ctx.Configuration, new VisualStudioCredential());

        // Circuit Breaker configurable : 3 échecs consécutifs → Open pendant 10 secondes.
        // En production : FailureThreshold=5, OpenDuration=30s (valeurs par défaut).
        // Pour la démo, valeurs réduites pour observer les transitions rapidement.
        services.AddSingleton(new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenDuration     = TimeSpan.FromSeconds(10)
        });

        // Deux producers distincts :
        //   "healthy-queue"  → queue valide — envois toujours réussis
        //   "failing-queue"  → queue simulée en panne (non-existent ou accès refusé)
        services.AddProducer<CircuitBreakerMessage>("healthy-queue");
        services.AddProducer<CircuitBreakerMessage>("failing-queue");

        services.AddHostedService<DoWork>();
    });

builder.Build().Run();
