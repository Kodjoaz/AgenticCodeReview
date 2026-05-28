namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Prépare le Claim Check d'un contexte de message avant publication.
    /// Responsabilité unique : upload blob si seuil dépassé, mise à jour des tokens.
    /// </summary>
    public interface IClaimCheckPreparer
    {
        Task PrepareAsync<TMessage>(
            MessageTransitContext<TMessage> context,
            Stream? fileStream,
            string? originalFileName,
            bool forceClaimCheck,
            CancellationToken cancellationToken)
            where TMessage : class;
    }
}
