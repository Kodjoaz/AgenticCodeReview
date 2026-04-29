namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Interface interne : patterns complémentaires du Producer (ClaimCheck).
    /// internal : ne fait pas partie du contrat public de la librairie.
    /// </summary>
    internal interface IProducerPatterns
    {
        Task PrepareClaimCheckAsync<TMessage>(MessageTransitContext<TMessage> context, Stream? fileStream, string? originalFileName, bool forceClaimcheck, CancellationToken cancellationToken) where TMessage : class;
    }
}
