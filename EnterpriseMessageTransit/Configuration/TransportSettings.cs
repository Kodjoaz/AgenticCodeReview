using System.ComponentModel.DataAnnotations;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class TransportSettings : IValidatableObject
    {
        [Required] public string EntityName { get; set; } = default!;
        public MessagingEntityType EntityType { get; set; } = MessagingEntityType.None;
        public SubscriptionInfoSettings? Subscription { get; set; }
        public TimeSpan? TTL { get; set; }
        public bool EnableSession { get; set; } = false;

        /// <summary>
        /// Politique de retry pour l'envoi (Producer uniquement).
        /// null = valeur par défaut <see cref="ProducerSendRetryPolicy.Default"/> (3 tentatives, 200 ms).
        /// </summary>
        public ProducerSendRetryPolicy? SendRetry { get; set; }

        /// <summary>
        /// Délai maximal d'un appel PublishAsync / PublishBatchAsync avant annulation automatique.
        /// Défaut : 30 secondes. Assigne <see cref="TimeSpan.Zero"/> pour désactiver (déconseillé en production).
        /// </summary>
        /// <remarks>P3-T3 — borner les envois non-bornés (O10 DE Review).</remarks>
        public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Lorsque <c>true</c>, vérifie au démarrage que l'entité Service Bus cible a
        /// <c>RequiresDuplicateDetection = true</c> avant d'autoriser les publications.
        /// Défaut : <c>false</c> (opt-in).
        /// </summary>
        /// <remarks>P3-T2 — Idempotence opt-in (O9 DE Review).</remarks>
        public bool EnforceIdempotentPublish { get; init; } = false;

        /// <summary>
        /// Taille maximale autorisée pour le corps d'un message individuel dans un batch, en Ko.
        /// Utilisé dans <c>PublishBatchAsync</c> pour détecter les messages individuellement trop volumineux
        /// avant l'envoi atomique, avec suggestion d'utiliser le pattern Claim-Check.
        /// <list type="bullet">
        ///   <item>Azure Service Bus Standard : <c>256</c></item>
        ///   <item>Azure Service Bus Premium : <c>1024</c> (1 Mo)</item>
        ///   <item><c>0</c> = limite déterminée dynamiquement par le broker via <c>ServiceBusMessageBatch.MaxSizeInBytes</c> (défaut)</item>
        /// </list>
        /// </summary>
        public int MaxMessageSizeKb { get; init; } = 0;


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(EntityName))
            {
                yield return new ValidationResult("EntityName is required.", new[] { nameof(EntityName) });
            }
            // Suppression: la présence de Subscription n’est plus exigée ici pour Topic
            // La validation spécifique sera effectuée côté consumer dans BaseMessageTransit.ValidateConfiguration.
        }
    }
}
