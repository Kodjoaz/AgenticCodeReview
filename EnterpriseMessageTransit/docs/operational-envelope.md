# Enveloppe opérationnelle — EnterpriseMessageTransit

> **Statut :** Valeurs de référence — à mettre à jour après exécution des benchmarks BenchmarkDotNet en Release.
>
> Exécution des benchmarks :
> ```
> dotnet run -c Release --project src/EnterpriseMessageTransit.Benchmarks
> ```
> Les résultats sont exportés dans `BenchmarkDotNet.Artifacts/results/`.

---

## 1. Sérialisation — `SerializerBenchmarks`

Mesures effectuées sur `JsonMessageSerializer` (implémentation par défaut).

| Opération | Taille payload | Médiane (µs) | Q3 (µs) | Allocation (B) |
|---|---|---|---|---|
| `Serialize` | 1 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `Serialize` | 10 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `Serialize` | 256 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `Deserialize` | 1 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `Deserialize` | 10 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `Deserialize` | 256 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `DeserializeSafe` | 1 Ko | *À mesurer* | *À mesurer* | *À mesurer* |
| `DeserializeSafe` | 10 Ko | *À mesurer* | *À mesurer* | *À mesurer* |

**Seuils d'alerte (à définir après baseline) :**
- `Serialize` 10 Ko : régression > 2× la médiane baseline → créer un ticket
- `Deserialize` 256 Ko : régression > 2× → ticket + investigation

---

## 2. Résolution d'audience — `EndpointResolverBenchmarks`

`IEndpointResolver.TryResolve` utilise un `Dictionary<string, EndpointSettings>` en interne → O(1) attendu.

| Opération | Audiences | Médiane (ns) | Q3 (ns) | Allocation (B) |
|---|---|---|---|---|
| `TryResolve` (premier) | 1 | *À mesurer* | *À mesurer* | *À mesurer* |
| `TryResolve` (premier) | 10 | *À mesurer* | *À mesurer* | *À mesurer* |
| `TryResolve` (premier) | 50 | *À mesurer* | *À mesurer* | *À mesurer* |
| `TryResolve` (dernier) | 50 | *À mesurer* | *À mesurer* | *À mesurer* |
| `TryResolve` (inconnu) | 50 | *À mesurer* | *À mesurer* | *À mesurer* |

**Critère de validation O(1) :** Médiane avec 50 audiences ≤ 2× médiane avec 1 audience.

---

## 3. Débit — tests de charge (P4-T5, requis Docker + NBomber)

> Prérequis : Service Bus Emulator (`tests/docker-compose.servicebus.yml`)

| Scénario | Cible | Résultat mesuré |
|---|---|---|
| Envoi séquentiel (1 thread) | ≥ 500 msg/s | *À mesurer* |
| Envoi concurrent (10 threads) | ≥ 3 000 msg/s | *À mesurer* |
| Batch de 100 messages | ≤ 50 ms latence P95 | *À mesurer* |
| Réception + complete (1 session) | ≥ 200 msg/s | *À mesurer* |

---

## 4. Limites d'Azure Service Bus (contraintes externes)

| Paramètre | Limite Standard | Limite Premium |
|---|---|---|
| Taille max d'un message | 256 Ko | 100 Mo |
| Taille batch max | 256 Ko | 100 Mo |
| Sessions actives simultanées | N/A | Dépend de la SKU |
| Débit max par entité | 1 000 msg/s | 10 000+ msg/s |
| Timeout de verrou par défaut | 60 s | 60 s |
| MaxDeliveryCount par défaut | 10 | 10 |

**Claim-Check automatique EMT :** déclenché quand `payload > ClaimCheckThresholdBytes` (configurable, défaut = 200 Ko).

> **`PublishBatchAsync` — batch atomique :** EMT crée un seul `ServiceBusMessageBatch` par appel. Si la somme des messages dépasse la capacité du batch, une `ArgumentException` est levée **avant tout envoi** — aucun message partiel n'est publié.
> Le Claim-Check est **interdit en batch** (voir ADR-004) — utiliser `PublishAsync` en boucle si des messages individuels sont volumineux.

---

## 5. Mémoire — profil d'allocation

| Composant | Allocation par appel | Notes |
|---|---|---|
| `Producer.PublishAsync` (1 Ko) | *À mesurer* | Inclut la sérialisation |
| `Producer.PublishAsync` (Claim-Check) | *À mesurer* | +1 upload Blob |
| `BaseConsumer.RunAsync` (1 Ko) | *À mesurer* | Inclut la désérialisation |
| `EndpointResolver.TryResolve` | *À mesurer* | Doit être ≈ 0 (lecture dict.) |

---

## 6. Recommandations de configuration production

### `PublishTimeout`

```json
{
  "Itinerary": [{
    "Target": "ma-queue",
    "Endpoint": {
      "PublishTimeout": "00:00:05"
    }
  }]
}
```

Valeur recommandée : **5 s** pour les queues standard, **30 s** pour les topics avec large audience.
Au-delà du timeout, `MessageSendException` est levée (circuit-breaker s'ouvre si configuré).

### Retry policy

```json
{
  "RetryPolicy": {
    "MaxImmediateRetries":    3,
    "InitialRetryDelay":     "00:00:01",
    "MaxRetryDelay":         "00:05:00",
    "RetryDelayMultiplier":   2.0
  }
}
```

### Taille du Claim-Check

```json
{
  "ClaimCheckThresholdBytes": 204800
}
```

200 Ko par défaut. Descendre à 50 Ko si les messages >50 Ko sont fréquents et que la latence d'upload Blob est acceptable (typiquement <100 ms sur la même région Azure).

### Limite applicative de taille de message (`MaxMessageSizeKb`)

Applicable **uniquement à `PublishBatchAsync`** — le Claim-Check couvre `PublishAsync` automatiquement.

```json
{
  "Itinerary": [{
    "Target": "ma-queue",
    "Endpoint": {
      "EntityName": "ma-queue",
      "MaxMessageSizeKb": 256
    }
  }]
}
```

| Valeur | Comportement |
|--------|-------------|
| `0` (défaut) | Aucune limite applicative — seule la limite broker (`TryAddMessage`) s'applique |
| `256` | Limite Standard Service Bus — rejet avant envoi si corps > 256 Ko |
| `1024` | Limite Premium Service Bus |
| Toute valeur inférieure | Contrainte organisationnelle (réseau, métier) — indépendante du tier Service Bus |

**Validation en deux étapes dans `PublishBatchAsync` :**
1. `MaxMessageSizeKb` → rejet fail-fast individuel par message, avant tout appel au broker
2. `ServiceBusMessageBatch.TryAddMessage` → rejet collectif (overhead headers inclus), source de vérité du broker

---

## 7. Historique des mesures

| Date | Version | Environnement | Métriques clés |
|---|---|---|---|
| *À renseigner* | — | — | — |

*Maintenir cet historique après chaque release majeure pour détecter les régressions de performance.*
