namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    [Serializable]
    public class TransitItineraryException : System.Exception
    {
        public int? StatusCode { get; }
        public TransitItineraryException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public TransitItineraryException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
