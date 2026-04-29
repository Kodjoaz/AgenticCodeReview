namespace RAMQ.COM.EnterpriseMessageTransit.Serialization
{
    /// <summary>
    /// Résultat typé d'une désérialisation. Permet de distinguer un payload vide,
    /// malformé, trop volumineux ou valide sans recourir à un <c>null</c> ambigu.
    /// </summary>
    /// <typeparam name="TMessage">Type du message désérialisé</typeparam>
    public sealed class DeserializationResult<TMessage> where TMessage : class
    {
        /// <summary>Message désérialisé. <c>null</c> uniquement si <see cref="IsSuccess"/> est <c>false</c>.</summary>
        public TMessage? Value { get; }

        /// <summary>Indique si la désérialisation a réussi.</summary>
        public bool IsSuccess { get; }

        /// <summary>Raison de l'échec (vide si succès).</summary>
        public DeserializationFailureReason FailureReason { get; }

        /// <summary>Message d'erreur détaillé (vide si succès).</summary>
        public string? ErrorMessage { get; }

        /// <summary>Exception d'origine (null si succès ou payload vide/trop volumineux).</summary>
        public Exception? Exception { get; }

        private DeserializationResult(TMessage? value, bool isSuccess, DeserializationFailureReason reason, string? errorMessage, Exception? exception)
        {
            Value = value;
            IsSuccess = isSuccess;
            FailureReason = reason;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        /// <summary>Crée un résultat de succès.</summary>
        public static DeserializationResult<TMessage> Success(TMessage value)
            => new(value, true, DeserializationFailureReason.None, null, null);

        /// <summary>Crée un résultat d'échec : payload vide ou whitespace.</summary>
        public static DeserializationResult<TMessage> EmptyPayload()
            => new(null, false, DeserializationFailureReason.EmptyPayload, "Payload is empty or whitespace.", null);

        /// <summary>Crée un résultat d'échec : payload trop volumineux.</summary>
        public static DeserializationResult<TMessage> PayloadTooLarge(int length, int maxLength)
            => new(null, false, DeserializationFailureReason.PayloadTooLarge,
                $"Payload too large ({length} chars); max allowed is {maxLength}.", null);

        /// <summary>Crée un résultat d'échec : payload malformé (JSON invalide).</summary>
        public static DeserializationResult<TMessage> Malformed(Exception exception)
            => new(null, false, DeserializationFailureReason.Malformed, exception.Message, exception);

        /// <summary>Crée un résultat d'échec : erreur inattendue.</summary>
        public static DeserializationResult<TMessage> UnexpectedError(Exception exception)
            => new(null, false, DeserializationFailureReason.UnexpectedError, exception.Message, exception);
    }

    /// <summary>
    /// Raison de l'échec de désérialisation.
    /// </summary>
    public enum DeserializationFailureReason
    {
        /// <summary>Pas d'échec — désérialisation réussie.</summary>
        None = 0,

        /// <summary>Payload vide ou composé uniquement de whitespace.</summary>
        EmptyPayload = 1,

        /// <summary>Payload dépasse la taille maximale autorisée.</summary>
        PayloadTooLarge = 2,

        /// <summary>Payload JSON invalide ou non conforme au type cible.</summary>
        Malformed = 3,

        /// <summary>Erreur inattendue lors de la désérialisation.</summary>
        UnexpectedError = 4
    }
}
