using System;
using System.Collections.Generic;
using System.Linq;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Résout un endpoint en mode adaptatif (producer ou consumer) via TryResolve.
    /// Règles consumer multi‑endpoints:
    ///   Queue: Target obligatoire.
    ///   Topic: Target -> Consumer -> Consumer+Action.
    /// Producer: uniquement Target (si multi‑endpoints Target requis).
    /// </summary>
    public class EndpointResolver : IEndpointResolver
    {
        private readonly IMessageTransitConfigurationService _config;
        private bool IsConsumer => _config is IConsumerConfigurationService;

        public EndpointResolver(IMessageTransitConfigurationService config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Tentative adaptative. Retourne false si non résolu. Ne lève jamais d'exception de résolution sauf doublons.
        /// </summary>
        public bool TryResolve(string? target, string? consumer, string? action, out EndpointSettings? endpoint)
        {
            endpoint = null;
            var itin = _config.AppSettings?.Itinerary;
            if (itin == null || itin.Count == 0)
            {
                return false;
            }

            // Mono-endpoint
            if (itin.Count == 1)
            {
                var single = itin[0];
                if (string.IsNullOrWhiteSpace(single.Target))
                {
                    single.Target = single.Endpoint.EntityName;
                }
                endpoint = single;
                return endpoint != null;
            }

            // Producer : résolution par target uniquement
            if (!IsConsumer)
            {
                return TryResolveProducer(itin, target, out endpoint);
            }

            // Consumer : résolution par type d'entité
            var topics = itin.Where(a => a.Endpoint?.EntityType == MessagingEntityType.Topic).ToList();
            var queues = itin.Where(a => a.Endpoint?.EntityType == MessagingEntityType.Queue).ToList();

            if (topics.Count == 0 && queues.Count > 0)
            {
                return TryResolveConsumerQueue(queues, target, out endpoint);
            }

            return TryResolveConsumerTopic(itin, topics, target, consumer, action, out endpoint);
        }

        private static bool TryResolveProducer(List<EndpointSettings> itin, string? target, out EndpointSettings? endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }
            endpoint = itin.FirstOrDefault(a => string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));
            return endpoint != null;
        }

        private static bool TryResolveConsumerQueue(List<EndpointSettings> queues, string? target, out EndpointSettings? endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }
            endpoint = queues.FirstOrDefault(a => string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));
            return endpoint != null;
        }

        private static bool TryResolveConsumerTopic(
            List<EndpointSettings> itin,
            List<EndpointSettings> topics,
            string? target,
            string? consumer,
            string? action,
            out EndpointSettings? endpoint)
        {
            endpoint = null;

            // 1. Via Target
            if (!string.IsNullOrWhiteSpace(target))
            {
                var byTarget = itin.Where(a => string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase)).ToList();

                if (byTarget.Count == 1)
                {
                    endpoint = byTarget[0];
                    return true;
                }
                if (byTarget.Count > 1)
                {
                    return false; // ambiguïté
                }
            }

            // 2. Via Consumer (topics)
            if (!string.IsNullOrWhiteSpace(consumer))
            {
                var byConsumer = topics.Where(a => a.Endpoint?.Subscription != null &&
                    string.Equals(a.Endpoint.Subscription.Consumer, consumer, StringComparison.OrdinalIgnoreCase)).ToList();

                if (byConsumer.Count == 1)
                {
                    endpoint = byConsumer[0];
                    return true;
                }
                if (byConsumer.Count > 1)
                {
                    if (!string.IsNullOrWhiteSpace(action))
                    {
                        var byConsumerAction = byConsumer.Where(a =>
                            !string.IsNullOrWhiteSpace(a.Endpoint!.Subscription!.Action) &&
                            string.Equals(a.Endpoint.Subscription.Action, action, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (byConsumerAction.Count == 1)
                        {
                            endpoint = byConsumerAction[0];
                            return true;
                        }
                        return false; // 0 ou >1 => échec
                    }
                    return false; // ambiguïté sans action
                }
                if (byConsumer.Count == 0 && !string.IsNullOrWhiteSpace(action))
                {
                    var byCA = topics.Where(a => a.Endpoint?.Subscription != null &&
                        string.Equals(a.Endpoint.Subscription.Consumer, consumer, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Endpoint.Subscription.Action, action, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (byCA.Count == 1)
                    {
                        endpoint = byCA[0];
                        return true;
                    }
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(action))
            {
                return false; // action sans consumer
            }

            return false; // non résolu
        }

        /// <summary>
        /// Valide qu'aucun doublon de target n'existe dans l'itinéraire.
        /// Cette méthode est appelée une seule fois au démarrage (depuis ValidateConfiguration).
        /// </summary>
        public static void ValidateDuplicateTargets(List<EndpointSettings> itin)
        {
            var dupTargets = itin
                .Where(a => !string.IsNullOrWhiteSpace(a.Target))
                .GroupBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (dupTargets.Count > 0)
            {
                throw new InvalidOperationException($"Duplicate target(s) detected: {string.Join(", ", dupTargets)}");
            }
        }
    }
}
