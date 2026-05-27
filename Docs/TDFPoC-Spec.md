# TDF PoC — Spécification du Proof of Concept

**Projet :** Remplacement de BizTalk HOA5/TDF par Azure Functions + EMT
**Version :** 3.0 — Architecture avec TDF.Integration.Producer, observabilité complète, nomenclature alignée
**Architecture cible :** [`target-state-hoa5-fr.md`](../target-state-hoa5-fr.md) — Sections 1, 3, 10 (observabilité), 11 (sécurité)
**Périmètre :** Proof of Concept — flux entrant TDF complet (3 étapes) avec observabilité via Message Transit Journal + AppInsights

---

## 1. Objectif du PoC

Valider end-to-end que le protocole TDF en 3 étapes — actuellement géré par BizTalk Server
(`TDF_OOA2` + `MEC_HOA5`) — peut être implémenté avec **Azure Functions .NET 8 isolated worker**
et la librairie **EnterpriseMessageTransit (EMT)**.

Démontrer aussi l'**observabilité complète** via Message Transit Journal (EMT) + Application Insights,
remplaçant la BizTalk Tracking DB non fonctionnelle.

### **Projets de code du PoC — TDF.Integration + HOA5.Integration**

| # | Projet | Type | Rôle |
|---|--------|------|------|
| **TDF.Integration** |  |  |  |
| 1 | `RAMQ.Samples.Queue.TDF.Integration.Frontend` | Azure Function — **Timer Trigger** | Simule le TDF Frontend (.NET 4.8) — Claim Check upload + HttpClient → Producer |
| 2 | `RAMQ.Samples.Queue.TDF.Integration.Producer` | Azure Function — **HTTP Trigger** | **Point d'entrée unique** — reçoit JSON du Frontend, VE, publie `tdf-queue` |
| 3 | `RAMQ.Samples.Queue.TDF.Integration.Subscriber` | Azure Function — **ServiceBus Trigger** | Orchestration légère — route par `Variables["step"]`, coordonne Orchestrator + Consumers |
| 4 | `RAMQ.Samples.Queue.TDF.Integration.Consumer` | Librairie — **EMT BaseConsumer** | Logique métier VTE — `EnvoyerLotFichierConsumer` (Étape 2), `CorrellerEnvoyerConsumer` (Étape 3) |
| 5 | `RAMQ.Samples.Queue.TDF.Integration.Orchestrator` | Azure Function — **Durable** | Machine à états inter-étapes — corrélation SessionId, validation AuthToken |
| **HOA5.Integration** |  |  |  |
| 6 | `RAMQ.Samples.Queue.HOA5.Integration.Subscriber` | Azure Function — **ServiceBus Trigger** | Écoute `hoa5-queue`, route vers Consumer |
| 7 | `RAMQ.Samples.Queue.HOA5.Integration.Consumer` | Librairie — **EMT BaseConsumer** | Logique métier — transformation TDF → contrat HOA5 Backend |
| 8 | `RAMQ.Samples.Queue.HOA5.Integration.Backend` | Azure Function — **HTTP Trigger** | Résout tokens depuis Blob Storage, persistance Oracle |

> **Infrastructure hors périmètre :** `tdf-queue`, `tdf-topic`, `hoa5-queue`, l'abonnement HOA5,
> et la règle `ForwardTo` sont gérés par l'équipe infrastructure. Ces ressources sont
> supposées **pré-provisionnées**. Le code ne crée aucune ressource Service Bus.

---

## 1.1 Observabilité — Message Transit Journal + Application Insights

### Pourquoi le Producer HTTP (pas Native Direct Service Bus) ?

**Décision architecturale :** Le TDF.Integration.Frontend appelle TDF.Integration.Producer via HttpClient,
plutôt que de publier directement dans `tdf-queue`. Cela centralise :

| Capacité | Via Producer ✅ | Direct SB ❌ |
|----------|-----------------|------------|
| **Message Transit Journal (EMT)** | Automatique — Azure Table Storage | Manuel — à implémenter |
| **Application Insights Traçage distribué** | Complet Frontend→Producer→SB→Subscriber→Consumer→API | Partiel — manque Producer |
| **Gestion États (STARTED/RETRY/COMPLETED/DLQ)** | EMT automatique | Manuel |
| **Alertes DLQ** | Automatique | À coder |
| **Indicateurs opérationnels (taux succès, latence, orphelins)** | Prédéfinis dans Journal | À calculer |
| **Conformité RAMQ (Modèle C + G)** | Complète | Partielle |

**Résultat :** Observabilité complète sans développement supplémentaire. Voir Section 8.

---

## 2. Protocole TDF (3 étapes) — Vue d'ensemble

| Étape | `Variables["step"]` | Pièce jointe | Description |
|-------|---------------------|--------------|-------------|
| 1 — InitierEnvoi | *(jamais publié)* | Non | Entièrement local dans le Worker : génère `SessionId` + `AuthorizationToken`. Aucun message Service Bus. |
| 2 — EnvoyerLotFichier | `"tdf.envoi"` | **Oui (ZIP)** | Upload ZIP → Blob. Publie `TdfTransactionCommand` dans `tdf-queue`. |
| 3 — CorrellerEnvoyer | `"tdf.correller"` | Non | Corrélation finale. Retourne `IndExecOrcSpec`. Routage conditionnel → `tdf-topic` si `"O"`. |

> **Invariant critique :** `Variables["step"]` est le seul discriminateur d'étape.
> `CurrentStage` n'est pas utilisé (patron **Sequential Convoy**, non Saga EMT).

---

## 3. Contrat de message — TdfTransactionCommand

### 3.1 Record de domaine partagé

```csharp
// Projet : RAMQ.Samples.Queue.TDF.SeqCon.Consumer / Messages/TdfTransactionCommand.cs
public sealed record TdfTransactionCommand(
    string  AuthorizationToken,
    string  NumeroEchange,
    string? BlobReference   = null,   // référence relative blob — Étape 2 uniquement
    string? AccuseReception = null);  // accusé de réception — Étape 3 uniquement
```

### 3.2 Règles sur `SessionId`

- Format PoC : `"POC-" + Guid.NewGuid().ToString("N")`
  ex. `"POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c"`
- **Obligatoire.** Si absent avec sessions activées, EMT lève `ArgumentNullException`.
- Identique aux Étapes 2 et 3 — double rôle : clé de corrélation Service Bus + `InstanceId` Durable.

### 3.3 Règles sur les Tokens (Claim Check)

- `TokenMessage.Reference` = **chemin relatif uniquement**.
  Format : `"tdf/echgfich/poc/{sessionId}/{yyyyMMddHHmmss}/payload.bin"`
  **Jamais une URL absolue, jamais un SAS token.**
- Étape 2 : au moins un `TokenKind.File` (Kind = 1) — référence vers le ZIP uploadé.
- Étape 3 : **aucun token** dans le message entrant. Le Subscriber fusionne les `FileTokens`
  depuis le résultat de l'orchestration Durable **avant** d'invoquer le Consumer.

### 3.4 Enveloppe JSON — Étape 2 (référence)

```json
{
  "MessageType":  "TdfTransactionCommand",
  "MessageId":    "9c4f7f9c2f5f4cd49f767eb86ec2397a",
  "SessionId":    "POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c",
  "CurrentStage": null,
  "Variables": { "step": "tdf.envoi" },
  "Tokens": [
    {
      "Kind":        1,
      "ContentType": "application/octet-stream",
      "Reference":   "tdf/echgfich/poc/POC-a1b2c3d4/20260526143025/payload.bin",
      "SizeBytes":   65536
    }
  ],
  "Message": {
    "AuthorizationToken": "auth-poc-a1b2c3d4",
    "NumeroEchange":      "POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c",
    "BlobReference":      "tdf/echgfich/poc/POC-a1b2c3d4/20260526143025/payload.bin",
    "AccuseReception":    null
  }
}
```

### 3.5 Enveloppe JSON — Étape 3 (référence)

