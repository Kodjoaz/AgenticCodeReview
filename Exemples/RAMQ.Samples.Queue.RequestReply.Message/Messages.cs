using System.ComponentModel.DataAnnotations;

namespace RAMQ.Samples.Queue.RequestReply.Message
{
    public record RequestMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;

    }

    public record ReplyMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;
    }
}
