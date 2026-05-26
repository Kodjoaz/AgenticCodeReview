using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.Samples.ConfigurationService;
using RAMQ.Samples.Queue.RequestReply.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;

var builder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var configuration = hostContext.Configuration;
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

        // Config consumer
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(sp => sp.GetRequiredService<ConsumerConfigurationService>());

        // Config producer (pour IMessageProducer<ReplyMessage> utilisé par RequestReplyConsumer)
        services.AddSingleton<ProducerConfigurationService>();
        services.AddSingleton<IProducerConfigurationService>(sp => sp.GetRequiredService<ProducerConfigurationService>());

        // Producer de réponses — cible configurée dans local.settings.json sous AppSettings:Endpoints:reply-queue
        services.AddProducer<RAMQ.Samples.Queue.RequestReply.Message.ReplyMessage>("reply-queue");

        // Consumer RequestReply — mono-endpoint (ConsumerConfigurationService = 1 endpoint) : aucun paramètre requis.
        services.AddConsumer<RequestReplyConsumer>();

        services.ConfigureAzureProviders(new VisualStudioCredential());
    });

builder.Build().Run();
