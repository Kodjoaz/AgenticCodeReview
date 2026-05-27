using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Activities;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Models;
using RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator.Options;

namespace RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator;

/// <summary>
/// Orchestrateur Durable Functions — machine à états pour la corrélation TDF.
///
/// ════════════════════════════════════════════════════════════════════════
/// DFO — Trois niveaux d'observabilité dans un orchestrateur Durable Functions
/// ════════════════════════════════════════════════════════════════════════
///
/// Niveau 1 — Logging classique structuré (ILogger replay-safe)
///   • QUOI    : Lignes de texte structurées avec message templates ({InstanceId}, {SessionId}…).
///   • OÙ      : Application Insights Traces → onglet « Logs » dans le portail AI.
///   • QUAND   : À chaque transition d'état, à chaque événement reçu, à chaque erreur.
///   • RÈGLE   : ctx.CreateReplaySafeLogger() est OBLIGATOIRE.
///               Un ILogger injecté produirait des lignes dupliquées à chaque replay
///               de l'orchestrateur (Durable Functions rejoue depuis l'historique).
///               Le logger replay-safe détecte les replays et supprime les doublons.
///   • LIMITE  : Le replay-safe logger ne supporte PAS BeginScope() → les dimensions
///               doivent être inlinées dans chaque message template.
///
/// Niveau 2 — Statut structuré (ctx.SetCustomStatus)
///   • QUOI    : JSON sérialisé, requêtable via l'API Durable Functions.
///   • OÙ      : Table d'historique Azure Storage + API REST Durable Functions.
///   • QUAND   : À chaque transition d'état — AwaitingCorrelation, Correlating,
///               Completed, Failed_Timeout, Failed_Token.
///   • AVANTAGE: Visible en temps réel sans ouvrir AI ni écrire une requête KQL.
///               Les DurationSeconds exposées dans le statut final permettent
///               des alertes SLA directement sur l'API Durable (pas besoin de AI).
///
/// Niveau 3 — Activity d'audit (RecordAuditActivity)
///   • QUOI    : Enregistrement durable, retriable, compensable de chaque résultat.
///   • OÙ      : Actuellement dans les logs (ILogger). En production : Table Storage,
///               Cosmos DB, ou Event Grid pour un trail d'audit requêtable.
///   • QUAND   : À la fin de chaque orchestration (succès ET échec).
///   • AVANTAGE: Survit aux redémarrages, réessayé automatiquement (RetryPolicy × 3),
///               idempotent par design, exécuté hors du contexte de replay.
///
/// ════════════════════════════════════════════════════════════════════════
/// Best practices orchestrateur Durable Functions appliquées
/// ════════════════════════════════════════════════════════════════════════
///   1. DÉTERMINISTE     — Aucun I/O direct. ctx.CurrentUtcDateTime (pas DateTime.Now),
///                         ctx.CreateTimer (pas Task.Delay). Tous les I/O via Activities.
///   2. REPLAY-SAFE      — Logger créé via ctx.CreateReplaySafeLogger (pas injecté).
///   3. STATUT STRUCTURÉ — SetCustomStatus avec OrchestrationStatus typé (queryable).
///   4. DURÉE TRACÉE     — ctx.CurrentUtcDateTime → DurationSeconds dans le statut final.
///   5. ACTIVITY I/O     — Audit via RecordAuditActivity avec RetryPolicy (3 × backoff ×2).
///   6. COMPENSATION     — Audit d'échec enregistré AVANT de lever l'exception.
///   7. CLEANUP TIMER    — timerCts annulé dans tous les chemins (succès, échec, timeout).
///   8. IDEMPOTENCE      — InstanceId = SessionId garantit l'unicité (imposé côté Subscriber).
///   9. OPTIONS          — Timeout externalisé dans IOptions&lt;StateFulOptions&gt; (pas de constante magique).
/// </summary>
public sealed class TdfTransactionOrchestrator
{
    // Politique de retry pour les Activities d'audit (idempotentes par design).
    // DFO : le backoff exponentiel (×2) évite la tempête de retry sous charge.
    private static readonly TaskOptions AuditRetryOptions = TaskOptions.FromRetryPolicy(
        new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0));

    private readonly IOptions<StateFulOptions> _options;

    public TdfTransactionOrchestrator(IOptions<StateFulOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    [Function(nameof(TdfTransactionOrchestrator))]
    public async Task<TransactionCorrelationResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        // ── Entrée ────────────────────────────────────────────────────────────
        var input = ctx.GetInput<EnvoyerLotFichierEvent>()
            ?? throw new InvalidOperationException(
                "Input EnvoyerLotFichierEvent manquant dans l'orchestration.");

        // DFO Niveau 1 — Logging classique structuré.
        // OBLIGATOIRE : CreateReplaySafeLogger évite les doublons lors des replays Durable.
        // NE PAS injecter ILogger<T> dans l'orchestrateur — il produirait N lignes
        // pour le même événement (une par replay depuis l'historique).
        var logger = ctx.CreateReplaySafeLogger<TdfTransactionOrchestrator>();

        // Horodatage de départ via l'historique durable (déterministe, résiste aux replays).
        // JAMAIS DateTime.Now ou DateTime.UtcNow ici — ces valeurs varient à chaque replay
        // et rendraient l'orchestrateur non-déterministe → corruption de l'historique.
        var startedAt = ctx.CurrentUtcDateTime;

        // DFO Niveau 1 — Log d'entrée avec toutes les dimensions de corrélation.
        // Information (pas Debug) : ce log est la trace de référence pour les audits.
        logger.LogInformation(
            "Orchestration TDF démarrée. " +
            "InstanceId={InstanceId}, SessionId={SessionId}, " +
            "NumeroEchange={NumeroEchange}, InitialMsgId={InitialMsgId}",
            ctx.InstanceId, input.SessionId, input.NumeroEchange, input.MessageId);

        // DFO Niveau 2 — Statut initial : visible immédiatement dans Durable Monitor.
        // Un opérateur peut voir "AwaitingCorrelation" et savoir que l'Étape 3 est attendue.
        ctx.SetCustomStatus(new OrchestrationStatus
        {
            Stage         = OrchestrationStage.AwaitingCorrelation,
            SessionId     = input.SessionId,
            NumeroEchange = input.NumeroEchange,
            InitialMsgId  = input.MessageId,
            StartedAt     = startedAt,
        });

        // ── Timeout configurable (DFO : pas de constante magique) ────────────
        var correlationTimeout = TimeSpan.FromSeconds(
            _options.Value.CorrelationTimeoutSeconds);

        // ── Attente de l'événement de corrélation avec timeout ────────────────
        var correlerTask = ctx.WaitForExternalEvent<CorrellerEnvoyerEvent>("CorrellerEnvoyer");

        // ctx.CreateTimer : OBLIGATOIRE pour les timers dans un orchestrateur.
        // Task.Delay est non-déterministe → ne survit pas aux redémarrages du worker.
        using var timerCts = new CancellationTokenSource();
        var timerTask = ctx.CreateTimer(
            ctx.CurrentUtcDateTime.Add(correlationTimeout),
            timerCts.Token);

        CorrellerEnvoyerEvent? correlEvent = null;
        try
        {
            var winner = await Task.WhenAny(correlerTask, timerTask);

            if (winner == timerTask)
            {
                // ── Chemin timeout ─────────────────────────────────────────────
                var timeoutAt   = ctx.CurrentUtcDateTime;
                var durationSec = (timeoutAt - startedAt).TotalSeconds;

                // DFO Niveau 2 — Statut d'échec avec durée : l'opérateur voit POURQUOI
                // et COMBIEN DE TEMPS l'orchestration a attendu avant d'échouer.
                ctx.SetCustomStatus(new OrchestrationStatus
                {
                    Stage           = OrchestrationStage.Failed_Timeout,
                    SessionId       = input.SessionId,
                    NumeroEchange   = input.NumeroEchange,
                    InitialMsgId    = input.MessageId,
                    StartedAt       = startedAt,
                    CompletedAt     = timeoutAt,
                    DurationSeconds = durationSec,
                    ErrorCode       = "TIMEOUT",
                    ErrorMessage    = $"Aucun CorrellerEnvoyer reçu dans {correlationTimeout.TotalSeconds}s.",
                });

                // DFO Niveau 1 — LogError pour déclencher les alertes AI.
                // LogError (pas LogWarning) : un timeout est une anomalie SLA à alerter.
                logger.LogError(
                    "Timeout corrélation TDF. " +
                    "InstanceId={InstanceId}, SessionId={SessionId}, " +
                    "TimeoutSec={TimeoutSec}, DurationSec={DurationSec:F1}",
                    ctx.InstanceId, input.SessionId,
                    correlationTimeout.TotalSeconds, durationSec);

                // DFO Niveau 3 — Compensation via Activity (retriable, idempotente).
                // L'Activity est exécutée AVANT le throw pour garantir l'enregistrement
                // de l'audit même si l'appelant swallow l'exception.
                await ctx.CallActivityAsync(
                    nameof(RecordAuditActivity),
                    new CorrelationAuditRecord
                    {
                        InstanceId      = ctx.InstanceId,
                        SessionId       = input.SessionId,
                        NumeroEchange   = input.NumeroEchange,
                        Stage           = OrchestrationStage.Failed_Timeout,
                        InitialMsgId    = input.MessageId,
                        CorrelMsgId     = null,
                        StartedAt       = startedAt,
                        EventAt         = timeoutAt,
                        DurationSeconds = durationSec,
                        ErrorCode       = "TIMEOUT",
                        ErrorMessage    = $"Timeout après {correlationTimeout.TotalSeconds}s.",
                    },
                    AuditRetryOptions);

                throw new TimeoutException(
                    $"L'orchestration {ctx.InstanceId} n'a pas reçu CorrellerEnvoyer " +
                    $"dans {correlationTimeout.TotalSeconds}s.");
            }

            // Timer annulé dès réception de l'événement pour éviter un déclenchement résiduel.
            timerCts.Cancel();
            correlEvent = await correlerTask;
        }
        catch (TimeoutException)
        {
            throw; // déjà géré ci-dessus
        }
        catch
        {
            timerCts.Cancel(); // nettoyage garanti dans tous les cas d'erreur
            throw;
        }

        // DFO Niveau 2 — Transition de statut "en cours de corrélation".
        ctx.SetCustomStatus(new OrchestrationStatus
        {
            Stage         = OrchestrationStage.Correlating,
            SessionId     = input.SessionId,
            NumeroEchange = input.NumeroEchange,
            InitialMsgId  = input.MessageId,
            StartedAt     = startedAt,
        });

        // DFO Niveau 1 — Log Information : progression normale, utile pour les traces d'audit.
        logger.LogInformation(
            "CorrellerEnvoyer reçu. InstanceId={InstanceId}, CorrelMsgId={CorrelMsgId}",
            ctx.InstanceId, correlEvent.MessageId);

        // ── Validation du token d'autorisation (détecte les corrélations croisées) ─
        if (correlEvent.AuthorizationToken != input.AuthorizationToken)
        {
            var failedAt  = ctx.CurrentUtcDateTime;
            var failedSec = (failedAt - startedAt).TotalSeconds;

            ctx.SetCustomStatus(new OrchestrationStatus
            {
                Stage           = OrchestrationStage.Failed_Token,
                SessionId       = input.SessionId,
                NumeroEchange   = input.NumeroEchange,
                InitialMsgId    = input.MessageId,
                StartedAt       = startedAt,
                CompletedAt     = failedAt,
                DurationSeconds = failedSec,
                ErrorCode       = "INVALID_TOKEN",
                ErrorMessage    = "AuthorizationToken de CorrellerEnvoyer ne correspond pas à l'envoi initial.",
            });

            // DFO Niveau 1 — LogError : une corrélation croisée est une anomalie de sécurité.
            logger.LogError(
                "Token d'autorisation invalide — corrélation croisée détectée. " +
                "InstanceId={InstanceId}, SessionId={SessionId}, DurationSec={DurationSec:F1}",
                ctx.InstanceId, input.SessionId, failedSec);

            // DFO Niveau 3 — Audit de compensation avant le throw.
            await ctx.CallActivityAsync(
                nameof(RecordAuditActivity),
                new CorrelationAuditRecord
                {
                    InstanceId      = ctx.InstanceId,
                    SessionId       = input.SessionId,
                    NumeroEchange   = input.NumeroEchange,
                    Stage           = OrchestrationStage.Failed_Token,
                    InitialMsgId    = input.MessageId,
                    CorrelMsgId     = correlEvent.MessageId,
                    StartedAt       = startedAt,
                    EventAt         = failedAt,
                    DurationSeconds = failedSec,
                    ErrorCode       = "INVALID_TOKEN",
                    ErrorMessage    = "AuthorizationToken mismatch.",
                },
                AuditRetryOptions);

            throw new InvalidOperationException(
                $"AuthorizationToken invalide dans CorrellerEnvoyer. InstanceId={ctx.InstanceId}");
        }

        // ── Succès ────────────────────────────────────────────────────────────
        var completedAt   = ctx.CurrentUtcDateTime;
        var durationTotal = (completedAt - startedAt).TotalSeconds;

        // DFO Niveau 2 — Statut final avec DurationSeconds et CompletedAt.
        // Ces champs permettent des alertes SLA via l'API Durable (GET /orchestrations)
        // sans écrire une requête KQL dans Application Insights.
        ctx.SetCustomStatus(new OrchestrationStatus
        {
            Stage           = OrchestrationStage.Completed,
            SessionId       = input.SessionId,
            NumeroEchange   = input.NumeroEchange,
            InitialMsgId    = input.MessageId,
            StartedAt       = startedAt,
            CompletedAt     = completedAt,
            DurationSeconds = durationTotal,
        });

        // DFO Niveau 1 — Log de complétion avec durée totale.
        // Information (pas Debug) : référence pour les rapports de performance SLA.
        logger.LogInformation(
            "Orchestration TDF complétée. " +
            "InstanceId={InstanceId}, SessionId={SessionId}, " +
            "NumeroEchange={NumeroEchange}, DurationSec={DurationSec:F1}",
            ctx.InstanceId, input.SessionId, input.NumeroEchange, durationTotal);

        // DFO Niveau 3 — Audit de succès via Activity (retriable, durable).
        await ctx.CallActivityAsync(
            nameof(RecordAuditActivity),
            new CorrelationAuditRecord
            {
                InstanceId      = ctx.InstanceId,
                SessionId       = input.SessionId,
                NumeroEchange   = input.NumeroEchange,
                Stage           = OrchestrationStage.Completed,
                InitialMsgId    = input.MessageId,
                CorrelMsgId     = correlEvent.MessageId,
                StartedAt       = startedAt,
                EventAt         = completedAt,
                DurationSeconds = durationTotal,
            },
            AuditRetryOptions);

        return new TransactionCorrelationResult
        {
            AuthorizationToken = input.AuthorizationToken,
            BlobReference      = input.BlobReference,
            NumeroEchange      = input.NumeroEchange,
            FileTokens         = input.FileTokens,
        };
    }
}


