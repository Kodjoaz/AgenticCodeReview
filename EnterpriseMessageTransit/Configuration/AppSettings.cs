using System.ComponentModel.DataAnnotations;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class AppSettings : IValidatableObject
    {
        [Required] public string ServiceBusNamespace { get; set; } = default!;
        public bool EnableJsonIndentation { get; set; }
        [Required] public string ApplicationName { get; set; } = default!;
        [Required] public string MessageTransitJournalName { get; set; } = default!;
        [Required] public string MessageTransitJournalStoreUri { get; set; } = default!;
        public string ConnectorUrl { get; set; } = default!;
        [Required] public List<EndpointSettings> Itinerary { get; set; } = default!;
        public ExponentialRetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Nombre maximum de messages acceptés par <c>PublishBatchAsync</c> en un seul appel.
        /// 0 = pas de limite (comportement par défaut, rétrocompatible).
        /// Recommandé : 100–500 selon la taille moyenne des messages.
        /// </summary>
        public int MaxBatchSize { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ApplicationName))
            {
                yield return new ValidationResult("ApplicationName is required.", new[] { nameof(ApplicationName) });
            }
            if (string.IsNullOrWhiteSpace(MessageTransitJournalName))
            {
                yield return new ValidationResult("MessageTransitJournal is required.", new[] { nameof(MessageTransitJournalName) });
            }
            if (Itinerary == null || Itinerary.Count == 0)
            {
                yield return new ValidationResult("Itinerary must contain at least one EndpointSettings.", new[] { nameof(Itinerary) });
            }
        }
    }
}
