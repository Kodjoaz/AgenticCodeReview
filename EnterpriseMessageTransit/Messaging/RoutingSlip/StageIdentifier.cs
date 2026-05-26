namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Identifiant normalisé d'un stage de routage, de la forme
    /// <c>"{Consumer}"</c> ou <c>"{Consumer}.{Action}"</c>.
    /// Immuable — construit par <see cref="ItineraryPlanner"/> au démarrage.
    /// </summary>
    public sealed record StageIdentifier(string Value)
    {
        /// <summary>Construit un identifiant de stage à partir du Consumer et de l'Action optionnelle.</summary>
        public static StageIdentifier From(string consumer, string? action = null)
            => new(string.IsNullOrWhiteSpace(action) ? consumer : $"{consumer}.{action}");

        /// <inheritdoc/>
        public override string ToString() => Value;
    }
}
