namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Options pour le pattern Claim Check (message volumineux et/ou pièce jointe).
    /// </summary>
    public record ClaimCheckOptions
    {
        /// <summary>
        /// Flux à stocker hors du message (pièce jointe).
        /// </summary>
        public Stream? FileStream { get; init; }

        /// <summary>
        /// Nom de fichier original associé au flux.
        /// </summary>
        public string? OriginalFileName { get; init; }

        /// <summary>
        /// Force l'application du Claim Check même si la taille ne l'exige pas.
        /// </summary>
        public bool ForceClaimCheck { get; init; }

        /// <summary>
        /// Instance par défaut (pas de claim check, pas de pièce jointe).
        /// Utilise static readonly pour éviter les allocations répétées et permettre la comparaison par référence.
        /// </summary>
        public static readonly ClaimCheckOptions None = new();

        /// <summary>
        /// Force le Claim Check sur le payload, sans pièce jointe.
        /// </summary>
        public static ClaimCheckOptions Force() => new() { ForceClaimCheck = true };

        public static ClaimCheckOptions WithAttachment(
            Stream fileStream,
            string originalFileName,
            bool forceClaimCheck = false) =>
            new()
            {
                FileStream = fileStream,
                OriginalFileName = originalFileName,
                ForceClaimCheck = forceClaimCheck
            };
    }
}
