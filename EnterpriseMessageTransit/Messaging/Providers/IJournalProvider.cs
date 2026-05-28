using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Interface pour l'implémentation de la publication des événements dans le journal.
    /// </summary>
    public interface IJournalProvider
    {
        Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Écrit un lot d'entrées de journal. Les entrées sont regroupées par PartitionKey
        /// et soumises via <c>TransactionalBatch</c> Azure Table (max 100 par transaction).
        /// Un échec de l'écriture batch ne doit pas faire échouer l'opération principale (pattern A5).
        /// </summary>
        Task WriteBatchAsync(IEnumerable<JournalEntry> entries, CancellationToken cancellationToken = default);
    }
}
