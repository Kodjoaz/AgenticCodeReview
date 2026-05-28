# EMT Deep Review — Comprendre Enterprise Message Transit de A à Z

> **Audience cible :** développeur junior arrivant sur le projet RAMQ et devant comprendre la librairie `EnterpriseMessageTransit` (EMT) **et tous les exemples du dossier `Exemples/`** sans pré-requis.
> **Objectif :** synthétiser dans un seul document toutes les revues effectuées sur la librairie (Senior, Lead, Distinguished, Phases 1-5, EMT 1.0, Request/Reply, Routing-Slip), inventorier les **35 projets exemples**, vérifier le respect des **principes SOLID ligne par ligne**, l'intégrité de **chaque pattern enterprise**, et fournir un **plan de résolution chiffré et priorisé**.
> **Sources consolidées :**
> - [EMT-SeniorEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-SeniorEngineerReview.md) — revue ligne-à-ligne du code Producer
> - [EMT-LeadEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-LeadEngineerReview.md) — conception locale, SRP, performance
> - [EMT-DistinguishedEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-DistinguishedEngineerReview.md) — angle plateforme, portabilité, gouvernance
> - [EMT-Review-Phase1.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase1.md) à [Phase5.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase5.md) — feuille de route
> - [architecture-routing-slip.md](../EnterpriseMessageTransit/docs/architecture-routing-slip.md) — refonte saga v2.0
> - [EMT1.0Review.md](EMT1.0Review.md) — revue v0.9.0 (Routing Slip v2.0 livré)
> - [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) — pattern Request/Reply partiel
> - [Exemples/](../Exemples/) — 35 projets de démonstration
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
7. [Inventaire des exemples — 35 projets dans `Exemples/`](#7-inventaire-des-exemples-31-projets-dans-exemples)
8. [Vérification des patterns enterprise — état actuel](#8-vérification-des-patterns-enterprise)
9. [Audit SOLID — ligne par ligne](#9-audit-solid--ligne-par-ligne)
10. [Récapitulatif des revues — corrigé et restant](#10-récapitulatif-des-revues)
11. [Plan de résolution — priorisé et chiffré](#11-plan-de-résolution)
    - [11.13 Analyse des breaking changes — cas O3](#1113-analyse-des-breaking-changes--cas-o3)
12. [Feuille de route — où va EMT](#12-feuille-de-route)
13. [Design For Operation — architecture observabilité enterprise](#13-design-for-operation--architecture-observabilité-enterprise)
    - [13.4 ILogger vs OpenTelemetry — la nuance critique](#134-ilogger-vs-opentelemetry--la-nuance-critique)
    - [13.5 Stratégie de filtrage à chaque étage](#135-stratégie-de-filtrage-à-chaque-étage--design-for-operation)
    - [13.10 Workbooks et alertes](#1310-workbooks-et-alertes--livrables-opérationnels-recommandés)
    - [13.11 Checklist pour démarrer](#1311-pour-démarrer--checklist-design-for-operation-pour-un-nouveau-domaine-ramq)
    - [13.12 Plan d'implémentation DFO en 4 phases (8-10 semaines)](#1312-plan-dimplémentation-design-for-operation)
14. [Glossaire](#14-glossaire)
15. [Pour aller plus loin](#15-pour-aller-plus-loin)

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

#### Qu'est-ce qu'une saga dans cette solution ?

Dans EMT, une **saga** est un processus métier distribué qui traverse plusieurs services de façon séquentielle, avec compensation automatique en cas d'échec. Ce n'est pas un orchestrateur central (pas de Durable Functions, pas de base d'état partagée) — c'est un **Routing Slip** : **l'itinéraire complet est embarqué dans le message lui-même** (le `SlipEnvelope`), et chaque étape avance le curseur avant de transmettre au suivant.

> 💡 **Pourquoi ce choix ?** RAMQ a des domaines cloisonnés avec des identités managées distinctes et des RBAC scopés. Un orchestrateur central devrait avoir accès à tous les domaines — ce qui viole la sécurité. Avec le Routing Slip, chaque worker ne connaît que sa propre queue et son propre domaine.

#### Les 3 acteurs du Routing Slip

```
ACTIVATEUR (Azure Function HTTP)
  → construit le SlipEnvelope via RoutingSlipBuilder
  → définit les étapes : nom, target (queue/topic), arguments typés
  → publie sur la première queue

ROUTING SLIP EXECUTOR (interne EMT — RoutingSlipExecutor<TArgs>)
  → désérialise l'enveloppe et les arguments de l'étape courante
  → appelle l'activité POCO : ExecuteAsync(ActivityContext<TArgs>, ct)
  → selon le résultat :
       Next        → avance le curseur, enrichit les Variables, publie à l'étape suivante
       Complete    → complète le message (fin normale)
       Fault       → DLQ + déclenche la compensation LIFO
       RetryImmediate / RetryExponential → lève l'exception EMT correspondante

ACTIVITÉ (IRoutingSlipActivity<TArgs> — POCO testable, zéro dépendance EMT)
  → reçoit ActivityContext<TArgs> (arguments de l'étape + variables partagées)
  → exécute la logique métier
  → retourne ActivityResult
```

#### Compensation (LIFO)

Si une étape retourne `ActivityResult.Fault(ex)`, le mécanisme de compensation remonte l'itinéraire en sens inverse et appelle `CompensateAsync` sur chaque étape déjà complétée :

```
Étapes :  [ReserverVoiture ✅] → [ReserverHotel ✅] → [ReserverVol ❌ Fault]
Compensation LIFO :  ReserverHotel.CompensateAsync → ReserverVoiture.CompensateAsync
```

#### Structure du message — `SlipEnvelope`

```csharp
public class SlipEnvelope
{
    public SlipHeader Header { get; init; }          // SlipId, SlipName, CorrelationId
    public IReadOnlyList<SlipStep> Steps { get; init; } // étapes avec Arguments typés
    public int Cursor { get; init; }                  // index étape courante
    public Dictionary<string, JsonElement> Variables { get; init; } // contexte partagé
}
```

`Variables` est le **porte-clés partagé** entre les étapes : une étape peut enrichir les variables (`ActivityResult.Next(vars => vars["IdDossier"] = id)`) et les étapes suivantes les lire via `ctx.Variables`.

#### Construction d'un slip (exemple réservation)

```csharp
var slip = new RoutingSlipBuilder("ReservationVoyage")
    .AddStep<ReserverVoitureArgs>("ReserverVoiture", args => { args.VehiculeType = "SUV"; })
    .AddStep<ReserverHotelArgs>  ("ReserverHotel",   args => { args.NbNuits = 3; })
    .AddStep<ReserverVolArgs>    ("ReserverVol",      args => { args.Destination = "YUL"; })
    .Build(correlationId: sessionId);

await _producer.PublishAsync(new MessageTransitContext<SlipEnvelope> { Message = slip }, null, ct);
```

#### Activité — POCO testable

```csharp
public class ReserverHotelActivity : IRoutingSlipActivity<ReserverHotelArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ReserverHotelArgs> ctx, CancellationToken ct)
    {
        var confirmation = await _hotelApi.ReserverAsync(ctx.Arguments.NbNuits, ct);
        if (!confirmation.IsSuccess)
            return ActivityResult.Fault(new HotelIndisponibleException(confirmation.Reason));

        // Enrichit les variables pour les étapes suivantes
        return ActivityResult.Next(vars => vars["ConfirmationHotel"] = confirmation.NumeroRef);
    }

    public Task CompensateAsync(ActivityContext<ReserverHotelArgs> ctx, CancellationToken ct)
        => _hotelApi.AnnulerReservationAsync(ctx.Variables["ConfirmationHotel"], ct);
}
```

#### Observabilité (R7 livré)

- Span OTel `routing_slip.step` avec tags `slip.id`, `slip.name`, `slip.step`, `slip.cursor`, `slip.total`.
- Counter `routing_slip_compensation_total{slip_name, reason}` incrémenté sur chaque `FaultResult`.
- Voir [§8.6](#86-saga--compensation) pour le tableau de vérification complet.

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
> 2. **Code EMT :** `MessageId` systématiquement renseigné + `TransportSettings.RequiresDuplicateDetection = true` → vérification au démarrage (✅).
> 3. **Métier :** `MessageId` **déterministe** pour les scénarios où le caller retente.

#### Mise en place complète — pseudo-code

**Étape 1 — Infrastructure (Bicep)**
```bicep
resource sbQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'sbq-mon-domaine'
  properties: {
    requiresDuplicateDetection: true              // ← activer côté broker
    duplicateDetectionHistoryTimeWindow: 'PT10M'  // fenêtre 10 minutes
    maxDeliveryCount: 10
  }
}
```

**Étape 2 — Config EMT (`appsettings.json` / `local.settings.json`)**
```json
"Endpoints": [{
  "Target": "mon-domaine",
  "Endpoint": {
    "EntityName": "sbq-mon-domaine",
    "EntityType": "Queue",
    "RequiresDuplicateDetection": true   // ← EMT vérifie au démarrage
  }
}]
```

**Étape 3 — Publisher (MessageId déterministe)**
```csharp
// Le MessageId doit être déterministe : même entrée métier = même MessageId.
// Ainsi, si le caller retente après un timeout réseau, Service Bus déduplique silencieusement.
var messageId = $"dossier-{dossierId}-action-{actionCode}"; // déterministe

var ctx = new MessageTransitContext<MaCommande>
{
    MessageId     = messageId,   // déterministe → protège contre les doubles envois
    CorrelationId = messageId,   // même valeur au 1er publish ; EMT préserve sur retry
    SessionId     = dossierId,   // si sessions activées
    Message       = new MaCommande { ... }
};

await _producer.PublishAsync(ctx, null, cancellationToken);
```

**Étape 4 — Démarrage (fast-fail automatique)**
```
// Au démarrage de l'application, EMT appelle automatiquement :
// ServiceBusAdministrationClient.GetQueueAsync("sbq-mon-domaine")
//   → props.RequiresDuplicateDetection == false ?
//       → ConfigurationException("L'entité 'sbq-mon-domaine' n'a pas RequiresDuplicateDetection...")
//   → props.RequiresDuplicateDetection == true ?
//       → OK, l'application démarre normalement
```

**Ce qui se passe sans `MessageId` déterministe :**
```
Caller envoie MaCommande(dossierId=D-001)  → MessageId=uuid-aaa → Service Bus OK
Timeout réseau → caller retente
Caller envoie MaCommande(dossierId=D-001)  → MessageId=uuid-bbb → Service Bus ACCEPTE (nouveau MessageId)
                                            → ⚠️ doublon métier : commande traitée 2 fois
```

**Ce qui se passe AVEC `MessageId` déterministe + `RequiresDuplicateDetection` :**
```
Caller envoie MaCommande(dossierId=D-001)  → MessageId="dossier-D-001-cmd" → Service Bus OK
Timeout réseau → caller retente
Caller envoie MaCommande(dossierId=D-001)  → MessageId="dossier-D-001-cmd" → Service Bus DÉDUPLIQUE
                                            → ✅ message ignoré silencieusement, 0 doublon
```

#### ⚠️ Comportement critique : MessageId régénéré sur retry exponentiel (sans session)

> **Lors d'un `ExponentialRetry` sans session**, EMT **génère un nouveau `MessageId`** pour le message re-schedulé. Raison : si `RequiresDuplicateDetection = true` est configuré sur la queue, Service Bus rejetterait silencieusement un retry portant le même `MessageId` (il considérerait le message comme un doublon).
>
> Pour préserver la traçabilité bout-en-bout malgré ce changement de `MessageId`, EMT copie l'identifiant original dans le `CorrelationId` du message retry :
> - **1er retry** : `CorrelationId = MessageId` original
> - **Retries suivants** : `CorrelationId` déjà en place → préservé tel quel
>
> **Conséquence pour les développeurs :** pour tracer un message à travers ses retries, utiliser **`CorrelationId`** — pas `MessageId`. Le `CorrelationId` ne change jamais et représente l'identité logique immuable du message original.

```
Publish initial : MessageId=AAA, CorrelationId=AAA
                       ↓ ExponentialRetry (no session)
Retry #1        : MessageId=BBB, CorrelationId=AAA  ← AAA préservé
                       ↓ ExponentialRetry
Retry #2        : MessageId=CCC, CorrelationId=AAA  ← AAA toujours là
```

> ℹ️ **Retry avec session** (`ImmediateRetry` ou `ExponentialRetry` session) : le message est abandonné (`AbandonAsync`) et re-livré par Service Bus — **même `MessageId`**, pas de régénération. L'ordre FIFO de session est préservé.

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

## 7. Inventaire des exemples — 35 projets dans `Exemples/`

Le dossier [`Exemples/`](../Exemples/) contient **35 projets** qui démontrent les patterns EMT en conditions réelles. Compris dans leur ensemble, ils forment la documentation vivante de la librairie.

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
| 25 | `RAMQ.Samples.Queue.TDF.Integration.Producer` | Azure Function HTTP — expose `/tdf/transaction/initial` et `/tdf/transaction/correlation` ; publie `TdfTransactionCommand` via `AddProducer<T>()` | TDF / Sequential Convoy (producer) | v2.0 | 🟢 Active |
| 26 | `RAMQ.Samples.Queue.TDF.Integration.Frontend` | `BackgroundService` (client de test) — génère des transactions TDF complètes toutes les 5 min, appelle le Producer via `ITdfProducerHttpClient` (Refit) | TDF / Sequential Convoy (client) | v2.0 | 🟢 Active |
| 27 | `RAMQ.Samples.Queue.TDF.Integration.Subscriber` | Azure Function `ServiceBusTrigger` session-aware — dispatche vers Durable Orchestrator (`tdf.envoi` → StartOrchestration, `tdf.correller` → RaiseEvent) | TDF / Sequential Convoy (dispatcher) | v2.0 | 🟢 Active |
| 28 | `RAMQ.Samples.Queue.TDF.Integration.Consumer` | Lib partagée — `TdfSeqConConsumer : BaseConsumer<TdfTransactionCommand>` avec logique validation + enrichissement + appel HOA5 réutilisée par Subscriber | TDF / Sequential Convoy (consumer lib) | v2.0 | 🟢 Active |
| 29 | `RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator` | Durable Functions — machine à états Sequential Convoy : attend `tdf.envoi`, timeout configurable, signal `CorrellerEnvoyer`, audit activity, custom status OTel | TDF / Sequential Convoy (orchestrateur) | v2.0 | 🟢 Active |
| 30 | `RAMQ.Samples.Queue.HOA5.Consumer` | Azure Function `BaseConsumer<CorrelationResultMessage>` — reçoit le résultat de corrélation publié par l'orchestrateur et appelle l'API Backend HOA5 | TDF / Sequential Convoy (consumer HOA5) | v2.0 | 🟢 Active |
| 31 | `RAMQ.Samples.Queue.HOA5.Integration.Backend` | Azure Function HTTP Backend — expose les endpoints métier HOA5 (`InscrireSuiviFichCornl`, etc.) appelés par `HOA5.Consumer` via Refit | TDF / Sequential Convoy (backend HOA5) | v2.0 | 🟢 Active |
| 32 | `RAMQ.Samples.Queue.ClaimCheck.Message` | DTO `RapportMessage` (métadonnées : RapportId, PatientId, TypeRapport, FileName, TailleOctets) | Claim Check | n/a | 🟢 Active |
| 33 | `RAMQ.Samples.Queue.ClaimCheck.Worker` | Azure Function HTTP — 3 cas : gros message JSON (> 256 Ko auto), pièce jointe binaire (`WithAttachment`), message léger inline | Claim Check (producteur) | v2.0 | 🟢 Active |
| 34 | `RAMQ.Samples.Queue.ClaimCheck.Consumer` | Lib `ClaimCheckConsumer : BaseConsumer<RapportMessage>` — Option A (référence blob → API downstream) + Option B (download inline via `IStorageProvider`) | Claim Check (consumer lib) | v2.0 | 🟢 Active |
| 35 | `RAMQ.Samples.Queue.ClaimCheck.Activator` | Azure Function `ServiceBusTrigger` — reçoit les messages de la queue `sbq-claimcheck-pdf` et délègue à `ClaimCheckConsumer` | Claim Check (trigger) | v2.0 | 🟢 Active |

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

### 7.3 Famille `Queue.TDF.Integration` — architecture Sequential Convoy complète

`TDF` (Traitement Différé Fiable) démontre le pattern **Sequential Convoy** avec Durable Functions : un orchestrateur stateful attend deux messages corrélés par session, avec timeout configurable et compensation. C'est le cas le plus avancé du repo — **7 projets** couvrant la chaîne complète Frontend → Producer → Subscriber → Orchestrateur → Consumer → Backend HOA5.

```
Frontend (BackgroundService client de test)
  ↓ HTTP POST → Producer (Azure Function HTTP)
Producer → publie TdfTransactionCommand (session-enabled)
  ↓ Service Bus Queue (sbq-tdf-seqcon-session)
Subscriber (ServiceBusTrigger session-aware)
  ├─ step=tdf.envoi  → StartOrchestrationAsync (InstanceId = SessionId)
  └─ step=tdf.correller → RaiseEventAsync("CorrellerEnvoyer")
       ↓
DurableOrchestrator (Sequential Convoy + timeout + audit)
       ↓ publie TransactionCorrelationResult
HOA5.Consumer (BaseConsumer) → appel HTTP → HOA5.Integration.Backend
```

Points techniques remarquables :
- **Sessions Service Bus** pour FIFO par dossier — `IsSessionsEnabled = true`.
- **Idempotence orchestrateur** : `InstanceId = SessionId` empêche les doublons Durable.
- **Observabilité trois niveaux** : logging replay-safe (no side-effects in orchestrator), Custom Status, Activity `RecordAuditActivity`.
- **Découplage Frontend/Producer** : Frontend appelle le Producer via `ITdfProducerHttpClient` (Refit) — jamais directement Service Bus.
- **`TdfSeqConConsumer`** hérite de `BaseConsumer<TdfTransactionCommand>` — réutilisé par injection dans Subscriber.

💡 **Pour un junior :** ne commencez pas par TDF. Lisez d'abord `Queue.Simple`, puis `Queue.RoutingSlip.Booking`, puis seulement TDF.Integration.

### 7.4 Observations transverses sur les samples

| # | Observation | Sévérité |
|---|---|---|
| **S-1** | ~~Aucun sample ne démontre Claim Check actif.~~ ✅ **Résolu R2** — `RAMQ.Samples.Queue.ClaimCheck.*` (4 projets) couvre gros message JSON, pièce jointe binaire et message léger. Options A (référence) et B (download inline) démontrées. | 🟢 Résolu |
| **S-2** | Aucun sample ne démontre le **Circuit Breaker en action** (injection de panne, ouverture, fermeture). Pattern implémenté mais non illustré. | 🟡 Mineur |
| **S-3** | ~~`Queue.RequestReply` est inutilisable en l'état.~~ ✅ **Résolu R3** — C1/C2/C3/I5 corrigés, 4 projets compilent et fonctionnent. | 🟢 Résolu |
| **S-4** | ~~Plusieurs samples copient/collent le même `Program.cs`.~~ ✅ **Résolu R12** — `AddEMTSampleProducerDefaults` + `AddEMTSampleConsumerDefaults` dans `RAMQ.Samples.MessageTransitHelper`, appliqués sur 10 samples. | 🟢 Résolu |
| **S-5** | Aucun sample ne démontre **`IRoutingSlipActivity` testé en isolation** (objectif phare de la v2.0). Pas de projet `*.Tests` côté samples. | 🟠 Majeur |
| **S-6** | `RAMQ.Samples.ConfigurationService` et `RAMQ.Samples.MessageTransitHelper` exposent une API commune sans contrat versionné. Risque de dérive entre samples. | 🟡 Mineur |
| **S-7** | Aucun `docker-compose` n'est fourni pour exécuter les samples localement (Service Bus Emulator + Azurite). Cf. P4-T1 livré dans le projet EMT.Tests, à répliquer pour les samples. | 🟡 Mineur |
| **S-8** | ~~Aucun sample ne montre les métriques OTel.~~ ✅ **Résolu** — `.WithMetrics(m => m.AddMeter(EMTInstrumentation.SourceName))` ajouté dans les 3 samples RoutingSlip (Queue Activateur, Queue Worker, Topic Worker). `IMetricsProvider` déjà enregistré par `ConfigureAzureProviders`. | 🟢 Résolu |

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
| Implémentation | 🟢 Complet | [`ClaimCheckOptions.cs`](../EnterpriseMessageTransit/Messaging/Producer/ClaimCheckOptions.cs), logique upload dans [`ClaimCheckPreparer`](../EnterpriseMessageTransit/Messaging/Producer/ClaimCheckPreparer.cs) (extrait de `Producer<T>` — S3) |
| Complétude | 🟡 Partielle | ✅ Upload + token, ❌ pas de nettoyage automatique des claim-checks orphelins (DE Review §4.1 point 3) |
| Testabilité | 🟢 Bon | `IStorageProvider` mockable |
| Observabilité | 🟢 **Complet** | Counters `claimcheck_uploads_total` + `claimcheck_downloads_total` + histogrammes durée câblés (R7) + tag `messaging.claimcheck` sur span `messaging.publish` |
| Documentation | 🟢 Bon | Aucun sample ne le démontre encore (cf. observation S-1 — lot R2 ouvert) |

🟠 **À compléter (lot R5 du plan de résolution) :**
- TTL Blob automatique ou job de nettoyage des orphelins (risque CAI/RGPD).
- Sample dédié `Queue.ClaimCheck` démontrant l'envoi d'un PDF de 5 Mo.

> ✅ Counters OTel (`claimcheck_uploads_total`, `claimcheck_downloads_total`) déjà livrés en R7.

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
| Observabilité | 🟢 **Complet** | Span dédié `routing_slip.compensation` (tags: `slip.id`, `slip.name`, `slip.step`, `slip.cursor`, `compensation.reason`) + counter `routing_slip_compensation_total{slip_name,reason}` |
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
| Implémentation | 🟢 **Complet** | `TransportSettings.RequiresDuplicateDetection` (défaut `false`) — déclenche validation au démarrage via `IdempotenceValidationService` + `ServiceBusHealthCheck.ValidateIdempotenceAsync`. |
| Complétude | 🟡 Partielle | Validation infra ✅ livrée ; guidance `MessageId` déterministe + sample dédié (lot R4 partiel) restants |
| Testabilité | 🟢 Bon | `ValidateIdempotenceCoreAsync` internal testable sans SDK Azure ; seam de test injecté |
| Observabilité | 🟡 Partielle | Counter `duplicate_detected_total` dans `IMetricsProvider` mais non câblé (broker ne notifie pas les doublons filtrés) |
| Documentation | 🟢 Bon | [idempotence.md](../EnterpriseMessageTransit/docs/idempotence.md) + §6.5 |

> ⚠️ **Invariant de traçabilité :** utiliser `CorrelationId` (immuable) pour corréler un message et ses retries — pas `MessageId` (régénéré à chaque retry exponentiel sans session). Voir [§6.5](#65-idempotence-et-duplicate-detection) pour le détail.

**Utilisation dans `appsettings.json` :**
```json
"Endpoints": [{
  "Target": "mon-endpoint",
  "Endpoint": {
    "EntityName": "sbq-mon-entite",
    "EntityType": "Queue",
    "RequiresDuplicateDetection": true
  }
}]
```
Au démarrage, EMT interroge l'API d'administration Service Bus. Si `RequiresDuplicateDetection` n'est pas activé sur l'entité → `ConfigurationException` avant d'accepter du trafic (fast-fail).

### 8.10 Récapitulatif des patterns

| Pattern | Statut global | Action requise |
|---|---|---|
| Producer / Consumer | 🟢 Complet | RAS |
| Routing Slip v2.0 | 🟢 Complet | RAS — référence à préserver |
| Claim Check | 🟡 70 % | Nettoyage orphelins + sample dédié (R5) |
| Request / Reply | 🟢 **Complet (R3 livré)** | RAS — `IRequestReplyClient<TRequest, TResponse>` |
| Pub/Sub Topic | 🟢 Complet | RAS |
| Saga / Compensation | 🟢 **Complet** | Counter `routing_slip_compensation_total` + span `routing_slip.compensation` câblés |
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
| **S** — Single Responsibility | 🟡 **Partiellement résolu** | S1 résolu R8 ; S3 résolu (IClaimCheckPreparer) ; S4 (AzureMessagingProvider) → Phase 6 |
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

🧭 **Refactor appliqué par R8 :**

```csharp
public abstract class BaseConsumer<TMessage> : IMessageConsumer<TMessage>
{
    // Délégation pure — interfaces fines (R9 + R8)
    private readonly IMessageDeserializer _deserializer;
    private readonly IMessageSettler     _settler;
    private readonly IConsumerTelemetry  _telemetry;   // IConsumerTelemetry livré R8

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

#### ✅ Violation S3 — **Résolu**

[`Messaging/Producer/Producer.cs`](../EnterpriseMessageTransit/Messaging/Producer/Producer.cs)

La responsabilité Claim Check (R2) a été extraite dans un service dédié `IClaimCheckPreparer` / `ClaimCheckPreparer` :

```csharp
// Avant : Producer<T> orchestrait lui-même l'upload blob, les tokens, les métriques
// Après : délégation propre
public class Producer<TMessage> : BaseMessageTransit<TMessage>, IMessageProducer<TMessage>
{
    private readonly IClaimCheckPreparer _claimCheckPreparer;  // ← R2 sorti
    // R1: Orchestration publish    → PublishAsync, PublishBatchAsync
    // R3: Journal A5               → _journal (délégué à IJournalProvider)
    // R4: Télémétrie OTel          → MessagingActivitySource (cross-cutting)
    // R5: Compensation Blob        → StorageProvider.DeleteAsync (compensation send failure)
}
```

**Changements livrés :**
- `IClaimCheckPreparer` (public interface) + `ClaimCheckPreparer` (internal sealed) extraits dans `Messaging/Producer/`
- `IProducerPatterns` supprimé — `Producer<T>` n'implémente plus qu'une seule interface publique
- `PrepareContextWithTokensAsync` et `NormalizeBlobReference` retirés de `Producer<T>` (~100 lignes supprimées)
- `ClaimCheckPreparer` enregistré en DI (`AddScoped<IClaimCheckPreparer, ClaimCheckPreparer>()`)
- `Producer<T>` : 509 lignes → ~390 lignes

**Responsabilités restantes dans `Producer<T>` :** R1 (orchestration), R3 (journal — déjà délégué), R4 (OTel — cross-cutting), R5 (compensation blob orphelin sur erreur send). Ces 4 responsabilités sont cohérentes dans un orchestrateur de publication.

#### 🟠 Violation S4 — `AzureMessagingProvider` est une god-class — Reportée Phase 6

Voir Lead Review §2.1. Mélange : envoi, batch, request/reply, désérialisation, résolution endpoint, hydratation `Attempt`, propagation traceparent, gestion sender cache, application circuit breaker… **Implémente une interface > 10 méthodes** (cf. §9.5 ISP).

> ⛾ **Décision (2026-05-28) :** la décomposition de `AzureMessagingProvider` en classes distinctes (`AzureMessagePublisher`, `AzureMessageSettler`, etc.) est reportée en **Phase 6**. La scission implique de partager `ServiceBusClient` + `ServiceBusSenderCache` entre plusieurs classes — à coordonner avec l'éventuelle refonte multi-broker.

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

#### ✅ Violation I1 — **Résolu par R3**

[`Messaging/Producer/IMessageProducer.cs`](../EnterpriseMessageTransit/Messaging/Producer/IMessageProducer.cs)

`GetResponseAsync` a été **retiré** de `IMessageProducer<T>` et déplacé dans une interface dédiée `IRequestReplyClient<TRequest, TResponse>`. `IMessageProducer<T>` ne contient plus que les opérations fire-and-forget :

```csharp
// ✅ IMessageProducer<T> — publish uniquement
public interface IMessageProducer<TPayload> where TPayload : class
{
    Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(...);
    Task<IReadOnlyList<string>> PublishBatchAsync(...);
}

// ✅ IRequestReplyClient<TRequest,TResponse> — request/reply séparé (R3)
public interface IRequestReplyClient<TRequest, TResponse>
{
    Task<MessageTransitContext<TResponse>?> GetResponseAsync(...);
}
```

Un client fire-and-forget injecte `IMessageProducer<T>` ; un client request/reply injecte `IRequestReplyClient<TRequest, TResponse>`. Violation ISP éliminée.

#### ✅ Violation I2 — **Résolu par R9**

`IMessagingProvider` décomposée en 5 interfaces fines. Voir [§9.3 O1](#-violation-o1--résolu-par-r9).

#### ✅ Violation I3 — **Résolu (S3 — IProducerPatterns supprimée)**

`IProducerPatterns` a été **supprimée** lors du refactoring S3. `Producer<T>` n'implémente plus que `IMessageProducer<TMessage>`. La logique claim check est maintenant dans `IClaimCheckPreparer` / `ClaimCheckPreparer`.

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

#### ✅ Violation D3 — **Résolu**

[`Configuration/Extensions/ConfigurerProviders.cs`](../EnterpriseMessageTransit/Configuration/Extensions/ConfigurerProviders.cs)

`ConfigureAzureProviders()` accepte un `TokenCredential?` optionnel. En production (Azure Functions / AKS), aucun credential passé → `ManagedIdentityCredential` par défaut (1 round-trip IMDS, sans overhead de chaîne). En développement local, l'appelant passe `new VisualStudioCredential()` :

```csharp
// Production (Azure) — ManagedIdentityCredential par défaut
services.ConfigureAzureProviders();

// Dev local — credential explicite
services.ConfigureAzureProviders(new VisualStudioCredential());
```

Tous les samples EMT utilisent `AddEMTSampleProducerDefaults(config, new VisualStudioCredential())` (R12).

### 9.7 Résumé SOLID — verdict par classe

| Classe / Interface | S | O | L | I | D | Statut |
|---|---|---|---|---|---|---|
| `MessageTransitContext<T>` | 🟠 | 🟢 | 🟢 | 🟢 | 🟢 | Séparer envelope/runtime (Phase 6 future) |
| `Producer<T>` | 🟡 | 🟢 | 🟢 | 🟢 | ✅ | S3 résolu (IClaimCheckPreparer) ; S4 → Phase 6 ; D2 résolu R9 |
| `BaseConsumer<T>` | ✅ | 🟢 | 🟢 | 🟢 | 🟢 | S1 résolu par R8 — délègue settlement/telemetry via interfaces fines |
| `IMessageProducer<T>` | 🟢 | 🟢 | 🟢 | ✅ | 🟢 | I1 résolu par R3 — `IRequestReplyClient<T,R>` séparé |
| `IMessagingProvider` | 🟢 | ✅ | 🟢 | ✅ | 🟢 | O1+I2 résolus par R9 — composite backward-compat sur 5 interfaces fines |
| `AzureMessagingProvider` | 🟠 | 🟢 | 🟢 | ✅ | 🟢 | S4 → Phase 6 ; I résolu R9 (5 interfaces fines via DI) |
| `AzureFunctionMessagingAdapter` | 🟠 | 🟢 | 🟢 | 🟢 | 🟠 | S/D hors scope v1.0 — Consumer AzFunc uniquement (R10) |
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

### 11.2 Lot R2 — Sample Claim Check de référence ✅ Livré

**Origine :** Observation S-1 sur les samples (§7.4).
**Objectif :** combler le trou pédagogique — aucun sample ne démontrait le Claim Check actif.

**Livré — 4 projets dans `Exemples/RAMQ.Samples.Queue.ClaimCheck.*` :**

| Projet | Rôle |
|---|---|
| `.Message` | DTO `RapportMessage` (métadonnées du rapport) |
| `.Worker` | Azure Function HTTP trigger — **3 cas de test couverts** |
| `.Consumer` | Lib `ClaimCheckConsumer : BaseConsumer<RapportMessage>` — Options A et B |
| `.Activator` | Azure Function `ServiceBusTrigger` — reçoit et délègue au Consumer |

**3 cas de test couverts dans le Worker :**
- **CAS 1** — `GET /api/publish-large-json` : payload JSON > 350 Ko → Claim Check automatique (seuil `ClaimCheckThresholdBytes`).
- **CAS 2** — `POST /api/publish-with-attachment` : pièce jointe binaire (PDF, image, etc.) uploadée via `ClaimCheckOptions.WithAttachment(stream, fileName)`.
- **CAS 3** — `GET /api/publish-light` : payload < seuil → message inline, `IsClaimCheckApplied = false` côté consumer.

**2 options de consommation dans le Consumer :**
- **Option A** : récupère `context.GetMessageToken()?.Reference` et le transmet à l'API downstream.
- **Option B** : télécharge inline via `IStorageProvider.DownloadAsync(reference)`.

**Priorité :** ~~🟠 **Majeur**~~ ✅ **Livré**.

### 11.3 Lot R3 — Refonte intégrale du pattern Request/Reply ✅ Livré

**Origine :** [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) — C1, C2, C3, I2-I5, S1-S8.
**Objectif :** rendre le pattern utilisable (**état initial :** sample ne compilait pas, worker crashait au démarrage — voir [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) pour le détail). ✅ **Résolu.**

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
**Risque :** ~~🟠 Moyen~~ ✅ **Breaking change livré** — `IMessageProducer<T>` a bien perdu `GetResponseAsync` (déplacé vers `IRequestReplyClient<T,R>`).
**Priorité :** ~~🔴 **Critique**~~ ✅ **Livré** — sample fonctionnel end-to-end.

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

**Livrables réalisés :**
- `IConsumerTelemetry` (internal) + `AzureConsumerTelemetry` — encapsule les spans OTel + métriques de réception.
- `ConsumeScope` (internal) — gère les activités `messaging.consume` + `messaging.deserialize`.
- `BaseConsumer<T>` : suppression de `protected MessagingProvider` ; délégation via `IMessageReceiver`, `IMessageSettler`, `IMessageDeserializer`, `IMessagingEndpointResolver`, `IConsumerTelemetry`.
- Réduit de ~200 lignes à ~115 lignes ; constructor backward-compatible (accepte toujours `IMessagingProvider`).

> ⚠️ **Note :** L'interface `IConsumerSettlement` et la classe `AzureConsumerSettlement` décrites dans le plan initial n'ont **pas été créées**. Le settlement est délégué directement via `IMessageSettler` (livré par R9), ce qui atteint le même objectif SRP sans couche supplémentaire.

**Critère de sortie :** `BaseConsumer<T>` réduit de ~200 à ~115 lignes ; settlement via `IMessageSettler` + telemetry via `IConsumerTelemetry` — tous deux mockables en test.
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
| **R2** | Sample Claim Check | 1 sem. | 🟠 Majeur | R1 | ✅ **Livré** |
| **R3** | Refonte Request/Reply | 3-4 sem. | 🔴 Critique | R1 | ✅ **Livré** |
| **R4** | Triangle idempotence | 2 sem. | 🟠 Majeur | R1 | 🟡 **Partiel** — `RequiresDuplicateDetection` livré ; sample + guidance MessageId déterministe restants |
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

## 13. Design For Operation — architecture observabilité enterprise

> **Audience :** SRE, Lead technique, développeurs (premier déploiement opérationnel).
> **Pré-requis :** notions de base d'OpenTelemetry et d'Azure Monitor.
> **Objectif de cette section :** présenter l'architecture Design For Operation cible pour EMT, expliquer la différence entre `ILogger` et OpenTelemetry, détailler comment les filtres opèrent à chaque étage du collecteur, et fournir la configuration Azure Monitor concrète pour les premières mises en production RAMQ.

### 13.1 Qu'est-ce que « Design For Operation » dans le contexte EMT ?

> 💡 **Définition simple :** *Design For Operation*, c'est concevoir le logiciel de telle sorte que **l'équipe d'exploitation puisse comprendre, mesurer et corriger un incident en production** sans deviner. Ce n'est pas un sujet d'observabilité technique, c'est une exigence métier : un dossier RAMQ bloqué doit pouvoir être expliqué à un agent CAI dans les 30 minutes.

Concrètement, Design For Operation dans EMT signifie répondre à **5 questions opérationnelles** avec **5 piliers techniques** :

| Question opérationnelle | Pilier EMT | Mécanisme technique |
|---|---|---|
| « **Où en est ce message ?** » | Distributed tracing | `ActivitySource` + W3C `traceparent` propagé |
| « **Combien de messages échouent ?** » | Metrics | `System.Diagnostics.Metrics` + Azure Monitor |
| « **Pourquoi celui-ci a échoué ?** » | Logs structurés | `ILogger` + scopes `BeginScope({MessageId, SessionId})` |
| « **Cette opération a-t-elle été faite, à qui, quand ?** » | Journal A5 (audit légal) | `IJournalProvider` → Azure Table |
| « **Le système est-il sain en ce moment ?** » | Health checks | `ServiceBusHealthCheck` + dashboard temps réel |

Ces 5 piliers ne sont **pas équivalents**. Confondre logs et traces, ou journal et métriques, mène à des incidents impossibles à diagnostiquer. La sous-section §13.4 détaille leurs frontières.

### 13.2 Architecture cible — schéma enterprise

```
                  ┌──────────────────────────────────────────┐
                  │     Code applicatif RAMQ                  │
                  │   (Producer, Consumer, Activity)          │
                  └────────┬──────────┬──────────┬───────────┘
                           │          │          │
              ┌────────────▼┐ ┌───────▼───┐ ┌────▼──────────┐
              │   ILogger    │ │ Activity  │ │ MetricsProv.  │
              │ (logs)       │ │ Source    │ │ (System.      │
              │              │ │ (traces)  │ │  Diagnostics. │
              │              │ │           │ │  Metrics)     │
              └────────┬─────┘ └─────┬─────┘ └────────┬──────┘
                       │             │                │
                       │   ┌─────────┴────────────────┘
                       │   │
                       ▼   ▼                              ┌──────────────────┐
              ┌──────────────────────┐                    │  IJournalProvider│
              │  OpenTelemetry SDK   │                    │  (Azure Table)   │
              │  (Tracer/Meter/Log   │                    │  ← audit légal   │
              │   Providers)         │                    │  ← découplé du   │
              └──────────┬───────────┘                    │    chemin OTel   │
                         │                                └────────┬─────────┘
                         │ Pipeline interne :                       │
                         │  ① Sampler (filtrage statistique)        │
                         │  ② Processor (enrichissement,            │
                         │    redaction PII, filtrage par tag)      │
                         │  ③ Batch Exporter                        │
                         ▼                                          │
              ┌──────────────────────────┐                          │
              │ Azure.Monitor.            │                          │
              │ OpenTelemetry.Exporter   │ ← exporter Microsoft     │
              └──────────┬───────────────┘                          │
                         │ HTTPS / gRPC                              │
                         ▼                                          ▼
              ┌──────────────────────────┐         ┌──────────────────────┐
              │ Application Insights      │         │ Azure Table Storage  │
              │ (façade, workspace-based)│         │  TransitJournal      │
              └──────────┬───────────────┘         │  (rétention 7 ans)   │
                         │                          └──────────┬──────────┘
                         ▼                                     │
              ┌──────────────────────────┐                     │
              │ Log Analytics Workspace  │ ◄─── union KQL ─────┘
              │ (stockage + KQL)         │     (corrélation audit ↔ traces)
              │  - dependencies          │
              │  - customMetrics         │
              │  - traces (logs)         │
              │  - exceptions            │
              └──────────┬───────────────┘
                         │
                         ▼
              ┌──────────────────────────────────────┐
              │  Azure Monitor — usages opérationnels │
              │  • Application Map (graphe sagas)     │
              │  • Live Metrics (latence < 1 s)        │
              │  • Smart Detection (anomalies)         │
              │  • Workbooks (dashboards RAMQ)         │
              │  • Action Groups (alertes PagerDuty)   │
              └──────────────────────────────────────┘
```

🟢 **Décision RAMQ : Azure Monitor (Application Insights workspace-based + Log Analytics Workspace) est le backend cible.** Le choix est définitif pour la v1.0. Pas de Grafana, Jaeger ou Prometheus en production — seulement comme outils dev locaux.

### 13.3 Les composants Azure Monitor à utiliser et pourquoi

> 💡 **Pour un junior — "Azure Monitor" n'est pas un seul produit.** C'est une famille de services qui se complètent. Voici lesquels EMT utilise et lesquels ignorer.

| Service Azure Monitor | Rôle | Utilisé par EMT ? | Justification |
|---|---|---|---|
| **Application Insights (workspace-based)** | Façade SDK + UI (Application Map, Live Metrics, Smart Detection) | ✅ **Oui — pierre angulaire** | Reçoit les exports OTel via `Azure.Monitor.OpenTelemetry.Exporter`. Ne stocke rien (façade). |
| **Log Analytics Workspace** | Stockage + moteur KQL | ✅ **Oui — automatiquement lié à App Insights** | C'est lui qui facture (Go ingérés). C'est lui qu'on requête en KQL. |
| **Azure Monitor Metrics (Platform metrics)** | Métriques de la plateforme Azure (CPU, mémoire VM, queue length SB) | ✅ **Oui — auto-activé** | Compléter les métriques applicatives EMT avec les métriques infra (queue length, dead letter count Service Bus). |
| **Azure Monitor Alerts** | Règles d'alerte sur métriques ou requêtes KQL | ✅ **Oui** | Brancher sur les seuils EMT documentés (cf. [`metrics.md`](../EnterpriseMessageTransit/docs/observability/metrics.md)). |
| **Action Groups** | Routage d'alertes (PagerDuty, Teams, email) | ✅ **Oui** | Standard RAMQ. |
| **Azure Monitor Workbooks** | Dashboards interactifs avec KQL | ✅ **Oui** | Recommandé pour les dashboards opérationnels EMT (voir §13.10). |
| **Azure Dashboards (anciens)** | Dashboards simples | ⚠️ Non — préférer Workbooks | Workbooks sont plus expressifs et versionnables (JSON). |
| **Azure Monitor Agent (AMA)** | Collecte de logs OS depuis VMs / VMSS | ❌ Non nécessaire pour Functions | Auto-activé sur Azure Functions via le runtime. |
| **Diagnostic Settings** | Export de logs de ressources Azure (Service Bus, Storage…) vers Log Analytics | ✅ **Oui — à configurer** | Capture les logs Service Bus côté broker (throttling, dead-lettering serveur). Complète les métriques applicatives EMT. |
| **OpenTelemetry Collector** (Microsoft hébergé ou self-hosted) | Sidecar de routage / filtrage avancé | ⚠️ **Optionnel** | Utile si on veut multi-destination (App Insights + Splunk RAMQ). Non requis en v1.0. |

#### 13.3.1 Configuration Diagnostic Settings Service Bus — souvent oublié

EMT instrumente le **côté applicatif**. Mais Service Bus lui-même émet des logs et métriques opérationnels (throttling, expiration de session, dead-lettering serveur) qui ne sont **pas** visibles côté EMT. Il faut activer les Diagnostic Settings :

```bicep
resource diagSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: serviceBusNamespace
  name: 'sb-to-loganalytics'
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}
```

Sans cette configuration, un message bloqué par throttling Service Bus apparaîtra comme un timeout côté EMT sans cause visible.

### 13.4 ILogger vs OpenTelemetry — la nuance critique

> 💡 **C'est probablement la confusion #1 d'un junior qui débute en observabilité.** Le code écrit `_logger.LogInformation(...)`, mais qu'est-ce qui se passe vraiment ? Est-ce que ça « part » dans Application Insights ? Sous quel nom de table ? Avec quels filtres ? La réponse demande de comprendre 3 couches.

#### 13.4.0 Vision claire — la phrase de référence à mémoriser

> 🧭 **`ILogger` est l'API qu'on écrit. OpenTelemetry est le pipeline qui transporte.** Ce sont **deux outils complémentaires**, pas concurrents.
>
> - On **écrit toujours** du code applicatif avec `ILogger<T>` injecté en DI. C'est l'abstraction Microsoft standard, totalement indépendante du backend.
> - On **configure le transport** vers Azure Monitor une seule fois dans `Program.cs` via OpenTelemetry. À partir de là, **chaque `_logger.LogInformation()` devient automatiquement** un événement OTel qui voyage vers Application Insights, sans que le code applicatif ne le sache.
> - Cette indépendance est **un atout DFO** : on peut tester en local avec `.AddConsoleExporter()`, déployer en prod avec `.AddAzureMonitorLogExporter()`, et le code applicatif **ne change jamais**.

#### 13.4.0.bis Les trois signaux d'observabilité — frontières claires

Avant de plonger dans le pipeline, il faut **distinguer les trois signaux** OpenTelemetry. Beaucoup de juniors croient qu'OpenTelemetry = traces. C'est faux : OTel a 3 piliers, et chacun a un usage précis.

| Signal | API .NET | Question opérationnelle qu'il répond | Exemple dans EMT |
|---|---|---|---|
| **Logs** | `ILogger<T>` | « Pourquoi celui-ci a échoué ? » | `_logger.LogWarning("Journal failed, MessageId={Id}", id)` |
| **Traces** (spans) | `ActivitySource` / `Activity` | « Où en est ce message, combien de temps a pris chaque étape ? » | `using var act = source.StartActivity("messaging.publish")` |
| **Metrics** | `Meter` / `Counter<T>` / `Histogram<T>` | « Combien, à quelle vitesse, à quel taux ? » | `counter.Add(1, new TagList { ... })` |

🟢 **Best practice #1 : choisir le bon signal selon la question.**
- Pour **diagnostiquer un incident précis** → logs structurés avec `MessageId` corrélable.
- Pour **comprendre la latence d'un parcours** → trace distribuée avec spans liés par `traceparent`.
- Pour **alerter sur des taux et des tendances** → métriques avec faible cardinalité.

🟠 **Anti-pattern fréquent à éviter :** émettre une métrique pour chaque erreur métier individuelle. Les métriques agrègent — si on met `MessageId` en tag, la cardinalité explose et Log Analytics refuse l'ingestion (limite : ~1000 valeurs distinctes par tag). **Pour les détails individuels, on utilise les logs.** Les métriques sont pour les **comptages agrégés**.

#### 13.4.0.ter Le déclic à retenir

```
┌─────────────────────────────────────────────────────────────────┐
│ Code applicatif (n'importe quelle classe RAMQ)                  │
│   - Injecte ILogger<T>, ActivitySource, IMetricsProvider        │
│   - Émet logs, spans, metrics SANS savoir où ça va              │
└────────────────────────────────┬────────────────────────────────┘
                                 │ APIs BCL .NET (zéro dépendance OTel)
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ Program.cs — point de wiring UNIQUE                              │
│   builder.Services.AddOpenTelemetry()                            │
│     .WithLogging(...)   ← bridge ILogger → OTel                  │
│     .WithTracing(...)   ← branche ActivitySource → OTel          │
│     .WithMetrics(...)   ← branche Meter → OTel                   │
│     .AddAzureMonitorExporter()  ← destination = Azure Monitor    │
└────────────────────────────────┬────────────────────────────────┘
                                 │ HTTPS export batch
                                 ▼
                       Application Insights / Log Analytics
```

> ✅ **Vision claire RAMQ DFO :** **un seul fichier `Program.cs` configure tout**. Le reste du code applicatif utilise les APIs BCL .NET standard. Migrer demain vers un autre backend (Splunk, Datadog, Grafana Cloud) ne demande **qu'un changement de l'exporter**, pas une réécriture du code métier.

#### 13.4.1 Les trois couches conceptuelles

```
┌────────────────────────────────────────────────────────────────┐
│  COUCHE 1 — Code applicatif                                     │
│  Ce qu'on écrit :                                                │
│    _logger.LogInformation("Saga {Stage} for {DossierId}",       │
│                            stage, dossierId);                    │
└────────────────────────────────┬───────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────┐
│  COUCHE 2 — ILogger pipeline (Microsoft.Extensions.Logging)     │
│  Le message devient un LogRecord structuré :                    │
│    { categoryName: "...Producer",                               │
│      eventId: 0,                                                 │
│      logLevel: Information,                                      │
│      messageTemplate: "Saga {Stage} for {DossierId}",          │
│      attributes: { Stage="Validate", DossierId="D-001" },       │
│      scopes: [ { MessageId="...", SessionId="..." } ] }         │
│                                                                  │
│  Filtres ILogger appliqués ICI (logLevel par catégorie) :      │
│    appsettings.json → "Logging:LogLevel:RAMQ" = "Warning"      │
│    → tout < Warning est jeté AVANT d'aller plus loin           │
└────────────────────────────────┬───────────────────────────────┘
                                 │ ← bridge OpenTelemetry
                                 ▼
┌────────────────────────────────────────────────────────────────┐
│  COUCHE 3 — OpenTelemetry Logs pipeline (via bridge)            │
│  Le LogRecord traverse le pipeline OTel :                       │
│    ① LogProcessor (enrichment : ServiceName, ResourceAttrs)    │
│    ② LogProcessor (redaction PII si configuré)                 │
│    ③ Exporter → Azure.Monitor.OpenTelemetry.Exporter           │
│                                                                  │
│  Filtres OTel appliqués ICI :                                  │
│    .AddOpenTelemetry(o => o.AddProcessor(monFiltreur))         │
│    → un processor peut DROPPER un LogRecord après ILogger     │
└────────────────────────────────┬───────────────────────────────┘
                                 │
                                 ▼ HTTPS
                       Application Insights
                                 │
                                 ▼
                       table `traces` dans Log Analytics
                       (avec customDimensions = scopes + attrs)
```

#### 13.4.2 Les 4 filtres distincts à connaître

Il y a **4 endroits** où un log peut être supprimé entre le code et Azure Monitor. Confondre ces 4 endroits est l'erreur classique :

| # | Filtre | Où | Mécanisme | Exemple |
|---|---|---|---|---|
| **F1** | **ILogger LogLevel par catégorie** | `appsettings.json` ou `host.json` | `"Logging:LogLevel:<Category>": "Warning"` | Filtrer tout `Debug` de la lib EMT mais garder `Information` du code métier |
| **F2** | **ILogger filter functions** | `Program.cs` | `.AddFilter("RAMQ.COM.EnterpriseMessageTransit", LogLevel.Warning)` | Idem F1, par code |
| **F3** | **OTel Sampler (pour traces, pas pour logs)** | `Program.cs` OTel config | `.SetSampler(new TraceIdRatioBasedSampler(0.10))` | Garder 10 % des traces normales, 100 % des erreurs |
| **F4** | **OTel Processor (custom)** | `Program.cs` OTel config | `.AddProcessor(new RedactionProcessor())` | Supprimer un attribut `data:secret=*` avant export |

🟠 **Erreur classique #1 :** activer F3 (Sampler) sur les **traces** en pensant filtrer les **logs**. Le Sampler OTel ne s'applique **qu'aux traces** (spans). Pour les logs, c'est F1, F2, ou F4.

🟠 **Erreur classique #2 :** baisser le LogLevel global à `Warning` (F1) en pensant économiser, et perdre tous les logs `Information` du code métier qui contiennent des `DossierId` utiles aux audits CAI.

#### 13.4.3 Configuration recommandée pour les premières prods RAMQ

```json
// host.json (Azure Function) — recommandation v1.0
{
  "version": "2.0",
  "telemetryMode": "OpenTelemetry",
  "logging": {
    "logLevel": {
      "default": "Information",
      "Host.General": "Warning",
      "Host.Aggregator": "Warning",
      "Microsoft.Azure.WebJobs.Hosting": "Warning",

      // EMT lib : on garde Information pour la traçabilité
      "RAMQ.COM.EnterpriseMessageTransit": "Information",

      // Code métier RAMQ : Information (audits)
      "RAMQ": "Information"
    },
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": false,
        "excludedTypes": "Request;Dependency"
      }
    }
  }
}
```

```csharp
// Program.cs — OpenTelemetry pour Worker / BackgroundService
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "RAMQ.Individu.ValiderAdresse",
        serviceVersion: "1.0.0",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(t => t
        .AddSource("RAMQ.COM.EnterpriseMessageTransit")
        .SetSampler(new PrioritizeErrorsSampler(ratio: 0.10))  // F3 : 10 % nominal, 100 % erreurs
        .AddProcessor(new PiiRedactionProcessor())             // F4 : redaction tags PII
        .AddAzureMonitorTraceExporter())
    .WithMetrics(m => m
        .AddMeter("RAMQ.COM.EnterpriseMessageTransit")
        .AddAzureMonitorMetricExporter())
    .WithLogging(l => l
        .AddProcessor(new PiiRedactionProcessor())             // F4 sur logs aussi
        .AddAzureMonitorLogExporter());
```

#### 13.4.4 Best practices ILogger + OpenTelemetry — checklist quotidienne RAMQ

Ces best practices sont issues de l'audit ligne-par-ligne d'EMT et des conventions Microsoft pour Azure Monitor. Elles sont **opposables en revue de code**.

##### BP-1 : toujours utiliser des **placeholders nommés** dans les messages de log

```csharp
// ❌ MAUVAIS — concatenation, attributs perdus en customDimensions
_logger.LogInformation("Saga " + stage + " for " + dossierId);

// ❌ MAUVAIS — interpolation, idem
_logger.LogInformation($"Saga {stage} for {dossierId}");

// ❌ MAUVAIS — placeholders positionnels, deviennent "arg0", "arg1" en customDim
_logger.LogInformation("Saga {0} for {1}", stage, dossierId);

// ✅ BON — placeholders nommés, deviennent customDimensions.Stage et .DossierId
_logger.LogInformation("Saga {Stage} for {DossierId}", stage, dossierId);
```

**Pourquoi :** seul le format avec placeholders nommés permet la requête KQL `where customDimensions.DossierId == "D-001"`. Sans cela, l'attribut est inaccessible pour les recherches et les jointures.

##### BP-2 : toujours envelopper avec `BeginScope` les blocs qui traitent **une unité de travail**

```csharp
// ✅ BON — tous les logs émis dans le bloc auront automatiquement
//        MessageId, SessionId, CorrelationId en customDimensions
using var scope = _logger.BeginScope(new Dictionary<string, object?>
{
    ["MessageId"]     = context.MessageId,
    ["SessionId"]     = context.SessionId,
    ["CorrelationId"] = context.CorrelationId
});

await ProcessAsync(context, ct);   // tous les LogXxx en interne héritent du scope
```

**Pourquoi :** sans scope, chaque log doit répéter `{MessageId}` dans son template. Avec scope, c'est automatique et impossible à oublier.

🟠 **Trou identifié dans EMT (cf. §13.8 O2/O3) :** la lib EMT elle-même n'utilise pas `BeginScope`. Lot R13 corrige.

##### BP-3 : utiliser des **`LoggerMessage` sources** pour le hot path

Pour les méthodes appelées des milliers de fois par seconde (`SendAsync`, `DeserializeMessageAsync`), le boxing des paramètres lors de l'appel `LogInformation(string, params object[])` coûte cher. Solution : source generator `LoggerMessage`.

```csharp
// ❌ MOINS BON — boxing à chaque appel, ~150 ns
_logger.LogInformation("Send completed for {Entity} in {DurationMs}ms", entity, durationMs);

// ✅ MEILLEUR — zero allocation après génération, ~40 ns
public static partial class LogMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
                   Message = "Send completed for {Entity} in {DurationMs}ms")]
    public static partial void SendCompleted(this ILogger logger, string entity, long durationMs);
}

// Appel :
_logger.SendCompleted(entity, sw.ElapsedMilliseconds);
```

**Pourquoi :** sur un Producer publiant 10 000 msg/s, le gain cumulé est mesurable (et c'est la **pratique recommandée Microsoft** pour les libs performantes). Recommandation pour EMT : migrer progressivement les hot paths (`Producer.PublishCoreAsync`, `RetryPolicyHandler`) vers ce pattern (lot R16 nouveau, priorité 🟡).

##### BP-4 : ne **jamais logger de PII en clair**

```csharp
// ❌ CRITIQUE — viole CAI / loi santé Québec
_logger.LogInformation("Assuré {NAM} validé", assure.NumeroAssuranceMaladie);

// ✅ BON — hash tronqué qui permet la corrélation sans révéler
_logger.LogInformation("Assuré {NamHash} validé", HashTruncate(assure.NumeroAssuranceMaladie));

// ✅ ENCORE MIEUX — identifiant interne non-PII
_logger.LogInformation("Assuré {AssureInternalId} validé", assure.InternalId);
```

**Pourquoi :** une fois en prod, le log devient une ligne dans Log Analytics qui peut être requêtée par tout utilisateur ayant accès au workspace. RGPD/CAI exigent que les PII soient masqués **avant** export. Le `PiiRedactionProcessor` (cf. §13.5.1) est une assurance de dernière ligne, **pas une excuse pour logger en clair**.

##### BP-5 : respecter la **hiérarchie des LogLevel**

| Niveau | Quand l'utiliser | Volume attendu |
|---|---|---|
| `LogTrace` | Diagnostic ultra-détaillé (parsing, boucles internes). **Désactivé en prod.** | Énorme — dev only |
| `LogDebug` | Détails développeur (paramètres reçus, branchements). **Désactivé en prod.** | Élevé — dev only |
| `LogInformation` | Événement métier nominal (« message publié », « saga avancée »). | Modéré — gardé en prod |
| `LogWarning` | Anomalie récupérée (échec journal A5, retry transitoire). | Faible |
| `LogError` | Erreur applicative non récupérable (exception non gérée). | Très faible |
| `LogCritical` | Panne système majeure (DI cassée, config manquante au démarrage). | Très rare |

🟠 **Anti-pattern fréquent :** logger une erreur applicative attendue (ex. validation métier `"Dossier déjà traité"`) en `LogError`. Ces logs polluent les alertes et augmentent le coût Log Analytics. **`LogInformation` ou `LogWarning` sont appropriés**, `LogError` est réservé aux conditions inattendues.

##### BP-6 : laisser le **`traceId` se propager automatiquement**

```csharp
// ✅ Aucun code requis — quand OTel Logs bridge est branché, tout log émis
//   dans une portée Activity capture AUTOMATIQUEMENT traceId et spanId
using var activity = MessagingActivitySource.Source.StartActivity("messaging.publish");
_logger.LogInformation("Publishing {MessageId}", id);
// ↑ Le log a déjà operation_Id = activity.TraceId en customDim
```

**Pourquoi :** c'est la magie du bridge OTel Logs. Le champ `operation_Id` dans Application Insights est rempli automatiquement, ce qui permet la jointure dans toutes les requêtes KQL. Ne **jamais** logger manuellement `TraceId` — c'est redondant et source d'incohérence.

##### BP-7 : émettre une **métrique** chaque fois que vous émettez un log de Warning ou Error récurrent

```csharp
// ✅ Le log donne le détail, la métrique permet l'alerte
catch (ServiceBusException ex) when (IsTransient(ex))
{
    _logger.LogWarning(ex, "Transient SB error on {Entity}", entity);
    _metrics?.IncrementImmediateRetry(entity);   // métrique pour l'alerte
    throw new ImmediateRetryException(...);
}
```

**Pourquoi :** un log ne déclenche pas d'alerte (le requêtage KQL est coûteux). Une métrique avec faible cardinalité est conçue pour ça. **Les deux sont complémentaires** : log = forensic, metric = alerting.

##### BP-8 : structurer les **scopes** comme un arbre

```csharp
// Niveau application (Function host) — scope racine
using var appScope = _logger.BeginScope(new { ServiceName = "RAMQ.Individu" });

// Niveau message (Producer/Consumer) — hérité automatiquement
using var msgScope = _logger.BeginScope(new { MessageId = ctx.MessageId, SessionId = ctx.SessionId });

// Niveau saga step (RoutingSlipExecutor) — hérité automatiquement
using var stepScope = _logger.BeginScope(new { SlipId = slip.Id, StepName = step.Name });

// À ce niveau, TOUT log inclut ServiceName + MessageId + SessionId + SlipId + StepName
_logger.LogInformation("Step started");
```

**Pourquoi :** les scopes s'imbriquent. Plus on descend, plus le contexte est riche. C'est exactement ce qu'il faut pour une saga RAMQ traversant 5 étapes — chaque log à n'importe quel niveau contient toujours `SlipId` pour la jointure.

##### BP-9 : **séparer logs métier et logs techniques**

```csharp
// Logs métier : événements compréhensibles par un agent CAI ou un auditeur
_logger.LogInformation("Dossier {DossierId} validé par {ConsumerName}", id, consumer);

// Logs techniques : détails infrastructure
_logger.LogDebug("Sender cache hit for {Entity}", entity);
```

**Pourquoi :** un agent CAI qui investigue un dossier n'a pas besoin de voir les détails du cache Service Bus. Les logs métier doivent rester **lisibles en langage métier**. Les logs techniques peuvent partir en `LogDebug` (désactivé en prod) ou en `LogTrace`.

##### BP-10 : tester la **catégorie de log** dans les tests unitaires

```csharp
// Pour un nouveau code qui ajoute du logging, vérifier la category :
public class MyClass(ILogger<MyClass> logger)  // ← category = "Namespace.MyClass"

// Dans appsettings.json, on peut alors filtrer précisément :
"Logging:LogLevel:Namespace.MyClass": "Warning"
```

**Pourquoi :** la catégorie de log est le **levier de filtrage F1** (cf. §13.4.2). Si toutes les classes injectent `ILogger<MyDomain>` (au lieu de `ILogger<MyClass>`), on perd la granularité de filtrage. **Toujours injecter `ILogger<ClasseCourante>`**, jamais `ILogger`.

### 13.4.5 Vision DFO — comment ces best practices se traduisent en valeur opérationnelle

| Best practice | Valeur opérationnelle |
|---|---|
| BP-1 (placeholders nommés) | Permet la jointure KQL `traces × dependencies × customMetrics` par `DossierId` → un agent CAI reconstruit un dossier en 5 minutes. |
| BP-2 (BeginScope) | Garantit que **chaque** log contient `MessageId` → aucun log orphelin, traçabilité 100 %. |
| BP-3 (LoggerMessage) | Performance — permet de garder LogInformation en prod sans coût mesurable. |
| BP-4 (no PII) | Conformité CAI/RGPD opposable. Évite incident réputationnel. |
| BP-5 (LogLevel hiérarchie) | Coût Log Analytics maîtrisé. Alertes pertinentes. |
| BP-6 (traceId auto) | Application Map Azure Monitor montre les sagas complètes sans effort. |
| BP-7 (log + metric paire) | Alertes sur taux possibles. Forensic disponible quand l'alerte sonne. |
| BP-8 (scopes en arbre) | Le contexte s'enrichit naturellement le long du parcours saga. |
| BP-9 (logs métier vs technique) | Audit CAI possible sans bruit infrastructure. |
| BP-10 (category granulaire) | Tuning fin du verbosity en prod sans recompilation. |

### 13.5 Stratégie de filtrage à chaque étage — Design For Operation

Le pipeline OTel a **trois étages où on peut filtrer**, chacun avec sa logique. Voici le tableau de décision opérationnel :

| Étage | Type d'objet filtré | Qui décide | Quand l'utiliser |
|---|---|---|---|
| **ILogger LogLevel** (F1, F2) | LogRecord (logs) | Dev → config | Réduire le bruit *avant* qu'il atteigne OTel. Toujours en premier. |
| **OTel Sampler** (F3) | Span (trace) | SRE → config OTel | Réduire le coût de **traces** (Log Analytics) sans perdre les erreurs. Sampling tête (head-based). |
| **OTel Processor** (F4) | LogRecord + Span + Metric | Dev → code | Filtrage fin par tag, redaction PII, enrichissement. Sampling queue (tail-based) custom. |
| **Diagnostic Settings sampling** | Catégorie de log (côté ressource Azure) | Ops Azure → portail | Filtrer les **logs de la plateforme** (Service Bus) avant ingestion Log Analytics. |
| **Log Analytics Data Collection Rules (DCR)** | Tables Log Analytics | Ops Azure → portail | Filtrage final : transformer ou supprimer des rows à l'ingestion. Utile pour la conformité (PII dernière chance). |

#### 13.5.1 Exemple de Processor custom pour la redaction PII

```csharp
public sealed class PiiRedactionProcessor : BaseProcessor<Activity>
{
    private static readonly string[] _piiTags = new[]
    {
        "messaging.message_id",  // peut contenir un identifiant assuré dans certains samples
        "ramq.assure_id",
        "ramq.nam"               // Numéro d'Assurance Maladie
    };

    public override void OnEnd(Activity activity)
    {
        foreach (var tagName in _piiTags)
        {
            var tag = activity.GetTagItem(tagName);
            if (tag is string s && !string.IsNullOrEmpty(s))
            {
                // Hash SHA-256 tronqué — corrélation possible sans révélation
                activity.SetTag(tagName, HashTruncate(s));
            }
        }
    }

    private static string HashTruncate(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).Substring(0, 16);  // 64 bits
    }
}
```

🟢 **Bonne pratique RAMQ :** même si EMT ne véhicule pas de PII dans ses spans par défaut, les **applications RAMQ** peuvent en injecter (ex. `activity.SetTag("ramq.assure_id", ...)`). Le processor est une assurance vie : il opère même si une équipe se trompe dans son code applicatif.

### 13.6 La nuance pratique sur `_logger.LogInformation`

Reprenons l'exemple type :

```csharp
_logger.LogInformation("Saga {Stage} for {DossierId}", stage, dossierId);
```

**Ce qui se passe réellement, étape par étape :**

1. **Création du LogRecord** : ILogger construit un `LogRecord` avec :
   - `categoryName` = nom de la classe qui a injecté `ILogger<T>` (ex. `RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer.Producer`)
   - `level` = `Information`
   - `messageTemplate` = `"Saga {Stage} for {DossierId}"`
   - `attributes` = `{ Stage = "Validate", DossierId = "D-001" }`
   - `scopes` = tous les `BeginScope` actifs (ex. `{ MessageId = "abc", SessionId = "D-001" }`)
   - `traceId` / `spanId` = **automatiquement** capturé de l'`Activity.Current` ambient → c'est ce qui lie le log au span dans Azure Monitor.

2. **Filtres F1/F2** : si `Logging.LogLevel.RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer = "Warning"`, ce log est **jeté ici**.

3. **Bridge OTel** : si OTel Logs est activé (`.WithLogging(...)` dans `Program.cs`), le `LogRecord` est passé au pipeline OTel.

4. **Processors OTel** : enrichissement (ServiceName), redaction PII (F4), filtrage personnalisé.

5. **Exporter** : `Azure.Monitor.OpenTelemetry.Exporter` envoie en batch via HTTPS vers l'endpoint Application Insights.

6. **Ingestion** : Application Insights stocke dans la table `traces` de Log Analytics avec :
   - `timestamp` = horodatage source
   - `message` = template rendu (`"Saga Validate for D-001"`)
   - `severityLevel` = 1 (Information)
   - `customDimensions` = `{ Stage, DossierId, MessageId, SessionId, traceId, spanId, ... }`

7. **Requête KQL** : on peut désormais faire :

```kusto
traces
| where customDimensions.DossierId == "D-001"
| join kind=inner (dependencies | where customDimensions.DossierId == "D-001") on operation_Id
| project timestamp, message, name, duration
| order by timestamp asc
```

→ On obtient le **fil complet** d'un dossier dans toutes les apps RAMQ qui ont l'envoyé/reçu.

> 💡 **Insight clé pour un junior :** un `_logger.LogInformation` **n'est jamais juste un log**. Il devient une ligne dans une base de données KQL géante, requêtable avec des jointures sur les `customDimensions`. C'est pour ça que **les noms de placeholders sont importants** (`{DossierId}` est mieux que `{0}` qui devient `arg0` en attribut). Et c'est pour ça que `BeginScope` est crucial : les attributs du scope sont automatiquement injectés dans **tous** les logs émis dans la portée du scope.

### 13.7 Catalogue des spans, métriques et logs EMT — vue consolidée

#### 13.7.1 Spans (traces)

| Span | Émis par | ActivityKind | Tags clés |
|---|---|---|---|
| `messaging.publish` | `Producer.cs` | Producer | `messaging.system`, `messaging.destination`, `messaging.message_id`, `messaging.session_id` |
| `messaging.send` | `AzureMessagingProvider.cs` | Producer | idem + `exception.*` si erreur |
| `messaging.send.batch` | `AzureMessagingProvider.cs` | Producer | `messaging.batch.message_count` |
| `messaging.consume` | `AzureConsumerTelemetry.cs` (via BaseConsumer) | Consumer | `messaging.correlation_id`, `messaging.consumer`, `messaging.target`, `messaging.action`, `messaging.status_code` |
| `messaging.deserialize` | `AzureConsumerTelemetry.cs` | Consumer | `messaging.claimcheck`, `deserialization.failure_reason` |
| `routing_slip.step` | `RoutingSlipExecutor.cs` | Internal | `slip.id`, `slip.name`, `slip.cursor`, `slip.step.name`, `slip.step.activity` |
| `routing_slip.compensation` | `RoutingSlipExecutor.cs` | Internal | `slip.id`, `slip.compensation.reason` |
| `messaging.claimcheck.upload` | `ClaimCheckPreparer.cs` | Internal | `messaging.claimcheck.size_bytes`, `messaging.claimcheck.reference` |

#### 13.7.2 Métriques

| Métrique | Type | Tags | Usage opérationnel |
|---|---|---|---|
| `messages_sent_total` | Counter | — | Volume |
| `messages_received_total` | Counter | — | Volume |
| `messages_dlq_total` | Counter | `entity`, `reason` | **🔴 Alerte si rate > 5/min** |
| `send_duration_ms` | Histogram | — | Latence p99 |
| `receive_duration_ms` | Histogram | — | Latence p99 |
| `circuit_state` | Gauge | `entity` | **🔴 Alerte si ≥ 1 persistant 60 s** |
| `circuit_transitions_total` | Counter | `entity`, `from`, `to` | Détection patterns dégradation |
| `deserialization_failures_total` | Counter | `reason` | **🟠 Alerte si > 0 avec reason=Malformed** |
| `claim_check_upload_duration_ms` | Histogram | — | Performance Blob |
| `claim_check_download_duration_ms` | Histogram | — | Performance Blob |
| `journal_write_duration_ms` | Histogram | — | Performance Table |
| `duplicate_detected_total` | Counter | `entity` | Idempotence côté broker |
| `active_sessions` | ObservableGauge | — | Saturation Service Bus |
| `cached_senders` | ObservableGauge | — | Efficacité du pool |

> 📂 **Catalogue complet et seuils détaillés :** voir [`metrics.md`](../EnterpriseMessageTransit/docs/observability/metrics.md).

#### 13.7.3 Logs structurés

| Émetteur | Niveau dominant | Scopes attendus |
|---|---|---|
| `Producer.cs` (7 appels) | Information / Warning (échec journal) | `{ MessageId, SessionId }` (depuis Activity) |
| `RoutingSlipExecutor.cs` (13 appels) | Information / Warning / Error | `{ SlipId, StepName, Cursor }` |
| `RetryPolicyHandler.cs` (8 appels) | Information / Warning / Error | `{ MessageId, Attempt, Reason }` |
| `AzureMessagingProvider.cs` (4 appels) | Warning / Error | `{ Entity, MessageId }` |
| `AzureJournalProvider.cs` (4 appels) | Warning si échec | `{ MessageId, CorrelationId }` |
| **Samples Consumer** (19/19) | Information | `{ MessageId, SessionId }` via `Logger.BeginScope` |

🟠 **Trou identifié :** la lib EMT **n'utilise pas elle-même `Logger.BeginScope`** — 0 occurrence dans le code lib. Les scopes ne sont posés que dans les samples. Conséquence : un log lib EMT n'a pas systématiquement `MessageId` en `customDimensions` (sauf si l'application appelante a déjà posé le scope plus haut).

**Recommandation R13** (nouveau lot) : injecter systématiquement `BeginScope({ MessageId, SessionId, CorrelationId })` dans `Producer.PublishCoreAsync` et `BaseConsumer.DeserializeMessageAsync` pour garantir la corrélation cross-app.

### 13.8 Audit ligne par ligne — recommandations stratégiques

Cette sous-section liste les **gaps observabilité ligne par ligne** identifiés dans l'audit (état au 28 mai 2026).

| # | Fichier:Ligne | Constat | Recommandation stratégique | Priorité |
|---|---|---|---|---|
| **O1** | `BaseConsumer.cs` (toutes lignes) | 0 occurrence `_logger.Log*` dans la lib EMT — délègue à `AzureConsumerTelemetry` | ✅ Volontaire (SRP). Garder. | 🟢 OK |
| **O2** | `Producer.cs:173-175` | `LogWarning(jEx, "Journal failed (publish)")` mais **aucun scope** → log orphelin sans `MessageId` en customDim si l'appelant n'a pas posé de scope | Wrapper le bloc `try { await _journal... } catch { _logger.LogWarning }` dans un `using var s = _logger.BeginScope(new { context.MessageId, context.CorrelationId })` | 🟠 Majeur |
| **O3** | `RetryPolicyHandler.cs` (8 logs) | Logs riches mais **aucun scope** | Idem O2 pour chaque appel | 🟠 Majeur |
| **O4** | `AzureMessagingProvider.cs:140-141` | Span `messaging.send` créé mais **pas de propagation `traceparent`** dans `ApplicationProperties` | Implémenter W3C TraceContext propagation (lot R14 nouveau) — déjà documenté comme Phase 3 dans `tracing.md:139` | 🔴 **Critique** |
| **O5** | `RoutingSlipExecutor.cs:122-124` | Span `routing_slip.step` parent du step suivant **non lié** car re-création d'`Activity` à chaque worker | Lier via `parentContext: ActivityContext.Parse(traceparent)` depuis SlipEnvelope.Header.TraceContext | 🔴 **Critique** |
| **O6** | `JsonMessageSerializer.cs` (9 logs) | Logs sur désérialisation mais **niveau LogLevel.Error pour erreurs récupérables** | Repasser à `LogWarning` ; reserver `LogError` aux exceptions inattendues | 🟡 Mineur |
| **O7** | `ClaimCheckPreparer.cs:57-59` | Span `messaging.claimcheck.upload` mais **pas de métrique `claim_check_uploads_total`** alors qu'`IncrementClaimCheckUploads` existe | Ajouter l'appel `_metrics?.IncrementClaimCheckUploads(...)` après upload réussi | 🟡 Mineur |
| **O8** | Samples Consumer (19 fichiers) | Toutes utilisent `BeginScope({MessageId, SessionId})` ✅ | Bonne pratique — exemple à imiter. **Documenter** dans `CONTRIBUTING.md` côté samples. | 🟢 OK |
| **O9** | Samples Activator (Azure Functions) | Aucun n'utilise `Logger.BeginScope` autour de `ConsumeAsync` | Ajouter scope englobant dans chaque Activator pour préserver la corrélation côté Function host | 🟠 Majeur |
| **O10** | `RetryPolicyHandler.cs:447` | `ReferralCount` injecté dans `ApplicationProperties` mais **pas comme tag span** | Ajouter `activity?.SetTag("messaging.referral_count", referralCount)` lors du retry | 🟡 Mineur |
| **O11** | `ServiceBusHealthCheck.cs` | Test connectivité SB ✅ mais ne vérifie pas `RequiresDuplicateDetection = true` | Étendre selon recommandation lot R4 (cf. §11.4) — idempotence triangle | 🟠 Majeur (déjà dans R4) |
| **O12** | Aucun fichier | **Aucun Workbook Azure Monitor pré-livré** dans le repo | Créer `docs/observability/workbooks/emt-overview.workbook.json` (template) | 🟡 Mineur |
| **O13** | Aucun fichier | Aucun template Bicep pour Diagnostic Settings Service Bus | Créer `docs/observability/bicep/diag-servicebus.bicep` (cf. §13.3.1) | 🟠 Majeur |
| **O14** | Aucun sample | Aucun sample n'utilise `PiiRedactionProcessor` ou similaire | Documenter le pattern dans `azure-monitor.md` + sample dédié | 🟡 Mineur |
| **O15** | `Producer.cs:163-175` | Journal A5 écrit **après** `SendAsync` → si journal échoue après envoi, message déjà publié, audit perdu | ✅ Volontaire (pattern A5 explicite). Garder. Compenser via outil de réconciliation périodique (lot R15 nouveau) | 🟡 Mineur |

### 13.9 Plan d'action Design For Operation — nouveaux lots R13-R15

Ajoutés au plan de résolution (§11) :

#### Lot R13 — Scopes `BeginScope` systématiques dans la lib EMT

**Origine :** §13.7.3 + §13.8 O2/O3/O9
**Objectif :** garantir que **tout log émis depuis EMT inclut `MessageId`, `SessionId`, `CorrelationId`** en `customDimensions`, même si l'application appelante n'a pas posé de scope.

**Livrables :**
- Wrapper `using var scope = _logger.BeginScope(...)` dans :
  - `Producer.PublishCoreAsync` (autour du bloc journal + send)
  - `BaseConsumer.DeserializeMessageAsync` (déjà partiellement via Activity, à confirmer)
  - `RetryPolicyHandler` (toutes les méthodes Handle*)
  - `RoutingSlipExecutor.RunAsync`
- Test unitaire qui vérifie que `_logger.LogWarning` émet bien `MessageId` en attributs (via `Microsoft.Extensions.Logging.Testing`).

**Estimation :** 1 semaine. **Priorité :** 🟠 Majeur.

#### Lot R14 — W3C TraceContext propagation Producer → Consumer

**Origine :** §13.8 O4/O5 ; `tracing.md:139` (déjà identifié comme Phase 3)
**Objectif :** lier les spans `messaging.publish` et `messaging.consume` dans **un seul arbre de trace distribuée** via `traceparent` injecté dans `ServiceBusMessage.ApplicationProperties`.

**Livrables :**
1. Dans `AzureMessagingProvider.SendAsync` : injecter `traceparent` (et `tracestate`) dans `ApplicationProperties` **avant** l'envoi.
2. Dans `BaseConsumer.DeserializeMessageAsync` : lire `traceparent` depuis `ApplicationProperties` et créer le span `messaging.consume` avec `parentContext: ActivityContext.Parse(traceparent)`.
3. Idem dans `RoutingSlipExecutor` : propager via `SlipEnvelope.Header.TraceContext` pour lier les étapes saga.
4. Tests d'intégration sur Service Bus Emulator : un trace unique de Producer → Consumer → step suivant.

**Estimation :** 2 semaines. **Priorité :** 🔴 **Critique** — sans cela, l'Application Map d'Azure Monitor montre des arbres déconnectés.

#### Lot R15 — Réconciliation périodique des messages vs journal

**Origine :** §13.8 O15 (pattern A5)
**Objectif :** détecter les messages **publiés mais non journalisés** (échec d'écriture A5 après SendAsync réussi) pour garantir l'auditabilité légale CAI.

**Livrables :**
- Function Timer (1h) qui :
  1. Lit les métriques `messages_sent_total` par entité sur la dernière heure.
  2. Compte les entrées journal pour la même fenêtre.
  3. Si delta > 0.1% → alerte + écriture d'une entrée journal compensatoire.
- Dashboard Workbook montrant cette réconciliation.

**Estimation :** 2 semaines. **Priorité :** 🟠 Majeur (conformité légale).

### 13.10 Workbooks et alertes — livrables opérationnels recommandés

Pour un premier déploiement RAMQ, **3 Workbooks Azure Monitor** sont à pré-livrer dans le repo (sous `docs/observability/workbooks/`) :

#### Workbook 1 — EMT Overview

```
Sections :
  1. Volume (sent / received / dlq)         — Time chart 24h
  2. Latency (p50/p95/p99 send + receive)   — Time chart 24h  
  3. Circuit breaker states                  — Table par entity, état actuel
  4. Error budget (DLQ rate vs SLO)         — Gauge
  5. Saga overview (slips actifs, stuck)    — Table
```

#### Workbook 2 — Saga Trace Explorer

```
Sections :
  1. Saga list (sliding 7j)                  — Picker SlipId
  2. Saga timeline                          — Gantt chart des spans routing_slip.step
  3. Compensations triggered                 — Liste
  4. Logs associés                          — KQL traces | join dependencies
```

#### Workbook 3 — FinOps / Coûts observabilité

```
Sections :
  1. Volume Log Analytics par table         — bar chart (dependencies, customMetrics, traces)
  2. Coût estimé / jour                     — calcul à partir du volume
  3. Sampling effective rate                — comparaison Sampler vs ingéré
  4. Top noisy categories                   — top 10 categoryName par volume
```

#### Alertes minimales — 6 règles à activer en prod RAMQ

| # | Nom | Métrique | Seuil | Sévérité | Action Group |
|---|---|---|---|---|---|
| **A1** | Circuit Breaker Open | `circuit_state` ≥ 1 persistant 60 s | Persistant 60 s | 🔴 Critical | PagerDuty oncall |
| **A2** | DLQ rate spike | `messages_dlq_total` rate > 5/min | 5 min glissantes | 🟠 High | Teams channel |
| **A3** | Deserialization Malformed | `deserialization_failures_total{reason=Malformed}` > 0 | Toute occurrence | 🟠 High | Teams + créateur PR |
| **A4** | Send latency p99 | `send_duration_ms` p99 > 1000 ms | Fenêtre 5 min | 🟡 Medium | Teams |
| **A5** | Claim check slow | `claim_check_download_duration_ms` p99 > 2 s | Fenêtre 5 min | 🟡 Medium | Teams |
| **A6** | Active sessions exhausted | `active_sessions` ≥ `MaxConcurrentSessions` × 0.9 | Fenêtre 1 min | 🟠 High | Teams |

### 13.11 Pour démarrer — checklist Design For Operation pour un nouveau domaine RAMQ

> 💡 **Pour un Lead technique qui démarre l'observabilité sur une nouvelle app RAMQ** — voici les 12 cases à cocher avant d'aller en prod.

| ☑ | Action | Référence |
|---|---|---|
| ☐ | Application Insights workspace-based créé + lié à Log Analytics | §13.3 |
| ☐ | `APPLICATIONINSIGHTS_CONNECTION_STRING` configurée en App Setting | §13.3 |
| ☐ | NuGet `Azure.Monitor.OpenTelemetry.Exporter` ou `.AspNetCore` référencé | `azure-monitor.md:124` |
| ☐ | `.AddSource("RAMQ.COM.EnterpriseMessageTransit")` + `.AddMeter(...)` dans Program.cs | `tracing.md:60` |
| ☐ | `host.json` `logLevel` configuré (cf. §13.4.3) | §13.4.3 |
| ☐ | Diagnostic Settings Service Bus activées vers le même workspace | §13.3.1 |
| ☐ | Sampler `PrioritizeErrorsSampler(0.10)` configuré | §13.4.3 |
| ☐ | `PiiRedactionProcessor` ajouté si l'app injecte des tags PII | §13.5.1 |
| ☐ | 6 alertes minimales (A1-A6) déployées | §13.10 |
| ☐ | 3 Workbooks pré-livrés importés dans le workspace | §13.10 |
| ☐ | `ServiceBusHealthCheck` enregistré dans `/health` endpoint | §13.7.3 |
| ☐ | Runbook équipe rédigé pour les alertes A1-A6 | À faire par équipe |

### 13.12 Plan d'implémentation Design For Operation

> **Objectif :** livrer une plateforme d'observabilité enterprise opérationnelle pour le **premier déploiement RAMQ** en 8-10 semaines, en 4 phases séquencées.
>
> **Hypothèses :**
> - Équipe : 2 développeurs (1 Lead SRE + 1 dev backend) + 1 SRE Azure à mi-temps + 1 architecte en revue.
> - Budget Azure : workspace Log Analytics dédié RAMQ-prod (tier Pay-As-You-Go pour démarrer).
> - Pré-requis : ADR-001 (Service Bus only) signé ; tests EMT verts ; PullRequest CI bloquante en place.

#### 13.12.1 Vue d'ensemble — 4 phases, 8-10 semaines

```
┌────────────────────────────────────────────────────────────────────┐
│ PHASE DFO-1 — Socle infrastructure (2 semaines)                    │
│   Objectif : Azure Monitor opérationnel + premier flux end-to-end  │
│   Livrables : App Insights, Log Analytics, Bicep, exporter NuGet  │
│   Sortie : un sample EMT émet logs+traces+metrics vers Azure Monitor│
└──────────────────────────┬─────────────────────────────────────────┘
                           │ dépend de la décision de workspace
                           ▼
┌────────────────────────────────────────────────────────────────────┐
│ PHASE DFO-2 — Instrumentation correcte (3 semaines)                │
│   Objectif : combler les gaps observabilité dans la lib EMT       │
│   Livrables : Lots R13, R14, R15 (scopes, traceparent, audit A5)  │
│   Sortie : Application Map montre les sagas en arbre complet      │
└──────────────────────────┬─────────────────────────────────────────┘
                           ▼
┌────────────────────────────────────────────────────────────────────┐
│ PHASE DFO-3 — Best practices et conformité (2 semaines)            │
│   Objectif : sampler, processors PII, filtres LogLevel              │
│   Livrables : PiiRedactionProcessor, sampler PrioritizeErrors,     │
│              config templates host.json + Program.cs               │
│   Sortie : conformité CAI/RGPD validée, FinOps sous 5 Go/jour      │
└──────────────────────────┬─────────────────────────────────────────┘
                           ▼
┌────────────────────────────────────────────────────────────────────┐
│ PHASE DFO-4 — Outillage opérationnel (2-3 semaines)                │
│   Objectif : runbooks, dashboards, alertes, formation équipes     │
│   Livrables : 3 Workbooks, 6 alertes, 1 runbook, 1 atelier 2h     │
│   Sortie : équipes RAMQ autonomes sur diagnostic incidents        │
└────────────────────────────────────────────────────────────────────┘
```

#### 13.12.2 Phase DFO-1 — Socle infrastructure (semaines 1-2)

**Objectif :** rendre Azure Monitor opérationnel pour EMT — un sample doit émettre logs + traces + metrics visibles dans le portail Application Insights.

| # | Tâche | Livrable | Effort | Owner | Dépend de |
|---|---|---|---|---|---|
| **D1.1** | Créer le Log Analytics Workspace `ramq-emt-prod` (région Canada Est) | Workspace + retention 90j configurée | 0.5j | SRE Azure | Décision région (Architecte) |
| **D1.2** | Créer l'Application Insights workspace-based lié à D1.1 | App Insights + connection string archivée en Key Vault | 0.5j | SRE Azure | D1.1 |
| **D1.3** | Templates Bicep `infra/monitoring/{workspace,appinsights,diag-servicebus}.bicep` | 3 templates Bicep dans le repo | 2j | Dev backend | D1.1, D1.2 |
| **D1.4** | Activer Diagnostic Settings sur le namespace Service Bus (logs + métriques platform → workspace D1.1) | Bicep `diag-servicebus.bicep` déployé | 0.5j | SRE Azure | D1.1, D1.3 |
| **D1.5** | Ajouter `Azure.Monitor.OpenTelemetry.AspNetCore` (ou `.Exporter`) aux samples cible | NuGet référencé dans 2 samples pilotes | 0.5j | Dev backend | — |
| **D1.6** | Wiring Program.cs OTel (Tracing + Metrics + Logging) dans le 1er sample pilote | `Program.cs` template documenté + sample qui marche en local et en cloud | 1j | Dev backend | D1.5, D1.2 |
| **D1.7** | Smoke test bout-en-bout : publier un message, vérifier que span + log + metric apparaissent dans Application Insights | 1 capture d'écran + checklist verte | 0.5j | Dev backend + Lead SRE | D1.6 |
| **D1.8** | RBAC : configurer rôles `Log Analytics Reader` / `Application Insights Component Contributor` pour les équipes RAMQ | Tableau d'attribution des rôles | 1j | SRE Azure | D1.1 |

**Critères de sortie de DFO-1 :**
- ☐ Application Map affiche un sample EMT et ses dépendances Service Bus
- ☐ Une requête KQL `dependencies | where name == "messaging.publish"` retourne des résultats
- ☐ Une requête KQL `customMetrics | where name == "messages_sent_total"` retourne des résultats
- ☐ Une requête KQL `traces | where customDimensions.MessageId == "<id-réel>"` retourne au moins le log de PublishAsync
- ☐ Bicep templates committés et déployables via pipeline DevOps

**Risques DFO-1 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Quota App Insights atteint en dev (sampling agressif Microsoft) | 🟡 Moyenne | Faible | Tier ingestion à 5 Go/jour gratuit, suffisant en dev/preprod |
| Latence d'ingestion 2-5 min déroute l'équipe | 🟠 Élevée | Faible | Documenter et utiliser Live Metrics pour le retour rapide |
| Connection string fuite dans un git commit | 🔴 Faible | Critique | Pré-commit hook + Key Vault systématique + rotation si fuite |

#### 13.12.3 Phase DFO-2 — Instrumentation correcte (semaines 3-5)

**Objectif :** combler les gaps observabilité de la lib EMT — les 3 lots majeurs R13, R14, R15.

| # | Tâche | Livrable | Effort | Owner | Dépend de |
|---|---|---|---|---|---|
| **D2.1** | **Lot R13** : `BeginScope` systématiques dans `Producer`, `BaseConsumer`, `RetryPolicyHandler`, `RoutingSlipExecutor` | PR avec scopes ajoutés + tests `Microsoft.Extensions.Logging.Testing` | 1 sem | Dev backend | DFO-1 |
| **D2.2** | **Lot R14 partie A** : injection `traceparent` dans `ApplicationProperties` côté `AzureMessagingProvider.SendAsync` + `SendBatchAsync` | PR Producer-side propagation | 4j | Dev backend | DFO-1 |
| **D2.3** | **Lot R14 partie B** : lecture `traceparent` + création span lié dans `BaseConsumer.DeserializeMessageAsync` | PR Consumer-side propagation | 3j | Dev backend | D2.2 |
| **D2.4** | **Lot R14 partie C** : propagation cross-saga via `SlipEnvelope.Header.TraceContext` dans `RoutingSlipExecutor` | PR saga propagation | 3j | Dev backend | D2.3 |
| **D2.5** | Tests d'intégration Service Bus Emulator : un trace unique Producer → Consumer → step suivant visible dans Application Map | Tests dans `EnterpriseMessageTransit.Tests/Integration/` | 3j | Dev backend | D2.4 |
| **D2.6** | **Lot R15** : Function Timer 1h pour réconciliation `messages_sent_total` vs journal A5 + alerte si delta > 0.1 % | Function `ReconciliationFunction` + KQL alert | 1 sem | Lead SRE | DFO-1 + Lot R7 livré |
| **D2.7** | Mise à jour `MessagingActivitySource.cs` documentation XML (semantic conventions OpenTelemetry Messaging 1.24) | PR avec docs + lien vers spec | 1j | Dev backend | — |

**Critères de sortie de DFO-2 :**
- ☐ Application Map RAMQ affiche un parcours saga complet en arbre (Producer → Consumer → step suivant) avec **un seul traceId**
- ☐ Tous les logs `ILogger` émis depuis la lib EMT contiennent `MessageId` en `customDimensions` (test KQL)
- ☐ Function Timer R15 tourne et publie sa métrique `journal_reconciliation_delta`
- ☐ Tests d'intégration Service Bus Emulator passent en CI

**Risques DFO-2 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Breaking change involontaire sur format `traceparent` (binary breaking pour messages en transit) | 🟡 Moyenne | Élevé | Fenêtre dual-format : code consumer lit `traceparent` si présent, ignore si absent. Pas de retrait avant 1 release |
| Effet de bord sur perf hot path | 🟡 Moyenne | Moyen | Bench BenchmarkDotNet avant/après — exigence < 5 % régression |
| Mauvaise propagation = arbres déconnectés (silencieux) | 🟠 Élevée | Élevé | Test d'intégration explicite qui vérifie `parentSpanId` non nul |

#### 13.12.4 Phase DFO-3 — Best practices et conformité (semaines 6-7)

**Objectif :** appliquer les best practices BP-1 à BP-10 systématiquement et garantir la conformité CAI/RGPD.

| # | Tâche | Livrable | Effort | Owner | Dépend de |
|---|---|---|---|---|---|
| **D3.1** | Implémenter `PrioritizeErrorsSampler` (10 % nominal, 100 % erreurs) dans un assembly utilitaire `RAMQ.Observability.Sampling` | Lib + tests unitaires | 2j | Dev backend | DFO-2 |
| **D3.2** | Implémenter `PiiRedactionProcessor` configurable via JSON (liste de tags PII à hasher) | Lib + tests + doc | 3j | Dev backend | DFO-2 |
| **D3.3** | Template `Program.cs` enterprise recommandé (3 variantes : Azure Function, BackgroundService AKS, ASP.NET Core API) | 3 fichiers template dans `docs/observability/templates/` | 2j | Dev backend | D3.1, D3.2 |
| **D3.4** | Template `host.json` enterprise avec `logLevel` recommandé (cf. §13.4.3) | 1 fichier template | 0.5j | Dev backend | — |
| **D3.5** | Migration des hot paths EMT vers `LoggerMessage` source generator (BP-3) — `Producer.PublishCoreAsync` + `RetryPolicyHandler` | PR avec `LogMessages.cs` partial + bench montrant gain | 1 sem | Dev backend | DFO-2 |
| **D3.6** | Audit PII : scanner le code RAMQ pour détecter les logs/spans susceptibles de contenir `NAM`, `AssureId`, `DossierId` | Rapport d'audit + ADR-009 « Catalogue PII RAMQ » | 3j | Lead SRE + Architecte | D3.2 |
| **D3.7** | Validation FinOps : projection volume Log Analytics sur 1 mois prod simulée. Si > 5 Go/jour → activer sampling 10 %, sinon laisser à 100 % | Rapport FinOps + décision sampling documentée | 2j | Lead SRE | DFO-2 + 2 sem de données |
| **D3.8** | CI : analyzer Roslyn maison qui warne si un `_logger.LogXxx` n'utilise pas de placeholders nommés (BP-1) | Analyzer NuGet interne | 3j | Dev backend | — |

**Critères de sortie de DFO-3 :**
- ☐ `PrioritizeErrorsSampler` déployé dans le 1er sample pilote, ratio observé en prod
- ☐ `PiiRedactionProcessor` configuré avec la liste PII RAMQ (ADR-009)
- ☐ 3 templates `Program.cs` publiés dans la doc et utilisés par au moins 1 nouveau projet
- ☐ Bench `LoggerMessage` montre gain ≥ 70 % sur `Producer.PublishCoreAsync`
- ☐ Volume Log Analytics projeté ≤ 5 Go/jour (tier gratuit) sur le 1er déploiement

**Risques DFO-3 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Sampler 10 % cache des bugs rares mais critiques | 🟠 Élevée | Élevé | Toujours 100 % sur erreurs (sampler custom), métriques toujours à 100 % (non sampled) |
| Liste PII RAMQ incomplète → fuite réglementaire | 🟡 Moyenne | Critique | Audit PII confié à un architecte senior + relecture juridique RAMQ |
| Analyzer Roslyn cause faux positifs et frustre l'équipe | 🟡 Moyenne | Faible | Mode `info` initialement, escalade vers `warning` après 2 sprints |

#### 13.12.5 Phase DFO-4 — Outillage opérationnel (semaines 8-10)

**Objectif :** rendre les équipes RAMQ autonomes pour diagnostiquer les incidents — runbook + Workbooks + alertes + formation.

| # | Tâche | Livrable | Effort | Owner | Dépend de |
|---|---|---|---|---|---|
| **D4.1** | Créer le Workbook **EMT Overview** (cf. §13.10) | JSON Workbook committé dans `docs/observability/workbooks/` | 2j | Lead SRE | DFO-2 |
| **D4.2** | Créer le Workbook **Saga Trace Explorer** | JSON Workbook | 3j | Lead SRE | DFO-2 |
| **D4.3** | Créer le Workbook **FinOps observabilité** | JSON Workbook | 1.5j | Lead SRE | DFO-3 |
| **D4.4** | Déployer les 6 alertes minimales A1-A6 (cf. §13.10) via Bicep | Bicep `infra/monitoring/alerts.bicep` | 2j | Lead SRE + SRE Azure | D4.1, A1-A6 seuils confirmés |
| **D4.5** | Configurer les Action Groups : PagerDuty pour 🔴 Critical, Teams pour 🟠 High, email pour 🟡 Medium | Action Groups Bicep + tests | 1j | SRE Azure | D4.4 |
| **D4.6** | Rédiger le runbook `docs/observability/runbooks/emt-incidents.md` : pour chaque alerte A1-A6 → symptômes, requêtes KQL d'investigation, actions correctives | 1 fichier markdown de référence | 1 sem | Lead SRE + Dev backend | DFO-3 |
| **D4.7** | Atelier formation 2h pour les équipes RAMQ : « Diagnostic d'un incident EMT en 15 minutes » avec exercices KQL | Slides + enregistrement + 3 exercices pratiques | 3j | Lead SRE + Architecte | D4.6 |
| **D4.8** | Migration progressive des autres samples vers la nouvelle stack DFO | PR par sample (cadence 1/semaine) | continu | Dev backend | DFO-3 |
| **D4.9** | Smoke test post-déploiement : provoquer 3 incidents simulés (DLQ spike, circuit open, deserialization malformed) et vérifier alertes A2/A1/A3 | Rapport de validation | 2j | Lead SRE | D4.4, D4.5 |

**Critères de sortie de DFO-4 (= prêt pour production) :**
- ☐ 3 Workbooks Azure Monitor importés et utilisés par 2 équipes RAMQ distinctes
- ☐ 6 alertes A1-A6 actives, routées via Action Groups
- ☐ Runbook référencé dans la procédure d'astreinte RAMQ
- ☐ Atelier formation tenu (≥ 80 % participation)
- ☐ Smoke test « 3 incidents simulés » : alerte déclenchée en < 5 min pour chacun

**Risques DFO-4 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Alert fatigue (trop d'alertes → équipe ignore) | 🟠 Élevée | Élevé | Démarrer avec les 6 seules, mesurer le taux de faux positifs avant d'ajouter |
| Adoption des Workbooks lente | 🟡 Moyenne | Moyen | Atelier formation + championing 1-2 développeurs « observability champions » par équipe |
| Runbook incomplet sur incident inédit | 🟡 Moyenne | Moyen | Post-mortem systématique → mise à jour du runbook après chaque incident |

#### 13.12.6 Tableau de synthèse — découpage par sprint

| Sprint | Phase | Lots traités | Effort total | Livrables clés |
|---|---|---|---|---|
| **S1** (sem 1-2) | DFO-1 | Infra Azure Monitor | 6.5j-dev + 3j-SRE | Workspace, App Insights, Bicep, 1 sample qui émet |
| **S2** (sem 3-4) | DFO-2 (partie A) | R13 + R14-A | 1.5 sem-dev | BeginScope partout + traceparent côté Producer |
| **S3** (sem 4-5) | DFO-2 (partie B) | R14-B + R14-C + R15 | 2 sem-dev + 1 sem-SRE | Consumer + saga propagation + reconciliation |
| **S4** (sem 6-7) | DFO-3 | BP-1 à BP-10 | 3 sem-dev + 1 sem-SRE | Sampler, processor PII, templates, audit PII |
| **S5** (sem 8-9) | DFO-4 (partie A) | Workbooks + alertes | 1.5 sem-SRE + 1 sem-dev | 3 Workbooks, 6 alertes, Action Groups |
| **S6** (sem 9-10) | DFO-4 (partie B) | Runbook + formation | 1 sem-SRE + 0.5 sem-architecte | Runbook, atelier, smoke test |

**Effort total agrégé :**
- Dev backend : ~8 semaines/personne
- Lead SRE : ~5 semaines/personne
- SRE Azure : ~2 semaines/personne (mi-temps × 4 sem)
- Architecte : ~1 semaine/personne (revues + audit PII)

**Capacité requise :** 2 devs backend + 1 Lead SRE + 1 SRE Azure mi-temps + 1 Architecte 20 %  = ~16 personnes-semaines sur 10 semaines calendaires.

#### 13.12.7 Dépendances avec les autres lots du plan (§11)

| Lot DFO | Bloqué par | Bloque |
|---|---|---|
| DFO-1 | — | Tous les autres lots DFO |
| DFO-2 (R13/R14) | DFO-1 | DFO-3 (templates utilisent les scopes), DFO-4 (Workbooks utilisent les traces) |
| DFO-3 | DFO-2 | DFO-4 (templates de référence dans le runbook) |
| DFO-4 | DFO-2, DFO-3 | — (livraison finale) |
| R7 (lot §11.7 — métriques manquantes) | — | DFO-2.6 (R15 réconciliation utilise une métrique) |
| R4 (lot §11.4 — triangle idempotence) | DFO-1 | DFO-4.6 (runbook duplicate_detected) |
| R3 (lot §11.3 — R/R) | déjà livré | — |

> 🧭 **Séquencing recommandé :** démarrer DFO-1 dès que possible, en parallèle des lots R1 (tests F6/F10) et R2 (sample Claim Check). Les lots R4 (idempotence triangle) et R7 (métriques manquantes) doivent être livrés **avant ou pendant** DFO-2 pour ne pas devoir refactorer le runbook ensuite.

#### 13.12.8 Plan de gestion du changement — communication équipes RAMQ

| Semaine | Communication | Cible | Format |
|---|---|---|---|
| S1 (kickoff) | « EMT entre dans son ère opérationnelle — DFO en 4 phases » | Toutes équipes RAMQ | Email + 30 min Town Hall |
| S2 | « Comment écrire un log qui aide vraiment (BP-1 à BP-10) » | Devs backend RAMQ | Blog interne + brown bag 30 min |
| S4 | « Premier sample en prod avec Azure Monitor — démo live » | Toutes équipes | Demo Teams 45 min |
| S6 | « Politique PII RAMQ pour les logs et traces » | Toutes équipes + RGPD officer | Doc + atelier 1h |
| S8 | « Comment utiliser les Workbooks pour diagnostiquer » | Devs + Ops | Atelier 2h hands-on |
| S10 | « DFO v1.0 prêt pour prod — qui adopte le mois prochain ? » | Lead techniques équipes | Steering 1h |

#### 13.12.9 Critères de succès post-déploiement (3 mois après DFO-4)

À mesurer 3 mois après la fin de DFO-4, idéalement automatisé via un Workbook KPI.

| KPI | Cible | Méthode de mesure |
|---|---|---|
| **MTTR incident EMT** (Mean Time To Resolution) | < 30 min | Logs incidents équipe astreinte |
| **Pourcentage d'incidents diagnostiqués avec uniquement le runbook** | ≥ 70 % | Post-mortem flag |
| **Coût Log Analytics / mois** | ≤ 200 $ CAD pour 1er déploiement | Facturation Azure |
| **Alertes faux positifs / total alertes** | ≤ 15 % | Post-mortem flag |
| **Application Map montre les sagas complètes** | 100 % des sagas testées | Inspection visuelle hebdo |
| **Taux d'adoption Workbooks** | ≥ 2 équipes / semaine utilisent les Workbooks | Audit log workspace |
| **Conformité PII (aucun NAM en clair en KQL)** | 100 % | Requête KQL automatique hebdo |

#### 13.12.10 Stratégie de roll-out par domaine RAMQ

Une fois DFO v1.0 prêt (fin S10), le déploiement aux différents domaines RAMQ se fait par **vagues** :

| Vague | Domaine pilote | Critère de passage à la vague suivante |
|---|---|---|
| **V1** (mois 1) | 1 domaine pilote (le moins critique — ex. Notification) | 2 semaines sans incident production lié à observabilité |
| **V2** (mois 2-3) | 2-3 domaines de complexité moyenne | MTTR mesuré ≤ 30 min sur 5 incidents successifs |
| **V3** (mois 4-6) | Tous les domaines restants | Pas de retour critique des Lead techniques sur la stack |
| **V4** (mois 6+) | Refactor des apps non-EMT pour utiliser le même stack | Décision steering |

> 💡 **Pour un Lead technique RAMQ :** la check-list §13.11 (« 12 cases à cocher ») est votre point d'entrée pour la vague V1. Suivre l'ordre strict. Le runbook DFO-4 (D4.6) couvre les 6 alertes — toute alerte non couverte demande un post-mortem et une mise à jour du runbook.

### 13.13 Liens vers la doc EMT existante

| Document EMT | Couverture |
|---|---|
| [`docs/observability/tracing.md`](../EnterpriseMessageTransit/docs/observability/tracing.md) | Spans, tags, propagation W3C, requêtes KQL traces |
| [`docs/observability/metrics.md`](../EnterpriseMessageTransit/docs/observability/metrics.md) | Catalogue 18 métriques, seuils alertes, KQL |
| [`docs/observability/azure-monitor.md`](../EnterpriseMessageTransit/docs/observability/azure-monitor.md) | Application Insights workspace-based, FinOps, sampling |
| [`docs/failure-modes.md`](../EnterpriseMessageTransit/docs/failure-modes.md) | Modes de défaillance et runbook |
| [`docs/operational-envelope.md`](../EnterpriseMessageTransit/docs/operational-envelope.md) | SLOs chiffrés, capacité |

---

## 14. Glossaire

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

## 15. Pour aller plus loin

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
8. **Les 31 samples sont la documentation vivante.** Les samples R/R (anciennement cassés) sont désormais fonctionnels (R3). Trous pédagogiques restants : Claim Check actif (R2), tests d'activités (R11).
9. **Phase 6 est entièrement hors scope.** Phase 6 = support multi-broker (Kafka / Confluent / RabbitMQ / CloudEvents). Aucun de ces volets n'est prévu dans cette phase du projet. Seul Azure Service Bus est dans le scope.
10. **Le plan de résolution R1-R12** chiffre 9-12 semaines en parallèle (3-4 devs) pour atteindre une v1.0 stable production-ready, avec critères d'acceptation explicites en [§11.11](#1111-critères-dacceptation-v10-stable).

---

*Document généré le 27 mai 2026 par revue agentique consolidée à partir des sources listées en en-tête + analyse des 35 projets `Exemples/` + audit SOLID ligne-par-ligne sur les classes principales d'EMT. Pour toute question, ouvrir une issue ou contacter l'équipe d'architecture RAMQ.*
