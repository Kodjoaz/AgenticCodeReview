using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Topic.PubSub.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var configuration = hostContext.Configuration;

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

        // Service de configuration consommateur (une seule impl�mentation)
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
                
        // Consumers — multi-endpoint Topic : seul le target est requis pour la résolution (EndpointResolver).
        services.AddConsumer<CarBookingConsumer>("CarBooking");
        services.AddConsumer<CarCancellationConsumer>("CarCancellation");
        services.AddConsumer<HotelBookingConsumer>("HotelBooking");
        services.AddConsumer<HotelCancellationConsumer>("HotelCancellation");
        services.AddConsumer<FlightBookingConsumer>("FlightBooking");
        services.AddConsumer<FlightCancellationConsumer>("FlightCancellation");

        // Enregistrement centralisé
        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

await builder.Build().RunAsync();

