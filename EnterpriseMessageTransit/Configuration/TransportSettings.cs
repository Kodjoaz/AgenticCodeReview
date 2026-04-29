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
