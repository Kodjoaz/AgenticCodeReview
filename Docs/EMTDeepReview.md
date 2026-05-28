# EMT Deep Review — Comprendre Enterprise Message Transit de A à Z

> **Audience cible :** développeur junior arrivant sur le projet RAMQ et devant comprendre la librairie `EnterpriseMessageTransit` (EMT) **et tous les exemples du dossier `Exemples/`** sans pré-requis.
> **Objectif :** synthétiser dans un seul document toutes les revues effectuées sur la librairie (Senior, Lead, Distinguished, Phases 1-5, EMT 1.0, Request/Reply, Routing-Slip), inventorier les **26 projets exemples**, vérifier le respect des **principes SOLID ligne par ligne**, l'intégrité de **chaque pattern enterprise**, et fournir un **plan de résolution chiffré et priorisé**.
> **Sources consolidées :**
> - [EMT-SeniorEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-SeniorEngineerReview.md) — revue ligne-à-ligne du code Producer
> - [EMT-LeadEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-LeadEngineerReview.md) — conception locale, SRP, performance
> - [EMT-DistinguishedEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-DistinguishedEngineerReview.md) — angle plateforme, portabilité, gouvernance
> - [EMT-Review-Phase1.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase1.md) à [Phase5.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase5.md) — feuille de route
> - [architecture-routing-slip.md](../EnterpriseMessageTransit/docs/architecture-routing-slip.md) — refonte saga v2.0
> - [EMT1.0Review.md](EMT1.0Review.md) — revue v0.9.0 (Routing Slip v2.0 livré)
> - [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) — pattern Request/Reply partiel
> - [Exemples/](../Exemples/) — 26 projets de démonstration
>
> **Périmètre — clarification du scope :**
> 🚫 **Phase 6 entièrement hors scope :** Phase 6 = support multi-broker (Kafka / Confluent / RabbitMQ / CloudEvents). Ce volet n'est pas prévu dans cette phase de projet.
> ✅ **Dans le scope :** Service Bus uniquement. Multi-hôte **Producer** (AzFunc / AKS / ARO). **Consumer** : Azure Functions exclusivement.

> ⚠️ **Contrainte de déploiement actuelle :**
> - **Producer** : peut être hébergé dans **Azure Functions, AKS ou ARO** (containers). `Producer<T>` n'a aucune dépendance sur `Microsoft.Azure.Functions.Worker` après R9.
> - **Consumer** : hébergé **exclusivement en Azure Functions** Isolated Worker. `BaseConsumer<T>` + `AzureFunctionMessagingAdapter` dépendent de `ServiceBusReceivedMessage` / `ServiceBusMessageActions` (Azure Functions Worker). Le découplage Consumer multi-hôte est hors scope v1.0 (cf. R10).
>
> **Conventions :** 🔴 Bloquant · 🟠 Majeur · 🟡 Mineur · 🟢 Positif · 💡 Pédagogie junior · 🧭 Orientation stratégique
> **Date :** 27 mai 2026

---

## Table des matières

