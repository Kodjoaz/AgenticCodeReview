# Review Architecture — Pattern Request/Reply Enterprise

## Résumé exécutif

Le pattern est **partiellement implémenté** : seul le côté répondeur (consumer) existe. Le côté demandeur (requester/producer) est absent. Plusieurs problèmes critiques empêcheraient le démarrage de l'application.

---

## 🔴 Problèmes CRITIQUES

### C1 — `ServiceBusClient` absent du DI container

`Program.cs` enregistre `RequestReplyConsumer` mais **n'enregistre jamais `ServiceBusClient`** requis par son constructeur. L'application crashe au démarrage avec `InvalidOperationException`.

```csharp
// MANQUANT dans Program.cs :
services.AddSingleton(sp =>
    new ServiceBusClient(
        configuration["ServiceBusConnection__fullyQualifiedNamespace"],
        new VisualStudioCredential()));
```

### C2 — Bypass complet de l'infrastructure EMT pour la réponse

`RequestReplyConsumer.cs` crée un `ServiceBusSender` brut pour chaque message :

```csharp
await using var sender = _serviceBusClient.CreateSender(replyEntity); // ❌ Sender créé par message
await sender.SendMessageAsync(sbReply, cancellationToken);
```

- **Aucune réutilisation de connexion** (`await using` détruit le sender à chaque appel)
- **Pas de retry EMT** sur l'envoi de la réponse
- **Pas de télémétrie/tracing** via le pipeline EMT
- **Couplage direct** sur le transport Azure Service Bus — casse si on change de provider

### C3 — Couplage au transport concret

```csharp
if (context.TransportMessage is AzureFunctionMessageTransit aft) // ❌ Cast dur
```

Le consumer dépend directement du type Azure. Si EMT change de transport, ce code casse silencieusement et `original` sera `null` → la réponse ne sera jamais envoyée sans erreur explicite.

---

## 🟠 Problèmes IMPORTANTS

### I1 — Payload de réponse : type anonyme au lieu de `ReplyMessage`

```csharp
var responsePayload = new            // ❌ type anonyme non désérialisable
{
    context.Message.Id,
    OriginalContent = context.Message.Content,
    RepliedContent  = context.Message.Content + " - replied from consumer"
};
```

Le record `ReplyMessage` est défini dans le projet `RequestReply.Message` mais **n'est jamais utilisé**. Un type anonyme ne peut pas être désérialisé côté requester.

### I2 — DI Lifetime incorrect

`services.AddTransient<RequestReplyConsumer>()` — ce consumer est **stateful** (appel `BindContext` avant chaque traitement). `Scoped` est le lifetime sémantiquement correct pour garantir qu'une instance est créée par invocation.

### I3 — Catches incomplets dans `ConsumeAsync`

`RequestReplyConsumer.cs` n'a qu'un seul `catch (Exception)` → `ImmediateRetryException`, `ExponentialRetryException`, `ImmediateDLQException` et `OperationCanceledException` ne sont pas gérés selon le contrat EMT.

### I4 — `ConfigureAzureProviders()` sans credential

```csharp
services.ConfigureAzureProviders(); // ❌ pas de credential pour dev local
// ✅ Doit être :
services.ConfigureAzureProviders(new VisualStudioCredential());
```

### I5 — Côté requester absent

Le pattern Request/Reply est **incomplet** — il n'existe aucun worker/producer qui :
- Envoie la `RequestMessage` avec `ReplyTo` + `CorrelationId` + `SessionId`
- Attend la réponse sur une reply queue (session receiver)
- Gère le timeout si la réponse n'arrive pas dans le délai imparti

---

## 🟡 Problèmes SECONDAIRES

