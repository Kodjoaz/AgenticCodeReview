using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Responsabilité unique : résolution d'endpoint et propagation de trace.
    /// </summary>
    public interface IMessagingEndpointResolver
    {
        EndpointSettings Resolve(string? target);

        /// <summary>
        /// Retourne le <c>traceparent</c> W3C propagé dans les <c>ApplicationProperties</c>,
        /// ou <c>null</c> si absent.
        /// </summary>
        string? GetTraceparent() => null;
    }
}
