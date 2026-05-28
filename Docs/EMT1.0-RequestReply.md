# Review Architecture — Pattern Request/Reply Enterprise

## ✅ Lot R3 livré — tous les problèmes résolus (2026-05-28)

> Le pattern est désormais **complet et fonctionnel**. Les 4 projets samples compilent et tournent.
> Architecture livrée : `IRequestReplyClient<TRequest, TResponse>` séparé de `IMessageProducer<T>` (ISP),
> côté requester implémenté de bout en bout, bypass EMT supprimé, observabilité OTel câblée.

---

## Résumé des corrections apportées par R3

| Item | Statut | Résolution |
|---|---|---|
| **C1** — `ServiceBusClient` absent du DI | ✅ Résolu | `AddEMTSampleProducerDefaults()` enregistre toute l'infra EMT + `ServiceBusClient` |
| **C2** — Bypass EMT (`ServiceBusSender` brut par message) | ✅ Résolu | Réponse via `IMessageProducer<ReplyMessage>` injecté en DI — pipeline EMT complet |
| **C3** — Cast dur `AzureFunctionMessageTransit` | ✅ Résolu | Supprimé — accès via accesseurs EMT typés |
| **I1** — Type anonyme non désérialisable | ✅ Résolu | `ReplyMessage` record fort utilisé |
| **I2** — DI Lifetime incorrect (`AddTransient`) | ✅ Résolu | `AddScoped` via helper DI |
| **I3** — Catches incomplets | ✅ Résolu | `ImmediateRetryException`, `ExponentialRetryException`, `ImmediateDLQException`, `OperationCanceledException` gérés |
| **I4** — `ConfigureAzureProviders()` sans credential | ✅ Résolu | `AddEMTSampleProducerDefaults(config, new VisualStudioCredential())` |
| **I5** — Côté requester absent | ✅ Résolu | `Worker` = `IRequestReplyClient<RequestMessage, ReplyMessage>.GetResponseAsync()` |
| **S1** — `#pragma warning disable` | ✅ Résolu | Nullable proper, warnings supprimés |
| **S2** — `_logger` duplique `Logger` hérité | ✅ Résolu | Supprimé |
| **S3** — `context?.Message == null` | ✅ Résolu | `ArgumentNullException.ThrowIfNull(context)` |
| **S4** — `reply.IsTransient` absent dans catch générique | ✅ Résolu | Ajouté |
| **S5** — `BeginScope` absent | ✅ Résolu | `Logger.BeginScope(MessageId, SessionId, RequestId)` |
| **S6** — `BuildResponseContext` non utilisé | ✅ Résolu | `context.CopyWithResponse(reply)` |
| **S7** — Encodage UTF-8 corrompu dans `Activator.cs` | ✅ Résolu | Fichier ré-encodé UTF-8 |
| **S8** — `return` au lieu de `OperationCanceledException` | ✅ Résolu | `throw` correct |

---

## Architecture livrée

```
Worker (Requester — BackgroundService)
  IRequestReplyClient<RequestMessage, ReplyMessage>
  ↓ GetResponseAsync(ctx, new RequestReplyOptions { ... })
    ↓ publie RequestMessage sur "request-queue" (SessionId = correlationId)
    ↓ attend sur "reply-queue" (session receiver, timeout configurable)

Activator (Azure Function ServiceBusTrigger — Responder)
  → délègue à RequestReplyConsumer via ConsumeAsync

RequestReplyConsumer : BaseConsumer<RequestMessage>
  → IMessageProducer<ReplyMessage> (injecté en DI)
  → PublishAsync(replyContext) — pipeline EMT complet (retry, OTel, journal)
  → CompleteMessageAsync
```

**DI Worker (`Program.cs`) :**
```csharp
services.AddEMTSampleProducerDefaults(hostContext.Configuration, new VisualStudioCredential());
services.AddRequestReplyClient<RequestMessage, ReplyMessage>("request-queue", "reply-queue");
services.AddHostedService<DoWork>();
```

**`RequestReplyConsumer.cs` :**
```csharp
public class RequestReplyConsumer : BaseConsumer<RequestMessage>
{
    private readonly IMessageProducer<ReplyMessage> _replyProducer;

    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<RequestMessage> context, CancellationToken cancellationToken)
    {
        var replyContext = new MessageTransitContext<ReplyMessage>
        {
            Message       = new ReplyMessage { Id = context.Message!.Id, Content = context.Message.Content + " - replied" },
            MessageId     = Guid.NewGuid().ToString("N"),
            SessionId     = context.SessionId,
            CorrelationId = context.MessageId   // corrélation : RequestId → CorrelationId réponse
        };
        await _replyProducer.PublishAsync(replyContext, null, cancellationToken);
        await CompleteMessageAsync(cancellationToken);
        return context.CopyWithResponse(new MessageTransitResponse { StatusCode = 200 });
    }
}
```

---

## Référence croisée

- Voir [EMTDeepReview.md §6.4](EMTDeepReview.md#64-request--reply) pour le détail des changements R3.
- Voir [EMTDeepReview.md §11.3](EMTDeepReview.md#113-lot-r3--refonte-intégrale-du-pattern-request-reply--livré) pour le plan de résolution.
- Samples : `RAMQ.Samples.Queue.RequestReply.{Message,Activator,Worker,Consumer}` — tous 🟢 Active.
