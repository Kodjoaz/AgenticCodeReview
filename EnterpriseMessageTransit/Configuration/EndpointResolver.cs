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
    /// <remarks>P3-T6 — les listes dérivées et l'index producer sont pré-calculés une seule fois
    /// (Lazy) pour éliminer les allocations LINQ répétées sur le chemin critique (O15 DE Review).</remarks>
    public class EndpointResolver : IEndpointResolver
    {
        private readonly IMessageTransitConfigurationService _config;
        private bool IsConsumer => _config is IConsumerConfigurationService;

        // P3-T6 — Caches pré-calculés à la première résolution (endpoints immuables après démarrage).
        private readonly Lazy<(
            List<EndpointSettings> Topics,
            List<EndpointSettings> Queues,
            Dictionary<string, EndpointSettings> ProducerIndex)> _cache;

        public EndpointResolver(IMessageTransitConfigurationService config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cache = new Lazy<(List<EndpointSettings>, List<EndpointSettings>, Dictionary<string, EndpointSettings>)>(() =>
            {
                var endpoints = _config.AppSettings?.Endpoints ?? new List<EndpointSettings>();
                ValidateDuplicateTargets(endpoints);
                var topics = endpoints.Where(a => a.Endpoint?.EntityType == MessagingEntityType.Topic).ToList();
                var queues = endpoints.Where(a => a.Endpoint?.EntityType == MessagingEntityType.Queue).ToList();
                var producerIndex = endpoints
                    .Where(a => !string.IsNullOrWhiteSpace(a.Target))
                    .GroupBy(a => a.Target!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                return (topics, queues, producerIndex);
            });
        }

        /// <summary>
        /// Tentative adaptative. Retourne false si non résolu. Ne lève jamais d'exception de résolution sauf doublons.
        /// </summary>
        public bool TryResolve(string? target, string? consumer, string? action, out EndpointSettings? endpoint)
        {
            endpoint = null;
            var endpoints = _config.AppSettings?.Endpoints;
            if (endpoints == null || endpoints.Count == 0)
            {
                return false;
            }

            // Mono-endpoint
            if (endpoints.Count == 1)
            {
                var single = endpoints[0];
                if (string.IsNullOrWhiteSpace(single.Target))
                {
                    single.Target = single.Endpoint.EntityName;
                }
                endpoint = single;
                return endpoint != null;
            }

            // Producer : résolution O(1) via index pré-calculé
            if (!IsConsumer)
            {
                return TryResolveProducer(target, out endpoint);
            }

            // Consumer : résolution par type d'entité (listes pré-calculées, zéro allocation LINQ)
            var (topics, queues, _) = _cache.Value;

            if (topics.Count == 0 && queues.Count > 0)
            {
                return TryResolveConsumerQueue(queues, target, out endpoint);
            }

            return TryResolveConsumerTopic(endpoints, topics, target, consumer, action, out endpoint);
        }

        private bool TryResolveProducer(string? target, out EndpointSettings? endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }
            // O(1) — dictionary lookup sans allocation LINQ
            return _cache.Value.ProducerIndex.TryGetValue(target, out endpoint);
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
