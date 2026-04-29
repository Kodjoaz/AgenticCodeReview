using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// Classe de base dont tous les objets de gestion d'événement de Message Transit hérite.
    /// </summary>
    /// <typeparam name="TMessage">Représente le contenu de l'événement</typeparam>
    public abstract class BaseMessageTransit<TMessage> where TMessage : class
    {
        protected readonly IMessageTransitConfigurationService Config;
        protected readonly ILogger Logger;
        protected readonly IMessageSerializer Serializer;
        protected readonly IStorageProvider StorageProvider;

        public BaseMessageTransit(
            ILogger logger,
            IMessageTransitConfigurationService config,
            IMessageSerializer serializer,
            IStorageProvider storageProvider)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            StorageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            ValidateConfiguration();
        }

        protected EndpointSettings ResolveAudience(string? explicitTarget = null)
        {
            var itin = Config.AppSettings?.Itinerary;
            if (itin == null || itin.Count == 0)
            {
                throw new ConfigurationException("Itinerary vide ou non configurée.");
            }

            if (itin.Count == 1)
            {
                return itin[0];
            }

            if (!string.IsNullOrWhiteSpace(explicitTarget))
            {
                var audience = itin.FirstOrDefault(a => a.Target == explicitTarget);
                if (audience == null)
                {
                    throw new ConfigurationException($"Target '{explicitTarget}' introuvable dans l’itinéraire.");
                }

                return audience;
            }
            throw new ConfigurationException("Target requis : plusieurs audiences configurées, injectez un target.");
        }

        protected void ValidateConfiguration()
        {
            if (Config.AppSettings == null)
            {
                throw new ConfigurationException("AppSettings manquant.");
            }

            var itinerary = Config.AppSettings.Itinerary;
            if (itinerary == null || itinerary.Count == 0)
            {
                throw new ConfigurationException("Itinerary manquant ou vide.");
            }

            foreach (var aud in itinerary)
            {
                if (aud == null)
                {
                    throw new ConfigurationException("EndpointSettings null dans Itinerary.");
                }

                Validator.ValidateObject(aud, new ValidationContext(aud), true);
                if (aud.Endpoint == null)
                {
                    throw new ConfigurationException($"Endpoint manquant pour Target={aud.Target}");
                }

                Validator.ValidateObject(aud.Endpoint, new ValidationContext(aud.Endpoint), true);
            }

            // Validation des doublons de target (une seule fois au démarrage)
            EndpointResolver.ValidateDuplicateTargets(itinerary);
        }

        protected bool RequiresClaimCheck(int sizeInBytes, bool forceClaimcheck)
        {
            int threshold = Config.BlobStorageSetting?.ClaimCheckThresholdBytes ?? 256 * 1024;
            return forceClaimcheck || sizeInBytes >= threshold;
        }
    }
}
