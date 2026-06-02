namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Options de publication. Encapsule les propriétés applicatives et les options Claim Check.
    /// Le target est résolu automatiquement via <see cref="IMessageTargetMap"/> (DI)
    /// ou par le fallback mono-audience. Il ne peut pas être surchargé à l'appel.
    /// </summary>
    public record PublishOptions
    {
        /// <summary>
        /// Propriétés applicatives ajoutées au message (Consumer, Action, etc.)
        /// </summary>
        public Dictionary<string, object>? Properties { get; init; }

        /// <summary>
        /// Options Claim Check (pièce jointe, forçage).
        /// </summary>
        public ClaimCheckOptions ClaimCheck { get; init; } = ClaimCheckOptions.None;

        /// <summary>
        /// Instance par défaut (aucun override).
        /// </summary>
        public static PublishOptions Default => new();
    }

    /// <summary>
    /// Options pour le pattern Request/Reply.
    /// Constitué des mêmes propriétés que PublishOptions sans héritage,
    /// afin de préserver l'égalité structurelle des records et de découpler les deux contrats.
    /// </summary>
    public record RequestReplyOptions
    {
        /// <summary>
        /// Propriétés applicatives ajoutées au message (Consumer, Action, etc.)
        /// </summary>
        public Dictionary<string, object>? Properties { get; init; }

        /// <summary>
        /// Options Claim Check (pièce jointe, forçage).
        /// </summary>
        public ClaimCheckOptions ClaimCheck { get; init; } = ClaimCheckOptions.None;

        /// <summary>
        /// Active le mode offline (ne pas attendre la réponse synchrone).
        /// </summary>
        public bool EnableOffline { get; init; }
    }
}
