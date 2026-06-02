namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception pour envoyer immédiatement le message en dead-letter.
    /// </summary>
    [Serializable]
    public class ImmediateDLQException : System.Exception
    {
        public int? StatusCode { get; }
        public ImmediateDLQException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public ImmediateDLQException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
