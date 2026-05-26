# EnterpriseMessageTransit — Documentation technique

> Documentation générée par rétro-ingénierie du code source.  
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

**EnterpriseMessageTransit** est une bibliothèque .NET interne (NuGet d'entreprise) qui standardise et simplifie l'accès à un bus de messages (Azure Service Bus actuellement). Elle fournit une couche d'abstraction permettant de :

- **Publier** des messages vers des Queues ou Topics Azure Service Bus.
- **Consommer** des messages reçus depuis ces entités.
- **Appliquer des patterns d'intégration** : Claim Check, Request/Reply, Saga (orchestration multi-étapes), sessions.
- **Journaliser** automatiquement les opérations dans une Azure Table.
- **Gérer les erreurs** avec des stratégies de retry (immédiat et exponentiel) et dead-lettering.

L'objectif à long terme est de supporter plusieurs fournisseurs de messagerie (ex. MuleSoft Anypoint MQ) sans modifier le code consommateur.

### Namespace racine

```
RAMQ.COM.EnterpriseMessageTransit
```

### Assembly

```
RAMQ.COM.EnterpriseMessageTransit.dll
```

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
├── Configuration/                  # Configuration, settings, résolution d'audience
│   ├── Extensions/
│   │   └── ConfigurerProviders.cs  # Extension DI pour enregistrer les services Azure
│   ├── AppSettings.cs              # Paramètres racine de l'application
│   ├── EndpointResolver.cs         # Résolution dynamique d'endpoint (producer/consumer)
│   ├── EndpointSettings.cs         # Configuration d'un endpoint (target + transport)
│   ├── BlobStorageSetting.cs       # Configuration du Blob Storage (Claim Check)
│   ├── EndpointInfoSettings.cs     # Détail d'un endpoint (entity name, type, subscription)
│   ├── ExponentialRetryPolicy.cs   # Politique de retry exponentiel
│   ├── IConsumerConfigurationService.cs
│   ├── IMessageTransitConfigurationService.cs
│   ├── IProducerConfigurationService.cs
│   ├── ReplyToEndpointInfo.cs      # Endpoint pour le pattern Request/Reply
│   └── SubscriptionInfoSettings.cs # Info subscription (consumer + action)
│
├── Exceptions/                     # Exceptions métier et techniques
│   ├── ConfigurationException.cs
│   ├── DeadLetteringFailedException.cs
│   ├── ExponentialRetryException.cs
│   ├── ImmediateDLQException.cs
│   ├── ImmediateRetryException.cs
│   ├── MessageCompletionException.cs
│   ├── MessageSendException.cs
│   ├── RoutingSlipException.cs
│   └── TransitItineraryException.cs
│
├── Messaging/                      # Cœur de la logique métier
│   ├── Consumer/
│   │   ├── BaseConsumer.cs         # Classe abstraite de base pour les consumers
│   │   ├── IConsumerPatterns.cs    # Interface marqueur pour les patterns consumer
│   │   └── IMessageConsumer.cs     # Contrat d'un consumer
│   ├── Producer/
│   │   ├── AnyProducer.cs          # Producer générique prêt à l'emploi
│   │   ├── BaseProducer.cs         # Classe abstraite de base pour les producers
│   │   ├── ClaimCheckOptions.cs    # Options du pattern Claim Check
│   │   ├── IMessageProducer.cs     # Contrat d'un producer
│   │   └── IProducerPatterns.cs    # Interface pour les patterns producer
│   ├── Providers/
│   │   ├── Azure/                  # Implémentation spécifique Azure
│   │   │   ├── Enum/
│   │   │   │   ├── OperationMode.cs
│   │   │   │   └── ServiceBusEntityType.cs
│   │   │   ├── AzureFunctionMessageTransit.cs
│   │   │   ├── AzureFunctionMessagingAdapter.cs
│   │   │   ├── AzureJournalProvider.cs
│   │   │   ├── AzureMessagingProperties.cs
│   │   │   ├── AzureMessagingProvider.cs
│   │   │   ├── AzureServiceBusProviderOptions.cs
│   │   │   └── AzureStorageProvider.cs
│   │   ├── IJournalProvider.cs     # Abstraction du journal
│   │   ├── IMessageActions.cs      # Actions sur les messages (complete, retry, DLQ)
│   │   ├── IMessageTransit.cs      # Abstraction d'un message reçu
│   │   ├── IMessagingAdapter.cs    # Adapter pour le binding du contexte de message
│   │   ├── IMessagingProvider.cs   # Fournisseur de messagerie (envoi, réception, résolution)
│   │   └── IStorageProvider.cs     # Abstraction du stockage (Blob)
│   ├── Enum/
│   │   ├── ProcessingEvent.cs
│   │   └── TokenKind.cs
│   ├── BaseMessageTransit.cs       # Classe de base commune (producer + consumer)
│   ├── MessageTransitContext.cs     # Enveloppe de contexte d'un message
│   ├── MessageTransitResponse.cs   # Réponse standardisée
│   ├── MessagingOptions.cs         # Options d'envoi d'un message
│   └── TokenMessage.cs             # Jeton pour le Claim Check (référence blob)
│
├── Serialization/
│   ├── IMessageSerializer.cs       # Abstraction de la sérialisation
│   └── JsonMessageSerializer.cs    # Implémentation JSON (System.Text.Json)
│
├── GlobalSuppressions.cs           # Suppressions d'avertissements d'analyse de code RAMQ
└── EnterpriseMessageTransit.csproj
```

---

## 4. Couche de configuration

### 4.1 `IMessageTransitConfigurationService`

Interface racine de configuration exposant :

| Propriété | Type | Description |
|---|---|---|
| `AppSettings` | `AppSettings?` | Configuration principale |
| `BlobStorageSetting` | `BlobStorageSetting?` | Configuration du stockage Blob |

Deux interfaces spécialisées en héritent :
- `IConsumerConfigurationService` — pour les consumers.
- `IProducerConfigurationService` — pour les producers.

### 4.2 `AppSettings`

| Propriété | Requis | Description |
|---|---|---|
| `ServiceBusNamespace` | Oui | FQDN du namespace Azure Service Bus |
| `ApplicationName` | Oui | Nom de l'application appelante |
| `MessageTransitJournalName` | Oui | Nom de la table Azure pour le journal |
| `MessageTransitJournalStoreUri` | Oui | URI du compte Azure Table Storage |
| `ConnectorUrl` | Non | URL d'un connecteur externe |
| `Itinerary` | Oui | Liste ordonnée d'`EndpointSettings` (les étapes de traitement) |
| `RetryPolicy` | Non | Politique de retry exponentiel |
| `EnableJsonIndentation` | Non | Active l'indentation JSON pour le débogage |


### 4.3 `EndpointSettings`

Représente un nœud dans l'itinéraire (une étape Saga ou un destinataire de message).

| Propriété | Description |
|---|---|
| `Target` | Identifiant logique de l'audience (optionnel si mono-audience) |
| `Endpoint` | Détails du endpoint Service Bus (`TransportSettings`) |

### 4.4 `TransportSettings`

| Propriété | Description |
|---|---|
| `EntityName` | Nom de l'entité Service Bus (queue ou topic) |
| `EntityType` | `Queue`, `Topic` ou `None` |
| `Subscription` | Information de subscription (pour les Topics) |
| `TTL` | Durée de vie optionnelle |

### 4.5 `SubscriptionInfoSettings`

| Propriété | Description |
|---|---|
| `Consumer` | Nom du consumer logique (requis) |
| `Action` | Action optionnelle pour différencier les abonnements |

### 4.6 `AudienceResolver`

Classe interne (`internal sealed`) qui effectue la résolution adaptative d'une audience dans l'itinéraire selon le contexte (producer vs consumer, mono vs multi-audience, Queue vs Topic).

**Règles de résolution (simplifiées) :**

1. **Mono-audience** → retourne la seule audience (assigne le `Target` à partir du `EntityName` si absent).
2. **Producer multi-audience** → résolution par `Target` exact.
3. **Consumer multi-audience** :
   - Queue : résolution par `Target`.
   - Topic : résolution par `Target` → puis par `Consumer` → puis par `Consumer` + `Action`.
4. Vérification de doublons de `Target` dans l'itinéraire.

### 4.7 `ExponentialRetryPolicy`

| Propriété | Défaut | Description |
|---|---|---|
| `InitialDelay` | 500 ms | Délai initial entre deux tentatives |
| `MaxDelay` | 60 s | Délai maximal |
| `UseJitter` | `true` | Ajoute un facteur aléatoire pour éviter le thundering herd |
| `MaxDeliveryCount` | 10 | Nombre maximal de tentatives avant DLQ |

### 4.8 `BlobStorageSetting`

| Propriété | Défaut | Description |
|---|---|---|
| `BlobServiceUri` | — | URI du service Blob Azure |
| `ContainerName` | — | Nom du conteneur |
| `FolderName` | — | Dossier de stockage dans le conteneur |
| `ClaimCheckThresholdBytes` | 256 KB | Seuil au-delà duquel le Claim Check s'applique automatiquement |

---

## 5. Couche de messagerie (Messaging)

### 5.1 `BaseMessageTransit<TMessage>`

Classe abstraite de base dont héritent `BaseProducer<T>` et `BaseConsumer<T>`.

**Responsabilités :**
- Validation de la configuration au démarrage (`ValidateConfiguration`).
- Résolution d'audience via l'itinéraire (`ResolveAudience`).
- Détermination de la nécessité du Claim Check (`RequiresClaimCheck`).

**Dépendances injectées :**
- `ILogger`
- `IMessageTransitConfigurationService`
- `IMessageSerializer`
- `IStorageProvider`

### 5.2 `MessageTransitContext<TMessage>`

Enveloppe générique transportant un message à travers les étapes de traitement.

| Propriété | Description |
|---|---|
| `MessageType` | Type logique du message |
| `Message` | Payload du message (sérialisé/désérialisé) |
| `MessageId` | Identifiant unique du message |
| `SessionId` | Identifiant de session (pour les sessions Service Bus) |
| `SequenceNumber` | Numéro de séquence attribué par Service Bus |
| `Attempt` | Numéro de la tentative courante |
| `CurrentStage` | Étape actuelle dans la Saga / l'itinéraire |
| `Tokens` | Liste de `TokenMessage` (références Claim Check) |
| `Variables` | Dictionnaire de métadonnées clé/valeur |
| `IsClaimCheckApplied` | Indicateur d'application du Claim Check |
| `ServiceBusMessage` | Référence au message Service Bus natif (non sérialisé) |

**Méthodes utilitaires :**
- `GetVariable<T>(key)` — récupère une variable typée.
- `GetMessageToken()` — retourne le token Claim Check du message.
- `GetFileToken()` — retourne le token Claim Check du fichier.
- `GetApplicationPropertyValue<T>(key)` — lecture de propriétés applicatives.

### 5.3 `MessageTransitResponse`

Réponse standardisée retournée par les opérations de publication et consommation.

| Propriété | Description |
|---|---|
| `StatusCode` | Code HTTP de statut |
| `Content` | Contenu textuel de la réponse |
| `IsTransient` | Erreur transitoire (peut être retentée) |
| `IsClaimCheckApplied` | Indicateur Claim Check |
| `IsPermanentFailure` | Échec permanent (non retentable) |
| `ErrorMessage` | Message d'erreur détaillé |
| `Metadata` | Métadonnées additionnelles |
| `CorrelationId` | Identifiant de corrélation |

### 5.4 `MessagingOptions`

Options passées lors de l'envoi d'un message.

| Propriété | Description |
|---|---|
| `Properties` | Propriétés applicatives (dictionnaire) |
| `EnableSession` | Active les sessions Service Bus |
| `FileStream` | Flux de fichier pour le Claim Check |
| `OriginalFileName` | Nom du fichier original |
| `ForceClaimCheck` | Force le Claim Check même sous le seuil |
| `Target` | Target explicite pour la résolution d'audience |
| `EnableOffline` | Mode hors-ligne |

### 5.5 `TokenMessage`

Représente un jeton pointant vers un objet stocké dans le Blob Storage (pattern Claim Check).

| Propriété | Description |
|---|---|
| `Kind` | `Message` ou `File` |
| `ContentType` | Type MIME |
| `Reference` | URL du Blob |
| `SizeBytes` | Taille en octets |

---

## 5.6 Producer

### `IMessageProducer<TPayload>`

Contrat d'un producer :

```csharp
Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
    MessageTransitContext<TPayload> context, ...);

