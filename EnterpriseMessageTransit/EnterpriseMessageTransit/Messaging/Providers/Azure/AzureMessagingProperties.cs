namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    public static class AzureMessagingProperties
    {
        public const string ReferralCount = "ReferralCount";

        // Consolidation : Consumer et Action sont définis dans MessagePropertyKeys (couche neutre).
        // Ces alias évitent la duplication de magic strings tout en préservant la rétrocompatibilité.
        public const string Consumer = MessagePropertyKeys.Consumer;
        public const string Action = MessagePropertyKeys.Action;

        public const string MaxDeliveryExceededReason = "MaxDeliveryCountExceeded";
        public const string SequenceNumberKey = "MessageBrokerSequenceNumber";
        public const string DeadLetterReason = "DeadLetterReason";
        public const string DeadLetterErrorDescription = "DeadLetterErrorDescription";
        public const string MessageId = "MessageId";
        public const string SessionId = "SessionId";
        public const string IsLast = "IsLast";
    }
}
