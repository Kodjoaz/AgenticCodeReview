# Gestion des ServiceBusSender — avant / après

Ce document décrit la problématique de durée de vie des clients Azure SDK (`ServiceBusClient`, `ServiceBusSender`) dans le projet, les symptômes observés, la cause racine, les corrections apportées et les règles à suivre pour tout nouvel exemple.

---

## Symptômes observés

- Exception intermittente lors de l'envoi de messages :
  `ServiceBusConnection has already been closed and cannot perform the requested operation.`
- `ObjectDisposedException` sur `ServiceBusConnection`.
- Fuites de sockets et augmentation des ressources sous charge.

---

## Cause racine

`ServiceBusSender` est créé via `ServiceBusClient.CreateSender(entity)` et conserve une référence interne à la connexion AMQP du client (`ServiceBusConnection`). Si le `ServiceBusClient` est disposé (fin de scope), tous les senders qui lui sont rattachés deviennent invalides.

Le problème apparaît lorsque :

```
ServiceBusSenderCache  →  Singleton  (vit toute la durée de l'application)
ServiceBusClient       →  Scoped    (disposé à la fin de chaque scope/invocation)
```

Le cache Singleton garde des références à des senders dont le client sous-jacent a été disposé.

---

## Règles de durée de vie (Lifetime)

| Service | Durée de vie | Raison |
|---|---|---|
| `TokenCredential` | **Singleton** | Stateless, thread-safe, partageable |
| `ServiceBusClient` | **Singleton** | Thread-safe, connexion AMQP persistante, doit rester vivant aussi longtemps que les senders |
| `BlobServiceClient` | **Singleton** | Thread-safe, connexion HTTP réutilisable |
| `TableServiceClient` | **Singleton** | Thread-safe, connexion HTTP réutilisable |
| `ServiceBusSenderCache` | **Singleton** | Cache de senders indexés par `namespace|entity`, doit persister entre invocations |
| `ISystemClock` | **Singleton** | Stateless |
| `IMessagingProvider` | **Scoped** | Porte l'état du message en cours (`BindContext`), isolé par invocation |
| `IMessagingAdapter` | **Scoped** | Porte `ServiceBusReceivedMessage` + `ServiceBusMessageActions`, isolé par invocation |
| `IJournalProvider` | **Scoped** | Isolé par invocation |
| `IStorageProvider` | **Scoped** | Isolé par invocation |
| `IEndpointResolver` | **Scoped** | Isolé par invocation |
| `IMessageSerializer` | **Scoped** | Isolé par invocation |
| `AnyProducer<T>` | **Scoped** | Dépend de `IMessagingProvider` (Scoped) — ne pas enregistrer en Transient |
| Azure Function (DoWork) | **Scoped** | Une instance par invocation |

---

## Corrections apportées

### 1. ConfigureAzureProviders — durées de vie corrigées & options par défaut

**Avant**

- Clients Azure (`ServiceBusClient`, `BlobServiceClient`, `TableServiceClient`) étaient enregistrés en `Scoped`.

**Après**

- Les clients Azure sont désormais enregistrés en **Singleton**. Exemple :

```csharp
services.AddSingleton(sp => new ServiceBusClient(fqdn, tokenCredential, clientOptions));
```

- Un jeu d'options par défaut pour `ServiceBusClientOptions` est appliqué pour fournir une stratégie de retry raisonnable côté SDK :

```csharp
var clientOptions = new ServiceBusClientOptions
{
    RetryOptions = new ServiceBusRetryOptions
    {
        Mode = ServiceBusRetryMode.Exponential,
        Delay = TimeSpan.FromMilliseconds(800),
        MaxDelay = TimeSpan.FromSeconds(30),
        MaxRetries = 5,
        TryTimeout = TimeSpan.FromSeconds(60)
    }
};
```

> Ces valeurs sont des suggestions / points de départ — adaptez selon votre SLA.

### 2. ConfigureAzureProviders — erreurs de configuration explicites

