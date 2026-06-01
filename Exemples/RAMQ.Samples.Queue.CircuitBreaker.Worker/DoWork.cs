using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.Samples.Queue.CircuitBreaker.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.CircuitBreaker.Worker
{
    /// <summary>
    /// Démontre le Circuit Breaker EMT en 3 phases observables :
    ///
    /// PHASE 1 — Closed (normal)
    ///   Les envois vers "healthy-queue" réussissent toujours.
    ///   Les envois vers "failing-queue" commencent à échouer.
    ///   Après FailureThreshold=3 échecs consécutifs → circuit s'ouvre.
    ///
    /// PHASE 2 — Open (circuit ouvert)
    ///   Chaque tentative vers "failing-queue" lève CircuitBreakerOpenException
    ///   immédiatement, sans toucher Service Bus.
    ///   Le circuit reste Open pendant OpenDuration=10 secondes.
    ///   → Métriques OTel : circuit_state{entity="failing-queue"}=1 (Open)
    ///                       circuit_transitions_total{from=Closed,to=Open}++
    ///
    /// PHASE 3 — HalfOpen → Closed (rétablissement)
    ///   Après OpenDuration, une seule tentative (test probe) est autorisée.
    ///   Si elle réussit → Closed → reprise normale.
    ///   Si elle échoue → retour Open pour OpenDuration supplémentaire.
    ///   → Métriques OTel : circuit_state=0 (Closed) ou 1 (Open)
    ///                       circuit_transitions_total{from=HalfOpen,to=Closed}++
    ///
    /// Configuration : FailureThreshold=3, OpenDuration=10s (voir Program.cs)
    /// </summary>
    public class DoWork : BackgroundService
    {
        private readonly ILogger<DoWork> _logger;
        private readonly IMessageProducer<CircuitBreakerMessage> _healthyProducer;
        private readonly IMessageProducer<CircuitBreakerMessage> _failingProducer;

        private int _iteration;

        public DoWork(
            ILogger<DoWork> logger,
            IMessageProducer<CircuitBreakerMessage> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Les deux producers partagent le même type TMessage mais des targets différentes.
            // Le IMessageTargetMap résout la cible depuis le contexte (MessageType + Target).
            // Pour la démo, on injecte IMessageProducer<CircuitBreakerMessage> — l'un publie
            // vers "healthy-queue", l'autre vers "failing-queue" via PublishOptions.Target.
            _healthyProducer = producer;
            _failingProducer = producer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "╔══════════════════════════════════════════════════════════════╗");
            _logger.LogInformation(
                "║  DÉMO Circuit Breaker EMT                                    ║");
            _logger.LogInformation(
                "║  FailureThreshold=3, OpenDuration=10s                        ║");
            _logger.LogInformation(
                "║  healthy-queue → toujours OK                                 ║");
            _logger.LogInformation(
                "║  failing-queue → panne simulée (CircuitBreakerOpenException) ║");
            _logger.LogInformation(
                "╚══════════════════════════════════════════════════════════════╝");

            while (!stoppingToken.IsCancellationRequested)
            {
                _iteration++;
                var id = Guid.NewGuid();

                // ── Envoi healthy (toujours réussi) ──────────────────────────────
                await PublierHealthyAsync(id, stoppingToken);

                // ── Envoi failing (démontre les 3 phases du circuit breaker) ──────
                await PublierFailingAsync(id, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        private async Task PublierHealthyAsync(Guid id, CancellationToken ct)
        {
            try
            {
                var ctx = new MessageTransitContext<CircuitBreakerMessage>
                {
                    MessageId = $"healthy-{id:N}",
                    Message   = new CircuitBreakerMessage { Id = id, Payload = $"Healthy #{_iteration}", Target = "healthy-queue" }
                };
                await _healthyProducer.PublishAsync(ctx,
                    new RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer.PublishOptions { Properties = new() { ["Target"] = "healthy-queue" } },
                    ct);
                _logger.LogInformation("✅ [healthy-queue] Envoi réussi #{Iteration} MessageId={Id}", _iteration, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [healthy-queue] Erreur inattendue #{Iteration}", _iteration);
            }
        }

        private async Task PublierFailingAsync(Guid id, CancellationToken ct)
        {
            try
            {
                var ctx = new MessageTransitContext<CircuitBreakerMessage>
                {
                    MessageId = $"failing-{id:N}",
                    Message   = new CircuitBreakerMessage { Id = id, Payload = $"Failing #{_iteration}", Target = "failing-queue" }
                };
                await _failingProducer.PublishAsync(ctx,
                    new RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer.PublishOptions { Properties = new() { ["Target"] = "failing-queue" } },
                    ct);
                _logger.LogInformation("✅ [failing-queue] Envoi réussi (circuit rétabli) #{Iteration}", _iteration);
            }
            catch (CircuitBreakerOpenException ex)
            {
                // Le circuit est ouvert — EMT bloque l'envoi immédiatement sans appel réseau.
                // L'application reste résiliente : elle logge et continue.
                _logger.LogWarning(
                    "🔴 [failing-queue] CIRCUIT OUVERT — envoi bloqué. Réouverture dans {Seconds}s. #{Iteration}",
                    (int)(ex.RetriesAllowedAfter - DateTimeOffset.UtcNow).TotalSeconds,
                    _iteration);
                // ← En production : implémenter un fallback (cache, queue locale, alerter l'opérateur)
            }
            catch (RAMQ.COM.EnterpriseMessageTransit.Exceptions.MessageSendException ex)
            {
                // Échec réseau réel → le CircuitBreakerManager enregistre l'échec.
                // Après FailureThreshold échecs consécutifs → circuit s'ouvre.
                _logger.LogError(ex,
                    "💥 [failing-queue] Échec envoi #{Iteration} — ConsecutiveFailures++. Circuit ouvrira après {Threshold} échecs consécutifs.",
                    _iteration, 3);
            }
            catch (OperationCanceledException) { /* arrêt demandé */ }
        }
    }
}