```json
{
  "MessageType":  "TdfTransactionCommand",
  "MessageId":    "3e7c1b9a4d2f4e8c9a0b1c2d3e4f5a6b",
  "SessionId":    "POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c",
  "CurrentStage": null,
  "Variables": { "step": "tdf.correller" },
  "Tokens":   [],
  "Message": {
    "AuthorizationToken": "auth-poc-a1b2c3d4",
    "NumeroEchange":      "POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c",
    "BlobReference":      null,
    "AccuseReception":    "ACK-POC-OK"
  }
}
```

---

## 4. Architecture et flux du PoC — Diagramme de bout en bout

```mermaid
flowchart TD
    subgraph WORKER["RAMQ.Samples.Queue.TDF.SeqCon.Worker (Timer Trigger)"]
        W1["Étape 1 — local\nSessionId + AuthToken"]
        W2["Étape 2\nTelechargerPieceJointe\nConstruireMessage\nPublier → TDF.Queue"]
        W3["Étape 3\nConstruireMessage\nPublier → TDF.Queue"]
        W1 --> W2 --> W3
    end

    QUEUE[("TDF.Queue\nSessions")]

    subgraph SUB["RAMQ.Samples.Queue.TDF.SeqCon.Subscriber (ServiceBus Trigger)"]
        S_BIND["BindContext + TryDeserialize"]
        S_ROUTE{"Variables['step']"}
        S_STEP2["HandleEnvoyerLotFichier\nConsumeAsync → ScheduleOrchestration\nCompleteMessageAsync"]
        S_STEP3["HandleCorrellerEnvoyer\nRaiseEvent → WaitCompletion\nFusionner FileTokens\nConsumeAsync\nsi 'O' → PublishAsync\nCompleteMessageAsync"]
        S_DLQ["DeadLetterMessageAsync"]
        S_BIND --> S_ROUTE
        S_ROUTE -->|"tdf.envoi"| S_STEP2
        S_ROUTE -->|"tdf.correller"| S_STEP3
        S_ROUTE -->|"inconnu"| S_DLQ
    end

    subgraph DURABLE["RAMQ.Samples.Queue.TDF.SeqCon.StateFul (Durable)"]
        SF["FileSent\n↓ CorrellerEnvoyer event\n↓ Valide AuthToken\nCompleted | Failed_Token | Failed_Timeout"]
    end

    subgraph CONS["RAMQ.Samples.Queue.TDF.SeqCon.Consumer (BaseConsumer)"]
        C2["EnvoyerLotFichier\nV + E"]
        C3["CorrellerEnvoyer\nV + T + E\nRetourne IndExecOrcSpec"]
    end

    TOPIC[("TDF.Topic\n+ sub HOA5\n→ HOA5.Queue")]
    HOA5Q[("HOA5.Queue")]

    subgraph HOA5C["RAMQ.Samples.Queue.HOA5.Consumer (ServiceBus Trigger + BaseConsumer)"]
        HC["VTE\nTransforme → TraiterTransmissionRequest\nAppelle HOA5.Backend via Refit"]
    end

    subgraph HOA5B["RAMQ.Samples.Queue.HOA5.Backend (HTTP Trigger)"]
        HB["POST /hoa5/traiter-transmission\nRésout Claim Check\nTélécharge ZIP\nJournalise"]
    end

    BLOB[("Azure Blob Storage\ninter-ppp")]

    WORKER -->|SessionId dans header SB| QUEUE
    QUEUE --> SUB
    SUB <-->|schedule / raise / wait| DURABLE
    SUB --> CONS
    S_STEP3 -->|"IndExecOrcSpec = 'O'"| TOPIC
    TOPIC --> HOA5Q
    HOA5Q --> HOA5C
    HOA5C -->|Refit POST| HOA5B
    HOA5B --> BLOB
    WORKER -->|upload ZIP avant Étape 2| BLOB
```

---

## 5. Composants — Architecture détaillée

---

### 5.0 Justification architecturale — Pourquoi Producer HTTP (pas Native Direct SB)

**Décision :** TDF.Integration.Frontend appelle TDF.Integration.Producer via HttpClient, plutôt que de publier directement dans `tdf-queue`.

**Raisons (voir Section 6 Observabilité) :**

1. ✅ **Message Transit Journal automatique** — EMT journalise chaque message dans Azure Table Storage
2. ✅ **Application Insights traçage distribué complet** — timeline Frontend→Producer→Subscriber→Consumer→API visible
3. ✅ **Gestion états automatique** — STARTED/RETRY/COMPLETED/DLQ sans code personnalisé
4. ✅ **Alertes DLQ intégrées** — détection automatique des messages en erreur
5. ✅ **Indicateurs opérationnels prédéfinis** — taux succès, latence p95, orphelins sans développement supplémentaire
6. ✅ **Conformité RAMQ** — Modèles C (Producer HTTP + JWT validation) + G (Frontend → Producer via OAuth)
7. ✅ **Point d'entrée unique** — Toutes les validations métier et VE centralisées au Producer

**Alternative rejetée (Native Direct SB) :**
- ❌ Aucune journalisation structurée automatique
- ❌ Traçage distribué partiel
- ❌ Gestion états manuelle (~2-3 semaines dev supplémentaire)
- ❌ Alertes DLQ personnalisées
- ❌ Moins conforme RAMQ

---

### 5.1 TDF.Integration.Producer — HTTP Trigger + VE + Publication

**Projet :** `RAMQ.Samples.Queue.TDF.SeqCon.Producer`
**Type :** Azure Function — **HTTP Trigger**
**Rôle :** Reçoit les requêtes HTTP du TDF Frontend. Effectue la validation et l'enrichissement (VE) des messages TDF avant publication dans `tdf-queue` via EMT. **Point d'entrée unique pour tous les messages TDF.**

#### 5.1.1 Responsabilités du TDF Producer

Le Producer expose **trois responsabilités distinctes** :

| Méthode | Responsabilité |
|---------|----------------|
| `RunAsync` (HTTP Trigger) | Point d'entrée HTTP — reçoit JSON `MessageTransitContext`, effectue VE, publie |
| `ValidateAndEnrichAsync` | Valide l'enveloppe message + `Variables["step"]`, peuple les champs manquants |
| `PublishAsync` | Appelle `IMessageProducer<T>.PublishAsync` + journalise |

#### 5.1.2 Point d'entrée HTTP

```csharp
public class TdfProducerFunction
{
    private readonly IMessageProducer<TdfTransactionCommand> _producer;
    private readonly ILogger<TdfProducerFunction> _logger;

    public TdfProducerFunction(
        IMessageProducer<TdfTransactionCommand> producer,
        ILogger<TdfProducerFunction> logger)
    {
        _producer = producer;
        _logger   = logger;
    }

    [Function("TdfProducer")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tdf/produce")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var context = await req.ReadFromJsonAsync<MessageTransitContext<TdfTransactionCommand>>(cancellationToken)
            ?? throw new InvalidOperationException("MessageTransitContext null ou invalide.");

        _logger.LogInformation(
            "TDF Producer reçu. Step={Step}, SessionId={S}",
            context.Variables?.GetValueOrDefault("step") ?? "inconnu",
            context.SessionId);

        // VE : validation et enrichissement
        await ValidateAndEnrichAsync(context, cancellationToken);

        // Publication dans tdf-queue
        await PublishAsync(context, cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(
            new { Status = "OK", MessageId = context.MessageId, SessionId = context.SessionId },
            cancellationToken);
        return response;
    }
```

#### 5.1.3 Validation et enrichissement (VE)

