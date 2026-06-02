using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    public class TokenMessage
    {
        public TokenKind Kind { get; set; }
        public string ContentType { get; set; } = default!;
        public string Reference { get; set; } = default!;
        public long? SizeBytes { get; set; }
    }
}
