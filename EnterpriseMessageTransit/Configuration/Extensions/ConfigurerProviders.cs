using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    public static class ConfigurerProviders
    {
        /// <summary>
        /// Enregistre les clients Azure via des usines qui lisent la configuration et utilisent un TokenCredential.
        /// Soit: ServiceBus, DataTable, Blob Storage, Messaging, Journal, AzureStorage, AzureFunctionMessaging, JsonMessageSerializer
        /// </summary>
        /// <param name="services">La collection de services DI.</param>
        /// <param name="credential">
        /// Credential à utiliser pour l'authentification Azure.
        /// Par défaut : <see cref="ManagedIdentityCredential"/> (cible IMDS directement, sans overhead de chaîne).
        /// Pour le développement local, passer <c>new DefaultAzureCredential()</c> explicitement.
        /// </param>
        public static IServiceCollection ConfigureAzureProviders(
            this IServiceCollection services,
            TokenCredential? credential = null)
        {
            // --- Singleton : TokenCredential -------------------------------------------------
            // ManagedIdentityCredential cible l'endpoint IMDS directement (1 round-trip).
            // DefaultAzureCredential essaie ~6 providers en séquence — overhead inutile en production.
            // Dev local : passer new VisualStudioCredential() ou new AzureCliCredential() à l'appelant.
            services.AddSingleton<TokenCredential>(sp => credential ?? new ManagedIdentityCredential());

            // --- Singleton : clients Azure SDK -----------------------------------------------
            // Les clients SDK (ServiceBusClient, BlobServiceClient, TableServiceClient) sont
            // thread-safe et conçus pour être réutilisés. Ils doivent être Singleton pour éviter
            // que le ServiceBusSenderCache (Singleton) ne référence un client disposé en fin de scope.
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IMessageTransitConfigurationService>();
                var tableUri = config.AppSettings?.MessageTransitJournalStoreUri;
                if (string.IsNullOrWhiteSpace(tableUri))
                {
                    throw new InvalidOperationException("AppSettings.MessageTransitJournalStoreUri is not configured.");
                }
                return new TableServiceClient(new Uri(tableUri), sp.GetRequiredService<TokenCredential>());
            });

            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IMessageTransitConfigurationService>();
                var uri = config.BlobStorageSetting?.BlobServiceUri;
                if (string.IsNullOrWhiteSpace(uri))
                {
                    throw new InvalidOperationException("BlobStorageSetting.BlobServiceUri is not configured.");
                }
                return new BlobServiceClient(new Uri(uri), sp.GetRequiredService<TokenCredential>());
            });

            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IMessageTransitConfigurationService>();
                var fqdn = config.AppSettings?.ServiceBusNamespace;
                if (string.IsNullOrWhiteSpace(fqdn))
                {
                    throw new InvalidOperationException("AppSettings.ServiceBusNamespace is not configured.");
                }

                var tokenCredential = sp.GetRequiredService<TokenCredential>();

                // Default ServiceBusClientOptions with sane retry policy — can be overridden later if needed
                var clientOptions = new ServiceBusClientOptions
                {
                    RetryOptions = new ServiceBusRetryOptions
                    {
                        Mode = ServiceBusRetryMode.Exponential,
                        Delay = TimeSpan.FromMilliseconds(800),
                        MaxDelay = TimeSpan.FromSeconds(30),
                        MaxRetries = 5,
                        TryTimeout = TimeSpan.FromSeconds(60)
                    }
                };

                return new ServiceBusClient(fqdn, tokenCredential, clientOptions);
            });

            // --- Singleton : cache des ServiceBusSender --------------------------------------
            // Un sender est une connexion AMQP réutilisable. Le cache (clé = namespace|entity)
            // évite de créer/fermer une connexion par message. Doit être Singleton pour persister
            // entre les invocations de fonctions Azure (processus long-running).
            services.AddSingleton<ServiceBusSenderCache>();

            // --- Singleton : IMetricsProvider ------------------------------------------------
            // Expose des compteurs, histogrammes et jauges pour la monitoring avec OpenTelemetry
            services.AddSingleton<IMetricsProvider, MetricsProvider>();

            // --- Singleton : CircuitBreakerManager -------------------------------------------
            // Un circuit breaker par entité Service Bus. Ouvre le circuit après N échecs consécutifs
            // pour éviter d'accumuler des retries inutiles quand Service Bus est indisponible.
            services.TryAddSingleton(new CircuitBreakerOptions());
            services.AddSingleton<CircuitBreakerManager>();



            // --- Singleton : ISystemClock ----------------------------------------------------
            services.AddSingleton<ISystemClock, DefaultSystemClock>();

            // --- Singleton : AzureServiceBusProviderOptions ----------------------------------
            // Valeurs par défaut (ReplyTimeout = 5 min, etc.). Surcharger avant d'appeler
            // ConfigureAzureProviders() si besoin : services.AddSingleton(new AzureServiceBusProviderOptions { ReplyTimeout = ... });
            services.TryAddSingleton(new AzureServiceBusProviderOptions());

            // --- Scoped : providers et adaptateurs -------------------------------------------
            // Scoped = une instance par invocation de fonction (par scope DI).
            // IMessagingProvider, IMessagingAdapter, IJournalProvider, IStorageProvider ont un
            // état lié au message en cours (BindContext) → ne pas partager entre invocations.
            services.AddScoped<IMessagingProvider, AzureMessagingProvider>();
            services.AddSingleton<IRetryPolicyHandler, RetryPolicyHandler>();
            services.AddScoped<IMessagingAdapter, AzureFunctionMessagingAdapter>();
            services.AddScoped<IJournalProvider, AzureJournalProvider>();
            services.AddScoped<IStorageProvider, AzureStorageProvider>();
            services.AddScoped<IEndpointResolver, EndpointResolver>();
            services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

            return services;
        }
    }
}
