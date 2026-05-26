using System.Text.Json;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Une étape dans le routing slip — auto-porteuse de toutes les informations de routing.
    /// Résolu par RoutingSlipBuilder depuis AppSettings.Endpoints via IEndpointResolver.
    /// Les workers n'ont jamais besoin de consulter leur propre config pour router.
    /// </summary>
    public sealed record SlipStep
    {
        /// <summary>
        /// Nom logique de l'étape = Target dans AppSettings.Endpoints = stepName donné à AddStep().
        /// Identique dans les logs, métriques et traces.
        /// Exemple : "ValiderAdmissibilite"
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Nom physique de l'entité Service Bus, résolu depuis Endpoints[i].Endpoint.EntityName.
        /// Jamais renseigné à la main — toujours via IEndpointResolver.
        /// Exemple : "queue-valider-admissibilite" ou "topic-enrichir"
        /// </summary>
        public required string EntityName { get; init; }

        /// <summary>
        /// Type d'entité Service Bus — résolu depuis Endpoints[i].Endpoint.EntityType.
        /// </summary>
        public MessagingEntityType EntityType { get; init; }

        /// <summary>
        /// Abonnement cible pour les étapes Topic. Null pour les étapes Queue.
        /// Consumer et Action sont publiés comme Application Properties Service Bus
        /// lors du Next() pour permettre le filtrage par règles SQL (gérées par DevOps).
        /// </summary>
        public SlipTopicSubscription? Subscription { get; init; }

        /// <summary>
        /// Arguments JSON sérialisés pour cette étape.
        /// Définis une fois par l'activateur — immuables pendant le voyage du slip.
        /// Désérialisés en TArgs par RoutingSlipExecutor avant d'appeler ExecuteAsync.
        /// </summary>
        public JsonElement Arguments { get; init; }

        /// <summary>Statut courant de l'étape : Pending → Active → Completed (ou Faulted).</summary>
        public SlipStepStatus Status { get; init; }
    }
}
