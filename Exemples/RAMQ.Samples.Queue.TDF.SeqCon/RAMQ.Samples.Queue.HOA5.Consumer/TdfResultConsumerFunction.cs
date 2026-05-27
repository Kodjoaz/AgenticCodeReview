using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.Samples.Queue.HOA5.Consumer.Messages;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.HOA5.Consumer;

/// <summary>
/// Azure Function — Consomme les résultats de corrélation TDF depuis la file de résultats.
///
/// ════════════════════════════════════════════════════════════════════════
/// Rôle dans le pattern Sequential Convoy
/// ════════════════════════════════════════════════════════════════════════
///
/// Ce Function est le point d'entrée Service Bus downstream.
/// L'orchestrateur TDF (StateFul) a publié le résultat de corrélation sur
/// "sbq-tdf-seqcon-result" après corrélation des étapes envoi + correller.
///
/// Flux de traitement :
///   1. BindContext — lie le message Service Bus au consumer.
///   2. DeserializeMessageAsync — désérialise CorrelationResultMessage.
///   3. ConsumeAsync — appelle HOA5 Backend API via Refit.
///   4. CompleteMessageAsync — settle le message (responsabilité du Function).
///   5. DeadLetterMessageAsync — si désérialisation ou erreur permanente.
///
/// DFO — Observabilité :
///   BeginScope → MessageId, SessionId, CorrelationId, NumeroEchange propagés.
///   enableScopeProperties = true dans host.json → dimensions Application Insights.
///   Stopwatch → Duration publiée pour alertes SLA.
/// </summary>
public sealed class TdfResultConsumerFunction
{
    private readonly ILogger<TdfResultConsumerFunction> _logger;
    private readonly CorrelationResultConsumer _consumer;

    public TdfResultConsumerFunction(
        ILogger<TdfResultConsumerFunction> logger,
        CorrelationResultConsumer consumer)
    {
        _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    [Function(nameof(TdfResultConsumerFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            "sbq-tdf-seqcon-result",
            Connection           = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Résultat TDF reçu. MessageId={MessageId}, CorrelationId={CorrelationId}",
            message.MessageId, message.CorrelationId);

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

        var deserResult = await _consumer.DeserializeMessageAsync<CorrelationResultMessage>(ct);
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

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"]     = message.MessageId,
            ["CorrelationId"] = message.CorrelationId,
            ["SessionId"]     = message.SessionId,
            ["NumeroEchange"] = context.Message?.NumeroEchange
        });

        try
        {
            var result = await _consumer.ConsumeAsync(context, ct);

            _logger.LogInformation(
                "Résultat TDF traité. MessageId={MessageId}, IsPermanentFailure={IsPermanentFailure}, IsTransient={IsTransient}, Duration={Duration}ms",
                message.MessageId, result.Message?.IsPermanentFailure, result.Message?.IsTransient, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Annulation. MessageId={MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erreur non gérée. MessageId={MessageId}, Duration={Duration}ms → DLQ",
                message.MessageId, sw.ElapsedMilliseconds);
            await _consumer.DeadLetterMessageAsync(ex, ct);
        }
    }
}
