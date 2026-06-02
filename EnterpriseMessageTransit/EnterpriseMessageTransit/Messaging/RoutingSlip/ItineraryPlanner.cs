using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Construit un <see cref="RoutingSlip"/> pré-résolu depuis <see cref="EndpointSettings"/>.
    /// <para>
    /// La résolution du <c>StageId</c> suit la même logique que l'ancienne méthode
    /// <c>BaseConsumer.FindIndexFromStage</c> :
    /// <list type="bullet">
    /// <item>Topic avec abonnement → <c>Consumer[.Action]</c></item>
    /// <item>Queue ou cible sans abonnement → <c>Target</c></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ItineraryPlanner : IItineraryPlanner
    {
        /// <summary>Instance statique réutilisable — sans état mutable.</summary>
        public static readonly ItineraryPlanner Default = new();

        /// <inheritdoc/>
        public RoutingSlip Plan(IReadOnlyList<EndpointSettings> itinerary)
        {
            if (itinerary is null) throw new ArgumentNullException(nameof(itinerary));
            if (itinerary.Count == 0) return RoutingSlip.Empty;

            var stages = new SlipStage[itinerary.Count];
            for (int i = 0; i < itinerary.Count; i++)
            {
                var ep    = itinerary[i];
                var target  = ep.Target ?? string.Empty;
                var stageId = ResolveStageId(ep);
                stages[i] = new SlipStage(target, stageId);
            }

            return new RoutingSlip(stages);
        }

        private static string ResolveStageId(EndpointSettings ep)
        {
            // Topic avec abonnement → Consumer[.Action]
            var sub = ep.Endpoint?.Subscription;
            if (sub != null && !string.IsNullOrWhiteSpace(sub.Consumer))
            {
                return string.IsNullOrWhiteSpace(sub.Action)
                    ? sub.Consumer
                    : $"{sub.Consumer}.{sub.Action}";
            }

            // Queue ou cible sans abonnement → Target
            return ep.Target ?? string.Empty;
        }
    }
}
