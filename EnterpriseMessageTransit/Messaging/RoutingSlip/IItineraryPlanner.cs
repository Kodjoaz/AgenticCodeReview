using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Construit un <see cref="RoutingSlip"/> pré-résolu depuis la configuration d'itinéraire.
    /// Doit être appelé une seule fois au démarrage (idéalement singleton DI).
    /// </summary>
    public interface IItineraryPlanner
    {
        /// <summary>
        /// Résout les identifiants de stage pour chaque <see cref="EndpointSettings"/>
        /// et retourne un <see cref="RoutingSlip"/> immuable.
        /// </summary>
        RoutingSlip Plan(IReadOnlyList<EndpointSettings> itinerary);
    }
}
