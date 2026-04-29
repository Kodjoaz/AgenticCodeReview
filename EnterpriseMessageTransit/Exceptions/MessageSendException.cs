namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception technique lors de l’envoi d’un message.
    /// </summary>
    [Serializable]
    public class MessageSendException : Exception
    {
        public int? StatusCode { get; }
        public MessageSendException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public MessageSendException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
