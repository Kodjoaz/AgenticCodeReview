namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    [Serializable]
    public class ConfigurationException : System.Exception
    {
        public int? StatusCode { get; }
        public ConfigurationException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public ConfigurationException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
