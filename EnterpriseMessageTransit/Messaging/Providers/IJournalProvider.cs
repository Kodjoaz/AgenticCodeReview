using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Interface pour l'implémentation de la publication des événements dans le journal.
    /// </summary>
    public interface IJournalProvider
    {
        Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    }
}
