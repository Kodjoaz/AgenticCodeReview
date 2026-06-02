namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception pour demander un retry exponentiel du message.
    /// </summary>
    [Serializable]
    public class ExponentialRetryException : System.Exception
    {
        public int? StatusCode { get; }
        public ExponentialRetryException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public ExponentialRetryException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
