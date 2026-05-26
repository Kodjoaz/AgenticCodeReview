using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RAMQ.Samples.Queue.MultiTarget.Message
{
    public record MultiTargetMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;

        [Required]
        public string Target { get; init; } = string.Empty;

    }

    public record Target4Message
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;
    }

    public record CarMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;
    }

    public record HotelMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;
    }

    public record FlightMessage
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public string Content { get; init; } = string.Empty;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AudienceTarget
    {
        Target1 = 0,
        Target2 = 1,
        Target3 = 2,
        Target4 = 3
    }
}
