using System.ComponentModel.DataAnnotations;

namespace RAMQ.Samples.Queue.Simple.Message
{
    public record SimpleMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;

        [Required]
        public string TargetConsumer { get; init; } = string.Empty;

    }

    public class ApiResponse
    {
        public int StatusCode { get; set; }
        public string? Content { get; set; }
        public string? Message { get; set; }
    }
}
