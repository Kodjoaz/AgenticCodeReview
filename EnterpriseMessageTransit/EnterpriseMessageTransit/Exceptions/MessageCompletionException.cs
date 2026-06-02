namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception technique lors de la complétion d’un message.
    /// </summary>
    [Serializable]
    public class MessageCompletionException : Exception
    {
        public int? StatusCode { get; }
        public MessageCompletionException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public MessageCompletionException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
