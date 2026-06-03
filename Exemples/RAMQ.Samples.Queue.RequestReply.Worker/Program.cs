using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.MessageTransitHelper;
using RAMQ.Samples.Queue.RequestReply.Message;
using RAMQ.Samples.Queue.RequestReply.Worker;

var builder = new HostBuilder()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // R12 — Boilerplate EMT réduit à un appel.
        services.AddEMTSampleProducerDefaults(hostContext.Configuration, new VisualStudioCredential());

        // Client Request/Reply : envoie sur "request-queue", reçoit les réponses sur "reply-queue"
        services.AddRequestReplyClient<RequestMessage, ReplyMessage>("request-queue", "reply-queue");

        services.AddHostedService<DoWork>();
    });

builder.Build().Run();

