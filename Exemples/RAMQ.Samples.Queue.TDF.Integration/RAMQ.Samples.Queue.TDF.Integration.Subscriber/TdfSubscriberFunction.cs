using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.Samples.Queue.TDF.Integration.Consumer;
using RAMQ.Samples.Queue.TDF.Integration.Consumer.Messages;
using RAMQ.Samples.Queue.TDF.Integration.StateFul;
using RAMQ.Samples.Queue.TDF.Integration.StateFul.Models;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.TDF.Integration.Subscriber;

/// <summary>
/// Subscriber TDF Sequential Convoy — Azure Function Service Bus session-aware.
///
/// ════════════════════════════════════════════════════════════════════════
/// Rôle du Subscriber dans le pattern Sequential Convoy
/// ════════════════════════════════════════════════════════════════════════
///
/// Étape 2 — tdf.envoi
///   1. BindContext + DeserializeMessageAsync via TdfSeqConConsumer.
///   2. ConsumeAsync (validation : NumeroEchange, AuthorizationToken, FileToken).
///   3. ScheduleNewOrchestrationInstanceAsync avec InstanceId = SessionId.
///      → Idempotence : un seul orchestrateur par SessionId (durable garantit l'unicité).
///   4. CompleteMessageAsync.
///
/// Étape 4 — tdf.correller
///   1. BindContext + DeserializeMessageAsync.
///   2. ConsumeAsync (validation AccuseReception + appel HOA5 API).
///   3. RaiseEventAsync("CorrellerEnvoyer") → réveille l'orchestrateur en attente.
///   4. CompleteMessageAsync.
///
/// Sequential Convoy — garantie d'ordre
///   IsSessionsEnabled = true → Service Bus livre les deux messages dans l'ordre
///   d'envoi DANS la même session. L'orchestrateur reçoit tdf.envoi AVANT tdf.correller.
///
/// DFO — Observabilité
///   BeginScope → MessageId, SessionId, CorrelationId, Step propagés dans chaque log.
///   Stopwatch → Duration publiée pour alertes SLA.
/// </summary>
public sealed class TdfSubscriberFunction
{
    private readonly ILogger<TdfSubscriberFunction> _logger;
    private readonly TdfSeqConConsumer _consumer;

    public TdfSubscriberFunction(
        ILogger<TdfSubscriberFunction> logger,
        TdfSeqConConsumer consumer)
    {
        _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    [Function(nameof(TdfSubscriberFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            "sbq-tdf-seqcon-session",
            Connection          = "ServiceBusConnection",
            AutoCompleteMessages = false,
            IsSessionsEnabled   = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Subscriber reçu. MessageId={MessageId}, SessionId={SessionId}",
            message.MessageId, message.SessionId);

        // ── Bind + Désérialiser ─────────────────────────────────────────────────────
        try
        {
            _consumer.BindContext(message, actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BindContext échoué. MessageId={MessageId} → DLQ", message.MessageId);
            await _consumer.DeadLetterMessageAsync(ex, ct);
            return;
        }

        var deserResult = await _consumer.DeserializeMessageAsync<TdfTransactionCommand>(ct);
        if (!deserResult.IsSuccess)
        {
            _logger.LogWarning(
                "Désérialisation échouée. MessageId={MessageId}, Raison={Reason} → DLQ",
                message.MessageId, deserResult.FailureReason);
            await _consumer.DeadLetterMessageAsync(
                new InvalidOperationException($"Désérialisation échouée : {deserResult.ErrorMessage}"), ct);
            return;
        }

        var context = deserResult.Value!;
        var step    = context.Variables?.GetValueOrDefault("step") as string;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"]     = message.MessageId,
            ["SessionId"]     = message.SessionId,
            ["CorrelationId"] = message.CorrelationId,
            ["Step"]          = step
        });

        try
        {
            // ── Appel Consumer TDF (validation + enrichissement + appel HOA5 si correller) ──
            await _consumer.ConsumeAsync(context, ct);

            // ── Routage Durable selon l'étape ────────────────────────────────────────────
            switch (step)
            {
                case "tdf.envoi":
                    await StartOrchestrationAsync(context, message, durableClient, ct);
                    break;

                case "tdf.correller":
                    await SignalCorrelationAsync(context, message, durableClient, ct);
                    break;

                default:
                    _logger.LogWarning(
                        "Step inconnu '{Step}'. MessageId={MessageId} → DLQ",
                        step, message.MessageId);
                    await _consumer.DeadLetterMessageAsync(
                        new InvalidOperationException($"Step inconnu : '{step}'"), ct);
                    return;
            }

            // ── Compléter le message — responsabilité exclusive du Subscriber ────────────
            await _consumer.CompleteMessageAsync(ct);

            _logger.LogInformation(
                "Subscriber terminé. Step={Step}, MessageId={MessageId}, Duration={Duration}ms",
                step, message.MessageId, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Annulation. MessageId={MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erreur non gérée. Step={Step}, MessageId={MessageId}, Duration={Duration}ms → DLQ",
                step, message.MessageId, sw.ElapsedMilliseconds);
            await _consumer.DeadLetterMessageAsync(ex, ct);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Démarre l'orchestration Durable avec InstanceId = SessionId.
    // Idempotent : si l'orchestration existe déjà (retry), ScheduleNew lève une
    // exception que le catch principal intercepte et DLQ le message.
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task StartOrchestrationAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        ServiceBusReceivedMessage message,
        DurableTaskClient durableClient,
        CancellationToken ct)
    {
        var instanceId = message.SessionId ?? context.SessionId
            ?? throw new InvalidOperationException("SessionId manquant pour tdf.envoi.");

        var fileToken = context.Tokens?.FirstOrDefault(
            t => t.Kind == TokenKind.File);

        var input = new EnvoyerLotFichierEvent
        {
            SessionId          = instanceId,
            NumeroEchange      = context.Message!.NumeroEchange,
            AuthorizationToken = context.Message.AuthorizationToken,
            BlobReference      = context.Message.BlobReference,
            FileTokens         = fileToken is not null ? [fileToken] : null,
            MessageId          = message.MessageId
        };

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            orchestratorName: nameof(TdfTransactionOrchestrator),
            input:            input,
            options:          new StartOrchestrationOptions { InstanceId = instanceId },
            cancellation:     ct);

        _logger.LogInformation(
            "Orchestration démarrée. InstanceId={InstanceId}, NumeroEchange={NumeroEchange}",
            instanceId, context.Message.NumeroEchange);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lève l'événement externe qui réveille l'orchestrateur en attente de corrélation.
    // L'orchestrateur utilise ctx.WaitForExternalEvent<CorrellerEnvoyerEvent>("CorrellerEnvoyer").
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task SignalCorrelationAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        ServiceBusReceivedMessage message,
        DurableTaskClient durableClient,
        CancellationToken ct)
    {
        var instanceId = message.SessionId ?? context.SessionId
            ?? throw new InvalidOperationException("SessionId manquant pour tdf.correller.");

        var eventPayload = new CorrellerEnvoyerEvent
        {
            AuthorizationToken = context.Message!.AuthorizationToken,
            MessageId          = message.MessageId
        };

        await durableClient.RaiseEventAsync(
            instanceId:   instanceId,
            eventName:    "CorrellerEnvoyer",
            eventPayload: eventPayload,
            cancellation: ct);

        _logger.LogInformation(
            "Événement CorrellerEnvoyer levé. InstanceId={InstanceId}",
            instanceId);
    }
}

