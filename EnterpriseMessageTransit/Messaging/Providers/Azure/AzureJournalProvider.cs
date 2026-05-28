using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Publication des événements dans le journal étant une DataTable Extensions: COMJournalAIS[Palier]
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AzureJournalProvider : IJournalProvider
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly IMessageTransitConfigurationService _config;
        private readonly ILogger<AzureJournalProvider> _logger;
        private readonly ISystemClock _systemClock;

        public AzureJournalProvider(
            TableServiceClient tableServiceClient,
            IMessageTransitConfigurationService config,
            ISystemClock systemClock,
            ILogger<AzureJournalProvider> logger)
        {
            _tableServiceClient = tableServiceClient ?? throw new ArgumentNullException(nameof(tableServiceClient));
            _config             = config             ?? throw new ArgumentNullException(nameof(config));
            _systemClock        = systemClock        ?? throw new ArgumentNullException(nameof(systemClock));
            _logger             = logger             ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetTableName()
        {
            var tableName = _config.AppSettings?.MessageTransitJournalName;
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException(
                    $"Le nom de la table de journalisation ({nameof(_config.AppSettings.MessageTransitJournalName)}) est absent ou non configuré dans AppSettings.");
            }
            return tableName;
        }

        public async Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        {
            var table  = _tableServiceClient.GetTableClient(GetTableName());
            var entity = BuildEntity(entry, _systemClock.UtcNow.UtcDateTime);

            try
            {
                await table.AddEntityAsync(entity, cancellationToken);
                _logger.LogInformation(
                    "Entrée de journal écrite : Partition={Partition} Ligne={Row} IdMessage={MessageId}",
                    entity.PartitionKey, entity.RowKey, entry.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write journal entry MessageId={MessageId} Partition={Partition}",
                    entry.MessageId, entry.Target);
                throw;
            }
        }

        /// <summary>
        /// R6 — Écriture batch via <c>TableClient.SubmitTransactionAsync</c>.
        /// Les entrées sont regroupées par PartitionKey (contrainte Azure Table) et
        /// découpées en tranches de 100 (limite de la transactional batch).
        /// </summary>
        public async Task WriteBatchAsync(IEnumerable<JournalEntry> entries, CancellationToken cancellationToken = default)
        {
            var entryList = entries?.ToList();
            if (entryList == null || entryList.Count == 0) return;

            var table = _tableServiceClient.GetTableClient(GetTableName());
            var now   = _systemClock.UtcNow.UtcDateTime;

            // Contrainte Azure Table : toutes les entités d'une transaction doivent avoir le même PartitionKey
            var byPartition = entryList
                .Select(e => BuildEntity(e, now))
                .GroupBy(e => e.PartitionKey);

            foreach (var group in byPartition)
            {
                var groupEntities = group.ToList();

                // Découpée en tranches de 100 (limite TransactionalBatch Azure Table)
                for (int offset = 0; offset < groupEntities.Count; offset += 100)
                {
                    var chunk = groupEntities.Skip(offset).Take(100).ToList();
                    var actions = chunk
                        .Select(e => new TableTransactionAction(TableTransactionActionType.Add, e))
                        .ToList();

                    try
                    {
                        await table.SubmitTransactionAsync(actions, cancellationToken);
                        _logger.LogInformation(
                            "Journal batch écrit : {Count} entrées Partition={Partition}",
                            chunk.Count, group.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to write journal batch ({Count} entries, Partition={Partition})",
                            chunk.Count, group.Key);
                        throw;
                    }
                }
            }
        }

        private static TableEntity BuildEntity(JournalEntry entry, DateTime now)
        {
            var partitionKey = entry.Target ?? "(none)";
            var rowKey       = $"{now:yyyyMMddHHmmssfff}-{entry.MessageId}-{Guid.NewGuid():N}";

            return new TableEntity(partitionKey, rowKey)
            {
                ["Consumer"]         = entry.Consumer        ?? "(none)",
                ["Action"]           = entry.Action          ?? "(none)",
                ["MessageId"]        = entry.MessageId,
                ["CorrelationId"]    = entry.CorrelationId   ?? string.Empty,
                ["Mode"]             = entry.Mode.ToString(),
                ["StatusCode"]       = entry.StatusCode,
                ["DeliveryCount"]    = entry.DeliveryCount,
                ["MaxDeliveryCount"] = entry.MaxDeliveryCount,
                ["DeadLetterReason"] = entry.DeadLetterReason ?? string.Empty,
                ["EnqueuedTimeUtc"]  = entry.EnqueuedTimeUtc
            };
        }
    }
}
