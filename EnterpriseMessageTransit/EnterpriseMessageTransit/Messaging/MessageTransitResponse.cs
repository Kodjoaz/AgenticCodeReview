namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    public class MessageTransitResponse
    {
        /// <summary>
        /// Code HTTP du statut de la réponse
        /// </summary>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.net.httpstatuscode?view=net-10.0"/>
        public int StatusCode { get; set; }

        /// <summary>
        /// Contenu de la réponse
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Indique si l'erreur est transitoire (peut être retentée).
        /// </summary>
        public bool IsTransient { get; set; }

        /// <summary>
        /// Indique si le mécanisme ClaimCheck a été appliqué.
        /// </summary>
        public bool IsClaimCheckApplied { get; set; }

        /// <summary>
        /// Indique si l'échec est permanent (non retentable).
        /// </summary>
        public bool IsPermanentFailure { get; set; }

        /// <summary>
        /// Message d'erreur détaillé (si applicable).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Métadonnées additionnelles (clé/valeur).
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Identifiant de corrélation pour le suivi.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Constructeur par défaut.
        /// </summary>
        public MessageTransitResponse() { }
    }
}



