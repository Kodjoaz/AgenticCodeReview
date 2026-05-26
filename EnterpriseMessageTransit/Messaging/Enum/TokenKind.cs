using System.Text.Json.Serialization;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TokenKind
    {
        Message,
        File
    }
}
