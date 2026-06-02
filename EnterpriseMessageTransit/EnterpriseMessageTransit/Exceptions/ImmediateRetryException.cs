namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception pour demander un retry immédiat du message.
    /// </summary>
    [Serializable]
    public class ImmediateRetryException : Exception
    {
        public int? StatusCode { get; }
        public ImmediateRetryException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public ImmediateRetryException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
