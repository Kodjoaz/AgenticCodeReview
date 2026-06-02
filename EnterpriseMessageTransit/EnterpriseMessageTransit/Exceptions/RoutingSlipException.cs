namespace RAMQ.COM.EnterpriseMessageTransit.Exceptions
{
    /// <summary>
    /// Exception fonctionnelle liée à une incohérence d’itinéraire (routing slip).
    /// </summary>    
    [Serializable]
    public class RoutingSlipException : Exception
    {
        public int? StatusCode { get; }
        public RoutingSlipException(string message, int? statusCode = null)
            : base(message) => StatusCode = statusCode;

        public RoutingSlipException(string message, Exception innerException, int? statusCode = null)
            : base(message, innerException) => StatusCode = statusCode;
    }
}
