# Producer Review — EnterpriseMessageTransit (EMT)

> **Date :** Avril 2026  
> **Périmètre :** Composants côté Producer de la librairie `RAMQ.COM.EnterpriseMessageTransit`  
> **Portée :** Architectural review · Code review · Code pattern review  
> **Convention :** 🔴 Bloquant · 🟠 Majeur · 🟡 Mineur · 🟢 Positif

---

## Lexique rapide

| Terme | Définition dans ce document |
|---|---|
| **Application cliente** | L'application (ex: Azure Function, API) qui injecte `IMessageProducer<T>` via le conteneur DI et appelle `PublishAsync` pour publier des messages. C'est elle le "consommateur" de la librairie EMT côté Producer. |
| **Consumer EMT** | Le composant interne de EMT qui *reçoit* les messages (classe `BaseConsumer`, à distinguer de l'application cliente). |
| **Provider** | L'implémentation infrastructure de `IMessagingProvider` (ex: `AzureMessagingProvider`). |
| **Target** | Identifiant logique d'un endpoint (ex: `"dispensateur"`, `"individu"`). Sert à résoudre la queue/topic Azure Service Bus. |
| **Claim Check** | Pattern Enterprise Integration : le payload volumineux est déposé en Blob Storage, et seule la référence (URL) voyage dans le message Service Bus. |

---

## Table des matières

1. [Cartographie des composants Producer](#1-cartographie-des-composants-producer)
2. [Architectural Review](#2-architectural-review)
3. [Code Review](#3-code-review)
4. [Code Pattern Review](#4-code-pattern-review)
5. [Vérification Clean Code et modularité](#5-vérification-clean-code-et-modularité)
6. [Synthèse et plan d'action](#6-synthèse-et-plan-daction)

---

## 1. Cartographie des composants Producer

```
IMessageProducer<TMessage>          ← Interface publique (injectée par l'application cliente via DI)
IProducerPatterns                   ← Interface patterns internes (ClaimCheck, RequestReply)
Producer<TMessage>                  ← Implémentation concrète
  └─ BaseMessageTransit<TMessage>   ← Classe abstraite de base partagée Consumer/Producer

PublishOptions / RequestReplyOptions   ← Value objects (record)
ClaimCheckOptions                      ← Value object (sealed class)
MessagingOptions                       ← DTO interne de transport vers le provider

IMessagingProvider                     ← Abstraction infrastructure
  └─ AzureMessagingProvider            ← Implémentation Azure Service Bus

IStorageProvider                       ← Abstraction Blob Storage
  └─ AzureStorageProvider              ← Implémentation Azure Blob

IJournalProvider                       ← Abstraction journalisation
  └─ AzureJournalProvider              ← Implémentation Azure Table Storage

ServiceBusSenderCache                  ← Cache singleton des senders ASB

IMessageTargetMap / MessageTargetMap   ← Résolution TMessage → target
ProducerServiceCollectionExtensions    ← Configuration DI (AddProducer<T>)
EndpointResolver                       ← Résolution des endpoints
```

> **Note pour les développeurs juniors :** L'application cliente (ex: une Azure Function, une API REST) est celle qui appelle `PublishAsync`. Elle ne connaît que `IMessageProducer<T>`. La librairie EMT gère tout le reste : sérialisation, routage, Claim Check, envoi Service Bus, journalisation.

**Flux d'exécution pour `PublishAsync` :**

```
Application cliente (ex: Azure Function)
  → Producer<T>.PublishAsync(context, options)
    → PublishCoreAsync()
      → ValidateRoutingProperties()                      // vérifie que seules "Consumer" et "Action" sont passées
      → IMessageTargetMap.ResolveTarget<TMessage>()      // résolution du target via la liaison DI (ex: "dispensateur")
      → IMessagingProvider.Resolve(target)               // résolution de l'endpoint (queue/topic ASB)
      → PrepareClaimCheckAsync()                         // serialize + blob upload si payload trop grand
      → IMessagingProvider.SendAsync()                   // envoi Service Bus
        → AzureMessagingProvider.SendAsync()
          → ServiceBusSenderCache.GetOrCreate()          // récupère un sender mis en cache (réutilisation TCP)
          → ServiceBusClient.CreateSender().SendMessageAsync()
      → IJournalProvider.WriteRecordAsync()              // journalisation Azure Table Storage
```

---

## 2. Architectural Review

### 2.1 ✅ Points forts

#### Séparation des responsabilités (SRP)
La librairie respecte globalement le Single Responsibility Principle. Chaque composant a une responsabilité claire : `Producer` orchestre, `AzureMessagingProvider` envoie, `AzureStorageProvider` stocke les blobs, `AzureJournalProvider` journalise. La couche de résolution (`EndpointResolver`, `IMessageTargetMap`) est également isolée.

#### Abstraction des infrastructures
L'utilisation d'interfaces (`IMessagingProvider`, `IStorageProvider`, `IJournalProvider`) rend le code découplé des infrastructures Azure. Un implémenteur peut substituer n'importe quel provider sans toucher au `Producer`. C'est conforme au Dependency Inversion Principle (DIP).

**Ce que ça permet concrètement :** pour les tests unitaires, on injecte un mock de `IMessagingProvider` → le test ne parle jamais à Azure Service Bus. L'application cliente ne change rien.

#### Design DI fluide
Le pattern `services.AddProducer<TMessage>("target")` via `ProducerServiceCollectionExtensions` est ergonomique, idiomatique pour .NET, et élimine efficacement le besoin de sous-classes vides.

#### Cache des senders ASB (`ServiceBusSenderCache`)
La mutualisation des `ServiceBusSender` via un singleton `ConcurrentDictionary` est une bonne pratique pour les Azure Functions qui sont stateless par nature. Évite la création de milliers de connexions TCP à chaque invocation.

---

### 2.2 🔴 Problème architectural bloquant

#### `IProducerPatterns` : interface interne exposée publiquement avec fuite d'abstraction

**Fichier :** `Messaging/Producer/IProducerPatterns.cs`

**Explication pour les développeurs juniors :** Une "fuite d'abstraction" se produit quand une interface censée être stable et propre expose des détails internes d'implémentation. Ici, `IProducerPatterns` est `public` et impose à toute classe qui l'implémente de connaître `IMessagingProvider` — une abstraction infrastructure qui n'a pas sa place dans un contrat de haut niveau.

```csharp
// ACTUEL - problématique
public interface IProducerPatterns
{
    Task PrepareClaimCheckAsync<TMessage>(...);

    // Pourquoi l'appelant doit-il passer un IMessagingProvider ?
    // Le Producer l'a déjà en champ injecté (_messagingProvider).
    Task<MessageTransitContext<MessageTransitResponse>?> ExecuteRequestReplyAsync<TMessage>(
        IMessagingProvider messagingProvider,   // ← fuite d'abstraction
        MessageTransitContext<TMessage> context,
        MessagingOptions options,
        CancellationToken cancellationToken) where TMessage : class;
}
```

**Conséquences concrètes :**
1. Toute modification interne de ces signatures est un **breaking change** pour les classes externes qui implémentent cette interface.
2. L'application cliente peut, par erreur, injecter `IProducerPatterns` au lieu de `IMessageProducer<T>` et appeler des méthodes internes.
3. `IMessagingProvider` dans la signature force toute implémentation externe à avoir une référence vers l'infrastructure Azure — violation du DIP.

**Recommandation :** Réduire la visibilité à `internal`. Cette interface ne sert qu'au `Producer` lui-même. Supprimer le paramètre `IMessagingProvider` redondant : le `Producer` utilise son champ `_messagingProvider` injecté.

```csharp
// RECOMMANDÉ
internal interface IProducerPatterns
{
    Task PrepareClaimCheckAsync<TMessage>(...);
    // Plus de IMessagingProvider en paramètre — le Producer utilise son champ injecté
    Task<MessageTransitContext<MessageTransitResponse>?> ExecuteRequestReplyAsync<TMessage>(
        MessageTransitContext<TMessage> context,
        MessagingOptions options,
        CancellationToken cancellationToken) where TMessage : class;
}
```

---

### 2.3 🟠 Problèmes architecturaux majeurs

#### État mutable dans `AzureMessagingProvider` — risque de race condition

**Fichier :** `Messaging/Providers/Azure/AzureMessagingProvider.cs`

**Explication pour les développeurs juniors :** Une **race condition** (condition de course) se produit quand deux fils d'exécution (threads) lisent ou modifient le même état partagé en même temps, produisant un résultat imprévisible et difficile à reproduire. C'est l'un des bugs les plus difficiles à diagnostiquer en production, car il ne se manifeste que sous charge.

`AzureMessagingProvider` stocke le contexte de routage dans des champs d'instance mutables :

```csharp
// ACTUEL — champs mutables d'instance
public class AzureMessagingProvider : IMessagingProvider
{
    private string? _target;    // ← état mutable partagé
    private string? _consumer;  // ← état mutable partagé
    private string? _action;    // ← état mutable partagé

    // Appelé par le Consumer EMT pour injecter le contexte de l'invocation entrante
    public void SetInvocationMetadata(string? target, string? consumer, string? action)
    {
        _target   = target;
        _consumer = consumer;
        _action   = action;
    }

    public EndpointSettings Resolve(string? target)
    {
        // Utilise _target comme fallback si target est null
        if (_endpointResolver.TryResolve(target ?? _target, _consumer, _action, out var aud) ...)
        //                                 ↑ peut valoir le mauvais target si un autre thread l'a modifié
    }
}
```

**Scénario de bug concret (pseudo-code) :**

```
// Deux invocations concurrentes dans un hôte multi-thread

// Thread A — message pour "dispensateur"
provider.SetInvocationMetadata("dispensateur", "consA", "actionA");
//   ↑ Thread A écrit _target = "dispensateur"

    // Thread B s'intercale ici avant que Thread A appelle SendAsync
    provider.SetInvocationMetadata("individu", "consB", "actionB");
    //   ↑ Thread B écrase _target = "individu"

// Thread A reprend l'exécution — _target vaut maintenant "individu"
await provider.SendAsync(contextA, options, ct);
// ↑ Bug silencieux : le message de Thread A est envoyé vers la queue "individu" !
//   Aucune exception. Le message est perdu dans la mauvaise queue.
```

**Pourquoi c'est risqué dans le contexte actuel :** `AzureMessagingProvider` est `Scoped` (une instance par requête dans ASP.NET Core ou par invocation de Function). En théorie Scoped, une seule invocation à la fois utilise l'instance. Mais si la durée de vie est changée en `Singleton` par erreur, ou si un futur hôte parallélise les invocations dans le même scope, le bug apparaît immédiatement.

**Recommandation :** Supprimer les champs `_target`, `_consumer`, `_action`. Passer ces valeurs directement via `MessagingOptions`, qui est déjà transmis à chaque appel.

```csharp
// RECOMMANDÉ — pas d'état mutable, le contexte voyage avec l'appel
public async Task SendAsync<T>(
    MessageTransitContext<T> context,
    MessagingOptions options,   // ← options.Target, options.Properties contiennent Consumer/Action
    CancellationToken cancellationToken)
{
    // Résolution directe depuis options — pas de champ d'instance
    var audience = _endpointResolver.Resolve(options.Target);
    // ...
}
```

#### `AzureMessagingProvider` dépend de `Microsoft.Azure.Functions.Worker`

**Fichier :** `Messaging/Providers/Azure/AzureMessagingProvider.cs`

`AzureMessagingProvider` importe `Microsoft.Azure.Functions.Worker`, qui est le runtime spécifique aux Azure Functions. Un provider d'infrastructure générique (Service Bus) ne devrait pas dépendre d'un runtime d'hébergement particulier. Si EMT est utilisé dans une application Console, un Worker Service ou une API ASP.NET Core, ce couplage crée des dépendances de packaging inutiles.

**Recommandation :** Isoler ce couplage exclusivement dans `AzureFunctionMessagingAdapter`, dont c'est le rôle déclaré.

---

### 2.4 🟡 Observations architecturales mineures

#### Absence de stratégie de retry configurable pour le Producer

**Contexte pour les développeurs juniors :** La librairie dispose d'une classe `ExponentialRetryPolicy` dans `AppSettings.RetryPolicy`. Cette politique est conçue pour le **Consumer EMT** : elle contrôle le délai entre les tentatives de retraitement d'un message (`InitialDelay`, `MaxDelay`, `MaxDeliveryCount`). Ces concepts sont propres au côté consommateur de Service Bus.

Le Producer a un besoin fondamentalement différent : retenter un *envoi* en cas d'erreur réseau/SDK transitoire. Ces deux politiques ne doivent pas être confondues :

| Aspect | `ExponentialRetryPolicy` (Consumer) | Retry Producer (absent) |
|---|---|---|
| Déclenché par | Erreur de traitement métier du message reçu | Erreur réseau/connexion lors de l'envoi |
| Délais | Secondes à minutes | Millisecondes |
| `MaxDeliveryCount` | Oui — concept ASB (nombre de relivraisons) | Non applicable |
| Configuré aujourd'hui | ✅ Dans `AppSettings.RetryPolicy` | ❌ Codé en dur dans `AzureMessagingProvider` |

**Situation actuelle dans `AzureMessagingProvider` :**

```csharp
// ACTUEL — retry ad-hoc, non configurable, 1 seule tentative
catch (Exception ex) when (IsFatalSendException(ex))
{
    var newSender = await _senderCache.ReplaceSenderAsync(_client, entityName);
    await newSender.SendMessageAsync(message, cancellationToken);
    // Si ça échoue encore → exception propagée, pas d'autres tentatives
}
```

**Recommandation :** Ajouter une propriété `SendRetry` dans `TransportSettings` (déjà le bon endroit pour les paramètres par endpoint) :

```csharp
// 1. Dans TransportSettings — ajouter la politique de retry Producer
public class TransportSettings
{
    [Required] public string EntityName { get; set; } = default!;
    public MessagingEntityType EntityType { get; set; }
    public bool EnableSession { get; set; }
    public TimeSpan? TTL { get; set; }
    public SubscriptionInfoSettings? Subscription { get; set; }

    // NOUVEAU — politique de retry pour l'envoi (Producer uniquement)
    public ProducerSendRetryPolicy? SendRetry { get; set; }
}

// 2. Nouvelle classe — distincte de ExponentialRetryPolicy (Consumer)
public class ProducerSendRetryPolicy
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public static ProducerSendRetryPolicy Default => new();
}

// 3. Dans AzureMessagingProvider.SendAsync — utiliser la politique configurée
var retryPolicy = audience.Endpoint.SendRetry ?? ProducerSendRetryPolicy.Default;
var attempt = 0;
var sender = _senderCache.GetOrCreate(_client, audience.Endpoint.EntityName);

while (true)
{
    try
    {
        await sender.SendMessageAsync(message, cancellationToken);
        break; // succès — sortir de la boucle
    }
    catch (Exception ex) when (IsFatalSendException(ex) && attempt < retryPolicy.MaxAttempts)
    {
        attempt++;
        _logger.LogWarning(ex, "Envoi échoué (tentative {Attempt}/{Max}), remplacement du sender...",
            attempt, retryPolicy.MaxAttempts);
        await Task.Delay(retryPolicy.InitialDelay * attempt, cancellationToken);
        sender = await _senderCache.ReplaceSenderAsync(_client, audience.Endpoint.EntityName);
    }
    // Les erreurs non-fatales et les dépassements de MaxAttempts propagent l'exception normalement
}
```

#### Le journal est dans le chemin critique d'envoi

Après chaque `SendAsync` réussi, `WriteRecordAsync` est appelé dans le même flux synchrone. Si la journalisation Azure Table Storage échoue, l'exception est propagée à l'application cliente — qui perçoit un échec d'envoi alors que le message a bien été publié sur Service Bus. Ce couplage fort peut rendre le diagnostic difficile.

---

## 3. Code Review

### 3.1 🟠 `Producer.PublishBatchAsync` — double appel à `ExtractMessageProperties`

**Fichier :** `Messaging/Producer/Producer.cs`

```csharp
// ACTUEL — ExtractMessageProperties appelé 2x par message dans la boucle
foreach (var ctx in contextsList)
{
    var journalEntry = new Messaging.Providers.JournalEntry(
        ExtractMessageProperties(properties).Consumer ?? "(none)",   // ← appel 1
        ExtractMessageProperties(properties).Action ?? "(none)",     // ← appel 2
        ...
    );
}
```

`ExtractMessageProperties(properties)` est appelé deux fois par itération dans la boucle du journal de `PublishBatchAsync`. Comme `properties` est constant sur l'ensemble du batch, cette extraction devrait être faite une seule fois avant la boucle.

**Recommandation :** Extraire avant la boucle et réutiliser le tuple.

```csharp
// RECOMMANDÉ
(string? consumer, string? action) = ExtractMessageProperties(properties);
foreach (var ctx in contextsList)
{
    var journalEntry = new JournalEntry(
        consumer ?? "(none)",
        action ?? "(none)",
        ...
    );
}
```

---

### 3.2 🟠 `AzureMessagingProvider.SendBatchAsync` — gestion batch incomplète avec perte silencieuse

**Fichier :** `Messaging/Providers/Azure/AzureMessagingProvider.cs`

```csharp
// ACTUEL — logique incomplète : nextBatch est créé mais jamais envoyé
using var nextBatch = await sender.CreateMessageBatchAsync(cancellationToken);
if (!nextBatch.TryAddMessage(message))
{
    await sender.SendMessageAsync(message, cancellationToken);
}
else
{
    // continue with nextBatch - for simplicity we send current and continue
    // ← nextBatch est jeté en fin de bloc using sans être envoyé !
}
```

La branche `else` du cas "message ajouté au nextBatch" ne fait rien : `nextBatch` sort du bloc `using` et est disposé sans être envoyé. Les messages qui entrent dans cette branche sont **silencieusement perdus**. C'est un bug de logique sérieux.

**Recommandation :** Implémenter correctement la gestion des batches séquentiels, ou utiliser une approche plus simple (liste de batches) afin d'éliminer ce risque de perte de message.

---

### 3.3 🟠 `ServiceBusSenderCache.ReplaceSenderAsync` — `async Task` dans un `lock`

**Fichier :** `Messaging/Providers/Azure/ServiceBusSenderCache.cs`

```csharp
// ACTUEL — appel fire-and-forget non awaité dans un lock
lock (gate)
{
    // ...
#pragma warning disable CS4014
    DisposeSenderAsync(oldSender);  // ← fire-and-forget non contrôlé
#pragma warning restore CS4014
    return newSender;
}
```

`ReplaceSenderAsync` est déclarée `async Task` mais retourne dans un bloc `lock` synchrone, ce qui est correct. Cependant, `DisposeSenderAsync` est appelée sans `await` dans le lock, supprimant délibérément l'avertissement CS4014 avec `#pragma`. Les exceptions lancées par `DisposeSenderAsync` seront non observées. La méthode `ReplaceSenderAsync` n'est pas réellement asynchrone et devrait être `Task<ServiceBusSender>` retournant `Task.FromResult(newSender)` pour être honnête sur sa nature synchrone.

**Recommandation :** Soit rendre la méthode synchrone (`ServiceBusSender ReplaceSender(...)`), soit implémenter un mécanisme correct de disposal asynchrone sans fire-and-forget.

---

### 3.4 ✅ `RequestReplyAsync` — timeout via `AzureServiceBusProviderOptions` *(corrigé)*

**Fichier :** `Messaging/Providers/Azure/AzureMessagingProvider.cs`

**Problème initial :** Le timeout de réception de la réponse Request/Reply était codé en dur à 30 secondes, sans possibilité de surcharge par endpoint ou par application.

**Ce qui a été changé :**

`AzureServiceBusProviderOptions` est injecté dans `AzureMessagingProvider` et sa propriété `ReplyTimeout` est utilisée dans `RequestReplyAsync` :

```csharp
// AVANT — hardcodé, non configurable
var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);

// APRÈS — valeur par défaut centralisée dans AzureServiceBusProviderOptions
var received = await receiver.ReceiveMessageAsync(_providerOptions.ReplyTimeout, cancellationToken);
```

**Valeur par défaut :** `AzureServiceBusProviderOptions.ReplyTimeout = TimeSpan.FromMinutes(5)`.

**Pourquoi 5 minutes ?**
- 30 secondes était trop court pour des appels à des systèmes legacy (WCF, SOAP) ou des traitements complexes.
- 5 minutes est une valeur enterprise raisonnable qui couvre la grande majorité des cas sans bloquer indéfiniment.
- Si le CancellationToken de la requête HTTP arrive à expiration avant, l'opération est annulée proprement.

**Comment surcharger la valeur par défaut** (enregistrer avant `ConfigureAzureProviders()`) :

```csharp
// Program.cs ou Startup.cs
services.AddSingleton(new AzureServiceBusProviderOptions
{
    ReplyTimeout = TimeSpan.FromMinutes(2)   // SLA plus strict
});
services.ConfigureAzureProviders();   // utilise TryAddSingleton → la valeur ci-dessus est conservée
```

**`AzureServiceBusProviderOptions` est enregistré en DI** dans `ConfigureAzureProviders()` avec `TryAddSingleton(new AzureServiceBusProviderOptions())`. Toutes les propriétés disponibles :

```csharp
public class AzureServiceBusProviderOptions
{
    public int MaxConcurrentSessions { get; set; }           = 10;
    public int MaxConcurrentCallsPerSession { get; set; }    = 1;
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan SessionIdleTimeout { get; set; }         = TimeSpan.FromSeconds(10);
    public int MaxMessageSize { get; set; }                  = 256 * 1024;
    public TimeSpan ReplyTimeout { get; set; }               = TimeSpan.FromMinutes(5);  // ← utilisé
}
```

---

### 3.5 🟠 `RequestReplyAsync` — `NullReferenceException` utilisée incorrectement

**Fichier :** `Messaging/Providers/Azure/AzureMessagingProvider.cs`

```csharp
// ACTUEL — mauvais type d'exception
if (context.Message == null)
{
    throw new NullReferenceException(nameof(context.Message));
}
```

`NullReferenceException` est une exception runtime non contrôlée, réservée par le CLR aux déréférencements nuls inattendus. Lever une `NullReferenceException` explicitement est un anti-pattern. La bonne exception pour un argument invalide est `ArgumentNullException` ou `ArgumentException`.

**Recommandation :**
```csharp
ArgumentNullException.ThrowIfNull(context.Message, nameof(context.Message));
```

---

### 3.6 🟡 `ValidateRoutingProperties` — validation trop restrictive et non documentée

**Fichier :** `Messaging/Producer/Producer.cs`

```csharp
// ACTUEL — seules "Consumer" et "Action" sont autorisées
var invalidKeys = properties.Keys
    .Where(k => !string.Equals(k, "Consumer", StringComparison.OrdinalIgnoreCase)
             && !string.Equals(k, "Action", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (invalidKeys.Count > 0)
{
    throw new ConfigurationException(...);
}
```

La règle qui n'autorise que les clés `Consumer` et `Action` dans le dictionnaire `properties` est une contrainte métier forte. Elle n'est pas documentée dans la Javadoc de `IMessageProducer.PublishAsync`. Le message d'erreur parle de "métadonnées de routage" sans expliquer le modèle de données. Un développeur junior ne comprendra pas pourquoi ses propriétés personnalisées sont refusées.

**Recommandation :** Documenter explicitement cette contrainte dans la Javadoc de l'interface `IMessageProducer<T>`, avec un lien vers le concept de propriétés applicatives.

---

### 3.7 ✅ `AzureStorageProvider` — `GetContainerName` lève une exception si non configuré *(corrigé)*

**Fichier :** `Messaging/Providers/Azure/AzureStorageProvider.cs`

**Problème initial :** Si `BlobStorageSetting.ContainerName` n'était pas configuré, le container `"default"` était utilisé silencieusement — aucun warning, aucune erreur, upload dans le mauvais container en production.

**Ce qui a été changé :**

```csharp
// AVANT — fallback silencieux dangereux
private string GetContainerName()
{
    return _config.BlobStorageSetting?.ContainerName ?? "default";
}

// APRÈS — fail-fast avec message explicite
private string GetContainerName()
{
    var containerName = _config.BlobStorageSetting?.ContainerName;
    if (string.IsNullOrWhiteSpace(containerName))
    {
        throw new InvalidOperationException(
            "BlobStorageSetting.ContainerName doit être configuré dans appsettings.json. " +
            "Aucune valeur par défaut n'est appliquée pour éviter d'écrire dans un conteneur non intentionnel.");
    }
    return containerName;
}
```

**Pourquoi pas de valeur par défaut ?**
Un container Blob est une ressource de production nommée. Utiliser `"default"` sans que ce soit une décision explicite revient à écrire dans un container potentiellement partagé avec d'autres données — risque de collision, de corruption, ou de fuite de données. Le développeur doit nommer explicitement son container dans la configuration.

**Configuration attendue dans `appsettings.json` :**
```json
{
  "BlobStorageSetting": {
    "BlobServiceUri": "https://moncompte.blob.core.windows.net",
    "ContainerName": "claim-check-prod",
    "FolderName": "messages",
    "ClaimCheckThresholdBytes": 262144
  }
}
```

---

### 3.8 🟡 `MessageTransitContext` — `IsClaimCheckApplied` est un champ public, pas une propriété

**Fichier :** `Messaging/MessageTransitContext.cs`

```csharp
// ACTUEL — champ public (non-encapsulé)
[JsonIgnore]
public bool IsClaimCheckApplied;
```

`IsClaimCheckApplied` est déclaré comme champ `public` au lieu d'une propriété `{ get; set; }`. En C#, les champs publics brisent l'encapsulation, ne sont pas sérialisables par certains sérialiseurs, ne supportent pas le data binding, et ne peuvent pas être overridés. Tous les autres membres de `MessageTransitContext` sont des propriétés.

**Recommandation :**
```csharp
[JsonIgnore]
public bool IsClaimCheckApplied { get; set; }
```

---

### 3.9 🟡 `MessagingOptions` est mutable via des setters publics

**Fichier :** `Messaging/MessagingOptions.cs`

```csharp
// ACTUEL — DTO interne mutable
public class MessagingOptions
{
    public Dictionary<string, object>? Properties { get; set; }
    public bool EnableSession { get; set; }
    // ...
}
```

`MessagingOptions` est un DTO interne créé dans `Producer` et passé à `IMessagingProvider`. Il n'a aucune raison d'être mutable : il est construit une seule fois et passé en lecture. Ses setters publics permettent à `AzureMessagingProvider` de modifier les options après coup, ce qui crée des comportements difficiles à tracer.

**Recommandation :** Convertir en `record` ou utiliser des propriétés `init`-only pour garantir l'immutabilité après construction.

---

### 3.10 🟡 `Producer.PublishBatchAsync` — résolution de target incorrecte et sessions ignorées

**Fichier :** `Messaging/Producer/Producer.cs`

Cette section documente deux comportements asymétriques entre `PublishAsync` (message unique) et `PublishBatchAsync`.

---

#### Sous-problème A — résolution du target absente dans le batch

**Contexte pour les développeurs juniors :** Le champ `CurrentStage` dans `MessageTransitContext` est une métadonnée de **traçabilité** — il représente l'étape courante dans l'itinéraire de traitement (où le message en est-il dans son voyage). Ce n'est **pas** un paramètre de routage. Le routing doit passer par `IMessageTargetMap.ResolveTarget<T>()`.

**Comparaison des deux chemins :**

```
// PublishAsync (CORRECT) — résolution explicite du target
string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();  // ← via DI map
EndpointSettings audience = _messagingProvider.Resolve(effectiveTarget);
string? resolvedTarget = audience.Target;
// CurrentStage est DÉFINI comme résultat du routage (output, pas input)
context.SetCurrentStage(resolvedTarget);
// Target transmis proprement dans les options
var options = new MessagingOptions { Target = resolvedTarget, ... };

// PublishBatchAsync (INCORRECT) — aucune résolution de target au niveau Producer
// Target n'est jamais résolu → MessagingOptions.Target reste null
var options = new MessagingOptions
{
    Properties = properties,
    EnableSession = false,  // ← hardcodé, voir Sous-problème B
    // Target = null  ← non défini !
};

// AzureMessagingProvider.SendBatchAsync reçoit Target=null et fait :
var resolved = Resolve(effectiveTarget ?? ctx.CurrentStage ?? _target);
//                                         ↑ utilise CurrentStage comme fallback de routing
//                                           = bug : CurrentStage n'est pas un paramètre de routage
```

**Recommandation :** Appliquer le même pattern de résolution dans `PublishBatchAsync` qu'en mode unitaire, **avant** la boucle sur les contextes :

```csharp
// RECOMMANDÉ dans PublishBatchAsync
// 1. Résoudre le target une seule fois pour tout le batch (même type TMessage)
string? effectiveTarget = _targetMap?.ResolveTarget<TMessage>();
EndpointSettings audience = _messagingProvider.Resolve(effectiveTarget);
string? resolvedTarget = audience.Target;
bool enableSession = audience.Endpoint.EnableSession;

// 2. Préparer chaque contexte (MessageId + CurrentStage + ClaimCheck)
foreach (var ctx in contextsList)
{
    if (string.IsNullOrWhiteSpace(ctx.MessageId))
        ctx.MessageId = Guid.NewGuid().ToString("N");

    if (string.IsNullOrWhiteSpace(ctx.CurrentStage))
        ctx.SetCurrentStage(resolvedTarget);   // CurrentStage = output de routage, pas input

    await PrepareClaimCheckAsync(ctx, ...);
}

// 3. Transmettre le target résolu dans les options
var options = new MessagingOptions
{
    Target = resolvedTarget,   // ← résolu proprement, pas null
    EnableSession = enableSession,
    ...
};
```

---

#### Sous-problème B — sessions ignorées dans le batch

**Contexte :** Azure Service Bus supporte les sessions sur les entités de type queue et topic. Lorsque `EnableSession = true` sur une entité, chaque message **doit** avoir un `SessionId` — sinon Service Bus rejette le message.

**Situation actuelle :** `PublishBatchAsync` hardcode `EnableSession = false`, ce qui :
- ignore la configuration de l'endpoint (`audience.Endpoint.EnableSession`)
- ne valide pas que les contextes ont un `SessionId` si requis
- peut envoyer des messages sans `SessionId` vers une entité session-activée → erreur Service Bus en production

**Vision sur la faisabilité des sessions avec un batch :**

Azure Service Bus autorise plusieurs messages avec des `SessionId` **différents** dans un même `ServiceBusMessageBatch`. Il n'y a pas de contrainte qui force tous les messages d'un batch à partager le même `SessionId`. Cependant, l'**ordre de traitement** est garanti *par session* — pas à l'échelle du batch entier.

Cela signifie que le batch peut contenir des messages pour différentes sessions, tant que chaque message a son propre `SessionId`. L'application cliente est responsable d'assigner des `SessionId` cohérents à chaque contexte avant d'appeler `PublishBatchAsync`.

**Recommandation :**

```csharp
// RECOMMANDÉ dans PublishBatchAsync, après résolution du target :
bool enableSession = audience.Endpoint.EnableSession;

foreach (var ctx in contextsList)
{
    // Même validation que PublishCoreAsync
    if (enableSession && string.IsNullOrWhiteSpace(ctx.SessionId))
    {
        throw new ArgumentException(
            $"SessionId obligatoire sur le contexte MessageId='{ctx.MessageId}' " +
            $"car l'endpoint '{resolvedTarget}' a EnableSession=true. " +
            "Chaque contexte du batch doit avoir son propre SessionId.");
    }
    // ...
}

var options = new MessagingOptions
{
    Target      = resolvedTarget,
    EnableSession = enableSession,  // ← lire depuis la config, ne pas hardcoder
    ...
};
```

**Résumé du comportement attendu après correction :**

| Cas | Comportement |
|---|---|
| `EnableSession = false` + pas de SessionId | ✅ OK, messages sans session |
| `EnableSession = true` + SessionId présent sur chaque ctx | ✅ OK, chaque message envoyé avec son SessionId |
| `EnableSession = true` + SessionId manquant sur au moins un ctx | ❌ `ArgumentException` immédiate (fail-fast) |
| Messages avec SessionId différents dans le même batch | ✅ Supporté par ASB — l'ordre est garanti par session |

---

### 3.11 🟢 Bonnes pratiques observées

- `ArgumentNullException.ThrowIfNull` utilisé systématiquement dans les constructeurs.
- `OperationCanceledException` est correctement re-propagé sans wrapping dans tous les try/catch.
- La sérialisation du payload est mise en cache dans `context.SerializedPayload` pour éviter la double-sérialisation entre ClaimCheck et l'envoi Service Bus.
- Le `CancellationToken` est propagé à tous les appels I/O asynchrones.
- La clé composite `namespace|entity` dans `ServiceBusSenderCache` est correcte pour le multi-namespace.

---

## 4. Code Pattern Review

### 4.1 🟠 Pattern `IProducerPatterns` — couplage infrastructure dans une interface de pattern

**Fichier :** `Messaging/Producer/IProducerPatterns.cs`

```csharp
// ACTUEL — IMessagingProvider dans la signature d'une interface de pattern
Task<MessageTransitContext<MessageTransitResponse>?> ExecuteRequestReplyAsync<TMessage>(
    IMessagingProvider messagingProvider,    // ← fuite d'abstraction
    MessageTransitContext<TMessage> context,
    MessagingOptions options,
    CancellationToken cancellationToken) where TMessage : class;
```

Un pattern de messagerie de haut niveau ne devrait pas avoir `IMessagingProvider` dans sa signature. Cela force toute classe qui implémente l'interface à connaître et injecter une dépendance infrastructure. Le `Producer` qui implémente cette interface a déjà `_messagingProvider` en champ privé — le paramètre est donc redondant et ne sert qu'à polluer le contrat.

**Recommandation :** Retirer `IMessagingProvider` du paramètre. Le `Producer` utilisera son champ injecté.

---

### 4.2 🟠 Pattern `PublishOptions` / `RequestReplyOptions` — héritage de records mal utilisé

**Fichier :** `Messaging/Producer/PublishOptions.cs`

```csharp
// ACTUEL — record qui hérite d'un autre record
public record PublishOptions { ... }
public record RequestReplyOptions : PublishOptions
{
    public bool EnableOffline { get; init; }
}
```

L'héritage entre `record` en C# est techniquement valide, mais brise l'égalité structurelle : deux instances de `RequestReplyOptions` avec les mêmes valeurs ne seront pas égales à deux `PublishOptions` équivalentes. Plus important, `ClaimCheck` est défini dans `PublishOptions` mais `PublishOptions` est documenté comme "options sans target fixe". La relation d'héritage implique que `RequestReplyOptions` **est un** `PublishOptions`, ce qui est sémantiquement discutable.

**Recommandation :** Préférer la composition. Définir un record `RequestReplyOptions` indépendant contenant un `PublishOptions` ou les mêmes propriétés (faible overhead). Cela découple les deux contrats.

---

### 4.3 🟠 Pattern Claim Check — `ClaimCheckOptions` est `sealed class` vs les autres qui sont `record`

**Fichiers :** `Messaging/Producer/ClaimCheckOptions.cs`, `PublishOptions.cs`

`PublishOptions` et `RequestReplyOptions` sont des `record`, mais `ClaimCheckOptions` est une `sealed class`. Cette incohérence perturbe les développeurs sur le comportement d'égalité, de copie (`with`), et de sérialisation. De plus, `ClaimCheckOptions.None` est une factory property `=> new()` qui crée une nouvelle instance à chaque appel, alors que `PublishOptions.Default` fait pareil. Si ces instances sont comparées par référence quelque part, cela peut causer des bugs.

**Recommandation :** Unifier le style. `ClaimCheckOptions` gagnerait à être un `record` pour bénéficier de l'égalité structurelle et du `with`. Ou sinon, adopter un pattern `static readonly` pour `None` et `Default` afin d'éviter les allocations répétées.

```csharp
// RECOMMANDÉ pour éviter les allocations répétées
public static readonly ClaimCheckOptions None = new();
```

---

### 4.4 🟠 Pattern `MapToResponseContext` — mapping manuel sans abstraction

**Fichier :** `Messaging/Producer/Producer.cs`

```csharp
// ACTUEL — mapping manuel répété (2 usages dans Producer)
protected MessageTransitContext<MessageTransitResponse> MapToResponseContext<TAnyMessage>(
    MessageTransitContext<TAnyMessage> source,
    MessageTransitResponse? response) where TAnyMessage : class
{
    return new MessageTransitContext<MessageTransitResponse>()
    {
        MessageId = source.MessageId,
        SessionId = source.SessionId,
        CurrentStage = source.CurrentStage,
        SequenceNumber = source.SequenceNumber,
        Attempt = source.Attempt,
        Tokens = source.Tokens,
        Variables = source.Variables,
        TransportMessage = source.TransportMessage,
        Message = response
    };
}
```

Ce mapping copie manuellement chaque propriété. Si une nouvelle propriété est ajoutée à `MessageTransitContext`, ce mapping sera silencieusement incorrect (propriété oubliée). Ce pattern "property copy" est fragile à la maintenance.

**Recommandation :** Documenter explicitement que tout ajout de propriété à `MessageTransitContext` doit être répercuté ici. Ou mieux, implémenter une méthode de copie sur `MessageTransitContext` lui-même : `context.CopyWithResponse(response)`.

---

### 4.5 🟡 Pattern `ExtractMessageProperties` — couplage aux noms de clés stringly-typed

**Fichier :** `Messaging/Producer/Producer.cs`

**Explication pour les développeurs juniors :** Un "magic string" (chaîne magique) est une valeur string écrite en dur dans le code, sans constante nommée. Si la même chaîne est utilisée à plusieurs endroits et qu'on la modifie par erreur (faute de frappe, casse différente), le compilateur ne détecte rien et le bug est silencieux.

Dans `Producer.cs`, les clés `"Consumer"` et `"Action"` apparaissent en dur comme magic strings dans deux méthodes :

```csharp
// Dans ValidateRoutingProperties — "Consumer" et "Action" en littéraux
var invalidKeys = properties.Keys
    .Where(k => !string.Equals(k, "Consumer", StringComparison.OrdinalIgnoreCase)
             && !string.Equals(k, "Action", StringComparison.OrdinalIgnoreCase))
    .ToList();

// Dans ExtractMessageProperties — idem
if (properties.TryGetValue("Consumer", out var consumerObj) ...) { ... }
if (properties.TryGetValue("Action", out var actionObj) ...) { ... }
```

Ces mêmes clés sont ensuite utilisées dans `AzureMessagingProvider` (`SendAsync`, `SendBatchAsync`, `ApplyMetadataToOutgoing`). Une incohérence de casse entre Producer et Provider crée un bug silencieux où les propriétés ne sont jamais transmises.

> **Vérification source :** La clé `"Target"` n'est **pas** dans la liste des propriétés autorisées au niveau du Producer (`ValidateRoutingProperties` ne l'accepte pas et `ExtractMessageProperties` ne l'extrait pas). Elle apparaît seulement au niveau du provider Azure (`AzureMessagingProvider.SendAsync`) pour un usage interne au transport — c'est un contexte différent.

**Important :** La classe `AzureMessagingProperties` existe déjà dans `Messaging/Providers/Azure/AzureMessagingProperties.cs` et définit ces constantes :

```csharp
// Existant — mais dans le namespace Azure (couche provider)
public static class AzureMessagingProperties
{
    public const string Consumer = "Consumer";
    public const string Action   = "Action";
    // ...
}
```

Cependant, utiliser `AzureMessagingProperties` depuis `Producer.cs` créerait une dépendance de la couche `Messaging.Producer` vers la couche `Messaging.Providers.Azure` — c'est une **violation de couche** (le Producer ne doit pas connaître les détails Azure).

**Recommandation :** Créer une classe de constantes partagée au niveau `Messaging` (couche neutre), et référencer ces constantes depuis les deux couches :

```csharp
// NOUVEAU — Messaging/MessagePropertyKeys.cs (couche neutre, internal)
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    internal static class MessagePropertyKeys
    {
        public const string Consumer = "Consumer";
        public const string Action   = "Action";
    }
}

// Usage dans Producer.cs
.Where(k => !string.Equals(k, MessagePropertyKeys.Consumer, StringComparison.OrdinalIgnoreCase)
         && !string.Equals(k, MessagePropertyKeys.Action,   StringComparison.OrdinalIgnoreCase))

// AzureMessagingProperties peut ensuite référencer les mêmes constantes
public static class AzureMessagingProperties
{
    public const string Consumer = MessagePropertyKeys.Consumer;
    public const string Action   = MessagePropertyKeys.Action;
    // ... autres constantes spécifiques Azure
}
```

---

### 4.6 🟡 Pattern Factory sur `ClaimCheckOptions` — `WithAttachment` non cohérent

**Fichier :** `Messaging/Producer/ClaimCheckOptions.cs`

```csharp
// ACTUEL
public static ClaimCheckOptions WithAttachment(
    Stream fileStream,
    string originalFileName,
    bool forceClaimCheck = false) => new() { ... };
```

`WithAttachment` est la seule factory method de `ClaimCheckOptions`. Mais il n'existe pas de factory pour le cas où l'on veut forcer le Claim Check sans pièce jointe. L'application cliente est obligée de construire l'objet manuellement avec l'initializer `new ClaimCheckOptions { ForceClaimCheck = true }`, contournant ainsi la factory API.

**Recommandation :** Ajouter :
```csharp
public static ClaimCheckOptions Force() => new() { ForceClaimCheck = true };
```

---

### 4.7 ✅ `PublishBatchAsync` — retourne `IReadOnlyList<string>`, supporte les sessions, atomicité documentée *(corrigé)*

**Fichier :** `Messaging/Producer/IMessageProducer.cs`, `Producer.cs`

#### A — Retour des MessageId à l'application cliente

**Problème initial :** `PublishBatchAsync` retournait `Task` (void). L'application cliente n'avait aucun moyen de savoir quels messages ont été envoyés, ni de relier chaque message à sa réponse Service Bus.

**Ce qui a été changé :**

```csharp
// AVANT
Task PublishBatchAsync(IEnumerable<MessageTransitContext<TPayload>> contexts, ...);

// APRÈS
Task<IReadOnlyList<string>> PublishBatchAsync(IEnumerable<MessageTransitContext<TPayload>> contexts, ...);
```

La liste retournée est **dans le même ordre que la collection d'entrée**. Si le 2ème contexte fourni par l'application cliente devient le 2ème `MessageId` dans la réponse, la corrélation est triviale.

#### B — Sessions dans un batch : le développeur fournit le `SessionId`

**Règle fondamentale :** Lorsque l'entité Service Bus a `EnableSession = true`, **chaque `MessageTransitContext` du batch doit avoir son `SessionId` renseigné** par l'application cliente. EMT ne génère pas de SessionId automatiquement pour le batch — c'est une décision métier qui appartient au développeur.

**Pourquoi ?** Le `SessionId` représente une corrélation métier (ex: un numéro de commande, un identifiant de dossier). EMT ne peut pas inventer cette valeur.

**Validation :** Si un contexte n'a pas de `SessionId` et que l'entité est session-activée, une `ArgumentException` est levée immédiatement (fail-fast) **avant tout envoi**.

#### C — Ordre FIFO et garanties de traitement avec sessions

| Garantie | Condition |
|---|---|
| FIFO strict entre messages du même `SessionId` | ✅ Garanti par Service Bus, dans l'ordre d'ajout au batch |
| FIFO entre messages de `SessionId` différents | ❌ Aucune garantie inter-sessions |
| Messages avec `SessionId` différents dans le même batch | ✅ Autorisé par Service Bus |
| Traitement par un seul consumer à la fois par session | ✅ Garanti par le verrouillage de session ASB |

#### D — Atomicité du batch : **non atomique par design**

> ⚠️ **Point important à comprendre pour les développeurs juniors.**

L'envoi batch dans Service Bus via `SendMessagesAsync(ServiceBusMessageBatch)` est atomique **par appel** — soit tous les messages du batch sont acceptés par le broker, soit aucun. **Mais** si le batch est trop grand, EMT le découpe en plusieurs batches de 256 Ko maximum, et **chaque batch est envoyé indépendamment**.

Conséquences :
- Si le 1er batch réussit et le 2ème échoue → les messages du 1er batch sont déjà publiés
- Il n'y a pas de transaction distribuée qui annule le 1er batch
- L'application cliente reçoit une `MessageSendException` et sait, grâce aux `MessageId` retournés en cas de succès partiel, quels messages ont été envoyés

**Ce que l'application cliente doit faire en cas d'erreur :**
```csharp
IReadOnlyList<string>? publishedIds = null;
try
{
    publishedIds = await _producer.PublishBatchAsync(contexts);
    // Tous les messages ont été publiés
}
catch (MessageSendException ex)
{
    // Certains messages peuvent avoir été publiés avant l'erreur.
    // Logguer et alerter — une intervention manuelle ou un mécanisme de reprise est requis.
    _logger.LogError(ex, "Échec batch — vérifier les messages en attente");
    throw;
}
```

#### E — Exemple complet : envoi batch avec sessions (API ASP.NET Core / ARO)

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// Scénario : publication d'un lot d'étapes de traitement pour plusieurs dossiers
// L'entité Service Bus est session-activée (EnableSession = true dans appsettings)
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/dossiers")]
public class DossierController : ControllerBase
{
    private readonly IMessageProducer<EvenementDossier> _producer;

    public DossierController(IMessageProducer<EvenementDossier> producer)
    {
        _producer = producer;
    }

    [HttpPost("traitement-batch")]
    public async Task<IActionResult> TraiterBatch(
        [FromBody] List<DossierTraitementDto> dossiers,
        CancellationToken cancellationToken)
    {
        // ── 1. Construction des contextes ─────────────────────────────────────
        // Chaque dossier possède son propre SessionId (= identifiant de corrélation métier).
        // Tous les messages du même dossier partagent ce SessionId → FIFO garanti par dossier.
        var contexts = dossiers.Select(d => new MessageTransitContext<EvenementDossier>
        {
            Message   = new EvenementDossier { DossierId = d.Id, Etape = d.Etape },
            MessageId = Guid.NewGuid().ToString("N"),   // généré par l'appelant ou par EMT si null
            SessionId = d.DossierId                     // ← SessionId = identifiant métier du dossier
        }).ToList();

        // ── 2. Publication du batch ───────────────────────────────────────────
        // La méthode valide que chaque contexte a un SessionId si l'entité est session-activée.
        // Elle retourne les MessageId dans le même ordre que `contexts`.
        IReadOnlyList<string> messageIds = await _producer.PublishBatchAsync(
            contexts,
            properties: new Dictionary<string, object>
            {
                { "Consumer", "TraitementDossier" },
                { "Action",   "DemarrerEtape" }
            },
            cancellationToken: cancellationToken);

        // ── 3. Corrélation : messageIds[i] correspond à contexts[i] ──────────
        // L'application cliente peut stocker ces IDs pour la traçabilité ou le suivi.
        var summary = contexts.Zip(messageIds, (ctx, id) => new
        {
            DossierId = ctx.SessionId,
            MessageId = id
        });

        return Ok(new
        {
            Publies    = messageIds.Count,
            MessageIds = messageIds,
            Detail     = summary
        });
    }
}
```

**Enregistrement DI (Program.cs) :**
```csharp
// Entité session-activée dans appsettings.json :
// "EnableSession": true  sur le TransportSettings de cette entité
services.AddProducer<EvenementDossier>("traitement-dossier");
services.ConfigureAzureProviders();
```

---

### 4.8 🟡 Pattern `GetResponseAsync` — délégation en cascade incohérente

**Fichier :** `Messaging/Producer/Producer.cs`

```
GetResponseAsync(context, replyOptions)
  → GetResponseCoreAsync(...)
    → ExecuteRequestReplyAsync(_messagingProvider, context, options, ct)
      → _messagingProvider.RequestReplyAsync(context, options, ct)
```

La chaîne d'appel pour Request/Reply comporte 3 niveaux d'indirection dont un (`ExecuteRequestReplyAsync`) défini par `IProducerPatterns`. Pour `PublishAsync`, la chaîne est :

```
PublishAsync → PublishCoreAsync → _messagingProvider.SendAsync
```

Seulement 2 niveaux. Cette asymétrie entre les deux chemins est déroutante et ne se justifie pas architecturalement.

**Recommandation :** Aligner les deux chemins sur le même nombre de niveaux, ou supprimer le niveau intermédiaire `ExecuteRequestReplyAsync` en appelant directement `_messagingProvider.RequestReplyAsync` dans `GetResponseCoreAsync`.

---

### 4.9 🟢 Patterns bien implémentés

- **Strategy pattern** via `IMessagingProvider` / `IStorageProvider` / `IJournalProvider` : parfaitement appliqué, rend les providers facilement testables avec des mocks.
- **Null Object pattern** via `ClaimCheckOptions.None` : élégant, évite les null-checks dans `PublishCoreAsync`.
- **Options pattern** (.NET standard) via `MessageTargetMapOptions` + `IOptions<T>` : idiomatique et correct.
- **Cache pattern** via `ServiceBusSenderCache` : conception solide pour un singleton multi-entity.
- **Guard clause pattern** : `ArgumentNullException.ThrowIfNull` en tête de constructeurs — lisible et maintenable.

---

---

## 5. Vérification Clean Code et modularité

> Cette section analyse la modularité du code — la capacité à modifier, tester et étendre chaque composant de façon indépendante — et applique les principes SOLID, DRY, et KISS tels qu'observés dans le code source réel.

---

### 5.1 Séparation des responsabilités — état actuel

#### ✅ Ce qui est bien découpé

| Composant | Responsabilité | Couplage |
|---|---|---|
| `Producer<T>` | Orchestration : résolution target, validation, claim-check, journal | Couplé à `IMessagingProvider` (normal) |
| `AzureMessagingProvider` | Transport Service Bus uniquement | ✅ Pas de logique métier |
| `AzureStorageProvider` | Blob Storage uniquement | ✅ Pas de logique de routage |
| `EndpointResolver` | Résolution d'endpoint uniquement | ✅ Pas d'envoi |
| `ServiceBusSenderCache` | Cache des senders uniquement | ✅ Aucune logique de message |
| `AzureJournalProvider` | Journalisation Table Storage uniquement | ✅ Séparé du transport |

#### 🟠 Couplage résiduel à corriger

**`AzureMessagingProvider` dépend de `Microsoft.Azure.Functions.Worker`** (voir §2.3).

`BindContext` dans `AzureMessagingProvider` caste directement vers `ServiceBusMessageActions` (type Functions Worker). Un producer ASP.NET Core / ARO n'utilise jamais `BindContext` — mais la dépendance package transite quand même via le `using` en tête de fichier.

**Impact :** tout projet qui référence EMT comme producer doit embarquer `Microsoft.Azure.Functions.Worker` dans ses dépendances transitives — overhead injustifié pour une API ou un Worker Service.

**Isolation recommandée :** déplacer tous les `BindContext` et les casts `ServiceBusMessageActions` dans `AzureFunctionMessagingAdapter`, et supprimer le `using Microsoft.Azure.Functions.Worker;` dans `AzureMessagingProvider.cs`.

---

### 5.2 Principe DRY (Don't Repeat Yourself)

#### 🟠 Double appel `ExtractMessageProperties` dans `PublishBatchAsync`

**Fichier :** `Producer.cs` — boucle de journalisation

```csharp
// ACTUEL — extraction appelée 2 fois par message dans la boucle
foreach (var ctx in contextsList)
{
    var journalEntry = new JournalEntry(
        ExtractMessageProperties(properties).Consumer ?? "(none)",  // ← appel 1
        ExtractMessageProperties(properties).Action ?? "(none)",    // ← appel 2
        ...
    );
}
```

`properties` est constant sur l'ensemble du batch. L'extraction doit être faite une seule fois avant la boucle.

```csharp
// RECOMMANDÉ
(string? consumer, string? action) = ExtractMessageProperties(properties);
foreach (var ctx in contextsList)
{
    var journalEntry = new JournalEntry(consumer ?? "(none)", action ?? "(none)", ...);
}
```

#### 🟠 Duplication de la logique de résolution entre `PublishCoreAsync` et `PublishBatchAsync`

Les deux méthodes font :
1. `_targetMap?.ResolveTarget<TMessage>()`
2. `_messagingProvider.Resolve(effectiveTarget)`
3. `audience.Endpoint.EnableSession`
4. `foreach ctx → SetCurrentStage(resolvedTarget)`

Cette séquence de 4 étapes est dupliquée. Une méthode privée `ResolveAudience()` centraliserait cette logique et garantirait que les deux chemins sont toujours cohérents.

---

### 5.3 Principe KISS (Keep It Simple, Stupid)

#### 🟠 `SendBatchAsync` — logique de découpage des batches trop complexe

**Fichier :** `AzureMessagingProvider.cs`

La logique actuelle de découpage en batches de 256 Ko utilise un `using var messageBatch` avec des branches imbriquées pour gérer le débordement. Le `nextBatch` est créé mais jamais envoyé dans la branche `else` (bug de perte silencieuse, voir §3.2).

La complexité cyclomatique de cette méthode est trop haute pour sa responsabilité. La refactorisation recommandée est d'extraire une méthode privée `FlushBatchAsync(sender, batch, entityName, ct)` qui s'occupe uniquement de l'envoi avec retry, et de séparer clairement la phase de construction des batches de la phase d'envoi.

#### 🟡 `GetResponseCoreAsync` — 4 niveaux d'appel pour RequestReply

```
GetResponseAsync → GetResponseCoreAsync → ExecuteRequestReplyAsync → RequestReplyAsync
```

3 niveaux d'indirection pour appeler `_messagingProvider.RequestReplyAsync`. Vs 2 niveaux pour Publish. Simplifier en appelant `_messagingProvider.RequestReplyAsync` directement dans `GetResponseCoreAsync`.

---

### 5.4 Principe de substitution de Liskov (LSP) — records et héritage

**Fichier :** `PublishOptions.cs`

```csharp
public record PublishOptions { ... }
public record RequestReplyOptions : PublishOptions { ... }
```

`RequestReplyOptions` hérite de `PublishOptions`. Une méthode qui accepte `PublishOptions` peut recevoir un `RequestReplyOptions` — mais l'égalité structurelle des records est cassée dans ce cas (deux `RequestReplyOptions` identiques ne seront pas égaux à un `PublishOptions` équivalent). LSP est techniquement respecté, mais l'égalité cassée est une source de bugs dans les tests unitaires.

---

### 5.5 Immutabilité — `MessagingOptions` mutable

**Fichier :** `Messaging/MessagingOptions.cs`

`MessagingOptions` est un DTO interne construit par `Producer` et passé à `IMessagingProvider`. Il a des setters `{ get; set; }` sur toutes ses propriétés — ce qui signifie que `AzureMessagingProvider` peut le modifier après réception. En pratique il ne le fait pas, mais rien ne l'en empêche.

**Recommandation :** Convertir en `record` avec propriétés `init` :

```csharp
// RECOMMANDÉ
public record MessagingOptions
{
    public Dictionary<string, object>? Properties { get; init; }
    public bool EnableSession { get; init; }
    public string? Target { get; init; }
    public bool EnableOffline { get; init; }
    public Stream? FileStream { get; init; }
    public string? OriginalFileName { get; init; }
    public bool ForceClaimCheck { get; init; }
}
```

---

### 5.6 Encapsulation — `IsClaimCheckApplied` champ public

**Fichier :** `Messaging/MessageTransitContext.cs`

```csharp
// ACTUEL — champ public, pas une propriété
public bool IsClaimCheckApplied;

// RECOMMANDÉ
public bool IsClaimCheckApplied { get; set; }
```

Tous les autres membres de `MessageTransitContext` sont des propriétés. Ce champ casse l'homogénéité et peut poser des problèmes avec les proxies de sérialisation.

---

### 5.7 Magic strings — clés `"Consumer"` et `"Action"`

**Fichiers :** `Producer.cs`, `AzureMessagingProvider.cs`, `AzureMessagingProperties.cs`

Les clés `"Consumer"` et `"Action"` apparaissent comme littéraux dans `ValidateRoutingProperties`, `ExtractMessageProperties`, `SendAsync`, `SendBatchAsync`, et `ApplyMetadataToOutgoing`. `AzureMessagingProperties` existe mais n'est pas utilisée par le `Producer`.

**Impact :** une faute de casse dans un fichier casse silencieusement la transmission des propriétés — aucune erreur de compilation, bug observable seulement en test d'intégration.

---

### 5.8 Synthèse modularité — score par composant

| Composant | SRP | DRY | KISS | Immuabilité | Couplage | Score |
|---|---|---|---|---|---|---|
| `Producer<T>` | ✅ | 🟠 double extract | ✅ | ✅ | ✅ | **B+** |
| `AzureMessagingProvider` | 🟠 porte Functions.Worker | ✅ | 🟠 batch complexe | 🟠 état mutable | 🟠 Functions.Worker | **C+** |
| `AzureStorageProvider` | ✅ | ✅ | ✅ | ✅ | ✅ | **A** |
| `ServiceBusSenderCache` | ✅ | ✅ | ✅ | — | ✅ | **A** |
| `EndpointResolver` | ✅ | ✅ | ✅ | — | ✅ | **A** |
| `MessagingOptions` | ✅ | ✅ | ✅ | 🟠 mutable | ✅ | **B** |
| `PublishOptions / RequestReplyOptions` | ✅ | ✅ | ✅ | ✅ | ✅ | **A-** |
| `AzureJournalProvider` | ✅ | ✅ | ✅ | — | ✅ | **A** |



### Tableau de bord

| # | Catégorie | Problème | Priorité | Effort | Statut |
|---|-----------|----------|----------|--------|--------|
| A1 | Architecture | `IProducerPatterns` exposée publiquement avec fuite d'abstraction (`IMessagingProvider` en paramètre) | 🔴 Bloquant | Faible | ✅ Corrigé |
| A2 | Architecture | État mutable dans `AzureMessagingProvider` — risque de race condition | 🟠 Majeur | Moyen | ✅ Corrigé |
| A3 | Architecture | Dépendance sur `Microsoft.Azure.Functions.Worker` dans le provider générique | 🟠 Majeur | Faible | ✅ Corrigé |
| A4 | Architecture | Absence de stratégie de retry configurable pour le Producer (`SendRetry`) | 🟡 Mineur | Moyen | ✅ Corrigé |
| A5 | Architecture | Journal dans le chemin critique d'envoi | 🟡 Mineur | Moyen | ✅ Corrigé |
| C1 | Code | `PublishBatchAsync` — double appel `ExtractMessageProperties` à chaque itération | 🟠 Majeur | Trivial | ✅ Corrigé |
| C2 | Code | `SendBatchAsync` — perte silencieuse de messages (bug logique nextBatch) | 🟠 Majeur | Moyen | ✅ Corrigé |
| C3 | Code | `ReplaceSenderAsync` — fire-and-forget non contrôlé, méthode async factice | 🟠 Majeur | Moyen | ✅ Corrigé |
| C4 | Code | Timeout Request/Reply — `AzureServiceBusProviderOptions.ReplyTimeout` (défaut 5 min) | 🟠 Majeur | Faible | ✅ Corrigé |
| C5 | Code | `NullReferenceException` levée manuellement (anti-pattern CLR) | 🟠 Majeur | Trivial | ✅ Corrigé |
| C6 | Code | `GetContainerName` retourne `"default"` silencieusement si non configuré | 🟡 Mineur | Trivial | ✅ Corrigé |
| C7 | Code | `IsClaimCheckApplied` est un champ public au lieu d'une propriété | 🟡 Mineur | Trivial | ✅ Corrigé |
| C8 | Code | `MessagingOptions` mutable (setters publics sur un DTO interne) | 🟡 Mineur | Faible | ✅ Corrigé |
| C9 | Code | `PublishBatchAsync` — target non résolu via `IMessageTargetMap`, sessions ignorées | 🟡 Mineur | Moyen | ✅ Corrigé |
| C10 | Code | `PublishBatchAsync` — ne retourne pas les MessageId, contrat incomplet | 🟡 Mineur | Faible | ✅ Corrigé |
| P1 | Pattern | `IProducerPatterns` — `IMessagingProvider` en paramètre (couche provider dans un contrat de pattern) | 🟠 Majeur | Faible | ✅ Corrigé |
| P2 | Pattern | Héritage `record RequestReplyOptions : PublishOptions` — égalité structurelle cassée | 🟠 Majeur | Faible | ✅ Corrigé |
| P3 | Pattern | `ClaimCheckOptions` en `sealed class` vs `record` — incohérence de style avec `PublishOptions` | 🟡 Mineur | Faible | ✅ Corrigé |
| P4 | Pattern | `MapToResponseContext` — mapping manuel fragile aux évolutions de `MessageTransitContext` | 🟡 Mineur | Faible | ✅ Corrigé |
| P5 | Pattern | Magic strings `"Consumer"` / `"Action"` — `AzureMessagingProperties` existe mais non utilisée par Producer | 🟡 Mineur | Trivial | ✅ Corrigé |
| P6 | Pattern | `ClaimCheckOptions` — absence de factory `Force()` | 🟡 Mineur | Trivial | ✅ Corrigé |
| P7 | Pattern | Chaîne d'appel asymétrique : 2 niveaux pour Publish, 3 niveaux pour RequestReply | 🟡 Mineur | Moyen | ✅ Corrigé |

### Éléments corrigés — Sprint 1

| Item | Description | Fichier(s) modifié(s) |
|---|---|---|
| C4 | `RequestReplyTimeout` lu depuis `AzureServiceBusProviderOptions` (défaut : 5 min, injectable en DI) | `AzureMessagingProvider.cs`, `ConfigurerProviders.cs` |
| C6 | `GetContainerName()` lève `InvalidOperationException` si `ContainerName` non configuré | `AzureStorageProvider.cs` |
| C9 | `PublishBatchAsync` résout le target via `IMessageTargetMap`, valide `SessionId` si `EnableSession` | `Producer.cs` |
| C10 | `PublishBatchAsync` retourne `IReadOnlyList<string>` (MessageId dans l'ordre d'entrée) | `IMessageProducer.cs`, `Producer.cs` |

### Éléments corrigés — Sprint 2 (Bugs et qualité)

| Item | Description | Fichier(s) modifié(s) |
|---|---|---|
| C1 | `ExtractMessageProperties` extrait une seule fois avant la boucle dans `PublishBatchAsync` | `Producer.cs` |
| C5 | `NullReferenceException` remplacée par `ArgumentNullException.ThrowIfNull` dans `RequestReplyAsync` | `AzureMessagingProvider.cs` |
| A1 / P1 | `IProducerPatterns` rendue `internal`, `ExecuteRequestReplyAsync(IMessagingProvider)` supprimée de l'interface | `IProducerPatterns.cs`, `Producer.cs` |
| P5 | Classe `MessagePropertyKeys` créée (`Messaging/MessagePropertyKeys.cs`), utilisée dans `Producer.cs` (fin des magic strings) | `MessagePropertyKeys.cs`, `Producer.cs` |

### Éléments corrigés — Sprint 3 (Design et robustesse)

| Item | Description | Fichier(s) modifié(s) |
|---|---|---|
| A2 | `_target`, `_consumer`, `_action` supprimés de `AzureMessagingProvider` — plus d'état mutable partagé | `AzureMessagingProvider.cs` |
| C2 | `SendBatchAsync` réécrit avec `Queue<ServiceBusMessage>` — aucune perte silencieuse de messages | `AzureMessagingProvider.cs` |
| C3 | `ReplaceSenderAsync` renommée `ReplaceSender` (synchrone) — fin du fire-and-forget non contrôlé | `ServiceBusSenderCache.cs`, `AzureMessagingProvider.cs` |
| C7 | `IsClaimCheckApplied` converti de champ public en propriété `{ get; set; }` | `MessageTransitContext.cs` |
| C8 | `MessagingOptions` converti de `class` en `record` avec propriétés `init` | `MessagingOptions.cs` |
| A4 | `ProducerSendRetryPolicy` créée ; `TransportSettings.SendRetry` ajouté ; retry configurable dans `AzureMessagingProvider` | `ProducerSendRetryPolicy.cs`, `TransportSettings.cs`, `AzureMessagingProvider.cs` |
| P2 | `RequestReplyOptions` ne hérite plus de `PublishOptions` (composition, égalité structurelle préservée) | `PublishOptions.cs` |
| P3 | `ClaimCheckOptions` converti en `record`, `None` en `static readonly`, factory `Force()` ajoutée | `ClaimCheckOptions.cs` |

### Éléments corrigés — Sprint 4 (Architecture long terme)

| Item | Description | Fichier(s) modifié(s) |
|---|---|---|
| A3 | `using Microsoft.Azure.Functions.Worker` supprimé de `AzureMessagingProvider` ; `BindContext` délègue à l'adapter | `AzureMessagingProvider.cs` |
| P4 | `MessageTransitContext.CopyWithResponse<T>()` ajouté ; `MapToResponseContext` délègue à cette méthode | `MessageTransitContext.cs`, `Producer.cs` |
| P6 | Factory `ClaimCheckOptions.Force()` ajoutée | `ClaimCheckOptions.cs` |
| P7 | `GetResponseCoreAsync` appelle directement `_messagingProvider.RequestReplyAsync` (suppression du niveau intermédiaire) | `Producer.cs` |
| A5 | Appels au journal enveloppés dans `try/catch` avec `LogWarning` — journal hors du chemin critique | `Producer.cs` |

---

*Review réalisée par analyse statique du code source.*  
*Mise à jour : Avril 2026 — toutes les recommandations (A1–A5, C1–C10, P1–P7) appliquées. Build : ✅ 0 erreur, 0 warning.*