Task<MessageTransitContext<MessageTransitResponse>?> GetResponseAsync(
    MessageTransitContext<TPayload> context, ...);
```

### `BaseProducer<TMessage>`

Classe abstraite implémentant `IMessageProducer<T>` et `IProducerPatterns`.

**Flux de `PublishAsync` :**

1. Validation du contexte et génération de `MessageId` si absent.
2. Résolution de l'audience via `IMessagingProvider.Resolve(Target)`.
3. Validation de `SessionId` si les sessions sont activées.
4. Alignement du `CurrentStage` pour la traçabilité.
5. Préparation du Claim Check si nécessaire (upload vers Blob Storage).
6. Construction des `MessagingOptions`.
7. Appel à `IMessagingProvider.SendAsync(...)`.
8. Écriture d'un enregistrement dans le journal (`IJournalProvider`).
9. Retour d'un `MessageTransitContext<MessageTransitResponse>`.

**Flux de `GetResponseAsync` (Request/Reply) :**

1. Mêmes étapes de préparation que `PublishAsync`.
2. Appel à `IMessagingProvider.RequestReplyAsync(...)` qui envoie puis attend la réponse sur une session.
3. Écriture dans le journal.
4. Retour du contexte de réponse.

**Méthode `PrepareContextWithTokensAsync` (Claim Check) :**

1. Sérialisation du contexte.
2. Si la taille dépasse le seuil ou si `ForceClaimCheck` est activé :
   - Upload du JSON vers Blob Storage.
   - Ajout d'un `TokenMessage` de type `Message` au contexte.
   - Suppression du payload du message (évite l'envoi doublé).
3. Si un `FileStream` est fourni :
   - Upload du fichier vers Blob Storage.
   - Ajout d'un `TokenMessage` de type `File`.

### `AnyProducer<TMessage>`

Producer générique prêt à l'emploi, héritant de `BaseProducer<T>` sans logique additionnelle. Utilisé pour les cas simples où aucune personnalisation n'est requise.

### `ClaimCheckOptions`

Objet d'options pour le pattern Claim Check :
- `FileStream` — flux de fichier à stocker.
- `OriginalFileName` — nom du fichier.
- `ForceClaimCheck` — force l'application même sous le seuil.
- `ClaimCheckOptions.None` — pas de Claim Check.
- `ClaimCheckOptions.WithAttachment(stream, name)` — factory pour les pièces jointes.

---

## 5.7 Consumer

### `IMessageConsumer<TMessage>`

Contrat d'un consumer :

```csharp
Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
    MessageTransitContext<TMessage> context, CancellationToken cancellationToken);

