namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Exception levée lorsque le circuit breaker est ouvert pour une entité Service Bus.
    /// Indique que l'entité est temporairement indisponible et que les envois sont rejetés
    /// pour éviter d'accumuler des retries inutiles.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>Nom de l'entité Service Bus (queue ou topic).</summary>
        public string EntityName { get; }

        /// <summary>Date/heure UTC à laquelle le circuit tentera de passer en HalfOpen.</summary>
        public DateTimeOffset RetriesAllowedAfter { get; }

        public CircuitBreakerOpenException(string entityName, DateTimeOffset retriesAllowedAfter)
            : base($"Circuit breaker is open for entity '{entityName}'. Retries allowed after {retriesAllowedAfter:O}.")
        {
            EntityName = entityName;
            RetriesAllowedAfter = retriesAllowedAfter;
        }
    }
}