```csharp
    // ─────────────────────────────────────────────────────────────────────
    //  VE : Valide l'enveloppe message et enrichit les champs manquants.
    //  Aucune logique métier — uniquement préparation à la publication.
    // ─────────────────────────────────────────────────────────────────────
    private async Task ValidateAndEnrichAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Message);

        // Valide les champs obligatoires
        if (string.IsNullOrWhiteSpace(context.SessionId))
            throw new InvalidOperationException("SessionId obligatoire.");

        var step = context.Variables?.GetValueOrDefault("step") as string;
        if (string.IsNullOrWhiteSpace(step))
            throw new InvalidOperationException("Variables[\"step\"] obligatoire.");

        if (!new[] { "tdf.envoi", "tdf.correller" }.Contains(step))
            throw new InvalidOperationException($"Variables[\"step\"] invalide : {step}");

        if (string.IsNullOrWhiteSpace(context.Message.NumeroEchange))
            throw new InvalidOperationException("NumeroEchange obligatoire.");

        if (string.IsNullOrWhiteSpace(context.Message.AuthorizationToken))
            throw new InvalidOperationException("AuthorizationToken obligatoire.");

        // Étape 2 : le jeton fichier doit être présent
        if (step == "tdf.envoi")
        {
            var fileToken = context.Tokens?.FirstOrDefault(t => t.Kind == TokenKind.File);
            if (fileToken == null || string.IsNullOrWhiteSpace(fileToken.Reference))
                throw new InvalidOperationException("Token fichier (Kind=File) manquant à l'Étape 2.");

            // Vérifie que Reference n'est pas une URL absolue
            if (fileToken.Reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Token.Reference ne doit pas être une URL absolue.");
        }

        _logger.LogInformation(
            "TDF Producer VE OK. Step={Step}, SessionId={S}, NumeroEchange={N}",
            step, context.SessionId, context.Message.NumeroEchange);

        await Task.CompletedTask;
    }
```

#### 5.1.4 Publication EMT

```csharp
    // ─────────────────────────────────────────────────────────────────────
    //  PUBLIER : délègue à IMessageProducer<TdfTransactionCommand>.PublishAsync.
    //  C'est la SEULE voie autorisée pour envoyer un message dans tdf-queue.
    //  SessionId est propagé automatiquement par EMT dans l'en-tête Service Bus.
    // ─────────────────────────────────────────────────────────────────────
    private async Task PublishAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        await _producer.PublishAsync(context, ct);

        _logger.LogInformation(
            "TDF Producer — Publié. Step={Step}, SessionId={S}, MessageId={Id}",
            context.Variables?["step"], context.SessionId, context.MessageId);
    }
}
```

#### 5.1.5 Configuration DI (`Program.cs`)

```csharp
var builder = new HostBuilder().ConfigureFunctionsWorkerDefaults();

builder.Services
    .AddProducer<TdfTransactionCommand>(opts =>
    {
        opts.Endpoint.EntityName    = builder.Configuration["AppSettings:Queue:EntityName"];
        opts.Endpoint.EnableSession = true;   // TDF.Queue — sessions obligatoires
    })
    .AddSingleton(_ =>
        new BlobServiceClient(builder.Configuration["BlobStorage:ConnectionString"])
            .GetBlobContainerClient("inter-ppp"))
    .Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

builder.Build().Run();
```

#### 5.1.5 Configuration `appsettings.json`

```json
{
  "ServiceBusConnection": "<connection string ou Managed Identity endpoint>",
  "AppSettings": {
    "Queue": { "EntityName": "tdf-queue" }
  }
}
```

**`host.json` :**

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": { "RAMQ": "Information" }
  }
}
```

---

### 5.1bis TDF Worker — Simulateur du TDF Frontend (PoC uniquement)

**Projet :** `RAMQ.Samples.Queue.TDF.SeqCon.Worker`
**Type :** Azure Function — **Timer Trigger** (PoC uniquement, non en production)
**Déclenchement :** `"0 */2 * * * *"` (toutes les 2 minutes — configurable)
**Rôle :** Simule le TDF Frontend pour le PoC. Génère les 3 étapes d'une transaction TDF, effectue le Claim Check (upload Blob), puis appelle le TDF Producer via `HttpClient`.

> **Note PoC :** En production, le TDF Frontend est le service WCF .NET 4.8 qui fait cet appel. Le Worker remplace ce rôle uniquement pour les tests.

#### 5.1bis.1 Orchestration des 3 étapes

```csharp
public class TdfSeqConWorkerFunction
{
    private readonly HttpClient _httpClient;
    private readonly BlobContainerClient _blobContainer;
    private readonly WorkerOptions _options;
    private readonly ILogger<TdfSeqConWorkerFunction> _logger;

    public TdfSeqConWorkerFunction(
        HttpClient httpClient,
        BlobContainerClient blobContainer,
        IOptions<WorkerOptions> options,
        ILogger<TdfSeqConWorkerFunction> logger)
    {
        _httpClient    = httpClient;
        _blobContainer = blobContainer;
        _options       = options.Value;
        _logger        = logger;
    }

