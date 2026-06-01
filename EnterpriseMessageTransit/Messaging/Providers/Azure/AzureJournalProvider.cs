using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using System.Diagnostics;
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
                _logger.LogDebug(
                    "Entrée de journal écrite : Partition={Partition} Ligne={Row} IdMessage={MessageId}",
                    entity.PartitionKey, entity.RowKey, entry.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec d'écriture dans le journal. MessageId={MessageId} Partition={Partition}",
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

            // R16-B — Auto-injection Activity.Current si entry ne porte pas déjà les champs trace
            var act        = Activity.Current;
            var traceId    = entry.TraceId      ?? act?.TraceId.ToString();
            var spanId     = entry.SpanId       ?? act?.SpanId.ToString();
            var parentSpanId = entry.ParentSpanId ?? act?.ParentSpanId.ToString();

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["Consumer"]         = entry.Consumer         ?? "(none)",
                ["Action"]           = entry.Action           ?? "(none)",
                ["MessageId"]        = entry.MessageId,
                ["CorrelationId"]    = entry.CorrelationId    ?? string.Empty,
                ["Mode"]             = entry.Mode.ToString(),
                ["StatusCode"]       = entry.StatusCode,
                ["DeliveryCount"]    = entry.DeliveryCount,
                ["MaxDeliveryCount"] = entry.MaxDeliveryCount,
                ["DeadLetterReason"] = entry.DeadLetterReason ?? string.Empty,
                ["EnqueuedTimeUtc"]  = entry.EnqueuedTimeUtc,
                ["SessionId"]        = entry.SessionId        ?? string.Empty,
                ["ApplicationName"]  = entry.ApplicationName  ?? string.Empty,
            };

            // Champs trace OTel (R16-B) — écrits uniquement si non-null pour économiser le stockage
            if (traceId    is not null) entity["TraceId"]      = traceId;
            if (spanId     is not null) entity["SpanId"]       = spanId;
            if (parentSpanId is not null) entity["ParentSpanId"] = parentSpanId;

            // Champs Routing Slip (R16-A) — écrits uniquement si présents
            if (entry.SlipId    is not null) entity["SlipId"]     = entry.SlipId;
            if (entry.SlipName  is not null) entity["SlipName"]   = entry.SlipName;
            if (entry.StepIndex is not null) entity["StepIndex"]  = entry.StepIndex.Value;
            if (entry.StepName  is not null) entity["StepName"]   = entry.StepName;
            if (entry.StepStatus is not null) entity["StepStatus"] = entry.StepStatus.Value.ToString();

            return entity;
        }
    }
}
