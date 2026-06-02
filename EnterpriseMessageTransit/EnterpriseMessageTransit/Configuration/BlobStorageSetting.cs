using System.ComponentModel.DataAnnotations;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class BlobStorageSetting : IValidatableObject
    {
        public string BlobServiceUri { get; set; } = default!;
        public string ContainerName { get; set; } = default!;
        public string FolderName { get; set; } = default!;
        public int ClaimCheckThresholdBytes { get; set; } = 256 * 1024;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ContainerName))
            {
                yield return new ValidationResult("ContainerName is required.", new[] { nameof(ContainerName) });
            }
            if (string.IsNullOrWhiteSpace(FolderName))
            {
                yield return new ValidationResult("FolderName is required.", new[] { nameof(FolderName) });
            }
            if (ClaimCheckThresholdBytes <= 0)
            {
                yield return new ValidationResult("ClaimCheckThresholdBytes must be > 0.", new[] { nameof(ClaimCheckThresholdBytes) });
            }
        }
    }
}