Les vérifications de configuration lèvent maintenant `InvalidOperationException` (plutôt que `ArgumentNullException`) si des settings attendus sont manquants (ex. `ServiceBusNamespace`, `BlobServiceUri`).

### 3. ServiceBusSenderCache — clé composite et Dispose

- La clé du cache est la concaténation `{FullyQualifiedNamespace}|{entityName}` pour supporter plusieurs namespaces.
- Le cache implémente `IAsyncDisposable` et dispose proprement tous les `ServiceBusSender` au shutdown.

### 4. Exemples / DI fixes

- `AnyProducer<T>` et les Azure Functions (DoWork) ont été basculés de `Transient` → **`Scoped`** pour éviter les captive dependencies.

---

## Retry — SDK vs Application

Deux couches de retry sont recommandées :

1. Retry côté SDK (ServiceBusClientOptions.Retry)
   - Configurez une stratégie `ServiceBusRetryOptions` comme ci‑dessus. Le SDK gère :
     - retries pour erreurs transitoires,
     - jitter/backoff,
     - timeout par opération.
   - Avantage : simple, centralisé, couvre la majorité des incidents transitoires.

2. Retry applicatif + réparation du cache (optionnel)
   - Pour les erreurs fatales (ex. `ObjectDisposedException`, `ServiceBusException` non-transitoire) :
     - Détecter l'erreur côté `AzureMessagingProvider.SendAsync`.
     - Retirer l'entrée du cache (`TryRemove`), disposer l'ancien sender, créer un nouveau via `ServiceBusClient.CreateSender`, réinsérer, et retenter l'envoi une fois.
   - Nécessite : backoff/exponential, instrumentation et protection contre boucles de recréation.

Recommandation : activer d'abord une stratégie SDK robuste puis n'ajouter la logique de remplacement du sender qu'en cas d'incidents persistants.

---

## Exemple : ServiceBusSenderCache (implémentation actuelle)

```csharp
public class ServiceBusSenderCache : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> _cache = new();

    public ServiceBusSender GetOrCreate(ServiceBusClient client, string entityName)
    {
        var key = $"{client.FullyQualifiedNamespace}|{entityName}";
        return _cache.GetOrAdd(key, _ => client.CreateSender(entityName));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _cache)
            await kv.Value.DisposeAsync();
        _cache.Clear();
    }
}
```

> Remarque : cette implémentation ne remplace pas automatiquement un sender « corrompu » ; voir la section précédente pour l'approche applicative.

---

## Bonnes pratiques opérationnelles

- Ajuster `ServiceBusRetryOptions` selon l'environnement (dev vs prod).
- Monitorer les métriques : connexions TCP ouvertes, exceptions `ObjectDisposedException`, taux de retrys.
- Journaliser les événements importants : création/dispotion des senders, remplacement en cache, erreurs fatales.
- Pour les environnements locaux, documenter l'usage d'Azurite / VisualStudioCredential pour le développement.

---

## Fichiers modifiés

| Fichier | Modification |
|---|---|
| `src/EnterpriseMessageTransit/Configuration/Extensions/ConfigurerProviders.cs` | Clients Azure en Singleton, options retry par défaut, messages de config clairs |
| `src/EnterpriseMessageTransit/Messaging/Providers/Azure/ServiceBusSenderCache.cs` | Clé composite `namespace|entity`, `IAsyncDisposable` |
| `src/Exemples/RAMQ.Samples.Queue.Simple.Worker/Program.cs` | `AnyProducer` et `DoWork` en Scoped, usings nettoyés, VisualStudioCredential pour dev local |

---

Si vous le souhaitez, j'implémente maintenant :
- une méthode `ReplaceIfFaulted` dans `ServiceBusSenderCache` et la logique de re-création dans `AzureMessagingProvider.SendAsync`, ou
- uniquement l'instrumentation (logs métriques) pour détecter des senders corrompus.

Quelle option préférez‑vous ?