# EnterpriseMessageTransit — Architecture (résumé)

> Version résumée de la documentation technique.  
> Pour les détails complets (propriétés, méthodes, paramètres), voir [architecture-technique.md](architecture-technique.md).  
> Dernière mise à jour : 2026-02-25

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Pile technologique](#2-pile-technologique)
3. [Architecture et structure du projet](#3-architecture-et-structure-du-projet)
4. [Couche de configuration](#4-couche-de-configuration)
5. [Couche de messagerie (Messaging)](#5-couche-de-messagerie-messaging)
6. [Couche Provider (fournisseur)](#6-couche-provider-fournisseur)
7. [Sérialisation](#7-sérialisation)
8. [Gestion des exceptions](#8-gestion-des-exceptions)
9. [Patterns d'intégration supportés](#9-patterns-dintégration-supportés)
10. [Injection de dépendances](#10-injection-de-dépendances)
11. [Diagramme de classes simplifié](#11-diagramme-de-classes-simplifié)
12. [Flux de données](#12-flux-de-données)
13. [Glossaire](#13-glossaire)

---

## 1. Vue d'ensemble

**EnterpriseMessageTransit** est une bibliothèque .NET interne (NuGet d'entreprise) qui standardise l'accès à un bus de messages (Azure Service Bus actuellement). Elle fournit une couche d'abstraction pour publier et consommer des messages, appliquer des patterns d'intégration (Claim Check, Request/Reply, Saga, sessions), journaliser les opérations et gérer les erreurs (retry, dead-lettering).

L'objectif à long terme est de supporter plusieurs fournisseurs de messagerie sans modifier le code consommateur.

- **Namespace racine :** `RAMQ.COM.EnterpriseMessageTransit`
- **Assembly :** `RAMQ.COM.EnterpriseMessageTransit.dll`

---

## 2. Pile technologique

| Élément | Valeur |
|---|---|
| Framework | .NET 8.0 |
| Langage | C# (nullable activé, implicit usings) |
| Service Bus | Azure Service Bus (`Azure.Messaging.ServiceBus` v5.22.2) |
| Stockage Blob | Azure Blob Storage (`Azure.Storage.Blobs` v12.25.0) |
| Stockage Table | Azure Data Tables (`Azure.Data.Tables` v12.11.0) |
| Functions | Azure Functions Worker (Durable Task v1.7.1) |
| Observabilité | Application Insights Worker Service v2.23.0 |

---

## 3. Architecture et structure du projet

```
EnterpriseMessageTransit/
├── Configuration/                  # Configuration, settings, résolution d'endpoint
│   ├── Extensions/
│   │   └── ConfigurerProviders.cs  # Extension DI pour enregistrer les services Azure
│   ├── AppSettings.cs
│   ├── EndpointResolver.cs
│   ├── EndpointSettings.cs
│   ├── BlobStorageSetting.cs
│   ├── TransportSettings.cs
│   ├── ExponentialRetryPolicy.cs
│   ├── IConsumerConfigurationService.cs
│   ├── IMessageTransitConfigurationService.cs
│   ├── IProducerConfigurationService.cs
│   ├── ReplyToEndpointInfo.cs
│   └── SubscriptionInfoSettings.cs
├── Exceptions/                     # Exceptions métier et techniques
├── Messaging/                      # Cœur de la logique métier
│   ├── Consumer/                   # BaseConsumer, IMessageConsumer
│   ├── Producer/                   # BaseProducer, AnyProducer, IMessageProducer
│   ├── Providers/
│   │   └── Azure/                  # Implémentation spécifique Azure
│   ├── Enum/
│   ├── BaseMessageTransit.cs       # Classe de base commune
│   ├── MessageTransitContext.cs    # Enveloppe de contexte d'un message
│   ├── MessageTransitResponse.cs   # Réponse standardisée
│   ├── MessagingOptions.cs         # Options d'envoi
│   └── TokenMessage.cs             # Jeton Claim Check
├── Serialization/                  # Abstraction et implémentation JSON
└── EnterpriseMessageTransit.csproj
```

---

## 4. Couche de configuration

### Interfaces

- **`IMessageTransitConfigurationService`** — Interface racine exposant `AppSettings` et `BlobStorageSetting`.
- **`IConsumerConfigurationService`** — Hérite de la précédente, dédiée aux consumers.
- **`IProducerConfigurationService`** — Hérite de la précédente, dédiée aux producers.

**Contrainte :** L'application consommatrice doit fournir sa propre implémentation de ces interfaces et l'enregistrer dans le DI.

### Classes de configuration

- **`AppSettings`** — Configuration racine. Contient le namespace Service Bus, le nom d'application, le journal (table name + URI), la liste de points de terminaison (`Endpoints`) et la politique de retry. Validable via `IValidatableObject`.

- **`EndpointSettings`** — Représente un nœud dans l'itinéraire : un `Target` (identifiant logique) associé à un `Endpoint`. Le `Target` devient optionnel en mode mono-audience.

- **`TransportSettings`** — Décrit un endpoint Service Bus (nom d'entité, type Queue/Topic, subscription, TTL).

- **`SubscriptionInfoSettings`** — Information de subscription pour les Topics : `Consumer` (requis) + `Action` (optionnelle).

- **`BlobStorageSetting`** — Configuration du Blob Storage pour le pattern Claim Check (URI, conteneur, dossier, seuil en octets).

- **`ExponentialRetryPolicy`** — Politique de retry exponentiel (délai initial, délai max, jitter, max delivery count).

- **`ReplyToEndpointInfo`** — Endpoint pour le pattern Request/Reply (entity name, TTL, timeout).

### `AudienceResolver` (internal sealed)

Résout dynamiquement l'audience cible dans l'itinéraire selon le contexte :

1. **Mono-audience** → retourne la seule audience.
2. **Producer multi-audience** → résolution par `Target` exact.
3. **Consumer multi-audience** → Queue par `Target` ; Topic par `Target` → `Consumer` → `Consumer.Action`.

**Contrainte :** Vérifie l'absence de doublons de `Target`. Non injectable (instancié via `new`).

---

## 5. Couche de messagerie (Messaging)

### 5.1 `BaseMessageTransit<TMessage>` (abstract)

Classe de base commune dont héritent `BaseProducer<T>` et `BaseConsumer<T>`. Valide la configuration au démarrage, résout les audiences et détermine la nécessité du Claim Check.

**Dépendances :** `ILogger`, `IMessageTransitConfigurationService`, `IMessageSerializer`, `IStorageProvider`.

### 5.2 `MessageTransitContext<TMessage>`

Enveloppe générique transportant un message à travers les étapes. Contient le payload, les identifiants (`MessageId`, `SessionId`, `SequenceNumber`), l'étape courante (`CurrentStage`), les tokens Claim Check et des variables de métadonnées.

**Contrainte :** Expose actuellement un `ServiceBusReceivedMessage?` (couplage Azure résiduel).

### 5.3 `MessageTransitResponse`

Réponse standardisée retournée après publication ou consommation. Inclut un `StatusCode` HTTP, les indicateurs transient/permanent, le Claim Check appliqué, un message d'erreur et un `CorrelationId`.

### 5.4 `MessagingOptions`

Options d'envoi : propriétés applicatives, session, flux fichier, Claim Check forcé, target, mode offline.

### 5.5 `TokenMessage`

Jeton référençant un objet dans le Blob Storage (pattern Claim Check). Identifié par un `Kind` (Message ou File), un type MIME, une URL de référence et une taille.

### 5.6 Producer

- **`IMessageProducer<TPayload>`** — Contrat définissant `PublishAsync` (publication simple) et `GetResponseAsync` (Request/Reply).

- **`BaseProducer<TMessage>`** (abstract) — Implémente le publish et le Request/Reply. Orchestre la résolution d'audience, la préparation du Claim Check (upload Blob si seuil dépassé), l'envoi via `IMessagingProvider`, et la journalisation via `IJournalProvider`. Supporte les sessions Service Bus.

  **Contrainte :** Le `SessionId` est obligatoire si `EnableSession` est activé.

- **`AnyProducer<TMessage>`** — Producer générique prêt à l'emploi, sans logique additionnelle. Utilisé pour les cas simples.

- **`ClaimCheckOptions`** — Objet d'options pour le Claim Check avec factory methods (`None`, `WithAttachment`).

- **`IProducerPatterns`** — Interface pour les patterns producer (`PrepareClaimCheckAsync`, `ExecuteRequestReplyAsync`).

### 5.7 Consumer

- **`IMessageConsumer<TMessage>`** — Contrat définissant `ConsumeAsync`, `TryDeserializeMessage` et `DeadLetterAsync`.

- **`BaseConsumer<TMessage>`** (abstract) — Implémente la consommation de messages. Responsabilités :
  1. **Désérialisation / binding** — lie le message Service Bus natif à l'adapter, désérialise et aligne le `CurrentStage`.
  2. **Actions message** — `CompleteMessageAsync` (avec détection automatique de fin de Saga via `__FinalStageCompleted`), `DeadLetterAsync`, `ImmediateRetryAsync`, `ExponentialRetryAsync`.
  3. **Routage Saga** — `RouteToNextStageAsync` détermine la prochaine étape dans l'itinéraire, construit le nouveau contexte (avec propagation des variables), envoie et complète le message courant. Plusieurs surcharges : avec vérification d'étape attendue, avec transformation de payload, ou passage direct.
  4. **Validation d'étapes** — `AssertNextStageExists`, `AssertLastStage`, `IsLastStage`.

  **Contrainte :** Le consumer doit appeler `BindContext(message, actions)` avant toute opération.

- **`IConsumerPatterns`** — Interface marqueur vide (pas de membres définis).

---

## 6. Couche Provider (fournisseur)

### 6.1 Interfaces d'abstraction

| Interface | Rôle |
|---|---|
| `IMessagingProvider` | Envoi, Request/Reply, résolution d'audience, désérialisation. Hérite de `IMessageActions`. |
| `IMessagingAdapter` | Binding du contexte de message natif (pont Azure Functions ↔ bibliothèque). Hérite de `IMessageActions`. |
| `IMessageTransit` | Abstraction d'un message reçu (MessageId, Content, SequenceNumber). |
| `IMessageActions` | Actions sur un message : complete, immediate retry, exponential retry, dead-letter. Inclut `SetInvocationMetadata` et `BindContext`. |
| `IStorageProvider` | Upload vers le Blob Storage (Claim Check). Pas de download ni delete pour l'instant. |
| `IJournalProvider` | Écriture d'enregistrements d'audit dans une table de journal. |

### 6.2 Implémentation Azure

- **`AzureMessagingProvider`** — Implémente `IMessagingProvider`. Orchestre la résolution d'audience via `AudienceResolver`, l'envoi via `ServiceBusClient`, le Request/Reply (envoi + réception sur session), et la délégation des actions vers `IMessagingAdapter`.

- **`AzureFunctionMessagingAdapter`** — Implémente `IMessagingAdapter`. Pont entre Azure Functions Worker et la bibliothèque. Gère le binding du `ServiceBusReceivedMessage`, les actions de completion/retry/DLQ, et deux stratégies de retry exponentiel :
  - **Avec session :** backoff bloquant (`Task.Delay`) + abandon du message.
  - **Sans session :** clone du message + `ScheduleMessageAsync` à un instant futur + completion de l'original.

- **`AzureStorageProvider`** — Implémente `IStorageProvider`. Upload de contenu (texte ou flux) vers Azure Blob Storage.

- **`AzureJournalProvider`** — Implémente `IJournalProvider`. Écrit les enregistrements d'audit dans une Azure Table (consumer, action, messageId, correlationId, target, mode, status, delivery count, etc.).

- **`AzureFunctionMessageTransit`** — Implémente `IMessageTransit`. Wrapper autour de `ServiceBusReceivedMessage`.

- **`AzureMessagingProperties`** — Constantes pour les noms de propriétés applicatives (ReferralCount, Consumer, Action, etc.).

- **`AzureServiceBusProviderOptions`** — Options techniques spécifiques Azure (sessions, auto-lock, idle timeout, taille max message, timeout reply).

### Enums

- **`OperationMode`** — PUBLISH, REQUEST_REPLY, COMPLETE, RETRY, DLQ, DEFER.
- **`ServiceBusEntityType`** — None, Topic, Queue.
- **`ProcessingEvent`** — Queued, Started, Processing, Completed, Waiting, Failed.
- **`TokenKind`** — Message, File.

---

## 7. Sérialisation

- **`IMessageSerializer`** — Abstraction avec `Serialize<T>` et `Deserialize<T>`.
- **`JsonMessageSerializer`** — Implémentation basée sur `System.Text.Json`. Supporte l'indentation optionnelle via `AppSettings.EnableJsonIndentation`.

**Contrainte :** Les `JsonSerializerOptions` sont recréées à chaque appel (pas de cache).

---

## 8. Gestion des exceptions

Hiérarchie d'exceptions sémantiques, toutes avec un `StatusCode` optionnel :

| Exception | Usage |
|---|---|
| `ConfigurationException` | Configuration manquante ou invalide |
| `MessageSendException` | Échec d'envoi |
| `MessageCompletionException` | Échec de completion |
| `ImmediateRetryException` | Déclenche un retry immédiat |
| `ExponentialRetryException` | Déclenche un retry exponentiel |
| `ImmediateDLQException` | Envoi immédiat en dead-letter |
| `DeadLetteringFailedException` | Échec du dead-lettering lui-même |
| `TransitItineraryException` | Erreur dans l'itinéraire Saga |
| `RoutingSlipException` | Incohérence dans le routing slip |

---

## 9. Patterns d'intégration supportés

- **Publication simple** — Le producer sérialise, résout l'audience cible et envoie via Service Bus (Queue ou Topic).

- **Claim Check** — Au-delà du seuil (256 KB par défaut) ou si forcé, le payload est uploadé dans Blob Storage et remplacé par un `TokenMessage` contenant l'URL. Supporte aussi les pièces jointes (fichier séparé).

- **Request/Reply** — Envoi avec `SessionId` + `ReplyToSessionId`, puis écoute de la réponse sur cette session.

- **Saga (orchestration multi-étapes)** — L'itinéraire définit des audiences ordonnées. Le consumer utilise `RouteToNextStageAsync` pour avancer séquentiellement et pose `__FinalStageCompleted` à la dernière étape.

- **Retry intelligent** — Immédiat (abandon pour re-delivery) ou exponentiel (backoff + jitter, comportement différent avec/sans session).

- **Dead-letter** — Envoi automatique en DLQ après dépassement du max delivery count.

---

## 10. Injection de dépendances

L'extension `ConfigureAzureProviders()` enregistre tous les services (Scoped) :

```csharp
services.ConfigureAzureProviders();
```

| Service | Implémentation |
|---|---|
| `TableServiceClient` | Factory (TokenCredential + URI) |
| `BlobServiceClient` | Factory (TokenCredential + URI) |
| `ServiceBusClient` | Factory (TokenCredential + FQDN) |
| `IMessagingProvider` | `AzureMessagingProvider` |
| `IJournalProvider` | `AzureJournalProvider` |
| `IMessageSerializer` | `JsonMessageSerializer` |
| `IStorageProvider` | `AzureStorageProvider` |
| `IMessagingAdapter` | `AzureFunctionMessagingAdapter` |

**Prérequis :** L'application doit enregistrer un `TokenCredential` et une implémentation de `IMessageTransitConfigurationService`.

---

## 11. Diagramme de classes simplifié

```
                    ┌──────────────────────────────┐
                    │  IMessageTransitConfigService │
                    │  ├─ IConsumerConfigService    │
                    │  └─ IProducerConfigService    │
                    └──────────────┬───────────────┘
                                   │
                    ┌──────────────▼───────────────┐
                    │    BaseMessageTransit<T>      │
                    │  (validation, résolution)     │
                    └──────┬──────────────┬────────┘
                           │              │
              ┌────────────▼──┐    ┌──────▼─────────┐
              │ BaseProducer<T>│    │ BaseConsumer<T> │
              │ (publish, RR) │    │ (consume, saga) │
              └───────┬───────┘    └────────┬────────┘
                      │                     │
              ┌───────▼───────┐    ┌────────▼────────┐
              │AnyProducer<T> │    │  [Votre code]   │
              │  (générique)  │    │   hérite de      │
              └───────────────┘    │  BaseConsumer<T> │
                                   └─────────────────┘

  ───── Providers ─────
  ┌─────────────────────┐   ┌──────────────────┐   ┌─────────────────┐
  │  IMessagingProvider  │   │  IStorageProvider │   │ IJournalProvider │
  │  (send, RR, resolve) │   │  (upload blob)   │   │ (audit table)   │
  └──────────┬──────────┘   └────────┬─────────┘   └────────┬────────┘
             │                       │                       │
  ┌──────────▼──────────┐   ┌───────▼──────────┐   ┌───────▼─────────┐
  │AzureMessagingProvider│   │AzureStorageProvider│  │AzureJournalProv.│
  └─────────────────────┘   └──────────────────┘   └─────────────────┘
```

---

## 12. Flux de données

### Publication (Producer)

```
Application → BaseProducer.PublishAsync(context)
    ├── Résolution audience (AudienceResolver)
    ├── Préparation Claim Check → AzureStorageProvider.UploadAsync()
    ├── AzureMessagingProvider.SendAsync() → ServiceBus
    └── AzureJournalProvider.WriteRecordAsync() → Azure Table
```

### Consommation (Consumer)

```
Azure Function Trigger (ServiceBusReceivedMessage)
    → BindContext(message, actions)
    → DeserializeMessage<T>()
    → [Logique métier dans ConsumeAsync()]
        ├── CompleteMessageAsync()
        ├── RouteToNextStageAsync()   → prochaine étape Saga
        ├── ImmediateRetryAsync()     → abandon pour re-delivery
        ├── ExponentialRetryAsync()   → schedule ou abandon
        └── DeadLetterAsync()         → DLQ
    → AzureJournalProvider.WriteRecordAsync()
```

---

## 13. Glossaire

| Terme | Définition |
|---|---|
| **Audience** | Destinataire logique d'un message (Target + Endpoint) |
| **Endpoints** | Liste ordonnée d'audiences (v1.x Saga). Remplacer par `RoutingSlipBuilder` en v2.0. |
| **Target** | Identifiant logique d'une audience |
| **Stage** | Étape courante dans le flux de traitement |
| **Claim Check** | Payload volumineux stocké hors du message (Blob), remplacé par un token |
| **Token** | Référence vers un objet Blob Storage |
| **Dead-letter** | File de messages en échec permanent |
| **Saga** | Orchestration multi-étapes avec routage séquentiel |
| **Request/Reply** | Envoi + attente de réponse via session |
| **Referral Count** | Compteur de re-planifications (retry exponentiel sans session) |
| **Routing Slip** | Itinéraire de traitement embarqué dans le message |