| # | Fichier | Issue |
|---|---------|-------|
| S1 | `RequestReplyConsumer.cs` | `#pragma warning disable` masque CS8618, CS nullable |
| S2 | `RequestReplyConsumer.cs` | `private readonly ILogger _logger` duplique `Logger` hérité de `BaseConsumer` |
| S3 | `RequestReplyConsumer.cs` | `if (context?.Message == null)` → utiliser `ArgumentNullException.ThrowIfNull(context)` |
| S4 | `RequestReplyConsumer.cs` | `reply.IsTransient = true` absent dans catch générique |
| S5 | `RequestReplyConsumer.cs` | `BeginScope` absent → pas de corrélation structurée dans les logs |
| S6 | `RequestReplyConsumer.cs` | `BuildResponseContext` non utilisé — retour inline avec `new MessageTransitContext<>` |
| S7 | `Activator.cs` | Encodage UTF-8 corrompu (caractères FR illisibles — fichier sauvegardé ANSI) |
| S8 | `Activator.cs` | `if (cancellationToken.IsCancellationRequested) return` devrait lever `OperationCanceledException` |

---

## Architecture cible Enterprise

```mermaid
sequenceDiagram
    participant W as Worker (Requester)<br/>IMessageProducer&lt;RequestMessage&gt;
    participant RQ as request-queue (ASB)
    participant A as Activator<br/>(Azure Function)
    participant C as RequestReplyConsumer<br/>BaseConsumer&lt;RequestMessage&gt;
    participant SBS as ServiceBusClient<br/>(Singleton DI)
    participant ReplyQ as reply-queue / session (ASB)
    participant WR as Worker (Waiter)<br/>SessionReceiver

    W->>RQ: SendAsync(RequestMessage)<br/>ReplyTo=reply-queue<br/>SessionId=correlationId<br/>TTL=30s
    RQ->>A: ServiceBusTrigger
    A->>C: BindContext + DeserializeMessageAsync
    C->>C: ConsumeAsync (logique métier)
    C->>SBS: CreateSender(ReplyTo)<br/>puis SendMessageAsync(ReplyMessage)
    SBS->>ReplyQ: SendAsync to ReplyTo<br/>SessionId=original.SessionId
    C->>RQ: CompleteMessageAsync
    ReplyQ->>WR: SessionReceiver.ReceiveAsync<br/>(by SessionId, timeout 30s)
    WR->>WR: Deserialize ReplyMessage
```

---

## Changements structurels requis

### 1 — Program.cs : enregistrements corrects

```csharp
// ✅ Enregistrement ServiceBusClient singleton (managed identity)
services.AddSingleton(sp =>
    new ServiceBusClient(
        configuration["ServiceBusConnection__fullyQualifiedNamespace"],
        new VisualStudioCredential()));

// ✅ Consumer — Scoped pour cycle de vie par invocation
services.AddScoped<RequestReplyConsumer>();

// ✅ Credential pour dev local
services.ConfigureAzureProviders(new VisualStudioCredential());
```

### 2 — Consumer : utiliser `ReplyMessage` typé + catches EMT complets

