namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception technique lors de l’échec du dead-lettering.
    /// </summary>
    [Serializable]
    public class DeadLetteringFailedException : System.Exception
    {
        public int? StatusCode { get; }
        public DeadLetteringFailedException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public DeadLetteringFailedException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
