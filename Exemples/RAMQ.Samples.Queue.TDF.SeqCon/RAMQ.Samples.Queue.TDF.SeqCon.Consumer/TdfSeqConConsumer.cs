using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Consumer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers;
using RAMQ.COM.EnterpriseMessageTransit.Serialization;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Http;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Messages;

namespace RAMQ.Samples.Queue.TDF.SeqCon.Consumer;

/// <summary>
/// Consumer TDF Sequential Convoy — Validate / Transform / Enrich.
/// NE fait PAS CompleteMessageAsync — c'est la responsabilité du Subscriber.
/// </summary>
public class TdfSeqConConsumer : BaseConsumer<TdfTransactionCommand>
{
    private readonly IHoa5BackendApi _api;

    public TdfSeqConConsumer(
        IMessagingProvider messagingProvider,
        ILogger<TdfSeqConConsumer> logger,
        IConsumerConfigurationService config,
        IMessageSerializer serializer,
        IStorageProvider storageProvider,
        IHoa5BackendApi api,
        string? targetName    = null,
        string? consumerName  = null,
        string? actionName    = null)
        : base(messagingProvider, logger, config, serializer, storageProvider, targetName, consumerName, actionName)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <inheritdoc />
    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        var step = context.Variables?.GetValueOrDefault("step") as string;

        using var scope = Logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"]     = context.MessageId,
            ["SessionId"]     = context.SessionId,
            ["CorrelationId"] = context.CorrelationId,
            ["Step"]          = step
        });

        Logger.LogInformation("Consumer TDF démarré. Step={Step}, NumeroEchange={NumeroEchange}",
            step, context.Message?.NumeroEchange);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = step switch
            {
                "tdf.envoi"     => await ConsumeEnvoyerLotFichierAsync(context, ct),
                "tdf.correller" => await ConsumeCorrellerEnvoyerAsync(context, ct),
                _               => throw new InvalidOperationException($"Variables['step'] inconnu : '{step}'")
            };

            Logger.LogInformation("Consumer TDF terminé. Step={Step}, Duration={Duration}ms", step, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Consumer TDF en erreur. Step={Step}, Duration={Duration}ms", step, sw.ElapsedMilliseconds);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Étape 2 — Validate + Enrich uniquement (aucun appel API)
    // -------------------------------------------------------------------------
    private Task<MessageTransitContext<MessageTransitResponse>> ConsumeEnvoyerLotFichierAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        Logger.LogDebug("Étape 2 — validation lot fichier. MessageId={MessageId}", context.MessageId);

        if (string.IsNullOrWhiteSpace(context.Message?.NumeroEchange))
            throw new ArgumentException("NumeroEchange manquant.", nameof(context));

        if (string.IsNullOrWhiteSpace(context.Message?.AuthorizationToken))
            throw new ArgumentException("AuthorizationToken manquant.", nameof(context));

        var fileToken = context.Tokens?.FirstOrDefault(t => t.Kind == RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum.TokenKind.File);
        if (fileToken is null)
            throw new ArgumentException("Token de type File requis pour tdf.envoi.", nameof(context));

        if (fileToken.Reference?.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true)
            throw new ArgumentException("Token.Reference doit être un chemin relatif, pas une URL absolue.", nameof(context));

        Logger.LogInformation("Étape 2 — validation OK. BlobReference={BlobRef}, SizeBytes={Size}",
            fileToken.Reference, fileToken.SizeBytes);

        return Task.FromResult(context.CopyWithResponse(new MessageTransitResponse { StatusCode = 200 }));
    }

    // -------------------------------------------------------------------------
    // Étape 3 — Validate + Transform + Enrich + appel API HOA5
    // -------------------------------------------------------------------------
    private async Task<MessageTransitContext<MessageTransitResponse>> ConsumeCorrellerEnvoyerAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        Logger.LogDebug("Étape 3 — corrélation envoi. MessageId={MessageId}", context.MessageId);

        if (string.IsNullOrWhiteSpace(context.Message?.AccuseReception))
            throw new ArgumentException("AccuseReception manquant.", nameof(context));

        if (string.IsNullOrWhiteSpace(context.Message?.NumeroEchange))
            throw new ArgumentException("NumeroEchange manquant.", nameof(context));

        var request = new InscrireSuiviFichCorlnRequest
        {
            NoEchg          = context.Message.NumeroEchange,
            AccuseReception = context.Message.AccuseReception,
            Erreur          = "0"
        };

        var sw = Stopwatch.StartNew();
        var response = await _api.InscrireSuiviFichCorlnAsync(
            idempotencyKey: context.MessageId ?? Guid.NewGuid().ToString("N"),
            correlationId:  context.SessionId  ?? string.Empty,
            request:        request,
            cancellationToken: ct);
        sw.Stop();

        Logger.LogInformation(
            "HOA5 InscrireSuiviFich OK. IndExecOrcSpec={Ind}, NoEchg={NoEchg}, Duration={Duration}ms",
            response.IndExecOrcSpec, response.NoEchg, sw.ElapsedMilliseconds);

        // IndExecOrcSpec est transmis via Content pour que le Subscriber puisse décider du routage HOA5
        return context.CopyWithResponse(new MessageTransitResponse
        {
            StatusCode = 200,
            Content    = response.IndExecOrcSpec
        });
    }
}