```csharp
public class RequestReplyConsumer : BaseConsumer<RequestMessage>
{
    private readonly ServiceBusClient _serviceBusClient;

    public RequestReplyConsumer(
        IMessagingProvider messagingProvider,
        ILogger<RequestReplyConsumer> logger,
        IConsumerConfigurationService config,
        IMessageSerializer serializer,
        IStorageProvider storageProvider,
        ServiceBusClient serviceBusClient)
        : base(messagingProvider, logger, config, serializer, storageProvider)
    {
        _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
    }

    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<RequestMessage> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var scope = Logger.BeginScope(new Dictionary<string, object?> {
            ["MessageId"] = context.MessageId,
            ["SessionId"] = context.SessionId,
            ["RequestId"] = context.Message?.Id });

        var reply = new MessageTransitResponse { StatusCode = 200, Content = "OK" };
        try
        {
            ServiceBusReceivedMessage? original = null;
            if (context.TransportMessage is AzureFunctionMessageTransit aft)
                original = aft.RawMessage;

            if (original?.ReplyTo != null)
            {
                var replyPayload = new ReplyMessage        // ← type fort désérialisable
                {
                    Id      = context.Message!.Id,
                    Content = context.Message.Content + " - replied from consumer"
                };
                var serialized = Serializer.Serialize(replyPayload);
                var sbReply = new ServiceBusMessage(serialized)
                {
                    MessageId     = Guid.NewGuid().ToString(),
                    SessionId     = original.SessionId ?? context.SessionId,
                    CorrelationId = original.CorrelationId ?? original.MessageId
                };
                sbReply.ApplicationProperties["Consumer"] = "RequestReplyConsumer";
                sbReply.ApplicationProperties["Action"]   = "Reply";

                await using var sender = _serviceBusClient.CreateSender(original.ReplyTo);
                await sender.SendMessageAsync(sbReply, cancellationToken);
                Logger.LogInformation("Réponse envoyée ReplyTo={ReplyTo} MessageId={MessageId}",
                    original.ReplyTo, sbReply.MessageId);
            }
            else
            {
                Logger.LogWarning("Aucun ReplyTo pour MessageId={MessageId}", context.MessageId);
            }
            await CompleteMessageAsync(cancellationToken);
        }
        catch (ImmediateRetryException ex)   { reply.IsTransient = true;        await ImmediateRetryAsync(ex, cancellationToken); }
        catch (ExponentialRetryException ex) { reply.IsTransient = true;        await ExponentialRetryAsync(ex, cancellationToken); }
        catch (ImmediateDLQException ex)     { reply.IsPermanentFailure = true; await DeadLetterMessageAsync(ex, cancellationToken); }
        catch (OperationCanceledException)   { Logger.LogWarning("Annulation RequestReplyConsumer Id={Id}", context.MessageId); throw; }
        catch (Exception ex)                 { reply.IsTransient = true;        Logger.LogError(ex, "Erreur MessageId={Id}", context.MessageId); await DeadLetterMessageAsync(ex, cancellationToken); }

        return BuildResponseContext(context, reply);
    }
}
```

### 3 — Côté requester (Worker manquant) — pattern de base

```csharp
// Worker qui envoie la requête et attend la réponse par session
var sessionId  = Guid.NewGuid().ToString();  // corrélation unique
var requestCtx = new MessageTransitContext<RequestMessage>
{
    MessageId   = Guid.NewGuid().ToString(),
    SessionId   = sessionId,
    Message     = new RequestMessage { Id = Guid.NewGuid(), Content = "my request" },
    MessageType = typeof(RequestMessage).AssemblyQualifiedName
};

// Publier avec ReplyTo et TTL
await _requestProducer.PublishAsync(requestCtx, new PublishOptions
{
    Properties = new Dictionary<string, object>
    {
        [AzureMessagingProperties.ReplyTo]   = "reply-queue",
        [AzureMessagingProperties.SessionId] = sessionId
    },
    TimeToLive = TimeSpan.FromSeconds(30)    // timeout explicite
}, cancellationToken);

// Attente de la réponse via session receiver sur "reply-queue" avec sessionId
var sessionReceiver = await _serviceBusClient.AcceptSessionAsync("reply-queue", sessionId, cancellationToken: cancellationToken);
var replyMsg = await sessionReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
var replyPayload = JsonSerializer.Deserialize<ReplyMessage>(replyMsg.Body);
```

---

## Checklist de correction

| Priorité | Fichier | Action |
|----------|---------|--------|
| 🔴 C1 | `Program.cs` | Enregistrer `ServiceBusClient` singleton |
| 🔴 C2 | `RequestReplyConsumer.cs` | Utiliser `ReplyMessage` typé + supprimer `await using` par message |
| 🔴 C3 | `RequestReplyConsumer.cs` | Gérer le cas `ReplyTo == null` explicitement |
| 🟠 I2 | `Program.cs` | `AddScoped<RequestReplyConsumer>()` |
| 🟠 I3 | `RequestReplyConsumer.cs` | Ajouter les 4 catches EMT |
| 🟠 I4 | `Program.cs` | `ConfigureAzureProviders(new VisualStudioCredential())` |
| 🟠 I5 | nouveau fichier | Implémenter côté requester |
| 🟡 S1-S6 | `RequestReplyConsumer.cs` | #pragma, _logger, ThrowIfNull, IsTransient, BeginScope, BuildResponseContext |
| 🟡 S7 | `Activator.cs` | Ré-encoder en UTF-8 |
| 🟡 S8 | `Activator.cs` | Utiliser `OperationCanceledException` au lieu de `return` |
