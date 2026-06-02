namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Métadonnées du slip — identité et traçabilité.
    /// Immuable après création par RoutingSlipBuilder.Build().
    /// </summary>
    public sealed record SlipHeader
    {
        /// <summary>
        /// Identifiant unique du slip — identique du début à la fin du workflow.
        /// Généré automatiquement par RoutingSlipBuilder.Build().
        /// </summary>
        public required string SlipId { get; init; }

        /// <summary>
        /// Nom lisible du workflow. Défini à la construction par RoutingSlipBuilder(slipName, ...).
        /// Apparaît dans les logs, métriques et traces.
        /// Exemple : "TraiterDossierBeneficiaire"
        /// </summary>
        public required string SlipName { get; init; }

        /// <summary>Identifiant de corrélation EMT — propagé automatiquement.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>Date et heure UTC de création du slip par l'activateur.</summary>
        public DateTimeOffset CreatedAt { get; init; }
    }
}
