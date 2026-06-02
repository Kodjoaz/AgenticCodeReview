using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;

namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Service hébergé qui valide, au démarrage de l'application, que chaque endpoint
    /// ayant <see cref="TransportSettings.RequiresDuplicateDetection"/> = <c>true</c>
    /// possède bien <c>RequiresDuplicateDetection</c> activé côté Service Bus.
    /// <para>
    /// En cas d'échec, une <see cref="Exceptions.ConfigurationException"/> est levée avant
    /// que l'application accepte du trafic (fast-fail).
    /// </para>
    /// </summary>
    /// <remarks>P3-T2 — branchement de la validation idempotence (O9 DE Review) — 8 mai 2026.</remarks>
    public sealed class IdempotenceValidationService : IHostedService
    {
        private readonly IMessageTransitConfigurationService _config;
        private readonly Func<string, MessagingEntityType, bool, CancellationToken, Task> _validate;
        private readonly ILogger<IdempotenceValidationService> _logger;

        /// <summary>Constructeur de production — délègue à <see cref="ServiceBusHealthCheck.ValidateIdempotenceAsync"/>.</summary>
        public IdempotenceValidationService(
            IMessageTransitConfigurationService config,
            ServiceBusAdministrationClient adminClient,
            ILogger<IdempotenceValidationService> logger)
            : this(config, logger,
                   (entity, type, enforce, ct) =>
                       ServiceBusHealthCheck.ValidateIdempotenceAsync(adminClient, entity, type, enforce, ct))
        {
        }

        /// <summary>Constructeur interne — seam de test.</summary>
        internal IdempotenceValidationService(
            IMessageTransitConfigurationService config,
            ILogger<IdempotenceValidationService> logger,
            Func<string, MessagingEntityType, bool, CancellationToken, Task> validate)
        {
            _config   = config   ?? throw new ArgumentNullException(nameof(config));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        /// <summary>
        /// Parcourt l'itinéraire et valide la déduplication pour chaque endpoint opt-in.
        /// Lève une exception si une entité ne satisfait pas la contrainte.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var endpoints = _config.AppSettings?.Endpoints;
            if (endpoints is null || endpoints.Count == 0)
                return;

            foreach (var entry in endpoints)
            {
                var transport = entry.Endpoint;
                if (transport is null || !transport.RequiresDuplicateDetection)
                    continue;

                _logger.LogInformation(
                    "IdempotenceValidationService: validation de RequiresDuplicateDetection pour l'entité '{EntityName}'.",
                    transport.EntityName);

                await _validate(transport.EntityName, transport.EntityType, true, cancellationToken);

                _logger.LogInformation(
                    "IdempotenceValidationService: '{EntityName}' — RequiresDuplicateDetection confirmé.",
                    transport.EntityName);
            }
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