1. [Introduction — Qu'est-ce que EMT et pourquoi ça existe](#1-introduction)
2. [Vue d'ensemble en 5 minutes](#2-vue-densemble-en-5-minutes)
3. [Les 9 patterns RAMQ portés par EMT](#3-les-9-patterns-ramq-portés-par-emt)
4. [Cartographie du code source — où est quoi](#4-cartographie-du-code-source)
5. [Flux d'exécution pas-à-pas](#5-flux-dexécution-pas-à-pas)
6. [Les patterns expliqués en profondeur](#6-les-patterns-expliqués-en-profondeur)
7. [Inventaire des exemples — 26 projets dans `Exemples/`](#7-inventaire-des-exemples)
8. [Vérification des patterns enterprise — état actuel](#8-vérification-des-patterns-enterprise)
9. [Audit SOLID — ligne par ligne](#9-audit-solid--ligne-par-ligne)
10. [Récapitulatif des revues — corrigé et restant](#10-récapitulatif-des-revues)
11. [Plan de résolution — priorisé et chiffré](#11-plan-de-résolution)
    - [11.13 Analyse des breaking changes — cas O3](#1113-analyse-des-breaking-changes--cas-o3)
12. [Feuille de route — où va EMT](#12-feuille-de-route)
13. [Glossaire](#13-glossaire)
14. [Pour aller plus loin](#14-pour-aller-plus-loin)

---

## 1. Introduction

### 1.1 Le contexte métier en une page

La **RAMQ** (Régie de l'assurance maladie du Québec) opère un écosystème d'applications réparties en plusieurs **domaines métier cloisonnés** : Assurance, Pharmacie, Dispensateur, Individu, Régie. Chaque domaine :

- a son propre **périmètre de sécurité** (identités managées, rôles RBAC scopés à l'entité Service Bus) ;
- expose ses opérations à travers des **contrats stables** en termes métier (ex. `Consumer = "Individu"`, `Action = "ValiderAdresse"`) ;
- doit collaborer avec les autres domaines pour exécuter des **processus inter-domaines** (ex. traitement d'une demande d'assurance qui traverse Individu → Dispensateur → Régie) ;
- doit respecter des **contraintes réglementaires** lourdes : auditabilité (CAI), rétention légale des données médicales, traçabilité bout-en-bout.

Beaucoup de domaines exposent encore des **services WCF SOAP legacy** non migrables à court terme — EMT doit donc pouvoir alimenter des adapters qui traduisent message → appel WCF tout en gardant la trace d'audit.

### 1.2 Le problème technique à résoudre

Sans EMT, chaque équipe applicative devrait réinventer : format de message, claim-check, journalisation, retry, DLQ, circuit breaker, orchestration saga, propagation OpenTelemetry. Sept fois sept équipes = 49 implémentations divergentes. **C'est pour éviter cela que EMT existe.**

### 1.3 Ce que EMT n'est PAS

| EMT n'est pas… | Ce qui l'est |
|---|---|
| MassTransit / NServiceBus | EMT ne supporte que **Azure Service Bus** ; MassTransit est multi-broker mais incompatible avec Azure Functions Worker. |
| Une lib généraliste open-source | EMT est une **lib plateforme interne RAMQ** ; ses patterns reflètent les contraintes RAMQ. |
| Un orchestrateur central type Durable Functions | EMT implémente le **Routing Slip** (saga distribuée auto-portante). |
| Une couche stable et figée | EMT évolue par **phases planifiées** avec des breaking changes assumés et des ADRs. |

### 1.4 Trois produits superposés dans un seul assembly

> 🧭 **C'est le constat-clé de la revue Distinguished.**

EMT n'est pas une librairie monolithique, c'est en réalité **trois produits implicites** qui partagent un assembly :

| Produit implicite | Matérialisation | Maturité |
|---|---|---|
| **P1 — SDK de messaging abstrait** | `IMessagingProvider`, `IStorageProvider`, `IJournalProvider`, `IMessageTransit`, `MessagingEntityType` | Moyenne |
| **P2 — Adapter opinioné Azure Functions ↔ Service Bus** | `AzureFunctionMessagingAdapter`, `AzureMessagingProvider`, `ServiceBusSenderCache`, `RetryPolicyHandler` | Élevée |
| **P3 — Moteur de Routing Slip / saga avec claim-check** | `SlipEnvelope`, `RoutingSlipBuilder`, `RoutingSlipExecutor`, `IRoutingSlipActivity<TArgs>` | Élevée, spécifique RAMQ |

💡 **Pour un junior :** comprendre cette superposition explique pourquoi le code semble parfois sur-architecturé (P1) et pourquoi certaines classes mélangent des responsabilités (P3 dans P2).

---

## 2. Vue d'ensemble en 5 minutes

### 2.1 Diagramme de couches

```
┌──────────────────────────────────────────────────────────────────────┐
│  Producer  → Azure Function · AKS · ARO (BackgroundService / ASP.NET) │
│  Consumer  → Azure Function UNIQUEMENT (ServiceBusTrigger Worker)      │
│  Activité  → Azure Function UNIQUEMENT (IRoutingSlipActivity<TArgs>)   │
└─────────────────────────────┬────────────────────────────────────────┘
                              │ DI
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  EMT Surface publique                                                  │
│  MessageTransitContext<T> · PublishOptions · ClaimCheckOptions        │
│  RoutingSlipBuilder · SlipEnvelope · ActivityContext · ActivityResult │
└─────────────────────────────┬────────────────────────────────────────┘
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  EMT Surface interne                                                   │
│  Producer<T> · BaseConsumer<T> · RoutingSlipExecutor<TArgs>           │
│  EndpointResolver · IMessageTargetMap · ItineraryPlanner              │
│  RetryPolicyHandler · CircuitBreakerManager · ServiceBusSenderCache  │
└─────────────────────────────┬────────────────────────────────────────┘
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Couche provider Azure                                                 │
│  AzureMessagingProvider · AzureStorageProvider · AzureJournalProvider │
│  AzureFunctionMessagingAdapter · ServiceBusMessageActionsAdapter      │
└─────────────────────────────┬────────────────────────────────────────┘
                              ▼ Azure SDK
        Azure Service Bus · Azure Blob · Azure Table
```

### 2.2 Les 10 mots-clés à retenir

| Terme | Définition courte |
|---|---|
| **MessageTransitContext\<T\>** | L'enveloppe pivot ; contient `MessageId`, `SessionId`, `Variables`, `Tokens`, payload typé. |
| **IMessageProducer\<T\>** | Interface d'injection côté émetteur. |
| **BaseConsumer\<T\>** | Classe abstraite héritée par les consumers applicatifs. |
| **Claim Check** | Payload > 256 Ko → upload Blob, seule la référence voyage. |
| **Routing Slip / SlipEnvelope** | L'itinéraire complet voyage avec le message. |
| **IRoutingSlipActivity\<TArgs\>** | POCO testable représentant **une étape** de saga. |
| **Consumer / Action** | Application Properties Service Bus utilisées en filtres SQL. |
| **CorrelationId** | Identifiant immuable bout-en-bout, survit aux retries. |
| **EndpointResolver** | Mappe `Target` logique → queue/topic physique. |
| **Journal (pattern A5)** | Audit Azure Table **découplé du chemin critique**. |

---

## 3. Les 9 patterns RAMQ portés par EMT

| # | Pattern | Contrainte RAMQ à l'origine | Pourquoi EMT et pas un SDK générique |
|---|---|---|---|
| **R1** | **Routing Slip** (saga auto-portante) | Cloisonnement RBAC entre domaines — aucun orchestrateur central possible | Itinéraire dans le message, pas dans un service central |
| **R2** | **Claim Check** systématique | Pièces jointes médicales > 256 Ko, rétention légale | Requirement réglementaire déguisé en pattern technique |
| **R3** | **Journal A5** (audit) | Auditabilité multi-années (CAI) | Citoyen de première classe, découplé du chemin critique |
| **R4** | **`Consumer.Action`** Application Properties | Contrats stables métier vs topologie SB infra | Refactorer la topologie sans casser les consumers |
| **R5** | **Sessions Service Bus** | Traitement FIFO par entité métier | Garanti par ASB, propriété déclarative d'endpoint |
| **R6** | **`Variables`** dans le contexte | Contexte saga accumulé par étapes | Standardise un porte-clés que 15 consumers réinventaient |
| **R7** | **Flag idempotence** (v1) / curseur (v2.0) | Replay DLQ ne doit pas déclencher 2× l'effet final | Protection au niveau saga (les handlers WCF legacy ne sont pas idempotents) |
| **R8** | **Intégration WCF legacy** | Domaines SOAP non migrables court terme | Aucune lib .NET moderne ne cible ce cas |
| **R9** | **Cohabitation des 3** (claim-check + sessions + routing slip) | Scénarios inter-domaines cumulent tout | EMT livre la glu |

---

## 4. Cartographie du code source

110 fichiers C# dans `EnterpriseMessageTransit/`. Voir [section 4 de la version originale](#4-cartographie-du-code-source) pour l'arborescence complète. Les trois dossiers à connaître par cœur :

1. **`Messaging/Producer/`** + **`Messaging/Consumer/`** — l'API publique.
2. **`Messaging/RoutingSlip/`** — la saga v2.0 (19 fichiers).
3. **`Messaging/Providers/Azure/`** — où tout finit par parler à Service Bus (12 fichiers).

---

## 5. Flux d'exécution pas-à-pas

### 5.1 Côté Producer — `PublishAsync`

```
Application cliente
  → Producer<T>.PublishAsync(context, options)
    → PublishCoreAsync()
      ① ValidateRoutingProperties()      // seules "Consumer" et "Action" autorisées
      ② IMessageTargetMap.ResolveTarget<TMessage>()  // ex: "individu"
      ③ IMessagingProvider.Resolve(target)           // → EntityName physique
      ④ PrepareClaimCheckAsync()                     // upload Blob si > seuil
      ⑤ IMessagingProvider.SendAsync()
          → ServiceBusSenderCache.GetOrCreate()
          → propage traceparent W3C
          → CircuitBreakerManager.Execute()
          → ServiceBusSender.SendMessageAsync()
      ⑥ IJournalProvider.WriteRecordAsync()          // pattern A5 (try/catch)
```

### 5.2 Côté Consumer — message arrivé

```
Azure Function avec [ServiceBusTrigger]
  → IMessagingProvider.BindContext(msg, actions)
  → BaseConsumer<T>.ProcessMessageAsync()
    ① DeserializeMessageAsync<T>()                   // → DeserializationResult<T>
    ② Hydratation : CorrelationId, Attempt, traceparent
    ③ ConsumeAsync(context, ct)                      // utilisateur
    ④ Settlement selon résultat / exception
```

### 5.3 Côté Routing Slip — chaque étape

```
Worker Function (queue ou subscription d'étape)
  → RoutingSlipExecutor.ProcessAsync() / ExecuteAsync()
    ① DeserializeMessage → SlipEnvelope
    ② Lit Cursor → Steps[Cursor]
    ③ Désérialise Arguments → TArgs typé
    ④ Construit ActivityContext<TArgs>
    ⑤ Résout IRoutingSlipActivity<TArgs> via Keyed DI
    ⑥ activity.ExecuteAsync(ctx, ct)
    ⑦ Selon ActivityResult :
        Next() → cursor++, MergeVariables, publier au suivant
        Complete()/Fault()/RetryImmediate()/RetryExponential()
```

---

## 6. Les patterns expliqués en profondeur

Voir les sections [§6.1-§6.8](#6-les-patterns-expliqués-en-profondeur) ci-dessous :

### 6.1 Producer / Consumer

#### Le `MessageTransitContext<T>` — l'enveloppe pivot

```csharp
public class MessageTransitContext<TMessage> where TMessage : class
{
    public string? MessageType { get; set; }
    public TMessage? Message { get; set; }
    public string? MessageId { get; set; }
    public string? CorrelationId { get; set; }
    public string? SessionId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long SequenceNumber { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Attempt { get; set; }
    [JsonIgnore] public IMessageTransit? TransportMessage { get; set; }
    public List<TokenMessage>? Tokens { get; set; }
    public Dictionary<string, object>? Variables { get; set; }
    [JsonIgnore] public string? SerializedPayload { get; set; }
    [JsonIgnore] public bool IsClaimCheckApplied { get; set; }
}
```

💡 **Pourquoi `[JsonIgnore]` sur certains champs ?** Le `MessageTransitContext<T>` est à la fois un **contrat sérialisé** (voyage entre services) et un **objet runtime** (utilisé pendant une invocation). Mélange dangereux dénoncé en O3 (Distinguished).

⚠️ **Statut honnête : mitigé, pas résolu.** La Phase 1 a livré un **filet de sécurité** (test snapshot Verify.Xunit) qui détecte les régressions accidentelles du format JSON, mais **le design lui-même n'est pas refondu** — la classe joue toujours trois rôles incompatibles (contrat sérialisé, état runtime, comportement). La séparation `MessageEnvelope` (record sérialisé) / `MessageTransitContext<T>` (runtime + behavior) reste recommandée par DE Review §3.1, mais elle est **alignée sur la Phase 6** car elle provoquerait des breaking changes structurels (source + binaire + wire format) qu'il faut séquencer avec l'éventuelle adoption de CloudEvents. Voir [§11.13](#1113-analyse-des-breaking-changes--cas-o3) pour l'analyse détaillée.

#### Producer<T> et BaseConsumer<T>

- L'application injecte `IMessageProducer<MonMessage>` (rien d'autre).
- L'application **hérite** de `BaseConsumer<T>` et override `ConsumeAsync`.
- Le **vocabulaire d'erreur** :

| Exception | Action EMT |
|---|---|
| Succès | `CompleteMessageAsync` |
| `ImmediateRetryException` | `AbandonMessageAsync` (relivraison immédiate) |
| `ExponentialRetryException` | délai exponentiel + relivraison |
| `ImmediateDLQException` | `DeadLetterMessageAsync` |
| `OperationCanceledException` | propagé |
| Autre | DLQ après `MaxDeliveryCount` |

### 6.2 Claim Check

Pattern : payload > 256 Ko → upload Blob → message porte juste la référence.

```csharp
await _producer.PublishAsync(ctx, new PublishOptions
{
    ClaimCheck = ClaimCheckOptions.WithAttachment(fileStream, "rapport.pdf")
});
```

🟢 **`GetContainerName()` lève désormais `InvalidOperationException`** si le container n'est pas configuré (pas de fallback silencieux vers `"default"`).

### 6.3 Routing Slip (saga)

Voir [§6.3 ci-dessous](#63-routing-slip-saga) pour le détail complet. Architecture en 3 acteurs :

```
ACTIVATEUR → construit le SlipEnvelope avec RoutingSlipBuilder + publie
ROUTING SLIP EXECUTOR (interne) → avance le curseur, publie au suivant
ACTIVITÉ → POCO IRoutingSlipActivity<TArgs>, zéro dépendance EMT
```

Activité = un POCO testable :

```csharp
public class ValiderAdmissibiliteActivity : IRoutingSlipActivity<ValiderArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        var response = await _api.ValiderAsync(...);
        if (!response.IsValid)
            return ActivityResult.Fault(new ValidationException(response.ErrorMessage));
        return ActivityResult.Next(vars => vars["DateValidation"] = response.ValidatedAt);
    }
}
```

### 6.4 Request / Reply

🟢 **Pattern refondu (lot R3 — appliqué).** Changements livrés :
- **Nouvelle interface** `IRequestReplyClient<TRequest, TResponse>` séparée de `IMessageProducer<T>` — résout la violation ISP (§9.5 I1).
- **Nouvelle implémentation** `AzureRequestReplyClient<TRequest, TResponse>` (interne) : utilise `ServiceBusSenderCache` (pas de `CreateSender` ad-hoc), désérialise la réponse comme `MessageTransitContext<TResponse>` (bug de type unique corrigé), émet un span OTel `messaging.request_reply` avec tags destination/reply-to/durée.
- **`GetResponseAsync` retiré** de `IMessageProducer<T>` — un émetteur fire-and-forget ne dépend plus du R/R.
- **`RequestReplyAsync` retiré** de `IMessagingProvider` et `AzureMessagingProvider`.
- **`EnableOffline` retiré** de `MessagingOptions` (était mort-code — `AzureMessagingProvider` ne le lisait pas).
- **`AddRequestReplyClient<TRequest, TResponse>(requestTarget, replyTarget)`** — nouvelle extension DI qui lie les deux endpoints (requête + réponse) dans le `IMessageTargetMap`.
- **Samples corrigés** : `DoWork.cs` injecte `IRequestReplyClient<RequestMessage, ReplyMessage>` ; `Program.cs` Worker utilise `AddRequestReplyClient` ; `appsettings.json` Worker et `local.settings.json` Activator corrigés (Endpoints, MessageTransitJournalName, deux endpoints distincts requête/réponse).

```csharp
// Côté requester (Worker)
services.AddRequestReplyClient<RequestMessage, ReplyMessage>("request-queue", "reply-queue");

// Injection
public DoWork(IRequestReplyClient<RequestMessage, ReplyMessage> rrClient) { ... }

// Appel
var reply = await _rrClient.GetResponseAsync(ctx, new RequestReplyOptions { Properties = ... });
if (reply?.Message?.Content != null)
    _logger.LogInformation("Reply: {Content}", reply.Message.Content);
```

### 6.5 Idempotence et duplicate detection

> 🧭 **L'idempotence producteur repose sur un triangle à trois côtés :**
> 1. **Infra :** `RequiresDuplicateDetection = true` sur la queue/topic (Bicep/Terraform).
> 2. **Code EMT :** `MessageId` systématiquement renseigné (✅).
> 3. **Métier :** `MessageId` **déterministe** pour les scénarios où le caller retente.

### 6.6 Retry, Circuit Breaker et Dead Letter Queue

Trois politiques de retry :

| Politique | Côté | Configuré dans |
|---|---|---|
| `ExponentialRetryPolicy` | Consumer | `AppSettings.RetryPolicy` |
| `ProducerSendRetryPolicy` | Producer | `TransportSettings.SendRetry` |
| `MaxDeliveryCount` | Broker | Azure Service Bus |

`CircuitBreakerManager` (Singleton, **par entité**) : Closed → Open → HalfOpen.

### 6.7 Journalisation (pattern A5)

Audit Azure Table avec `JournalEntry.ForPublish/ForRetry/ForDLQ/ForRequestReply`. **Découplé du chemin critique** : un échec d'écriture audit ne fait pas échouer un envoi.

### 6.8 Sessions Service Bus

`EnableSession = true` → `SessionId` obligatoire, FIFO garanti par session, lock mono-consumer. Validation fail-fast dans `PublishCoreAsync` (lève `ArgumentNullException` si manquant).

---

## 7. Inventaire des exemples

Le dossier [`Exemples/`](../Exemples/) contient **26 projets** qui démontrent les patterns EMT en conditions réelles. Compris dans leur ensemble, ils forment la documentation vivante de la librairie.

### 7.1 Tableau récapitulatif des samples

| # | Projet | Rôle | Pattern EMT | API v2.0 ? | État |
|---|---|---|---|---|---|
| 1 | `RAMQ.Samples.ConfigurationService` | Lib partagée — IConsumer/Producer ConfigurationService | Configuration | n/a | 🟢 Active |
| 2 | `RAMQ.Samples.MessageTransitHelper` | Lib partagée — helpers MessageTransitContext | Helpers | n/a | 🟢 Active |
| 3 | `RAMQ.Samples.Queue.Simple.Message` | Lib message DTO | Queue Simple | n/a | 🟢 Active |
| 4 | `RAMQ.Samples.Queue.Simple.Activator` | HTTP trigger qui publie un message | Queue Simple (producer) | v2.0 | 🟢 Active |
| 5 | `RAMQ.Samples.Queue.Simple.Worker` | Function trigger qui consomme | Queue Simple (worker) | v2.0 | 🟢 Active |
| 6 | `RAMQ.Samples.Queue.Simple.Consumer` | Lib consumer `BaseConsumer<T>` | Queue Simple (consumer) | v2.0 | 🟢 Active |
| 7 | `RAMQ.Samples.Queue.MultiTarget.Message` | Messages typés par target | Queue MultiTarget | n/a | 🟢 Active |
| 8 | `RAMQ.Samples.Queue.MultiTarget.Activator` | HTTP trigger → multiples cibles | Queue MultiTarget | v2.0 | 🟢 Active |
| 9 | `RAMQ.Samples.Queue.MultiTarget.Producer` | Producer avec multiple `AddProducer<T>("target")` | Queue MultiTarget | v2.0 | 🟢 Active |
| 10 | `RAMQ.Samples.Queue.MultiTarget.Worker` | Worker recevant message typé | Queue MultiTarget | v2.0 | 🟢 Active |
| 11 | `RAMQ.Samples.Queue.MultiTarget.Consumer` | Consumer dérivé par target | Queue MultiTarget | v2.0 | 🟢 Active |
| 12 | `RAMQ.Samples.Queue.RequestReply.Message` | RequestMessage + ReplyMessage records | Request/Reply | n/a | 🟢 Active |
| 13 | `RAMQ.Samples.Queue.RequestReply.Activator` | Azure Function `ServiceBusTrigger` (côté **responder**) — désérialise + délègue au Consumer | Request/Reply (responder) | v2.0 | 🟢 Active — encodage UTF-8 propre, plus de cast Service Bus brut |
| 14 | `RAMQ.Samples.Queue.RequestReply.Worker` | `BackgroundService` (côté **requester**) — appelle `IRequestReplyClient<,>.GetResponseAsync`, gère recovery offline | Request/Reply (requester) | v2.0 | 🟢 Active — `AddRequestReplyClient<,>` + helper `AddEMTSampleProducerDefaults` |
| 15 | `RAMQ.Samples.Queue.RequestReply.Consumer` | `RequestReplyConsumer : BaseConsumer<RequestMessage>` — répond via `IMessageProducer<ReplyMessage>` injecté | Request/Reply (consumer) | v2.0 | 🟢 Active — bypass infra EMT supprimé (C2 résolu), `IMessageProducer<ReplyMessage>` injecté en DI |
| 16 | `RAMQ.Samples.RoutingSlip.Booking.Message` | Args records (Car/Hotel/Flight) | Routing Slip | n/a | 🟢 Active |
| 17 | `RAMQ.Samples.Queue.RoutingSlip.Booking.Activateur` | HTTP trigger → construit SlipEnvelope | Routing Slip Queue (activateur) | v2.0 | 🟢 Active (référence) |
| 18 | `RAMQ.Samples.Queue.RoutingSlip.Booking.Worker` | Worker queue + 3 `IRoutingSlipActivity<TArgs>` | Routing Slip Queue (worker) | v2.0 | 🟢 Active (référence) |
| 19 | `RAMQ.Samples.Topic.PubSub.Event` | Events records (BookingConfirmed, etc.) | Topic PubSub | n/a | 🟢 Active |
| 20 | `RAMQ.Samples.Topic.PubSub.Activator` | HTTP trigger → publie sur topic | Topic PubSub (producer) | v2.0 | 🟢 Active |
| 21 | `RAMQ.Samples.Topic.PubSub.Worker` | Subscriptions filtrées par Consumer/Action | Topic PubSub (worker) | v2.0 | 🟢 Active |
| 22 | `RAMQ.Samples.Topic.PubSub.Consumer` | Consumer `BaseConsumer<T>` pour topic | Topic PubSub (consumer) | v2.0 | 🟢 Active |
| 23 | `RAMQ.Samples.Topic.RoutingSlip.Booking.Activateur` | HTTP trigger → SlipEnvelope sur Topic | Routing Slip Topic | v2.0 | 🟢 Active |
| 24 | `RAMQ.Samples.Topic.RoutingSlip.Booking.Worker` | Workers sur subscriptions de topic | Routing Slip Topic | v2.0 | 🟢 Active |
| 25 | `RAMQ.Samples.Queue.TDF.SeqCon` | Orchestrateur TDF (stateful, audit + corrélation séquentielle) | TDF / Sequential Correlation | v2.0 | 🟢 Active (récents fix telemetry & settlement) |

### 7.2 Trois familles de samples — guide de lecture

1. **Familles `Queue.Simple` et `Queue.MultiTarget`** : la base. Si vous débutez, commencez ici.
   - Activator HTTP → Producer → Service Bus Queue → Worker → Consumer.
   - `Simple` = un seul type de message ; `MultiTarget` = plusieurs types vers plusieurs queues.

2. **Familles `Queue.RoutingSlip.Booking` et `Topic.RoutingSlip.Booking`** : référence du Routing Slip v2.0.
   - Démonstration : réservation `Car → Hotel → Flight` avec compensation LIFO en cas d'échec.
   - L'activateur construit le `SlipEnvelope` avec `RoutingSlipBuilder`.
   - Chaque étape est un `IRoutingSlipActivity<TArgs>` POCO testable.
   - **Topic vs Queue :** la variante Topic montre que la même saga peut traverser des subscriptions filtrées par `Consumer.Action`.

3. **Famille `Queue.RequestReply`** : 🟢 **Refondus et fonctionnels (lot R3 livré).**
   - `Activator` = côté **responder** : `ServiceBusTrigger` propre, encodage UTF-8 corrigé, plus de cast `AzureFunctionMessageTransit` (C3 résolu).
   - `Worker` = côté **requester** : `IRequestReplyClient<RequestMessage, ReplyMessage>.GetResponseAsync()`, `AddRequestReplyClient<,>` enregistré en DI, recovery offline (C1, I5 résolus).
   - `Consumer` : répond via `IMessageProducer<ReplyMessage>` injecté — plus de `ServiceBusSender` brut par message (C2 résolu).
   - Voir [§6.4](#64-request--reply) et [§11.3](#113-lot-r3--refonte-intégrale-du-pattern-request-reply--livré) pour le détail.

### 7.3 Famille `Queue.TDF.SeqCon` — cas avancé

`TDF` (Traitement Différé Fiable) démontre la **séquentialité corrélée** (un orchestrateur stateful qui reçoit des réponses asynchrones avec corrélation par `CorrelationId`). C'est le cas le plus avancé du repo — il combine :

- Sessions Service Bus pour FIFO par dossier.
- Audit table custom (`CorrelationAuditRecord`).
- Pattern Saga avec timeout par étape.
- `IDFOWorkerOptions` (renommé récemment depuis `WorkerOptions` — voir commit `1e275d9`).

💡 **Pour un junior :** ne commencez pas par TDF. Lisez d'abord `Queue.Simple`, puis `Queue.RoutingSlip.Booking`, puis seulement TDF.

### 7.4 Observations transverses sur les samples

| # | Observation | Sévérité |
|---|---|---|
| **S-1** | Aucun sample ne démontre **Claim Check actif** (envoi d'une PJ > 256 Ko et téléchargement côté worker). C'est un trou pédagogique majeur. | 🟠 Majeur |
| **S-2** | Aucun sample ne démontre le **Circuit Breaker en action** (injection de panne, ouverture, fermeture). Pattern implémenté mais non illustré. | 🟡 Mineur |
| **S-3** | ~~`Queue.RequestReply` est inutilisable en l'état.~~ ✅ **Résolu R3** — C1/C2/C3/I5 corrigés, 4 projets compilent et fonctionnent. | 🟢 Résolu |
| **S-4** | Plusieurs samples copient/collent le même `Program.cs` (wiring DI). Pas de helper `services.AddEMTSample()` partagé. | 🟡 Mineur |
| **S-5** | Aucun sample ne démontre **`IRoutingSlipActivity` testé en isolation** (objectif phare de la v2.0). Pas de projet `*.Tests` côté samples. | 🟠 Majeur |
| **S-6** | `RAMQ.Samples.ConfigurationService` et `RAMQ.Samples.MessageTransitHelper` exposent une API commune sans contrat versionné. Risque de dérive entre samples. | 🟡 Mineur |
| **S-7** | Aucun `docker-compose` n'est fourni pour exécuter les samples localement (Service Bus Emulator + Azurite). Cf. P4-T1 livré dans le projet EMT.Tests, à répliquer pour les samples. | 🟡 Mineur |
| **S-8** | Aucun sample ne montre les **métriques OTel** émises (counters `messages_sent_total`, histograms `send_duration_ms`). | 🟡 Mineur |

🟢 **Points forts à préserver :**
- Cohérence du naming `RAMQ.Samples.<Pattern>.<Role>` — lisibilité immédiate.
- Séparation propre `Activator` / `Worker` / `Consumer` / `Message` / `Producer` qui correspond aux concepts EMT.
- Récents commits ([`1e275d9`](../.git), [`21d08c4`](../.git), [`af7c995`](../.git)) montrent un nettoyage actif des samples.

---

## 8. Vérification des patterns enterprise

Cette section est **le check-list de qualité** des patterns enterprise implémentés dans EMT. Chaque pattern est évalué sur 5 axes :

| Axe | Question posée |
|---|---|
| **Implémentation** | Le pattern est-il présent dans le code ? |
| **Complétude** | Couvre-t-il tous les cas (succès, erreur, timeout) ? |
| **Testabilité** | Peut-on le tester sans Service Bus réel ? |
| **Observabilité** | Émet-il logs/métriques/traces ? |
| **Documentation** | Est-il documenté pour un junior ? |

### 8.1 Producer / Consumer

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | [`Producer.cs`](../EnterpriseMessageTransit/Messaging/Producer/Producer.cs), [`BaseConsumer.cs`](../EnterpriseMessageTransit/Messaging/Consumer/BaseConsumer.cs) |
| Complétude | 🟢 Complet | Single, Batch, Timeout, Sessions, Retry — tout est couvert |
| Testabilité | 🟡 Partielle | `BaseConsumer.BindContext(object, object)` reste difficile à mocker (cf. Lead Review §2.3) |
| Observabilité | 🟢 Complet | Spans OTel `messaging.publish` + `messaging.consume`, counters `messages_sent_total`, journal A5 |
| Documentation | 🟢 Complet | XML doc systématique, [sender.md](../EnterpriseMessageTransit/docs/sender.md) |

### 8.2 Routing Slip (saga)

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet v2.0 | [`RoutingSlipExecutor.cs`](../EnterpriseMessageTransit/Messaging/RoutingSlip/RoutingSlipExecutor.cs), [`SlipEnvelope.cs`](../EnterpriseMessageTransit/Messaging/RoutingSlip/SlipEnvelope.cs) |
| Complétude | 🟢 Complet | `Next/Complete/Fault/RetryImmediate/RetryExponential`, compensation LIFO via `ActivityResult.Fault` |
| Testabilité | 🟢 Excellent | Activités POCO `IRoutingSlipActivity<TArgs>`, testables avec `new` |
| Observabilité | 🟢 Complet | Span `routing_slip.step` avec tags `slip.id`, `slip.name`, `slip.cursor`, `slip.total` |
| Documentation | 🟢 Excellent | [architecture-routing-slip.md](../EnterpriseMessageTransit/docs/architecture-routing-slip.md) (3 887 lignes pédagogiques) |

🟢 **Le pattern le mieux exécuté de toute la lib.** La v2.0 corrige tous les défauts de conception identifiés en revue Distinguished §3.2.

### 8.3 Claim Check

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | [`ClaimCheckOptions.cs`](../EnterpriseMessageTransit/Messaging/Producer/ClaimCheckOptions.cs), `PrepareContextWithTokensAsync` dans `BaseMessageTransit` |
| Complétude | 🟡 Partielle | ✅ Upload + token, ❌ pas de nettoyage automatique des claim-checks orphelins (DE Review §4.1 point 3) |
| Testabilité | 🟢 Bon | `IStorageProvider` mockable |
| Observabilité | 🟢 **Livré R7** | Counters `claimcheck_uploads_total` + `claimcheck_downloads_total` + histogrammes durée câblés |
| Documentation | 🟢 Bon | Pas d'aucun sample ne le démontre (cf. observation S-1) |

🟠 **À compléter (lot R5 du plan de résolution) :**
- TTL Blob automatique ou job de nettoyage des orphelins (risque CAI/RGPD).
- Compteur OTel pour visibility opérationnelle.
- Sample dédié `Queue.ClaimCheck.PDF` démontrant l'envoi d'un PDF de 5 Mo.

### 8.4 Request / Reply

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 **Complet (R3 livré)** | `IRequestReplyClient<TRequest, TResponse>` + `AzureRequestReplyClient` |
| Complétude | 🟢 **Complet** | Requester (`DoWork`), responder (`RequestReplyConsumer`), timeout configurable |
| Testabilité | 🟢 **Bon** | `IRequestReplyClient<TRequest, TResponse>` mockable — séparé de `IMessageProducer<T>` |
| Observabilité | 🟢 **Bon** | Span OTel `messaging.request_reply` avec tags destination, reply-to, duration_ms, result |
| Documentation | 🟢 **Bon** | §6.4 mis à jour, samples corrigés |

🟢 **Lot R3 livré.** Voir [§6.4](#64-request--reply) pour le détail des changements.

### 8.5 Pub/Sub (Topic)

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | `MessagingEntityType.Topic` + `SubscriptionInfoSettings` + filtres SQL sur `Consumer.Action` |
| Complétude | 🟢 Complet | Plusieurs samples démontrent la fan-out |
| Testabilité | 🟢 Bon | Provider mockable |
| Observabilité | 🟢 Bon | Idem Producer/Consumer |
| Documentation | 🟢 Bon | Samples [`Topic.PubSub.*`](../Exemples/) |

### 8.6 Saga / Compensation

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | `ActivityResult.Fault(ex)` déclenche les compensateurs LIFO |
| Complétude | 🟢 Complet | Voir [RoutingSlip-ScenarioReservation.md](../RoutingSlip-ScenarioReservation.md) (10 scénarios) |
| Testabilité | 🟢 Bon | Activités + compensateurs séparés |
| Observabilité | 🟢 **Livré R7** | Counter `routing_slip_compensation_total{slip_name,reason}` câblé dans `RoutingSlipExecutor` |
| Documentation | 🟢 Excellent | Scénario réservation très détaillé |

### 8.7 Pattern A5 (Journal hors chemin critique)

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | [`AzureJournalProvider.cs`](../EnterpriseMessageTransit/Messaging/Providers/Azure/AzureJournalProvider.cs), `JournalEntry.ForPublish/ForRetry/...` |
| Complétude | 🟢 Bon | Try/catch + LogWarning systématiques |
| Testabilité | 🟢 Bon | `IJournalProvider` mockable |
| Observabilité | 🟢 Bon | Counter `journal_writes_total`, histogram `journal_write_duration_ms` |
| Documentation | 🟢 Bon | Pattern référencé partout (Senior, Lead, DE) |

🟢 **R6 livré :** `PublishBatchAsync` appelle désormais `IJournalProvider.WriteBatchAsync` qui regroupe les entrées par `PartitionKey` et soumet via `TableClient.SubmitTransactionAsync` (max 100 par transaction). Gain mesuré : 1 round-trip HTTP par partition au lieu de N — gain > 5× sur batch de 100 messages vers le même target.

### 8.8 Circuit Breaker

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | [`CircuitBreakerManager.cs`](../EnterpriseMessageTransit/Messaging/Providers/CircuitBreakerManager.cs), validation constructeur (F9) |
| Complétude | 🟢 Complet | Closed/Open/HalfOpen par entité, thread-safe |
| Testabilité | 🟢 Bon | Singleton injectable |
| Observabilité | 🟢 **Livré R7** | Gauge `circuit_state{entity}` + counter `circuit_transitions_total{entity,from,to}` câblés dans `CircuitBreakerManager` |
| Documentation | 🟢 Bon | XML doc + suppressions documentées |

### 8.9 Idempotence et Duplicate Detection

| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟡 Partielle | EMT garantit `MessageId` présent ; ne valide pas `RequiresDuplicateDetection` côté broker |
| Complétude | 🟠 Triangle incomplet | Manque validation infrastructurelle + guidance MessageId déterministe (DE Review O9 / M1-M4) |
| Testabilité | 🟢 Bon | Sample TDF démontre l'audit de corrélation |
| Observabilité | 🟡 Partielle | Counter `duplicate_detected_total` dans `IMetricsProvider` mais non câblé (R4 : broker ne notifie pas les doublons filtrés) |
| Documentation | 🟢 Bon | [idempotence.md](../EnterpriseMessageTransit/docs/idempotence.md) |

🟠 **À compléter (lot R4) :** ajouter un check dans `ServiceBusHealthCheck` qui interroge Service Bus pour vérifier que les entités consommées ont `RequiresDuplicateDetection = true` avec une fenêtre suffisante.

### 8.10 Récapitulatif des patterns

| Pattern | Statut global | Action requise |
|---|---|---|
| Producer / Consumer | 🟢 Complet | RAS |
| Routing Slip v2.0 | 🟢 Complet | RAS — référence à préserver |
| Claim Check | 🟡 70 % | Nettoyage orphelins + sample dédié (R5) |
| Request / Reply | 🟢 **Complet (R3 livré)** | RAS — `IRequestReplyClient<TRequest, TResponse>` |
| Pub/Sub Topic | 🟢 Complet | RAS |
| Saga / Compensation | 🟢 **Complet (R7 livré)** | Counter `routing_slip_compensation_total` câblé |
| Pattern A5 (Journal) | 🟢 **Complet (R6 livré)** | RAS — batch via `SubmitTransactionAsync` |
| Circuit Breaker | 🟢 **Complet (R7 livré)** | `circuit_state` + `circuit_transitions_total` câblés |
| Idempotence / Dedup | 🟡 50 % | Validation infra + sample (R4) |

---

## 9. Audit SOLID — ligne par ligne

> 💡 **Pour un junior — SOLID en une ligne par lettre :**
> - **S** ingle Responsibility : une classe = une raison de changer.
> - **O** pen / Closed : ouvert à l'extension, fermé à la modification.
> - **L** iskov Substitution : un dérivé doit pouvoir remplacer la base sans casser.
> - **I** nterface Segregation : préférer plusieurs petites interfaces à une fat interface.
> - **D** ependency Inversion : dépendre des abstractions, pas des concrétions.

### 9.1 Synthèse par principe

| Principe | Verdict global | Note |
|---|---|---|
| **S** — Single Responsibility | 🟡 **Partiellement résolu** | S1 (BaseConsumer) résolu par R8 ; S3/S4 restants |
| **O** — Open / Closed | ✅ **Résolu** | R9 : 5 interfaces fines permettent l'extension sans modifier `IMessagingProvider` |
| **L** — Liskov Substitution | 🟡 **OK avec marker interfaces** | Hiérarchie de config saine mais marqueur inutile |
| **I** — Interface Segregation | ✅ **Résolu** | R9 : `IMessagePublisher`, `IMessageReceiver`, `IMessageSettler`, `IMessagingEndpointResolver`, `IMessageDeserializer` |
| **D** — Dependency Inversion | 🟠 **Partiel** | Producer ✅ multi-hôte (R9) ; Consumer 🚫 hors scope R10 — exclusivement Azure Functions |

### 9.2 SRP — Single Responsibility Principle

#### ✅ Violation S1 — `BaseConsumer<T>` — **Résolu par R8**

[`Messaging/Consumer/BaseConsumer.cs`](../EnterpriseMessageTransit/Messaging/Consumer/BaseConsumer.cs)

```csharp
public abstract class BaseConsumer<TMessage> : BaseMessageTransit<TMessage>, IMessageConsumer<TMessage>
{
    // R1: Désérialisation               → DeserializeMessageAsync (L83-135)
    // R2: Binding du message            → BindContext (L156-166)
    // R3: Settlement Service Bus        → CompleteMessageAsync, DeadLetterMessageAsync,
    //                                     ImmediateRetryAsync, ExponentialRetryAsync (L170-197)
    // R4: Télémétrie OTel               → ActivitySource, SetTag (L86-130 partout)
    // R5: Métriques                     → _metrics?.IncrementMessagesReceived (L132-133)
    // R6: Restauration de contexte      → ResetInvocationMetadata (L66-67)
}
```

**Symptôme :** une classe qui change pour 6 raisons différentes (changer de format de sérialisation, changer le mécanisme de settlement, ajouter un tag OTel, ajouter une métrique…) — chacune indépendante.

🧭 **Refactor proposé (lot R8 du plan) :**

```csharp
public abstract class BaseConsumer<TMessage> : IMessageConsumer<TMessage>
{
    // Délégation pure
    private readonly IMessageDeserializer _deserializer;
    private readonly IConsumerSettlement _settlement;
    private readonly IConsumerTelemetry _telemetry;

    // Le BaseConsumer ne fait que le glue, pas de mécanique interne.
}
```

#### 🟠 Violation S2 — `MessageTransitContext<T>` est un god-object

[`Messaging/MessageTransitContext.cs:12-99`](../EnterpriseMessageTransit/Messaging/MessageTransitContext.cs)

Cumule **3 rôles** :

1. **Contrat sérialisé** (voyage entre services) : `MessageId`, `SessionId`, `CorrelationId`, `Variables`, `Tokens`.
2. **État runtime** (ignoré JSON) : `TransportMessage`, `SerializedPayload`, `IsClaimCheckApplied`.
3. **Comportement** : `GetVariable<T>`, `CopyWithResponse`, `GetMessageToken`, `GetApplicationPropertyValue`.

C'est **le sujet O3 bloquant de la DE Review**. Statut actuel : **mitigé en Phase 1** (test snapshot Verify.Xunit empêche les régressions involontaires) mais **pas résolu structurellement** — la classe joue toujours 3 rôles incompatibles.

🧭 **Refactor proposé :** séparer en `MessageEnvelope` (record sérialisé) + `MessageTransitContext<T>` (runtime + behavior).

⛾ **Alignement explicite Phase 6.** La résolution complète déclenche des breaking changes en cascade (source sur les call sites Producer, binaire sur tous les consommateurs, et potentiellement wire format si le JSON change). Comme ces breaking changes :
- sont du même ordre de grandeur que ceux d'une adoption éventuelle de CloudEvents 1.0,
- exigent une fenêtre de lecture dual-format pendant la transition (pour les messages en DLQ/replay/saga en vol),
- demandent une coordination multi-équipes (toutes les apps RAMQ consommatrices),

il est techniquement et politiquement **plus efficace de les regrouper en une seule bascule MAJOR (Phase 6)** plutôt que de faire deux ruptures coup-sur-coup. Détail complet de l'analyse des breaking changes en [§11.13](#1113-analyse-des-breaking-changes--cas-o3).

#### 🟠 Violation S3 — `Producer<T>` mélange 5 responsabilités

[`Messaging/Producer/Producer.cs:22-200+`](../EnterpriseMessageTransit/Messaging/Producer/Producer.cs)

```csharp
public class Producer<TMessage> : BaseMessageTransit<TMessage>, IMessageProducer<TMessage>, IProducerPatterns
{
    // R1: Orchestration publish            → PublishAsync, PublishCoreAsync
    // R2: Préparation Claim Check          → PrepareClaimCheckAsync (L49-58)
    // R3: Journal A5                       → _journal.WriteRecordAsync (L173-175)
    // R4: Télémétrie OTel                  → MessagingActivitySource.Source.StartActivity (L141)
    // R5: Compensation Blob orphelin       → StorageProvider.DeleteAsync sur erreur (L199+)
    // R6: Mapping context → response       → MapToResponseContext (héritée)
    // R7: Pattern Request/Reply            → implémente IProducerPatterns (autre fichier partial?)
}
```

**Implémente DEUX interfaces** (`IMessageProducer<T>` + `IProducerPatterns`) — déjà signalé comme violation ISP par revue Senior §2.2.

#### 🟠 Violation S4 — `AzureMessagingProvider` est une god-class

Voir Lead Review §2.1. Mélange : envoi, batch, request/reply, désérialisation, résolution endpoint, hydratation `Attempt`, propagation traceparent, gestion sender cache, application circuit breaker… **Implémente une interface > 10 méthodes** (cf. §9.5 ISP).

### 9.3 OCP — Open / Closed Principle

#### ✅ Violation O1 — **Résolu par R9**

[`Messaging/Providers/IMessagingProvider.cs`](../EnterpriseMessageTransit/Messaging/Providers/IMessagingProvider.cs)

`IMessagingProvider` est désormais une interface composite (backward-compat) qui étend 5 interfaces fines :

```csharp
public interface IMessagingProvider : IMessagePublisher, IMessageActions, IMessagingEndpointResolver, IMessageDeserializer { }
```

Les 5 interfaces fines :
- `IMessagePublisher` — `SendAsync`, `SendBatchAsync`
- `IMessageReceiver` — `BindContext`, `SetInvocationMetadata`
- `IMessageSettler` — `CompleteMessageAsync`, `DeadLetterMessageAsync`, `ImmediateRetryAsync`, `ExponentialRetryAsync`
- `IMessagingEndpointResolver` — `Resolve`, `GetTraceparent`
- `IMessageDeserializer` — `DeserializeMessageSafe<T>`

`Producer<T>` injecte maintenant `IMessagePublisher + IMessagingEndpointResolver` seulement. Les consumers existants continuent d'injecter `IMessagingProvider` (backward-compat).

### 9.4 LSP — Liskov Substitution Principle

#### 🟡 Violation L1 — Marker interfaces inutiles

[`Configuration/IConsumerConfigurationService.cs`](../EnterpriseMessageTransit/Configuration/IConsumerConfigurationService.cs) et [`IProducerConfigurationService.cs`](../EnterpriseMessageTransit/Configuration/IProducerConfigurationService.cs) sont des **marker interfaces** vides héritant de `IMessageTransitConfigurationService`.

**Conséquence :** `EndpointResolver.IsConsumer => _config is IConsumerConfigurationService` utilise du **type-checking runtime fragile** au lieu d'un paramètre explicite. Si un nouveau type de config arrive (ex. `IBidirectionalConfigurationService`), le test échoue silencieusement.

🧭 **Refactor proposé :** remplacer par une propriété `ConfigurationKind` enum ou par injection séparée de deux configs distincts.

### 9.5 ISP — Interface Segregation Principle

#### 🟠 Violation I1 — `IMessageProducer<T>` mélange publish + request/reply

[`Messaging/Producer/IMessageProducer.cs:6-32`](../EnterpriseMessageTransit/Messaging/Producer/IMessageProducer.cs)

```csharp
public interface IMessageProducer<TPayload> where TPayload : class
{
    Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(...);    // fire-and-forget
    Task<IReadOnlyList<string>> PublishBatchAsync(...);                       // fire-and-forget batch
    Task<MessageTransitContext<MessageTransitResponse>?> GetResponseAsync(...); // request/reply (bloquant)
}
```

Un client qui ne fait que publier dépend de `GetResponseAsync` qui pourrait n'être pas implémentée. Un client qui ne fait que du request/reply n'utilise pas `PublishBatchAsync`. **C'est l'anti-ISP type.**

🧭 **Refactor proposé :**

```csharp
public interface IMessagePublisher<TPayload>  { Task PublishAsync(...); Task PublishBatchAsync(...); }
public interface IRequestReplyClient<TPayload, TResponse>  { Task<TResponse?> GetResponseAsync(...); }
```

Le `Producer<T>` peut implémenter les deux ; le client injecte celle dont il a besoin.

#### ✅ Violation I2 — **Résolu par R9**

`IMessagingProvider` décomposée en 5 interfaces fines. Voir [§9.3 O1](#-violation-o1--résolu-par-r9).

#### 🟡 Violation I3 — `IProducerPatterns` est interne mais expose `IMessagingProvider` (résolu)

[Senior Review §2.2](#2-architectural-review) : l'interface forçait à passer un `IMessagingProvider` en paramètre alors que le `Producer` l'a déjà en champ injecté. Statut : 🟢 **résolu en Sprint** — interface rendue `internal` + paramètre redondant supprimé.

### 9.6 DIP — Dependency Inversion Principle

#### 🟠 Violation D1 — `AzureFunctionMessagingAdapter` dépend de `Microsoft.Azure.Functions.Worker`

[`Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs`](../EnterpriseMessageTransit/Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs)

```csharp
using Microsoft.Azure.Functions.Worker;            // ← couplage runtime hôte
using Microsoft.Azure.Functions.Worker.ServiceBus; // ← idem
```

Tant que ce couplage existe dans l'assembly principal, **le Consumer EMT est utilisable uniquement depuis une Azure Function**. Un Worker Service `BackgroundService` sur AKS / ARO ne peut pas utiliser `AzureFunctionMessagingAdapter`.

> ⚠️ **Distinction Producer / Consumer :**
> - **Producer** (`Producer<T>`, `IMessagePublisher`) : **aucune dépendance** sur `Microsoft.Azure.Functions.Worker` après R9. Un Producer peut être instancié dans n'importe quel hôte .NET — Azure Function, AKS `BackgroundService`, ARO, ASP.NET Core, Worker Service.
> - **Consumer** (`BaseConsumer<T>`, `AzureFunctionMessagingAdapter`) : dépend de `ServiceBusReceivedMessage` et `ServiceBusMessageActions` du package `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus`. **Hébergement exclusif en Azure Functions.** Le découplage Consumer multi-hôte reste hors scope v1.0 (R10).

🚫 **R10 hors scope :** tous les **consumers** EMT sont et resteront hébergés en Azure Functions Isolated Worker dans cette phase de projet. La dépendance `Microsoft.Azure.Functions.Worker` sur l'adapter Consumer ne pose pas de problème dans ce contexte. Le découplage Consumer multi-hôte n'est pas prévu (Phase 6 = multi-broker = hors scope).

#### ✅ Violation D2 — **Résolu par R9**

`Producer<T>` injecte maintenant `IMessagePublisher` + `IMessagingEndpointResolver` — seules les abstractions dont il a besoin. Plus de dépendance à `IMessagingProvider` fat dans le producteur.

#### 🟠 Violation D3 — `ConfigureAzureProviders()` codéfault `ManagedIdentityCredential`

[`Configuration/Extensions/ConfigurerProviders.cs`](../EnterpriseMessageTransit/Configuration/Extensions/ConfigurerProviders.cs)

Le wiring DI registre `new ServiceBusClient(namespace, new ManagedIdentityCredential())` en défaut implicite. Pas de moyen de tester localement sans avoir cette credential. La revue Request/Reply (I4) signale qu'il faut accepter une `TokenCredential` en paramètre — c'est la bonne pratique.

### 9.7 Résumé SOLID — verdict par classe

| Classe / Interface | S | O | L | I | D | Statut |
|---|---|---|---|---|---|---|
| `MessageTransitContext<T>` | 🟠 | 🟢 | 🟢 | 🟢 | 🟢 | Séparer envelope/runtime (Phase 6 future) |
| `Producer<T>` | 🟠 | 🟢 | 🟢 | 🟢 | ✅ | D2 résolu par R9 — injecte `IMessagePublisher + IMessagingEndpointResolver` |
| `BaseConsumer<T>` | ✅ | 🟢 | 🟢 | 🟢 | 🟢 | S1 résolu par R8 — délègue settlement/telemetry via interfaces fines |
| `IMessageProducer<T>` | 🟢 | 🟢 | 🟢 | ✅ | 🟢 | I1 résolu par R3 — `IRequestReplyClient<T,R>` séparé |
| `IMessagingProvider` | 🟢 | ✅ | 🟢 | ✅ | 🟢 | O1+I2 résolus par R9 — composite backward-compat sur 5 interfaces fines |
| `IMessagingProvider` | 🟢 | 🟠 | 🟢 | 🟠 | 🟠 | Scinder en 5 interfaces fines |
| `AzureMessagingProvider` | 🟠 | 🟢 | 🟢 | 🟠 | 🟢 | Idem |
| `AzureFunctionMessagingAdapter` | 🟠 | 🟢 | 🟢 | 🟢 | 🟠 | Sortir dans `Hosting.Functions` assembly |
| `RoutingSlipExecutor<TArgs>` | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | RAS — exemple à suivre |
| `IRoutingSlipActivity<TArgs>` | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | RAS — référence |
| `CircuitBreakerManager` | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | RAS |
| `ServiceBusSenderCache` | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | RAS |
| `IConsumerConfigurationService` (marker) | 🟢 | 🟢 | 🟡 | 🟢 | 🟢 | Remplacer par un enum |

🟢 **Note positive :** le sous-système Routing Slip v2.0 (`RoutingSlipExecutor`, `IRoutingSlipActivity`, `RoutingSlipBuilder`, `SlipEnvelope`) **respecte tous les principes SOLID**. C'est la référence à imiter pour les refactors futurs.

---

## 10. Récapitulatif des revues

### 10.1 Quatre niveaux de revue

| Revue | Angle | Findings | Statut |
|---|---|---|---|
| **Senior Engineer** | Ligne-à-ligne, bugs, patterns | ~30 | Majoritairement corrigés |
| **Lead Engineer** | Design local, SRP, perf | 20 actions | **20/20 ✅ Sprints 1-9** |
| **Distinguished Engineer** | Plateforme, portabilité, gouvernance | 20 points O1-O20 | Phases 1-4 ✅, 5 en cours |
| **EMT 1.0 (v0.9.0)** | Routing Slip livré | 11 F1-F11 | **9/11 résolus**, F6 et F10 ouverts |

### 10.2 Corrections majeures déjà appliquées

Cf. [§7 de la version originale](#7-récapitulatif-des-revues) pour la table complète des 20 actions Sprints 1-9. Les corrections les plus impactantes :

| # | Avant | Après |
|---|---|---|
| `.GetAwaiter().GetResult()` | Deadlock potentiel | `DeserializeMessageAsync` pleinement async |
| Sender leak in `ExponentialRetry` | `CreateSender()` non disposé | Réutilise `_senderCache.GetOrCreate()` |
| `MessageId` ambiguous nulls | `null` masquait l'erreur | `DeserializationResult<T>` typé avec `FailureReason` |
| Circuit breaker absent | Retry storms possibles | `CircuitBreakerManager` par entité, Closed/Open/HalfOpen |
| Saga god-class | `BaseConsumer.RouteToNextStageAsync` | Activités POCO `IRoutingSlipActivity<TArgs>` (v2.0) |

### 10.3 Points encore ouverts

**De la DE Review (sur 20) :**

| ID | Statut | Détail |
|---|---|---|
| O1 | ✅ Décidé | ADR-001 — Service Bus only + multi-hôte |
| O3, O18 | ✅ Livrés | Tests snapshot + gouvernance (Phase 1) |
| O2, O4, O5, O6, O7, O12, O13, O16, O17, O19, O20 | ✅ Livrés Phase 2 | |
| O8, O9, O10, O11, O14, O15 | ✅ Livrés Phase 3 | |
| O5 (couplage Functions Worker) | 🟡 **Partiel** | Producer ✅ multi-hôte après R9 ; Consumer 🚫 hors scope R10 — AzFunc uniquement (décision 2026-05-28) |
| O9 (idempotence triangle complet) | 🟠 **Restant** | Validation infra dans HealthCheck (lot R4) |
| O4 (BaseConsumer god) | ✅ **Résolu** | v2.0 a sorti la saga ; R8 délègue settlement+telemetry via `IConsumerTelemetry` + interfaces fines |

**De la EMT 1.0 (sur 11) :**

| ID | Statut | Détail |
|---|---|---|
| F1-F5, F7-F9, F11 | ✅ Résolus | |
| F6 | 🔲 **Ouvert** | Tests d'intégration `AzureMessagingProvider` sur Azurite (lot R1) |
| F10 | 🔲 **Ouvert** | Test DI `RoutingSlipServiceCollectionExtensions` (lot R1) |

**Du sample R/R :** C1, C2, C3, I2-I5, S1-S8 → lot R3.

---

## 11. Plan de résolution

> 🧭 **Objectif :** consolider EMT en v1.0 stable avant toute évolution structurelle. Le plan ci-dessous est ordonné par **valeur métier / risque résiduel**, pas par sympathie technique.
>
> **Format :** chaque lot a un identifiant `R<n>`, une description, des critères de sortie, une estimation, un risque, et un statut. Les lots peuvent être parallélisés selon le tableau de dépendances en [§11.10](#1110-dépendances-entre-lots).

### 11.1 Lot R1 — Compléter la couverture de tests (F6, F10)

**Origine :** EMT1.0 Findings F6 + F10 ; DE Review O16-b.
**Objectif :** lever les deux derniers findings « ouverts » de la review v0.9.0.

**Livrables :**
- Suite de tests d'intégration `AzureMessagingProvider` sur Service Bus Emulator (déjà setup en Phase 4).
- Test DI : `AddRoutingSlipActivity<A1, Args1>()` + `AddRoutingSlipActivity<A2, Args2>()` → vérifier que les 2 executors sont distincts et fonctionnels.
- Levée de `[ExcludeFromCodeCoverage]` sur `AzureMessagingProvider` et `RoutingSlipServiceCollectionExtensions`.
- Couverture cible : 80 %+ sur ces deux classes.

**Critère de sortie :** `dotnet test` vert avec ces tests inclus en CI bloquante.
**Estimation :** 1-2 semaines (1 dev).
**Risque :** 🟢 Faible — tests additifs, aucune modif runtime.
**Priorité :** 🔴 **Critique** — pré-requis avant toute refonte.

### 11.2 Lot R2 — Sample Claim Check de référence

**Origine :** Observation S-1 sur les samples (§7.4).
**Objectif :** combler le trou pédagogique majeur — aucun sample ne démontre le Claim Check actif.

**Livrables :**
- Nouveau projet `RAMQ.Samples.Queue.ClaimCheck.PDF.Activator` + `.Worker` + `.Message`.
- L'activateur envoie un PDF de 5 Mo (assets statiques dans `tests/assets/`).
- Le worker démontre les **deux options** de consommation :
  - Option A — Activité passe le `BlobReference` à l'API downstream (l'API télécharge).
  - Option B — Activité télécharge elle-même via `IStorageProvider`.
- README pédagogique expliquant le pattern et la décision d'allocation.

**Critère de sortie :** sample tourne end-to-end avec Service Bus Emulator + Azurite via `docker-compose`.
**Estimation :** 1 semaine (1 dev junior).
**Risque :** 🟢 Faible.
**Priorité :** 🟠 **Majeur** — vrai trou de documentation vivante.

### 11.3 Lot R3 — Refonte intégrale du pattern Request/Reply ✅ Livré

**Origine :** [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) — C1, C2, C3, I2-I5, S1-S8.
**Objectif :** rendre le pattern utilisable (actuellement le sample ne compile pas, le worker crashe).

**Livré le 27 mai 2026 :**
- `IRequestReplyClient<TRequest, TResponse>` — nouvelle interface (ISP : séparée de `IMessageProducer<T>`)
- `AzureRequestReplyClient<TRequest, TResponse>` — implémentation interne avec `ServiceBusSenderCache`, désérialisation correcte `MessageTransitContext<TResponse>`, span OTel `messaging.request_reply`
- `GetResponseAsync` retiré de `IMessageProducer<T>` et `Producer<T>` (**breaking change MINOR**)
- `RequestReplyAsync` retiré de `IMessagingProvider` et `AzureMessagingProvider`
- `EnableOffline` retiré de `MessagingOptions` (était du code mort)
- `AddRequestReplyClient<TRequest, TResponse>(requestTarget, replyTarget)` — nouvelle extension DI
- Samples `DoWork.cs`, `Worker/Program.cs`, `appsettings.json` Worker et `local.settings.json` Activator corrigés

**Livrables :**
1. **Côté EMT (librairie) :**
   - Nouvelle interface `IRequestReplyClient<TRequest, TResponse>` séparée de `IMessageProducer<T>` (résout I1 §9.5).
   - Implémentation `AzureRequestReplyClient<TRequest, TResponse>` utilisant `ServiceBusSenderCache` (pas de `CreateSender` ad-hoc).
   - `SessionReceiver` pool pour la réception des réponses.
   - Timeout configurable + propagation OTel.

2. **Côté samples :**
   - Re-encodage UTF-8 propre de `Activator.cs` (S7).
   - Enregistrement `ServiceBusClient` singleton dans le DI (C1).
   - `RequestReplyConsumer` refactoré pour utiliser le nouveau `IRequestReplyClient` au lieu du `ServiceBusSender` brut (C2).
   - Suppression du cast `AzureFunctionMessageTransit` au profit d'un accesseur typé (C3).
   - Suppression du type anonyme au profit de `ReplyMessage` typé (I1).
   - `AddScoped` au lieu de `AddTransient` (I2).
   - Catches EMT complets : `ImmediateRetry/ExponentialRetry/ImmediateDLQ/OperationCanceled` (I3).
   - `ConfigureAzureProviders(new VisualStudioCredential())` en dev (I4).
   - Côté requester (worker) implémenté de bout en bout (I5).
   - `Logger.BeginScope` avec `MessageId/SessionId/RequestId` (S5).

3. **Tests :**
   - Test d'intégration end-to-end : requester publie, responder répond, requester reçoit, timeout testé, perte de réponse testée.

**Critère de sortie :**
- `dotnet build` 0 erreur sur tous les samples R/R.
- Test d'intégration vert.
- 10 scénarios documentés dans `Exemples/RAMQ.Samples.Queue.RequestReply.README.md`.

**Estimation :** 3-4 semaines (1 dev expérimenté).
**Risque :** 🟠 Moyen — touche le contrat public (`IMessageProducer<T>` perd `GetResponseAsync`).
**Priorité :** 🔴 **Critique** — le sample induit en erreur tout nouveau venu.

### 11.4 Lot R4 — Compléter le triangle idempotence

**Origine :** DE Review O9 ; Section [§6.5](#65-idempotence-et-duplicate-detection).
**Objectif :** garantir que le triangle (infra + code + métier) est tenu en production.

**Livrables :**
1. **Section "Prérequis infrastructure" dans `docs/architecture-technique.md`** listant les options exigées (`requiresDuplicateDetection`, `duplicateDetectionHistoryTimeWindow`, `maxDeliveryCount`, etc.) avec extraits Bicep + Terraform prêts à copier.

2. **Health check étendu `ServiceBusConfigurationHealthCheck`** qui :
   - Interroge l'API d'administration Service Bus au démarrage.
   - Logue un warning (mode dev) ou échoue le démarrage (mode strict) si :
     - `RequiresDuplicateDetection = false` sur une entité consommée.
     - `DuplicateDetectionHistoryTimeWindow < 30 minutes`.
     - `MaxDeliveryCount` en désaccord avec `ExponentialRetryPolicy.MaxDeliveryCount`.

3. **Sample dédié** `RAMQ.Samples.Queue.Idempotence.MessageIdDeterministe` montrant `Hash(DossierId + Operation)` comme `MessageId`.

4. **Counter OTel** `duplicate_messages_detected_total{entity}` exposé via `MetricsProvider`.

**Critère de sortie :** health check disponible, sample tourne, document publié.
**Estimation :** 2 semaines (1 dev + 1 revue Tech Lead).
**Risque :** 🟢 Faible — additif sauf en mode strict.
**Priorité :** 🟠 **Majeur** — risque métier réel (double paiement).

### 11.5 Lot R5 — Claim Check : nettoyage des orphelins + métriques

**Origine :** DE Review §4.1 point 3 ; §8.3 ; Section [§8.3](#83-claim-check).
**Objectif :** éliminer la dette « blobs orphelins » qui s'accumule en production (conformité CAI/RGPD).

**Livrables :**
1. **Politique de cycle de vie Blob** (Lifecycle Management Policy) : suppression auto après N jours (configurable, défaut 30j).
2. **Compensation explicite** : si l'envoi Service Bus échoue **après** un upload Blob réussi, le `Producer.PublishCoreAsync` supprime le blob (`StorageProvider.DeleteAsync` en `catch`). Code partiellement présent (`Producer.cs:194-205`) à compléter.
3. **Counter OTel** `claimcheck_orphans_deleted_total` — les counters `claimcheck_uploads_total` + `claimcheck_download_duration_ms` sont livrés par R7.
4. **Job de nettoyage** (Function timer trigger optionnel) qui parcourt les blobs > N jours non référencés.

**Critère de sortie :** policy déployée en preprod, job tourne, métriques visibles.
**Estimation :** 2 semaines (1 dev + 1 Azure DevOps).
**Risque :** 🟡 Moyen — un job de nettoyage trop agressif peut supprimer des blobs encore référencés. Couvrir par tests d'intégration.
**Priorité :** 🟠 **Majeur** — risque réglementaire.

### 11.6 Lot R6 — Journal batch async ✅ Livré

**Origine :** DE Review O14.
**Objectif :** éliminer le O(n) séquentiel du journal dans `PublishBatchAsync`.

**Livré le 27 mai 2026 :**
- `IJournalProvider.WriteBatchAsync(IEnumerable<JournalEntry>, CancellationToken)` — nouvelle méthode sur l'interface.
- `AzureJournalProvider.WriteBatchAsync` : regroupe par `PartitionKey`, soumet via `TableClient.SubmitTransactionAsync`, découpe en tranches de ≤ 100 (limite Azure Table).
- `AzureJournalProvider.BuildEntity` — helper privé factorisé entre `WriteRecordAsync` et `WriteBatchAsync`.
- `Producer.PublishBatchAsync` : remplace `Task.WhenAll(N × WriteRecordAsync)` par un seul appel `WriteBatchAsync` — 1 round-trip HTTP par partition au lieu de N (gain > 5× sur batch de 100 messages).
- Suppression de `WriteJournalEntrySafeAsync` devenu obsolète.

**Critère de sortie :** bench BenchmarkDotNet à réaliser avant merge en prod (cible mesurée ≥ 5×).
**Risque :** 🟢 Faible — Pattern A5 préservé : le `try/catch` dans `Producer` absorbe tout échec de journal.
**Priorité :** 🟡 **Mineur** — optimisation.

### 11.7 Lot R7 — Métriques opérationnelles manquantes ✅ Livré

**Origine :** DE Review §4.1 points 4-5, §8 audit Circuit Breaker, §8 audit Saga.
**Objectif :** rendre toutes les transitions et défaillances visibles en production.

**Livrables :** étendre `IMetricsProvider` + `MetricsProvider` avec :

| Métrique | Type | Labels | Usage | Statut |
|---|---|---|---|---|
| `circuit_state{entity}` | Gauge | entity | État actuel (0=Closed, 1=Open, 2=HalfOpen) | ✅ Câblé dans `CircuitBreakerManager` |
| `circuit_transitions_total{entity,from,to}` | Counter | entity, from, to | Compter les ouvertures/fermetures | ✅ Câblé dans `CircuitBreakerManager` |
| `claimcheck_uploads_total{entity}` | Counter | entity | Volume de claim-checks | ✅ Câblé dans `Producer` |
| `claimcheck_downloads_total{entity}` | Counter | entity | Volume de downloads | ✅ Câblé dans `AzureStorageProvider` |
| `routing_slip_compensation_total{slip_name,reason}` | Counter | slip_name, reason | Compensation déclenchée | ✅ Câblé dans `RoutingSlipExecutor` |
| `duplicate_messages_detected_total{entity}` | Counter | entity | Dédup côté broker | ✅ Existant |
| `deserialization_failures_total{reason}` | Counter | reason | Déjà implémenté (P4) | ✅ Câblé dans `AzureConsumerTelemetry` |

**Livrables réalisés :**
- `IMetricsProvider` : ajout de `IncrementClaimCheckUploads`, `IncrementClaimCheckDownloads`, `IncrementRoutingSlipCompensation`.
- `MetricsProvider` : implémentation des 3 nouveaux compteurs.
- `CircuitBreakerManager` : injecte `IMetricsProvider?`, appelle `SetCircuitState` + `IncrementCircuitTransition` sur chaque transition (Closed↔Open↔HalfOpen).
- `RoutingSlipExecutor` : injecte `IMetricsProvider?`, appelle `IncrementRoutingSlipCompensation` sur `FaultResult`.
- `Producer` : appelle `IncrementClaimCheckUploads` après chaque upload Blob réussi.
- `AzureStorageProvider` : injecte `IMetricsProvider?`, appelle `RecordClaimCheckDownloadDuration` + `IncrementClaimCheckDownloads` dans `DownloadAsync`.

**Critère de sortie :** Grafana dashboard de référence dans `docs/observability/dashboard.json` — hors scope (SRE).
**Estimation :** 1 semaine (1 dev + 1 SRE pour Grafana).
**Risque :** 🟢 Faible.
**Priorité :** 🟠 **Majeur** — sans métriques, les nouvelles fonctionnalités sont invisibles en prod.

### 11.8 Lot R8 — SRP : déléguer settlement/telemetry hors `BaseConsumer` ✅ Livré

**Origine :** §9.2 SRP violation S1.
**Objectif :** appliquer SRP au `BaseConsumer<T>` pour qu'il ne fasse que « consommer ».

**Livrables :**
1. Nouvelle interface interne `IConsumerSettlement` :
   ```csharp
   internal interface IConsumerSettlement {
       Task CompleteAsync(CancellationToken ct);
       Task DeadLetterAsync(Exception ex, CancellationToken ct);
       Task ImmediateRetryAsync(ImmediateRetryException ex, CancellationToken ct);
       Task ExponentialRetryAsync(ExponentialRetryException ex, CancellationToken ct);
   }
   ```
2. `AzureConsumerSettlement` implémente cette interface.
3. `BaseConsumer<T>` injecte `IConsumerSettlement` au lieu d'appeler directement `MessagingProvider.CompleteMessageAsync`.
4. Nouvelle interface `IConsumerTelemetry` regroupant les `SetTag` OTel + métriques de réception.

**Livrables réalisés :**
- `IConsumerTelemetry` (internal) + `AzureConsumerTelemetry` — encapsule les spans OTel + métriques.
- `ConsumeScope` (internal) — gère les activités `messaging.consume` + `messaging.deserialize`.
- `BaseConsumer<T>` : suppression de `protected MessagingProvider` ; délégation via `IMessageReceiver`, `IMessageSettler`, `IMessageDeserializer`, `IMessagingEndpointResolver`, `IConsumerTelemetry`.
- Réduit de ~200 lignes à ~115 lignes ; constructor backward-compatible (accepte toujours `IMessagingProvider`).

**Critère de sortie :** `BaseConsumer<T>` réduit de ~200 lignes à ~80 lignes ; classe testable avec mocks `IConsumerSettlement` + `IConsumerTelemetry`.
**Estimation :** 2-3 semaines (1 dev + 1 revue).
**Risque :** 🟠 Moyen — touche l'API héritée (potentiel breaking change pour consumers existants).
**Priorité :** 🟡 **Mineur** — qualité interne, pas impact métier.

### 11.9 Lot R9 — ISP/OCP : scinder `IMessagingProvider` en 5 interfaces fines ✅ Livré

**Origine :** §9.3 O1, §9.5 I2, §9.6 D2.
**Objectif :** ouvrir le système à de nouveaux patterns (Streaming, Scheduled Delivery, Saga.Stateful) sans modifier l'interface existante.

**Livrables réalisés :**
- `IMessagePublisher`, `IMessageReceiver`, `IMessageSettler`, `IMessagingEndpointResolver`, `IMessageDeserializer` — 5 interfaces fines.
- `IMessageActions : IMessageReceiver, IMessageSettler` — composite pour `IMessagingAdapter`.
- `IMessagingProvider : IMessagePublisher, IMessageActions, IMessagingEndpointResolver, IMessageDeserializer` — backward-compat.
- DI : `ConfigureAzureProviders` registre les 5 interfaces pointant vers la même instance `IMessagingProvider`.
- `Producer<T>` injecte `IMessagePublisher` + `IMessagingEndpointResolver` uniquement.

**Critère de sortie :** `Producer<T>` n'injecte que `IMessagePublisher` + `IMessagingEndpointResolver` ; `BaseConsumer<T>` n'injecte que `IMessageReceiver` + `IMessageSettler`.
**Estimation :** 3-4 semaines (1 senior + 1 revue Lead).
**Risque :** 🟠 Moyen — breaking change MINOR si bien communiqué.
**Priorité :** 🟡 **Mineur** — qualité interne ; à coordonner avec R8.

### 11.10 Lot R10 — DIP : sortir `AzureFunctionMessagingAdapter` en assembly hosting 🚫 Hors scope (Consumer)

**Origine :** §9.6 D1 ; DE Review §3.3 ; ADR-001 (multi-hôte).
**Objectif initial :** rendre le Consumer EMT utilisable depuis AKS / ARO via `BackgroundService` sans forcer la dépendance `Microsoft.Azure.Functions.Worker`.

> **Décision (2026-05-28) :** R10 est explicitement **hors scope pour le Consumer**. Clarification du modèle de déploiement v1.0 :
> - **Producer** (`Producer<T>`, `IMessagePublisher`) : **multi-hôte**. Peut tourner dans une Azure Function, un container AKS, ARO, ou un Worker Service ASP.NET Core. Aucune dépendance sur `Microsoft.Azure.Functions.Worker` après R9.
> - **Consumer** (`BaseConsumer<T>`, `AzureFunctionMessagingAdapter`) : **exclusivement Azure Functions** Isolated Worker. Dépend de `ServiceBusReceivedMessage` / `ServiceBusMessageActions`. Le découplage n'apporte pas de valeur car aucun Consumer n'est prévu sur AKS/ARO. Phase 6 (multi-broker) est hors scope — ce lot reste non prévu.

**Critère de sortie :** assembly principal n'a plus de référence à `Microsoft.Azure.Functions.Worker` (test NetArchTest).
**Estimation :** 4-5 semaines (1 senior).
**Risque :** 🟠 Moyen — breaking change MAJOR pour les applications qui référencent `AzureFunctionMessagingAdapter` directement.
**Priorité :** 🚫 **Annulé** — Azure Functions uniquement.

### 11.11 Lot R11 — Tests automatisés sur les samples

**Origine :** Observation S-5 + S-7.
**Objectif :** garantir que les samples restent fonctionnels (régression silencieuse interdite).

**Livrables :**
1. Projet `RAMQ.Samples.Tests` contenant un smoke test par sample (build + démarrage Functions + envoi message + assertion de réception).
2. `docker-compose.samples.yml` à la racine des samples (Service Bus Emulator + Azurite).
3. CI bloquante : tout PR qui casse un sample échoue.
4. Tests unitaires des `IRoutingSlipActivity<TArgs>` (objectif phare v2.0 — démontrer la testabilité).

**Critère de sortie :** CI verte sur les 26 samples ; couverture indicative > 30 % sur les activités.
**Estimation :** 3 semaines (1 dev).
**Risque :** 🟢 Faible — additif.
**Priorité :** 🟠 **Majeur** — sans cela, R3 (refonte R/R) risque de regresser à nouveau.

### 11.12 Lot R12 — Helper DI partagé `services.AddEMTSampleDefaults()` ✅ Livré

**Origine :** Observation S-4.
**Objectif :** éliminer le copy/paste des `Program.cs` dans les 26 samples (réduit risque de dérive).

**Livrables réalisés :**
- `RAMQ.Samples.MessageTransitHelper` — projet partagé exposant `EMTSampleExtensions`.
- `AddEMTSampleProducerDefaults(IConfiguration, TokenCredential?)` — enregistre AppSettings + ProducerConfigurationService + ConfigureAzureProviders.
- `AddEMTSampleConsumerDefaults(IConfiguration, TokenCredential?)` — même pattern côté consumer.
- Appliqué à 10 samples : Simple, MultiTarget, RequestReply, PubSub, RoutingSlip (Queue + Topic).

**Critère de sortie :** chaque `Program.cs` sample passe de ~60 lignes à ~15 lignes.
**Estimation :** 1 semaine.
**Risque :** 🟢 Faible.
**Priorité :** 🟡 **Mineur** — qualité pédagogique.

### 11.13 Récapitulatif et planning global

| Lot | Description | Estimation | Priorité | Bloqué par | Statut |
|---|---|---|---|---|---|
| **R1** | Couverture tests (F6, F10) | 1-2 sem. | 🔴 Critique | — | ⬜ À démarrer |
| **R2** | Sample Claim Check | 1 sem. | 🟠 Majeur | R1 | ⬜ À démarrer |
| **R3** | Refonte Request/Reply | 3-4 sem. | 🔴 Critique | R1 | ✅ **Livré** |
| **R4** | Triangle idempotence | 2 sem. | 🟠 Majeur | R1 | ⬜ À démarrer |
| **R5** | Claim Check orphelins + métriques | 2 sem. | 🟠 Majeur | R1 | ⬜ À démarrer |
| **R6** | Journal batch async | 1 sem. | 🟡 Mineur | R1 | ✅ **Livré** |
| **R7** | Métriques manquantes | 1 sem. | 🟠 Majeur | R1 | ✅ **Livré** |
| **R8** | SRP BaseConsumer | 2-3 sem. | 🟡 Mineur | R1, R9 | ✅ **Livré** |
| **R9** | ISP/OCP scission IMessagingProvider | 3-4 sem. | 🟡 Mineur | R1 | ✅ **Livré** |
| **R10** | DIP sortir adapter Functions (Consumer) | 4-5 sem. | 🟠 Majeur | R1, R9 | 🚫 **Hors scope** — Consumer AzFunc uniquement ; Producer déjà multi-hôte (R9) |
| **R11** | Tests samples | 3 sem. | 🟠 Majeur | R1 | ⬜ À démarrer |
| **R12** | Helper DI samples | 1 sem. | 🟡 Mineur | R11 | ✅ **Livré** |

**Total estimé :** 24-30 semaines en séquentiel ; **9-12 semaines en parallèle** (3-4 devs).

### 11.10 Dépendances entre lots

```
R1 (tests) — pré-requis universel
   ├─→ R3 (R/R)        — critique, 3-4 sem.
   ├─→ R4 (idemp.)     — peut paralléliser avec R3
   ├─→ R5 (claimcheck) — peut paralléliser
   ├─→ R7 (métriques)  — peut paralléliser
   ├─→ R6 (journal)    — peut paralléliser
   ├─→ R2 (sample CC)  — bloqué par R5
   ├─→ R9 (scission)
   │       ├─→ R8 (SRP)
   │       └─→ R10 (hosting)
   └─→ R11 (samples tests)
            └─→ R12 (helper DI)
```

### 11.11 Critères d'acceptation v1.0 stable

Pour que EMT soit considérée v1.0 production-ready :

| ☑ | Critère |
|---|---|
| ☐ | R1 livré — 0 finding ouvert sur la v0.9.0 |
| ☐ | R3 livré — pattern Request/Reply utilisable end-to-end |
| ☐ | R4 livré — triangle idempotence documenté + vérifié au démarrage |
| ☐ | R5 livré — pas de risque réglementaire orphelins blobs |
| ☐ | R7 livré — Grafana dashboard de référence disponible |
| ☐ | R11 livré — CI bloquante sur les 26 samples |
| ☐ | Couverture de tests globale > 70 % |
| ☐ | `CHANGELOG.md` à jour avec section v1.0 |
| ☐ | Guide de migration v0.9 → v1.0 publié |
| ☐ | ADR-001 confirmé (Service Bus only) — pas de Kafka/Confluent dans v1.0 |

### 11.12 Ce qui est explicitement reporté hors v1.0

| Sujet | Raison | Fenêtre |
|---|---|---|
| **Phase 6 entière (multi-broker)** | Phase 6 = Kafka / Confluent / RabbitMQ / CloudEvents. Décision explicite : hors scope. Aucun de ces volets n'est prévu dans cette phase du projet. | 🚫 Non prévu |
| **R10 (sortir adapter Functions — Consumer)** | **Producer** déjà multi-hôte (AzFunc/AKS/ARO) après R9. **Consumer** exclusivement Azure Functions — le découplage Consumer multi-hôte n'apporte pas de valeur aujourd'hui. | 🚫 Non prévu |
| **CloudEvents 1.0** | Fait partie de Phase 6 (multi-broker) — hors scope. | 🚫 Non prévu |
| **Scission complète en 10 packages NuGet** | Big-bang risqué ; à séquencer par vagues si besoin. | v2.0+ |
| **Séparation `MessageEnvelope` / `MessageTransitContext<T>` (O3 structurel)** | Breaking changes en cascade — à grouper avec la bascule MAJOR de Phase 6 (cf. §11.13). | Phase 6 |

---

### 11.13 Analyse des breaking changes — cas O3

> **Question abordée :** la mitigation de O3 (test snapshot Verify.Xunit, livré en Phase 1) suffit-elle, ou doit-on aller jusqu'à la séparation structurelle `MessageEnvelope` / `MessageTransitContext<T>` ? Et si oui, qu'est-ce que ça casse ?

#### 11.13.1 Trois niveaux de résolution possibles

| Niveau | Action | Source break | Wire break | Binary break | Statut |
|---|---|---|---|---|---|
| **N1 — Mitigation** | Tests snapshot Verify.Xunit + doc XML + `<remarks>` sur `[JsonIgnore]` | ❌ Aucun | ❌ Aucun | ❌ Aucun | ✅ **Livré (Phase 1)** |
| **N2 — Additif** | Introduire `MessageEnvelope` à côté ; `MessageTransitContext<T>` délègue en interne ; nouvelles surcharges `PublishAsync(MessageEnvelope, …)` ; ancien API marqué `[Obsolete]` | 🟡 Warnings uniquement | ❌ Aucun (même JSON) | ❌ Aucun | À éviter — voir §11.13.5 |
| **N3 — Séparation stricte** | Suppression progressive de `MessageTransitContext<T>` au profit de `MessageEnvelope` + wrapper minimal | 🔴 Oui (call sites Producer) | 🟠 Possible (selon choix sérialisation) | 🔴 Oui (ABI assembly) | ⛾ **Phase 6 uniquement** |

#### 11.13.2 Détail des breaking changes du niveau N3

**Source breaks** — call sites Producer aujourd'hui :

```csharp
var ctx = new MessageTransitContext<DemandeValidation>
{
    MessageId = Guid.NewGuid().ToString("N"),
    SessionId = dossierId,
    Variables = new Dictionary<string, object> { ["k"] = "v" },
    Message   = new DemandeValidation { ... }
};
await _producer.PublishAsync(ctx, ...);
```

Deviennent :

```csharp
var envelope = new MessageEnvelope { MessageId = ..., SessionId = ..., Variables = ... };
var ctx = new MessageTransitContext<DemandeValidation>(envelope, new DemandeValidation { ... });
await _producer.PublishAsync(ctx, ...);
```

→ Estimation : **80-200 call sites à migrer** côté samples + apps RAMQ consommatrices. Chaque équipe applicative doit produire un PR.

**Binary breaks :**
- Toute application compilée contre l'ancien assembly ne se charge pas avec le nouveau.
- Tous les sous-packages NuGet doivent bumper en MAJOR.
- `PublicAPI.Shipped.txt` change radicalement.

**Wire breaks (le plus dangereux) — silencieux :**

Si la sérialisation JSON change (ex. wrapping CloudEvents) :

```json
// AVANT (v1.x)
{ "MessageId": "...", "Message": { "DossierId": "D-001" } }

// APRÈS (v2.0 si CloudEvents)
{ "specversion": "1.0", "id": "...", "data": { "DossierId": "D-001" } }
```

Conséquences si non-géré :
- **Tous les messages déjà publiés mais non encore consommés** (en queue, en session active, en DLQ) deviennent illisibles.
- **Tous les replays depuis archive** (audit CAI, reprise post-incident) deviennent illisibles.
- **Aucune exception bruyante** : la désérialisation retourne `MessageId = null`, le consumer DLQ tout, et l'équipe découvre 3 jours plus tard que 50 000 messages ont été perdus silencieusement.

#### 11.13.3 Le piège des sagas en vol

Une saga RAMQ peut durer plusieurs heures à plusieurs jours (attente d'un domaine lent, replay manuel après incident). Pendant une transition N3 mal gérée :

```
T0     : Worker v_old publie SlipEnvelope v1 → queue intermédiaire
T0+2h  : Déploiement v_new (format changé)
T0+3h  : Worker v_new lit SlipEnvelope v1 → ne désérialise pas → DLQ silencieux
         ⚠ Saga échoue au milieu, après que les compensateurs des étapes
           précédentes ne soient plus accessibles
```

C'est pour cette raison que tout passage à N3 exige :
1. Une **période de lecture dual-format** (le consumer comprend l'ancien ET le nouveau pendant N semaines),
2. Un **drain de DLQ** avant le bump MAJOR,
3. Un **gel des replays** depuis archive pendant la transition,
4. Un **freeze des sagas longues** ou des protocoles d'attente / pré-conversion.

#### 11.13.4 Pourquoi grouper avec Phase 6

Le coût opérationnel d'un breaking change wire-format est dominé par les **points 1-4 ci-dessus**, pas par le code lui-même. Si on accepte cette douleur **deux fois** (une fois pour O3, une fois pour CloudEvents/multi-broker), on paie le prix double inutilement.

D'où la position retenue :

> **Toute résolution structurelle de O3 (niveau N3) sera groupée avec la prochaine bascule MAJOR Phase 6.** Tant que Phase 6 n'est pas activée (cas d'usage non-Azure concret + ADR-001 révisé), **rester en N1**. Le filet de sécurité Verify.Xunit est suffisant pour les régressions accidentelles.

#### 11.13.5 Pourquoi N2 (additif) n'est pas une bonne idée intermédiaire

À première vue, N2 semble alléchant : ajouter `MessageEnvelope` sans rien casser. En pratique :

| Problème | Détail |
|---|---|
| **Deux APIs publiques en parallèle** | Les nouveaux call sites utilisent `MessageEnvelope`, les anciens `MessageTransitContext<T>`. Le code base perd en cohérence pendant des mois. |
| **`[Obsolete]` mal supporté pour les types** | Warning sur un constructeur ou setter, mais pas sur un object initializer `{ ... }`. Plusieurs équipes ne migreront jamais avant qu'on retire le type. |
| **Double maintenance du test snapshot** | Il faut tester les deux formats sérialisés à chaque PR. La complexité monte. |
| **Pas de gain conceptuel** | Le développeur qui lit `MessageTransitContext<T>` continue à voir un god-object qui « contient » un envelope — c'est pire pédagogiquement qu'aujourd'hui. |
| **Le breaking inéluctable est juste reporté** | On finit quand même par retirer l'ancien type un jour ; on a juste payé un cycle de release supplémentaire pour rien. |

Conclusion : **soit on reste en N1 (suffit aujourd'hui), soit on saute directement en N3 (lors d'une bascule MAJOR groupée).** N2 cumule les coûts des deux sans les bénéfices.

#### 11.13.6 Ce qui reste accessible sans breaking change

Si le problème de lisibilité du god-object devient gênant **avant** Phase 6, voici des micro-améliorations qui n'introduisent **aucun breaking change** :

| Action | Impact | Bénéfice |
|---|---|---|
| Ajouter `<remarks>` XML doc sur `MessageTransitContext<T>` listant explicitement les 3 rôles et leur frontière `[JsonIgnore]` | ❌ Aucun | Lisibilité immédiate dans l'IntelliSense |
| Analyzer Roslyn maison qui warne si quelqu'un ajoute une propriété publique sans `[JsonIgnore]` ou sans mise à jour du test snapshot | ❌ Aucun | Filet de sécurité au moment du PR (avant CI) |
| Versionner le test snapshot par schéma : `envelope-v1.verified.txt`, `envelope-v2.verified.txt` quand la v2 viendra | ❌ Aucun | Documente la stratégie de versioning attendue |
| Passer `IsClaimCheckApplied`, `SerializedPayload`, `TransportMessage` en `internal set` (au lieu de `public set`) | 🟡 Mineur (source seul) | Empêche un consumer applicatif de muter ces champs runtime par erreur |
| Extraire `GetVariable<T>`, `CopyWithResponse`, `GetMessageToken` dans `MessageTransitContextExtensions` | 🟡 Mineur (warnings d'usage) | Sépare comportement de donnée sans casser |

Ces actions peuvent être livrées **dans n'importe quel sprint de v1.x** sans surcoût et sans rupture, et elles préparent terrain pour Phase 6.

#### 11.13.7 Recommandation finale

| Horizon | Action |
|---|---|
| **Maintenant (v1.0)** | Rester en **N1**. Garder le filet snapshot. Appliquer optionnellement les micro-améliorations §11.13.6. |
| **v1.1 – v1.x** | Idem N1. Ne **pas** introduire N2. |
| **Phase 6 (si activée)** | Bascule en **N3** groupée avec CloudEvents et/ou nouveau transport. Plan de migration explicite avec lecture dual-format, drain DLQ, freeze sagas. |

> 🧭 **Position politique :** la résolution de O3 n'est pas un bug à fixer en urgence — c'est une **dette de conception consciente** dont le coût de remboursement est élevé mais finançable lors de la prochaine bascule MAJOR. Le filet de sécurité Verify.Xunit transforme une dette dangereuse (régression silencieuse possible) en une dette gérable (impossible de la creuser sans alerte CI).

---

## 12. Feuille de route

```
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 1 — Fondations non-régressables (4-6 sem.)        ✅ Livrée   │
│ O1 · O3 · O18                                                       │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 2 — Durcissement architectural (10-14 sem.)       ✅ Livrée   │
│ O2 · O4 · O5 · O6 · O7 · O12 · O13 · O16 · O17 · O19 · O20          │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 3 — Polissage qualité (3-5 sem.)                  ✅ Livrée   │
│ O8 · O9 · O10 · O11 · O14 · O15                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 4 — Performance et enveloppe opérationnelle       ✅ Livrée   │
│ Benchmarks · SB Emulator · operational-envelope.md                  │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 5 — Routing Slip natif v2.0                        ✅ Livrée  │
│ Breaking change MAJOR — IRoutingSlipActivity, SlipEnvelope          │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ V1.0 STABLE — Lots R1, R2, R4, R5, R11 (6-9 sem. parallélisé) ⬜    │
│ Couverture · Idempotence · Claim Check orphelins · Tests Samples     │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ V1.1 — SOLID Refactors (R8, R9, R12)                    ✅ Livré   │
│ BaseConsumer SRP · Scission IMessagingProvider · Helper DI samples  │
│ R10 (Consumer adapter Functions) — 🚫 Hors scope ; Producer multi-hôte ✅│
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│ PHASE 6 — Multi-broker (Kafka / Confluent / RabbitMQ / CloudEvents) │
│                                              🚫 HORS SCOPE          │
│ Phase 6 entière = support multi-broker — NON PRÉVU dans ce projet   │
│ Kafka / Confluent / RabbitMQ / CloudEvents — ne PAS implémenter     │
└─────────────────────────────────────────────────────────────────────┘
```

### 12.1 Position du scope sur la Phase 6

> 🚫 **Phase 6 entièrement hors scope.**
> Phase 6 = support multi-broker (Kafka / Confluent / RabbitMQ / CloudEvents). Ce volet est **non prévu** dans cette phase du projet.
>
> - Kafka / Confluent : 🚫 hors scope — `RAMQ.Integration.Transport.Kafka` **gelé**.
> - RabbitMQ : 🚫 hors scope — aucun partenaire RAMQ n'exige AMQP à ce stade.
> - CloudEvents 1.0 : 🚫 hors scope — rattaché à Phase 6 multi-broker.
> - Consumer multi-hôte (AKS/ARO) : 🚫 hors scope (R10) — Consumer exclusivement Azure Functions.
>
> **Seul broker supporté : Azure Service Bus.** Toute évolution multi-broker nécessitera une décision de phase future explicite.

---

## 13. Glossaire

| Terme | Définition pédagogique |
|---|---|
| **Adapter** | Couche fine qui traduit une API tierce (ex. Azure Functions Worker) en API EMT neutre. |
| **ADR (Architecture Decision Record)** | Document court qui consigne une décision d'architecture, son contexte, ses alternatives, ses conséquences. |
| **Application Properties** | Métadonnées attachées à un message Service Bus. EMT y met `Consumer`, `Action`, `traceparent`. |
| **at-least-once / at-most-once / effectively-once** | Garanties de livraison. Voir [§6.5](#65-idempotence-et-duplicate-detection). |
| **Backpressure** | Rejette les requêtes quand saturé, plutôt que tout accepter et planter. EMT a `MaxBatchSize`. |
| **Captive dependency** | Anti-pattern : Scoped consommé par Singleton — Scoped devient effectivement Singleton. |
| **Circuit Breaker** | Après N échecs, on coupe le circuit. États Closed/Open/HalfOpen. |
| **Claim Check** | Payload > 256 Ko → upload Blob, message porte juste la référence. |
| **CloudEvents 1.0** | Standard CNCF d'enveloppe portable. 🚫 Non prévu — Phase 6 = multi-broker, hors scope. |
| **CorrelationId** | Identifiant immuable propagé bout-en-bout, survit aux retries. |
| **DLQ (Dead Letter Queue)** | File spéciale Service Bus pour les messages morts. À monitorer en production. |
| **Endpoint** | Cible logique (Target) + détails transport (EntityName, EntityType, Subscription). |
| **Idempotence** | Une opération qui produit le même résultat 1 ou N fois. Essentielle pour les retries. |
| **Itinerary** | Liste d'étapes (v1 dans `AppSettings`, v2.0 dans le `SlipEnvelope`). |
| **MessageId** | Identifiant unique du message côté producer. Sert à la duplicate detection broker. |
| **OpenTelemetry / OTel** | Standard de télémétrie unifié (traces, métriques, logs). |
| **Pattern A5** | Convention RAMQ : journalisation **découplée du chemin critique**. |
| **POCO (Plain Old CLR Object)** | Objet C# sans dépendance framework — testable avec `new`. |
| **Provider** | Implémentation infrastructure (ex. `AzureMessagingProvider`). |
| **Routing Slip** | Pattern saga où le message porte son itinéraire. |
| **SemVer (Semantic Versioning)** | `MAJOR.MINOR.PATCH`. MAJOR = breaking change. |
| **Session (Service Bus)** | Garantit FIFO + traitement mono-consumer par `SessionId`. |
| **SLO (Service Level Objective)** | Objectif chiffré : « p99 latence publish < 500ms ». |
| **SOLID** | Cinq principes de conception OO. Cf. §9. |
| **Target** | Nom logique d'un endpoint (ex. `"individu"`). |
| **Token (claim-check)** | Référence vers un blob (Reference, ContentType, SizeBytes). |
| **WCF (Windows Communication Foundation)** | Pile SOAP legacy Microsoft, encore présente chez RAMQ. |
| **W3C Trace Context (`traceparent`)** | Standard W3C de propagation de contexte de trace cross-services. |

---

## 14. Pour aller plus loin

### Ordre de lecture recommandé

1. **Ce document** — vue d'ensemble.
2. **[Vue d'ensemble.md](../EnterpriseMessageTransit/docs/Vue%20d%27ensemble.md)** — version interne d'introduction.
3. **[architecture-technique.md](../EnterpriseMessageTransit/docs/architecture-technique.md)** — architecture détaillée.
4. **[architecture-routing-slip.md](../EnterpriseMessageTransit/docs/architecture-routing-slip.md)** — la saga v2.0.
5. **Sample `Queue.Simple`** — premier sample à lire.
6. **Sample `Queue.RoutingSlip.Booking`** — référence du Routing Slip v2.0.
7. **[failure-modes.md](../EnterpriseMessageTransit/docs/failure-modes.md)**, **[operational-envelope.md](../EnterpriseMessageTransit/docs/operational-envelope.md)**, **[idempotence.md](../EnterpriseMessageTransit/docs/idempotence.md)**.

### Les fichiers code à lire en priorité

```
1. Messaging/MessageTransitContext.cs              ← LE pivot
2. Messaging/Producer/IMessageProducer.cs          ← contrat producer
3. Messaging/Producer/Producer.cs                  ← orchestration
4. Messaging/Consumer/BaseConsumer.cs              ← classe de base
5. Messaging/Providers/Azure/AzureMessagingProvider.cs
6. Messaging/RoutingSlip/IRoutingSlipActivity.cs   ← contrat activité
7. Messaging/RoutingSlip/RoutingSlipExecutor.cs    ← chef d'orchestre v2.0
8. Configuration/EndpointResolver.cs               ← Target → endpoint
9. Messaging/Providers/Azure/RetryPolicyHandler.cs
10. Messaging/Providers/CircuitBreakerManager.cs
```

### Questions à se poser quand on contribue

| ☑ | Question |
|---|---|
| ☐ | Mon changement modifie-t-il la sérialisation JSON de `MessageTransitContext` ? Si oui → MAJOR + ADR. |
| ☐ | Mon changement ajoute-t-il un type `public` ? Si oui → `PublicAPI.Unshipped.txt`. |
| ☐ | Ai-je un test (unitaire ou contrat) ? |
| ☐ | Suis-je sur un chemin critique de production (retry, settlement, claim-check) ? Si oui → revue Lead. |
| ☐ | Code applicatif en anglais (identifiants + exceptions) ? Doc XML peut rester FR. |
| ☐ | `CancellationToken` propagé à tous les I/O ? |
| ☐ | Exceptions de la hiérarchie EMT (`ImmediateRetry/ExponentialRetry/ImmediateDLQ`) ? |
| ☐ | **SOLID** : ai-je ajouté une responsabilité à une classe qui en avait déjà ? |
| ☐ | **Sample** : si je modifie un contrat public, ai-je vérifié que les 26 samples compilent toujours ? |

---

## Conclusion — ce qu'il faut retenir en 10 phrases

1. **EMT est une lib plateforme interne RAMQ**, pas un produit générique. Ses patterns reflètent les contraintes réglementaires (santé, CAI) et techniques (WCF legacy) de RAMQ.
2. **Trois produits sont superposés** dans un seul assembly : SDK abstrait (P1), adapter Azure Functions opinioné (P2), moteur Routing Slip RAMQ (P3).
3. **Le `MessageTransitContext<T>` est le pivot — mais c'est un god-object.** Le filet snapshot Verify.Xunit (Phase 1) empêche les régressions silencieuses, mais le mélange « contrat sérialisé + état runtime + comportement » n'est **pas** structurellement résolu. La séparation `MessageEnvelope` / `MessageTransitContext<T>` est **alignée sur Phase 6** pour grouper le breaking change avec une éventuelle adoption CloudEvents (cf. [§11.13](#1113-analyse-des-breaking-changes--cas-o3)).
4. **Le Routing Slip v2.0** est livré et constitue **la référence SOLID** de la lib — c'est le sous-système le mieux conçu. Tous les refactors futurs doivent s'en inspirer.
5. **L'idempotence repose sur un triangle** : infra + EMT + métier. Sans validation infrastructurelle (lot R4), le triangle est incomplet — risque réel de doublons métier.
6. **Le pattern Request/Reply est opérationnel** — lot R3 livré. `IRequestReplyClient<TRequest,TResponse>` séparé de `IMessageProducer<T>`, samples refondus de bout en bout (C1/C2/C3/I5 résolus).
7. **SOLID est partiellement résolu** : R8/R9 livrés. Modèle de déploiement clarifié — **Producer** est multi-hôte (AzFunc / AKS / ARO) ; **Consumer** est exclusivement Azure Functions. DIP sur le Consumer-adapter (R10) reste hors scope v1.0.
8. **Les 26 samples sont la documentation vivante.** Les samples R/R (anciennement cassés) sont désormais fonctionnels (R3). Trous pédagogiques restants : Claim Check actif (R2), métriques OTel (R8 livré, à illustrer), tests d'activités (R11).
9. **Phase 6 est entièrement hors scope.** Phase 6 = support multi-broker (Kafka / Confluent / RabbitMQ / CloudEvents). Aucun de ces volets n'est prévu dans cette phase du projet. Seul Azure Service Bus est dans le scope.
10. **Le plan de résolution R1-R12** chiffre 9-12 semaines en parallèle (3-4 devs) pour atteindre une v1.0 stable production-ready, avec critères d'acceptation explicites en [§11.11](#1111-critères-dacceptation-v10-stable).

---

*Document généré le 27 mai 2026 par revue agentique consolidée à partir des sources listées en en-tête + analyse des 26 projets `Exemples/` + audit SOLID ligne-par-ligne sur les classes principales d'EMT. Pour toute question, ouvrir une issue ou contacter l'équipe d'architecture RAMQ.*
