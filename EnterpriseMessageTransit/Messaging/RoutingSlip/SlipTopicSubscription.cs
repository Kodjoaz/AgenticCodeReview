namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Abonnement Service Bus pour une étape Topic — type propre au routing slip.
    /// Distinct de SubscriptionInfoSettings (config infrastructure).
    /// </summary>
    public sealed record SlipTopicSubscription
    {
        /// <summary>
        /// Nom du consumer abonné au topic.
        /// Publié comme Application Property "Consumer" sur le message Service Bus.
        /// La pipeline DevOps a configuré une règle SQL sur l'abonnement qui filtre sur cette valeur.
        /// Exemple : "EnrichirConsumer"
        /// </summary>
        public required string Consumer { get; init; }

        /// <summary>
        /// Action optionnelle (null = action par défaut, abonnement sans suffixe d'action).
        /// Publié comme Application Property "Action" sur le message Service Bus.
        /// Exemple : "Traiter"
        ///
        /// Abonnement Service Bus résultant :
        ///   Action = null      → "{Consumer}"           (ex: "EnrichirConsumer")
        ///   Action = "Traiter" → "{Consumer}.{Action}"  (ex: "EnrichirConsumer.Traiter")
        /// </summary>
        public string? Action { get; init; }
    }
}
