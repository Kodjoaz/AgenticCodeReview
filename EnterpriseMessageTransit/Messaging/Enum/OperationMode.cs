namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum
{
    /// <summary>
    /// Mode d’écriture dans le journal/BAM ou de traitement technique.
    /// </summary>
    public enum OperationMode
    {
        PUBLISH,
        REQUEST_REPLY,
        COMPLETE,
        RETRY,
        DLQ,
        DEFER
    }
}