    [Function("TdfSeqConWorker")]
    public async Task Run(
        [TimerTrigger("%Worker:TimerSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("TDF Worker démarré. {Timestamp}", DateTimeOffset.UtcNow);
        await ExecuterTransactionTroisEtapesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  3 ÉTAPES : Étape 1 (local) → Étape 2 (Claim Check + appel Producer)
    //             → Étape 3 (appel Producer)
    // ─────────────────────────────────────────────────────────────────────
    private async Task ExecuterTransactionTroisEtapesAsync(CancellationToken ct)
    {
        // ── ÉTAPE 1 — InitierEnvoi (entièrement local, aucun message Service Bus) ──────
        var sessionId     = "POC-" + Guid.NewGuid().ToString("N");
        var authToken     = "auth-poc-" + Guid.NewGuid().ToString("N")[..12];
        var numeroEchange = sessionId;

        _logger.LogInformation(
            "Étape 1 (local). SessionId={S}, AuthToken={A}", sessionId, authToken);

        // ── ÉTAPE 2 — EnvoyerLotFichier ─────────────────────────────────────────────
        var (blobPath, sizeBytes) = await TelechargerPieceJointeAsync(sessionId, ct);

        var ctxEtape2 = ConstruireMessage(
            step           : "tdf.envoi",
            sessionId      : sessionId,
            authToken      : authToken,
            numeroEchange  : numeroEchange,
            blobPath       : blobPath,
            sizeBytes      : sizeBytes,
            accuseReception: null);

        await AppelerProducerAsync(ctxEtape2, ct);
        _logger.LogInformation("Étape 2 envoyée au Producer. SessionId={S}, BlobPath={B}", sessionId, blobPath);

        // ── ÉTAPE 3 — CorrellerEnvoyer (après délai configurable) ────────────────────
        await Task.Delay(TimeSpan.FromSeconds(_options.Step3DelaySeconds), ct);

        var ctxEtape3 = ConstruireMessage(
            step           : "tdf.correller",
            sessionId      : sessionId,
            authToken      : authToken,    // DOIT correspondre — l'orchestrateur valide
            numeroEchange  : numeroEchange,
            blobPath       : null,         // aucune pièce jointe à l'Étape 3
            sizeBytes      : 0,
            accuseReception: "ACK-POC-OK");

        await AppelerProducerAsync(ctxEtape3, ct);
        _logger.LogInformation("Étape 3 envoyée au Producer. SessionId={S}", sessionId);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CLAIM CHECK : génère un ZIP de test et l'uploade dans Blob Storage.
    //  Retourne (blobPath, sizeBytes). Ne retourne jamais d'URL absolue.
    // ─────────────────────────────────────────────────────────────────────
    private async Task<(string BlobPath, long SizeBytes)> TelechargerPieceJointeAsync(
        string sessionId, CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var blobPath  = $"tdf/echgfich/poc/{sessionId}/{timestamp}/payload.bin";

        // Contenu de test : 64 Ko de données aléatoires simulant un ZIP
        var zipBytes = new byte[64 * 1024];
        Random.Shared.NextBytes(zipBytes);

        var blobClient = _blobContainer.GetBlobClient(blobPath);
        await blobClient.UploadAsync(
            content   : new BinaryData(zipBytes),
            overwrite : false,            // idempotence — ne jamais écraser
            cancellationToken: ct);

        _logger.LogInformation(
            "Claim Check téléversé. BlobPath={P}, Size={S} octets", blobPath, zipBytes.Length);

        // Retourne le chemin RELATIF — jamais d'URL absolue ni de SAS token
        return (blobPath, zipBytes.Length);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  MESSAGE : construit le MessageTransitContext<TdfTransactionCommand>
    //  pour l'étape demandée.
    //  — Étape 2 : ajoute un TokenKind.File avec la référence relative blob.
    //  — Étape 3 : aucun token (le Subscriber fusionnera les FileTokens).
    // ─────────────────────────────────────────────────────────────────────
    private static MessageTransitContext<TdfTransactionCommand> ConstruireMessage(
        string  step,
        string  sessionId,
        string  authToken,
        string  numeroEchange,
        string? blobPath,
        long    sizeBytes,
        string? accuseReception)
    {
        var tokens = new List<TokenMessage>();

        if (step == "tdf.envoi" && !string.IsNullOrWhiteSpace(blobPath))
        {
            tokens.Add(new TokenMessage
            {
                Kind        = TokenKind.File,           // Kind = 1
                ContentType = "application/octet-stream",
                Reference   = blobPath,                 // chemin RELATIF — jamais d'URL
                SizeBytes   = sizeBytes
            });
        }

        return new MessageTransitContext<TdfTransactionCommand>
        {
            MessageType  = nameof(TdfTransactionCommand),
            MessageId    = Guid.NewGuid().ToString("N"),
            SessionId    = sessionId,       // OBLIGATOIRE — EMT lève ArgumentNullException si absent
            CurrentStage = null,            // non utilisé — patron Sequential Convoy
            Variables    = new Dictionary<string, object> { ["step"] = step },
            Tokens       = tokens,
            Message      = new TdfTransactionCommand(
                AuthorizationToken : authToken,
                NumeroEchange      : numeroEchange,
                BlobReference      : blobPath,
                AccuseReception    : accuseReception)
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  APPEL PRODUCER : envoie le message via HttpClient au TDF Producer.
    //  C'est le pont vers l'architecture Azure — simule l'appel du TDF Frontend.
    // ─────────────────────────────────────────────────────────────────────
    private async Task AppelerProducerAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        var producerUrl = _options.ProducerEndpoint;
        var content = JsonContent.Create(context);

        var response = await _httpClient.PostAsync(producerUrl, content, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "TDF Worker → Producer OK. Step={Step}, SessionId={S}",
            context.Variables["step"], context.SessionId);
    }
}
```

#### 5.1bis.2 Configuration DI et Options (`Program.cs`)

```csharp
builder.Services
    .AddHttpClient<TdfSeqConWorkerFunction>()
    .AddSingleton(_ =>
        new BlobServiceClient(builder.Configuration["BlobStorage:ConnectionString"])
            .GetBlobContainerClient("inter-ppp"))
    .Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

builder.Build().Run();
```

**Options :**

```csharp
public sealed class WorkerOptions
{
    public string TimerSchedule       { get; set; } = "0 */2 * * * *";
    public int    Step3DelaySeconds   { get; set; } = 5;
    public string ProducerEndpoint    { get; set; } = "https://<function-app>.azurewebsites.net/api/tdf/produce";
}
```

**`appsettings.json` (Worker):**

```json
{
  "BlobStorage": {
    "ConnectionString": "<connection string ou Managed Identity>"
  },
  "Worker": {
    "TimerSchedule":    "0 */2 * * * *",
    "Step3DelaySeconds": 5,
    "ProducerEndpoint": "https://<function-app>.azurewebsites.net/api/tdf/produce"
  }
}
```

---

### 5.2 TDF Subscriber — Orchestration légère + Routage par `Variables["step"]`

**Projet :** `RAMQ.Samples.Queue.TDF.SeqCon.Subscriber`
**Type :** Azure Function — ServiceBus Trigger
**Rôle :** Couche d'orchestration mince sur `tdf-queue`. **Aucune logique métier.**
Route par `Variables["step"]` vers les Consumers appropriés. Coordonne le Durable Orchestrator. Seul responsable du `PublishAsync` vers `tdf-topic`.

#### Règles absolues

| Règle | Description |
|-------|-------------|
| `AutoCompleteMessages = false` | EMT contrôle le cycle de vie du message |
| `IsSessionsEnabled = true` | Sequential Convoy — une session par échange |
| Jamais `ServiceBusMessageActions` directement | Toujours via `_consumer.CompleteMessageAsync` / `DeadLetterMessageAsync` |
| Complétion différée | `_consumer.CompleteMessageAsync` est **toujours le dernier appel** |
| Pas de logique métier | Tout le VTE délégué au Consumer |
| `PublishAsync` dans le Subscriber | Le Consumer retourne `IndExecOrcSpec` — c'est le Subscriber qui publie vers `tdf-topic` |

#### Structure de classe

```csharp
public class TdfSeqConSubscriberFunction
{
    private readonly TdfSeqConConsumer _consumer;
    private readonly IMessageProducer<TdfTransactionCommand> _topicProducer;
    private readonly ILogger<TdfSeqConSubscriberFunction> _logger;

    public TdfSeqConSubscriberFunction(
        TdfSeqConConsumer consumer,
        IMessageProducer<TdfTransactionCommand> topicProducer,
        ILogger<TdfSeqConSubscriberFunction> logger)
    { /* ... */ }

    [Function("TdfSeqConSubscriber")]
    public async Task RunAsync(
        [ServiceBusTrigger(
            "%AppSettings:Queue:EntityName%",
            Connection          = "ServiceBusConnection",
            AutoCompleteMessages = false,
            IsSessionsEnabled   = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _consumer.BindContext(message, actions);

        if (!_consumer.TryDeserializeMessage<TdfTransactionCommand>(out var context))
        {
            await _consumer.DeadLetterMessageAsync(
                new InvalidOperationException("Format de message invalide."),
                cancellationToken);
            return;
        }

        var step = context.Variables.GetValueOrDefault("step") as string;
        _logger.LogInformation("Reçu. Step={S}, SessionId={Id}", step, context.SessionId);

        switch (step)
        {
            case "tdf.envoi":
                await HandleEnvoyerLotFichierAsync(context, durableClient, cancellationToken);
                break;

            case "tdf.correller":
                await HandleCorrellerEnvoyerAsync(context, durableClient, cancellationToken);
                break;

            default:
                await _consumer.DeadLetterMessageAsync(
                    new InvalidOperationException($"Variables['step'] inconnu : '{step}'"),
                    cancellationToken);
                break;
        }
    }
```

#### Handler Étape 2

```csharp
    private async Task HandleEnvoyerLotFichierAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        DurableTaskClient durableClient, CancellationToken ct)
    {
        // 1. Consumer V+E — valide enveloppe + tokens, pas d'appel API
        await _consumer.ConsumeAsync(context, ct);

        // 2. Démarrer orchestration (InstanceId = SessionId — corrélation 1:1)
        var instanceId = context.SessionId
            ?? throw new InvalidOperationException("SessionId manquant.");

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            "TdfTransactionOrchestrator",
            new EnvoyerLotFichierEvent
            {
                SessionId          = context.SessionId,
                NumeroEchange      = context.Message.NumeroEchange,
                AuthorizationToken = context.Message.AuthorizationToken,
                BlobReference      = context.Message.BlobReference,
                FileTokens         = context.Tokens?.ToList(),
                MessageId          = context.MessageId
            },
            new StartOrchestrationOptions { InstanceId = instanceId },
            ct);

        _logger.LogInformation("Orchestration démarrée. InstanceId={Id}", instanceId);

        // 3. Complétion différée — TOUJOURS en dernier
        await _consumer.CompleteMessageAsync(ct);
    }
```

#### Handler Étape 3

```csharp
    private async Task HandleCorrellerEnvoyerAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        DurableTaskClient durableClient, CancellationToken ct)
    {
        var instanceId = context.SessionId
            ?? throw new InvalidOperationException("SessionId manquant.");

        // 1. Notifier l'orchestrateur de l'arrivée de l'Étape 3
        await durableClient.RaiseEventAsync(
            instanceId,
            "CorrellerEnvoyer",
            new CorrellerEnvoyerEvent
            {
                AuthorizationToken = context.Message.AuthorizationToken,
                MessageId          = context.MessageId
            },
            ct);

        // 2. Attendre la complétion (validation token + récupération données Étape 2)
        var result = await durableClient.WaitForInstanceCompletionAsync(instanceId, ct);

        // 3. Échec orchestration → DLQ immédiate, pas de replay
        if (result.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
        {
            _logger.LogWarning("Orchestration échouée. Status={S}", result.RuntimeStatus);
            await _consumer.DeadLetterMessageAsync(
                new InvalidOperationException(
                    $"Orchestration {instanceId} : {result.RuntimeStatus}. NonReplayable."),
                ct);
            return;
        }

        // 4. Récupérer résultat Étape 2 depuis l'orchestration
        var correlation = result.ReadOutputAs<TransactionCorrelationResult>();

        // 5. Fusionner FileTokens de l'Étape 2 dans le contexte courant
        //    L'Étape 3 n'a pas de tokens — ils ont été transportés via l'orchestration.
        if (correlation.FileTokens?.Count > 0)
        {
            context.Tokens ??= [];
            context.Tokens.AddRange(correlation.FileTokens);
        }

        // 6. Consumer VTE complet avec contexte enrichi
        var consumerResult = await _consumer.ConsumeAsync(context, ct);

        // 7. Publication conditionnelle vers TDF.Topic
        //    IMPORTANT : le Subscriber orchestre — pas le Consumer.
        if (consumerResult?.IndExecOrcSpec == "O")
        {
            await _topicProducer.PublishAsync(
                new MessageTransitContext<TdfTransactionCommand>
                {
                    MessageType  = nameof(TdfTransactionCommand),
                    MessageId    = Guid.NewGuid().ToString("N"),
                    SessionId    = context.SessionId,
                    CurrentStage = null,
                    Variables    = new() { ["step"] = "tdf.hoa5" },
                    Tokens       = context.Tokens,
                    Message      = context.Message
                },
                properties: new Dictionary<string, object>
                {
                    ["Consumer"] = "All",
                    ["Action"]   = "http://RAMQ.HO.HOA5_ServTransmPrel_bt.SchFichCHSpec"
                },
                ct);

            _logger.LogInformation("Publié vers TDF.Topic. SessionId={S}", context.SessionId);
        }

        // 8. Complétion différée — TOUJOURS en dernier
        await _consumer.CompleteMessageAsync(ct);
    }
}
```

#### Configuration DI (`Program.cs`)

```csharp
builder.Services
    .AddConsumer<TdfTransactionCommand, TdfSeqConConsumer>()
    .AddProducer<TdfTransactionCommand>(name: "queue", opts =>
    {
        opts.Endpoint.EntityName    = config["AppSettings:Queue:EntityName"];  // TDF.Queue
        opts.Endpoint.EnableSession = true;
    })
    .AddProducer<TdfTransactionCommand>(name: "topic", opts =>
    {
        opts.Endpoint.EntityName = config["AppSettings:Topic:EntityName"];     // TDF.Topic
    });
```

**`host.json` :**

```json
{
  "version": "2.0",
  "extensions": {
    "serviceBus": {
      "prefetchCount": 0,
      "messageHandlerOptions": {
        "maxConcurrentCalls": 2,
        "autoComplete": false,
        "maxAutoRenewDuration": "00:05:00"
      }
    },
    "durableTask": {
      "storageProvider": { "type": "azure" }
    }
  }
}
```

---

### 5.3 TDF Consumers — VTE (Validation, Transform, Enrich)

**Projet :** `RAMQ.Samples.Queue.TDF.SeqCon.Consumer`
**Type :** Librairie .NET 8 — `BaseConsumer<TdfTransactionCommand>` (EMT)
**Rôle :** Logique métier des Étapes 2 et 3. Deux consommateurs spécifiques aux étapes : `EnvoyerLotFichierConsumer` (Étape 2 — V+E) et `CorrellerEnvoyerConsumer` (Étape 3 — V+T+E).
**Contraintes :** Aucune référence à Service Bus. Pas de `IMessageProducer`. Pas de `CompleteMessageAsync`.

```csharp
public class TdfSeqConConsumer : BaseConsumer<TdfTransactionCommand>
{
    private readonly IHoa5BackendApi _api;    // Refit — appelé à l'Étape 3 uniquement
    private readonly ILogger<TdfSeqConConsumer> _logger;

    protected override async Task<ConsumeResult> ConsumeAsync(
        MessageTransitContext<TdfTransactionCommand> context,
        CancellationToken ct)
    {
        var step = context.Variables.GetValueOrDefault("step") as string;
        return step switch
        {
            "tdf.envoi"     => await ConsumeEnvoyerLotFichierAsync(context, ct),
            "tdf.correller" => await ConsumeCorrellerEnvoyerAsync(context, ct),
            _               => throw new InvalidOperationException($"Step inconnu : '{step}'")
        };
    }
```

#### Consumer Étape 2 — V + E

```csharp
    // Validate + Enrich uniquement. Pas d'appel API. Pas de résolution Claim Check.
    private Task<ConsumeResult> ConsumeEnvoyerLotFichierAsync(
        MessageTransitContext<TdfTransactionCommand> context, CancellationToken _)
    {
        // ── VALIDATE ────────────────────────────────────────────────────
        ArgumentNullException.ThrowIfNull(context.Message);

        if (string.IsNullOrWhiteSpace(context.Message.NumeroEchange))
            throw new InvalidOperationException("NumeroEchange manquant.");

        if (string.IsNullOrWhiteSpace(context.Message.AuthorizationToken))
            throw new InvalidOperationException("AuthorizationToken manquant.");

        var fileToken = context.Tokens?.FirstOrDefault(t => t.Kind == TokenKind.File)
            ?? throw new InvalidOperationException("Token fichier (Kind=File) manquant.");

        if (string.IsNullOrWhiteSpace(fileToken.Reference))
            throw new InvalidOperationException("Token.Reference vide.");

        if (fileToken.Reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
         || fileToken.Reference.Contains('?'))
            throw new InvalidOperationException(
                "Token.Reference ne doit pas être une URL absolue. Chemin relatif uniquement.");

        // ── ENRICH ──────────────────────────────────────────────────────
        // Le fichier reste dans Blob Storage — résolution par HOA5.Backend.
        _logger.LogInformation(
            "Étape 2 validée. NumeroEchange={N}, TokenRef={R}",
            context.Message.NumeroEchange, fileToken.Reference);

        return Task.FromResult(new ConsumeResult());
    }
```

#### Consumer Étape 3 — V + T + E

```csharp
    // Validate + Transform + Enrich. Appelle HOA5.Backend. Retourne IndExecOrcSpec.
    private async Task<ConsumeResult> ConsumeCorrellerEnvoyerAsync(
        MessageTransitContext<TdfTransactionCommand> context, CancellationToken ct)
    {
        // ── VALIDATE ────────────────────────────────────────────────────
        ArgumentNullException.ThrowIfNull(context.Message);

        if (string.IsNullOrWhiteSpace(context.Message.AccuseReception))
            throw new InvalidOperationException("AccuseReception manquant.");

        if (string.IsNullOrWhiteSpace(context.Message.NumeroEchange))
            throw new InvalidOperationException("NumeroEchange manquant.");

        // ── TRANSFORM ───────────────────────────────────────────────────
        // Remplace la map BizTalk InscrireSuiviFich → contrat OOA2
        var request = new InscrireSuiviFichCorlnRequest
        {
            NoEchg          = context.Message.NumeroEchange,
            AccuseReception = context.Message.AccuseReception,
            Erreur          = "0"
        };

        // ── ENRICH ──────────────────────────────────────────────────────
        // Idempotency-Key = MessageId → protège contre les replays Service Bus
        // X-Correlation-Id = SessionId → traçabilité bout-en-bout
        var response = await _api.InscrireSuiviFichCorlnAsync(
            idempotencyKey : context.MessageId,
            correlationId  : context.SessionId,
            request        : request,
            ct);

        _logger.LogInformation(
            "Étape 3 traitée. NumeroEchange={N}, IndExecOrcSpec={I}",
            context.Message.NumeroEchange, response.IndExecOrcSpec);

        // Retourne IndExecOrcSpec au Subscriber — c'est lui qui décide de PublishAsync
        return new ConsumeResult { IndExecOrcSpec = response.IndExecOrcSpec };
    }
}
```

#### Contrats et interface Refit

```csharp
// IHoa5BackendApi.cs
[Headers("Content-Type: application/json")]
public interface IHoa5BackendApi
{
    [Post("/hoa5/inscrire-suivi-fich")]
    Task<InscrireSuiviFichCorlnResponse> InscrireSuiviFichCorlnAsync(
        [Header("Idempotency-Key")] string idempotencyKey,
        [Header("X-Correlation-Id")] string correlationId,
        [Body] InscrireSuiviFichCorlnRequest request,
        CancellationToken cancellationToken = default);
}

public record InscrireSuiviFichCorlnRequest
{
    public required string NoEchg          { get; init; }
    public required string AccuseReception { get; init; }
    public required string Erreur          { get; init; }
}

public record InscrireSuiviFichCorlnResponse
{
    public required string IndExecOrcSpec { get; init; }   // "O" ou "N"
    public required string NoEchg         { get; init; }
    public required string Erreur         { get; init; }
}

public record ConsumeResult
{
    public string? IndExecOrcSpec { get; init; }
}
```

---

### 5.4 TDF Durable Orchestrator — Corrélation d'état

**Projet :** `RAMQ.Samples.Queue.TDF.SeqCon.StateFul`
**Type :** Azure Durable Function
**Rôle :** Machine à états inter-étapes. Valide le `AuthorizationToken`. Aucun appel API. Aucune logique métier.

#### Modèles

```csharp
// Input (depuis Subscriber après Étape 2)
public record EnvoyerLotFichierEvent
{
    public required string             SessionId          { get; init; }
    public required string             NumeroEchange      { get; init; }
    public required string             AuthorizationToken { get; init; }
    public string?                     BlobReference      { get; init; }
    public List<TokenMessage>?         FileTokens         { get; init; }
    public required string             MessageId          { get; init; }
}

// Événement externe (depuis Subscriber après Étape 3)
public record CorrellerEnvoyerEvent
{
    public required string AuthorizationToken { get; init; }
    public required string MessageId          { get; init; }
}

// Output (retourné au Subscriber)
public record TransactionCorrelationResult
{
    public required string             AuthorizationToken { get; init; }
    public string?                     BlobReference      { get; init; }
    public required string             NumeroEchange      { get; init; }
    public List<TokenMessage>?         FileTokens         { get; init; }
}
```

#### Orchestrateur

```csharp
public class TdfTransactionOrchestrator
{
    [Function("TdfTransactionOrchestrator")]
    public async Task<TransactionCorrelationResult> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var input = ctx.GetInput<EnvoyerLotFichierEvent>()
            ?? throw new InvalidOperationException("Input orchestration null.");

        ctx.SetCustomStatus("FileSent");

        // Minuteur : 30 s en PoC (pour valider le chemin d'erreur timeout),
        // 24 h en production. Configurable via variable d'env DURABLE_TIMEOUT_SECONDS.
        var timeout = ctx.GetInput<int?>() is { } secs
            ? TimeSpan.FromSeconds(secs)
            : TimeSpan.FromSeconds(30);   // PoC par défaut

        using var timerCts = new CancellationTokenSource();
        var correlerTask   = ctx.WaitForExternalEvent<CorrellerEnvoyerEvent>("CorrellerEnvoyer");
        var timerTask      = ctx.CreateTimer(ctx.CurrentUtcDateTime.Add(timeout), timerCts.Token);

        // Utilise CreateTimer (persisté) — JAMAIS Task.Delay (non persisté, non rejouable)
        var winner = await Task.WhenAny(correlerTask, timerTask);

        if (winner == timerTask)
        {
            ctx.SetCustomStatus("Failed_Timeout");
            throw new TimeoutException(
                $"Étape 3 non reçue dans le délai imparti. SessionId={input.SessionId}");
        }

        timerCts.Cancel();
        var correlEvent = await correlerTask;

        // Valide que l'AuthorizationToken de l'Étape 3 correspond à celui de l'Étape 2
        if (correlEvent.AuthorizationToken != input.AuthorizationToken)
        {
            ctx.SetCustomStatus("Failed_Token");
            throw new InvalidOperationException(
                $"AuthorizationToken invalide à l'Étape 3. SessionId={input.SessionId}");
        }

        ctx.SetCustomStatus("Completed");

        return new TransactionCorrelationResult
        {
            AuthorizationToken = input.AuthorizationToken,
            BlobReference      = input.BlobReference,
            NumeroEchange      = input.NumeroEchange,
            FileTokens         = input.FileTokens
        };
    }
}
```

**Machine à états :**

```
[*] ──► FileSent
FileSent ──► Completed      (Étape 3 reçue + AuthToken valide)
FileSent ──► Failed_Token   (AuthToken Étape 3 ≠ AuthToken Étape 2)
FileSent ──► Failed_Timeout (30 s PoC / 24 h prod sans Étape 3)
```

---

### 5.5 HOA5 Subscriber + Consumer — VTE vers Backend

**Projet :** `RAMQ.Samples.Queue.HOA5.Consumer`
**Type :** Azure Function — ServiceBus Trigger + `BaseConsumer<TdfTransactionCommand>` (EMT)
**Rôle :** Réception sur `hoa5-queue`. VTE (Validation, Transform, Enrich) : transforme TDF → contrat HOA5 Backend + appel Refit.

#### Subscriber HOA5

```csharp
[Function("Hoa5Subscriber")]
public async Task RunAsync(
    [ServiceBusTrigger(
        "%AppSettings:Queue:EntityName%",
        Connection          = "ServiceBusConnection",
        AutoCompleteMessages = false)]   // autoComplete: false dans host.json aussi
    ServiceBusReceivedMessage message,
    ServiceBusMessageActions  actions,
    CancellationToken cancellationToken)
{
    _consumer.BindContext(message, actions);

    if (!_consumer.TryDeserializeMessage<TdfTransactionCommand>(out var context))
    {
        await _consumer.DeadLetterMessageAsync(
            new InvalidOperationException("Désérialisation échouée."), cancellationToken);
        return;
    }

    await _consumer.ConsumeAsync(context, cancellationToken);
    await _consumer.CompleteMessageAsync(cancellationToken);   // DERNIER appel
}
```

#### Consumer HOA5 (VTE)

```csharp
public class Hoa5Consumer : BaseConsumer<TdfTransactionCommand>
{
    protected override async Task<ConsumeResult> ConsumeAsync(
        MessageTransitContext<TdfTransactionCommand> context, CancellationToken ct)
    {
        // ── VALIDATE ────────────────────────────────────────────────────
        ArgumentNullException.ThrowIfNull(context.Message);
        if (string.IsNullOrWhiteSpace(context.Message.NumeroEchange))
            throw new InvalidOperationException("NumeroEchange manquant.");

        var fileToken = context.Tokens?.FirstOrDefault(t => t.Kind == TokenKind.File);

        // ── TRANSFORM ───────────────────────────────────────────────────
        // Remplace la map BizTalk transformerMsgFichCHToMsgSVC.btm
        var request = new TraiterTransmissionRequest
        {
            NumeroEchange        = context.Message.NumeroEchange,
            AuthorizationToken   = context.Message.AuthorizationToken,
            AccuseReception      = context.Message.AccuseReception,
            FileTokenReference   = fileToken?.Reference,     // chemin relatif UNIQUEMENT
            FileTokenContentType = fileToken?.ContentType,
            FileTokenSizeBytes   = fileToken?.SizeBytes
        };

        // ── ENRICH ──────────────────────────────────────────────────────
        await _backendClient.TraiterTransmissionAsync(
            idempotencyKey : context.MessageId,
            correlationId  : context.SessionId,
            request        : request,
            ct);

        _logger.LogInformation(
            "HOA5 Backend appelé. NumeroEchange={N}, TokenRef={R}",
            context.Message.NumeroEchange, fileToken?.Reference);

        return new ConsumeResult();
    }
}
```

#### Interface Refit vers HOA5.Backend

```csharp
[Headers("Content-Type: application/json")]
public interface IHoa5BackendClient
{
    [Post("/hoa5/traiter-transmission")]
    Task TraiterTransmissionAsync(
        [Header("Idempotency-Key")] string idempotencyKey,
        [Header("X-Correlation-Id")] string correlationId,
        [Body] TraiterTransmissionRequest request,
        CancellationToken cancellationToken = default);
}

public record TraiterTransmissionRequest
{
    public required string  NumeroEchange        { get; init; }
    public required string  AuthorizationToken   { get; init; }
    public string?          AccuseReception      { get; init; }
    public string?          FileTokenReference   { get; init; }   // chemin relatif blob
    public string?          FileTokenContentType { get; init; }
    public long?            FileTokenSizeBytes   { get; init; }
}
```

---

### 5.6 HOA5 Backend — Résolution de jetons Claim Check

**Projet :** `RAMQ.Samples.Queue.HOA5.Backend`
**Type :** Azure Function — HTTP Trigger
**Rôle :** Backend HOA5. Reçoit `TraiterTransmissionRequest`, résout le Claim Check depuis Azure Blob Storage (téléchargement blob), journalise, retourne `200 OK`.

```csharp
[Function("TraiterTransmission")]
public async Task<HttpResponseData> RunAsync(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "hoa5/traiter-transmission")]
    HttpRequestData req, CancellationToken ct)
{
    var idempotencyKey = req.Headers.TryGetValues("Idempotency-Key", out var keys)
        ? keys.FirstOrDefault() : null;
    var correlationId  = req.Headers.TryGetValues("X-Correlation-Id", out var ids)
        ? ids.FirstOrDefault() : null;

    var request = await req.ReadFromJsonAsync<TraiterTransmissionRequest>(ct);

    if (string.IsNullOrWhiteSpace(request?.NumeroEchange))
        return req.CreateResponse(HttpStatusCode.BadRequest);

    _logger.LogInformation(
        "HOA5 Backend. Key={K}, NumeroEchange={N}, TokenRef={R}",
        idempotencyKey, request.NumeroEchange, request.FileTokenReference);

    // Résolution Claim Check — télécharge le ZIP si token présent
    if (!string.IsNullOrWhiteSpace(request.FileTokenReference))
    {
        var blob     = _blobContainer.GetBlobClient(request.FileTokenReference);
        var download = await blob.DownloadContentAsync(ct);
        var content  = download.Value.Content.ToArray();

        // PoC : journalisation uniquement.
        // Production : décompresser ZIP, parser XML, insérer Oracle.
        _logger.LogInformation(
            "Claim Check résolu. Taille={S} octets, ContentType={C}",
            content.Length, request.FileTokenContentType);
    }

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(
        new { Status = "OK", NumeroEchange = request.NumeroEchange }, ct);
    return response;
}
```

---

## 6. Observabilité — Message Transit Journal + Application Insights

### 6.1 Message Transit Journal (EMT)

EMT écrit automatiquement dans **Azure Table Storage** (`MessageTransitJournal`) pour chaque opération de Producer et Consumer.
Cela remplace la **BizTalk Tracking DB non fonctionnelle**.

**Structure des enregistrements :**

| Champ | Description | Exemple |
|-------|-------------|---------|
| `PartitionKey` | Domaine d'application | `"TDF"` ou `"HOA5"` |
| `RowKey` | `{MessageId}_{Timestamp}` | `"9c4f7f9c2f5f4cd49f767eb86ec2397a_20260527T103005Z"` |
| `SessionId` | Corrélation transaction | `"POC-a1b2c3d4e5f641a28b3c4d5e6f7a8b9c"` |
| `MessageId` | Traçabilité bout en bout | `"9c4f7f9c2f5f4cd49f767eb86ec2397a"` |
| `Mode` | État du cycle de vie | `STARTED`, `RETRY`, `COMPLETED`, `DLQ` |
| `DeliveryCount` | Numéro de tentative | `1`, `2`, `3` |
| `Consumer` | Classe Consumer exécutée | `"EnvoyerLotFichierConsumer"` |
| `Action` | Action métier (step) | `"tdf.envoi"` |
| `StatusCode` | HTTP ou exception | `200`, `500`, `null` |
| `ErrorDescription` | Si Mode=DLQ | `"AuthToken invalid"` |
| `EnqueuedTimeUtc` | Mis en file Service Bus | `"2026-05-27T10:30:00.000Z"` |
| `StartedTimeUtc` | Début Consumer | `"2026-05-27T10:30:00.500Z"` |
| `CompletedTimeUtc` | Fin Consumer | `"2026-05-27T10:30:05.200Z"` |

**Requête Analytics — Messages par état :**

```sql
SELECT Mode, COUNT(*) as Count, AVG(DATEDIFF(ms, StartedTimeUtc, CompletedTimeUtc)) as AvgDurationMs
FROM MessageTransitJournal
WHERE PartitionKey='TDF' AND Timestamp >= DATEADD(hour,-1,GETUTCDATE())
GROUP BY Mode
ORDER BY Count DESC
```

### 6.2 Application Insights — Traçage distribué

Toutes les Azure Functions sont instrumentées automatiquement. Cela capture :

**Traçage frontend-to-backend :**
```
TDF.Integration.Frontend [Timer Trigger]
  ├─ CustomEvent: "TDF.Integration.Frontend.Started" {SessionId, Timestamp}
  ├─ Dependency: "Blob Storage Upload" {BlobPath, SizeBytes}
  ├─ Dependency: "HttpClient POST" {ProducerUrl, StatusCode=200}
  │
  └─ TDF.Integration.Producer [HTTP Trigger]
    ├─ CustomEvent: "TDF.Integration.Producer.VE.Started" {SessionId, Step}
    ├─ Dependency: "IMessageProducer.PublishAsync" {SessionId, MessageId}
    │
    └─ Azure Service Bus — tdf-queue
      ├─ Message enqueued {SessionId, DeliveryCount=1}
      │
      └─ TDF.Integration.Subscriber [ServiceBus Trigger]
        ├─ Request: "HandleEnvoyerLotFichierAsync | HandleCorrellerEnvoyerAsync"
        ├─ Dependency: "TDF.Integration.Orchestrator (Durable)"
        │
        └─ TDF.Integration.Consumer
          ├─ CustomEvent: "ConsumeAsync.Started" {Step, SessionId}
          ├─ Dependency: "OOA2_InscrireSuiviFich_Ws" [Étape 3] {Duration=150ms, StatusCode=200}
          └─ CustomEvent: "ConsumeAsync.Completed" {Step, IndExecOrcSpec}
```

**Latence end-to-end visible dans AppInsights :**
- Frontend → Producer : ~10ms (HTTP)
- Producer VE + Publish : ~8ms
- Service Bus latency : ~5ms
- Subscriber trigger + Consumer : ~160ms (incluant appel OOA2)
- **Total Frontend → Completed : ~180ms**

### 6.3 Indicateurs opérationnels clés

| Indicateur | Calcul | Seuil d'alerte |
|-----------|--------|----------------|
| **Taux de succès bout en bout** | `COUNT(Mode='COMPLETED') / COUNT(*)` | < 99% |
| **Latence p95** | `PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY CompletedTimeUtc - StartedTimeUtc)` | > SLA défini |
| **Taux de réessai** | `COUNT(DeliveryCount > 1) / COUNT(*)` | Augmentation soudaine |
| **Volume DLQ** | `COUNT(Mode='DLQ') par Consumer` | Toute DLQ = triage requis |
| **Messages orphelins** | TDF `COMPLETED` sans HOA5 `COMPLETED` (même MessageId) après 15min | > 0 = investigation |
| **Zombies Orchestrator** | Instances Durable actives depuis >24h | = 0 (minuteurs anti-zombie) |

### 6.4 Alertes Azure Monitor

```json
{
  "alerts": [
    {
      "name": "TDF DLQ Alert",
      "condition": "COUNT(MessageTransitJournal.Mode='DLQ') > 0",
      "actionGroup": "TDF-OnCall",
      "description": "Messages entrant en DLQ. Vérifier ErrorDescription."
    },
    {
      "name": "TDF Latency p95 Exceeded",
      "condition": "p95(CompletedTimeUtc - StartedTimeUtc) > 500ms",
      "actionGroup": "TDF-Performance",
      "description": "Dégradation de latence. Vérifier Consumer VTE + appels API en aval."
    },
    {
      "name": "TDF Orphan Messages",
      "condition": "TDF.COMPLETED sans HOA5.COMPLETED après 15min",
      "actionGroup": "TDF-OnCall",
      "description": "Transfert échoué de TDF vers HOA5. Vérifier tdf-topic subscription."
    }
  ]
}
```

---

## 7. Topologie de la solution — Répertoires et projets

```
RAMQ.Samples.Queue.TDF.SeqCon/
│
├── RAMQ.Samples.Queue.TDF.SeqCon.sln
│
├── RAMQ.Samples.Queue.TDF.SeqCon.Worker/
│   ├── *.csproj                           # net8.0, Functions.Worker, Storage.Blobs
│   ├── Program.cs                         # AddProducer<TdfTransactionCommand>
│   ├── host.json
│   ├── appsettings.json
│   ├── Options/WorkerOptions.cs
│   └── Functions/
│       └── TdfSeqConWorkerFunction.cs     # TimerTrigger + 4 méthodes
│
├── RAMQ.Samples.Queue.TDF.SeqCon.Subscriber/
│   ├── *.csproj                           # net8.0, Functions.Worker, DurableTask.Client
│   ├── Program.cs                         # AddConsumer + AddProducer (queue + topic)
│   ├── host.json                          # autoComplete: false, maxConcurrentCalls: 2
│   ├── appsettings.json
│   └── Functions/
│       └── TdfSeqConSubscriberFunction.cs # ServiceBusTrigger + DurableClient
│
├── RAMQ.Samples.Queue.TDF.SeqCon.Consumer/
│   ├── *.csproj                           # net8.0, classlib, Refit
│   ├── TdfSeqConConsumer.cs               # BaseConsumer<TdfTransactionCommand>
│   ├── Api/
│   │   ├── IHoa5BackendApi.cs             # Refit interface
│   │   └── InscrireSuiviFichCorln*.cs
│   └── Messages/
│       ├── TdfTransactionCommand.cs
│       └── ConsumeResult.cs
│
├── RAMQ.Samples.Queue.TDF.SeqCon.StateFul/
│   ├── *.csproj                           # net8.0, Functions.Worker, DurableTask.Worker
│   ├── Program.cs
│   ├── host.json
│   └── Orchestrators/
│       ├── TdfTransactionOrchestrator.cs
│       └── Events/
│           ├── EnvoyerLotFichierEvent.cs
│           ├── CorrellerEnvoyerEvent.cs
│           └── TransactionCorrelationResult.cs
│
├── RAMQ.Samples.Queue.HOA5.Consumer/
│   ├── *.csproj                           # net8.0, Functions.Worker, Refit
│   ├── Program.cs                         # AddConsumer<TdfTransactionCommand, Hoa5Consumer>
│   ├── host.json                          # autoComplete: false
│   ├── appsettings.json
│   ├── Functions/
│   │   └── Hoa5SubscriberFunction.cs      # ServiceBusTrigger
│   ├── Hoa5Consumer.cs                    # BaseConsumer<TdfTransactionCommand>
│   └── Api/
│       ├── IHoa5BackendClient.cs          # Refit interface
│       └── TraiterTransmissionRequest.cs
│
└── RAMQ.Samples.Queue.HOA5.Backend/
    ├── *.csproj                           # net8.0, Functions.Worker, Storage.Blobs
    ├── Program.cs
    ├── host.json
    ├── appsettings.json
    └── Functions/
        └── TraiterTransmissionFunction.cs # HttpTrigger POST /hoa5/traiter-transmission
```

---

## 7. Critères de succès

| # | Critère | Indicateur observable |
|---|---------|----------------------|
| 1 | Worker publie Étape 2 | Log Worker : ZIP uploadé + `PublishAsync` OK. Message visible dans `tdf-queue` (sessionId correct). |
| 2 | Worker publie Étape 3 | Log Worker : `PublishAsync` OK pour `tdf.correller` avec **même** `SessionId`. |
| 3 | Subscriber route Étape 2 | Log Subscriber : `HandleEnvoyerLotFichierAsync` invoqué. |
| 4 | Consumer V+E Étape 2 | Log Consumer : validation OK, aucun appel API. |
| 5 | Orchestration Durable démarrée | Instance visible, état `FileSent`, `InstanceId` = `SessionId`. |
| 6 | Subscriber route Étape 3 | Log Subscriber : événement `CorrellerEnvoyer` envoyé à l'orchestration. |
| 7 | Orchestration Completed | État `Completed`. `TransactionCorrelationResult` contient `FileTokens` de l'Étape 2. |
| 8 | FileTokens fusionnés | `context.Tokens` enrichi des `FileTokens` Étape 2 avant appel Consumer Étape 3. |
| 9 | Consumer VTE Étape 3 | Log Consumer : `InscrireSuiviFichCorln` appelé, `IndExecOrcSpec = "O"` reçu. |
| 10 | Publication vers TDF.Topic | Log Subscriber : `PublishAsync` avec `Consumer='All'`, `Action=HOA5_ServTransmPrel_bt`. |
| 11 | HOA5 Consumer invoqué | Log HOA5 Subscriber : Consumer VTE exécuté sur `hoa5-queue`. |
| 12 | HOA5 Backend résout le blob | Log Backend : blob téléchargé, taille et contentType journalisés. |
| 13 | Erreur — token invalide | Étape 3 avec `AuthToken` différent → orchestration `Failed_Token` → DLQ. |
| 14 | Erreur — timeout (30 s) | Pas d'Étape 3 dans le délai → orchestration `Failed_Timeout` → DLQ. |
| 15 | Complétion différée | Erreur avant `CompleteMessageAsync` → message redistribué par Service Bus → rejoué sans perte. |

---

## 8. Dépendances NuGet

| Package | Projet(s) | Usage |
|---------|-----------|-------|
| `RAMQ.COM.EnterpriseMessageTransit` | Worker, Subscriber, Consumer, HOA5.Consumer | Producer, BaseConsumer |
| `Microsoft.Azure.Functions.Worker` | Tous les projets Function | Runtime isolated |
| `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` | Subscriber, HOA5.Consumer | ServiceBusTrigger |
| `Microsoft.DurableTask.Client` | Subscriber | schedule / raise / wait |
| `Microsoft.DurableTask.Worker` | StateFul | Orchestrator |
| `Azure.Storage.Blobs` | Worker, HOA5.Backend | Upload / résolution Claim Check |
| `Refit.HttpClientFactory` | Consumer, HOA5.Consumer | Appels HTTP typés |
| `Microsoft.Extensions.Http.Resilience` | Consumer, HOA5.Consumer | Retry policy HTTP |

---

## 9. Décisions de conception

| ID | Décision | Justification |
|----|----------|---------------|
| D-001 | `Variables["step"]` discrimine les étapes (pas `CurrentStage`) | `CurrentStage` est lié au mécanisme Saga EMT — non applicable au Sequential Convoy |
| D-002 | `SessionId` = `InstanceId` Durable | Corrélation 1:1 session Service Bus / instance orchestration, idempotence naturelle |
| D-003 | 4 méthodes distinctes dans le Worker | Responsabilités isolées : transaction, message, pièce jointe, publication — testabilité maximale |
| D-004 | `CompleteMessageAsync` toujours en dernier | Garantie at-least-once : tout échec avant complétion → redistribution Service Bus |
| D-005 | Consumer sans `IMessageProducer` | Patron Subscriber-orchestre : seul le Subscriber évalue `IndExecOrcSpec` et publie vers `tdf-topic` |
| D-006 | `Token.Reference` = chemin relatif uniquement | Sécurité : ne jamais exposer le nom de compte de stockage ni de SAS token dans les messages |
| D-007 | `AutoCompleteMessages = false` + `autoComplete: false` dans `host.json` | Obligation EMT — double garde contre la complétion automatique |
| D-008 | `ctx.CreateTimer` (Durable) — jamais `Task.Delay` | Persisté et rejouable ; `Task.Delay` est détruit au redémarrage du Function Host |
| D-009 | `Idempotency-Key = MessageId` sur tous les appels API | Protection contre les replays Service Bus — les APIs doivent ignorer les doublons |

---

## 10. Références

| Document | Lien | Section pertinente |
|----------|------|-------------------|
| Architecture cible HOA5/TDF | [`target-state-hoa5-fr.md`](../target-state-hoa5-fr.md) | Section 1 (contexte), Section 3 (topologie Service Bus), Section 6 (orchestration Durable) |
| Exemples EMT (référence implémentation) | [`Exemples/`](../Exemples/) | — |
| Changelog EMT | [`EnterpriseMessageTransit/CHANGELOG.md`](../EnterpriseMessageTransit/CHANGELOG.md) | — |
