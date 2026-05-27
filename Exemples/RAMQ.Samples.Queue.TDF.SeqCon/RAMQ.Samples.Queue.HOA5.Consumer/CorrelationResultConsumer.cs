using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Exceptions;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.Samples.Queue.HOA5.Consumer.Messages;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Http;
using System.Diagnostics;

namespace RAMQ.Samples.Queue.HOA5.Consumer;

/// <summary>
/// Consumer HOA5 — Traite les résultats de corrélation TDF et les transmet à HOA5 Backend.
///
/// ════════════════════════════════════════════════════════════════════════
/// Rôle dans le pattern Sequential Convoy
/// ════════════════════════════════════════════════════════════════════════
///
/// Ce consumer est le destinataire final (downstream) de l'orchestration TDF.
/// L'orchestrateur (StateFul) publie un TransactionCorrelationResult sur la file
/// de résultats après avoir corrélé les deux étapes (tdf.envoi + tdf.correller).
///
/// Ce consumer :
///   1. Reçoit le CorrelationResultMessage (forme locale de TransactionCorrelationResult).
///   2. Construit la requête InscrireSuiviFichCornl et appelle HOA5 Backend via Refit.
///   3. Journalise le résultat avec la durée (SLA).
///   4. NE complète PAS le message — c'est la responsabilité du TdfResultConsumerFunction.
///
/// Idempotence :
///   L'Idempotency-Key est le MessageId du message Service Bus.
///   Si HOA5 Backend reçoit un appel dupliqué, il retourne la même réponse.
/// </summary>
public sealed class CorrelationResultConsumer : BaseConsumer<CorrelationResultMessage>
{
    private readonly IHoa5BackendApi _api;

    public CorrelationResultConsumer(
        IMessagingProvider messagingProvider,
        ILogger<CorrelationResultConsumer> logger,
        IConsumerConfigurationService config,
        IMessageSerializer serializer,
        IStorageProvider storageProvider,
        IHoa5BackendApi api,
        string? targetName   = null,
        string? consumerName = null,
        string? actionName   = null)
        : base(messagingProvider, logger, config, serializer, storageProvider,
               targetName, consumerName, actionName)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<CorrelationResultMessage> context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        if (context.Message is null)
        {
            Logger.LogWarning("Message nul. MessageId={MessageId}", context.MessageId);
            await DeadLetterMessageAsync(new InvalidOperationException("Message nul."), cancellationToken);
            return context.CopyWithResponse(new MessageTransitResponse { IsPermanentFailure = true, ErrorMessage = "Message nul." });
        }

        var msg = context.Message;
        var idempotencyKey = context.MessageId ?? Guid.NewGuid().ToString("N");
        var correlationId  = context.CorrelationId ?? Guid.NewGuid().ToString("N");

        using var scope = Logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"]     = context.MessageId,
            ["CorrelationId"] = correlationId,
            ["NumeroEchange"] = msg.NumeroEchange
        });

        Logger.LogInformation(
            "Corrélation TDF reçue. NumeroEchange={NumeroEchange}, BlobReference={BlobReference}",
            msg.NumeroEchange, msg.BlobReference);

        try
        {
            var request = new InscrireSuiviFichCorlnRequest
            {
                NoEchg          = msg.NumeroEchange,
                AccuseReception = "CORR-OK",
                Erreur          = "0"
            };

            var response = await _api.InscrireSuiviFichCorlnAsync(
                idempotencyKey: idempotencyKey,
                correlationId:  correlationId,
                request:        request,
                cancellationToken: cancellationToken);

            Logger.LogInformation(
                "HOA5 Backend répondu. IndExecOrcSpec={IndExecOrcSpec}, NoEchg={NoEchg}, Duration={Duration}ms",
                response.IndExecOrcSpec, response.NoEchg, sw.ElapsedMilliseconds);

            await CompleteMessageAsync(cancellationToken);
            return context.CopyWithResponse(new MessageTransitResponse { StatusCode = 200 });
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode >= 500)
        {
            Logger.LogWarning(ex,
                "HOA5 Backend erreur transiente. Status={Status}, NumeroEchange={NumeroEchange}",
                ex.StatusCode, msg.NumeroEchange);
            await ExponentialRetryAsync(
                new ExponentialRetryException($"HOA5 Backend HTTP {(int)ex.StatusCode}.", ex, (int)ex.StatusCode),
                cancellationToken);
            return context.CopyWithResponse(new MessageTransitResponse { IsTransient = true, ErrorMessage = $"HOA5 Backend HTTP {(int)ex.StatusCode}." });
        }
        catch (Refit.ApiException ex)
        {
            Logger.LogError(ex,
                "HOA5 Backend erreur permanente. Status={Status}, NumeroEchange={NumeroEchange}",
                ex.StatusCode, msg.NumeroEchange);
            await DeadLetterMessageAsync(ex, cancellationToken);
            return context.CopyWithResponse(new MessageTransitResponse { IsPermanentFailure = true, ErrorMessage = $"HOA5 Backend HTTP {(int)ex.StatusCode}." });
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Annulation. NumeroEchange={NumeroEchange}", msg.NumeroEchange);
            return context.CopyWithResponse(new MessageTransitResponse { IsTransient = true, ErrorMessage = "Annulé." });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Erreur non gérée. NumeroEchange={NumeroEchange}", msg.NumeroEchange);
            await ExponentialRetryAsync(
                new ExponentialRetryException(ex.Message, ex),
                cancellationToken);
            return context.CopyWithResponse(new MessageTransitResponse { IsTransient = true, ErrorMessage = ex.Message });
        }
    }
}
