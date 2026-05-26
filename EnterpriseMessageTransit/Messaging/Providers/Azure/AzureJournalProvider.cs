using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
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
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetTableName()
        {
            var tableName = _config.AppSettings?.MessageTransitJournalName;
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException($"Le nom de la table de journalisation ({nameof(_config.AppSettings.MessageTransitJournalName)}) est absent ou non configuré dans AppSettings.");
            }

            return tableName;
        }

        public async Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        {
            var table = _tableServiceClient.GetTableClient(GetTableName());
            var now = _systemClock.UtcNow.UtcDateTime;

            var partitionKey = entry.Target ?? "(none)";
            var rowKey = $"{now:yyyyMMddHHmmssfff}-{entry.MessageId}-{Guid.NewGuid():N}";

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["Consumer"] = entry.Consumer ?? "(none)",
                ["Action"] = entry.Action ?? "(none)",
                ["MessageId"] = entry.MessageId,
                ["CorrelationId"] = entry.CorrelationId ?? string.Empty,
                ["Mode"] = entry.Mode.ToString(),
                ["StatusCode"] = entry.StatusCode,
                ["DeliveryCount"] = entry.DeliveryCount,
                ["MaxDeliveryCount"] = entry.MaxDeliveryCount,
                ["DeadLetterReason"] = entry.DeadLetterReason ?? string.Empty,
                ["EnqueuedTimeUtc"] = entry.EnqueuedTimeUtc
            };

            try
            {
                await table.AddEntityAsync(entity, cancellationToken);
                _logger.LogInformation("Entrée de journal écrite : Partition={Partition} Ligne={Row} IdMessage={MessageId}", partitionKey, rowKey, entry.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write journal entry MessageId={MessageId} Partition={Partition}", entry.MessageId, entry.Target);
                throw;
            }
        }
    }
}
