using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Models;

namespace RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Activities;

/// <summary>
/// Activity Function — enregistre l'audit de corrélation TDF (Niveau 3 DFO).
///
/// ════════════════════════════════════════════════════════════════════════
/// DFO — Pourquoi l'observabilité fonctionne DIFFÉREMMENT ici vs l'orchestrateur
/// ════════════════════════════════════════════════════════════════════════
///
/// Règle fondamentale Durable Functions :
///   • L'orchestrateur EST REJOUÉ depuis l'historique Azure Storage chaque fois
///     qu'un événement arrive (WaitForExternalEvent, timer, résultat d'activity).
///   • Une Activity N'EST JAMAIS rejouée — elle s'exécute une seule fois (ou est
///     retriée depuis zéro si elle échoue).
///
/// Conséquences sur le logging :
///
///   ORCHESTRATEUR                     ACTIVITY (ici)
///   ─────────────────────────────     ──────────────────────────────────
///   ctx.CreateReplaySafeLogger()      ILogger<T> injecté ✅ (pas de replay)
///   BeginScope() INTERDIT ❌           BeginScope() autorisé ✅
///   DateTime.Now INTERDIT ❌           DateTime.UtcNow autorisé ✅
///   Task.Delay INTERDIT ❌             Stopwatch, await Task autorisés ✅
///   Tous I/O INTERDITS ❌              HTTP, DB, Storage, logs I/O autorisés ✅
///
/// Pourquoi BeginScope() fonctionne ici (et pas dans l'orchestrateur) :
///   host.json : "enableScopeProperties": true
///   → Les propriétés du scope (InstanceId, SessionId…) sont automatiquement
///     injectées dans customDimensions de chaque trace Application Insights.
///   → Sans enableScopeProperties, BeginScope est ignoré silencieusement.
///
/// Logging classique vs Observabilité (rappel) :
///   LOGGING CLASSIQUE : "Qu'est-ce qui s'est passé ?"
///     → LogInformation / LogError → texte dans AI Traces.
///   OBSERVABILITÉ     : "Pourquoi le système se comporte-t-il ainsi ?"
///     → ActivityLatencyMs mesuré avec Stopwatch → alerte si > SLA.
///     → customDimensions structurés → requêtes KQL sur Session/Exchange.
///
/// En production, remplacer le ILogger par un vrai store d'audit :
///   await _auditStore.InsertAsync(audit, cancellationToken);
///   (Azure Table Storage, Cosmos DB, Event Grid — durable et requêtable)
/// </summary>
public sealed class RecordAuditActivity
{
    private readonly ILogger<RecordAuditActivity> _logger;

    public RecordAuditActivity(ILogger<RecordAuditActivity> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(RecordAuditActivity))]
    public Task RunAsync(
        [ActivityTrigger] CorrelationAuditRecord audit,
        FunctionContext executionContext)
    {
        // DFO — Stopwatch pour mesurer la latence de l'Activity elle-même.
        // Autorisé ici (non-replay). En production : publier ActivityLatencyMs
        // comme métrique custom dans Application Insights pour les alertes SLA.
        var sw = Stopwatch.StartNew();

        // DFO — BeginScope avec customDimensions structurés.
        // Fonctionne ici (Activity non-replay) + host.json enableScopeProperties: true.
        // Ces propriétés s'ajoutent automatiquement à customDimensions de chaque
        // trace AI émise dans ce bloc — aucune répétition dans chaque LogXxx.
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["InstanceId"]    = audit.InstanceId,
            ["SessionId"]     = audit.SessionId,
            ["NumeroEchange"] = audit.NumeroEchange,
            ["Stage"]         = audit.Stage.ToString(),
        });

        // DFO — Persistance de l'audit (stub → ILogger ; en prod → Storage).
        // L'orchestrateur appelle cette Activity avec RetryPolicy(3 × backoff ×2) :
        // l'écriture sera retriée automatiquement si le store est temporairement indisponible.
        var isFailure = audit.ErrorCode is not null;

        if (isFailure)
        {
            // LogError (pas LogWarning) car un échec de corrélation impacte la transaction.
            // customDimensions du scope s'ajoutent automatiquement à cette trace.
            _logger.LogError(
                "Audit corrélation ÉCHEC. Stage={Stage}, ErrorCode={ErrorCode}, " +
                "DurationSec={DurationSec:F1}, InitialMsgId={InitialMsgId}, " +
                "CorrelMsgId={CorrelMsgId}, ErrorMessage={ErrorMessage}",
                audit.Stage, audit.ErrorCode,
                audit.DurationSeconds, audit.InitialMsgId,
                audit.CorrelMsgId, audit.ErrorMessage);
        }
        else
        {
            _logger.LogInformation(
                "Audit corrélation SUCCÈS. Stage={Stage}, DurationSec={DurationSec:F1}, " +
                "InitialMsgId={InitialMsgId}, CorrelMsgId={CorrelMsgId}",
                audit.Stage, audit.DurationSeconds,
                audit.InitialMsgId, audit.CorrelMsgId);
        }

        sw.Stop();

        // DFO — Latence de l'Activity elle-même (pas de la transaction métier).
        // En production : TelemetryClient.TrackMetric("AuditActivityLatencyMs", sw.ElapsedMilliseconds)
        // pour des alertes automatiques si l'écriture du store dépasse le SLA.
        _logger.LogInformation(
            "RecordAuditActivity complétée. ActivityLatencyMs={ActivityLatencyMs}",
            sw.ElapsedMilliseconds);

        return Task.CompletedTask;
    }
}