bool TryDeserializeMessage<TPayload>(out MessageTransitContext<TPayload>? context);

Task DeadLetterAsync(Exception exception, CancellationToken cancellationToken);
```

### `BaseConsumer<TMessage>`

Classe abstraite implémentant `IMessageConsumer<T>` et `IConsumerPatterns`.

**Responsabilités principales :**

1. **Désérialisation et binding** :
   - `BindContext(message, actions)` — lie le message Service Bus natif et les actions à l'adapter.
   - `DeserializeMessage<T>()` — désérialise le message via le provider et aligne le `CurrentStage`.

2. **Actions sur les messages** :
   - `CompleteMessageAsync` — marque le message comme traité. Détection automatique de la dernière étape Saga (pose le flag `__FinalStageCompleted`).
   - `DeadLetterAsync` — envoie le message en dead-letter queue.
   - `ImmediateRetryAsync` — retry immédiat (abandon du message pour retraitement).
   - `ExponentialRetryAsync` — retry exponentiel avec backoff.

3. **Routage Saga** (`RouteToNextStageAsync`) :
   - Résolution de l'étape courante dans l'itinéraire.
   - Détermination de la prochaine audience.
   - Construction du nouveau contexte avec propagation des `Variables`, `MessageId`, `SessionId`.
   - Envoi du message vers l'entité suivante.
   - Completion du message courant.
   - Surcharges avec vérification d'étape attendue (`expectedNextTarget`) et transformation de payload (`buildNextPayload`).

4. **Validation d'étapes** :
   - `AssertNextStageExists(stage, expected)` — vérifie que la prochaine étape correspond à celle attendue.
   - `AssertLastStage()` — affirme que l'étape courante est la dernière.
   - `IsLastStage()` — vérifie si l'étape est la dernière de l'itinéraire.

---

## 6. Couche Provider (fournisseur)

### 6.1 Interfaces d'abstraction

| Interface | Responsabilité |
|---|---|
| `IMessagingProvider` | Envoi, réception, résolution d'audience, désérialisation |
| `IMessagingAdapter` | Binding du contexte de message natif (pont entre Azure Functions et la bibliothèque) |
| `IMessageTransit` | Abstraction d'un message reçu (MessageId, Content, SequenceNumber) |
| `IMessageActions` | Actions sur un message : complete, immediate retry, exponential retry, dead-letter |
| `IStorageProvider` | Upload vers le stockage Blob (Claim Check) |
| `IJournalProvider` | Écriture dans le journal (table Azure) |

### 6.2 Implémentation Azure

#### `AzureMessagingProvider`

Implémente `IMessagingProvider`. Orchestre :
- La résolution d'audience via `AudienceResolver`.
- L'envoi via `ServiceBusClient.CreateSender(entityName)`.
- Le pattern Request/Reply (envoi + réception sur session).
- La délégation des actions message vers `IMessagingAdapter`.
- La désérialisation des messages reçus.

#### `AzureFunctionMessagingAdapter`

Implémente `IMessagingAdapter`. Pont entre Azure Functions Worker et la bibliothèque :
- `BindContext(message, actions)` — réceptionne le `ServiceBusReceivedMessage` et les `ServiceBusMessageActions` injectés par Azure Functions.
- `CompleteMessageAsync` — appelle `Actions.CompleteMessageAsync(Message)`.
- `ImmediateRetryAsync` — abandon du message si en dessous du max delivery count, sinon DLQ.
- `ExponentialRetryAsync` — deux stratégies :
  - **Avec session** : `Task.Delay` (backoff bloquant) + abandon.
  - **Sans session** : clone du message + `ScheduleMessageAsync` à un instant futur + completion du message original.
- `DeadLetterAsync` — envoi en DLQ avec raison et description.
- Journalisation de chaque action via `IJournalProvider`.

#### `AzureStorageProvider`

Implémente `IStorageProvider`. Upload de contenu (texte ou flux) vers Azure Blob Storage dans un conteneur et dossier configurés.

#### `AzureJournalProvider`

Implémente `IJournalProvider`. Écrit les enregistrements d'audit dans une Azure Table. Chaque enregistrement contient :
- Consumer, Action, MessageId, CorrelationId, Target
- Mode d'opération (PUBLISH, REQUEST_REPLY, COMPLETE, RETRY, DLQ, DEFER)
- StatusCode, DeliveryCount, MaxDeliveryCount, DeadLetterReason
- Horodatage d'enfilement (EnqueuedTimeUtc)

#### `AzureFunctionMessageTransit`

Implémente `IMessageTransit`. Wrapper autour de `ServiceBusReceivedMessage` exposant `MessageId`, `Content` et `SequenceNumber`.

---

## 7. Sérialisation

### `IMessageSerializer`

```csharp
string Serialize<TMessage>(TMessage obj);
TMessage? Deserialize<TMessage>(string data);
```

### `JsonMessageSerializer`

Implémentation basée sur `System.Text.Json`. Supporte l'indentation optionnelle via `AppSettings.EnableJsonIndentation`.

---

## 8. Gestion des exceptions

La bibliothèque définit une hiérarchie d'exceptions sémantiques :

| Exception | Usage |
|---|---|
| `ConfigurationException` | Configuration manquante ou invalide |
| `MessageSendException` | Échec d'envoi d'un message |
| `MessageCompletionException` | Échec de la completion d'un message |
| `ImmediateRetryException` | Déclenche un retry immédiat |
| `ExponentialRetryException` | Déclenche un retry exponentiel |
| `ImmediateDLQException` | Envoi immédiat en dead-letter |
| `DeadLetteringFailedException` | Échec du processus de dead-lettering lui-même |
| `TransitItineraryException` | Erreur dans l'itinéraire Saga (étape manquante, etc.) |
| `RoutingSlipException` | Incohérence dans le routing slip |

Toutes les exceptions exposent un `StatusCode` optionnel pour aligner avec les codes HTTP.

---

## 9. Patterns d'intégration supportés

### 9.1 Publication simple (Publish)

Le producer sérialise le message dans un `MessageTransitContext<T>`, résout l'audience cible et envoie via Azure Service Bus (Queue ou Topic).

### 9.2 Claim Check

Lorsque la taille d'un message dépasse le seuil configuré (256 KB par défaut) ou que `ForceClaimCheck` est activé :
1. Le contenu est uploadé dans Azure Blob Storage.
2. Un `TokenMessage` contenant l'URL du Blob remplace le payload dans le contexte.
3. Le message léger (avec token) transite par Service Bus.
4. Le consumer peut récupérer le contenu original via l'URL du token.

Supporte aussi les pièces jointes (fichier séparé du message).

### 9.3 Request/Reply

Le producer envoie un message avec un `SessionId` et `ReplyToSessionId` identiques, puis écoute la réponse sur cette session. Timeout configurable.

### 9.4 Saga (orchestration multi-étapes)

La liste de points de terminaison (`AppSettings.Endpoints`) définit une liste ordonnée d'audiences. Le consumer utilise `RouteToNextStageAsync` pour :
1. Déterminer l'étape suivante dans la liste d'endpoints.
2. Construire un nouveau contexte avec le payload transformé.
3. Envoyer vers la prochaine entité Service Bus.
4. Compléter le message courant.
5. Poser automatiquement le flag `__FinalStageCompleted` à la dernière étape.

> ⚠️ **Évolution v2.0 (Routing Slip)** : `RouteToNextStageAsync` et `AppSettings.Endpoints` multi-étapes seront remplacés par `IRoutingSlipActivity<TArgs>` + `RoutingSlipExecutor`. Voir `docs/architecture-routing-slip.md`.

### 9.5 Retry intelligent

Deux stratégies :
- **Retry immédiat** : abandon du message pour re-delivery par Service Bus.
- **Retry exponentiel** : calcul d'un délai avec backoff exponentiel + jitter optionnel. Comportement différent selon la présence de sessions.

### 9.6 Dead-letter

Les messages qui échouent au-delà du nombre maximal de tentatives sont automatiquement envoyés dans la dead-letter queue avec la raison de l'échec.

---

## 10. Injection de dépendances

L'extension `ConfigurerProviders.ConfigureAzureProviders()` enregistre tous les services nécessaires dans le conteneur DI :

```csharp
services.ConfigureAzureProviders();
```

**Services enregistrés (Scoped) :**

| Service | Implémentation |
|---|---|
| `TableServiceClient` | Factory (via `TokenCredential` + URI de config) |
| `BlobServiceClient` | Factory (via `TokenCredential` + URI de config) |
| `ServiceBusClient` | Factory (via `TokenCredential` + FQDN de config) |
| `IMessagingProvider` | `AzureMessagingProvider` |
| `IJournalProvider` | `AzureJournalProvider` |
| `IMessageSerializer` | `JsonMessageSerializer` |
| `IStorageProvider` | `AzureStorageProvider` |
| `IMessagingAdapter` | `AzureFunctionMessagingAdapter` |

**Prérequis :** L'application doit enregistrer au préalable :
- Un `TokenCredential` (ex. `DefaultAzureCredential`, `ManagedIdentityCredential`).
- Une implémentation de `IMessageTransitConfigurationService` (et/ou `IConsumerConfigurationService` / `IProducerConfigurationService`).

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

### Publication d'un message (Producer)

```
Application
    │
    ▼
