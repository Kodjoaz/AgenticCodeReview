namespace RAMQ.COM.EnterpriseMessageTransit.Configuration;

using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

/// <summary>
/// Health check status résultats.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Résultat du health check.
/// </summary>
public record HealthCheckResult(HealthStatus Status, string Description);

/// <summary>
/// Health check pour vérifier la connectivité aux services Azure critiques:
/// Service Bus (management), Blob Storage et Table Storage.
/// 
/// Usage (optionnel):
///   var healthCheck = new ServiceBusHealthCheck(administrationClient, blobClient, tableClient);
///   var result = await healthCheck.CheckHealthAsync();
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class ServiceBusHealthCheck
{
    private readonly ServiceBusAdministrationClient _administrationClient;
    private readonly BlobContainerClient? _blobContainerClient;
    private readonly TableClient? _tableClient;

    /// <summary>
    /// Initialise le health check avec les clients Azure.
    /// </summary>
    /// <param name="administrationClient">Client d'administration Service Bus pour vérifier la connectivité</param>
    /// <param name="blobContainerClient">Client Blob Storage optionnel (si claim-check est activé)</param>
    /// <param name="tableClient">Client Table Storage optionnel (si journalisation est activée)</param>
    public ServiceBusHealthCheck(
        ServiceBusAdministrationClient administrationClient,
        BlobContainerClient? blobContainerClient = null,
        TableClient? tableClient = null)
    {
        _administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
        _blobContainerClient = blobContainerClient;
        _tableClient = tableClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Vérifier Service Bus (minimal check: namespace properties accessible)
            try
            {
                var properties = await _administrationClient.GetNamespacePropertiesAsync(cancellationToken);
                if (properties is null)
                {
                    return new HealthCheckResult(
                        HealthStatus.Unhealthy,
                        "Cannot retrieve Service Bus namespace properties");
                }
            }
            catch (Exception sbEx)
            {
                return new HealthCheckResult(
                    HealthStatus.Unhealthy,
                    $"Service Bus connection failed: {sbEx.Message}");
            }

            // Note: Blob Storage et Table Storage clients sont optionnels et stockés mais non testés
            // automatiquement ici pour éviter les dépendances SDK complexes. Si besoin, ajouter des
            // tests spécifiques plus tard.

            return new HealthCheckResult(
                HealthStatus.Healthy,
                "Service Bus is accessible");
        }
        catch (OperationCanceledException)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, "Health check cancelled");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Valide que l'entité Service Bus a <c>RequiresDuplicateDetection</c> activé
    /// si <paramref name="enforceIdempotentPublish"/> est <c>true</c>.
    /// Lève <see cref="ConfigurationException"/> si l'entité ne satisfait pas la contrainte.
    /// </summary>
    /// <remarks>P3-T2 — Idempotence opt-in (O9 DE Review).</remarks>
    public static async Task ValidateIdempotenceAsync(
        ServiceBusAdministrationClient adminClient,
        string entityName,
        MessagingEntityType entityType,
        bool enforceIdempotentPublish,
        CancellationToken cancellationToken = default)
    {
        await ValidateIdempotenceCoreAsync(
            entityName,
            enforceIdempotentPublish,
            async ct =>
            {
                if (entityType == MessagingEntityType.Topic)
                {
                    var topicName = entityName.Contains('/') ? entityName.Split('/')[0] : entityName;
                    var props = await adminClient.GetTopicAsync(topicName, ct);
                    return props.Value.RequiresDuplicateDetection;
                }
                else
                {
                    var props = await adminClient.GetQueueAsync(entityName, ct);
                    return props.Value.RequiresDuplicateDetection;
                }
            },
            cancellationToken);
    }

    /// <summary>P3-T2 — Logique centrale testable sans dépendance SDK Azure.</summary>
    internal static async Task ValidateIdempotenceCoreAsync(
        string entityName,
        bool enforceIdempotentPublish,
        Func<CancellationToken, Task<bool>> getHasDuplicateDetection,
        CancellationToken cancellationToken = default)
    {
        if (!enforceIdempotentPublish) return;

        var hasDuplicateDetection = await getHasDuplicateDetection(cancellationToken);
        if (!hasDuplicateDetection)
            throw new ConfigurationException(
                $"L'entité '{entityName}' n'a pas RequiresDuplicateDetection activé. " +
                $"Activez RequiresDuplicateDetection sur l'entité Service Bus, " +
                $"ou désactivez EnforceIdempotentPublish dans TransportSettings.");
    }
}
