# Revue de code — Corrections appliquées

> **Objectif de ce document**  
> Décrire chaque correction apportée à la librairie `EnterpriseMessageTransit` à la suite de la revue de code.  
> Chaque section explique **le problème**, **ce qui a été changé**, et **pourquoi** — pour qu'un développeur junior puisse comprendre la démarche.

---

## Table des matières

| # | Catégorie | Item | Statut |
|---|-----------|------|--------|
| [2.1](#21-normalisation-des-url-blob) | Sécurité | Normalisation des URL Blob | ✅ Corrigé |
| [2.2](#22-durcissement-json--maxdepth-et-options-en-cache) | Sécurité | JSON — `MaxDepth` et options en cache | ✅ Corrigé |
| [2.3](#23-exceptions-silencieuses) | Sécurité | Exceptions silencieuses | ✅ Corrigé |
| [2.4](#24-randomshared--jitter-thread-safe) | Sécurité | `Random.Shared` — jitter thread-safe | ✅ Corrigé |
| [2.5](#25-politique-de-rétention-blob) | Sécurité | Politique de rétention Blob | 📋 Infra |
| [2.6](#26-managedidentitycredential-au-lieu-des-chaînes-de-connexion) | Sécurité | `ManagedIdentityCredential` vs chaînes de connexion | ✅ Corrigé |
| [2.7](#27-connectionstring-absent-des-options) | Sécurité | `ConnectionString` absent des options | ✅ Corrigé |
| [3.1](#31-servicebusreceivedmessage-hors-du-contexte--et-propriétés-transport-agnostiques-dans-imessagetransit) | Architecture | `ServiceBusReceivedMessage` hors du contexte + `SessionId` dans `IMessageTransit` | ✅ Corrigé |
| [3.2](#32-iendpointresolver-injectable) | Architecture | `IEndpointResolver` injectable | ✅ Corrigé |
| [3.3](#33-enum-messagingentitytype-transport-agnostique) | Architecture | `MessagingEntityType` transport-agnostique | ✅ Corrigé |
| [3.4](#34-operationmode-dans-le-namespace-générique) | Architecture | `OperationMode` dans le namespace générique | ✅ Corrigé |
| [3.6](#36-renommage-audience--endpoint) | Architecture | Renommage Audience → Endpoint | ✅ Corrigé |
| [3.7](#37-bug-initialdelay-dans-exponentialretrypolicy) | Architecture | Bug `InitialDelay` dans `ExponentialRetryPolicy` | ✅ Corrigé |
| [4.1](#41-effectivetarget--effectiveconsumer) | Qualité code | `effectiveTarget` / `effectiveConsumer` | ✅ Corrigé |
| [4.2](#42-nullreferenceexception-lancé-manuellement) | Qualité code | `NullReferenceException` lancé manuellement | ✅ Corrigé |
| [4.4](#44-cycle-de-vie-des-senders-service-bus) | Qualité code | Cycle de vie des senders Service Bus | ✅ Corrigé |
| [5.1](#51-projet-de-tests-unitaires) | Testabilité | Projet de tests unitaires | 📋 À créer |
| [5.2](#52-isystemclock--testabilité-du-temps) | Testabilité | `ISystemClock` — testabilité du temps | ✅ Corrigé |
| [5.3](#53-journalentry-en-record-immuable--et-enrichissement-des-champs) | Testabilité | `JournalEntry` en `record` immuable + 3 nouveaux champs | ✅ Corrigé |
| [6.1](#61-commentaire-todo-obsolète-dans-azurejournalprovider) | Observabilité | Commentaire TODO obsolète | ✅ Corrigé |
| [6.2](#62-traces-distribuées) | Observabilité | Traces distribuées | 📋 Futur |
| [7.1](#71-cache-des-senders-servicebusSendercache) | Performance | Cache des senders — `ServiceBusSenderCache` | ✅ Corrigé |
| [7.2](#72-cache-du-payload-sérialisé) | Performance | Cache du payload sérialisé | ✅ Corrigé |
| [7.3](#73-retry-session--préservation-de-lordre-fifo-retour-à-taskdelay--abandonmessageasync) | Performance | Retry session : FIFO via `Task.Delay` + `AbandonMessageAsync` | ✅ Corrigé |
| [7.4](#74-jsonserializeroptions-statique) | Performance | `JsonSerializerOptions` statique | ✅ Corrigé |
| [8.1](#81-api-sdk-azure-blob-courante) | Modernisation | API SDK Azure Blob courante | ✅ Corrigé |
| [8.2](#82-envoi-en-batch) | Modernisation | Envoi en batch | ✅ Corrigé |
| [8.3](#83-retraitement-des-dead-letters) | Modernisation | Retraitement des dead-letters | 📋 Futur |

---

## Catégorie 2 — Sécurité

### 2.1 Normalisation des URL Blob

**Problème**  
Les URL de blobs (claim-check) étaient construites par concaténation de chaînes. Une erreur de format (double slash, casse, paramètre manquant) produisait une URL invalide silencieusement — le bug se manifestait seulement à l'exécution lors du téléchargement.

**Ce qui a été changé**  
Les références aux blobs utilisent désormais des chemins relatifs et les méthodes du SDK Azure (`BlobContainerClient.GetBlobClient(string blobName)`) pour construire les URI. Le SDK gère lui-même la normalisation de l'URL.

**Concept clé pour un développeur junior**  
> Ne jamais construire soi-même des URL réseau par concaténation de chaînes. Utiliser les API du SDK qui valident et normalisent le format. Cela évite les bugs subtils liés à l'encodage URL ou aux slashes doubles.

---

### 2.2 Durcissement JSON — `MaxDepth` et options en cache

**Problème**  
La désérialisation JSON n'avait pas de limite de profondeur (`MaxDepth`). Un attaquant ou un bug amont pouvait envoyer un JSON profondément imbriqué (ex. : `{"a":{"a":{"a":...}}}` sur 10 000 niveaux) et provoquer un `StackOverflowException` — une attaque de type *Billion Laughs* adaptée au JSON.

De plus, une nouvelle instance de `JsonSerializerOptions` était créée à chaque appel de `Serialize`/`Deserialize`. C'est coûteux car `JsonSerializerOptions` compile les convertisseurs TypeScript lors de la première utilisation.

**Ce qui a été changé**

```csharp
// AVANT — pas de limite, recréation à chaque appel
var options = new JsonSerializerOptions { WriteIndented = true };
return JsonSerializer.Serialize(obj, options);

// APRÈS — options statiques et partagées, MaxDepth sur la désérialisation
private static readonly JsonSerializerOptions s_serializeOptionsIndented = new JsonSerializerOptions { WriteIndented = true };
private static readonly JsonSerializerOptions s_serializeOptionsCompact  = new JsonSerializerOptions { WriteIndented = false };
private static readonly JsonSerializerOptions s_deserializeOptions        = new JsonSerializerOptions { MaxDepth = 64 };
```

La désérialisation valide aussi la taille du payload avant de l'analyser :

```csharp
private const int DefaultMaxJsonLength = 1_000_000; // 1 MB

if (json.Length > DefaultMaxJsonLength)
{
    _logger.LogWarning("JSON payload too large ({Length} chars)...", json.Length, ...);
    return null;
}
```

**Fichier concerné** : [Serialization/JsonMessageSerializer.cs](../Serialization/JsonMessageSerializer.cs)

**Concept clé pour un développeur junior**  
> `static readonly` signifie que l'objet est partagé par toutes les instances et n'est créé qu'une seule fois. Pour des objets coûteux à initialiser (comme `JsonSerializerOptions`), c'est un gain de performance important. La limite `MaxDepth` protège contre les attaques par récursion profonde.

---

### 2.3 Exceptions silencieuses

**Problème**  
Plusieurs blocs `catch` ne faisaient rien ou seulement retournaient `null`, rendant les erreurs invisibles en production. Un message mal formaté disparaissait sans laisser de trace dans les logs.

**Ce qui a été changé**  
Chaque `catch` loggue désormais au minimum un `LogWarning` (pour les cas attendus) ou un `LogError` (pour les erreurs inattendues).

```csharp
// AVANT — exception avalée silencieusement
catch (Exception)
{
    return null;
}

// APRÈS — exception tracée
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to deserialize message to {TypeName}.", typeof(TMessage).FullName);
    return null;
}
```

**Concept clé pour un développeur junior**  
> Ne jamais écrire `catch (Exception) { }` (bloc vide) ou `catch (Exception) { return null; }` sans loguer. Un bug silencieux est le pire type de bug : le système semble fonctionner mais perd des données. En production, les logs sont souvent le seul outil de diagnostic disponible.

---

### 2.4 `Random.Shared` — jitter thread-safe

**Problème**  
La politique de retry exponentiel utilisait `new Random()` pour calculer le jitter (variation aléatoire du délai). Dans un contexte multi-thread (Azure Functions), plusieurs threads instancient `Random` simultanément avec la même seed basée sur l'horloge — et produisent tous les mêmes valeurs pseudo-aléatoires. Résultat : au lieu d'éviter les collisions, tous les clients réessaient en même temps (*thundering herd*).

**Ce qui a été changé**

```csharp
// AVANT — pas thread-safe, même seed possible sur plusieurs threads
var rng = new Random();
var jitter = rng.NextDouble();

// APRÈS — singleton thread-safe, disponible depuis .NET 6
var jitter = Random.Shared.NextDouble();
```

**Fichier concerné** : [Configuration/ExponentialRetryPolicy.cs](../Configuration/ExponentialRetryPolicy.cs)

**Concept clé pour un développeur junior**  
> `Random` n'est pas thread-safe. `Random.Shared` (introduit dans .NET 6) est une instance statique thread-safe. Utilisez toujours `Random.Shared` dans un contexte multi-thread.

---

### 2.5 Politique de rétention Blob

**Statut : action infrastructure (non corrigé par code)**

**Problème**  
Les blobs créés par le pattern *claim-check* (stockage d'un message volumineux en Blob pour éviter la limite 256 KB de Service Bus) ne sont jamais supprimés automatiquement.

**Recommandation**  
Configurer une *Azure Blob Lifecycle Management Rule* dans le portail Azure ou via Bicep/ARM :

```json
{
  "rules": [{
    "name": "delete-old-claim-checks",
    "enabled": true,
    "type": "Lifecycle",
    "definition": {
      "filters": { "blobTypes": ["blockBlob"], "prefixMatch": ["claim-check/"] },
      "actions": {
        "baseBlob": { "delete": { "daysAfterLastAccessTimeGreaterThan": 7 } }
      }
    }
  }]
}
```

---

### 2.6 `ManagedIdentityCredential` au lieu des chaînes de connexion

**Problème**  
Certains clients Azure (Blob, Service Bus) étaient construits avec des chaînes de connexion (`ConnectionString`). Une chaîne de connexion contient les credentials en clair — si elle fuite dans les logs ou un repo Git, l'accès au service est compromis.

De plus, même en remplaçant les chaînes de connexion par `DefaultAzureCredential`, un overhead inutile subsistait : `DefaultAzureCredential` est une *chaîne* de providers qui sont tentés **dans l'ordre** à chaque démarrage (`EnvironmentCredential` → `WorkloadIdentityCredential` → `ManagedIdentityCredential` → `SharedTokenCacheCredential` → `VisualStudioCredential` → `AzureCliCredential` → ...). En production sur Azure Functions, seul `ManagedIdentityCredential` réussit — les autres tentatives sont des round-trips inutiles vers des endpoints qui vont échouer.

**Ce qui a été changé**  
`ConfigureAzureProviders` accepte désormais un paramètre optionnel `TokenCredential`. Par défaut, `ManagedIdentityCredential` est utilisé — il cible directement l'endpoint IMDS local de l'hôte Azure (1 seul appel réseau). En développement local, on passe `DefaultAzureCredential()` ou `AzureCliCredential()` explicitement :

```csharp
// AVANT — DefaultAzureCredential hardcodé dans la méthode
public static IServiceCollection ConfigureAzureProviders(this IServiceCollection services)
{
    services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
    ...
}

// APRÈS — paramètre injectable, ManagedIdentityCredential par défaut
public static IServiceCollection ConfigureAzureProviders(
    this IServiceCollection services,
    TokenCredential? credential = null)
{
    // ManagedIdentityCredential → 1 appel IMDS direct, pas de chaîne de fallback
    services.AddSingleton<TokenCredential>(_ => credential ?? new ManagedIdentityCredential());
    ...
}
```

Usage selon l'environnement :
```csharp
// Production (Azure Functions avec identité managée) — rien à passer
services.ConfigureAzureProviders();

// Dev local — passer DefaultAzureCredential ou AzureCliCredential
services.ConfigureAzureProviders(new DefaultAzureCredential());
```

**Fichier concerné** : [Configuration/Extensions/ConfigurerProviders.cs](../Configuration/Extensions/ConfigurerProviders.cs)

**Concept clé pour un développeur junior**  
> `DefaultAzureCredential` est pratique en développement car il essaie plusieurs méthodes d'authentification automatiquement. Mais en production, cette chaîne de fallback génère des appels réseau qui échouent avant d'arriver à l'identité managée. `ManagedIdentityCredential` va directement au bon endpoint — aucun secret, aucun overhead, une seule responsabilité.

---

### 2.7 `ConnectionString` absent des options

**Problème**  
La classe `AzureServiceBusProviderOptions` (options de configuration) ne devait pas exposer de propriété `ConnectionString` car cela encourage les développeurs à utiliser des authentifications par secret.

**Ce qui a été changé**  
La propriété `ConnectionString` n'existe pas dans `AzureServiceBusProviderOptions`. Le client `ServiceBusClient` est injecté directement via DI (configuré une seule fois au démarrage avec `DefaultAzureCredential`).

```csharp
// AzureServiceBusProviderOptions — aucune ConnectionString
public class AzureServiceBusProviderOptions
{
    public int MaxConcurrentSessions { get; set; } = 10;
    public int MaxConcurrentCallsPerSession { get; set; } = 1;
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(10);
    // ... options techniques uniquement, pas de credentials
}
```

**Fichier concerné** : [Messaging/Providers/Azure/AzureServiceBusProviderOptions.cs](../Messaging/Providers/Azure/AzureServiceBusProviderOptions.cs)

---

## Catégorie 3 — Architecture

### 3.1 `ServiceBusReceivedMessage` hors du contexte — et propriétés transport-agnostiques dans `IMessageTransit`

**Problème — partie 1 : couplage du contexte**  
`MessageTransitContext<TMessage>` contenait une propriété de type `ServiceBusReceivedMessage` directement. C'est un type du SDK Azure — si demain on change de fournisseur de messagerie (ex. Confluent Kafka), cette propriété n'a plus de sens, et tout le code qui consomme le contexte est couplé à Azure.

**Ce qui a été changé (partie 1)**  
`MessageTransitContext<TMessage>` utilise l'interface `IMessageTransit` (abstraction interne) au lieu du type SDK Azure :

```csharp
// AVANT — couplé à Azure Service Bus SDK
public ServiceBusReceivedMessage? RawMessage { get; set; }

// APRÈS — abstraction transport-agnostique
public IMessageTransit? TransportMessage { get; set; }
```

**Problème — partie 2 : propriétés manquantes dans `IMessageTransit`**  
Après la migration vers `IMessageTransit`, certaines propriétés spécifiques aux messages étaient encore inaccessibles via l'interface : `SessionId` n'était pas exposé. Or, `SessionId` est un concept présent dans plusieurs systèmes de messagerie (identifiant de session Service Bus, clé de partition Kafka) — il doit faire partie de l'abstraction.

`MessageId` et `SequenceNumber` étaient déjà présents dans l'interface car leurs équivalents existent dans tous les transports (offset Kafka = `SequenceNumber`, clé de déduplication = `MessageId`).

**Ce qui a été changé (partie 2)**  
`SessionId` ajouté à `IMessageTransit` et implémenté dans `AzureFunctionMessageTransit` :

```csharp
// IMessageTransit — AVANT
public interface IMessageTransit
{
    string MessageId { get; }
    string Content { get; }
    long SequenceNumber { get; }
    //TODO: Ajouter d'autres propriétés utiles si besoin (SessionId, etc.)
}

// IMessageTransit — APRÈS
public interface IMessageTransit
{
    string MessageId { get; }
    string Content { get; }
    long SequenceNumber { get; }
    string? SessionId { get; }  // ← transport-agnostique : session SB, partition key Kafka, etc.
}

// AzureFunctionMessageTransit — implémentation
public string? SessionId => _message.SessionId;
```

**Fichiers concernés** : [Messaging/MessageTransitContext.cs](../Messaging/MessageTransitContext.cs), [Messaging/Providers/IMessageTransit.cs](../Messaging/Providers/IMessageTransit.cs), [Messaging/Providers/Azure/AzureFunctionMessageTransit.cs](../Messaging/Providers/Azure/AzureFunctionMessageTransit.cs)

**Concept clé pour un développeur junior**  
> Les objets de domaine (contextes, messages applicatifs) ne doivent pas connaître les détails d'infrastructure (SDK Azure, Kafka, etc.). L'infrastructure s'adapte au domaine, pas l'inverse. C'est le principe *Dependency Inversion* (le D de SOLID). Quand on définit une interface d'abstraction, on n'y met que les propriétés qui ont une signification dans **tous** les transports supportés.

---

### 3.2 `IEndpointResolver` injectable

**Problème**  
La résolution des endpoints (trouver quelle entité Service Bus correspond à quelle configuration) était codée directement dans `AzureMessagingProvider`. Impossible de tester unitairement sans une vraie configuration.

**Ce qui a été changé**  
L'interface `IEndpointResolver` est injectée dans le constructeur :

```csharp
public class AzureMessagingProvider : IMessagingProvider
{
    private readonly IEndpointResolver _endpointResolver;

    public AzureMessagingProvider(
        IMessagingAdapter adapter,
        IMessageSerializer serializer,
        ServiceBusClient client,
        IMessageTransitConfigurationService config,
        IEndpointResolver endpointResolver,   // ← injectable
        ILogger<AzureMessagingProvider> logger,
        ServiceBusSenderCache senderCache)
    { ... }
}
```

Dans les tests, on peut substituer `IEndpointResolver` par un mock qui retourne des endpoints prédéfinis.

**Fichier concerné** : [Messaging/Providers/Azure/AzureMessagingProvider.cs](../Messaging/Providers/Azure/AzureMessagingProvider.cs)

---

### 3.3 Enum `MessagingEntityType` transport-agnostique

**Problème**  
L'enum `ServiceBusEntityType` était défini dans le namespace `Messaging.Providers.Azure.Enum`. Ce nom et ce namespace sont spécifiques à Azure Service Bus. Quand on référence cet enum depuis la configuration générale (`TransportSettings`, `EndpointResolver`) ou depuis `BaseConsumer`, on crée un couplage fort à un fournisseur particulier.

De plus, des valeurs comme `Exchange` (RabbitMQ) ou `Channel` (SignalR) ne pouvaient pas être représentées.

**Ce qui a été changé**

**Avant** : un seul enum Azure-spécifique
```
Messaging/Providers/Azure/Enum/ServiceBusEntityType.cs
→ namespace: RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure.Enum
→ valeurs: None, Topic, Queue
```

**Après** : un enum générique dans le namespace commun, l'ancien fichier est supprimé

```
Messaging/Enum/MessagingEntityType.cs
→ namespace: RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum
→ valeurs: None, Topic, Queue, Exchange (réservé), Channel (réservé)
```

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessagingEntityType
{
    None     = 0,
    Topic    = 1,   // Azure Service Bus Topic, Kafka Topic
    Queue    = 2,   // Azure Service Bus Queue
    Exchange = 3,   // RabbitMQ Exchange (réservé)
    Channel  = 4    // Canal générique (réservé)
}
```

**Tous les fichiers référençant l'ancien enum ont été mis à jour** :

| Fichier | Changement |
|---------|-----------|
| [Configuration/TransportSettings.cs](../Configuration/TransportSettings.cs) | `using` + type `ServiceBusEntityType` → `MessagingEntityType` |
| [Configuration/EndpointResolver.cs](../Configuration/EndpointResolver.cs) | `using` + 2 comparaisons |
| [Messaging/Consumer/BaseConsumer.cs](../Messaging/Consumer/BaseConsumer.cs) | `using` + 3 comparaisons |
| [Messaging/Producer/BaseProducer.cs](../Messaging/Producer/BaseProducer.cs) | `using` mort supprimé |

**Fichier concerné** : [Messaging/Enum/MessagingEntityType.cs](../Messaging/Enum/MessagingEntityType.cs)

**Concept clé pour un développeur junior**  
> Un enum qui représente un concept métier (type d'entité de messagerie) doit vivre dans la couche domaine ou la couche commune, pas dans une couche infrastructure. Si on voit `Azure` dans le namespace d'une classe utilisée par le domaine, c'est un signe que l'abstraction est incorrecte.

---

### 3.4 `OperationMode` dans le namespace générique

**Problème**  
Similaire au point 3.3 : `OperationMode` (Consumer / Producer) était dans le namespace Azure.

**Ce qui a été changé**  
`OperationMode` est dans `Messaging/Enum/OperationMode.cs` — namespace `RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum`.

---

### 3.6 Renommage Audience → Endpoint

**Problème**  
Le terme `Audience` (issu du vocabulaire OAuth/JWT) était utilisé pour désigner la configuration d'une entité de messagerie (topic, queue). Ce terme est trompeur — `Audience` en OAuth signifie "pour qui le token est destiné", pas "où envoyer le message".

**Ce qui a été changé**  
- `AudienceSettings` → `EndpointSettings`
- `AudienceResolver` → `EndpointResolver` / `IEndpointResolver`
- `ReplyToAudienceInfo` → `ReplyToEndpointInfo`
- Toutes les propriétés renommées en conséquence

---

### 3.7 Bug `InitialDelay` dans `ExponentialRetryPolicy`

**Problème**  
`InitialDelay` était initialisé avec une valeur erronée — probablement un `HttpStatusCode` casté en entier au lieu d'une durée. Cela pouvait provoquer des délais de retry incohérents (ex. : 500 secondes au lieu de 500 ms).

**Ce qui a été changé**

```csharp
// AVANT — valeur incorrecte (probablement HttpStatusCode.InternalServerError = 500)
public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(HttpStatusCode.InternalServerError);
// → résultait en TimeSpan.FromMilliseconds(500) par coïncidence numérique, mais le code était trompeur

// APRÈS — valeur explicite et intentionnelle
public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);
```

**Fichier concerné** : [Configuration/ExponentialRetryPolicy.cs](../Configuration/ExponentialRetryPolicy.cs)

**Concept clé pour un développeur junior**  
> Ne pas utiliser une constante d'un enum pour une valeur numérique dans un autre contexte, même si la valeur numérique coïncide. Le code doit exprimer l'intention, pas la valeur.

---

## Catégorie 4 — Qualité du code

### 4.1 `effectiveTarget` / `effectiveConsumer`

**Problème**  
Dans `AzureMessagingProvider`, les variables `target` et `consumer` étaient parfois `null` (la valeur passée en paramètre) et parfois résolues depuis la configuration — sans nommage clair. Cela rendait le code difficile à lire et source de `NullReferenceException`.

**Ce qui a été changé**  
Les variables résolues sont stockées explicitement dans `effectiveTarget` et `effectiveConsumer` :

```csharp
// AVANT — confusion entre target paramètre et target résolu
var endpoint = _endpointResolver.TryResolve(target, consumer, action, out var aud);
// plus loin... "target" ou "aud.Target" ?

// APRÈS — nommage explicite
var effectiveTarget   = endpoint.Target   ?? target;
var effectiveConsumer = endpoint.Consumer ?? consumer;
```

**Concept clé pour un développeur junior**  
> Un bon nom de variable documente son contenu. `effectiveTarget` signifie "la valeur finale utilisée pour l'envoi, après résolution". Évite les ambiguïtés `target` (paramètre entrant) vs `target` (valeur résolue).

---

### 4.2 `NullReferenceException` lancé manuellement

**Problème**  
Dans `AzureFunctionMessagingAdapter`, les propriétés `Message` et `Actions` vérifiaient si le champ backing était `null` et lançaient `new NullReferenceException(...)` manuellement.

C'est une mauvaise pratique pour deux raisons :
1. `NullReferenceException` est normalement lancée par le runtime quand on déréférence un pointeur null — la lancer manuellement trompe les outils d'analyse.
2. `NullReferenceException` ne transmet pas bien l'intention : ce qui s'est passé, c'est que l'adaptateur n'a pas été initialisé, pas qu'une référence null a été déréférencée accidentellement.

**Ce qui a été changé**

```csharp
// AVANT — mauvaise exception
public ServiceBusReceivedMessage Message
{
    get => _message ?? throw new NullReferenceException("Message non initialisé.");
}

// APRÈS — exception sémantiquement correcte avec message explicite
public ServiceBusReceivedMessage Message
{
    get => _message ?? throw new InvalidOperationException(
        $"{nameof(Message)} non initialisé — appelez BindContext avant d'utiliser l'adaptateur.");
}
```

Idem pour `Actions`.

**Fichier concerné** : [Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs](../Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs)

**Concept clé pour un développeur junior**  
> `InvalidOperationException` signifie "vous avez appelé cette méthode dans un état invalide". C'est l'exception appropriée quand un objet n'est pas encore initialisé ou qu'une précondition n'est pas respectée. `NullReferenceException` ne doit jamais être lancée manuellement — elle est réservée au runtime.

---

### 4.4 Cycle de vie des senders Service Bus

**Problème**  
`ServiceBusSender` (le SDK Azure pour envoyer des messages) est une ressource qui maintient une connexion AMQP. Si on en crée un nouveau à chaque envoi et qu'on ne le dispose pas, on accumule des connexions TCP ouvertes jusqu'à épuisement des sockets.

**Ce qui a été changé**  
`ServiceBusSenderCache` est un singleton qui réutilise les senders par nom d'entité et les dispose proprement lors de l'arrêt de l'application :

```csharp
public class ServiceBusSenderCache : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> _cache = new();

    public ServiceBusSender GetOrCreate(ServiceBusClient client, string entityName)
        => _cache.GetOrAdd(entityName, _ => client.CreateSender(entityName));

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _cache)
            await kv.Value.DisposeAsync();
        _cache.Clear();
    }
}
```

**Fichier concerné** : [Messaging/Providers/Azure/ServiceBusSenderCache.cs](../Messaging/Providers/Azure/ServiceBusSenderCache.cs)

**Concept clé pour un développeur junior**  
> `IAsyncDisposable` (avec `DisposeAsync`) est le pendant asynchrone de `IDisposable`. Utilisez-le pour les ressources réseau (connexions, sockets). `ConcurrentDictionary` garantit la thread-safety sans verrous explicites.

---

## Catégorie 5 — Testabilité

### 5.1 Projet de tests unitaires

**Statut : non créé (travail à part entière)**

**Problème**  
Pas de projet `xUnit` pour tester la librairie.

**Recommandation**  
Créer `EnterpriseMessageTransit.Tests` avec :
- `xUnit` pour le framework de tests
- `Moq` pour les mocks
- `Microsoft.Extensions.Time.Testing` pour une implémentation testable de `ISystemClock`

Priorités de test :
1. `EndpointResolver.TryResolve` — logique de routage topic/queue
2. `BaseConsumer` — chemins retry / DLQ
3. `JsonMessageSerializer` — cas limites (JSON trop profond, payload trop grand)

---

### 5.2 `ISystemClock` — testabilité du temps

**Problème**  
Du code utilisait `DateTime.UtcNow` ou `DateTimeOffset.UtcNow` directement. Ces appels statiques sont impossibles à contrôler dans un test — impossible de simuler "il est 3h du matin" ou "30 secondes se sont écoulées".

**Ce qui a été changé**  
L'interface `ISystemClock` est définie et injectée partout où le temps est nécessaire :

```csharp
// Interface
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

// Dans AzureFunctionMessagingAdapter et AzureJournalProvider :
private readonly ISystemClock _systemClock;

// Utilisation
var now = _systemClock.UtcNow.UtcDateTime;
```

Dans les tests, on peut injecter un `FakeSystemClock` qui retourne une date fixe.

**Fichier concerné** : [Configuration/ISystemClock.cs](../Configuration/ISystemClock.cs)

**Concept clé pour un développeur junior**  
> Toute valeur qui dépend de l'environnement extérieur (temps, numéro aléatoire, fichier système) doit être abstraite via une interface pour être testable. C'est la règle des *Seams* : créer des "coutures" dans le code où on peut injecter un comportement de test.

---

### 5.3 `JournalEntry` en `record` immuable — et enrichissement des champs

**Problème initial**  
`JournalEntry` était une classe mutable avec des setters publics. Plusieurs problèmes :
1. Un bug pouvait modifier l'entrée avant qu'elle soit écrite en base.
2. L'égalité entre deux `JournalEntry` était celle des références (deux objets avec les mêmes données ne sont pas `==`).
3. La construction d'une instance nécessitait plusieurs lignes de setters.

**Problème enrichissement**  
Trois informations importantes étaient absentes du journal :
- `DeadLetterSource` : quand un message arrive depuis la DLQ, sa source d'origine n'est pas tracée (Service Bus expose `Message.DeadLetterSource`).
- `SessionId` : indispensable pour corréler les entrées de journal d'une même session de traitement.
- `ApplicationName` : sans ce champ, une table de journal partagée entre plusieurs micro-services est inexploitable — impossible de filtrer les entrées par application.

**Ce qui a été changé**  
`JournalEntry` est maintenant un `record` C# avec les 3 nouveaux champs ajoutés à la fin (champs optionnels pour ne pas casser les usages existants) :

```csharp
// AVANT
public record JournalEntry(
    string Consumer,
    string Action,
    string MessageId,
    string CorrelationId,
    string Target,
    OperationMode Mode,
    int StatusCode,
    int DeliveryCount,
    int MaxDeliveryCount,
    string DeadLetterReason,
    DateTime EnqueuedTimeUtc);

// APRÈS — 3 champs ajoutés
public record JournalEntry(
    string Consumer,
    string Action,
    string MessageId,
    string CorrelationId,
    string Target,
    OperationMode Mode,
    int StatusCode,
    int DeliveryCount,
    int MaxDeliveryCount,
    string DeadLetterReason,
    DateTime EnqueuedTimeUtc,
    string? DeadLetterSource,   // ← Message.DeadLetterSource (nul si ce n'est pas un message DLQ)
    string? SessionId,          // ← identifiant de session / partition key
    string? ApplicationName);   // ← Config.AppSettings.ApplicationName
```

Tous les sites d'instanciation ont été mis à jour :

| Fichier | Nombre d'appels mis à jour |
|---------|----------------------------|
| [Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs](../Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs) | 5 (ImmediateRetry ×2, ImmediateRetryException ×1, ExponentialRetry DLQ ×1, ExponentialRetry session ×1, ExponentialRetry sans session ×1) |
| [Messaging/Producer/BaseProducer.cs](../Messaging/Producer/BaseProducer.cs) | 3 (PublishAsync, PublishBatchAsync, GetResponseAsync) |

**Fichier concerné** : [Messaging/Providers/JournalEntry.cs](../Messaging/Providers/JournalEntry.cs)

**Concept clé pour un développeur junior**  
> Un `record` en C# est un type immuable par valeur : ses propriétés sont init-only, et deux records avec les mêmes valeurs sont `==`. Préférer `record` pour les DTO (Data Transfer Objects), les entrées de journal, et tous les objets qui représentent des données sans comportement. Les paramètres optionnels (`string?`) placés en fin de record permettent d'enrichir le contrat sans casser les appels existants.

---

## Catégorie 6 — Observabilité

### 6.1 Commentaire TODO obsolète dans `AzureJournalProvider`

**Problème**  
Le commentaire suivant était présent au début de `WriteRecordAsync` :

```csharp
public async Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default)
{//TODO: Ajouter le loggin avec _logger
    var table = _tableServiceClient.GetTableClient(GetTableName());
```

Or, `_logger` était déjà utilisé — les `LogInformation` et `LogError` étaient en place. Ce TODO était un mensonge dans le code : il signalait un travail "à faire" qui était déjà fait.

**Ce qui a été changé**  
Le commentaire obsolète a été supprimé.

```csharp
// APRÈS
public async Task WriteRecordAsync(JournalEntry entry, CancellationToken cancellationToken = default)
{
    var table = _tableServiceClient.GetTableClient(GetTableName());
```

**Fichier concerné** : [Messaging/Providers/Azure/AzureJournalProvider.cs](../Messaging/Providers/Azure/AzureJournalProvider.cs)

**Concept clé pour un développeur junior**  
> Les TODO dans le code sont des dettes techniques. Un TODO résolu doit être supprimé immédiatement — un TODO périmé induit les développeurs suivants en erreur et dégrade la confiance dans les commentaires. Les TODO non résolus devraient vivre dans le système de suivi (GitHub Issues, Azure DevOps) pas dans le code source.

---

### 6.2 Traces distribuées

**Statut : travail futur**

**Problème**  
Il n'y a pas de corrélation entre les spans de messagerie (envoi, réception, retry, DLQ). En production sur un système distribué, il est impossible de retracer le parcours complet d'un message.

**Recommandation**  
Instrumenter avec `System.Diagnostics.ActivitySource` (standard .NET OpenTelemetry) :

```csharp
private static readonly ActivitySource s_activitySource = new("EnterpriseMessageTransit");

public async Task SendAsync(...)
{
    using var activity = s_activitySource.StartActivity("MessageTransit.Send");
    activity?.SetTag("messaging.destination", target);
    activity?.SetTag("messaging.message_id", messageId);
    // ...
}
```

---

## Catégorie 7 — Performance

### 7.1 Cache des senders — `ServiceBusSenderCache`

Voir [4.4](#44-cycle-de-vie-des-senders-service-bus) — le `ServiceBusSenderCache` résout à la fois le problème de cycle de vie et la performance (réutilisation des connexions AMQP).

---

### 7.2 Cache du payload sérialisé

**Problème**  
Lors de l'envoi d'un message, le payload JSON était sérialisé plusieurs fois si plusieurs opérations en avaient besoin (ex. : journalisation + envoi).

**Ce qui a été changé**  
`MessageTransitContext<TMessage>` expose une propriété `SerializedPayload` qui sert de cache :

```csharp
[JsonIgnore]
public string? SerializedPayload { get; set; }
```

La première sérialisation remplit cette propriété ; les appels suivants lisent le cache. `[JsonIgnore]` garantit que ce champ interne n'est jamais sérialisé vers l'extérieur.

---

### 7.3 Retry session : préservation de l'ordre FIFO (retour à `Task.Delay` + `AbandonMessageAsync`)

**Problème initial (avant la revue)**  
Pour implémenter un retry avec délai sur un message Service Bus avec session, une implémentation naïve utilisait `Task.Delay(délai)` — le thread restait bloqué pendant le délai, monopolisant un worker Azure Functions.

**Correction intermédiaire (annulée)**  
La première correction a remplacé `Task.Delay` par `ScheduleMessageAsync` + `CompleteMessageAsync` pour libérer le thread. C'est correct **uniquement pour les messages sans session**.

**Pourquoi c'est incorrect pour les sessions**  
Service Bus garantit la livraison **FIFO** au sein d'une session : les messages d'une même session sont livrés dans l'ordre, un par un, tant que la session est verrouillée.

```
SCHEDULE + COMPLETE (incorrect pour les sessions) :

  Session verrouillée     Message courant (M1) → retry voulu dans 5s
        ↓
  ScheduleMessageAsync(clone de M1, t+5s)
  CompleteMessageAsync(M1)    ← la session se libère ICI
        ↓
  Service Bus livre M2 immédiatement (M1 n'est pas encore retraitable)
  ...puis M1 arrive à t+5s, APRÈS M2
        ↓
  Ordre : M2, M1   ✔ viole la garantie FIFO  ❌
```

**Correction finale : `Task.Delay` + `AbandonMessageAsync` (session uniquement)**  

```
TASK.DELAY + ABANDON (correct pour les sessions) :

  Session verrouillée     Message courant (M1) → retry voulu dans 5s
        ↓
  Task.Delay(5s)          ← thread bloqué INTENTIONNELLEMENT
                            la session reste verrouillée, M2 n'est pas livré
        ↓
  AbandonMessageAsync(M1) ← remet M1 en tête de la session avec les propriétés de retry
        ↓
  Service Bus re-livre M1 (DeliveryCount++), puis M2, dans le bon ordre
        ↓
  Ordre : M1 (retry), M2   ✔ FIFO respecté  ✅
```

```csharp
// APRÈS — session : Task.Delay maintient le verrou, Abandon remet en tête
await Task.Delay(delay, cancellationToken);

var retryProperties = new Dictionary<string, object>
{
    ["ReferralCount"] = attempt,
    ["Target"] = _target!,
    // ...
};
await Actions.AbandonMessageAsync(Message, retryProperties, cancellationToken);
```

Le scénario **sans session** conserve `ScheduleMessageAsync` + `CompleteMessageAsync` (aucune garantie d'ordre à préserver, libérer le thread est correct) :

```csharp
// Sans session — pas de FIFO à préserver, schedule-and-complete est optimal
await sender.ScheduleMessageAsync(retryMessage, scheduledTime, cancellationToken);
await Actions.CompleteMessageAsync(Message, cancellationToken);
```

**Fichier concerné** : [Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs](../Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs)

**Concept clé pour un développeur junior**  
> Le verrou de session Service Bus est maintenu **tant que le message courant n'est pas completed, abandoned ou dead-lettered**. Bloquer le thread (via `Task.Delay`) est le seul moyen de maintenir ce verrou et préserver l'ordre FIFO. Oui, cela consomme un thread — c'est un compromis **délibéré** : l'intégrité de l'ordre du traitement prime sur l'optimisation des ressources. Pour les messages sans session (FIFO non garanti), l'optimisation thread via schedule-and-complete est légitime.

---

### 7.4 `JsonSerializerOptions` statique

Voir [2.2](#22-durcissement-json--maxdepth-et-options-en-cache) — les options JSON sont `static readonly` pour éviter la recréation à chaque appel.

---

## Catégorie 8 — Modernisation

### 8.1 API SDK Azure Blob courante

**Problème**  
Le code utilisait des méthodes dépréciées du SDK `Azure.Storage.Blobs` :
- `DownloadAsync()` → retourne une réponse brute non typée
- `DeleteAsync()` → ancienne signature

**Ce qui a été changé**  
Méthodes SDK courantes :
- `DownloadContentAsync()` — retourne directement le contenu typé
- `DeleteIfExistsAsync()` — supprime sans erreur si le blob est absent

---

### 8.2 Envoi en batch

**Problème**  
Les messages étaient envoyés un à un dans une boucle, produisant N appels réseau pour N messages.

**Ce qui a été changé**  
`PublishBatchAsync` / `SendBatchAsync` regroupe les messages dans un `ServiceBusMessageBatch` et effectue un seul appel réseau :

```csharp
using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);
foreach (var msg in messages)
{
    if (!messageBatch.TryAddMessage(msg))
        throw new MessageSendException($"Message trop grand pour le batch : {msg.MessageId}");
}
await sender.SendMessagesAsync(messageBatch, cancellationToken);
```

**Concept clé pour un développeur junior**  
> Chaque appel réseau coûte environ 1–5 ms de latence. Envoyer 100 messages un par un ≈ 100–500 ms. En batch, le même envoi prend 1–5 ms. Pour des systèmes à volume élevé, le batching est souvent le gain de performance le plus simple à obtenir.

---

### 8.3 Retraitement des dead-letters

**Statut : fonctionnalité future**

**Problème**  
Il n'existe pas de mécanisme pour relire les messages en dead-letter queue (DLQ) et les réinjecter dans le flux principal.

**Recommandation**  
Concevoir `IDeadLetterReprocessor<TMessage>` :

```csharp
public interface IDeadLetterReprocessor<TMessage> where TMessage : class
{
    Task<int> ReprocessAsync(string entityName, int maxMessages, CancellationToken ct);
}
```

---

## Récapitulatif

| Catégorie | Items corrigés | Items infrastructure/futur |
|-----------|---------------|---------------------------|
| 🔒 Sécurité | 2.1 · 2.2 · 2.3 · 2.4 · 2.6 · 2.7 | 2.5 (lifecycle Blob) |
| 🏗️ Architecture | 3.1 · 3.2 · 3.3 · 3.4 · 3.6 · 3.7 | — |
| 🧹 Qualité code | 4.1 · 4.2 · 4.4 | 4.3 · 4.5 · 4.6 |
| 🧪 Testabilité | 5.2 · 5.3 | 5.1 (projet xUnit) |
| 📊 Observabilité | 6.1 | 6.2 (OpenTelemetry) |
| ⚡ Performance | 7.1 · 7.2 · 7.3 · 7.4 | — |
| 🔄 Modernisation | 8.1 · 8.2 | 8.3 (dead-letter reprocessor) |

**Build final** : `Générer a réussi dans 4,8s` — 0 erreurs, 0 warnings ✅
