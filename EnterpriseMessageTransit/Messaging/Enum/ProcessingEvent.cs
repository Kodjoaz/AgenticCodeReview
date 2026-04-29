using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessingEvent
    {
        Queued,
        Started,
        Processing,
        InProgress = Processing,
        Completed,
        Waiting,
        Failed
    }
}
