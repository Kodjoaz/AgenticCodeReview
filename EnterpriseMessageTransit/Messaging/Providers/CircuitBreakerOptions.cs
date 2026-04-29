namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Configuration du circuit breaker pour les opérations d'envoi Service Bus.
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Nombre d'échecs consécutifs avant d'ouvrir le circuit.
        /// Par défaut : 5.
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Durée pendant laquelle le circuit reste ouvert avant de passer en half-open.
        /// Par défaut : 30 secondes.
        /// </summary>
        public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
    }
}
