using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions;
using RAMQ.Samples.ConfigurationService;

namespace RAMQ.Samples.MessageTransitHelper
{
    /// <summary>
    /// R12 — Extensions DI partagées pour réduire le boilerplate des samples EMT.
    /// Encapsule la séquence standard : Configure AppSettings + BlobStorageSetting +
    /// ConfigurationService + ConfigureAzureProviders.
    /// </summary>
    public static class EMTSampleExtensions
    {
        /// <summary>
        /// Enregistre les services EMT standards pour un sample producteur.
        /// </summary>
        /// <param name="services">La collection de services DI.</param>
        /// <param name="configuration">Configuration de l'hôte (lit AppSettings et BlobStorageSetting).</param>
        /// <param name="credential">Credential Azure à utiliser (ex: <c>new VisualStudioCredential()</c> en dev).</param>
        public static IServiceCollection AddEMTSampleProducerDefaults(
            this IServiceCollection services,
            IConfiguration configuration,
            TokenCredential? credential = null)
        {
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
            services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

            services.AddSingleton<ProducerConfigurationService>();
            services.AddSingleton<IMessageTransitConfigurationService>(
                sp => sp.GetRequiredService<ProducerConfigurationService>());
            services.AddSingleton<IProducerConfigurationService>(
                sp => sp.GetRequiredService<ProducerConfigurationService>());

            services.ConfigureAzureProviders(credential);
            return services;
        }

        /// <summary>
        /// Enregistre les services EMT standards pour un sample consommateur.
        /// </summary>
        /// <param name="services">La collection de services DI.</param>
        /// <param name="configuration">Configuration de l'hôte (lit AppSettings et BlobStorageSetting).</param>
        /// <param name="credential">Credential Azure à utiliser (ex: <c>new VisualStudioCredential()</c> en dev).</param>
        public static IServiceCollection AddEMTSampleConsumerDefaults(
            this IServiceCollection services,
            IConfiguration configuration,
            TokenCredential? credential = null)
        {
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
            services.Configure<BlobStorageSetting>(configuration.GetSection("BlobStorageSetting"));

            services.AddSingleton<ConsumerConfigurationService>();
            services.AddSingleton<IMessageTransitConfigurationService>(
                sp => sp.GetRequiredService<ConsumerConfigurationService>());
            services.AddSingleton<IConsumerConfigurationService>(
                sp => sp.GetRequiredService<ConsumerConfigurationService>());

            services.ConfigureAzureProviders(credential);
            return services;
        }
    }
}
