using System.ComponentModel.DataAnnotations;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class EndpointSettings : IValidatableObject
    {
        [Required] public TransportSettings Endpoint { get; set; } = default!;
        // Target devient optionnel
        public string? Target { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Endpoint == null)
            {
                yield return new ValidationResult("Endpoint is required.", new[] { nameof(Endpoint) });
                yield break;
            }

            // La règle "Target requis" dépend du nombre d’audiences dans AppSettings.Itinerary.
            // On tente de récupérer le parent AppSettings via validationContext.Items.
            if (validationContext.Items.TryGetValue("AppSettings", out var rootObj) && rootObj is AppSettings root)
            {
                var count = root.Itinerary?.Count ?? 0;
                if (count > 1 && string.IsNullOrWhiteSpace(Target))
                {
                    yield return new ValidationResult(
                        "Target is required when multiple audiences are configured.",
                        new[] { nameof(Target) });
                }
            }
            // Si non fourni, et qu'il s'agit d'une seule audience, Target peut rester null (sera résolu par défaut).
        }
    }
}
