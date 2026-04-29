using System.ComponentModel.DataAnnotations;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class SubscriptionInfoSettings : IValidatableObject
    {
        [Required] public string Consumer { get; set; } = default!;
        public string? Action { get; set; } // Optionnelle

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Consumer))
            {
                yield return new ValidationResult("Consumer is required.", new[] { nameof(Consumer) });
            }
        }
    }
}
