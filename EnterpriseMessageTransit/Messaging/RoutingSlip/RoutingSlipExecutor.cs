using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Implémentation interne du chef d'orchestre du routing slip pour une étape donnée.
    ///
    /// Flux d'une étape Queue/Topic :
    ///   1. Désérialise le SlipEnvelope depuis le message entrant.
    ///   2. Valide le curseur et désérialise les arguments de l'étape courante (TArgs).
    ///   3. Construit l'ActivityContext et appelle l'activité.
    ///   4. Traite le résultat : Next → publie l'étape suivante, Complete → complète,
    ///      Fault → DLQ, RetryImmediate/RetryExponential → lève l'exception EMT correspondante.
    /// </summary>
    internal sealed class RoutingSlipExecutor<TArgs> : IRoutingSlipExecutor where TArgs : class
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IRoutingSlipActivity<TArgs> _activity;
        private readonly ILogger _logger;
        private readonly IMetricsProvider? _metrics;
        private readonly IJournalProvider? _journal;

        public RoutingSlipExecutor(
            IRoutingSlipActivity<TArgs> activity,
            ILogger<RoutingSlipExecutor<TArgs>> logger,
            IMetricsProvider? metrics = null,
            IJournalProvider? journal = null)
        {
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _metrics  = metrics;
            _journal  = journal;
        }

        // ─── IRoutingSlipExecutor ────────────────────────────────────────────

        public Task ProcessAsync(IMessagingProvider provider, CancellationToken ct)
            => RunAsync(provider, ct);

        public Task ExecuteAsync(IMessagingProvider provider, CancellationToken ct)
            => RunAsync(provider, ct);

        // ─── Pipeline principal ──────────────────────────────────────────────

        private async Task RunAsync(IMessagingProvider provider, CancellationToken ct)
        {
            // 1. Lire l'enveloppe
            var result = provider.DeserializeMessageSafe<SlipEnvelope>();
            if (!result.IsSuccess || result.Value == null)
            {
                _logger.LogError(
                    "RoutingSlipExecutor: impossible de désérialiser le SlipEnvelope. Raison={Reason}: {Message}",
                    result.FailureReason, result.ErrorMessage);
                await provider.DeadLetterMessageAsync(new InvalidOperationException(
                    $"SlipEnvelope désérialization échouée : {result.FailureReason} — {result.ErrorMessage}"), ct);
                return;
            }

            var ctx = result.Value;
            var envelope = ctx.Message
                ?? throw new InvalidOperationException("SlipEnvelope.Message est null après désérialisation.");

            // 2. Valider le curseur
            if (envelope.Cursor < 0 || envelope.Cursor >= envelope.Steps.Count)
            {
                _logger.LogError(
                    "RoutingSlipExecutor: curseur hors limites. Cursor={Cursor}, Total={Total}, SlipId={SlipId}",
                    envelope.Cursor, envelope.Steps.Count, envelope.Header.SlipId);
                await provider.DeadLetterMessageAsync(new InvalidOperationException(
                    $"Curseur hors limites : {envelope.Cursor} / {envelope.Steps.Count}"), ct);
                return;
            }

            var currentStep = envelope.CurrentStep;

            // 3. Désérialiser les arguments de l'étape courante
            TArgs arguments;
            try
            {
                arguments = currentStep.Arguments.Deserialize<TArgs>(_jsonOptions)
                    ?? throw new InvalidOperationException(
                        $"Arguments désérialisés à null pour l'étape '{currentStep.Name}'.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "RoutingSlipExecutor: impossible de désérialiser les arguments en {TArgs} pour l'étape '{Step}', SlipId={SlipId}",
                    typeof(TArgs).Name, currentStep.Name, envelope.Header.SlipId);
                await provider.DeadLetterMessageAsync(ex, ct);
                return;
            }

            // 4. Construire le contexte activité
            var activityCtx = new ActivityContext<TArgs>
            {
                Arguments     = arguments,
                Variables     = envelope.Variables,
                SlipId        = envelope.Header.SlipId,
                CorrelationId = ctx.CorrelationId ?? envelope.Header.CorrelationId,
                Attempt       = ctx.Attempt,
                StepName      = currentStep.Name,
                StepIndex     = envelope.Cursor,
                TotalSteps    = envelope.Steps.Count,
                ClaimCheckToken = ctx.GetMessageToken()
            };

            // R13 — BeginScope : SlipId/StepName/CorrelationId dans customDimensions de tous les logs de cette étape.
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["SlipId"]        = envelope.Header.SlipId,
                ["SlipName"]      = envelope.Header.SlipName,
                ["StepName"]      = currentStep.Name,
                ["StepIndex"]     = envelope.Cursor,
                ["CorrelationId"] = ctx.CorrelationId ?? envelope.Header.CorrelationId,
                ["Attempt"]       = ctx.Attempt
            });

            _logger.LogInformation(
                "RoutingSlipExecutor: début étape {Step} ({Index}/{Total}), SlipId={SlipId}, Attempt={Attempt}",
                currentStep.Name, envelope.Cursor + 1, envelope.Steps.Count,
                envelope.Header.SlipId, ctx.Attempt);

            // 5. Appeler l'activité
            ActivityResult activityResult;
            using var stepActivity = MessagingActivitySource.Source.StartActivity(
                "routing_slip.step",
                ActivityKind.Internal);
            stepActivity?.SetTag("slip.id",       envelope.Header.SlipId);
            stepActivity?.SetTag("slip.name",     envelope.Header.SlipName);
            stepActivity?.SetTag("slip.step",     currentStep.Name);
            stepActivity?.SetTag("slip.cursor",   envelope.Cursor);
            stepActivity?.SetTag("slip.total",    envelope.Steps.Count);

            try
            {
                activityResult = await _activity.ExecuteAsync(activityCtx, ct);
            }
            catch (Exception ex)
            {
                stepActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex,
                    "RoutingSlipExecutor: exception non gérée dans l'activité {Activity}, étape '{Step}', SlipId={SlipId}",
                    _activity.GetType().Name, currentStep.Name, envelope.Header.SlipId);
                await provider.DeadLetterMessageAsync(ex, ct);
                return;
            }

            // 6. Traiter le résultat
            switch (activityResult)
            {
                case ActivityResult.NextResult next:
                    await SafeJournalAsync(JournalEntry.ForSlipStep(
                        envelope.Header.SlipId, envelope.Header.SlipName,
                        envelope.Cursor, currentStep.Name, SlipStepStatus.Completed,
                        currentStep.EntityName, ctx.CorrelationId ?? envelope.Header.CorrelationId), ct);
                    await HandleNextAsync(provider, ctx, envelope, next, ct);
                    break;

                case ActivityResult.CompleteResult:
                    _logger.LogInformation(
                        "RoutingSlipExecutor: Complete explicite à l'étape '{Step}', SlipId={SlipId}",
                        currentStep.Name, envelope.Header.SlipId);
                    await SafeJournalAsync(JournalEntry.ForSlipComplete(
                        envelope.Header.SlipId, envelope.Header.SlipName,
                        envelope.Steps.Count, currentStep.EntityName,
                        ctx.CorrelationId ?? envelope.Header.CorrelationId), ct);
                    await provider.CompleteMessageAsync(ct);
                    stepActivity?.SetStatus(ActivityStatusCode.Ok);
                    break;

                case ActivityResult.FaultResult fault:
                    _logger.LogError(fault.Exception,
                        "RoutingSlipExecutor: Fault à l'étape '{Step}', SlipId={SlipId}",
                        currentStep.Name, envelope.Header.SlipId);
                    stepActivity?.SetStatus(ActivityStatusCode.Error, fault.Exception.Message);
                    using (var compensationActivity = MessagingActivitySource.Source.StartActivity(
                        "routing_slip.compensation",
                        ActivityKind.Internal,
                        stepActivity?.Context ?? default))
                    {
                        compensationActivity?.SetTag("slip.id",            envelope.Header.SlipId);
                        compensationActivity?.SetTag("slip.name",          envelope.Header.SlipName);
                        compensationActivity?.SetTag("slip.step",          currentStep.Name);
                        compensationActivity?.SetTag("slip.cursor",        envelope.Cursor);
                        compensationActivity?.SetTag("compensation.reason", fault.Exception.GetType().Name);
                        compensationActivity?.SetTag("exception.message",  fault.Exception.Message);
                        compensationActivity?.SetStatus(ActivityStatusCode.Error, fault.Exception.Message);
                        _metrics?.IncrementRoutingSlipCompensation(
                            envelope.Header.SlipName,
                            fault.Exception.GetType().Name);
                        await SafeJournalAsync(JournalEntry.ForSlipStep(
                            envelope.Header.SlipId, envelope.Header.SlipName,
                            envelope.Cursor, currentStep.Name, SlipStepStatus.Faulted,
                            currentStep.EntityName, ctx.CorrelationId ?? envelope.Header.CorrelationId,
                            deadLetterReason: fault.Exception.Message), ct);
                        await SafeJournalAsync(JournalEntry.ForSlipCompensation(
                            envelope.Header.SlipId, envelope.Header.SlipName,
                            envelope.Cursor, currentStep.Name, currentStep.EntityName,
                            ctx.CorrelationId ?? envelope.Header.CorrelationId,
                            compensationReason: fault.Exception.Message), ct);
                    }
                    await provider.DeadLetterMessageAsync(fault.Exception, ct);
                    break;

                case ActivityResult.RetryImmediateResult retryImmediate:
                    _logger.LogWarning(
                        "RoutingSlipExecutor: RetryImmediate à l'étape '{Step}', Raison={Reason}, SlipId={SlipId}",
                        currentStep.Name, retryImmediate.Reason, envelope.Header.SlipId);
                    throw new ImmediateRetryException(retryImmediate.Reason);

                case ActivityResult.RetryExponentialResult retryExponential:
                    _logger.LogWarning(
                        "RoutingSlipExecutor: RetryExponential à l'étape '{Step}', Raison={Reason}, SlipId={SlipId}",
                        currentStep.Name, retryExponential.Reason, envelope.Header.SlipId);
                    throw new ExponentialRetryException(retryExponential.Reason,
                        retryExponential.InnerException!);

                default:
                    _logger.LogError(
                        "RoutingSlipExecutor: type de résultat inconnu {Type} pour l'étape '{Step}', SlipId={SlipId}",
                        activityResult.GetType().Name, currentStep.Name, envelope.Header.SlipId);
                    await provider.DeadLetterMessageAsync(
                        new InvalidOperationException($"Type de résultat inconnu : {activityResult.GetType().Name}"),
                        ct);
                    break;
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private async Task HandleNextAsync(
            IMessagingProvider provider,
            MessageTransitContext<SlipEnvelope> ctx,
            SlipEnvelope envelope,
            ActivityResult.NextResult next,
            CancellationToken ct)
        {
            // Dernière étape → Complete automatique + journal ForSlipComplete
            if (envelope.IsLastStep)
            {
                _logger.LogInformation(
                    "RoutingSlipExecutor: dernière étape '{Step}' terminée, slip complet. SlipId={SlipId}",
                    envelope.CurrentStep.Name, envelope.Header.SlipId);
                await SafeJournalAsync(JournalEntry.ForSlipComplete(
                    envelope.Header.SlipId, envelope.Header.SlipName,
                    envelope.Steps.Count, envelope.CurrentStep.EntityName,
                    ctx.CorrelationId ?? envelope.Header.CorrelationId), ct);
                await provider.CompleteMessageAsync(ct);
                return;
            }

            // Enrichir les variables partagées
            Dictionary<string, JsonElement> mergedVariables;
            try
            {
                mergedVariables = MergeVariables(envelope.Variables, next.EnrichVariables);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                _logger.LogError(ex,
                    "RoutingSlipExecutor: enrichissement des variables échoué à l'étape '{Step}', SlipId={SlipId}",
                    envelope.CurrentStep.Name, envelope.Header.SlipId);
                await provider.DeadLetterMessageAsync(ex, ct);
                return;
            }

            // Construire l'enveloppe suivante (immuable par construction)
            int nextCursor  = envelope.Cursor + 1;
            var updatedSteps = BuildUpdatedSteps(envelope.Steps, envelope.Cursor);
            var nextEnvelope = new SlipEnvelope
            {
                Header    = envelope.Header with { CorrelationId = ctx.CorrelationId ?? envelope.Header.CorrelationId },
                Steps     = updatedSteps,
                Cursor    = nextCursor,
                Variables = mergedVariables
            };

            var nextStep = nextEnvelope.Steps[nextCursor];

            // Calculer le target de routage
            string nextTarget = ResolveTarget(nextStep);

            var nextCtx = new MessageTransitContext<SlipEnvelope>
            {
                MessageId     = ctx.MessageId,
                CorrelationId = ctx.CorrelationId ?? envelope.Header.CorrelationId,
                SessionId     = ctx.SessionId,
                Message       = nextEnvelope
            };

            var props = BuildProperties(nextStep);
            var options = new MessagingOptions { Target = nextTarget, Properties = props };

            _logger.LogInformation(
                "RoutingSlipExecutor: avance vers '{Next}' ({NextIdx}/{Total}), SlipId={SlipId}",
                nextStep.Name, nextCursor + 1, envelope.Steps.Count, envelope.Header.SlipId);

            await provider.SendAsync(nextCtx, options, ct);
            await provider.CompleteMessageAsync(ct);
        }

        /// <summary>
        /// Retourne le target de routage pour l'étape suivante.
        /// - Queue  : stepName (= Target dans AppSettings.Endpoints du worker)
        /// - Topic  : aussi stepName — Consumer/Action sont passés dans les Properties
        /// </summary>
        private static string ResolveTarget(SlipStep step)
            => step.Name;

        /// <summary>
        /// Propriétés Service Bus publiées sur le message de l'étape suivante.
        /// Consumer et Action permettent aux abonnements Topic de filtrer via règles SQL.
        /// </summary>
        private static Dictionary<string, object> BuildProperties(SlipStep step)
        {
            var props = new Dictionary<string, object>
            {
                ["EntityType"] = step.EntityType.ToString(),
                ["EntityName"] = step.EntityName
            };

            if (step.EntityType == MessagingEntityType.Topic && step.Subscription != null)
            {
                props["Consumer"] = step.Subscription.Consumer;
                if (!string.IsNullOrWhiteSpace(step.Subscription.Action))
                    props["Action"] = step.Subscription.Action;
            }

            return props;
        }

        /// <summary>Copie les étapes en marquant l'étape courante comme Completed.</summary>
        private static SlipStep[] BuildUpdatedSteps(IReadOnlyList<SlipStep> steps, int completedCursor)
        {
            var updated = new SlipStep[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                updated[i] = i == completedCursor
                    ? steps[i] with { Status = SlipStepStatus.Completed }
                    : i == completedCursor + 1
                        ? steps[i] with { Status = SlipStepStatus.Active }
                        : steps[i];
            }
            return updated;
        }

        private const int MaxVariableCount = 50;
        private const int MaxVariableBytes  = 4096; // 4 Ko

        /// <summary>
        /// Fusionne les variables existantes avec les nouvelles valeurs de l'enrichissement.
        /// Les valeurs existantes sont préservées. Les nouvelles clés (ou écrasées) sont ajoutées.
        /// </summary>
        /// <exception cref="InvalidOperationException">Si le nombre de clés dépasse 50 ou la taille sérialisée dépasse 4 Ko.</exception>
        /// <exception cref="JsonException">Si une valeur de l'enrichissement ne peut pas être sérialisée en JSON.</exception>
        private static Dictionary<string, JsonElement> MergeVariables(
            Dictionary<string, JsonElement> existing,
            Action<IDictionary<string, object>>? enrich)
        {
            if (enrich == null)
                return existing;

            var enrichDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            enrich(enrichDict);

            // Copie existantes + fusionne les nouvelles
            var merged = new Dictionary<string, JsonElement>(existing, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in enrichDict)
            {
                merged[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
            }

            if (merged.Count > MaxVariableCount)
                throw new InvalidOperationException(
                    $"Variables limit exceeded: {merged.Count} clés (max {MaxVariableCount}).");

            var serializedBytes = System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(merged));
            if (serializedBytes > MaxVariableBytes)
                throw new InvalidOperationException(
                    $"Variables limit exceeded: {serializedBytes} octets sérialisés (max {MaxVariableBytes}).");

            return merged;
        }

        /// <summary>
        /// Pattern A5 — écriture journal non-bloquante pour le Routing Slip.
        /// Un échec de journal ne propage jamais vers le caller.
        /// </summary>
        private async Task SafeJournalAsync(JournalEntry entry, CancellationToken ct)
        {
            if (_journal is null) return;
            try   { await _journal.WriteRecordAsync(entry, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "RoutingSlipExecutor: journal step failed (pattern A5). SlipId={SlipId} Step={Step}", entry.SlipId, entry.StepName); }
        }
    }
}
