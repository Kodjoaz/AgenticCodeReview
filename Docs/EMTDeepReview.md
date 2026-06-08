# EMT Deep Review — Comprendre Enterprise Message Transit de A à Z

> **Audience cible :** développeur junior arrivant sur le projet RAMQ et devant comprendre la librairie `EnterpriseMessageTransit` (EMT) **et tous les exemples du dossier `Exemples/`** sans pré-requis.
> **Objectif :** synthétiser dans un seul document toutes les revues effectuées sur la librairie (Senior, Lead, Distinguished, Phases 1-5, EMT 1.0, Request/Reply, Routing-Slip), inventorier les **37 projets exemples**, vérifier le respect des **principes SOLID ligne par ligne**, l'intégrité de **chaque pattern enterprise**, et fournir un **plan de résolution chiffré et priorisé**.
> **Sources consolidées :**
> - [EMT-SeniorEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-SeniorEngineerReview.md) — revue ligne-à-ligne du code Producer
> - [EMT-LeadEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-LeadEngineerReview.md) — conception locale, SRP, performance
> - [EMT-DistinguishedEngineerReview.md](../EnterpriseMessageTransit/docs/EMT-DistinguishedEngineerReview.md) — angle plateforme, portabilité, gouvernance
> - [EMT-Review-Phase1.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase1.md) à [Phase5.md](../EnterpriseMessageTransit/docs/EMT-Review-Phase5.md) — feuille de route
> - [architecture-routing-slip.md](../EnterpriseMessageTransit/docs/architecture-routing-slip.md) — refonte saga v2.0
> - [EMT1.0Review.md](EMT1.0Review.md) — revue v0.9.0 (Routing Slip v2.0 livré)
> - [EMT1.0-RequestReply.md](EMT1.0-RequestReply.md) — pattern Request/Reply partiel
> - [Exemples/](../Exemples/) — 37 projets de démonstration
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
    - [6.7 Message Transit Journal — BAM enterprise + Single Pane of Glass](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique)
    - [6.9 Multi-Target Producer — pattern fanout typé](#69-multi-target-producer--pattern-fanout-typé)
7. [Inventaire des exemples — 37 projets dans `Exemples/`](#7-inventaire-des-exemples-31-projets-dans-exemples)
8. [Vérification des patterns enterprise — état actuel](#8-vérification-des-patterns-enterprise)
9. [Audit SOLID — ligne par ligne](#9-audit-solid--ligne-par-ligne)
10. [Récapitulatif des revues — corrigé et restant](#10-récapitulatif-des-revues)
11. [Plan de résolution — priorisé et chiffré](#11-plan-de-résolution)
    - [11.13 Analyse des breaking changes — cas O3](#1113-analyse-des-breaking-changes--cas-o3)
12. [Feuille de route — où va EMT](#12-feuille-de-route)
13. [Design For Operation — architecture observabilité enterprise](#13-design-for-operation--architecture-observabilité-enterprise)
    - [13.4 ILogger vs OpenTelemetry — la nuance critique](#134-ilogger-vs-opentelemetry--la-nuance-critique)
    - [13.4.4 10 best practices ILogger + OTel opposables en revue](#1344-best-practices-ilogger--opentelemetry--checklist-quotidienne-ramq)
    - [13.4.6 Niveaux de log dans le code métier RAMQ — guide opposable](#1346-niveaux-de-log-dans-le-code-métier-ramq--guide-opposable)
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

### 1.5 Stack technique — versions des composants runtime

> **Référence au 28 mai 2026.** Versions utilisées dans la solution `EnterpriseMessageTransit.sln`.

#### Runtime et framework

| Composant | Version utilisée | Notes |
|---|---|---|
| **.NET** | **8.0 LTS** | Cible de toute la solution — support jusqu'en novembre 2026 |
| **EMT Library** (`RAMQ.COM.EnterpriseMessageTransit`) | **0.9.0** | Pre-v1.0 — breaking changes assumés entre sprints |
| **Azure Functions Isolated Worker Runtime** | **v4** | `AzureFunctionsVersion` = v4 dans tous les `.csproj` |
| **Microsoft.NET.Sdk.Functions** | **4.6.0** | SDK de build Azure Functions |

#### Azure Functions Worker (packages d'exécution)

| Package | Version en solution | Rôle |
|---|---|---|
| `Microsoft.Azure.Functions.Worker` | **2.51.0** | Host isolation worker |
| `Microsoft.Azure.Functions.Worker.Sdk` | **2.0.2 – 2.0.7** | Build SDK worker |
| `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` | **5.22.2** | Trigger `[ServiceBusTrigger]`, `ServiceBusReceivedMessage`, sessions |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | **3.1.0 – 3.3.0** | Trigger `[HttpTrigger]`, `HttpRequestData` |
| `Microsoft.Azure.Functions.Worker.Extensions.Timer` | **4.3.1** | Trigger `[TimerTrigger]` |
| `Microsoft.Azure.Functions.Worker.ApplicationInsights` | **2.0.0** | Intégration Application Insights isolated worker |
| `Microsoft.Azure.Functions.Worker.OpenTelemetry` | **1.1.0** | `.UseFunctionsWorkerDefaults()` + OTel |

#### Durable Functions

| Package | Version en solution | Rôle |
|---|---|---|
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | **1.1.7** | SDK Durable Functions v3 pour isolated worker — Orchestrateurs, Activités, Entités |
| `Microsoft.Azure.WebJobs.Extensions.DurableTask` | **2.13.7** | Implémentation sous-jacente du runtime Durable (requis en tant que dépendance indirecte) |

> ℹ️ **Note Durable Functions :** la version **1.1.7** de `Extensions.DurableTask` est l'extension isolated worker. Le runtime Durable lui-même (2.x) est géré via `WebJobs.Extensions.DurableTask`. Ce découplage est intentionnel dans l'architecture isolated worker Azure Functions v4.

#### Azure SDK

| Package | Version en solution | Rôle |
|---|---|---|
| `Azure.Messaging.ServiceBus` | **7.18.1** | Client Service Bus (envoi, réception, sessions, administration) |
| `Azure.Identity` | **1.13.0** | `ManagedIdentityCredential`, `VisualStudioCredential`, `DefaultAzureCredential` |
| `Azure.Data.Tables` | **12.11.0** | Table Storage — journal EMT (`JournalEntry`) |
| `Azure.Storage.Blobs` | **12.25.0** | Blob Storage — Claim Check (`AzureStorageProvider`) |
| `Azure.Monitor.OpenTelemetry.Exporter` | **1.8.0** | Export métriques/traces vers Azure Monitor / Application Insights |

#### Observabilité (OpenTelemetry)

| Package | Version en solution | Rôle |
|---|---|---|
| `OpenTelemetry.Extensions.Hosting` | **1.15.3** | `AddOpenTelemetry()`, `WithMetrics()`, `WithTracing()` |
| ~~`OpenTelemetry.Exporter.OpenTelemetryProtocol`~~ | ~~1.9.0~~ | **Supprimé** — dépendance retirée (vulnérabilité `GHSA-4625-4j76-fww9`). Export via `Azure.Monitor.OpenTelemetry.Exporter` uniquement. |
| `OpenTelemetry.Instrumentation.Http` | **1.9.0** | Instrumentation automatique `HttpClient` |
| `Microsoft.ApplicationInsights.WorkerService` | **2.23.0** | Application Insights pour Worker Service |

#### Divers

| Package | Version en solution | Rôle |
|---|---|---|
| `Refit.HttpClientFactory` | **8.0.0** | Client HTTP typé (TDF Frontend → Producer) |
| `Microsoft.Extensions.Http.Resilience` | **8.10.0** | Politiques de retry sur `HttpClient` |
| ~~`Microsoft.CodeAnalysis.PublicApiAnalyzers`~~ | ~~3.3.4~~ | **Supprimé** — retiré de tous les csproj. |

#### ✅ Points d'attention résolus

| Composant | Problème initial | Résolution |
|---|---|---|
| `Microsoft.Azure.Functions.Worker` | Versions mixtes (2.0.0, 2.1.0, 2.51.0) | ✅ Tous alignés sur **2.51.0** |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.9.0 | Vulnérabilité modérée `GHSA-4625-4j76-fww9` | ✅ **Dépendance supprimée** — export via `Azure.Monitor.OpenTelemetry.Exporter` uniquement |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | Analyseur de surface publique obsolète dans ce contexte | ✅ **Supprimé** de tous les csproj |

#### ⚠️ Prérequis machine de build — Windows Long Paths

> Le projet `RAMQ.Samples.Queue.TDF.Integration.DurableOrchestrator` génère un sous-projet `WorkerExtensions` dont le chemin de sortie dépasse **260 caractères** (limite Windows `MAX_PATH`) :
> ```
> ...\TDF.Integration.DurableOrchestrator\obj\Debug\net8.0\WorkerExtensions\bin\Release\net8.0\
>     runtimes\win\lib\netstandard2.0\System.Security.Cryptography.ProtectedData.dll
> (~265 chars)
> ```
> MSBuild's `Copy` task échoue silencieusement avec `MSB3030: file not found` quand le chemin dépasse `MAX_PATH` et que le support des chemins longs Windows est désactivé.

**Activation requise (une fois par machine, admin) :**
```cmd
reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f
```
Ou via PowerShell :
```powershell
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -Value 1
```

> ✅ Prend effet immédiatement pour les **nouveaux processus** (pas de redémarrage requis). À configurer également sur les agents CI/CD (GitHub Actions, Azure Pipelines) avant `dotnet build`.

**Sur Azure Pipelines / GitHub Actions :** ajouter avant le step `dotnet build` :
```yaml
# GitHub Actions
- name: Enable Windows long paths
  run: |
    reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f
  shell: cmd
```
```yaml
# Azure Pipelines
- script: |
    reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f
  displayName: 'Enable Windows long paths'
```

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
| **Journal (MTJ — BAM)** | **Business Activity Monitoring** stockés en Azure Table, découplé du chemin critique (pattern A5). Système nerveux métier (cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique)). |

---

## 3. Les 9 patterns RAMQ portés par EMT

| # | Pattern | Contrainte RAMQ à l'origine | Pourquoi EMT et pas un SDK générique |
|---|---|---|---|
| **R1** | **Routing Slip** (saga auto-portante) | Cloisonnement RBAC entre domaines — aucun orchestrateur central possible | Itinéraire dans le message, pas dans un service central |
| **R2** | **Claim Check** systématique | Pièces jointes médicales > 256 Ko, rétention légale | Requirement réglementaire déguisé en pattern technique |
| **R3** | **MTJ — Business Activity Monitoring** (BAM enterprise) | Pilotage métier temps réel + auditabilité 7 ans (CAI). Pas un audit log, mais le **système nerveux métier** de la plateforme RAMQ (cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique)) | Pilier stratégique de niveau plateforme, découplé du chemin critique via pattern A5 |
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

#### Patterns de MessageId déterministe — guidance

> 🧭 **Règle fondamentale :** un `MessageId` déterministe doit être **stable, unique et représenter une intention métier précise**. Même payload republié par le même caller dans la même fenêtre de déduplication → même `MessageId` → Service Bus déduplique.

##### Règle 1 — Composer avec les clés métier naturelles

```csharp
// Pattern recommandé : concaténer les identifiants naturels de l'opération
// Format : <domaine>-<entité>-<id>-<action>[-<version>]

// Cas 1 : traitement d'un dossier (1 action par dossier)
var messageId = $"individu-dossier-{dossierId}-valider";

// Cas 2 : action sur un dossier avec version (re-soumettre possible)
var messageId = $"pharmacie-recette-{recetteId}-v{version}-soumettre";

// Cas 3 : événement daté (1 seul envoi par jour)
var messageId = $"regie-rapport-{organismeId}-{DateTime.UtcNow:yyyyMMdd}";
```

##### Règle 2 — Utiliser un hash déterministe quand la clé métier est longue

```csharp
// Quand les identifiants sont trop longs pour une clé lisible,
// utiliser SHA256 tronqué (16 premiers octets = 32 hex chars, collision negligeable)
private static string DeterministicId(params string[] parts)
{
    var raw = string.Join("|", parts);
    var hash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(raw));
    // 16 octets → 32 hex chars → bien en-dessous de la limite Service Bus (128 chars)
    return Convert.ToHexString(hash[..16]).ToLowerInvariant();
}

// Usage
var messageId = DeterministicId("individu", dossierId, "ValiderAdresse", correlationId);
```

##### Règle 3 — Ne jamais utiliser `Guid.NewGuid()` seul comme MessageId dans un flux retriable

```csharp
// ❌ Anti-pattern : un nouveau Guid à chaque appel → zéro protection contre les doublons
var ctx = new MessageTransitContext<MaCommande>
{
    MessageId = Guid.NewGuid().ToString("N"),  // ← Si le caller retente, doublon garanti
    Message   = new MaCommande { ... }
};

// ✅ Correct : MessageId ancré sur les données métier
var ctx = new MessageTransitContext<MaCommande>
{
    MessageId = $"{dossierId}-{actionCode}",   // ← Stable à travers les retries
    Message   = new MaCommande { ... }
};
```

##### Règle 4 — Cas où `Guid.NewGuid()` est acceptable

| Scénario | MessageId déterministe requis ? | Justification |
|---|---|---|
| Publication fire-and-forget sans retry applicatif | ❌ Non | Le caller ne retentera jamais |
| Queue **sans** `RequiresDuplicateDetection` | ❌ Non | Le broker ne déduplique pas |
| Événement de notification (audit, log, monitoring) | ❌ Non | Le doublon est inoffensif |
| Commande métier avec effet de bord (paiement, réservation) | ✅ **Oui** | Un doublon = double paiement |
| Message dans une saga Routing Slip | ✅ **Oui** | Le `SlipId` est le bon `MessageId` de base |

##### Règle 5 — Routing Slip : utiliser `SlipId` comme base

```csharp
// Dans un Routing Slip, chaque message de l'étape suivante doit utiliser
// le SlipId + step pour garantir l'idempotence de l'avancement du curseur.
// RoutingSlipExecutor le fait automatiquement :
var nextCtx = new MessageTransitContext<SlipEnvelope>
{
    MessageId     = $"{envelope.Header.SlipId}-step{nextCursor}",  // déterministe
    CorrelationId = envelope.Header.CorrelationId,
    Message       = nextEnvelope
};
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

#### Sample idempotence — pseudo-code end-to-end

> 💡 **Scénario RAMQ :** un service publie une commande `ValiderAdresse` pour un dossier patient. Le réseau coupe après l'envoi. Le caller retente. Sans idempotence → doublon métier (adresse validée 2 fois). Avec idempotence → Service Bus ignore le 2ème envoi silencieusement.

**Configuration broker (`local.settings.json`)**
```json
"Endpoints": [{
  "Target": "valider-adresse",
  "Endpoint": {
    "EntityName":                    "sbq-individu-valider-adresse",
    "EntityType":                    "Queue",
    "RequiresDuplicateDetection":    true
  }
}]
// Côté Bicep :
// duplicateDetectionHistoryTimeWindow: 'PT10M'  ← fenêtre 10 minutes
// maxDeliveryCount: 10
```

**Worker — 2 envois du même message (simulation retry caller)**
```
// Cas 1 : 1er envoi → MessageId déterministe basé sur les clés métier
MessageId = Hash("D-001" + "ValiderAdresse")   // ex. "d001-valider-7a3f..."
PublishAsync(ctx)   → Service Bus ACCEPTE → Consumer reçoit le message → log: "Adresse validée"

// Cas 2 : timeout réseau, caller retente AVEC LE MÊME MessageId
MessageId = Hash("D-001" + "ValiderAdresse")   // même résultat : "d001-valider-7a3f..."
PublishAsync(ctx)   → Service Bus DÉDUPLIQUE → Consumer NE REÇOIT PAS le message
                    → ✅ 0 doublon, adresse validée une seule fois
```

**Consumer — démontre DeliveryCount = 1 toujours**
```
Consumer reçoit IdempotentCommand { DossierId="D-001", Operation="ValiderAdresse" }
  → Logger.LogInformation("Commande reçue DeliveryCount={Count}", context.Attempt)
  // Même si le Worker a publié 3 fois le même message,
  // ce log n'apparaît qu'UNE seule fois — preuve de la déduplication.
  → CompleteMessageAsync()
```

**Ce que ce sample enseigne :**

| Observation | Explication |
|---|---|
| 2 `PublishAsync` → 1 seul message consommé | Service Bus déduplique par `MessageId` dans la fenêtre configurée |
| `CorrelationId` identique sur les 2 envois | Traçabilité préservée même si le 2ème est ignoré |
| `DeliveryCount = 1` toujours | Confirme que ce n'est pas un retry Service Bus — c'est bien une déduplication côté broker |
| Fast-fail au démarrage si config manquante | `RequiresDuplicateDetection = true` + `ConfigurationException` si broker pas configuré |

### 6.6 Retry, Circuit Breaker et Dead Letter Queue

Trois politiques de retry :

| Politique | Côté | Configuré dans |
|---|---|---|
| `ExponentialRetryPolicy` | Consumer | `AppSettings.RetryPolicy` |
| `ProducerSendRetryPolicy` | Producer | `TransportSettings.SendRetry` |
| `MaxDeliveryCount` | Broker | Azure Service Bus |

`CircuitBreakerManager` (Singleton, **par entité**) : Closed → Open → HalfOpen.

### 6.7 Message Transit Journal (MTJ) — Business Activity Monitoring stratégique

> 🧭 **Décision plateforme RAMQ :** le MTJ n'est **pas** un journal d'audit technique. C'est notre **Business Activity Monitoring (BAM)** — le système nerveux métier qui mesure, alerte et explique le comportement de chaque processus d'intégration RAMQ, en temps réel et sur 7 ans.
>
> Cette décision élève le MTJ au rang de **produit plateforme stratégique**, pas un sidecar facultatif.

#### 6.7.0 Positionnement stratégique — pourquoi le MTJ est notre BAM

> 💡 **Pour un junior — qu'est-ce que le Business Activity Monitoring ?** Le BAM est un concept enterprise (Gartner, Forrester) qui mesure les **événements métier** d'une organisation en temps réel : « combien de dossiers RAMQ ont franchi l'étape de validation aujourd'hui ? », « quel domaine est en retard sur son SLA de 2 minutes ? », « pourquoi le taux de DLQ a doublé ce matin ? ». Le BAM se distingue de l'**APM (Application Performance Monitoring)** par sa focalisation **métier**, pas infrastructure.

##### Les 5 dimensions BAM que le MTJ doit couvrir

| Dimension BAM | Question métier RAMQ | Champ MTJ qui la porte |
|---|---|---|
| **Volume métier** | « Combien de dossiers d'assurance traités aujourd'hui ? » | `Mode=PUBLISH` × `Action="ValiderDossier"` |
| **Latence métier (E2E)** | « Combien de temps entre soumission et notification ? » | `EnqueuedTimeUtc` cross-stages corrélés par `CorrelationId` / `SlipId` |
| **Taux de succès / échec** | « Quel domaine échoue le plus aujourd'hui ? » | `Mode=DLQ` group by `Target` + `DeadLetterReason` |
| **Conformité SLA** | « Le domaine Pharmacie respecte-t-il son SLA de 5 min ? » | Différence `EnqueuedTimeUtc` step N - step 0 par `SlipId` |
| **Anomalies métier** | « Pourquoi 12 % de dossiers stuck après l'étape 3 ? » | Slips publiés sans `ForSlipComplete` correspondant |

##### Différences clefs MTJ-BAM vs APM (Application Insights)

| Aspect | APM (Application Insights / OTel) | BAM (Message Transit Journal) |
|---|---|---|
| **Question principale** | « Le code va-t-il bien ? » | « Le métier va-t-il bien ? » |
| **Rétention** | 30-90 jours (FinOps OTel) | 7 ans (conformité légale RAMQ) |
| **Granularité** | Span technique, latence ms | Événement métier, conformité SLA |
| **Public cible** | SRE, devs backend | Direction, Conformité, Architectes métier |
| **Volume** | Élevé (millions/jour, sampling 10 %) | Modéré (toutes les opérations métier, 100 %) |
| **Source de vérité** | Sampling probabiliste | **Exhaustif, jamais sampled** |
| **Outil de requête** | KQL Application Insights | KQL Log Analytics + Power BI |
| **Coût/Go** | ~2,76 USD/Go (Log Analytics) | ~0,05 CAD/Go (Table Storage) |

🟢 **Insight stratégique :** le MTJ est **complémentaire** à Application Insights, pas concurrent. Application Insights couvre les **incidents techniques** (« mon code a planté »). Le MTJ couvre les **événements métier** (« la pharmacie n'a pas validé ce dossier dans le délai »). Les deux **se joignent par TraceId** (lot R16) pour donner une vision complète à un opérateur.

##### Architecture cible BAM enterprise pour EMT — 4 couches

```
┌───────────────────────────────────────────────────────────────────────────────┐
│  COUCHE 1 — Sources d'événements métier (EMT runtime)                          │
│    Producer · BaseConsumer · RoutingSlipExecutor · RetryPolicyHandler         │
│    → émettent JournalEntry enrichies (R16) via IJournalProvider               │
└────────────────────────────────────┬──────────────────────────────────────────┘
                                     │ pattern A5 — découplé du chemin critique
                                     ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│  COUCHE 2 — Stockage primaire BAM                                              │
│    Azure Table Storage  (table `MessageTransitJournal`)                       │
│      PartitionKey = Target (endpoint logique)                                 │
│      RowKey = horodatage + MessageId + Guid                                   │
│      Rétention : 7 ans (conformité CAI)                                       │
│      Coût : ~0,05 CAD/Go — quasi-gratuit même à 100 Go                        │
└────────────────────────────────────┬──────────────────────────────────────────┘
                                     │ Change Feed Azure Tables (optionnel)
                                     │ OU export batch via Data Factory
                                     ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│  COUCHE 3 — Traitement temps réel BAM (optionnel, Phase 2 BAM)                 │
│    Azure Stream Analytics OU Azure Data Explorer (ADX)                        │
│      → calcul de KPI temps réel (rolling windows)                             │
│      → enrichissement par référentiels métier RAMQ                            │
│      → détection d'anomalies via Azure Anomaly Detector                       │
└────────────────────────────────────┬──────────────────────────────────────────┘
                                     ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│  COUCHE 4 — Consommation BAM                                                   │
│    • Power BI Dashboards (direction métier, conformité)                       │
│    • Azure Monitor Workbooks (SRE, Lead techniques)                           │
│    • Application Insights (jointure technique via TraceId — lot R16)          │
│    • Alertes Action Groups (SLA breach, anomalies métier)                     │
│    • Export juridique (archive 7 ans pour CAI / enquêtes)                     │
└───────────────────────────────────────────────────────────────────────────────┘
```

##### Les outils Azure stratégiques pour le BAM EMT

| Service Azure | Rôle BAM | Quand l'introduire |
|---|---|---|
| **Azure Table Storage** | Stockage primaire 7 ans, source de vérité | ✅ Phase 1 — déjà en place |
| **Log Analytics Workspace** | Requêtes KQL exploratoires (corrélation MTJ ↔ traces) | ✅ Phase 1 BAM — exposer la Table via `externaldata()` ou export Data Factory |
| **Power BI Premium** | Dashboards métier exécutifs (direction RAMQ) | 🟠 Phase 2 BAM — dès qu'on a besoin de KPI partagés à la direction |
| **Azure Data Explorer (ADX)** | Requêtes temps réel haute performance sur des milliards d'événements | 🟡 Phase 3 BAM — au-delà de 10 millions de messages/jour |
| **Azure Stream Analytics** | Agrégations temps réel, détection patterns SLA | 🟡 Phase 3 BAM — alertes < 1 minute sur breach SLA |
| **Azure Anomaly Detector** | Détection automatique de variations anormales par domaine | 🟡 Phase 3 BAM — maturité opérationnelle ≥ 6 mois |
| **Azure Data Factory** | Export Table Storage → Power BI / ADX (planifié) | 🟠 Phase 2 BAM — alimentation initiale Power BI |
| **Azure Purview** | Catalogage et lineage des données BAM (gouvernance) | 🟡 Phase 3 BAM — alignement avec stratégie data RAMQ |

##### KPI BAM cibles pour le premier déploiement RAMQ

| KPI | Définition KQL | Cible métier |
|---|---|---|
| **Throughput dossiers/heure** | `count() by bin(EnqueuedTimeUtc, 1h)` filtré sur `Action=ValidationDossier` | Tendance hebdomadaire |
| **Latence E2E médiane** | `percentile(durationMs, 50)` par `SlipName` | < 5 min pour 95 % des slips |
| **Taux DLQ** | `100.0 * count(Mode=DLQ) / count(Mode=PUBLISH)` par domaine | < 0,1 % en nominal |
| **Slips stuck** | Slips avec `ForPublish` mais sans `ForSlipComplete` dans les 24h | Alerte au-delà de 5 |
| **Conformité SLA par domaine** | `% slips où Duration < SLA` group by Target | ≥ 98 % |
| **Top 5 raisons DLQ semaine** | `count() by DeadLetterReason` order by count desc | Pour priorisation backlog |
| **Distribution par Application Source** | `count() by ApplicationName` | Identifier les apps émettrices et leur poids |

##### Single Pane of Glass — le pont opérationnel ↔ métier

> 🧭 **Concept enterprise central :** le *single pane of glass* est une **vue unifiée** où un opérateur (SRE, agent CAI, Lead technique, direction métier) peut **partir d'un point quelconque** (alerte business, log technique, dossier métier) et **dériver dans toutes les dimensions** sans changer d'outil ni perdre le contexte. C'est ce qui transforme deux systèmes côte-à-côte (APM + BAM) en une **plateforme d'observabilité unique**.

##### L'analogie du tableau de bord automobile

> 💡 **Pour un junior — comprendre le single pane of glass en 30 secondes :**
>
> Imagine que tu conduis une voiture. Sur ton tableau de bord, tu vois en même temps : la vitesse, le niveau d'essence, la température moteur, le voyant ABS, le GPS. **Tu ne descends pas du véhicule pour aller lire chaque jauge sous le capot, dans le réservoir, ou dans la roue.** Tout est devant toi, et un seul coup d'œil suffit.
>
> Le single pane of glass, c'est ça pour l'observabilité de la plateforme RAMQ :
> - **Une seule UI** (Azure Monitor Workbooks) où on voit tout en même temps.
> - **Un même contexte** propagé : la voiture a un VIN unique ; nos messages ont un `TraceId` unique.
> - **Des « voyants » qui parlent à chaque persona** : la direction voit la jauge essence (KPI métier), le mécanicien voit la température moteur (métriques techniques).
> - **Un drill-down d'un clic** : si le voyant ABS s'allume, le mécanicien branche son scanner sur la prise OBD-II sans démonter la voiture. Pour nous : un clic du Workbook business vers l'Application Map technique.

##### Définition précise du single pane of glass appliquée à EMT

Le single pane of glass EMT respecte **3 propriétés non-négociables** :

1. **Convergence visuelle** — Toutes les données d'observabilité (logs, traces, métriques, MTJ-BAM, alertes) sont **accessibles depuis une seule URL** : le portail Azure Monitor. Pas de Grafana à côté, pas de Power BI ailleurs, pas de console séparée pour Service Bus DLQ.

2. **Convergence sémantique** — Tous les événements observés sont **liés par des identifiants communs** (`TraceId`, `SlipId`, `CorrelationId`, `MessageId`). Cela permet la requête transversale : « tous les logs + tous les spans + toutes les entrées BAM ayant `TraceId = X` ».

3. **Convergence narrative** — Pour chaque persona (direction, SRE, agent CAI), il existe **un Workbook unique** qui est son point d'entrée naturel. Le persona n'a pas à connaître la structure technique des données pour les explorer.

> 🧭 **La phrase de référence à mémoriser :** *« Un seul endroit pour entrer, mille chemins pour creuser, zéro contexte perdu. »*

##### Avant / Après — la même investigation, deux mondes

###### Sans single pane of glass (état actuel, 2026-05)

```
   3h12 — alerte SLA breach « domaine Pharmacie »
   ─────────────────────────────────────────────────
   ✉ Email PagerDuty avec un texte vague :
       "Pharmacy domain SLA exceeded — 47 dossiers affected"

   📱 Ouverture Power BI BAM dashboard
   → onglet 1 : voir le KPI breach
   → onglet 2 : voir les 47 DossierId concernés
   → copier-coller des 47 IDs dans un fichier texte

   🖥️ Ouverture Application Insights dans un autre onglet
   → coller chaque DossierId dans la barre de recherche
   → ne trouve rien — DossierId est en customDim, pas indexé
   → essai par CorrelationId — mais le breach n'en donnait pas

   🖥️ Ouverture Azure Portal pour Service Bus
   → cliquer sur la queue Pharmacie → DLQ → voir les messages
   → format MessageId différent, pas relié au DossierId

   🖥️ Ouverture du repo GitHub pour comprendre le code
   → chercher dans quelle classe le SLA est mesuré
   → trouver le code, comprendre le contexte

   ⏱️  3h12 → 6h45  (3h33 d'investigation avant 1ère hypothèse)
   😤  6h45 → équipe nuit transmet à équipe jour faute de pouvoir conclure
```

###### Avec single pane of glass (état cible après lots R13/R14/R16)

```
   3h12 — alerte SLA breach « domaine Pharmacie »
   ─────────────────────────────────────────────────
   📱 Notification Azure Monitor avec lien direct :
       "47 dossiers en SLA breach → ouvrir Workbook 2 (filtré)"

   🖥️ Clic → Workbook 2 « EMT Operational » s'ouvre déjà filtré
   → table en haut : 47 dossiers, colonnes [DossierId, TraceId, StepStuck, Domain]

   → clic sur la 1ʳᵉ ligne (DossierId D-2026-04-0042)
   → Application Map s'affiche en latéral droit, filtré par ce TraceId
   → on voit : Validate ✓ (200ms) → Enrich ✓ (180ms) → Reserve TIMEOUT (30s)

   → clic « voir les exceptions associées »
   → 1 trace remontée : "Pharmacy.WCF.SoapException: Timeout 30s"

   → clic « voir l'entrée MTJ correspondante »
   → entrée BAM : `Mode=DLQ`, `DeadLetterReason="SOAP timeout"`,
                   `StepName="Reserve"`, `Target="pharmacie-soap-adapter"`

   → clic « voir tous les dossiers similaires depuis 1h »
   → 47 résultats, tous avec la même cause racine

   ⏱️  3h12 → 3h27  (15 min d'investigation, cause racine identifiée)
   ✅  3h27 → ouverture ticket WCF Pharmacie, mitigation lancée à 3h35
```

**Différence quantifiée :** **3h33 → 15 min**, soit un facteur **14×** sur le MTTR (Mean Time To Resolution). Voilà pourquoi le single pane of glass n'est pas un « nice to have » — c'est le **multiplicateur de productivité opérationnelle** qui justifie tous les lots DFO.

##### Pourquoi c'est particulièrement critique pour RAMQ

| Contrainte RAMQ | Comment le single pane of glass aide |
|---|---|
| **Cloisonnement des domaines** (assurance, pharmacie, dispensateur, etc.) | Un opérateur RAMQ centralisé peut diagnostiquer un incident traversant 3 domaines sans avoir accès aux 3 cockpits séparés |
| **Conformité CAI** (loi d'accès à l'information) | Un agent CAI reconstruit le parcours d'un dossier en 5 minutes, signe un PDF audit, et peut retracer 7 ans en arrière |
| **WCF SOAP legacy** | Les timeouts WCF sont vus dans le même Workbook que les retries EMT — pas besoin de switcher sur le portail legacy |
| **Multi-équipes (49 implémentations possibles)** | Toutes les équipes voient les mêmes Workbooks, parlent le même vocabulaire (BAM/APM) — pas de divergence |
| **Cycles de release distincts** | Quand une équipe upgrade EMT v1.0 → v1.1, le single pane of glass reste identique — invariant opérationnel |
| **Astreinte 24/7** | Réveillé à 3h du matin, l'opérateur **doit** diagnostiquer en < 30 min — sinon le mandat de santé publique tombe |

##### Les 4 erreurs à éviter quand on construit son single pane of glass

> 🟠 **Erreur #1 — Multiplier les outils en pensant « le meilleur de chaque ».**
> Grafana pour les métriques, Splunk pour les logs, Power BI pour le BAM, Azure Monitor pour les traces. Sur le papier c'est mieux. **En pratique, chaque outil ajouté divise par 2 la vitesse de diagnostic** (latence cognitive de basculement). Un seul outil « bon partout » bat 4 outils excellents séparés.

> 🟠 **Erreur #2 — Oublier que les personas ne sont pas symétriques.**
> Un Lead technique veut commencer par la trace technique. Une direction métier veut commencer par le KPI. Une équipe conformité veut commencer par le dossier individuel. **Construire un seul Workbook pour les 3 = personne ne l'utilise.** Il faut 3 Workbooks avec leurs **points d'entrée propres** + drill-down réciproques.

> 🟠 **Erreur #3 — Croire que la corrélation peut se faire après coup.**
> Si `TraceId` n'est pas injecté à l'origine dans tous les signaux (logs, MTJ, métriques), aucune requête KQL ne pourra le reconstituer. **La propagation des identifiants doit être faite by-design à l'émission**, pas en post-traitement. C'est pour ça que les lots R13/R14/R16 sont des prérequis.

> 🟠 **Erreur #4 — Confondre « beaucoup de données » et « bon single pane of glass ».**
> Un dashboard qui montre 200 graphiques n'est pas un single pane of glass — c'est du bruit. Un single pane of glass montre **les 5-10 éléments qui comptent**, avec drill-down vers le détail. **Moins de signal visible, plus de signal accessible.**

##### Comment savoir si on a réussi son single pane of glass

Le test est binaire : **prendre un opérateur d'astreinte à 3h du matin, lui montrer une alerte, chronométrer.** Trois résultats possibles :

| Diagnostic | Verdict |
|---|---|
| < 15 min — cause racine + impact business identifiés | ✅ Single pane of glass réussi |
| 15-60 min — diagnostic atteint mais avec friction multi-outils | 🟡 En cours — manque drill-down 1 clic |
| > 60 min — l'opérateur doit appeler un dev senior pour comprendre | 🔴 Échec — pas un single pane of glass, juste plusieurs outils |

L'objectif RAMQ pour la v1.0 : **>= 80 % des incidents diagnostiqués en < 30 min, mesuré sur 3 mois post-DFO-4** (cf. KPI §13.12.9).

##### Pourquoi le single pane of glass est non-négociable pour RAMQ

Aujourd'hui, sans single pane of glass, voici ce qui se passe lors d'un incident :

```
   3h du matin — alerte SLA breach « domaine Pharmacie »
   │
   ▼
   Agent astreinte ouvre Power BI dashboard BAM
   → voit 47 dossiers en breach
   → veut comprendre pourquoi
   │
   ▼  ❌ Friction
   Doit basculer manuellement dans Application Insights
   → cherche par CorrelationId (slow scan)
   → 50 traces remontent, lesquelles correspondent aux 47 dossiers ?
   │
   ▼  ❌ Friction
   Doit basculer dans Azure Portal pour voir Service Bus DLQ
   → cherche par MessageId — pas le même format
   │
   ▼  ❌ Friction
   Doit basculer dans le code RAMQ pour comprendre la logique
   │
   ▼  ⏱️  Diagnostic ≈ 2-4 heures
```

Avec single pane of glass (cible) :

```
   3h du matin — alerte SLA breach « domaine Pharmacie » (dans Azure Monitor)
   │
   ▼
   Workbook unique « EMT Operations » dans Azure Monitor
   → clic sur l'alerte → vue agrégée 47 dossiers
   → 1 clic « voir traces » → Application Map filtrée par les TraceId concernés
   → 1 clic « voir KQL MTJ » → timeline complète des slips
   → 1 clic « voir Service Bus DLQ » → messages réels via deep-link Azure Portal
   │
   ▼  ⏱️  Diagnostic ≈ 15 minutes
```

##### Architecture du single pane of glass EMT — 3 surfaces, 1 vérité

```
        ┌──────────────────────────────────────────────────────────┐
        │       AZURE MONITOR — UI unique (Workbooks)               │
        │                                                            │
        │  ┌────────────────────────────────────────────────────┐   │
        │  │ Workbook 1 — EMT Executive (BAM, direction RAMQ)   │   │
        │  │   → KPI métier, conformité SLA, taux de DLQ        │   │
        │  │   → drill-down : 1 clic vers Workbook 2            │   │
        │  └────────────────┬───────────────────────────────────┘   │
        │                   │ TraceId / SlipId / CorrelationId      │
        │  ┌────────────────▼───────────────────────────────────┐   │
        │  │ Workbook 2 — EMT Operational (APM+BAM, SRE/Lead)   │   │
        │  │   → Application Map, traces, métriques, logs       │   │
        │  │   → vues côte-à-côte BAM ↔ APM                     │   │
        │  │   → drill-down : 1 clic vers ressource Azure       │   │
        │  └────────────────┬───────────────────────────────────┘   │
        │                   │ deep-links Azure portal               │
        │  ┌────────────────▼───────────────────────────────────┐   │
        │  │ Workbook 3 — EMT Forensic (CAI, conformité)        │   │
        │  │   → recherche par DossierId / MessageId / SlipId   │   │
        │  │   → reconstruction timeline 7 ans                  │   │
        │  │   → export PDF pour dossier juridique              │   │
        │  └────────────────────────────────────────────────────┘   │
        │                                                            │
        └──────────────────────────────────────────────────────────┘
                                  │
                  ┌───────────────┴────────────────┐
                  ▼                                ▼
    ┌────────────────────────┐    ┌────────────────────────┐
    │ Log Analytics          │    │ Azure Table Storage    │
    │ (APM technique 90 j)   │    │ (BAM métier 7 ans)     │
    │  dependencies          │◄──►│  MessageTransitJournal │
    │  customMetrics         │    │                        │
    │  traces                │    │ ← TraceId/SpanId/SlipId│
    │  exceptions            │    │   permettent jointure  │
    └────────────────────────┘    └────────────────────────┘
                              │
                              ▼
                ┌──────────────────────────┐
                │ Power BI Premium          │
                │ (BAM exécutif, direction) │
                │ ← export Data Factory     │
                └──────────────────────────┘
```

##### Les 3 personas et leur point d'entrée dans le single pane of glass

| Persona | Question d'entrée | Workbook de départ | Drill-down possible |
|---|---|---|---|
| **Direction métier RAMQ** | « Combien de dossiers traités cette semaine par domaine ? » | Workbook 1 (Executive) ou Power BI | Vers Workbook 2 si anomalie détectée |
| **Lead technique / SRE** | « Pourquoi l'alerte A2 (DLQ spike) s'est déclenchée ? » | Workbook 2 (Operational) | Vers Workbook 1 (impact business) ou Workbook 3 (audit) |
| **Agent CAI / Conformité** | « Reconstruire le parcours du dossier D-2026-04-0042 » | Workbook 3 (Forensic) | Vers Application Insights (cause technique) si besoin |

🟢 **Invariant critique :** ces 3 Workbooks **utilisent les mêmes clefs de jointure** (TraceId, SlipId, MessageId, CorrelationId, DossierId). Cela suppose que ces clefs sont **propagées de bout en bout** — c'est précisément la raison d'être des lots R13 (BeginScope), R14 (W3C TraceContext propagation) et R16 (TraceId/SlipId dans MTJ).

##### Comment construire le single pane of glass — décomposition technique

Le single pane of glass repose sur **3 capacités techniques mutuellement renforcées** :

1. **Identifiants partagés** entre APM et BAM
   - `TraceId` (W3C, 32 hex) — généré par OTel SDK, propagé via lot R14, persisté en MTJ par lot R16
   - `SlipId` (Guid) — généré par `RoutingSlipBuilder`, propagé via `SlipHeader`, persisté en MTJ par lot R16
   - `CorrelationId` (Guid) — immuable, survit aux retries, présent partout depuis Phase 1
   - `MessageId` (Guid) — peut être régénéré sur retry exponentiel no-session (donc moins fiable)
   - `DossierId` / `AssureId` (PII !) — métier, transitent via `Variables` (hash recommandé en `customDimensions`, valeur claire en MTJ seulement)

2. **Jointures KQL natives** entre Log Analytics et Azure Table
   - Pattern `externaldata()` pour accéder à Azure Table Storage depuis KQL
   - Lookup tables MaterializedView pour les KPI les plus fréquents
   - Pré-calculs Stream Analytics pour les vues temps réel

3. **Workbooks Azure Monitor avec deep-links**
   - Liens vers spécifique trace dans Application Map (`#blade/AppInsightsExtension/TraceDetailBlade`)
   - Liens vers entité Service Bus DLQ (`#blade/Microsoft_Azure_ServiceBus/...`)
   - Liens vers ressource Azure Function / Worker (logs en direct)
   - Liens vers le Workbook adjacent (drill-down avec paramètres préfillés)

##### Requête KQL emblématique du single pane of glass

```kusto
// "Donne-moi tout sur ce dossier — APM + BAM en une requête"
let dossierId = "D-2026-04-0042";

// BAM : timeline business complète depuis Azure Table
let bam = externaldata(TraceId:string, SlipId:string, SlipName:string,
                      StepIndex:int, StepName:string, StepStatus:string,
                      EnqueuedTimeUtc:datetime, Mode:string, StatusCode:int,
                      Target:string, ApplicationName:string,
                      Consumer:string, Action:string)
    [@"https://ramq.table.core.windows.net/MessageTransitJournal?$filter=..."]
        with(format='csv');

// APM : tous les spans et logs ayant ce dossier en customDimensions
let apm = union dependencies, traces, exceptions
    | where tostring(customDimensions["DossierId"]) == dossierId
            or tostring(customDimensions["dossier_id"]) == dossierId
    | project timestamp, itemType, name, message, duration, operation_Id, customDimensions;

// Union — UNE SEULE TIMELINE pour le single pane of glass
bam
| project EnqueuedTimeUtc, source="BAM", name=strcat(SlipName, ".", StepName),
          status=StepStatus, mode=Mode, statusCode=StatusCode,
          operation_Id=TraceId, target=Target
| union (
    apm
    | project EnqueuedTimeUtc=timestamp, source="APM", name, status=tostring(itemType),
              mode="", statusCode=0, operation_Id, target=""
)
| order by EnqueuedTimeUtc asc
| project EnqueuedTimeUtc, source, name, status, target, operation_Id
```

**Résultat :** une seule ligne temporelle qui mélange événements BAM (étapes saga) et APM (spans + erreurs). Pour un agent CAI : « 2026-04-12 09:34 → step ValiderAdmissibilite Completed, 2026-04-12 09:35 → exception NamException, 2026-04-12 09:36 → step EnrichirDonnees Faulted ». **Diagnostic en 30 secondes**.

##### Trois règles d'or du single pane of glass RAMQ

> 🟢 **Règle #1 — Une seule URL d'entrée pour chaque persona.**
> Workbook 1 pour la direction, Workbook 2 pour les SRE/Lead, Workbook 3 pour la conformité. Pas de « ouvrir 5 onglets ». Si un persona doit aller dans 2 outils, on a échoué.

> 🟢 **Règle #2 — TraceId partout, sans exception.**
> Toute donnée d'observabilité (log, span, métrique, entrée MTJ) doit contenir `TraceId`. C'est la clef de jointure unique. Lots R13/R14/R16 sont **non-négociables**.

> 🟢 **Règle #3 — Drill-down toujours en 1 clic.**
> Un opérateur ne devrait jamais avoir à copier/coller un ID entre deux outils. Tous les liens cross-workbook et cross-portail doivent être pré-construits avec paramètres URL.

##### Pourquoi cette vision est **stratégique** pour RAMQ

> 💡 **Une plateforme d'intégration sans BAM est une boîte noire métier.** EMT v0.x était une lib technique. EMT v1.0 + MTJ-BAM devient un **outil de pilotage métier** :
>
> - **La direction RAMQ** sait combien de dossiers ont été traités par domaine, en temps réel.
> - **L'équipe Conformité** peut prouver qu'un dossier a été audité selon la procédure, en quelques requêtes.
> - **Les architectes métier** identifient les goulots d'étranglement entre domaines avant qu'ils ne deviennent des incidents.
> - **Les Lead techniques** corrèlent un incident technique (Application Insights, trace) à son **impact métier** (MTJ, nombre de dossiers affectés) en une seule requête.
> - **Les agents CAI** reconstruisent le parcours d'un dossier en 5 minutes, pas 5 jours.

C'est cette vision qui justifie l'investissement R16 — ce n'est pas un refactor d'audit log, c'est la **construction du système nerveux métier de la plateforme d'intégration RAMQ**.

#### 6.7.1 État actuel — ce que le journal capture

**Interface :** [`IJournalProvider.cs:8-18`](../EnterpriseMessageTransit/Messaging/Providers/IJournalProvider.cs)

```csharp
public interface IJournalProvider
{
    Task WriteRecordAsync(JournalEntry entry, CancellationToken ct = default);
    Task WriteBatchAsync(IEnumerable<JournalEntry> entries, CancellationToken ct = default);
}
```

**Record `JournalEntry`** ([`JournalEntry.cs:9-23`](../EnterpriseMessageTransit/Messaging/Providers/JournalEntry.cs)) — **13 champs** :

| Champ | Type | Source | Usage |
|---|---|---|---|
| `Consumer` | string | propriétés du message | Routage métier RAMQ |
| `Action` | string | propriétés du message | Routage métier RAMQ |
| `MessageId` | string | `MessageTransitContext.MessageId` | Identifiant unique du message |
| `CorrelationId` | string | `MessageTransitContext.CorrelationId` (immuable) | Corrélation bout-en-bout cross-retries |
| `Target` | string | EndpointResolver | Endpoint logique destinataire |
| `Mode` | enum `OperationMode` | factory utilisée | `PUBLISH` / `RETRY` / `DLQ` / `REQUEST_REPLY` |
| `StatusCode` | int | factory + contexte | HTTP-like : 200 OK, 410 DLQ, 429 retry |
| `DeliveryCount` | int | broker | Nombre de tentatives de livraison |
| `MaxDeliveryCount` | int | config consumer | Plafond avant DLQ automatique |
| `DeadLetterReason` | string | exception ou raison | Renseigné en DLQ uniquement |
| `EnqueuedTimeUtc` | DateTime | broker | Horodatage côté Service Bus |
| `DeadLetterSource` | string? | broker | « UserError », « MaxDeliveryCountExceeded »… |
| `SessionId` | string? | contexte | Si entité session-activée |
| `ApplicationName` | string? | `AppSettings` | Nom de l'app émettrice |

**Stockage :** Azure Table Storage, table `AppSettings.MessageTransitJournalName`.

**Stratégie d'indexation** ([`AzureJournalProvider.cs:112-113`](../EnterpriseMessageTransit/Messaging/Providers/Azure/AzureJournalProvider.cs)) :
- `PartitionKey = Target ?? "(none)"` — une partition par endpoint logique
- `RowKey = {timestamp:yyyyMMddHHmmssfff}-{MessageId}-{Guid:N}` — unicité absolue, tri chronologique par scan

**Batch optimisé :** [`AzureJournalProvider.cs:76-105`](../EnterpriseMessageTransit/Messaging/Providers/Azure/AzureJournalProvider.cs) utilise `SubmitTransactionAsync` (max 100 entités par batch, groupées par PartitionKey) — **transactionnel par batch**, pas O(n) séquentiel.

**Factory methods statiques** (4) :
- `JournalEntry.ForPublish(...)` : StatusCode=200, Mode=PUBLISH, DeliveryCount=1
- `JournalEntry.ForRetry(...)` : StatusCode=429, Mode=RETRY
- `JournalEntry.ForDLQ(...)` : StatusCode=410, Mode=DLQ
- `JournalEntry.ForRequestReply(...)` : StatusCode personnalisé, Mode=REQUEST_REPLY

#### 6.7.2 Pattern A5 — découplage critique

> 🧭 **Le pattern A5 est une décision de design consciente, pas un bug.**

Quand `Producer.PublishCoreAsync` appelle `_journal.WriteRecordAsync(...)` après un `SendAsync` réussi, le bloc est wrappé dans un `try/catch` :

```csharp
// Producer.cs:170-175 — pattern A5
try
{
    await _journal.WriteRecordAsync(journalEntry, effectiveCt);
}
catch (Exception jEx)
{
    _logger.LogWarning(jEx, "Journal failed (publish) — message sent but not journalized");
    // ↑ on N'EST PAS responsable de la disponibilité du journal côté chemin critique
}
```

**Justification :** si Azure Table est indisponible 5 minutes, on préfère **continuer à envoyer les messages** (système reste utile) quitte à perdre 5 minutes d'audit, plutôt que de tout bloquer. La conformité audit est un **nice-to-have à la seconde près**, pas un **must-have à la seconde près**.

🟡 **Note :** réconciliation périodique entre `messages_sent_total` et entrées journal (lot R15) — **hors scope**.

#### 6.7.3 Trous comblés (R16) — l'audit ligne-par-ligne

Les **3 trous structurels** du MTJ ont été résolus par le lot R16 :

##### T1 — ✅ RoutingSlipExecutor journalise chaque étape (R16-D)

Avant R16, aucune entrée journal n'était écrite pour les étapes Routing Slip. Pour une saga RAMQ à 5 étapes :

```
Activator                  → Producer.PublishAsync()  → 1 entrée journal ✅
Worker step 1 (Validate)   → RoutingSlipExecutor      → 0 entrée journal ❌
Worker step 2 (Enrich)     → RoutingSlipExecutor      → 0 entrée journal ❌
Worker step 3 (Reserve)    → RoutingSlipExecutor      → 0 entrée journal ❌
Worker step 4 (Notify)     → RoutingSlipExecutor      → 0 entrée journal ❌
                                                       ───────────────────
                                                       TOTAL : 1 entrée
```

**Conséquence pour l'auditabilité CAI :**
- Impossible de savoir si l'étape « Reserve » a été franchie avec succès
- Impossible de répondre à « Quand le DossierId D-001 a-t-il été validé ? »
- Impossible de reconstruire la timeline d'un slip via le journal seul

✅ **Résolu R16-D :** `RoutingSlipExecutor<TArgs>` injecte désormais `IJournalProvider` (optional) et écrit `ForSlipStep(Completed)` après chaque `Next()`, `ForSlipStep(Faulted)` + `ForSlipCompensation` sur `Fault`, `ForSlipComplete` sur le dernier step. Pattern A5 : `SafeJournalAsync` — un échec journal ne propage jamais.

##### T2 — ✅ `TraceId` / `SpanId` dans `JournalEntry` (R16-A + R16-B)

Avant R16 : corrélation journal ↔ Application Insights impossible sans jointure manuelle par `CorrelationId`.

Aujourd'hui, pour relier une entrée journal à un span Application Insights, l'opérateur **doit faire une jointure par `CorrelationId`** :

```kusto
// requête actuelle — joindre journal et trace
let journal = externaldata(...)["MessageTransitJournal"];
journal
| join kind=inner (dependencies | where name == "messaging.publish")
    on $left.CorrelationId == $right.customDimensions.CorrelationId
```

**Limites :**
- `CorrelationId` n'est **pas** indexé dans Log Analytics (slow)
- Plusieurs spans peuvent avoir le même `CorrelationId` (retries) → match ambigu
- Demande à l'opérateur de connaître la structure exacte des deux schémas

**Avec `TraceId` natif dans `JournalEntry` :**

```kusto
journal
| join kind=inner dependencies on $left.TraceId == $right.operation_Id
// ↑ jointure O(1) sur index natif Log Analytics
```

✅ **Résolu R16-A + R16-B :** `JournalEntry` expose `TraceId`, `SpanId`, `ParentSpanId`. Toutes les factory methods (`ForPublish`, `ForRetry`, `ForDLQ`, `ForSlipStep`…) injectent `Activity.Current` automatiquement. `AzureJournalProvider.BuildEntity` persiste ces champs en Azure Table.

##### T3 — ✅ `SlipId` dans `JournalEntry` (R16-A)

Avant R16 : impossible de reconstruire la timeline d'un slip sans requête croisée Application Insights + Table Journal.

Le `SlipId` est porté par `SlipHeader` (dans le payload `SlipEnvelope`), mais **n'est pas extrait** vers `JournalEntry`. Un agent CAI qui cherche « tout ce qui s'est passé pour le slip `f3a8-...` » doit :
1. Aller dans Application Insights pour les traces (qui ont `slip.id` en tag span)
2. Aller dans la Table Journal pour l'audit légal
3. Faire la corrélation manuellement par `CorrelationId`

✅ **Résolu R16-A :** `JournalEntry` expose `SlipId`, `SlipName`, `StepIndex`, `StepName`, `StepStatus`. Les factory methods `ForSlipStep`, `ForSlipCompensation`, `ForSlipComplete` peuplent ces champs. Une seule requête KQL sur la Table Journal reconstruit la timeline complète d'un slip.

#### 6.7.4 Vision cible — `JournalEntry` enrichi

```csharp
public sealed record JournalEntry
{
    // === Identifiants métier (existants) ===
    public string Consumer        { get; init; }
    public string Action          { get; init; }
    public string MessageId       { get; init; }
    public string CorrelationId   { get; init; }
    public string Target          { get; init; }

    // === Traçabilité Design For Operation (nouveaux — lot R16) ===
    public string? TraceId        { get; init; }   // ← injecté depuis Activity.Current?.TraceId
    public string? SpanId         { get; init; }   // ← idem pour l'opération courante
    public string? ParentSpanId   { get; init; }   // ← pour saga : span du worker précédent

    // === Routing Slip (nouveaux — lot R16) ===
    public string? SlipId         { get; init; }   // ← depuis SlipEnvelope.Header.SlipId
    public string? SlipName       { get; init; }   // ← workflow logique (ex. "TraiterDossier")
    public int?    StepIndex      { get; init; }   // ← cursor courant
    public string? StepName       { get; init; }   // ← nom de l'étape franchie
    public SlipStepStatus? StepStatus { get; init; }  // ← Completed / Faulted / Compensated

    // === Résultat opération (existants) ===
    public OperationMode Mode     { get; init; }
    public int  StatusCode        { get; init; }
    public int  DeliveryCount     { get; init; }
    public int  MaxDeliveryCount  { get; init; }
    public string? DeadLetterReason   { get; init; }
    public string? DeadLetterSource   { get; init; }

    // === Contexte (existants) ===
    public DateTime EnqueuedTimeUtc   { get; init; }
    public string?  SessionId         { get; init; }
    public string?  ApplicationName   { get; init; }
}
```

#### 6.7.5 Intégration MTJ ↔ Routing Slip — design cible

Le `RoutingSlipExecutor` doit injecter **une entrée journal par étape franchie**. Voici le pattern proposé pour le lot R16 :

```csharp
// RoutingSlipExecutor.cs — version cible (pseudocode)
internal sealed class RoutingSlipExecutor<TArgs> : IRoutingSlipExecutor
{
    private readonly IJournalProvider _journal;   // ← NOUVELLE dépendance DI

    public async Task ExecuteAsync(IMessagingProvider provider, CancellationToken ct)
    {
        var envelope = /* ... désérialiser le SlipEnvelope ... */;
        var currentStep = envelope.Steps[envelope.Cursor];

        using var activity = MessagingActivitySource.Source.StartActivity(
            "routing_slip.step",
            ActivityKind.Internal);
        activity?.SetTag("slip.id", envelope.Header.SlipId);
        activity?.SetTag("slip.name", envelope.Header.SlipName);
        activity?.SetTag("slip.cursor", envelope.Cursor);
        activity?.SetTag("slip.step.name", currentStep.Name);

        ActivityResult result;
        try
        {
            result = await activity.ExecuteAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            await JournalSlipStepAsync(envelope, currentStep, status: Faulted, ex, ct);
            throw;
        }

        // ✅ Journalisation systématique de chaque step
        await JournalSlipStepAsync(envelope, currentStep,
            status: result is NextResult ? Completed : (result is FaultResult ? Faulted : ...),
            exception: null, ct);

        // Routing vers le step suivant ou Complete...
    }

    private async Task JournalSlipStepAsync(
        SlipEnvelope envelope, SlipStep step, SlipStepStatus status,
        Exception? ex, CancellationToken ct)
    {
        var entry = JournalEntry.ForSlipStep(    // ← NOUVELLE factory
            messageId:        envelope.Header.SlipId,    // step n'a pas son propre MessageId
            correlationId:    envelope.Header.CorrelationId,
            traceId:          Activity.Current?.TraceId.ToString(),
            spanId:           Activity.Current?.SpanId.ToString(),
            parentSpanId:     Activity.Current?.ParentSpanId.ToString(),
            slipId:           envelope.Header.SlipId,
            slipName:         envelope.Header.SlipName,
            stepIndex:        envelope.Cursor,
            stepName:         step.Name,
            stepStatus:       status,
            target:           step.EntityName,
            deadLetterReason: ex?.Message,
            applicationName:  /* ... */);

        try { await _journal.WriteRecordAsync(entry, ct); }
        catch (Exception jEx) { _logger.LogWarning(jEx, "Journal step failed"); }  // A5
    }
}
```

**Conséquence pour une saga 5 steps :**

```
Activator                  → 1 entrée ForPublish     (SlipName="TraiterDossier", Cursor=0, Status=Active)
Worker step 1 (Validate)   → 1 entrée ForSlipStep    (Cursor=1, StepName="Validate", Status=Completed)
Worker step 2 (Enrich)     → 1 entrée ForSlipStep    (Cursor=2, StepName="Enrich", Status=Completed)
Worker step 3 (Reserve)    → 1 entrée ForSlipStep    (Cursor=3, StepName="Reserve", Status=Faulted!)
Compensation Validate      → 1 entrée ForSlipStep    (Cursor=1, StepName="Validate", Status=Compensated)
                             ─────────────────────────────────────────────────────────────────
                             5 entrées — timeline complète reconstructible
```

**Requête KQL après enrichissement :**

```kusto
externaldata(...)["MessageTransitJournal"]
| where SlipId == "f3a8-..."
| order by EnqueuedTimeUtc asc
| project EnqueuedTimeUtc, StepIndex, StepName, StepStatus, DeadLetterReason
```

→ **Reconstruction d'un slip complet en une requête Table (pas même besoin d'Application Insights).**

#### 6.7.6 Intégration MTJ ↔ DFO — propagation TraceId

> 🧭 **Le journal devient le pont entre l'audit légal (Azure Table, rétention 7 ans) et l'observabilité opérationnelle (Application Insights, rétention 90 jours).**

Le lot R14 (W3C TraceContext propagation, déjà introduit en §13.9) doit être **couplé au lot R16** :

| Lot | Rôle dans la corrélation |
|---|---|
| **R14** | Propage `traceparent` côté **Service Bus ApplicationProperties** (Producer → Consumer) |
| **R16** | Injecte `TraceId/SpanId` dans **`JournalEntry`** (Activity.Current → Journal) |

**Source unique de vérité** : `Activity.Current`. Si l'`Activity` ambiente est posée correctement (lots R13 + R14), alors :

```csharp
// AzureJournalProvider.WriteRecordAsync (interne)
var entity = BuildEntity(entry);
// Injection automatique des champs trace si absents dans entry
if (string.IsNullOrEmpty(entry.TraceId) && Activity.Current is { } act)
{
    entity["TraceId"]      = act.TraceId.ToString();
    entity["SpanId"]       = act.SpanId.ToString();
    entity["ParentSpanId"] = act.ParentSpanId.ToString();
}
```

**Bénéfice opérationnel pour un agent CAI / SRE :**

| Avant R16 | Après R16 |
|---|---|
| Cherche dans la Table Journal par `CorrelationId` (slow scan) | Cherche par `TraceId` ou `SlipId` (index direct) |
| Joint manuellement avec Application Insights | Lien cliquable « Voir cette trace dans Application Map » dans le Workbook |
| Reconstruit un saga 5-steps en lisant 5 logs distincts | Une seule requête KQL sur la Table Journal |

#### 6.7.7 Compensation et DLQ — couverture cible

Le journal doit aussi capturer **les compensations Routing Slip** (déclenchées en cas de `Fault`) — aujourd'hui ces opérations ne laissent aucune trace journalisée.

**Cas à couvrir (lot R16) :**

| Événement | Factory cible | Quand |
|---|---|---|
| Publish initial du slip | `ForPublish` (existant) | Activator → `PublishAsync` |
| Step franchi avec succès | `ForSlipStep(status=Completed)` (nouveau) | RoutingSlipExecutor après `ActivityResult.Next` |
| Step en faute | `ForSlipStep(status=Faulted)` (nouveau) | RoutingSlipExecutor après `ActivityResult.Fault` |
| Compensation déclenchée | `ForSlipCompensation` (nouveau) | RoutingSlipExecutor en mode rollback LIFO |
| Slip complété | `ForSlipComplete` (nouveau) | RoutingSlipExecutor sur dernier step + Complete |
| Retry exponentiel | `ForRetry` (existant) | RetryPolicyHandler |
| DLQ final | `ForDLQ` (existant) | RetryPolicyHandler après MaxDeliveryCount |

#### 6.7.8 Lot R16 — ✅ Livré

> **Livré :** MTJ enrichi — Routing Slip totalement intégré au journal, TraceId/SpanId auto-injectés, SlipId natif. R15 (réconciliation `messages_sent_total`) — **hors scope**.

##### Phase R16-A — Enrichissement schéma `JournalEntry` (1 semaine)

| # | Tâche | Livrable |
|---|---|---|
| R16-A.1 | Ajouter `TraceId`, `SpanId`, `ParentSpanId` (nullable) à `JournalEntry` | PR additif |
| R16-A.2 | Ajouter `SlipId`, `SlipName`, `StepIndex`, `StepName`, `StepStatus` (nullable) | PR additif |
| R16-A.3 | Étendre `AzureJournalProvider.BuildEntity` pour persister les nouveaux champs en Azure Table | PR — entité Table flexible |
| R16-A.4 | Test unitaire : round-trip JournalEntry avec tous les champs Trace + Slip | Test vert |
| R16-A.5 | Migration de table : aucune (Azure Table est schemaless — les colonnes apparaissent automatiquement à la première écriture) | Note dans MIGRATION.md |

**Compatibilité :** 100 % rétrocompatible — les anciennes entrées sans `TraceId` restent lisibles, les nouvelles ont les champs supplémentaires.

##### Phase R16-B — Auto-injection `Activity.Current` (3 jours)

| # | Tâche | Livrable |
|---|---|---|
| R16-B.1 | Dans `AzureJournalProvider.WriteRecordAsync` : si `entry.TraceId` est null, lire `Activity.Current?.TraceId` | PR |
| R16-B.2 | Idem dans `WriteBatchAsync` | PR |
| R16-B.3 | Test : appel `WriteRecordAsync` dans une portée `Activity.StartActivity(...)` → l'entrée journal porte le bon `TraceId` | Test vert |

##### Phase R16-C — Nouvelles factory methods (1 semaine)

| # | Tâche | Livrable |
|---|---|---|
| R16-C.1 | `JournalEntry.ForSlipStep(...)` : accepte SlipId, StepName, Cursor, StepStatus, TraceId | PR + test |
| R16-C.2 | `JournalEntry.ForSlipCompensation(...)` : variante pour compensations LIFO | PR + test |
| R16-C.3 | `JournalEntry.ForSlipComplete(...)` : variante pour le complete final | PR + test |
| R16-C.4 | Documenter le catalogue de factories dans `docs/observability/journal.md` (nouveau fichier) | Doc + 6 exemples |

##### Phase R16-D — Intégration `RoutingSlipExecutor` (1 semaine)

| # | Tâche | Livrable |
|---|---|---|
| R16-D.1 | Injecter `IJournalProvider` dans `RoutingSlipExecutor<TArgs>` (constructeur) | PR |
| R16-D.2 | Wrapper Try/Catch (pattern A5) autour de l'écriture journal — `SafeJournalAsync` | PR |
| R16-D.3 | Écrire `ForSlipStep(Completed)` après chaque `Next()` réussi | PR |
| R16-D.4 | Écrire `ForSlipStep(Faulted)` lors d'un `Fault(ex)` | PR |
| R16-D.5 | Écrire `ForSlipCompensation` pour chaque compensateur déclenché | PR |
| R16-D.6 | Écrire `ForSlipComplete` sur le dernier step réussi | PR |
| R16-D.7 | Test d'intégration sur Service Bus Emulator : saga 3 steps → 4 entrées journal (1 publish + 3 steps) | Test vert |

##### Phase R16-E — Workbook et alertes basés sur le MTJ enrichi (1 semaine)

| # | Tâche | Livrable |
|---|---|---|
| R16-E.1 | Workbook **MTJ Slip Timeline** — vue chronologique d'un slip par `SlipId` | JSON Workbook |
| R16-E.2 | Workbook **MTJ Audit Trail** — recherche par `DossierId` / `MessageId` / `SlipId` | JSON Workbook |
| R16-E.3 | Alerte sur slips bloqués > N heures (slip publié sans `ForSlipComplete` correspondant) | Bicep alert |
| R16-E.4 | Documentation : pattern KQL « Reconstruction d'un slip » avec exemples | Doc `docs/observability/journal-kql.md` |

**Total R16 :** ~4-5 semaines (1 dev backend + 0.5 dev SRE en revue).

**Dépendances :**
- Pré-requis : Lot R14 (W3C TraceContext propagation) livré avant R16-B (sinon `Activity.Current.TraceId` n'est pas lié aux spans Producer/Consumer).
- Co-requis : Lot R15 (réconciliation) bénéficie directement du `SlipId` natif pour les alertes de slip stuck.

**Risques R16 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Cardinalité Table Azure explose (cf. limite 252 colonnes) | 🟢 Faible | Faible | 5 nouveaux champs nullable seulement → 18 colonnes au total, marge énorme |
| Coût Table Storage augmente × 5 (5 entrées/saga au lieu de 1) | 🟡 Moyenne | Moyen | Table Storage coûte ~0,05 $ CAD / Go — impact négligeable même × 10 |
| Refactor lot R16-D casse des tests Routing Slip existants | 🟡 Moyenne | Moyen | TDD : écrire d'abord les tests, refactor ensuite |
| Conflit avec lots R13 (BeginScope) et R14 (traceparent) | 🟡 Moyenne | Moyen | Séquence stricte : R13 → R14 → R16-B → R16-D |

### 6.8 Sessions Service Bus

`EnableSession = true` → `SessionId` obligatoire, FIFO garanti par session, lock mono-consumer. Validation fail-fast dans `PublishCoreAsync` (lève `ArgumentNullException` si manquant).

### 6.9 Multi-Target Producer — pattern fanout typé ✅ Livré (R17)

> 💡 **Pour un junior — le besoin métier :** une application RAMQ doit parfois envoyer **différents types de messages** vers **différentes queues/topics** dans la même opération. Exemple typique : un service de réservation qui publie en parallèle un `CarMessage` vers la queue `car-bookings`, un `HotelMessage` vers `hotel-bookings`, et un `FlightMessage` vers `flight-bookings`. Le code applicatif doit aujourd'hui **réinventer la glu** à chaque fois.

#### 6.9.1 État actuel — sample `Queue.MultiTarget` (~150 lignes de boilerplate)

Le sample [`RAMQ.Samples.Queue.MultiTarget.*`](../Exemples/) démontre le pattern, mais demande au développeur d'**implémenter 5 projets** et **3 classes Producer + 3 classes Consumer** pour 3 cibles. Voici la structure actuelle :

```
Exemples/
├── RAMQ.Samples.Queue.MultiTarget.Message/      (records typés : CarMessage, HotelMessage, FlightMessage)
├── RAMQ.Samples.Queue.MultiTarget.Producer/
│   ├── IMultiTargetProducer.cs                  (interface — propre au sample)
│   ├── MultiTargetProducer<TMessage>.cs         (classe abstraite — pattern Strategy)
│   ├── CarProducer.cs                           (sous-classe — ~22 lignes)
│   ├── HotelProducer.cs                         (sous-classe — ~22 lignes)
│   ├── FlightProducer.cs                        (sous-classe — ~22 lignes)
│   └── MultiTargetPublicationService.cs         (orchestrateur — itère la chaîne)
├── RAMQ.Samples.Queue.MultiTarget.Consumer/
│   ├── CarConsumer.cs                           (~30 lignes)
│   ├── HotelConsumer.cs                         (~30 lignes)
│   └── FlightConsumer.cs                        (~30 lignes)
├── RAMQ.Samples.Queue.MultiTarget.Activator/    (3 ServiceBusTrigger Functions)
└── RAMQ.Samples.Queue.MultiTarget.Worker/       (DoWork — appelle MultiTargetPublicationService)
```

#### 6.9.2 Le pain point côté Producer — concret

Voici comment le développeur publie aujourd'hui un message vers une cible « Car » :

```csharp
// 1. Définir une sous-classe ProducerStrategy pour chaque type
public class CarProducer : MultiTargetProducer<CarMessage>, IMultiTargetProducer
{
    private readonly IMessageProducer<CarMessage> _producer;
    public CarProducer(IMessageProducer<CarMessage> producer) => _producer = producer;

    public override string NomTarget => "Car";

    public override MessageTransitContext<CarMessage> CreerContexte(
        string target, Guid id, string content) => new()
    {
        MessageId = id.ToString("N"),
        Message   = new CarMessage { Id = id, Content = content }
    };

    public async Task<bool> TryPublishAsync(string target, Guid id, string content, CancellationToken ct)
    {
        if (!target.Equals("Car", StringComparison.OrdinalIgnoreCase)) return false;
        var ctx = CreerContexte(target, id, content);
        await _producer.PublishAsync(ctx, ...);
        return true;
    }
}

// 2. Idem pour HotelProducer, FlightProducer — copy/paste avec changement de "Car" → "Hotel"

// 3. Service orchestrateur
public class MultiTargetPublicationService
{
    private readonly IEnumerable<IMultiTargetProducer> _producers;

    public async Task PublierAsync(string target, Guid id, string content, CancellationToken ct)
    {
        foreach (var producer in _producers)
        {
            if (await producer.TryPublishAsync(target, id, content, ct))
                return;
        }
        throw new InvalidOperationException($"Aucun producer ne reconnaît la cible : {target}");
    }
}

// 4. Worker
public class DoWork : BackgroundService
{
    public DoWork(MultiTargetPublicationService service) { /* ... */ }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _service.PublierAsync("Car",    Guid.NewGuid(), "...", ct);
        await _service.PublierAsync("Hotel",  Guid.NewGuid(), "...", ct);
        await _service.PublierAsync("Flight", Guid.NewGuid(), "...", ct);
    }
}

// 5. DI Setup
services.AddTransient<IMultiTargetProducer, CarProducer>();
services.AddTransient<IMultiTargetProducer, HotelProducer>();
services.AddTransient<IMultiTargetProducer, FlightProducer>();
services.AddSingleton<MultiTargetPublicationService>();
```

🟠 **Constat :** ce code est **du boilerplate non-métier**. Aucune ligne ci-dessus ne fait quoi que ce soit qu'EMT ne pourrait pas faire. C'est de la glu d'infrastructure que **chaque équipe RAMQ doit réinventer**.

**Comptage des problèmes :**

| Problème | Impact |
|---|---|
| ~150 lignes de boilerplate par déploiement multi-target | Vélocité ↓, dette ↑ |
| Pattern Strategy par chaînage `foreach + TryPublishAsync` | O(n) à chaque publish, pas de garantie de résolution |
| Magic strings (`"Car"`, `"Hotel"`, `"Flight"`) | Pas de type-safety, fautes de frappe silencieuses |
| Aucune extension EMT — chaque équipe ré-invente | Divergence inter-équipes, incohérence d'observabilité |
| `IMultiTargetProducer` n'est pas dans EMT, mais dans le sample | Pas d'invariant garanti par la lib |
| Pas de propagation OTel auto | Lots R13/R14 doivent être réimplémentés dans chaque CarProducer |

#### 6.9.3 Design cible — `IMultiTargetProducer<TBase>` natif dans EMT

> 🧭 **Vision :** absorber le pattern Strategy dans la lib EMT. Le développeur déclare ses cibles **une fois en DI**, et obtient une API **type-safe** qui route automatiquement le bon message vers la bonne queue.

##### Interface livrée dans la lib EMT (lot R17)

```csharp
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer
{
    /// <summary>
    /// Producer multi-cible avec routage automatique par type.
    /// Chaque message TPayload (héritier ou implémenteur de TBase) est routé
    /// vers la cible logique configurée par AddMultiTargetProducer{TPayload}("target").
    /// </summary>
    public interface IMultiTargetProducer<TBase> where TBase : class
    {
        /// <summary>
        /// Publie un message TPayload vers la cible logique enregistrée pour ce type.
        /// La résolution de cible se fait via IMessageTargetMap (zero magic string).
        /// </summary>
        Task<MessageTransitContext<MessageTransitResponse>> PublishAsync<TPayload>(
            MessageTransitContext<TPayload> context,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default)
            where TPayload : class, TBase;

        /// <summary>
        /// Batch typé : publie une collection hétérogène de TPayload où chaque
        /// élément est routé vers SA cible (résolution par type d'objet).
        /// </summary>
        Task<IReadOnlyList<string>> PublishMixedBatchAsync(
            IEnumerable<MessageTransitContext<TBase>> contexts,
            PublishOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
```

##### Extensions DI (lot R17)

```csharp
namespace RAMQ.COM.EnterpriseMessageTransit.Configuration.Extensions
{
    public static class MultiTargetProducerServiceCollectionExtensions
    {
        /// <summary>
        /// Enregistre un IMultiTargetProducer{TBase} où TBase est une classe ou interface
        /// commune (peut être 'object' si vraiment hétérogène).
        /// </summary>
        public static IServiceCollection AddMultiTargetProducer<TBase>(
            this IServiceCollection services,
            Action<MultiTargetBuilder<TBase>> configure)
            where TBase : class
        {
            var builder = new MultiTargetBuilder<TBase>(services);
            configure(builder);
            services.AddSingleton<IMultiTargetProducer<TBase>>(sp =>
                new EmtMultiTargetProducer<TBase>(sp, builder.Targets));
            return services;
        }
    }

    public sealed class MultiTargetBuilder<TBase> where TBase : class
    {
        public IDictionary<Type, string> Targets { get; } = new Dictionary<Type, string>();

        /// <summary>
        /// Lie un type TPayload (sous-type de TBase) à une cible logique.
        /// Internalement, AddProducer{TPayload}(target) est aussi appelé.
        /// </summary>
        public MultiTargetBuilder<TBase> AddTarget<TPayload>(string target)
            where TPayload : class, TBase
        {
            _services.AddProducer<TPayload>(target);
            Targets[typeof(TPayload)] = target;
            return this;
        }
    }
}
```

##### Usage côté application — **5 lignes au lieu de ~150**

```csharp
// Program.cs côté Producer-side (Worker / Activator)
services.AddMultiTargetProducer<IBookingMessage>(b => b
    .AddTarget<CarMessage>("Car")
    .AddTarget<HotelMessage>("Hotel")
    .AddTarget<FlightMessage>("Flight"));

// Côté code applicatif
public class BookingService
{
    private readonly IMultiTargetProducer<IBookingMessage> _producer;

    public BookingService(IMultiTargetProducer<IBookingMessage> producer) => _producer = producer;

    public async Task ReserveAllAsync(Guid id, CancellationToken ct)
    {
        // Le bon producer interne est choisi automatiquement par typeof(TPayload)
        await _producer.PublishAsync(new MessageTransitContext<CarMessage>
            { Message = new CarMessage { /* ... */ } }, ct: ct);
        await _producer.PublishAsync(new MessageTransitContext<HotelMessage>
            { Message = new HotelMessage { /* ... */ } }, ct: ct);
        await _producer.PublishAsync(new MessageTransitContext<FlightMessage>
            { Message = new FlightMessage { /* ... */ } }, ct: ct);
    }
}
```

**Gain quantifié :**

| Métrique | Avant (sample actuel) | Après (`IMultiTargetProducer<T>` natif) | Gain |
|---|---|---|---|
| Lignes de code applicatif | ~150 | ~5 (DI) + appels typés | **-95 %** |
| Classes à créer par target | 1 Producer + 1 Consumer | 0 Producer (uniquement Consumer) | **-50 %** |
| Magic strings | Oui (`"Car"`, `"Hotel"`) | Non (résolution par type) | **-100 %** |
| Type safety | `string target` | `where TPayload : TBase` | ✅ |
| Cohérence OTel cross-app | Variable | Garantie EMT | ✅ |

#### 6.9.4 Avantages opérationnels du design natif

1. **Type-safety :** une faute de frappe `"Caar"` ne compile plus — la cible est résolue par `typeof(TPayload)`.
2. **Cohérence observabilité :** `IMultiTargetProducer<T>` hérite gratuitement de tous les améliorations DFO (lots R13, R14, R16) — chaque équipe n'a plus à les reproduire.
3. **Découplage du fanout :** l'application appelante ne connaît pas le nom de la queue Service Bus — c'est de la **configuration**, pas du **code**.
4. **Test unitaire trivial :** mocker `IMultiTargetProducer<IBookingMessage>` suffit, plus besoin de mocker N `IMessageProducer<T>` individuels.
5. **Batch hétérogène natif :** `PublishMixedBatchAsync` permet d'envoyer Car + Hotel + Flight en **un seul appel optimisé** par EMT (sender cache mutualisé).
6. **Compatible request/reply :** un projet futur peut ajouter `IMultiTargetRequestReplyClient<TBase, TResponse>` sur le même pattern.

#### 6.9.5 Lot R17 — ✅ Livré

> **Livré :** `IMultiTargetProducer<TBase>` ajouté à EMT, boilerplate Strategy supprimé du sample, migration complète.

##### Phase R17-A — API publique (3 jours)

| # | Tâche | Livrable |
|---|---|---|
| R17-A.1 | Définir `IMultiTargetProducer<TBase>` + `EmtMultiTargetProducer<TBase>` (impl interne) dans `Messaging.Producer/` | PR additif |
| R17-A.2 | Définir `MultiTargetBuilder<TBase>` + `AddMultiTargetProducer<TBase>(...)` extension | PR additif |
| R17-A.3 | Résolution interne `typeof(TPayload) → target` via `IMessageTargetMap` (déjà existant) | Pas de nouveau code, réutilisation |
| R17-A.4 | Mise à jour `PublicAPI.Unshipped.txt` avec les nouveaux types publics | PR |

##### Phase R17-B — Implémentation `PublishMixedBatchAsync` (3 jours)

| # | Tâche | Livrable |
|---|---|---|
| R17-B.1 | Grouper la collection mixte par `typeof(TPayload)` | Code interne |
| R17-B.2 | Pour chaque groupe, appeler `IMessageProducer<TPayload>.PublishBatchAsync` correspondant | Code interne |
| R17-B.3 | Retourner la liste de `MessageId` dans l'ordre d'entrée (même contrat que `PublishBatchAsync`) | Code interne |
| R17-B.4 | Tests unitaires : batch mixte de 100 messages = 3 batches Service Bus (un par type) | Tests verts |

##### Phase R17-C — Refonte des samples Multi-Target (1 semaine)

| # | Tâche | Livrable |
|---|---|---|
| R17-C.1 | Supprimer les classes `CarProducer`, `HotelProducer`, `FlightProducer` du sample | -75 lignes |
| R17-C.2 | Supprimer `IMultiTargetProducer` (interface du sample) et `MultiTargetProducer<T>` (classe abstraite) | -50 lignes |
| R17-C.3 | Supprimer `MultiTargetPublicationService` (remplacé par `IMultiTargetProducer<IBookingMessage>` natif EMT) | -34 lignes |
| R17-C.4 | Refondre `DoWork.cs` côté Worker — utilisation directe de `IMultiTargetProducer<IBookingMessage>` | -20 lignes |
| R17-C.5 | Refondre `Program.cs` côté Worker — `AddMultiTargetProducer<IBookingMessage>(b => ...)` | -5 lignes |
| R17-C.6 | Ajouter `IBookingMessage` (interface marqueur) dans `RAMQ.Samples.Queue.MultiTarget.Message` | +5 lignes |
| R17-C.7 | Sample devient une **référence pédagogique** : démontrer l'API en ~30 lignes au lieu de ~150 | Doc README sample mise à jour |

##### Phase R17-D — Documentation et pédagogie (3 jours)

| # | Tâche | Livrable |
|---|---|---|
| R17-D.1 | Créer `docs/multi-target.md` dans EMT : explication pattern + exemples | Doc complète |
| R17-D.2 | Ajouter section dans `CONTRIBUTING.md` côté samples : « Quand utiliser `IMultiTargetProducer<T>` vs `IMessageProducer<T>` ? » | Doc |
| R17-D.3 | Mettre à jour le sample README avec un benchmark avant/après (lignes de code) | Doc |
| R17-D.4 | Ajouter un test d'architecture (NetArchTest) : « les samples Producer ne doivent plus implémenter de pattern Strategy maison » | Test architecture |

**Total R17 :** ~3 semaines (1 dev backend + 0.5 dev SRE en revue).

**Dépendances :**
- Pré-requis : Lot R1 (tests) livré — pour valider la non-régression.
- Indépendant des lots R13/R14/R16 (ne touche pas l'observabilité ni le journal).
- Couplage possible : `IMultiTargetProducer<T>` peut être utilisé par les **activateurs Routing Slip** pour publier la première étape avec un type métier (au lieu de `SlipEnvelope`).

**Risques R17 et mitigations :**

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Le générique `where TPayload : TBase` rend l'API complexe pour un junior | 🟡 Moyenne | Faible | Doc avec 3 exemples : `TBase = interface`, `TBase = classe abstraite`, `TBase = object` |
| L'ancien sample `Queue.MultiTarget.Producer` reste référencé par d'autres projets | 🟢 Faible | Faible | Garder l'ancien pendant 1 release marqué `[Obsolete]`, suppression complète à la release suivante |
| Incompatibilité avec request/reply (lot R3 livré) | 🟢 Faible | Moyen | `IMultiTargetProducer<TBase>` n'expose **pas** request/reply (séparation ISP préservée) |
| Confusion entre `IMultiTargetProducer<TBase>` (mêmes broker, multiples cibles) et multi-broker | 🟡 Moyenne | Faible | Note explicite dans la doc : « EMT v1.x supporte uniquement Service Bus — multi-cible ≠ multi-broker » |

#### 6.9.6 Avant / Après — vue de l'application appelante

##### Avant (aujourd'hui — sample `Queue.MultiTarget`)

```csharp
// 8 fichiers, ~150 lignes
// 3 classes Producer + 1 interface + 1 classe abstraite + 1 service + 3 enregistrements DI

await _multiTargetService.PublierAsync("Car",    id, "...", ct);
await _multiTargetService.PublierAsync("Hotel",  id, "...", ct);
await _multiTargetService.PublierAsync("Flight", id, "...", ct);
// ↑ magic strings, pas de type safety, ~150 LOC en support
```

##### Après (cible — `IMultiTargetProducer<T>` natif EMT)

```csharp
// 1 enregistrement DI, 0 classe Producer custom

services.AddMultiTargetProducer<IBookingMessage>(b => b
    .AddTarget<CarMessage>("Car")
    .AddTarget<HotelMessage>("Hotel")
    .AddTarget<FlightMessage>("Flight"));

// Usage
await _producer.PublishAsync(new MessageTransitContext<CarMessage> { ... }, ct: ct);
await _producer.PublishAsync(new MessageTransitContext<HotelMessage> { ... }, ct: ct);
await _producer.PublishAsync(new MessageTransitContext<FlightMessage> { ... }, ct: ct);
// ↑ type-safe, 0 magic string, EMT s'occupe du routage
```

🟢 **Verdict :** le pattern Multi-Target Producer doit appartenir à EMT, pas au sample. C'est de l'infrastructure mutualisable, pas du code métier RAMQ.

---

## 7. Inventaire des exemples — 37 projets dans `Exemples/`

Le dossier [`Exemples/`](../Exemples/) contient **37 projets** qui démontrent les patterns EMT en conditions réelles. Compris dans leur ensemble, ils forment la documentation vivante de la librairie.

### 7.1 Tableau récapitulatif des samples

| # | Projet | Rôle | Pattern EMT | API v2.0 ? | État |
|---|---|---|---|---|---|
| 1 | `RAMQ.Samples.ConfigurationService` | Lib partagée — IConsumer/Producer ConfigurationService | Configuration | n/a | 🟢 Active |
| 2 | `RAMQ.Samples.MessageTransitHelper` | Lib partagée — helpers MessageTransitContext | Helpers | n/a | 🟢 Active |
| 3 | `RAMQ.Samples.Queue.Simple.Message` | Lib message DTO | Queue Simple | n/a | 🟢 Active |
| 4 | `RAMQ.Samples.Queue.Simple.Activator` | HTTP trigger qui publie un message | Queue Simple (producer) | v2.0 | 🟢 Active |
| 5 | `RAMQ.Samples.Queue.Simple.Worker` | Function trigger qui consomme | Queue Simple (worker) | v2.0 | 🟢 Active |
| 6 | `RAMQ.Samples.Queue.Simple.Consumer` | Lib consumer `BaseConsumer<T>` | Queue Simple (consumer) | v2.0 | 🟢 Active |
| 7 | `RAMQ.Samples.Queue.MultiTarget.Message` | DTOs typés (`CarMessage`, `HotelMessage`, `FlightMessage`) + interface marqueur `IBookingMessage` | Queue MultiTarget | n/a | 🟢 Active |
| 8 | `RAMQ.Samples.Queue.MultiTarget.Activator` | Azure Function ServiceBusTrigger — reçoit les 3 types de messages | Queue MultiTarget | v2.0 | 🟢 Active |
| 9 | `RAMQ.Samples.Queue.MultiTarget.Worker` | Worker `IMultiTargetProducer<IBookingMessage>` — démo `PublishAsync<T>` + `PublishMixedBatchAsync` (R17) | Queue MultiTarget | v2.0 | 🟢 Active — boilerplate Strategy supprimé |
| 10 | `RAMQ.Samples.Queue.MultiTarget.Consumer` | Consumer dérivé par target | Queue MultiTarget | v2.0 | 🟢 Active |
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
| 36 | `RAMQ.Samples.Queue.CircuitBreaker.Message` | DTO `CircuitBreakerMessage` (Id, Payload, Target) | Circuit Breaker | n/a | 🟢 Active |
| 37 | `RAMQ.Samples.Queue.CircuitBreaker.Worker` | BackgroundService — 3 phases : Closed → Open (3 échecs) → HalfOpen → Closed ; `CircuitBreakerOpenException` capturée + métriques OTel `circuit_state` / `circuit_transitions_total` | Circuit Breaker | v2.0 | 🟢 Active |

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

#### Pourquoi Durable Orchestrator et non Durable Entity ?

> 🧭 **Question architecturale fréquente :** Durable Functions propose deux abstractions — les **Orchestrateurs** (workflows) et les **Entités** (acteurs stateful). Pourquoi le TDF Sequential Convoy utilise-t-il un Orchestrateur plutôt qu'une Entité ?

**Rappel — différence entre les deux :**

| | Durable Orchestrator | Durable Entity |
|---|---|---|
| Modèle mental | Machine à états finie avec étapes séquentielles | Acteur persistant avec état mutable |
| Attente d'événement externe | `WaitForExternalEventAsync("event", timeout)` — **natif** | Non natif — nécessite un Orchestrateur ou un Timer Entity en plus |
| Timeout configurable | `WaitForExternalEventAsync(…, TimeSpan.FromMinutes(x))` — intégré | Doit être implémenté manuellement via `context.SignalEntity` différé |
| History / Audit | Historique complet de chaque step → audit CAI natif | Pas d'historique de transitions — état courant seulement |
| Custom Status | `context.SetCustomStatus(…)` — visible dans Azure Portal / Grafana | Non disponible |
| Idempotence de création | `ScheduleNewOrchestrationInstanceAsync(instanceId)` — rejette les doublons si même ID | `context.SignalEntity(entityId)` — crée l'entité si inexistante, pas de concept de "déjà en cours" |
| Replay déterministe | Oui — seules les activités ont des side-effects ; l'orchestrateur rejoue sans appels réseau | Non applicable — les entités n'ont pas de replay |

**Pourquoi le Sequential Convoy TDF exige un Orchestrateur :**

Le pattern TDF attend **deux messages corrélés dans un ordre précis** (`tdf.envoi` puis `tdf.correller`) avec un **timeout** entre les deux :

```
tdf.envoi reçu    → démarrer l'orchestrateur (StartOrchestration)
                        ↓ attendre "CorrellerEnvoyer" pendant max X minutes
tdf.correller reçu → RaiseEventAsync("CorrellerEnvoyer") → orchestrateur se réveille
                  OU
Timeout expiré    → orchestrateur traite le cas "tdf.correller jamais reçu"
```

Avec une **Durable Entity**, ce flux deviendrait :
```
tdf.envoi reçu    → SignalEntity(entityId, "OnEnvoi")  → entité stocke l'état "EnvoiReçu"
                       + créer un Timer Entity séparé pour le timeout
tdf.correller reçu → SignalEntity(entityId, "OnCorrellation") → entité appelle le backend
Timeout           → Timer Entity signale l'entité → entité traite le cas timeout
```

Problèmes de l'approche Entity pour ce cas :
1. **Pas de timeout natif** — il faut un second acteur (Timer Entity ou Orchestrateur) juste pour gérer le délai. La complexité double.
2. **Pas d'historique** — l'audit CAI exige de tracer chaque transition d'état. Une Entity expose uniquement son état actuel, pas ses transitions.
3. **Custom Status impossible** — le monitoring opérationnel (Azure Portal, Grafana) ne peut pas afficher "En attente de corrélation depuis 3 min" pour une Entity.
4. **Idempotence de création plus fragile** — `SignalEntity` ne protège pas contre les doubles créations de la même façon que `ScheduleNewOrchestrationInstanceAsync(instanceId = sessionId)`.
5. **Replay-safe logging impossible** — dans un Orchestrateur, les logs dans `orchestrationContext.CreateReplaySafeLogger()` sont automatiquement filtrés lors du replay. Les Entities n'ont pas ce concept.

**Quand utiliser Durable Entity à la place :**

Les Entities sont préférables quand :
- L'état doit être **partagé entre plusieurs orchestrateurs** (ex. : un compteur de transactions par dossier accessible par plusieurs workflows en parallèle).
- Le cycle de vie est **long et sans étapes définies** (ex. : un dossier HOA5 qui accumule des événements sur des semaines sans timeline fixe).
- La logique est du **CRUD pur** sans machine à états (ex. : `IncrementeCompteur`, `AjouterEtape`, `GetStatut`).

Dans le TDF, le cycle de vie est court et structuré (envoi → corrélation → résultat), avec un timeout dur — l'Orchestrateur est le bon choix.

### 7.4 Observations transverses sur les samples

| # | Observation | Sévérité |
|---|---|---|
| **S-1** | ~~Aucun sample ne démontre Claim Check actif.~~ ✅ **Résolu R2** — `RAMQ.Samples.Queue.ClaimCheck.*` (4 projets) couvre gros message JSON, pièce jointe binaire et message léger. Options A (référence) et B (download inline) démontrées. | 🟢 Résolu |
| **S-2** | ~~Aucun sample ne démontre le Circuit Breaker en action.~~ ✅ **Résolu** — `RAMQ.Samples.Queue.CircuitBreaker.*` (2 projets) démontre les 3 phases : Closed → Open (après 3 échecs) → HalfOpen → Closed. `CircuitBreakerOpenException` capturée, métriques OTel visibles. | 🟢 Résolu |
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

### 8.7 Message Transit Journal — BAM enterprise (pattern A5)

> 🧭 **Position stratégique :** le MTJ est notre **Business Activity Monitoring** (cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique)) — pas un simple audit log. Pattern A5 désigne le **mécanisme** (découplage du chemin critique), pas la finalité (BAM enterprise).


| Axe | Verdict | Évidence |
|---|---|---|
| Implémentation | 🟢 Complet | [`AzureJournalProvider.cs`](../EnterpriseMessageTransit/Messaging/Providers/Azure/AzureJournalProvider.cs), `JournalEntry.ForPublish/ForRetry/ForDLQ/ForSlipStep/ForSlipCompensation/ForSlipComplete` |
| Complétude | 🟢 Complet | Try/catch + `SafeJournalAsync` systématiques (pattern A5) + Routing Slip totalement intégré (R16) |
| Testabilité | 🟢 Bon | `IJournalProvider` mockable |
| Observabilité | 🟢 Bon | Counter `journal_writes_total`, histogram `journal_write_duration_ms` + `TraceId/SpanId` auto-injectés |
| Documentation | 🟢 Bon | Voir [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique) |

🟢 **R6 livré :** `WriteBatchAsync` — batch `SubmitTransactionAsync`, 1 round-trip par partition.

🟢 **R16 livré :** Routing Slip totalement intégré — `JournalEntry` enrichi (`TraceId`, `SpanId`, `SlipId`, `SlipName`, `StepIndex`, `StepName`, `StepStatus`), `RoutingSlipExecutor` journalise chaque step (Completed/Faulted/Compensated/Complete). `SlipStepStatus.Compensated` ajouté.

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
| Complétude | 🟡 Partielle | Validation infra ✅ livrée ; guidance `MessageId` déterministe ✅ livrée (§6.5 — 5 règles + patterns) ; sample dédié restant |
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
| Pattern A5 (Journal) | 🟢 **Complet (R6 + R16 livrés)** | Batch `SubmitTransactionAsync` + Routing Slip intégré + TraceId/SpanId auto-injectés |
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
| **L** — Liskov Substitution | 🟡 **OK** | Marker interfaces → Phase 6 (L1) |
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

#### 🟡 Violation L1 — Marker interfaces — ⛾ Reportée Phase 6

[`Configuration/IConsumerConfigurationService.cs`](../EnterpriseMessageTransit/Configuration/IConsumerConfigurationService.cs) et [`IProducerConfigurationService.cs`](../EnterpriseMessageTransit/Configuration/IProducerConfigurationService.cs) sont des **marker interfaces** vides héritant de `IMessageTransitConfigurationService`.

**Conséquence :** `EndpointResolver.IsConsumer => _config is IConsumerConfigurationService` utilise du **type-checking runtime fragile** au lieu d'un paramètre explicite. Si un nouveau type de config arrive (ex. `IBidirectionalConfigurationService`), le test échoue silencieusement.

🧭 **Refactor proposé :** remplacer par une propriété `ConfigurationKind` enum ou par injection séparée de deux configs distincts.

> ⛾ **Décision (2026-05-28) :** reporté en **Phase 6**. Le refactor impacte ~15 fichiers (lib + samples) : `BaseConsumer`, `Producer`, `ConsumerConfigurationService`, `ProducerConfigurationService`, `EndpointResolver`, et tous les call sites DI. Le coût est disproportionné pour le bénéfice actuel — le `is IConsumerConfigurationService` fonctionne sans risque de régression dans l'architecture v1.x.

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
| « **Cette opération a-t-elle été faite, à qui, quand ? Quel KPI métier en résulte ?** » | **MTJ — Business Activity Monitoring** (cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique)) | `IJournalProvider` → Azure Table → Power BI / ADX |
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
              │  OpenTelemetry SDK   │                    │  (BAM enterprise)│
              │  (Tracer/Meter/Log   │                    │  Azure Table     │
              │   Providers)         │                    │  ← rétention 7 ans│
              │                      │                    │  ← Business KPI  │
              │                      │                    │  ← découplé OTel │
              └──────────┬───────────┘                    └────────┬─────────┘
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
              │ (APM technique,           │         │  MessageTransitJournal│
              │  workspace-based)         │         │  (BAM — rétention 7a)│
              └──────────┬───────────────┘         └──────────┬──────────┘
                         │                                     │
                         │                                     ▼
                         │                          ┌──────────────────────┐
                         │                          │ Azure Data Factory   │
                         │                          │ (export planifié BAM)│
                         │                          └──────────┬──────────┘
                         │                                     │
                         ▼                                     ▼
              ┌──────────────────────────┐         ┌──────────────────────┐
              │ Log Analytics Workspace  │         │  Power BI Premium    │
              │ (stockage + KQL)         │         │  (dashboards métier  │
              │  - dependencies          │  ◄────► │   direction RAMQ)    │
              │  - customMetrics         │ TraceId │  ← KPI volume/SLA    │
              │  - traces (logs)         │ jointure│  ← conformité CAI    │
              │  - exceptions            │ (R16)   │  ← BAM exécutif       │
              └──────────┬───────────────┘         └──────────────────────┘
                         │
                         ▼
              ┌──────────────────────────────────────┐
              │  Azure Monitor — usages opérationnels │
              │  • Application Map (graphe sagas)     │
              │  • Live Metrics (latence < 1 s)        │
              │  • Smart Detection (anomalies)         │
              │  • Workbooks APM + BAM (dashboards RAMQ)│
              │  • Action Groups (alertes PagerDuty)   │
              └──────────────────────────────────────┘
```

🟢 **Décision RAMQ : Azure Monitor (Application Insights workspace-based + Log Analytics Workspace) est le backend APM cible.** Pour le BAM, **Azure Table Storage** (MessageTransitJournal) + **Power BI Premium** sont les backends ciblés. **APM et BAM sont deux pilliers complémentaires**, joints par `TraceId` (lot R16) — pas concurrents. Pas de Grafana, Jaeger ou Prometheus en production — seulement comme outils dev locaux.

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

### 13.3.2 Rôle d'Application Insights dans la solution EMT

#### Architecture : collecteur → stockage

```
Applications EMT (workers Azure Functions)
    ↓ SDK AppInsights + UseAzureMonitorExporter
Application Insights
    │  (façade UI : Application Map, Live Metrics, Transaction Search)
    ↓ stocke dans
Log Analytics Workspace (Canada East)
    ├── table "requests"       → invocations Azure Functions
    ├── table "dependencies"   → spans OTel (saga steps, SB calls)
    ├── table "traces"         → logs applicatifs (Warning+)
    ├── table "exceptions"     → erreurs avec stack trace
    └── table "customMetrics"  → métriques EMT (messages_sent, circuit_state…)
```

> ⚠️ **Distinction fondamentale :**
> - **INGESTION** = trafic entrant vers Log Analytics (facturé ~2,30 USD/GB · 5 GB/mois gratuits)
> - **STOCKAGE** = données déjà ingérées (gratuit 90 jours · archive ~0,02 USD/GB/mois)
>
> Application Insights est un **collecteur-façade** : il ne stocke rien lui-même depuis 2020. Toutes les données sont dans Log Analytics. "Purger AppInsights" = réduire la rétention dans Log Analytics.

#### Stratégie de contrôle des coûts — 3 leviers

| Levier | Où configurer | Effet |
|---|---|---|
| **Daily Cap** | Azure Portal → Log Analytics → Usage → Daily Cap | Plafond dur : 167 MB/jour = 5 GB/mois max |
| **Niveau de log AppInsights** | `host.json` : `"ApplicationInsights": "Warning"` | Seuls Warning/Error ingérés — Information reste en console locale |
| **Sampling adaptatif** | `host.json` : `maxTelemetryItemsPerSecond: 5` | Réduit le volume à fort débit, Exceptions et Requests toujours conservées |

#### Rétention recommandée pour RAMQ (CAI 7 ans)

| Table | Rétention interactive | Archive (CAI) |
|---|---|---|
| `exceptions` | 90 jours | 7 ans (~0,02 USD/GB/mois) |
| `requests` | 90 jours | 7 ans |
| `traces` | 30 jours | 3 ans |
| `dependencies` | 30 jours | 3 ans |
| `customMetrics` | 30 jours | 1 an |

> Configuration : Azure Portal → Log Analytics → Tables → sélectionner chaque table → Retention settings.

---

### 13.3.3 Analyse de coût mensuel — Routing Slip Booking (3 étapes)

#### Formule de calcul

```
Coût mensuel = MAX(0, Volume ingéré (GB) - 5 GB) × 2,30 USD/GB

Volume ingéré = Σ (items non samplés × taille)
              + Σ (items samplés × taille × taux de sampling)

Items jamais samplés (toujours facturés) : requests, exceptions
Items samplés : traces, dependencies (jusqu'à maxTelemetryItemsPerSecond)
```

Chaque item Application Insights = JSON enrichi (opération, cloud, session, customDimensions) → **~1-2 KB par item**.

---

#### Télémétrie générée par app

**App 1 — Activateur** *(HTTP trigger → construit SlipEnvelope → publie sur Service Bus)*

| Table | Quantité | Taille | Total | Samplé ? |
|---|---|---|---|---|
| `requests` | 1 (invocation HTTP) | 2 KB | 2 KB | ❌ Jamais |
| `dependencies` | 1 (SB send `PublishAsync`) | 2 KB | 2 KB | ✅ Oui |
| `traces` — Information | 2 (RoutingSlipBuilder + publish log) | 1 KB | 2 KB | ✅ Oui |
| `traces` — Warning | 0 (chemin nominal) | — | 0 KB | — |
| `customMetrics` | agrégés / 60 sec | ~0,1 KB | ~0 KB | ❌ Jamais |
| **Total Information** | | | **~6 KB** | |
| **Total Warning** | | | **~4 KB** | |

---

**App 2 — Worker, Étape 1 : ReserverVoiture** *(SB trigger → BookCarActivity → publie vers ReserverHotel)*

| Table | Quantité | Taille | Total | Samplé ? |
|---|---|---|---|---|
| `requests` | 1 (SB trigger) | 2 KB | 2 KB | ❌ Jamais |
| `dependencies` | 3 (SB receive + span `routing_slip.step` + span `booking.car.reserve` + SB send) | 2 KB | 6 KB | ✅ Oui |
| `traces` — Information | 3 (Info + Warning + Error = BookCarActivity, lignes 45-55) + 2 (RoutingSlipExecutor: début étape + avance vers) | 1 KB | 5 KB | ✅ Oui |
| `traces` — Warning | 1 (Warning BookCarActivity) + 0 RoutingSlipExecutor | 1 KB | 1 KB | ✅ Oui |
| `traces` — ForSlipStep (journal R16) | 1 (JournalEntry.ForSlipStep Completed) | 1 KB | 1 KB | ✅ Oui |
| **Total Information** | | | **~14 KB** | |
| **Total Warning** | | | **~10 KB** | |

---

**App 2 — Worker, Étape 2 : ReserverHotel** *(identique à Étape 1)*

| | Information | Warning |
|---|---|---|
| Total | **~14 KB** | **~10 KB** |

---

**App 2 — Worker, Étape 3 : ReserverVol** *(dernière étape → ForSlipComplete, pas de SB send)*

| Table | Quantité | Taille | Total |
|---|---|---|---|
| `requests` | 1 | 2 KB | 2 KB |
| `dependencies` | 2 (SB receive + spans) | 2 KB | 4 KB |
| `traces` Info | 4 (BookFlightActivity logs + executor) | 1 KB | 4 KB |
| `traces` Warning | 1 | 1 KB | 1 KB |
| `traces` ForSlipComplete | 1 | 1 KB | 1 KB |
| **Total Information** | | | **~11 KB** |
| **Total Warning** | | | **~8 KB** |

---

#### Récapitulatif par saga complète — chemin nominal (0 retry)

| App | Invocations | Information | Warning |
|---|---|---|---|
| **Activateur** | 1 | 6 KB | 4 KB |
| **Worker** ReserverVoiture | 1 | 14 KB | 10 KB |
| **Worker** ReserverHotel | 1 | 14 KB | 10 KB |
| **Worker** ReserverVol | 1 | 11 KB | 8 KB |
| **TOTAL saga nominale** | **4 invocations** | **~45 KB** | **~32 KB** |

---

#### Impact des retries sur le coût

> ⚠️ **Chaque retry = une nouvelle invocation Azure Functions.** Pour le retry exponentiel (sans session), EMT crée un nouveau message avec un nouveau `MessageId` — c'est une **nouvelle entrée facturable** dans Application Insights.

**Télémétrie d'un retry exponentiel :**

| Table | Contenu | Taille |
|---|---|---|
| `requests` | Nouvelle invocation SB (nouveau `MessageId`) | 2 KB |
| `dependencies` | SB receive + re-schedule (send différé) | 4 KB |
| `traces` Warning | RetryPolicyHandler: "Retry exponentiel tentative N/10" | 1 KB |
| `traces` Warning | RoutingSlipExecutor: "RetryExponential à l'étape..." | 1 KB |
| **Total par retry** | | **~8 KB** |

**Télémétrie d'un DLQ (MaxDeliveryCount atteint) :**

| Table | Contenu | Taille |
|---|---|---|
| `requests` | Invocation finale | 2 KB |
| `dependencies` | SB receive + DLQ send | 4 KB |
| `traces` Warning | "Nombre maximal de tentatives atteint → DLQ" | 1 KB |
| `exceptions` | Exception EMT (si Fault) | 3 KB |
| **Total DLQ** | | **~10 KB** |

---

#### Coût réel selon le taux de retry

**Formule :**
```
Volume/saga = Volume nominal + (nb retries moyen × 8 KB/retry)
```

| Taux retry | Retries moy./saga | Volume/saga (Warning) | Δ vs nominal |
|---|---|---|---|
| 0 % (chemin parfait) | 0 | 32 KB | référence |
| 5 % de sagas avec 1 retry | 0,05 | 32,4 KB | +1 % |
| 10 % avec 2 retries | 0,2 | 33,6 KB | +5 % |
| 20 % avec 3 retries | 0,6 | 36,8 KB | +15 % |
| Infrastructure dégradée (50 % avec 5 retries) | 2,5 | 52 KB | +63 % |
| Outage total (100 % → DLQ, 10 retries) | 10 | 112 KB | +250 % |

> 💡 **Pour RAMQ :** en production stable, 5-10 % de sagas avec 1-2 retries est normal. Le volume réel est ~5-10 % supérieur au calcul nominal. C'est négligeable.
> En cas d'incident Service Bus (outage partiel), le taux de retry peut exploser → c'est pourquoi le **Daily Cap est non-négociable** : il protège contre les pics de coût en cas d'infrastructure dégradée.

---

---

#### Base de calcul — nominal vs worst case

```
MaxDeliveryCount = 10 (config RetryPolicy)
→ 1 tentative initiale + 9 retries max avant DLQ

Worst case par étape = 1 tentative + 9 retries + 1 DLQ
  = 14 KB + (9 × 8 KB) + 10 KB = 96 KB/étape

Worst case par saga (3 étapes, tous les retries épuisés) :
  Activateur          :   4 KB  (ne retente pas)
  + Step 1 worst case :  96 KB
  + Step 2 worst case :  96 KB  (si Step 1 finit par réussir)
  + Step 3 worst case :  96 KB  (si Step 2 finit par réussir)
  = ~292 KB/saga Warning — worst case absolu
```

> ⚠️ Le worst case se produit lors d'un **incident infrastructure** (Service Bus throttling, panne réseau). C'est exactement le moment où le volume de télémétrie explose — et où le **Daily Cap est indispensable**.

---

#### Scénario A — Information, worst case

```
host.json : "ApplicationInsights": "Information"
Base worst case : ~400 KB/saga (retries + logs détaillés)
```

| Sagas/mois | Calcul | Volume | Coût ingestion |
|---|---|---|---|
| 1 000 | 1 000 × 400 KB | 400 MB | **Gratuit** |
| 10 000 | 10 000 × 400 KB | 4 GB | **Gratuit** |
| **12 500** | **12 500 × 400 KB** | **5 GB** | **← seuil gratuit** |
| 50 000 | 50 000 × 400 KB | 20 GB | **(20-5)×2,30 = 34,50 USD** |
| 100 000 | 100 000 × 400 KB | 40 GB | **(40-5)×2,30 = 80,50 USD** |
| 300 000 | 300 000 × 400 KB | 120 GB | **(120-5)×2,30 = 264,50 USD** |

> ⚠️ En incident, 12 500 sagas avec retries max dépassent déjà 5 GB. Sans Daily Cap → **facture imprévisible**.

---

#### Scénario B — Warning, worst case

```
host.json : "ApplicationInsights": "Warning"
Base worst case : ~292 KB/saga
```

| Sagas/mois | Calcul | Volume | Coût ingestion |
|---|---|---|---|
| 1 000 | 1 000 × 292 KB | 292 MB | **Gratuit** |
| 10 000 | 10 000 × 292 KB | 2,9 GB | **Gratuit** |
| **17 000** | **17 000 × 292 KB** | **5 GB** | **← seuil gratuit** |
| 50 000 | 50 000 × 292 KB | 14,6 GB | **(14,6-5)×2,30 = 22,08 USD** |
| 100 000 | 100 000 × 292 KB | 29,2 GB | **(29,2-5)×2,30 = 55,66 USD** |
| 300 000 | 300 000 × 292 KB | 87,6 GB | **(87,6-5)×2,30 = 190,08 USD** |

---

#### Scénario C — Warning + Sampling, worst case

```
host.json : "ApplicationInsights": "Warning"
            maxTelemetryItemsPerSecond: 5
            excludedTypes: "Exception;Request"
```

Les `requests` (invocations) **ne sont jamais samplées** — en worst case avec 10 retries × 3 étapes = 30 invocations non samplées par saga.

```
Volume effectif worst case :
  requests non samplés = 30 invocations × 2 KB = 60 KB (toujours facturé)
  traces + dependencies samplés à 80% = (292-60) KB × 20% = 46 KB
  Total ≈ 106 KB/saga
```

| Sagas/mois | Calcul | Volume | Coût ingestion |
|---|---|---|---|
| 10 000 | 10 000 × 106 KB | 1,1 GB | **Gratuit** |
| **47 000** | **47 000 × 106 KB** | **5 GB** | **← seuil gratuit** |
| 100 000 | 100 000 × 106 KB | 10,6 GB | **(10,6-5)×2,30 = 12,88 USD** |
| 300 000 | 300 000 × 106 KB | 31,8 GB | **(31,8-5)×2,30 = 61,74 USD** |

---

#### Scénario D — Daily Cap 167 MB/jour (configuration actuelle)

```
Azure Portal : Daily Cap = 167 MB/jour = 5 GB/mois
```

| Scénario | Résultat |
|---|---|
| Nominal (0 retry) | **Gratuit** — 32 KB/saga, seuil à 160K sagas/mois |
| Worst case (tous retries) | **Gratuit** — cap bloque à 167 MB/jour |
| Incident Service Bus | **Gratuit** — cap protège quelle que soit la tempête |

> ✅ **Le Daily Cap est le seul mécanisme qui protège contre le worst case.**
> Sans Daily Cap, un incident Service Bus de 24h peut générer des centaines de GB de retries en télémétrie → facture imprévisible.
> **La configuration actuelle (`host.json` + Daily Cap Azure Portal) garantit ≤ 5 GB/mois dans tous les scénarios.**

---

#### Coût de stockage archive (CAI 7 ans)

Pour 5 GB ingérés/mois (cap maximum) :

| Période | Données cumulées | Coût archive/mois |
|---|---|---|
| 1 an | 60 GB | **1,20 USD** |
| 3 ans | 180 GB | **3,60 USD** |
| 7 ans | 420 GB | **8,40 USD** |

> 💡 Le coût total sur 7 ans pour un pilote à 5 GB/mois : **~0 USD ingestion + ~350 USD archive** = moins de 50 USD/an pour la conformité CAI complète.

---

### 13.4 ILogger vs OpenTelemetry — la nuance critique

> 💡 **C'est probablement la confusion #1 d'un junior qui débute en observabilité.** Le code écrit `_logger.LogInformation(...)`, mais qu'est-ce qui se passe vraiment ? Est-ce que ça « part » dans Application Insights ? Sous quel nom de table ? Avec quels filtres ? La réponse demande de comprendre 3 couches.

#### 13.4.0 Vision claire — la phrase de référence à mémoriser

> 🧭 **`ILogger` est l'API qu'on écrit. OpenTelemetry est le pipeline qui transporte.** Ce sont **deux outils complémentaires**, pas concurrents.
>
> - On **écrit toujours** du code applicatif avec `ILogger<T>` injecté en DI. C'est l'abstraction Microsoft standard, totalement indépendante du backend.
> - On **configure le transport** vers Azure Monitor une seule fois dans `Program.cs` via OpenTelemetry. À partir de là, **chaque `_logger.LogInformation()` devient automatiquement** un événement OTel qui voyage vers Application Insights, sans que le code applicatif ne le sache.
> - Cette indépendance est **un atout DFO** : on peut tester en local avec `.AddConsoleExporter()`, déployer en prod avec `.AddAzureMonitorLogExporter()`, et le code applicatif **ne change jamais**.

#### 13.4.0.bis Les trois signaux d'observabilité — définitions, frontières et exemples EMT

> 💡 **Pour un junior :** beaucoup de développeurs croient qu'OpenTelemetry = traces. C'est faux. OTel a **3 piliers distincts**, chacun répond à une question différente en production. Confondre les trois mène à des dashboards inutiles et des incidents impossibles à diagnostiquer.

---

##### Signal 1 — LOGS : « Pourquoi ce message précis a-t-il échoué ? »

Un log est un **événement discret, daté, textuel** qui décrit quelque chose qui s'est passé à un instant précis. C'est le **carnet de bord** de l'application.

**Caractéristiques :**
- Lié à un **message individuel** (via `MessageId`, `CorrelationId` dans `BeginScope`)
- Contient le **contexte métier complet** (DossierId, SlipId, étape, tentative)
- Utile pour **diagnostiquer un cas précis** — "pourquoi LE message X a-t-il échoué ?"
- Stocké dans Log Analytics : table `AppTraces` (ou `traces` en syntaxe AppInsights)

**API .NET :** `ILogger<T>` injecté en DI

**Exemples concrets dans EMT RoutingSlip Booking :**
```csharp
// BookCarActivity.cs — Log d'erreur avec contexte complet
_logger.LogError(
    "[{Step}] Service voiture en panne permanente (CRASH-) — tentative {Attempt}, SlipId={SlipId}",
    ctx.StepName, ctx.Attempt, ctx.SlipId);
// → AppInsights : Severity=Error, customDimensions.SlipId, customDimensions.Step

// RetryPolicyHandler.cs — Log warning avec scope BeginScope (R13)
// Le scope injecte automatiquement MessageId/CorrelationId dans TOUS les logs suivants
using var scope = _logger.BeginScope(new Dictionary<string, object?>
{
    ["MessageId"]     = message.MessageId,
    ["CorrelationId"] = message.CorrelationId,
    ["DeliveryCount"] = attempt
});
_logger.LogWarning(
    "Retry exponentiel : nombre maximal de tentatives atteint, envoi en file des lettres mortes. MessageId={MessageId} Tentative={DeliveryCount}",
    message.MessageId, attempt);
// → AppInsights : customDimensions.MessageId, customDimensions.DeliveryCount automatiquement présents
```

**Où chercher dans AppInsights :**
```kusto
// Trouver POURQUOI un message a échoué
traces
| where customDimensions.MessageId == "9ac1eed5a0ae4a79b4727da57713a234"
| order by timestamp asc
| project timestamp, severityLevel, message, customDimensions
```

---

##### Signal 2 — TRACES (Spans) : « Où en est ce message, combien de temps a pris chaque étape ? »

Une trace est un **arbre de spans** qui représente le **parcours complet d'une opération** à travers plusieurs services. Chaque span = une étape avec une durée mesurée.

**Caractéristiques :**
- Représente un **flux de bout en bout** (Activateur → ReserverVoiture → ReserverHotel → ReserverVol)
- Tous les spans d'une même saga partagent le **même `operation_Id`** (W3C TraceId — cf. P4-T3)
- Mesure la **latence** de chaque étape (durée en ms)
- Stocké dans Log Analytics : table `AppDependencies` + `AppRequests` (ou `dependencies`/`requests`)

**API .NET :** `ActivitySource` + `Activity` (System.Diagnostics)

**Exemples concrets dans EMT RoutingSlip Booking :**
```csharp
// RoutingSlipExecutor.cs — Span qui enveloppe toute l'exécution d'une étape
using var stepActivity = MessagingActivitySource.Source.StartActivity(
    "routing_slip.step",
    ActivityKind.Internal);
stepActivity?.SetTag("slip.id",    envelope.Header.SlipId);
stepActivity?.SetTag("slip.step",  currentStep.Name);
stepActivity?.SetTag("slip.cursor", envelope.Cursor);
// → AppInsights : dependency avec Name="routing_slip.step", duration mesuré

// BookCarActivity.cs — Span métier enfant du routing_slip.step
using var span = BookingTelemetry.Source.StartActivity("booking.car.reserve", ActivityKind.Client);
span?.SetTag("booking.reservation_id", ctx.Arguments.ReservationId.ToString());
span?.SetTag("booking.car.model",      ctx.Arguments.CarModel);
span?.SetStatus(ActivityStatusCode.Ok);
// → AppInsights : dependency enfant visible dans End-to-End Transaction view
```

**Ce qu'on voit dans AppInsights End-to-End Transaction view :**
```
operation_Id: c68a464782c127220eaa209fe8cf0928
│
├── function BookingActivateur      (6.1 s)  ← HTTP trigger
│   └── messaging.send              (3.1 s)  ← envoi vers ReserverVoiture
│
├── function ReserverVoiture        (5.4 s)  ← SB trigger, même operation_Id grâce à P4-T3
│   ├── messaging.consume           (5.3 s)
│   │   └── routing_slip.step       (5.2 s)
│   │       └── booking.car.reserve (50 ms) ← span métier BookCarActivity
│
├── function ReserverHotel          (565 ms) ← idem
└── function ReserverVol            (376 ms) ← idem
```

**Où chercher dans AppInsights :**
```kusto
// Reconstituer le parcours complet d'une saga
union requests, dependencies
| where operation_Id == "c68a464782c127220eaa209fe8cf0928"
| order by timestamp asc
| project timestamp, itemType, name, duration, success
```

---

##### Signal 3 — METRICS : « Combien de messages échouent, à quel taux, sur quelle période ? »

Une métrique est une **mesure agrégée dans le temps**. Elle ne dit pas QUEL message a échoué, mais **COMBIEN** ont échoué sur les 5 dernières minutes.

**Caractéristiques :**
- Agrégation statistique — **jamais de MessageId en tag** (cardinalité limitée à ~1000 valeurs distinctes)
- Déclencheur d'**alertes** : "taux d'erreur > 5% sur 10 min"
- Utile pour les **dashboards opérationnels en temps réel** (Live Metrics, Workbooks)
- Stocké dans Log Analytics : table `AppMetrics` (ou `customMetrics`)

**API .NET :** `System.Diagnostics.Metrics.Meter` / `Counter<T>` / `Histogram<T>`

**Exemples concrets dans EMT (`EMTInstrumentation.cs` + `IMetricsProvider`) :**
```csharp
// Compteur — nombre de messages envoyés (agrégé par queue)
_metrics.IncrementMessagesSent(entityName, entityType);
// → AppInsights : customMetrics.Name="emt.messages.sent", dimensions: queue="sbq-rcp-routingslipcarreservation-unit"

// Histogramme — latence d'envoi (distribution statistique)
_metrics.RecordSendDuration(sw.Elapsed.TotalMilliseconds, entityName);
// → AppInsights : customMetrics.Name="emt.messages.send_duration_ms", p50/p95/p99

// Compteur d'erreurs — nombre de compensations routing slip
_metrics.IncrementRoutingSlipCompensation(slipName, faultType);
// → AppInsights : customMetrics.Name="emt.routing_slip.compensation"
// → Peut déclencher une alerte Azure Monitor : si count > 10/min → PagerDuty
```

**Dashboard KQL metrics :**
```kusto
// Taux d'erreur par queue — déclencheur d'alerte
customMetrics
| where name == "emt.routing_slip.compensation"
| summarize total = sum(value) by bin(timestamp, 5m), tostring(customDimensions.slip_name)
| render timechart
```

---

##### Les trois signaux en action — scénario DFO réel

> **Alerte 14h32 :** "Taux de compensation RoutingSlip Booking > 15% sur les 10 dernières minutes"

**Étape 1 — Métriques confirment l'alerte et donnent la magnitude**
```kusto
customMetrics | where name == "emt.routing_slip.compensation"
| summarize count() by bin(timestamp, 1m)
// → 47 compensations en 10 min (normal : < 3/min). Incident confirmé.
```

**Étape 2 — Traces identifient QUELS messages et QUELLE étape**
```kusto
dependencies | where name == "routing_slip.step" and success == false
| where timestamp > ago(10m)
| project operation_Id, customDimensions.slip_step, duration
// → Tous les échecs sont sur step="ReserverVol", durée > 4s. Service vol en panne.
```

**Étape 3 — Logs expliquent POURQUOI le service vol échoue**
```kusto
traces | where severityLevel >= 3  -- Warning et au-dessus
| where customDimensions.SlipStep == "ReserverVol"
| where timestamp > ago(10m)
| project timestamp, message, customDimensions.MessageId
// → "HTTP 504 Gateway Timeout — service réservation vol inaccessible"
// → MessageId identifiable → support peut retrouver le dossier RAMQ exact
```

**Résolution complète en < 5 minutes** grâce aux 3 signaux complémentaires.

---

##### Tableau de décision — quel signal pour quelle question ?

| Question | Signal | Pourquoi |
|---|---|---|
| Le système est-il sain en ce moment ? | **Metrics** | Agrégats temps réel, faible coût, alertable |
| Quelle étape est lente ou en erreur ? | **Traces** | Vue parcours avec durées, corrélation cross-services |
| Pourquoi CE message précis a-t-il échoué ? | **Logs** | Contexte individuel complet avec MessageId |
| Ce message a-t-il été traité ? (audit CAI) | **Journal MTJ** | Rétention 7 ans, immuable, indépendant OTel |
| Quelle est la tendance sur 30 jours ? | **Metrics** | Agrégation historique, rapport direction |
| Comment reconstituer le parcours exact d'un dossier ? | **Traces + Logs** | `operation_Id` relie les deux |

> **Règle d'or :** les **métriques alertent**, les **traces localisent**, les **logs expliquent**. Les trois sont nécessaires — aucun ne remplace les autres.

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
// host.json (Azure Function dotnet-isolated) — recommandation v1.0
// ⚠️ Template de base — pour la version complète et validée avec filtres anti-bruit AppInsights
//    voir §13.10.8 (AppInsightsNoiseFilter + logLevel complet).
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default":                                 "Information",

      // ── Logs infrastructure Azure Functions ───────────────────────────────────
      "Function":                                "None",
      // ↑ Supprime "Executing/Executed 'Functions.X'" et "Trigger Details"
      //   Ces logs sont du bruit infrastructure — catégorie Function.{NomDeLaFunction}

      "Host":                                    "Warning",
      // ↑ Supprime les logs de démarrage du host (Warmup Extension, etc.)

      "Microsoft":                               "Warning",
      "Azure.Identity":                          "None",
      "Microsoft.Identity":                      "None",
      "Grpc":                                    "None",
      "Microsoft.Azure.Functions.Worker.Grpc":   "None",

      // ── Logs métier RAMQ ────────────────────────────────────────────────────
      "RAMQ.COM.EnterpriseMessageTransit":        "Information",
      "RAMQ":                                     "Information"
    },
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled":             true,
        "excludedTypes":         "Exception;Request",
        // ↑ Exceptions et invocations : jamais échantillonnées (toujours 100% visibles)
        "maxTelemetryItemsPerSecond": 20
      },
      "enableLiveMetricsFilters": true
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

✅ **Résolu R13 :** `BeginScope` systématiques ajoutés dans `Producer.PublishCoreAsync`, `BaseConsumer.DeserializeMessageAsync` (scope actif jusqu'au settlement), `RetryPolicyHandler`, `RoutingSlipExecutor.RunAsync`. Chaque log EMT enrichit désormais `customDimensions` avec `MessageId`, `CorrelationId`, `SessionId`.

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

### 13.4.6 Niveaux de log dans le code métier RAMQ — guide opposable

> 💡 **Pour un junior — pourquoi cette section :** BP-5 (§13.4.4) survole la hiérarchie des niveaux. Cette sous-section approfondit, **avec des exemples RAMQ concrets**, pour répondre à la question : *« Mon code métier doit logger cet événement — quel niveau ? »*. C'est une **règle de revue de code opposable** — pas une suggestion.
>
> 🟢 **Focus principal : le code métier RAMQ** (consumers, activities, services applicatifs). La lib EMT a ses propres conventions (cf. §13.4.4 BP-5).

#### 13.4.6.1 Tableau de décision rapide

> **Le test des 3 questions :** quand tu hésites sur le niveau, demande-toi :
> 1. **Cet événement est-il attendu dans un fonctionnement normal ?** Oui → Information ou Debug. Non → Warning, Error ou Critical.
> 2. **Cela demande-t-il une action humaine ?** Oui → Warning (à examiner), Error (à corriger), Critical (à corriger immédiatement). Non → Information / Debug.
> 3. **Le système peut-il continuer ?** Oui → Warning. Non → Error ou Critical.

```
Réponse aux 3 questions → niveau de log :

│   Attendu ?    │ Action humaine ? │ Système OK ? │  Niveau  │
├────────────────┼──────────────────┼──────────────┼──────────┤
│ Oui            │ Non              │ Oui          │ Information │
│ Oui (debug)    │ Non              │ Oui          │ Debug      │
│ Non            │ Oui (vérifier)   │ Oui          │ Warning    │
│ Non            │ Oui (corriger)   │ Partiel      │ Error      │
│ Non            │ Oui (URGENT)     │ Non          │ Critical   │
```

#### 13.4.6.2 LogTrace — pour le diagnostic ultra-détaillé

**Quand l'utiliser :**
- Tracer le parsing d'un message volumineux (chaque champ)
- Tracer une boucle interne (sortir l'état à chaque itération)
- Valider qu'une condition rare est atteinte pendant un debug ponctuel

**Quand NE PAS l'utiliser :**
- Tout le temps en production — `LogTrace` est **toujours désactivé** en prod RAMQ
- Code métier nominal — préfère `LogDebug` ou `LogInformation`

**Exemple métier RAMQ :**

```csharp
// ✅ Approprié — détail d'un parser personnalisé qu'on diagnostique en dev
_logger.LogTrace("Champ HL7 segment={Segment} index={Idx} valeur='{Valeur}'",
    segment, idx, valeur);

// ❌ Inapproprié — événement qui mérite Information
_logger.LogTrace("Dossier {DossierId} validé", id);   // Devrait être LogInformation
```

**Coût en prod :** quasi-nul (filtré par F1 LogLevel avant OTel). **Volume attendu :** énorme — réservé au dev.

#### 13.4.6.3 LogDebug — pour les détails développeur

**Quand l'utiliser :**
- Détails d'implémentation utiles à un dev qui debug un problème
- Paramètres reçus, branchements pris, valeurs intermédiaires
- Tracking d'état interne dans une boucle métier

**Quand NE PAS l'utiliser :**
- Événements métier visibles à un auditeur ou agent CAI — utiliser `LogInformation`
- Erreurs récupérées — utiliser `LogWarning`
- Tout ce qui doit survivre en production

**Exemples métier RAMQ :**

```csharp
// ✅ Approprié — détail debug d'une logique de calcul
_logger.LogDebug("Calcul prime — DossierId={DossierId} BaseAssure={Base} Coefficient={Coeff} TotalCalcule={Total}",
    dossierId, baseAssure, coefficient, totalCalcule);

// ✅ Approprié — branchement métier rarement pris
if (assure.HasExemptionSpeciale)
{
    _logger.LogDebug("Branche exemption spéciale prise pour AssureId={InternalId}", assure.InternalId);
    // ... traitement spécifique
}

// ❌ Inapproprié — étape majeure du flux métier
_logger.LogDebug("Validation admissibilité réussie");   // Devrait être LogInformation
```

**Coût en prod :** quasi-nul si désactivé (par défaut chez RAMQ : `Warning` dans `host.json`). **Volume attendu :** élevé — désactivé en prod nominale, activé temporairement pour debug ciblé via `Logging:LogLevel:RAMQ.Pharmacie = "Debug"`.

#### 13.4.6.4 LogInformation — pour les événements métier nominaux

**🟢 C'est le niveau par défaut du code métier RAMQ.**

**Quand l'utiliser :**
- Étape majeure d'un processus métier franchie avec succès (validation, calcul, notification)
- Démarrage / fin d'une opération significative pour l'audit
- Décision métier prise (acceptation, rejet, escalade)
- Événement qui doit apparaître dans la timeline d'un dossier pour un agent CAI

**Quand NE PAS l'utiliser :**
- Détail d'implémentation — préfère `LogDebug`
- Erreur récupérée — préfère `LogWarning`
- Boucle interne — `LogTrace` ou rien du tout
- Tâche périodique répétitive (toutes les 100ms) — risque d'inonder Log Analytics

**Exemples métier RAMQ :**

```csharp
// ✅ Étape métier franchie
_logger.LogInformation(
    "Dossier {DossierId} validé pour AssureId={AssureIdHash} avec statut {Statut}",
    dossierId, HashTruncate(assureId), statut);

// ✅ Décision métier
_logger.LogInformation(
    "Demande admissibilité {DossierId} acceptée — montant calculé {MontantCAD:N2} CAD",
    dossierId, montant);

// ✅ Notification d'un événement métier important
_logger.LogInformation(
    "Notification envoyée pour {DossierId} via canal {Canal} — confirmation {ConfirmationId}",
    dossierId, canal, confirmationId);

// ❌ Trop verbeux — chaque opération CRUD ne mérite pas LogInformation
_logger.LogInformation("Lecture en base pour DossierId={Id}", id);   // Préfère LogDebug

// ❌ Détail d'implémentation
_logger.LogInformation("Cache hit pour {Key}", key);   // Préfère LogDebug
```

**Coût en prod :** modéré — c'est ce qui consomme le plus de volume Log Analytics. **C'est gardé en prod** chez RAMQ pour l'auditabilité.

#### 13.4.6.5 LogWarning — pour les anomalies récupérées

**Quand l'utiliser :**
- Anomalie attendue mais inattendue (retry transitoire qui réussit)
- Dégradation de performance détectée (timeout proche, throughput abaissé)
- Donnée métier suspecte mais traitée (champ optionnel manquant, valeur hors gamme)
- Comportement à surveiller mais qui n'a pas bloqué la transaction
- Échec d'une dépendance secondaire (le journal A5 échoue mais l'envoi a réussi)

**Quand NE PAS l'utiliser :**
- Erreur métier attendue (« dossier déjà traité ») — utilise `LogInformation`
- Erreur non récupérable — utilise `LogError`
- Branche métier rare mais valide — utilise `LogDebug` ou `LogInformation`

**Exemples métier RAMQ :**

```csharp
// ✅ Anomalie récupérée
try { await _pharmacieApi.ValiderAsync(id, ct); }
catch (TransientException ex)
{
    _logger.LogWarning(ex,
        "Pharmacie API timeout pour {DossierId} — retry programmé",
        dossierId);
    throw new ExponentialRetryException("Pharmacie timeout", ex);
}

// ✅ Donnée métier suspecte mais traitée
if (assure.AdresseValidee == null)
{
    _logger.LogWarning(
        "Adresse non validée pour {DossierId} — utilisation adresse historique",
        dossierId);
    adresse = await GetAdresseHistoriqueAsync(assure.InternalId, ct);
}

// ✅ Dégradation détectée
if (sw.ElapsedMilliseconds > _slaMs)
{
    _logger.LogWarning(
        "SLA potentiel — {Operation} pour {DossierId} a pris {Duree}ms (cible {SlaMs}ms)",
        nameof(ValiderAdmissibiliteAsync), dossierId, sw.ElapsedMilliseconds, _slaMs);
}

// ❌ Anti-pattern — événement métier nominal n'est PAS un warning
_logger.LogWarning("Dossier {DossierId} rejeté", id);   // Rejet est métier valide
                                                        // → LogInformation
```

**Coût en prod :** faible — devrait représenter < 5 % du volume total. **Trigger d'alerte :** un volume Warning > 10 % du total signale un problème systémique à investiguer.

#### 13.4.6.6 LogError — pour les erreurs non récupérables

**Quand l'utiliser :**
- Exception inattendue qui empêche le traitement métier
- Erreur permanente qui requiert un correctif (bug, donnée corrompue)
- Échec d'une dépendance critique sans alternative (référentiel principal indisponible)
- Tout cas où le dossier ne peut pas avancer

**Quand NE PAS l'utiliser :**
- Erreur métier prévue (« dossier expiré ») — c'est du `LogInformation` pour audit
- Retry transitoire — c'est du `LogWarning`
- Échec d'une dépendance secondaire (journal A5) qui n'empêche pas l'opération — c'est `LogWarning`

**Exemples métier RAMQ :**

```csharp
// ✅ Exception inattendue
try { await ProcessDossierAsync(ctx, ct); }
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogError(ex,
        "Erreur non gérée traitement {DossierId} — DLQ programmé",
        ctx.Message?.DossierId);
    throw new ImmediateDLQException("Erreur inattendue", ex);
}

// ✅ Donnée corrompue
if (dossier.NumeroAssuranceMaladie?.Length != 12)
{
    _logger.LogError(
        "Donnée corrompue — DossierId={DossierId} NAM invalide (longueur={Len})",
        dossier.Id, dossier.NumeroAssuranceMaladie?.Length ?? 0);
    throw new ImmediateDLQException("NAM invalide");
}

// ✅ Dépendance critique indisponible
catch (RegistreCentralIndisponibleException ex)
{
    _logger.LogError(ex,
        "Registre central RAMQ indisponible pour {DossierId} — escalade nécessaire",
        dossierId);
    throw;
}

// ❌ Anti-pattern — erreur métier attendue n'est PAS un Error
_logger.LogError("Dossier {DossierId} déjà traité", id);   // C'est un cas métier valide
                                                            // → LogInformation
```

**Coût en prod :** très faible. **Trigger d'alerte :** chaque `LogError` devrait potentiellement déclencher une alerte (selon le filtre KQL). Si vous avez 100 LogError/jour en nominal, c'est que vous abusez du niveau — il faut redescendre les vrais warnings en `LogWarning`.

#### 13.4.6.7 LogCritical — pour les pannes système majeures

**Quand l'utiliser :**
- DI cassée au démarrage (configuration manquante, service non enregistré)
- Référentiel principal RAMQ complètement inaccessible (pas juste lent — totalement indisponible)
- Détection d'un état système incohérent grave (fichiers de configuration manipulés, certificats expirés)
- Tout ce qui doit réveiller PagerDuty à 3h du matin **sans hésitation**

**Quand NE PAS l'utiliser :**
- Un dossier qui échoue — c'est `LogError`
- Une dépendance qui timeout — c'est `LogWarning` (transitoire) ou `LogError` (permanent)

**Exemples métier RAMQ :**

```csharp
// ✅ DI / configuration critique manquante
public class PharmacieClient
{
    public PharmacieClient(IOptions<PharmacieOptions> options, ILogger<PharmacieClient> logger)
    {
        if (string.IsNullOrEmpty(options.Value.WcfEndpoint))
        {
            logger.LogCritical(
                "PharmacieClient — WcfEndpoint absent dans la config — démarrage impossible");
            throw new InvalidOperationException("WcfEndpoint requis");
        }
    }
}

// ✅ Référentiel central indisponible pendant > 5 min
if (_registreHealthCheck.IsDownLongerThan(TimeSpan.FromMinutes(5)))
{
    _logger.LogCritical(
        "Registre central RAMQ indisponible depuis {DurationMin} min — tous les flux affectés",
        elapsedMin);
}

// ❌ Anti-pattern — un seul timeout n'est pas Critical
_logger.LogCritical("Timeout WCF Pharmacie pour DossierId=...");   // Devrait être LogWarning
                                                                    // (retry) ou LogError (épuisé)
```

**Coût en prod :** quasi-nul (très rare). **Trigger d'alerte :** chaque `LogCritical` réveille l'astreinte sans filtre.

#### 13.4.6.8 Anti-patterns à éviter — examen en revue de code

> Ces 10 anti-patterns sont **opposables en revue de code** — un PR qui en contient doit être corrigé.

| # | Anti-pattern | Pourquoi c'est mauvais | Correction |
|---|---|---|---|
| **AP-1** | `LogError` sur une erreur métier prévue (« dossier déjà traité ») | Pollue les alertes, masque les vraies erreurs | Remplacer par `LogInformation` |
| **AP-2** | `LogInformation` dans une boucle (« lecture record N » × 10000) | Inonde Log Analytics, coût FinOps × 100 | Remplacer par `LogDebug`, ou logger uniquement le résumé |
| **AP-3** | `LogCritical` pour un événement non bloquant | Réveille PagerDuty à tort, alert fatigue | Remplacer par `LogError` ou `LogWarning` selon contexte |
| **AP-4** | `LogWarning` sans contexte (`_logger.LogWarning("Échec")`) | Inutile en investigation — sans `DossierId`, on ne peut rien faire | Ajouter placeholders nommés (BP-1) |
| **AP-5** | `LogTrace` pour un événement nominal de fin de processus | Désactivé en prod → audit perdu | Remplacer par `LogInformation` |
| **AP-6** | `LogError` qui swallow l'exception (`catch { logger.LogError(...); }` sans rethrow) | Erreur masquée, comportement imprévisible aval | Soit rethrow, soit convertir en `LogWarning` si vraiment récupéré |
| **AP-7** | Logger une exception en `LogInformation` ou `LogDebug` | Sévérité incorrecte → pas d'alerte déclenchée | Selon contexte : `LogWarning` (récupéré) ou `LogError` (non) |
| **AP-8** | Concaténer la stack trace dans le message (`$"{ex} {ex.StackTrace}"`) | Pollue le `messageTemplate`, casse l'agrégation | Passer l'exception en 1ᵉʳ argument : `LogError(ex, "Échec {DossierId}", id)` |
| **AP-9** | Log de `LogInformation` qui est en fait du marketing (`"Démarrage de l'application 🎉"`) | Bruit, faible valeur opérationnelle | À retirer ou passer en `LogDebug` |
| **AP-10** | Log de PII en clair (`LogInformation("NAM={Nam}", nam)`) | Violation CAI/RGPD | Hash tronqué (BP-4) |

#### 13.4.6.9 Cas d'usage RAMQ — exemples complets commentés

##### Cas 1 — Activity de validation admissibilité (Routing Slip)

```csharp
public sealed class ValiderAdmissibiliteActivity : IRoutingSlipActivity<ValiderArgs>
{
    private readonly IRegistreCentral _registre;
    private readonly ILogger<ValiderAdmissibiliteActivity> _logger;
    private readonly TimeSpan _slaCible = TimeSpan.FromMilliseconds(500);

    public async Task<ActivityResult> ExecuteAsync(
        ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        // BP-2 : scope englobant — toutes les lignes en dessous auront ces customDim
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["DossierId"]     = ctx.Arguments.DossierId,
            ["AssureIdHash"]  = HashTruncate(ctx.Arguments.AssureId),
            ["SlipId"]        = ctx.SlipId,
            ["StepName"]      = ctx.StepName
        });

        // LogDebug : détail diagnostic — désactivé en prod nominale
        _logger.LogDebug("Démarrage validation admissibilité");

        var sw = Stopwatch.StartNew();
        try
        {
            var verdict = await _registre.ValiderAsync(ctx.Arguments.DossierId, ct);
            sw.Stop();

            // LogWarning : SLA dépassé mais le traitement a réussi (anomalie récupérée)
            if (sw.Elapsed > _slaCible)
            {
                _logger.LogWarning(
                    "SLA admissibilité dépassé — {DureeMs}ms (cible {SlaMs}ms)",
                    sw.ElapsedMilliseconds, _slaCible.TotalMilliseconds);
            }

            if (!verdict.IsValid)
            {
                // LogInformation : rejet métier — événement nominal, à auditer
                _logger.LogInformation(
                    "Dossier non admissible — raison {RaisonCode} : {RaisonLibelle}",
                    verdict.RaisonCode, verdict.RaisonLibelle);
                return ActivityResult.Fault(new NonAdmissibleException(verdict.RaisonLibelle));
            }

            // LogInformation : succès nominal — événement audit clé
            _logger.LogInformation(
                "Admissibilité validée — DateEffet={DateEffet:yyyy-MM-dd} en {DureeMs}ms",
                verdict.DateEffet, sw.ElapsedMilliseconds);

            return ActivityResult.Next(vars => vars["DateValidation"] = DateTime.UtcNow);
        }
        catch (TransientException ex)
        {
            // LogWarning : retry programmé — anomalie attendue dans un système distribué
            _logger.LogWarning(ex, "Erreur transitoire admissibilité — retry programmé");
            return ActivityResult.RetryExponential("Erreur transitoire admissibilité", ex);
        }
        catch (Exception ex)
        {
            // LogError : exception non gérée — requiert investigation
            _logger.LogError(ex, "Erreur non gérée validation admissibilité — DLQ");
            return ActivityResult.Fault(ex);
        }
    }
}
```

##### Cas 2 — Service métier de calcul de prime

```csharp
public sealed class CalculPrimeService
{
    private readonly ILogger<CalculPrimeService> _logger;

    public PrimeResult Calculer(Dossier dossier, BaremeReglementaire bareme)
    {
        using var scope = _logger.BeginScope(new { dossier.Id, BaremeVersion = bareme.Version });

        // LogDebug : détail intermédiaire de calcul, utile en debug seulement
        _logger.LogDebug("Calcul prime — base={Base} coefficient={Coef}",
            dossier.BaseAssuree, bareme.CoefficientDomaine);

        if (dossier.HasExemptionSpeciale)
        {
            // LogInformation : branche métier importante — à auditer
            _logger.LogInformation(
                "Exemption spéciale appliquée — prime fixée à 0 pour exemption {ExemptionCode}",
                dossier.ExemptionCode);
            return PrimeResult.Exempte();
        }

        var prime = dossier.BaseAssuree * bareme.CoefficientDomaine;

        if (prime > _bareme.PlafondAlertePrime)
        {
            // LogWarning : valeur hors gamme habituelle, à examiner
            _logger.LogWarning(
                "Prime calculée supérieure au plafond d'alerte — {Prime:N2} > {Plafond:N2}",
                prime, _bareme.PlafondAlertePrime);
        }

        // LogInformation : résultat métier final — événement audit
        _logger.LogInformation("Prime calculée : {Prime:N2} CAD", prime);
        return PrimeResult.Calcule(prime);
    }
}
```

#### 13.4.6.10 Configuration recommandée par environnement

**`appsettings.Development.json` — pour les devs locaux :**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "RAMQ": "Debug",
      "RAMQ.COM.EnterpriseMessageTransit": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**`appsettings.Staging.json` — pour la pré-prod :**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "RAMQ": "Information",
      "RAMQ.COM.EnterpriseMessageTransit": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

**`host.json` Azure Functions — pour la prod :**
```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "RAMQ":                                    "Information",
      "RAMQ.COM.EnterpriseMessageTransit":        "Information",
      "Function":                                "None",
      "Microsoft":                               "Warning",
      "Host":                                    "Warning",
      "Azure.Identity":                          "None",
      "Grpc":                                    "None"
    }
  }
}
```
> Voir §13.10.8 pour la version complète avec les règles AppInsightsNoiseFilter et les annotations junior.

> 💡 **Astuce DFO :** pour activer `LogDebug` ponctuellement en prod (diagnostic d'incident), modifier la config dans Azure App Configuration → reload sans redéploiement → désactiver après le diagnostic.

#### 13.4.6.11 Checklist revue de code « LogLevel »

À cocher pour chaque PR qui contient du logging :

| ☑ | Question |
|---|---|
| ☐ | Chaque `LogXxx` utilise des placeholders nommés (BP-1) ? |
| ☐ | Aucun `LogError` sur une erreur métier prévue (AP-1) ? |
| ☐ | Aucun `LogInformation` dans une boucle de plus de 100 itérations (AP-2) ? |
| ☐ | Aucun `LogCritical` sur un événement récupérable (AP-3) ? |
| ☐ | Chaque `LogWarning` / `LogError` contient au moins `DossierId` ou équivalent métier (AP-4) ? |
| ☐ | Aucune PII en clair (AP-10, BP-4) ? |
| ☐ | Les exceptions sont passées en 1ᵉʳ argument, pas dans le message (AP-8) ? |
| ☐ | Le scope `BeginScope` est posé autour de l'unité de travail (BP-2) ? |
| ☐ | Chaque `LogWarning`/`LogError` récurrent a une métrique associée pour l'alerting (BP-7) ? |
| ☐ | La hiérarchie de catégorie suit `ILogger<ClasseCourante>` (BP-10) ? |

#### 13.4.6.12 Volume attendu par niveau — calibrage prod

À comparer en preprod / prod pour valider que les niveaux sont bien calibrés :

| Niveau | Volume cible (% du total) | Volume anormal |
|---|---|---|
| `LogTrace` | 0 % (filtré) | > 0 % |
| `LogDebug` | 0 % (filtré) | > 1 % → vérifier la config |
| `LogInformation` | 85-95 % | < 70 % ou > 99 % |
| `LogWarning` | 3-10 % | > 15 % → bug systémique |
| `LogError` | 0,1-2 % | > 5 % → incident en cours |
| `LogCritical` | < 0,01 % | > 0,1 % → bug calibrage |

> 🟠 **Si vous voyez > 5 % de `LogError` ou > 15 % de `LogWarning` en prod nominale**, ce n'est pas un problème de production — c'est un problème de **calibrage des niveaux** dans le code. À corriger avant de déployer.

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

#### Lot R13 — ✅ Livré — Scopes `BeginScope` systématiques dans la lib EMT

**Origine :** §13.7.3 + §13.8 O2/O3/O9

**Livré :**
- `Producer.PublishCoreAsync` : `BeginScope({MessageId, CorrelationId, SessionId, Target})`
- `BaseConsumer.DeserializeMessageAsync` : `BeginScope({MessageId, CorrelationId, SessionId, Consumer, Action})` — scope actif jusqu'au settlement (Complete/DLQ/Retry)
- `RetryPolicyHandler.HandleImmediateRetryAsync` + `HandleExponentialRetryAsync` : `BeginScope({MessageId, CorrelationId, SessionId, DeliveryCount})`
- `RoutingSlipExecutor.RunAsync` : `BeginScope({SlipId, SlipName, StepName, StepIndex, CorrelationId, Attempt})`

#### Lot R14 — ✅ Livré — W3C TraceContext propagation Producer → Consumer

**Origine :** §13.8 O4/O5 ; `tracing.md:139`

**Livré (P4-T2) :**
1. `AzureMessagingProvider.SendAsync` + `SendBatchAsync` : injectent `traceparent` (et `tracestate` si présent) dans `ApplicationProperties` — Producer → Service Bus → Consumer en un seul arbre de trace.
2. `AzureFunctionMessagingAdapter.GetTraceparent()` : lit `traceparent` depuis `ApplicationProperties` du message reçu.
3. `AzureConsumerTelemetry.BeginReceive(traceparent)` : crée le span `messaging.consume` avec `parentId: traceparent` — lien W3C entre Publisher et Consumer visible dans Application Map.

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

### 13.10.5 Requêtes KQL essentielles — validation DFO Routing Slip

#### Pourquoi deux noms de tables ?

Application Insights existait avant Log Analytics avec son propre stockage et ses propres noms de tables (`traces`, `requests`…). Quand Microsoft a migré AppInsights vers Log Analytics en 2020, les tables ont été renommées en convention Log Analytics (`AppTraces`, `AppRequests`…). Pour la compatibilité, les anciens noms restent disponibles **uniquement dans le portail Application Insights** comme alias.

> **C'est la même donnée, deux noms, deux portails :**
>
> | Portail | Table logs | Table métriques | Timestamp | Dimensions |
> |---|---|---|---|---|
> | **Application Insights → Logs** | `traces` | `customMetrics` | `timestamp` | `customDimensions.X` |
> | **Log Analytics → Logs** | `AppTraces` | `AppMetrics` | `TimeGenerated` | `Properties.X` |
>
> Recommandation RAMQ : utiliser **Log Analytics + noms `App*`** — c'est le standard Azure durable.

---

#### 🔍 Q1 — Trouver les sagas récentes

**Application Insights :**
```kusto
traces
| where timestamp > ago(1h)
| where isnotempty(customDimensions.SlipId)
| summarize
    debut    = min(timestamp),
    fin      = max(timestamp),
    warnings = countif(severityLevel == 2),
    errors   = countif(severityLevel == 3),
    statut   = iff(countif(message contains "slip complet") > 0, "✅ Terminé",
               iff(countif(severityLevel == 3) > 0, "❌ Erreur", "⏳ En cours"))
  by SlipId   = tostring(customDimensions.SlipId),
     SlipName = tostring(customDimensions.SlipName)
| order by debut desc
```

**Log Analytics :**
```kusto
AppTraces
| where TimeGenerated > ago(1h)
| where isnotempty(Properties.SlipId)
| summarize
    debut    = min(TimeGenerated),
    fin      = max(TimeGenerated),
    warnings = countif(SeverityLevel == 2),
    errors   = countif(SeverityLevel == 3),
    statut   = iff(countif(Message contains "slip complet") > 0, "✅ Terminé",
               iff(countif(SeverityLevel == 3) > 0, "❌ Erreur", "⏳ En cours"))
  by SlipId   = tostring(Properties.SlipId),
     SlipName = tostring(Properties.SlipName)
| order by debut desc
```

---

#### 📋 Q2 — Timeline complète d'une saga

**Application Insights :**
```kusto
let monSlipId = "COLLE-TON-SLIP-ID";
union traces, dependencies, exceptions
| where timestamp > ago(24h)
| where tostring(customDimensions.SlipId) == monSlipId
| project
    timestamp,
    niveau  = case(severityLevel == 1, "INFO ",
                   severityLevel == 2, "WARN ",
                   severityLevel == 3, "ERROR", "SPAN "),
    etape   = tostring(customDimensions.StepName),
    message = coalesce(message, name, outerMessage)
| order by timestamp asc
```

**Log Analytics :**
```kusto
let monSlipId = "COLLE-TON-SLIP-ID";
union AppTraces, AppDependencies, AppExceptions
| where TimeGenerated > ago(24h)
| where tostring(Properties.SlipId) == monSlipId
| project
    TimeGenerated,
    niveau  = case(SeverityLevel == 1, "INFO ",
                   SeverityLevel == 2, "WARN ",
                   SeverityLevel == 3, "ERROR", "SPAN "),
    etape   = tostring(Properties.StepName),
    message = coalesce(Message, Name, OuterMessage)
| order by TimeGenerated asc
```

---

#### ❌ Q3 — Détecter les échecs et compensations

**Application Insights :**
```kusto
union traces, exceptions
| where timestamp > ago(24h)
| where severityLevel >= 2
      and tostring(customDimensions.SlipName) == "Booking"
| project
    timestamp,
    SlipId  = tostring(customDimensions.SlipId),
    etape   = tostring(customDimensions.StepName),
    niveau  = iff(severityLevel == 3, "ERROR", "WARN"),
    message = coalesce(message, outerMessage)
| order by timestamp desc
```

**Log Analytics :**
```kusto
union AppTraces, AppExceptions
| where TimeGenerated > ago(24h)
| where SeverityLevel >= 2
      and tostring(Properties.SlipName) == "Booking"
| project
    TimeGenerated,
    SlipId  = tostring(Properties.SlipId),
    etape   = tostring(Properties.StepName),
    niveau  = iff(SeverityLevel == 3, "ERROR", "WARN"),
    message = coalesce(Message, OuterMessage)
| order by TimeGenerated desc
```

---

#### 🔁 Q4 — Identifier les étapes fragiles (retries)

**Application Insights :**
```kusto
traces
| where timestamp > ago(24h)
| where message contains "Retry exponentiel" or message contains "Retry immédiat"
| summarize
    nb_retries       = count(),
    sagas_distinctes = dcount(tostring(customDimensions.SlipId))
  by etape = tostring(customDimensions.StepName)
| extend taux_retry = round(100.0 * nb_retries / sagas_distinctes, 1)
| order by nb_retries desc
```

**Log Analytics :**
```kusto
AppTraces
| where TimeGenerated > ago(24h)
| where Message contains "Retry exponentiel" or Message contains "Retry immédiat"
| summarize
    nb_retries       = count(),
    sagas_distinctes = dcount(tostring(Properties.SlipId))
  by etape = tostring(Properties.StepName)
| extend taux_retry = round(100.0 * nb_retries / sagas_distinctes, 1)
| order by nb_retries desc
```

---

#### 📊 Q5 — Métriques opérationnelles EMT

**Application Insights :**
```kusto
customMetrics
| where timestamp > ago(1h)
| where name in ("messages_sent_total", "routing_slip_compensation_total",
                 "circuit_state", "circuit_transitions_total")
| summarize valeur = sum(valueSum) by name, bin(timestamp, 5m)
| render timechart
```

**Log Analytics :**
```kusto
AppMetrics
| where TimeGenerated > ago(1h)
| where Name in ("messages_sent_total", "routing_slip_compensation_total",
                 "circuit_state", "circuit_transitions_total")
| summarize valeur = sum(Sum) by Name, bin(TimeGenerated, 5m)
| render timechart
```

---

#### 🚨 Q6 — Circuit Breaker — détecter les ouvertures

**Application Insights :**
```kusto
customMetrics
| where timestamp > ago(24h)
| where name == "circuit_transitions_total"
| where tostring(customDimensions.to) == "Open"
| project timestamp,
          entite = tostring(customDimensions.entity),
          de     = tostring(customDimensions.from),
          vers   = tostring(customDimensions.to)
| order by timestamp desc
```

**Log Analytics :**
```kusto
AppMetrics
| where TimeGenerated > ago(24h)
| where Name == "circuit_transitions_total"
| where tostring(Properties.to) == "Open"
| project TimeGenerated,
          entite = tostring(Properties.entity),
          de     = tostring(Properties.from),
          vers   = tostring(Properties.to)
| order by TimeGenerated desc
```

---

#### ⏱️ Q7 — SLA — durée des sagas (p50/p95/p99)

**Application Insights :**
```kusto
traces
| where timestamp > ago(24h)
| where tostring(customDimensions.SlipName) == "Booking"
| summarize debut = min(timestamp), fin = max(timestamp)
  by SlipId = tostring(customDimensions.SlipId)
| extend duree_sec = datetime_diff('second', fin, debut)
| summarize p50 = percentile(duree_sec, 50),
            p95 = percentile(duree_sec, 95),
            p99 = percentile(duree_sec, 99),
            max = max(duree_sec)
```

**Log Analytics :**
```kusto
AppTraces
| where TimeGenerated > ago(24h)
| where tostring(Properties.SlipName) == "Booking"
| summarize debut = min(TimeGenerated), fin = max(TimeGenerated)
  by SlipId = tostring(Properties.SlipId)
| extend duree_sec = datetime_diff('second', fin, debut)
| summarize p50 = percentile(duree_sec, 50),
            p95 = percentile(duree_sec, 95),
            p99 = percentile(duree_sec, 99),
            max = max(duree_sec)
```

---

#### 🗂️ Q8 — Audit CAI — historique d'un dossier

**Application Insights :**
```kusto
traces
| where timestamp > ago(90d)
| where tostring(customDimensions.CorrelationId) contains "D-001"
| summarize
    nb_sagas = dcount(tostring(customDimensions.SlipId)),
    premiere = min(timestamp),
    derniere = max(timestamp),
    etapes   = make_set(tostring(customDimensions.StepName))
  by DossierId = tostring(customDimensions.CorrelationId)
```

**Log Analytics :**
```kusto
AppTraces
| where TimeGenerated > ago(90d)
| where tostring(Properties.CorrelationId) contains "D-001"
| summarize
    nb_sagas = dcount(tostring(Properties.SlipId)),
    premiere = min(TimeGenerated),
    derniere = max(TimeGenerated),
    etapes   = make_set(tostring(Properties.StepName))
  by DossierId = tostring(Properties.CorrelationId)
```

---

### 13.10.6 Requêtes KQL essentielles — Application Insights spécifiques

> Ces requêtes utilisent des fonctionnalités **propres à Application Insights** non disponibles dans Log Analytics seul :
> - `operation_Id` : identifiant de trace W3C qui relie toutes les invocations d'une saga
> - `cloud_RoleName` : nom de l'application source (Activateur, Worker…)
> - `itemType` : type d'item (trace, request, dependency, exception)
> - Fonction `app()` : jointure cross-ressource entre plusieurs AppInsights
>
> **Accès :** Azure Portal → Application Insights → Logs

---

#### 🗺️ Q9 — Application Map en KQL — flux entre composants

```kusto
// Voir les appels entre Activateur et Worker (nœuds de l'Application Map)
dependencies
| where timestamp > ago(1h)
| where cloud_RoleName contains "Booking"
| summarize
    nb_appels      = count(),
    duree_moy_ms   = avg(duration),
    taux_echec     = round(100.0 * countif(success == false) / count(), 1)
  by source    = cloud_RoleName,
     cible     = target,
     operation = name
| order by nb_appels desc
```

---

#### 🔗 Q10 — Corréler toutes les invocations d'une saga via operation_Id

```kusto
// operation_Id = TraceId W3C — relie Activateur + 3 Workers en un seul arbre
// Trouver l'operation_Id d'une saga :
requests
| where timestamp > ago(1h)
| where cloud_RoleName contains "Activateur"
| project timestamp, operation_Id, name, duration, success
| order by timestamp desc
| take 10
```

```kusto
// Puis reconstruire tout l'arbre depuis cet operation_Id :
let monOperationId = "COLLE-TON-OPERATION-ID";
union requests, dependencies, traces, exceptions
| where timestamp > ago(24h)
| where operation_Id == monOperationId
| project
    timestamp,
    composant = cloud_RoleName,
    type      = itemType,
    nom       = coalesce(name, message, outerMessage),
    duree_ms  = duration,
    succes    = tostring(success)
| order by timestamp asc
```

---

#### 🏷️ Q11 — Performance par composant (Activateur vs Worker)

```kusto
// Durée moyenne des invocations par composant
requests
| where timestamp > ago(24h)
| where cloud_RoleName contains "Booking"
| summarize
    nb_invocations = count(),
    duree_p50_ms   = percentile(duration, 50),
    duree_p95_ms   = percentile(duration, 95),
    taux_echec_pct = round(100.0 * countif(success == false) / count(), 1)
  by composant = cloud_RoleName, operation = name
| order by duree_p95_ms desc
```

---

#### 🔍 Q12 — Trouver une saga depuis un MessageId Service Bus

```kusto
// Remonter d'un MessageId SB vers le SlipId et la trace complète
traces
| where timestamp > ago(24h)
| where customDimensions.MessageId == "TON-MESSAGE-ID"
| project
    timestamp,
    SlipId      = tostring(customDimensions.SlipId),
    operation_Id,
    composant   = cloud_RoleName,
    message
| take 1
// → copier le SlipId ou operation_Id → lancer Q2 ou Q10
```

---

#### 📈 Q13 — Taux de succès global des sagas (healthcheck)

```kusto
// Vue d'ensemble : ratio sagas réussies vs échouées sur 24h
traces
| where timestamp > ago(24h)
| where isnotempty(customDimensions.SlipId)
| summarize
    terminées = dcountif(tostring(customDimensions.SlipId),
                    message contains "slip complet"),
    en_erreur = dcountif(tostring(customDimensions.SlipId),
                    severityLevel == 3),
    total     = dcount(tostring(customDimensions.SlipId))
  by SlipName = tostring(customDimensions.SlipName)
| extend taux_succes = round(100.0 * terminées / total, 1)
| project SlipName, total, terminées, en_erreur, taux_succes
```

---

#### 🔔 Q14 — Alertes — sagas bloquées depuis plus de N minutes

```kusto
// Sagas démarrées depuis > 10 min sans completion (stuck sagas)
let seuil_min = 10;
traces
| where timestamp > ago(2h)
| where isnotempty(customDimensions.SlipId)
| summarize
    debut    = min(timestamp),
    derniere = max(timestamp),
    complete = countif(message contains "slip complet")
  by SlipId = tostring(customDimensions.SlipId),
     SlipName = tostring(customDimensions.SlipName)
| where complete == 0  // pas encore terminée
| extend age_min = datetime_diff('minute', now(), debut)
| where age_min > seuil_min
| project SlipId, SlipName, debut, age_min
| order by age_min desc
// → Brancher en alerte Azure Monitor sur ce résultat
```

---

#### 🔀 Q15 — Jointure cross-ressource (Activateur + Worker dans 2 AppInsights)

```kusto
// Si Activateur et Worker ont des AppInsights SÉPARÉS
// Utiliser app() pour croiser les données
union
    app("nom-appinsights-activateur").traces,
    app("nom-appinsights-worker").traces
| where timestamp > ago(1h)
| where tostring(customDimensions.SlipId) == "TON-SLIP-ID"
| project timestamp, cloud_RoleName, message
| order by timestamp asc
// Note : dans votre config actuelle, un seul AppInsights suffit
```

---

### 13.10.7 Scénarios DFO — requêtes de diagnostic opérationnel

> Ces scénarios répondent aux questions réelles posées à 3h du matin lors d'un incident.  
> Chaque requête est commentée ligne par ligne pour être compréhensible sans contexte.  
> **Portail :** Application Insights → Logs (syntaxe `traces` / `customDimensions`)

---

#### 🚨 Scénario 1 — "L'astreinte : qu'est-ce qui se passe en ce moment ?"

```kusto
// VUE D'ENSEMBLE EN 1 REQUÊTE
// Objectif : comprendre en 30 secondes l'état de la plateforme
// Montre : sagas actives, en erreur, taux de retry, dernière activité

traces
| where timestamp > ago(30m)                          // dernières 30 minutes
| where isnotempty(customDimensions.SlipId)            // uniquement les saga logs
| summarize
    // Compter les sagas par statut
    terminées  = dcountif(tostring(customDimensions.SlipId),
                    message contains "slip complet"),
    en_erreur  = dcountif(tostring(customDimensions.SlipId),
                    severityLevel == 3),
    en_retry   = dcountif(tostring(customDimensions.SlipId),
                    message contains "Retry"),
    actives    = dcount(tostring(customDimensions.SlipId)),
    // Dernière activité pour détecter si la plateforme est gelée
    derniere_activite = max(timestamp)
  by SlipName = tostring(customDimensions.SlipName)    // groupé par type de saga
| extend
    taux_erreur = round(100.0 * en_erreur / actives, 1),
    // Alerte si plus de 5 min sans activité → possible freeze
    silence_min = datetime_diff('minute', now(), derniere_activite)
| project SlipName, actives, terminées, en_erreur, en_retry,
          taux_erreur, silence_min, derniere_activite
| order by taux_erreur desc                            // les plus critiques en premier
```

---

#### 🔥 Scénario 2 — "Les retries explosent — où est le problème ?"

```kusto
// DÉTECTION D'UNE TEMPÊTE DE RETRIES
// Objectif : identifier rapidement quelle étape cause une cascade de retries
// Use case : Service Bus throttle, panne réseau, API downstream indisponible

traces
| where timestamp > ago(1h)
| where message contains "Retry exponentiel"           // retries exponent. = infra KO
      or message contains "Retry immédiat"             // retries imméd. = logique métier
| summarize
    nb_retries   = count(),                            // volume total de retries
    nb_sagas     = dcount(tostring(customDimensions.SlipId)),  // sagas impactées
    // Calculer la tendance : retries en accélération ?
    retries_15min = countif(timestamp > ago(15m)),
    retries_prev  = countif(timestamp <= ago(15m))
  by
    etape   = tostring(customDimensions.StepName),     // étape qui plante
    type_retry = case(
        message contains "exponentiel", "exponentiel (infra)",
        message contains "immédiat",    "immédiat (logique)",
        "autre")
| extend
    taux_retry_par_saga = round(1.0 * nb_retries / nb_sagas, 1),
    // Si retries_15min > retries_prev*2 → tempête en cours
    acceleration = iff(retries_15min > retries_prev * 2, "⚠️ EN ACCÉLÉRATION", "stable")
| order by nb_retries desc
```

---

#### 💀 Scénario 3 — "Combien de dossiers patients ont été impactés par l'incident ?"

```kusto
// IMPACT MÉTIER D'UN INCIDENT
// Objectif : répondre à la direction et à CAI en chiffres précis
// Paramètres : ajuster debut_incident et fin_incident

let debut_incident = datetime(2026-06-04 14:00:00);    // heure début incident
let fin_incident   = datetime(2026-06-04 15:30:00);    // heure fin incident

traces
| where timestamp between (debut_incident .. fin_incident)
| where isnotempty(customDimensions.SlipId)
| summarize
    // Nombre de sagas tentées pendant l'incident
    nb_sagas_tentees = dcount(tostring(customDimensions.SlipId)),
    // Dossiers avec au moins 1 erreur
    nb_dossiers_ko   = dcountif(
        tostring(customDimensions.CorrelationId),
        severityLevel == 3),
    // Dossiers qui ont quand même terminé (retry réussi)
    nb_dossiers_ok   = dcountif(
        tostring(customDimensions.CorrelationId),
        message contains "slip complet")
  by SlipName = tostring(customDimensions.SlipName)
| extend
    nb_dossiers_bloques = nb_dossiers_ko - nb_dossiers_ok,   // réellement perdus
    // Pourcentage d'impact pour le rapport de direction
    pct_impact = round(100.0 * nb_dossiers_ko / nb_sagas_tentees, 1)
| project SlipName, nb_sagas_tentees, nb_dossiers_ko,
          nb_dossiers_ok, nb_dossiers_bloques, pct_impact
```

---

#### 🔍 Scénario 4 — "Ce dossier est bloqué depuis hier — que s'est-il passé ?"

```kusto
// INVESTIGATION D'UN DOSSIER SPÉCIFIQUE
// Objectif : reconstruire la vie complète d'un dossier pour l'équipe support
// Remplacer D-001 par le vrai identifiant du dossier

let dossier = "D-001";

union traces, exceptions
| where timestamp > ago(7d)                            // 7 jours d'historique
| where tostring(customDimensions.CorrelationId) contains dossier
// --- CHRONOLOGIE COMPLÈTE ---
| project
    timestamp,
    // Simplifier le nom du composant pour la lisibilité
    composant  = replace_string(cloud_RoleName, "RAMQ.Samples.Queue.RoutingSlip.", ""),
    SlipId     = tostring(customDimensions.SlipId),
    etape      = tostring(customDimensions.StepName),
    tentative  = tostring(customDimensions.Attempt),
    // Classifier chaque ligne pour faciliter la lecture
    type_event = case(
        message contains "slip complet",  "✅ SAGA TERMINÉE",
        message contains "Fault",         "❌ FAULT → DLQ",
        message contains "Compens",       "↩️ COMPENSATION",
        message contains "Retry",         "🔁 RETRY",
        message contains "début étape",   "▶️ ÉTAPE DÉMARRÉE",
        message contains "avance vers",   "→ ÉTAPE SUIVANTE",
        severityLevel == 3,               "🚫 ERREUR",
        "• log"),
    detail = coalesce(message, outerMessage)
| order by timestamp asc
```

---

#### 💸 Scénario 5 — "Le volume AppInsights a explosé — pourquoi ?"

```kusto
// ANALYSE D'UNE ANOMALIE DE VOLUME (COÛT)
// Objectif : identifier d'où vient un pic de données inattendu
// Use case : facture AppInsights anormale, Daily Cap atteint trop tôt

// Étape 1 : voir la tendance horaire
traces
| where timestamp > ago(24h)
| summarize
    nb_items  = count(),                               // volume de logs
    nb_sagas  = dcount(tostring(customDimensions.SlipId))  // sagas correspondantes
  by heure = bin(timestamp, 1h),
     composant = cloud_RoleName
| order by nb_items desc
```

```kusto
// Étape 2 : identifier le type de logs qui domine
traces
| where timestamp > ago(6h)
| summarize count() by
    // Classer par niveau pour voir si c'est un spam de Warning/Info
    niveau    = case(severityLevel == 1, "INFO",
                     severityLevel == 2, "WARN",
                     severityLevel == 3, "ERROR", "autre"),
    // Et par composant source
    composant = cloud_RoleName,
    // Et par étape pour trouver la boucle
    etape     = tostring(customDimensions.StepName)
| order by count_ desc
| take 20
// → Si 1 étape + 1 composant dominent avec beaucoup de WARN : tempête de retries
// → Si INFO domine : le niveau de log n'est pas filtré (vérifier ApplicationInsights: Warning)
```

---

#### 🩺 Scénario 6 — "La plateforme est-elle saine ? Rapport du matin"

```kusto
// RAPPORT QUOTIDIEN DE SANTÉ — à exécuter chaque matin
// Objectif : vue d'ensemble sur 24h pour le stand-up ou le rapport direction

let periode = ago(24h);

// Bloc 1 : volumes
let volumes = traces
| where timestamp > periode
| where isnotempty(customDimensions.SlipId)
| summarize
    total_sagas    = dcount(tostring(customDimensions.SlipId)),
    sagas_ok       = dcountif(tostring(customDimensions.SlipId),
                         message contains "slip complet"),
    sagas_erreur   = dcountif(tostring(customDimensions.SlipId),
                         severityLevel == 3),
    total_retries  = countif(message contains "Retry"),
    total_dlq      = countif(message contains "DLQ" or message contains "lettre morte")
  by SlipName = tostring(customDimensions.SlipName);

// Bloc 2 : SLA (durée des sagas)
let sla = traces
| where timestamp > periode
| where isnotempty(customDimensions.SlipId)
| summarize debut = min(timestamp), fin = max(timestamp)
  by SlipId = tostring(customDimensions.SlipId),
     SlipName = tostring(customDimensions.SlipName)
| extend duree_sec = datetime_diff('second', fin, debut)
| summarize p50 = percentile(duree_sec, 50),
            p95 = percentile(duree_sec, 95)
  by SlipName;

// Jointure et présentation finale
volumes
| join kind=leftouter sla on SlipName
| extend
    taux_succes = round(100.0 * sagas_ok / total_sagas, 1),
    // Santé globale : vert si > 99%, orange si > 95%, rouge sinon
    sante = case(
        1.0 * sagas_ok / total_sagas >= 0.99, "🟢 SAIN",
        1.0 * sagas_ok / total_sagas >= 0.95, "🟡 DÉGRADÉ",
        "🔴 CRITIQUE")
| project SlipName, sante, total_sagas, sagas_ok, sagas_erreur,
          taux_succes, total_retries, total_dlq,
          sla_p50_sec = p50, sla_p95_sec = p95
```

---

### 13.10.8 Filtres anti-bruit AppInsights et corrélation bout-en-bout

#### Le principe fondamental — trois pipelines, trois filtres

> 💡 **Pour un junior :** la confusion la plus courante sur les filtres AppInsights vient du fait qu'on voit "du bruit" et on cherche "un seul endroit pour tout bloquer". Il n'existe pas. AppInsights reçoit la télémétrie par **trois chemins séparés**, et chaque chemin a son propre filtre.

```
CHEMIN 1 — Logs du code worker (ILogger dans ton code RAMQ)
─────────────────────────────────────────────────────────────────────
_logger.LogError("Service en panne permanente...")
  → Microsoft.Extensions.Logging pipeline (processus worker)
    → FILTRE ① : logging.AddFilter("RAMQ", LogLevel.Error) [Program.cs]
    │   → bloque Information et Warning de ton code avant AppInsights
    │   → laisse passer Error et Critical
    ↓
    ApplicationInsightsLoggerProvider → AppInsights
    → table AppTraces (Log Analytics)

CHEMIN 2 — Logs du host Azure Functions (runtime, PAS ton code)
─────────────────────────────────────────────────────────────────────
Azure Functions Worker middleware → _logger.LogInformation("Executing...")
  → ILogger pipeline du processus HOST (séparé du worker)
    → FILTRE ② : host.json logLevel
    │   → "Function": "None" → bloque Executing/Executed/Trigger Details
    │   → "Host": "Warning" → bloque Initializing Warmup Extension
    │   → "Microsoft": "Warning" → bloque les SDK Microsoft verbeux
    ↓
    AppInsights (TraceTelemetry)

CHEMIN 3 — Dépendances (ce que les SDK capturent automatiquement)
─────────────────────────────────────────────────────────────────────
HttpClient / Azure SDK / OTel exporter / Azure Identity
  → DependencyTrackingTelemetryModule (capture TOUT automatiquement)
    → Chain ITelemetryProcessor
      → FILTRE ③ : AppInsightsNoiseFilter
      │   → bloque les dépendances infra (Azure Service Bus SDK,
      │     /v2/track, Microsoft.AAD, FunctionRpc, InProc...)
      │   → laisse passer les spans RAMQ métier
      ↓
      TelemetryChannel → AppInsights
      → table AppDependencies (Log Analytics)
```

**Règle à retenir :**

| Si le bruit vient de... | Filtre à utiliser | Pourquoi |
|---|---|---|
| Logs `ILogger` de ton code RAMQ (Warning/Information non désirés) | `logging.AddFilter("RAMQ", LogLevel.Error)` dans `Program.cs` | Filtre au niveau de l'`ILogger` avant toute transmission |
| Logs de démarrage Azure Functions (Executing, Warmup, Host...) | `host.json` `logLevel` | Ces logs viennent du **processus host**, hors de portée du worker |
| Dépendances HTTP ou InProc capturées automatiquement par le SDK | `AppInsightsNoiseFilter` (`ITelemetryProcessor`) | Ce sont des **DependencyTelemetry** — ni `Program.cs` ni `host.json` ne les voient |

> **Comment identifier lequel dans AppInsights Search :** la colonne **Type** indique `Trace` (log → filtrer dans `Program.cs` ou `host.json`) ou `Dependency` (capturé auto → filtrer dans `AppInsightsNoiseFilter`).

---

#### Pourquoi `host.json` seul ne suffit pas

`host.json` logLevel ne contrôle que les logs du **processus host** Azure Functions. Les `DependencyTelemetry` (dépendances HTTP/InProc) ne passent PAS par le pipeline de logs — elles sont capturées directement par `DependencyTrackingTelemetryModule`. Et les logs du processus **worker** (ton code RAMQ) sont filtrés par `logging.AddFilter()` dans `Program.cs`.

```
Exemple : VisualStudioCredential.GetToken
  → Azure.Identity appelle login.microsoftonline.com via HttpClient
  → DependencyTrackingTelemetryModule l'intercepte DIRECTEMENT
  → Crée une DependencyTelemetry {Name="VisualStudioCredential.GetToken", Type="InProc|Microsoft.AAD"}
  → host.json logLevel ne le voit JAMAIS (ce n'est pas un log ILogger)
  → Seul AppInsightsNoiseFilter peut le bloquer
```

#### Pourquoi `AppInsightsNoiseFilter` seul ne suffit pas

`AppInsightsNoiseFilter` ne traite que les `DependencyTelemetry`. Les logs de démarrage Azure Functions (`Executing 'Functions.X'`, `Initializing Warmup Extension`) sont des `TraceTelemetry` générés par le pipeline `ILogger` du **host**. `AppInsightsNoiseFilter.Process(item)` ne reçoit que des `DependencyTelemetry` — les `TraceTelemetry` passent dans une autre branche du pipeline et lui sont invisibles.

```
Exemple : "Executing 'Functions.ReserverVol'"
  → Azure Functions Worker middleware appelle _logger.LogInformation(...)
  → Catégorie : "Function.ReserverVol"
  → Pipeline ILogger (host) → ApplicationInsightsLoggerProvider
  → AppInsightsNoiseFilter ne le voit JAMAIS (c'est un TraceTelemetry)
  → Seul host.json "Function": "None" peut le bloquer
```

#### `AddFilter("RAMQ", LogLevel.Error)` dans `Program.cs` — le filtre le plus fiable pour le code RAMQ

En dotnet-isolated avec `ConfigureFunctionsApplicationInsights()`, le `ApplicationInsightsLoggerProvider` **respecte** les `AddFilter()` déclarés dans `ConfigureLogging`. Ce filtre agit au niveau de l'`ILogger` lui-même — le log Warning/Information n'est jamais créé, donc il ne peut pas atteindre AppInsights.

```csharp
.ConfigureLogging(logging =>
{
    logging.AddFilter("RAMQ",      LogLevel.Error);   // ← seuls Error/Critical RAMQ → AppInsights ✅
    logging.AddFilter("Microsoft", LogLevel.Warning); // ← console ET AppInsights worker ✅
    logging.AddFilter("System",    LogLevel.Warning);
    logging.AddFilter("Azure",     LogLevel.Warning);
})
```

> **Pourquoi `"RAMQ"` dans `host.json` est désormais redondant :** le filtre code-side ci-dessus garantit qu'aucun log RAMQ Warning/Information ne quitte le worker. L'entrée `"RAMQ"` dans `host.json` peut être omise. En revanche, les autres entrées (`Function`, `Host`, `Microsoft`) restent **indispensables** car elles filtrent les logs du processus **host** — hors de portée de `Program.cs`.

> **Limite :** `AddFilter("Microsoft", Warning)` dans `Program.cs` filtre les logs Microsoft du **worker** uniquement. Les logs Microsoft générés par le **processus host** (Executing, Warmup, etc.) ne sont pas affectés — c'est pour cela que `host.json` reste nécessaire pour ces catégories.

---

#### Tableau complet — quelle trace vient d'où, quel filtre la bloque

| Trace visible dans AppInsights | Type AppInsights | Source | Filtre qui la bloque |
|---|---|---|---|
| `Executing 'Functions.ReserverVol'` | `TraceTelemetry` | Azure Functions Worker middleware (ILogger catégorie `Function.X`) | `host.json` : `"Function": "None"` |
| `Trigger Details: MessageId: ...` | `TraceTelemetry` | Service Bus trigger extension (ILogger) | `host.json` : `"Function": "None"` |
| `Initializing Warmup Extension` | `TraceTelemetry` | Azure Functions host startup (ILogger catégorie `Host.*`) | `host.json` : `"Host": "Warning"` |
| SDK Azure logs verbeux | `TraceTelemetry` | `ILogger` catégories `Microsoft.*` | `host.json` : `"Microsoft": "Warning"` |
| `POST /v2/track` | `DependencyTelemetry` (HTTP) | OTel Azure Monitor exporter → HttpClient → DependencyTrackingTelemetryModule | `AppInsightsNoiseFilter` : `data.Contains("/v2/track")` |
| `VisualStudioCredential.GetToken` | `DependencyTelemetry` (InProc) | Azure.Identity → DependencyTrackingTelemetryModule | `AppInsightsNoiseFilter` : `type.Contains("Microsoft.AAD")` |
| `ServiceBusSender.Send` | `DependencyTelemetry` (Azure SDK) | Azure.Messaging.ServiceBus SDK → DependencyTrackingTelemetryModule | `AppInsightsNoiseFilter` : `type.StartsWith("Azure Service Bus")` |
| `POST stxalpum/COMJournalAISUnit` | `DependencyTelemetry` (HTTP) | Azure.Data.Tables → HttpClient → DependencyTrackingTelemetryModule | `AppInsightsNoiseFilter` : `type.StartsWith("Azure table")` |
| `POST /Settlement/Complete` | `DependencyTelemetry` (HTTP) | Functions message settlement → HttpClient | `AppInsightsNoiseFilter` : `data.Contains("/Settlement/")` |
| `TableClient.AddEntity` | `DependencyTelemetry` (InProc) | Azure.Data.Tables SDK → DependencyTrackingTelemetryModule | `AppInsightsNoiseFilter` : `type.Contains("Microsoft.Tables")` |
| `function BookingActivateur (8.8 s)` | `DependencyTelemetry` (InProc) | Runtime Azure Functions — track automatiquement la durée de chaque invocation | `AppInsightsNoiseFilter` : `type == "InProc"` |
| `LogWarning` / `LogInformation` du code RAMQ | `TraceTelemetry` | `ILogger<T>` dans ton code worker (catégorie commence par `RAMQ`) | `logging.AddFilter("RAMQ", LogLevel.Error)` dans `Program.cs` |

> **Piège classique :** `FilterHttpRequestMessage` (OTel) ne résout PAS le problème des dépendances. Ce filtre contrôle uniquement la création de *spans OTel* — il ne touche pas le pipeline SDK AppInsights. S'il bloque `*.in.applicationinsights.azure.com`, l'OTel exporter ne peut plus envoyer de données. **Ne jamais l'utiliser pour filtrer le bruit AppInsights.**

---

#### Solution — 3 niveaux de filtrage

---

##### Niveau 0 — `logging.AddFilter()` dans `Program.cs` : filtre ILogger pour le code RAMQ

**Principe pour un junior :** Ce filtre agit au niveau de l'`ILogger` lui-même, **avant** que quoi que ce soit ne soit envoyé à AppInsights. Si un `LogWarning(...)` est bloqué ici, il n'existe tout simplement plus — AppInsights ne le verra jamais.

```csharp
.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddSimpleConsole(opts => { opts.IncludeScopes = false; opts.TimestampFormat = "HH:mm:ss.fff "; });
    logging.AddFilter("Azure",     LogLevel.Warning);
    logging.AddFilter("Microsoft", LogLevel.Warning);
    logging.AddFilter("System",    LogLevel.Warning);
    logging.AddFilter("RAMQ",      LogLevel.Error);   // ← seuls Error/Critical RAMQ → AppInsights
})
```

> **Pourquoi c'est plus fiable que `host.json` pour le code RAMQ :** `ConfigureFunctionsApplicationInsights()` synchronise ce filtre avec le pipeline AppInsights worker. En dotnet-isolated, le worker a son propre pipeline AppInsights — ce filtre le contrôle directement. `host.json` cible le processus host, pas le worker.

---

##### Niveau 1 — `AppInsightsNoiseFilter` : ITelemetryProcessor dans `Program.cs`

**Principe pour un junior :** Le SDK AppInsights intercepte TOUS les appels sortants automatiquement — même ses propres appels, les appels Azure Identity pour les tokens, les appels Service Bus SDK, les appels Azure Table Storage. Un `ITelemetryProcessor` est un filtre qui s'exécute juste avant l'envoi vers AppInsights. On peut y supprimer (`return`) ou laisser passer (`next.Process(item)`) chaque élément de télémétrie.

**Enregistrement** dans `Program.cs` (conditionnel — uniquement si AppInsights est configuré) :

```csharp
if (!string.IsNullOrWhiteSpace(appInsightsCs))
{
    services.AddApplicationInsightsTelemetryWorkerService();
    services.ConfigureFunctionsApplicationInsights();
    services.AddApplicationInsightsTelemetryProcessor<AppInsightsNoiseFilter>(); // ← ajouter
}
```

**Classe complète** (à placer en bas du fichier `Program.cs`) :

```csharp
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Diagnostics;

/// <summary>
/// Filtre les DependencyTelemetry auto-capturées par DependencyTrackingTelemetryModule.
/// Le module intercepte TOUS les appels HttpClient et tous les spans InProc des SDK Azure.
/// Sans ce filtre, AppInsights est pollué par de la télémétrie infrastructure sans valeur métier.
///
/// Pour RE-ACTIVER une règle (debug ponctuel) : commenter la ligne correspondante.
/// Pour AJOUTER une règle : ajouter une condition dans le bloc if.
/// </summary>
internal sealed class AppInsightsNoiseFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry dep)
        {
            var data = dep.Data ?? string.Empty; // URL de l'appel HTTP (vide pour les InProc)
            var type = dep.Type ?? string.Empty; // catégorie SDK : "Azure Service Bus", "InProc | Microsoft.AAD", etc.

            if (
                // ── Appels HTTP vers l'infrastructure AppInsights ──────────────────────────
                // L'OTel exporter et le SDK AppInsights envoient leurs données via HttpClient.
                // DependencyTrackingTelemetryModule les capture → boucle infinie de télémétrie.
                // Ne JAMAIS désactiver ces deux règles : elles évitent une boucle de feedback.
                data.Contains("applicationinsights.azure.com") ||      // endpoint OTel + SDK ingestion
                data.Contains("livediagnostics.monitor.azure.com") ||  // Live Metrics heartbeat (ping/s)

                // ── Canal gRPC host ↔ worker (Azure Functions dotnet-isolated) ──────────────
                // En mode dotnet-isolated, le worker et le host communiquent via gRPC.
                // Ce canal est de la plomberie interne — aucune valeur pour diagnostiquer une saga.
                // Désactiver si vous debuggez un problème de communication host/worker.
                data.Contains("FunctionRpc") ||

                // ── Auto-télémétrie AppInsights SDK ───────────────────────────────────────
                // Le SDK envoie ses propres données par HTTP. Il se capture lui-même.
                // Règles de sécurité pour éviter la boucle auto-référentielle.
                data.Contains("/v2/track") ||                           // OTel Azure Monitor exporter
                data.Contains("/v2.1/track") ||                        // AppInsights SDK v2 direct

                // ── Login Azure AD (renouvellement de tokens) ─────────────────────────────
                // VisualStudioCredential renouvelle son token AAD via HTTP toutes les ~60 min.
                // Apparaît comme une dépendance HTTP vers login.microsoftonline.com.
                // Désactiver pour debugger des problèmes d'authentification Azure.
                data.Contains("login.microsoftonline.com") ||

                // ── Message settlement (acquittement Service Bus) ─────────────────────────
                // En dotnet-isolated, CompleteMessageAsync() passe par un endpoint HTTP interne
                // /Settlement/Complete vers le host Azure Functions. Pur infrastructure.
                // Désactiver pour debugger des problèmes de settlement de messages.
                data.Contains("/Settlement/") ||

                // ── Spans InProc Azure Identity (token AAD en mémoire) ────────────────────
                // VisualStudioCredential.GetToken crée un span OTel de type InProc.
                // Différent du HTTP ci-dessus : c'est le span de l'opération de refresh,
                // pas l'appel HTTP vers Azure AD.
                // Type : "InProc | Microsoft.AAD"
                type.Contains("Microsoft.AAD") ||

                // ── Spans InProc Azure Table Storage (journal EMT) ────────────────────────
                // TableClient.AddEntity() crée un span InProc quand le journal EMT écrit
                // une entrée dans Azure Table Storage (R16 — MTJ/Journal).
                // Type : "InProc | Microsoft.Tables"
                // ⚠️ Désactiver pour voir les écritures journal en détail (debug R16).
                type.Contains("Microsoft.Tables") ||

                // ── Dépendances HTTP Azure Table Storage (journal EMT) ────────────────────
                // La même écriture journal génère aussi un appel HTTP vers le storage account.
                // On voit : "POST stxalpum/COMJournalAISUnit, Type: Azure table"
                // Type : "Azure table"
                // ⚠️ Désactiver pour voir les appels HTTP au storage journal (debug R16).
                type.StartsWith("Azure table", StringComparison.OrdinalIgnoreCase) ||

                // ── Dépendances Azure Service Bus SDK ─────────────────────────────────────
                // ServiceBusSender.Send() crée une dépendance de type "Azure Service Bus"
                // pour chaque message envoyé (avance du routing slip, etc.).
                // Ce sont des appels bas-niveau SDK — le routing slip est déjà visible
                // via les spans routing_slip.step créés par EMT à un niveau plus haut.
                // ⚠️ Désactiver pour voir le détail des envois Service Bus (debug réseau).
                type.StartsWith("Azure Service Bus", StringComparison.OrdinalIgnoreCase) ||

                // ── Invocations de fonctions Azure (Type: InProc) ─────────────────────────
                // Le runtime Azure Functions crée automatiquement une DependencyTelemetry
                // de type "InProc" pour chaque invocation de fonction (ex: BookingActivateur).
                // Elle indique la durée totale d'exécution de la fonction, mais ça fait doublon
                // avec les spans EMT routing_slip.step qui donnent le même niveau de détail.
                // ⚠️ Désactiver pour monitorer la durée globale des invocations Functions.
                type == "InProc"
            )
                return; // ← supprimer cet élément — ne pas envoyer à AppInsights
        }

        next.Process(item); // ← laisser passer tout le reste (traces RAMQ métier)
    }
}
```

> **Guide décision pour un junior — activer ou désactiver une règle ?**
>
> | Règle | Désactiver si… |
> |---|---|
> | `applicationinsights.azure.com` | **Jamais** — boucle garantie |
> | `FunctionRpc` | Problème de communication host ↔ worker |
> | `Microsoft.AAD` / `login.microsoftonline.com` | Problème d'authentification Azure Identity |
> | `Microsoft.Tables` / `Azure table` | Problème avec le journal EMT (R16) |
> | `Azure Service Bus` | Problème de routage des messages Service Bus |
> | `/Settlement/` | Messages qui restent bloqués (non acquittés) |
> | `InProc` | Tu veux voir la durée totale des invocations Functions dans AppInsights |

---

##### Niveau 2 — `host.json` : logLevel pour les logs du processus host

**Principe pour un junior :** En dotnet-isolated, Azure Functions s'exécute en **deux processus séparés** : le **host** (runtime Functions) et le **worker** (ton code .NET). `host.json` contrôle uniquement les logs du processus **host** vers AppInsights. Ton code RAMQ tourne dans le worker et est filtré par `logging.AddFilter()` dans `Program.cs`. Les deux configurations ne se substituent pas — elles filtrent des processus différents.

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      // "RAMQ" N'EST PLUS NÉCESSAIRE ICI.
      // logging.AddFilter("RAMQ", LogLevel.Error) dans Program.cs est plus fiable
      // car il filtre au niveau de l'ILogger worker avant toute transmission.
      // Si vous l'ajoutez quand même, assurez-vous qu'il est cohérent avec Program.cs.

      "Function":                                "None",
      // ↑ Supprime : "Executing 'Functions.ReserverVol'", "Trigger Details", "Executed..."
      //   Catégorie utilisée par le Worker middleware Azure Functions pour start/end d'invocation.
      //   ⚠️ Désactiver (mettre "Information") pour voir les logs de démarrage des fonctions.

      "Microsoft":                               "Warning",
      // ↑ Supprime les logs Information des SDK Microsoft côté HOST
      //   (différent du AddFilter("Microsoft", Warning) dans Program.cs qui filtre le worker)

      "Host":                                    "Warning",
      // ↑ Supprime les logs de démarrage du host ("Initializing Warmup Extension", etc.)

      "Azure.Identity":                          "None",
      // ↑ Supprime complètement les logs Azure Identity (token refresh verbeux)

      "Microsoft.Identity":                      "None",
      "Grpc":                                    "None",
      "Microsoft.Azure.Functions.Worker.Grpc":   "None"
      // ↑ Supprime les logs du canal gRPC host ↔ worker (verbosité extrême en debug)
    },
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled":                  true,
        "excludedTypes":              "Exception;Request",
        // ↑ Exceptions et Requests JAMAIS échantillonnées → toujours 100% visibles
        "maxTelemetryItemsPerSecond": 20
        // ↑ Limite le débit pour rester sous 5 GB/mois (budget free tier)
      },
      "enableLiveMetricsFilters": true
    }
  }
}
```

> **Règle d'or :** `"Function": "None"` et `"Microsoft": "Warning"` dans `host.json` sont **indispensables** — ils filtrent les logs du **processus host** qui sont hors de portée de `Program.cs`. En revanche, `"RAMQ"` dans `host.json` est redondant si `logging.AddFilter("RAMQ", LogLevel.Error)` est déjà dans `Program.cs`.

---

#### Résultat final validé

End-to-End Transaction view AppInsights — uniquement les 4 traces RAMQ métier :

```
10:18:06 AM  Internal  function BookingActivateur   (6.1 s)   ← saga déclenchée
10:19:29 AM  Internal  function ReserverVoiture     (5.6 s)   ← étape 1
10:19:35 AM  Internal  function ReserverHotel       (565 ms)  ← étape 2
10:19:35 AM  Internal  function ReserverVol         (376 ms)  ← étape 3 — saga complète ✅
```

Toutes sous le même `operation_Id` (W3C TraceId propagé par P4-T3 via `traceparent` dans les `ApplicationProperties` Service Bus).

---

#### Corrélation bout-en-bout — le `operation_Id`

> **C'est la clé de voûte du DFO.** Le `operation_Id` est le **W3C TraceId** propagé par P4-T3 via `traceparent` dans les `ApplicationProperties` de chaque message Service Bus. Il relie toutes les invocations d'une même saga en un seul arbre de trace.

```
Activateur (HTTP trigger)
  └── operation_Id = "abc123"
       ↓ traceparent: "00-abc123-spanId-01" dans ApplicationProperties
  ReserverVoiture (SB Trigger) — messaging.consume(parentId:"00-abc123-...")
    └── operation_Id = "abc123"   ← hérité via RoutingSlipExecutor
         ↓ routing_slip.step enfant de messaging.consume → écrit "00-abc123-..." sur le suivant
    ReserverHotel (SB Trigger) — messaging.consume(parentId:"00-abc123-...")
      └── operation_Id = "abc123" ← idem
           ↓
      ReserverVol (SB Trigger) — messaging.consume(parentId:"00-abc123-...")
        └── operation_Id = "abc123" ← même arbre de bout en bout ✅
```

Dans AppInsights : cliquer sur une trace → **End-to-end transaction details** → vue visuelle complète de la saga.

---

#### Implémentation technique — comment EMT propage le TraceId

La propagation ne fonctionne PAS automatiquement en dotnet-isolated Azure Functions. Trois composants EMT coopèrent :

**1. Côté producteur — `AzureMessagingProvider.SendAsync`**

```csharp
using var activity = MessagingActivitySource.Source.StartActivity("messaging.send", ActivityKind.Producer);

// Injection W3C dans les ApplicationProperties du message Service Bus
if (Activity.Current?.Id is { } traceId)
    message.ApplicationProperties["traceparent"] = traceId;
```

L'ID écrit est `"00-{TraceId}-{SpanId_messaging.send}-01"`. Le TraceId est celui de l'activateur.

**2. Côté worker — `RoutingSlipExecutor.RunAsync` (P4-T3)**

> **Piège** : `RoutingSlipExecutor` appelle `provider.DeserializeMessageSafe()` directement, **contournant** `BaseConsumer.DeserializeMessageAsync`. Sans correctif, il crée un nouveau TraceId à chaque step et propage ce nouveau TraceId sur le message suivant → cascade : 4 operation_Id différents pour une même saga.

```csharp
// En tête de RunAsync — AVANT DeserializeMessageSafe et StartActivity routing_slip.step
var traceparent = provider.GetTraceparent(); // lit ApplicationProperties["traceparent"]

// (1) Tag l'Activity Azure Functions (nouveau TraceId) pour ServiceBusCorrelationInitializer
Activity.Current?.SetTag("messaging.source.traceparent", traceparent);

// (2) Crée messaging.consume avec le parentId du producteur
//     → routing_slip.step hérite de ce contexte → écrit le bon traceparent sur le suivant
using var consumeActivity = traceparent != null
    ? MessagingActivitySource.Source.StartActivity("messaging.consume", ActivityKind.Consumer, parentId: traceparent)
    : MessagingActivitySource.Source.StartActivity("messaging.consume", ActivityKind.Consumer);
```

Après ce bloc, `Activity.Current = messaging.consume` avec le **même TraceId** que l'activateur. Tous les spans `routing_slip.step` et `booking.*.reserve` en dessous héritent de ce TraceId. Et `SendAsync` écrit `"00-{TraceId_activateur}-..."` sur le message suivant — propageant le même TraceId à chaque step.

**3. Côté worker — `ServiceBusCorrelationInitializer` (ITelemetryInitializer)**

Le runtime Azure Functions crée l'Activity d'invocation (step 1) **avant** que notre code tourne — cette Activity a un nouveau TraceId. Ce `ITelemetryInitializer` corrige l'`operation_Id` de la télémétrie AppInsights capturée dans ce contexte :

```csharp
internal sealed class ServiceBusCorrelationInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        // Tag posé en (1) ci-dessus sur l'Activity Azure Functions
        var traceparent = Activity.Current?.GetTagItem("messaging.source.traceparent") as string;
        if (traceparent == null) return;
        var parts = traceparent.Split('-');
        if (parts.Length < 4 || parts[1].Length != 32) return;
        telemetry.Context.Operation.Id       = parts[1]; // TraceId de l'activateur
        telemetry.Context.Operation.ParentId = parts[2]; // SpanId du messaging.send
    }
}
```

**Enregistrement** dans `Program.cs` de chaque worker (conditionnel à AppInsights) :
```csharp
services.AddSingleton<ITelemetryInitializer, ServiceBusCorrelationInitializer>();
```

---

### 13.10.9 Requêtes enterprise — transactions bout-en-bout

> **Syntaxe :** Application Insights → Logs  
> Ces requêtes exploitent `operation_Id` (W3C TraceId) pour corréler les 4 invocations d'une saga en une seule transaction bout-en-bout.

---

#### 🏢 E1 — Tableau de bord executive : santé plateforme en temps réel

```kusto
// RAPPORT EXÉCUTIF TEMPS RÉEL
// Pour le directeur technique : état de la plateforme en 5 indicateurs
// Rafraîchir toutes les 5 minutes sur un dashboard Azure Monitor

let periode = ago(1h);

// ── Indicateur 1 : Volume et taux de succès ──────────────────────────────
let kpi_volume = requests
| where timestamp > periode
| where cloud_RoleName contains "Booking"
| summarize
    total_invocations = count(),
    succes            = countif(success == true),
    echecs            = countif(success == false)
| extend taux_succes_pct = round(100.0 * succes / total_invocations, 2);

// ── Indicateur 2 : Latence bout-en-bout par saga ──────────────────────────
let kpi_latence = requests
| where timestamp > periode
| where cloud_RoleName contains "Booking"
| summarize debut = min(timestamp), fin = max(timestamp)
  by operation_Id                            // chaque operation_Id = 1 saga
| extend duree_saga_sec = datetime_diff('second', fin, debut)
| summarize
    p50_sec = percentile(duree_saga_sec, 50),
    p95_sec = percentile(duree_saga_sec, 95),
    p99_sec = percentile(duree_saga_sec, 99);

// ── Résultat fusionné ──────────────────────────────────────────────────────
kpi_volume
| extend p50_sec = toscalar(kpi_latence | project p50_sec),
         p95_sec = toscalar(kpi_latence | project p95_sec),
         taux_sla_ok = iff(toscalar(kpi_latence | project p95_sec) < 30,
                           "✅ SLA respecté (p95 < 30s)",
                           "⚠️ SLA dégradé")
| project total_invocations, taux_succes_pct, p50_sec, p95_sec, taux_sla_ok
```

---

#### 🔍 E2 — Reconstruire une transaction complète depuis n'importe quel identifiant

```kusto
// INVESTIGATION ENTERPRISE : partir de N'IMPORTE QUEL identifiant
// MessageId SB, SlipId, CorrelationId, DossierId, TraceId — tout fonctionne
// Use case : l'équipe support reçoit un ticket avec "le dossier D-001 est bloqué"

let identifiant = "D-001";   // ← MessageId, SlipId, CorrelationId ou DossierId

// Étape 1 : trouver l'operation_Id (TraceId W3C) à partir de l'identifiant
let operation = traces
| where timestamp > ago(7d)
| where tostring(customDimensions.SlipId)       contains identifiant
       or tostring(customDimensions.CorrelationId) contains identifiant
       or tostring(customDimensions.MessageId)     contains identifiant
| project operation_Id, SlipId = tostring(customDimensions.SlipId)
| take 1;

// Étape 2 : reconstruire TOUTE la transaction depuis cet operation_Id
let monOperationId = toscalar(operation | project operation_Id);

union requests, dependencies, traces, exceptions
| where timestamp > ago(7d)
| where operation_Id == monOperationId
| project
    timestamp,
    // Quel composant a émis cet événement
    composant    = replace_string(cloud_RoleName,
                       "RAMQ.Samples.Queue.RoutingSlip.Booking.", ""),
    // Type d'événement
    type_event   = case(
        itemType == "request",    "📥 Invocation",
        itemType == "dependency", "🔗 Appel SB/API",
        itemType == "trace" and severityLevel == 3, "❌ Erreur",
        itemType == "trace" and severityLevel == 2, "⚠️ Warning",
        itemType == "trace",      "ℹ️ Log",
        itemType == "exception",  "💥 Exception",
        "autre"),
    // L'étape saga courante
    etape        = tostring(customDimensions.StepName),
    // Le message ou nom de l'opération
    detail       = coalesce(message, name, outerMessage),
    // Durée si c'est une invocation ou un appel
    duree_ms     = duration
| order by timestamp asc
```

---

#### 📊 E3 — Analyse de performance : goulots d'étranglement dans la chaîne saga

```kusto
// ANALYSE DES GOULOTS D'ÉTRANGLEMENT
// Objectif : identifier quelle étape ralentit la saga bout-en-bout
// Montre la durée réelle de chaque étape + temps d'attente entre étapes

requests
| where timestamp > ago(24h)
| where cloud_RoleName contains "Booking"
| project
    timestamp,
    operation_Id,
    // Nom de l'étape = nom de la Function (ReserverVoiture, ReserverHotel, ReserverVol)
    etape         = name,
    // Durée d'exécution de cette invocation
    duree_exec_ms = duration,
    // La Function a-t-elle réussi ?
    succes        = success
// Calculer l'heure de fin de chaque invocation
| extend fin_invocation = timestamp + totimespan(strcat(tostring(toint(duree_exec_ms)), "ms"))
// Joindre avec la prochaine étape pour calculer le temps d'attente SB
| join kind=leftouter (
    requests
    | where cloud_RoleName contains "Booking"
    | project operation_Id, etape_suivante = name, debut_suivante = timestamp
  ) on operation_Id
| where debut_suivante > fin_invocation            // étape suivante APRÈS l'actuelle
| summarize
    duree_exec_p95_ms   = percentile(duree_exec_ms, 95),
    attente_sb_p95_ms   = percentile(datetime_diff('millisecond', debut_suivante, fin_invocation), 95),
    nb_invocations      = count()
  by etape
// Total = exécution + attente Service Bus
| extend total_p95_ms = duree_exec_p95_ms + attente_sb_p95_ms
| order by total_p95_ms desc
// ← L'étape en tête est le goulot d'étranglement
```

---

#### 🏥 E4 — Conformité CAI : audit complet d'un domaine sur une période

```kusto
// RAPPORT CONFORMITÉ CAI
// Objectif : prouver l'exhaustivité du traitement sur une période
// Pour la direction CAI/conformité : chaque dossier a-t-il été traité ?

let date_debut = datetime(2026-06-01);
let date_fin   = datetime(2026-06-04);

traces
| where timestamp between (date_debut .. date_fin)
| where isnotempty(customDimensions.CorrelationId)    // = DossierId RAMQ
| summarize
    // Pour chaque dossier
    nb_sagas_lancees    = dcount(tostring(customDimensions.SlipId)),
    nb_sagas_terminees  = dcountif(tostring(customDimensions.SlipId),
                              message contains "slip complet"),
    nb_sagas_dlq        = dcountif(tostring(customDimensions.SlipId),
                              message contains "lettre morte" or message contains "DLQ"),
    nb_sagas_en_cours   = dcountif(tostring(customDimensions.SlipId),
                              message !contains "slip complet"
                              and message !contains "DLQ"),
    premiere_activite   = min(timestamp),
    derniere_activite   = max(timestamp)
  by DossierId = tostring(customDimensions.CorrelationId)
| extend
    // Statut CAI : le dossier est-il entièrement traité ?
    statut_cai = case(
        nb_sagas_en_cours == 0 and nb_sagas_dlq == 0,  "✅ TRAITÉ",
        nb_sagas_dlq > 0,                               "❌ EN DLQ — action requise",
        nb_sagas_en_cours > 0,                          "⏳ EN COURS",
        "⚠️ INCONNU"),
    // Alerter si dernière activité > 48h (dossier potentiellement gelé)
    alerte_gel = iff(datetime_diff('hour', now(), derniere_activite) > 48,
                     "🚨 INACTIF > 48h", "OK")
| where statut_cai != "✅ TRAITÉ"                       // ← dossiers qui nécessitent attention
| order by statut_cai, derniere_activite asc
```

---

#### 🔗 E5 — Comparaison avant/après déploiement (regression testing)

```kusto
// ANALYSE D'IMPACT D'UN DÉPLOIEMENT
// Objectif : comparer automatiquement les métriques avant/après une mise en prod
// Use case : valider qu'un nouveau déploiement n'a pas dégradé les performances

let heure_deploiement = datetime(2026-06-04 14:00:00);
let fenetre_min       = 60;    // minutes à comparer de chaque côté

let avant = requests
| where timestamp between ((heure_deploiement - totimespan(strcat(tostring(fenetre_min), "m")))
                            .. heure_deploiement)
| where cloud_RoleName contains "Booking"
| summarize
    nb_invocations = count(),
    p95_ms         = percentile(duration, 95),
    taux_echec_pct = round(100.0 * countif(success == false) / count(), 2)
  by etape = name;

let apres = requests
| where timestamp between (heure_deploiement
                            .. (heure_deploiement + totimespan(strcat(tostring(fenetre_min), "m"))))
| where cloud_RoleName contains "Booking"
| summarize
    nb_invocations = count(),
    p95_ms         = percentile(duration, 95),
    taux_echec_pct = round(100.0 * countif(success == false) / count(), 2)
  by etape = name;

// Comparaison avant/après avec indicateur de régression
avant
| join kind=leftouter apres on etape
| project
    etape,
    p95_avant_ms   = p95_ms,
    p95_apres_ms   = p95_ms1,
    echec_avant    = taux_echec_pct,
    echec_apres    = taux_echec_pct1,
    // Détecter une régression : p95 augmente de > 20% ou taux d'échec double
    regression     = case(
        p95_ms1 > p95_ms * 1.2,              "🔴 RÉGRESSION LATENCE",
        taux_echec_pct1 > taux_echec_pct * 2, "🔴 RÉGRESSION TAUX ERREUR",
        p95_ms1 < p95_ms * 0.8,              "🟢 AMÉLIORATION",
        "🟡 STABLE")
| order by regression desc
```

---

#### 🌐 E6 — Vue multi-domaines : tous les types de sagas RAMQ

```kusto
// CONSOLIDATION MULTI-DOMAINES
// Objectif : tableau de bord unifié pour toutes les sagas RAMQ
// (Booking, TraiterDossier, ValiderAdresse, etc.)

requests
| where timestamp > ago(24h)
| where cloud_RoleName contains "RAMQ"
| summarize
    // Métriques par type de saga (SlipName)
    by_saga = make_bag(pack(
        "invocations", count(),
        "p95_ms",      percentile(duration, 95),
        "taux_echec",  round(100.0 * countif(success == false) / count(), 1)
    ))
  by SlipName = tostring(customDimensions.SlipName),
     composant = cloud_RoleName
| where isnotempty(SlipName)
| project SlipName, composant, by_saga
| order by SlipName, composant
```

---

### 13.10.10 Requêtes KQL — métriques EMT : scénarios enterprise avancés

> **Syntaxe :** Log Analytics → `customMetrics` (ou AppInsights → `customMetrics`)  
> Ces requêtes exploitent les métriques exposées par `MetricsProvider` (EMT) et instrumentées via `System.Diagnostics.Metrics`. Elles répondent aux questions opérationnelles de niveau enterprise que les traces et logs seuls ne permettent pas.

---

#### Catalogue des métriques EMT disponibles

| Métrique | Type | Dimensions | Usage |
|---|---|---|---|
| `messages_sent_total` | Counter | `entity_name`, `entity_type` | Volume envoyé par queue/topic |
| `messages_received_total` | Counter | `entity_name`, `entity_type` | Volume reçu par consumer |
| `messages_dlq_total` | Counter | `entity_name`, `reason` | Dead Letter par queue et raison |
| `send_duration_ms` | Histogram | `entity_name` | Latence d'envoi Service Bus |
| `receive_duration_ms` | Histogram | `entity_name` | Latence de traitement consumer |
| `retry_delay_ms` | Histogram | `attempt` | Distribution des délais retry |
| `immediate_retry_total` | Counter | `entity_name` | Volume retry immédiats |
| `exponential_retry_total` | Counter | `entity_name` | Volume retry exponentiels |
| `routing_slip_compensation_total` | Counter | `slip_name`, `reason` | Compensations saga (Fault) |
| `circuit_state` | Gauge | `entity` | État circuit breaker (0=Closed, 1=Open, 2=HalfOpen) |
| `circuit_transitions_total` | Counter | `entity`, `from`, `to` | Transitions circuit breaker |
| `deserialization_failures_total` | Counter | `reason` | Échecs de désérialisation |
| `duplicate_detected_total` | Counter | `entity_name` | Messages dupliqués interceptés |
| `claimcheck_uploads_total` | Counter | `entity_name` | Uploads blob Claim-Check |
| `claimcheck_downloads_total` | Counter | `entity_name` | Downloads blob Claim-Check |
| `claim_check_upload_duration_ms` | Histogram | `entity_name` | Latence upload blob |
| `claim_check_download_duration_ms` | Histogram | `entity_name` | Latence download blob |
| `journal_write_duration_ms` | Histogram | — | Latence écriture MTJ |
| `active_sessions` | Gauge | — | Sessions Service Bus actives |
| `cached_senders` | Gauge | — | Senders Service Bus en cache |

> **Convention de nommage dans `customMetrics` :** le nom de la métrique apparaît tel quel dans `name` (ex: `messages_dlq_total`). Les dimensions sont dans `customDimensions` (ex: `customDimensions.entity_name`).

---

#### 📊 M1 — Taux de Dead Letter par queue — déclencheur d'alerte critique

```kusto
// TAUX DLQ EN TEMPS RÉEL — Déclenche une alerte si > 5% sur une fenêtre de 10 min
// Cas d'usage : détection précoce d'une panne de service en aval (ex: API voiture hors service)
// Alerte Azure Monitor : configurer sur ce résultat avec seuil count > 0

let fenetre = 10m;
let seuil_dlq_pct = 5.0;

let sent = customMetrics
| where timestamp > ago(fenetre)
| where name == "messages_sent_total"
| summarize total_envoyes = sum(valueSum) by tostring(customDimensions.entity_name);

let dlq = customMetrics
| where timestamp > ago(fenetre)
| where name == "messages_dlq_total"
| summarize total_dlq = sum(valueSum) by tostring(customDimensions.entity_name);

sent
| join kind=leftouter dlq on $left.entity_name == $right.entity_name
| extend total_dlq = coalesce(total_dlq, 0.0)
| extend taux_dlq_pct = iff(total_envoyes > 0, round(100.0 * total_dlq / total_envoyes, 2), 0.0)
| where taux_dlq_pct > seuil_dlq_pct
| project entity_name, total_envoyes, total_dlq, taux_dlq_pct
| order by taux_dlq_pct desc
// → Résultat : liste des queues en dépassement de seuil DLQ
// → Si vide → système sain sur cette métrique
```

---

#### 📊 M2 — Tempête de retries — détection d'instabilité infrastructure

```kusto
// STORM DETECTOR — Détecte quand les retries dépassent 3x le volume normal
// Cas d'usage astreinte : taux de retry anormal = signal précoce de panne transitoire
// → Investiguer avec M1 (DLQ) et la requête D3 (§13.10.7) sur les logs Error

let baseline_minutes = 60; // fenêtre de référence (normale)
let alert_minutes    = 10; // fenêtre courte (alerte)

let baseline = customMetrics
| where timestamp between (ago(baseline_minutes * 1min) .. ago(alert_minutes * 1min))
| where name in ("immediate_retry_total", "exponential_retry_total")
| summarize baseline_rate = sum(valueSum) / (baseline_minutes - alert_minutes);

let current = customMetrics
| where timestamp > ago(alert_minutes * 1min)
| where name in ("immediate_retry_total", "exponential_retry_total")
| summarize current_total = sum(valueSum);

current
| extend baseline_rate = toscalar(baseline)
| extend expected = baseline_rate * alert_minutes
| extend ratio = iff(expected > 0, round(current_total / expected, 2), 0.0)
| extend statut = case(
    ratio > 5.0, "🔴 TEMPÊTE CRITIQUE",
    ratio > 3.0, "🟠 ANOMALIE DÉTECTÉE",
    ratio > 1.5, "🟡 LÉGÈRE HAUSSE",
    "🟢 NORMAL")
| project current_total, expected = round(expected, 1), ratio, statut
// → ratio > 3 = déclencher alerte P2
// → ratio > 5 = page astreinte immédiate
```

---

#### 📊 M3 — Distribution de latence P50/P95/P99 par queue

```kusto
// ANALYSE DE LATENCE — Distribution percentile par queue sur 1h
// Cas d'usage SLA : vérifier que P95 < 500ms (SLA RAMQ messaging)
// Utile avant/après un déploiement pour détecter une régression de performance

customMetrics
| where timestamp > ago(1h)
| where name == "receive_duration_ms"
| extend entity = tostring(customDimensions.entity_name)
| summarize
    p50  = percentile(value, 50),
    p95  = percentile(value, 95),
    p99  = percentile(value, 99),
    p999 = percentile(value, 99.9),
    volume = count(),
    max_ms = max(value)
  by entity
| extend sla_ok = iff(p95 < 500, "✅ SLA respecté", "❌ SLA dépassé")
| order by p95 desc
// → Colonnes : entity | p50 | p95 | p99 | p999 | volume | max_ms | sla_ok
// → Réf SLA RAMQ : P95 < 500ms, P99 < 2000ms
```

---

#### 📊 M4 — État des circuit breakers — tableau de bord temps réel

```kusto
// CIRCUIT BREAKER DASHBOARD — État instantané de tous les circuit breakers
// Cas d'usage : l'opérateur vérifie l'état des connexions Service Bus avant de traiter des tickets
// 0=Closed (normal), 1=Open (service en panne), 2=HalfOpen (test de récupération)

let etat_label = dynamic({"0": "🟢 Closed", "1": "🔴 Open", "2": "🟡 HalfOpen"});

customMetrics
| where name == "circuit_state"
| extend entity = tostring(customDimensions.entity)
| summarize etat = arg_max(timestamp, value) by entity  // dernière valeur connue
| extend etat_str = tostring(etat_label[tostring(toint(value))])
| project entity, etat_str, derniere_mesure = timestamp
| order by entity

// Compléter avec l'historique des transitions :
// customMetrics | where name == "circuit_transitions_total"
// | extend from = tostring(customDimensions.from), to = tostring(customDimensions.to)
// | summarize transitions = sum(valueSum) by entity, from, to
// | order by transitions desc
```

---

#### 📊 M5 — Analyse des échecs de désérialisation — détection de messages corrompus

```kusto
// DESERIALIZATION FAILURES — Identifier la cause et le volume par type d'erreur
// Cas d'usage CAI : un message corrompu dans la queue bloque-t-il d'autres traitements ?
// Corréler avec M1 (DLQ) : beaucoup de déséri failures = souvent des DLQ ensuite

customMetrics
| where timestamp > ago(24h)
| where name == "deserialization_failures_total"
| extend raison = tostring(customDimensions.reason)
| summarize
    total    = sum(valueSum),
    heure_pic = arg_max(timestamp, valueSum)
  by raison
| extend heure_premier_pic = heure_pic
| order by total desc
| project raison, total, heure_premier_pic
// → Si "PayloadTooLarge" → messages >  seuil → vérifier seuil ClaimCheck
// → Si "InvalidJson"     → producteur envoyant du JSON malformé
// → Si "NullPayload"     → message vide → bug producteur
```

---

#### 📊 M6 — Throughput et saturation par queue — capacity planning

```kusto
// CAPACITY PLANNING — Volume envoyé vs reçu par queue, détection de backlog
// Cas d'usage : identifier si une queue accumule des messages (consumer trop lent)
// Utile pour anticiper le besoin de scale-out avant une montée en charge RAMQ

let periode = 1h;

let envoyes = customMetrics
| where timestamp > ago(periode)
| where name == "messages_sent_total"
| summarize sent = sum(valueSum) by entity = tostring(customDimensions.entity_name);

let recus = customMetrics
| where timestamp > ago(periode)
| where name == "messages_received_total"
| summarize recv = sum(valueSum) by entity = tostring(customDimensions.entity_name);

envoyes
| join kind=fullouter recus on entity
| extend entity = coalesce(entity, entity1)
| extend sent = coalesce(sent, 0.0), recv = coalesce(recv, 0.0)
| extend backlog_pct = iff(sent > 0, round(100.0 * (sent - recv) / sent, 1), 0.0)
| extend statut = case(
    backlog_pct > 30, "🔴 BACKLOG CRITIQUE — scale-out requis",
    backlog_pct > 10, "🟠 BACKLOG MODÉRÉ — surveiller",
    backlog_pct > 0,  "🟡 LÉGÈRE ACCUMULATION",
    "🟢 ÉQUILIBRÉ")
| project entity, sent, recv, backlog_pct, statut
| order by backlog_pct desc
```

---

#### 📊 M7 — Performance Claim-Check — audit blob upload/download

```kusto
// CLAIM-CHECK PERFORMANCE — Latence des opérations blob pour les gros messages
// Cas d'usage : diagnostic si les messages ClaimCheck sont lents à traiter
// Seuil recommandé : upload P95 < 1000ms, download P95 < 500ms

customMetrics
| where timestamp > ago(6h)
| where name in ("claim_check_upload_duration_ms", "claim_check_download_duration_ms")
| extend operation = iff(name == "claim_check_upload_duration_ms", "upload", "download")
| extend entity = tostring(customDimensions.entity_name)
| summarize
    p50 = percentile(value, 50),
    p95 = percentile(value, 95),
    p99 = percentile(value, 99),
    volume = count()
  by operation, entity
| extend sla = case(
    operation == "upload"   and p95 < 1000, "✅",
    operation == "download" and p95 < 500,  "✅",
    "❌ DÉGRADÉ")
| order by operation, entity
```

---

#### 📊 M8 — Dashboard de santé global EMT — vue opérateur astreinte

```kusto
// HEALTH DASHBOARD — Synthèse en 1 tableau pour l'opérateur en astreinte
// Répond à la question : "Est-ce que tout va bien en ce moment ?"
// À afficher dans un Azure Monitor Workbook en mode auto-refresh 5 min

let fen = 15m;

// ── Métriques clés agrégées ───────────────────────────────────────────────────
customMetrics
| where timestamp > ago(fen)
| where name in (
    "messages_sent_total",
    "messages_received_total",
    "messages_dlq_total",
    "immediate_retry_total",
    "exponential_retry_total",
    "routing_slip_compensation_total",
    "deserialization_failures_total",
    "duplicate_detected_total"
  )
| summarize valeur = sum(valueSum) by name
| extend kpi = case(
    name == "messages_sent_total",              "📤 Messages envoyés",
    name == "messages_received_total",          "📥 Messages reçus",
    name == "messages_dlq_total",               "☠️  Dead Letters",
    name == "immediate_retry_total",            "🔄 Retry immédiats",
    name == "exponential_retry_total",          "⏱️  Retry exponentiels",
    name == "routing_slip_compensation_total",  "↩️  Compensations saga",
    name == "deserialization_failures_total",   "💥 Erreurs désérialisation",
    name == "duplicate_detected_total",         "🔁 Doublons détectés",
    name)
| extend statut = case(
    name == "messages_dlq_total"              and valeur > 0, "⚠️ ALERTE",
    name == "routing_slip_compensation_total" and valeur > 5, "⚠️ ALERTE",
    name == "deserialization_failures_total"  and valeur > 0, "⚠️ ALERTE",
    "✅ OK")
| project kpi, valeur = toint(valeur), statut
| order by kpi
// → Partager ce Workbook tile avec l'équipe astreinte RAMQ
// → Toute ligne "ALERTE" = creuser avec M1-M7 selon le KPI concerné
```

---

#### 📊 M9 — Corrélation métriques + traces — RCA complet (Root Cause Analysis)

```kusto
// ROOT CAUSE ANALYSIS — Croiser métriques DLQ avec les traces Error pour trouver la cause
// Cas d'usage : alerte DLQ reçue → identifier en < 5 min quel message et pourquoi

// Étape 1 : trouver les queues en DLQ sur les 30 dernières minutes
let queues_en_dlq = customMetrics
| where timestamp > ago(30m)
| where name == "messages_dlq_total"
| summarize dlq_count = sum(valueSum) by entity = tostring(customDimensions.entity_name)
| where dlq_count > 0
| project entity;

// Étape 2 : trouver les logs Error sur ces mêmes queues
traces
| where timestamp > ago(30m)
| where severityLevel >= 3   // Error et Critical
| where message contains "lettres mortes" or message contains "Fault" or message contains "DLQ"
| extend entity = tostring(customDimensions.entity_name ?? customDimensions.Step)
| where entity in (queues_en_dlq) or isempty(entity)
| project timestamp, message, operation_Id,
          MessageId = tostring(customDimensions.MessageId),
          Tentative = tostring(customDimensions.DeliveryCount)
| order by timestamp desc
| take 20
// → Chaque ligne = un message DLQ avec son MessageId, operation_Id et raison
// → Cliquer sur operation_Id → End-to-End Transaction view → parcours complet de la saga
```

---

#### 📊 M10 — SLA compliance RAMQ — rapport mensuel automatisé

```kusto
// RAPPORT SLA MENSUEL — Conformité aux engagements de service RAMQ
// À intégrer dans un Scheduled Query Rule (Azure Monitor) → rapport mail automatique
// Seuils SLA RAMQ v1 : DLQ < 0.1%, retry < 5%, latence P95 < 500ms

let mois = ago(30d);

// ── Taux de succès global ─────────────────────────────────────────────────────
let vol_sent = toscalar(customMetrics
    | where timestamp > mois and name == "messages_sent_total"
    | summarize sum(valueSum));

let vol_dlq = toscalar(customMetrics
    | where timestamp > mois and name == "messages_dlq_total"
    | summarize sum(valueSum));

let taux_dlq = iff(vol_sent > 0, round(100.0 * vol_dlq / vol_sent, 3), 0.0);

// ── Latence P95 ───────────────────────────────────────────────────────────────
let p95_latence = toscalar(customMetrics
    | where timestamp > mois and name == "receive_duration_ms"
    | summarize percentile(value, 95));

// ── Compensations saga ────────────────────────────────────────────────────────
let compensations = toscalar(customMetrics
    | where timestamp > mois and name == "routing_slip_compensation_total"
    | summarize sum(valueSum));

// ── Synthèse ──────────────────────────────────────────────────────────────────
print
    ["Volume total envoyé"]    = toint(vol_sent),
    ["Volume DLQ"]             = toint(vol_dlq),
    ["Taux DLQ (%)"]           = taux_dlq,
    ["SLA DLQ < 0.1%"]         = iff(taux_dlq < 0.1, "✅ CONFORME", "❌ HORS SLA"),
    ["Latence P95 (ms)"]       = round(p95_latence, 0),
    ["SLA Latence < 500ms"]    = iff(p95_latence < 500, "✅ CONFORME", "❌ HORS SLA"),
    ["Compensations saga"]     = toint(compensations)
```

---

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
| **Pattern A5** | Convention RAMQ : journalisation **découplée du chemin critique** (mécanisme). C'est le moyen technique du MTJ — pas sa finalité (qui est le BAM). |
| **BAM (Business Activity Monitoring)** | Pratique enterprise de monitoring **métier** des activités, par opposition à l'APM (technique). EMT implémente le BAM via le Message Transit Journal — stocké sur Azure Table 7 ans, exposé en Power BI et Workbooks Azure Monitor. Cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique). |
| **APM (Application Performance Monitoring)** | Pratique de monitoring **technique** des applications (logs, traces, métriques). EMT s'appuie sur Azure Monitor (Application Insights + Log Analytics). Complémentaire au BAM. |
| **MTJ (Message Transit Journal)** | Le journal d'audit EMT stocké en Azure Table, élevé au rang de **BAM enterprise stratégique** pour RAMQ. Cf. [§6.7](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique). |
| **Single pane of glass** | Vue unifiée d'observabilité où un opérateur peut entrer en un point quelconque (alerte business, log technique, dossier métier) et dériver dans toutes les dimensions sans changer d'outil. **Multiplicateur de productivité opérationnelle** — facteur 14× sur le MTTR mesuré. Cf. [§6.7.0](#67-message-transit-journal-mtj--business-activity-monitoring-stratégique). |
| **MTTR (Mean Time To Resolution)** | Temps moyen entre la détection d'un incident et sa résolution. Objectif RAMQ : MTTR < 30 min sur 80 % des incidents (KPI §13.12.9). |
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

*Document généré le 27 mai 2026 par revue agentique consolidée à partir des sources listées en en-tête + analyse des 37 projets `Exemples/` + audit SOLID ligne-par-ligne sur les classes principales d'EMT. Pour toute question, ouvrir une issue ou contacter l'équipe d'architecture RAMQ.*