BaseProducer.PublishAsync(context)
    │
    ├── Résolution audience (AudienceResolver)
    ├── Préparation Claim Check (si nécessaire)
    │     └── AzureStorageProvider.UploadAsync()
    ├── AzureMessagingProvider.SendAsync()
    │     └── ServiceBusClient.CreateSender().SendMessageAsync()
    └── AzureJournalProvider.WriteRecordAsync()
```

### Consommation d'un message (Consumer)

```
Azure Function Trigger (ServiceBusReceivedMessage)
    │
    ▼
BaseConsumer.BindContext(message, actions)
    │
    ▼
BaseConsumer.DeserializeMessage<T>()
    │
    ▼
[Votre logique métier dans ConsumeAsync()]
    │
    ├── CompleteMessageAsync()        → Actions.CompleteMessageAsync()
    ├── RouteToNextStageAsync()       → Envoi vers la prochaine étape Saga
    ├── ImmediateRetryAsync()         → Actions.AbandonMessageAsync()
    ├── ExponentialRetryAsync()       → ScheduleMessage / Abandon
    └── DeadLetterAsync()             → Actions.DeadLetterMessageAsync()
    │
    └── AzureJournalProvider.WriteRecordAsync()
```

---

## 13. Glossaire

| Terme | Définition |
|---|---|
| **Audience** | Destinataire logique d'un message (défini par un `Target` + un `Endpoint`) |
| **Itinerary** | Liste ordonnée d'audiences définissant les étapes d'un flux Saga |
| **Target** | Identifiant logique d'une audience dans l'itinéraire |
| **Stage** | Étape courante dans le flux de traitement (aligné sur `CurrentStage`) |
| **Claim Check** | Pattern EIP : le payload volumineux est stocké hors du message (Blob) et un token le remplace |
| **Token** | Référence vers un objet stocké dans le Blob Storage |
| **Dead-letter** | File de messages ayant échoué définitivement |
| **Saga** | Pattern d'orchestration multi-étapes avec routage séquentiel |
| **Request/Reply** | Pattern synchrone : envoi d'un message et attente d'une réponse via session |
| **Referral Count** | Compteur de re-planifications pour le retry exponentiel sans session |
| **Routing Slip** | Modèle où l'itinéraire de traitement est embarqué dans le message |
