# Métriques — EnterpriseMessageTransit

> **Audience :** SRE · Lead technique · Développeur senior  
> **Implémentation :** `System.Diagnostics.Metrics` (BCL .NET 8)  
> **MeterName :** `RAMQ.COM.EnterpriseMessageTransit` (version `1.0.0`)  
> **Classe :** `MetricsProvider` (internal, Phase 2)

---

## Pourquoi System.Diagnostics.Metrics ?

`System.Diagnostics.Metrics` est l'API native .NET (pas de dépendance externe). Les métriques sont **exportables** vers n'importe quel backend OpenTelemetry — Application Insights, Prometheus, Grafana — via configuration côté hôte uniquement. La bibliothèque EMT elle-même n'a aucune dépendance sur un exporter particulier.

---

## Catalogue complet des métriques

### Métriques existantes (Phase 1)

| Nom | Type | Unit | Tags | Description |
|---|---|---|---|---|
| `messages_sent_total` | Counter | messages | — | Messages envoyés avec succès |
| `messages_received_total` | Counter | messages | — | Messages reçus et traités avec succès |
| `messages_dlq_total` | Counter | messages | `entity`, `reason` | Messages envoyés en Dead Letter Queue |
| `send_duration_ms` | Histogram | ms | — | Durée des opérations d'envoi |
| `receive_duration_ms` | Histogram | ms | — | Durée des opérations de réception |
| `retry_delay_ms` | Histogram | ms | — | Délai appliqué en retry exponentiel |
| `immediate_retry_total` | Counter | messages | — | Nombre de retries immédiats |
| `exponential_retry_total` | Counter | messages | — | Nombre de retries exponentiels |
| `active_sessions` | ObservableGauge | sessions | — | Sessions Service Bus actives en mémoire |
| `cached_senders` | ObservableGauge | senders | — | Senders Service Bus en cache (réutilisation) |

### Métriques ajoutées en Phase 2 (P2-A3)

| Nom | Type | Unit | Tags | Description |
|---|---|---|---|---|
| `circuit_state` | ObservableGauge | state | `entity` | État du circuit breaker par entité : `0`=Closed, `1`=Open, `2`=HalfOpen |
| `circuit_transitions_total` | Counter | transitions | `entity`, `from`, `to` | Nombre de transitions de circuit breaker |
| `deserialization_failures_total` | Counter | messages | `reason` | Échecs de désérialisation par raison (`Malformed`, `TooLarge`, `Empty`) |
| `claim_check_upload_duration_ms` | Histogram | ms | — | Durée des uploads de blob claim-check |
| `claim_check_download_duration_ms` | Histogram | ms | — | Durée des téléchargements de blob claim-check |
| `journal_write_duration_ms` | Histogram | ms | — | Durée des écritures dans le Message Transit Journal |
| `saga_stage_advance_total` | Counter | transitions | `saga_type`, `from_stage`, `to_stage` | Avancement d'une étape saga |
| `duplicate_detected_total` | Counter | messages | `entity` | Messages dupliqués détectés (dedup par `MessageId`) |

---

## Valeurs du tag `reason` pour `messages_dlq_total`

| Valeur | Signification |
|---|---|
| `ImmediateDLQ` | Consumer a levé `ImmediateDLQException` |
| `MaxDeliveryCountExceeded` | Service Bus a dead-letteré après épuisement des tentatives |
| `Malformed` | Désérialisation impossible — JSON invalide |
| `ClaimCheckOrphan` | Blob référencé introuvable |

---

## Valeurs du tag `reason` pour `deserialization_failures_total`

| Valeur | Signification |
|---|---|
| `Malformed` | JSON structurellement invalide |
| `TooLarge` | Taille du body dépasse le seuil de désérialisation sécurisée |
| `Empty` | Body vide ou null |

---

## Valeurs du tag `circuit_state`

| Valeur `gauge` | État | Signification |
|---|---|---|
| `0` | `Closed` | Nominal — messages acceptés |
| `1` | `Open` | Incidents trop fréquents — rejet de tous les messages |
| `2` | `HalfOpen` | Test de reprise — un seul message autorisé |

### Architecture du circuit breaker — portée par instance

Le `CircuitBreakerManager` est un **état en mémoire, par processus**. Il protège les threads d'une même instance contre les cascades de timeouts, mais il n'est **pas distribué**.

```
Azure Functions (scale-out)
│
├── VM #1 → Processus dotnet → CircuitBreakerManager
│              ├── Thread A  ┐
│              ├── Thread B  ├─ partagent le MÊME état (Singleton DI, thread-safe via lock)
│              └── Thread C  ┘
│
├── VM #2 → Processus dotnet → CircuitBreakerManager  ← état ISOLÉ de VM #1
│
└── VM #3 → Processus dotnet → CircuitBreakerManager  ← état ISOLÉ de VM #1 et VM #2
```

**Ce que ça implique pour la métrique `circuit_state` :**

- Chaque instance reporte **son propre état** dans Log Analytics — la même entité Service Bus peut apparaître `Open` sur VM #1 et `Closed` sur VM #2 simultanément.
- En cas de scale-out, une nouvelle instance démarre toujours en `Closed` (aucun historique).
- Un redémarrage d'instance remet l'état à zéro.

**Ce que le circuit breaker protège réellement :**  
Une seule instance contre la répétition de tentatives échouées vers Service Bus. Si une VM détecte 5 échecs consécutifs, elle arrête de marteler pendant 30 s — mais les autres VMs ne le savent pas.

**Limitation connue :** la protection distribuée (état partagé entre toutes les instances) n'est pas implémentée. Elle nécessiterait un store externe (ex. Azure Cache for Redis). Ce cas n'est pas adressé dans la version actuelle d'EMT.

**Interprétation d'une alerte `circuit_state ≥ 1` :**  
Au moins une instance a détecté suffisamment d'échecs pour ouvrir son circuit local. Vérifier l'état de Service Bus et les logs de l'instance concernée (filtrer par `cloud_RoleInstance` dans Log Analytics).

---

## Seuils d'alerte recommandés

| Métrique | Seuil | Sévérité | Action |
|---|---|---|---|
| `circuit_state{entity=*}` ≥ 1 | Persistant > 60 s | 🔴 Critical | Vérifier Service Bus + incident Azure |
| `circuit_transitions_total{to=Open}` rate > 0 | Toute occurrence | 🟠 High | Analyser cause (throttling, erreur réseau) |
| `messages_dlq_total` rate > 5/min | 5 min glissantes | 🟠 High | Inspecter DLQ + logs Consumer |
| `deserialization_failures_total{reason=Malformed}` > 0 | Toute occurrence | 🟡 Medium | Vérifier version Producer |
| `claim_check_download_duration_ms` p99 > 2000 ms | Fenêtre 5 min | 🟡 Medium | Vérifier latence Azure Blob Storage |
| `send_duration_ms` p99 > 1000 ms | Fenêtre 5 min | 🟡 Medium | Vérifier throttling Service Bus |
| `duplicate_detected_total` rate > 0 | Fenêtre 1 h | 🔵 Info | Vérifier cohérence des `MessageId` Producer |

---

## Configuration côté application hôte

### Azure Functions (Isolated Worker) — Application Insights

```csharp
// Program.cs
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("RAMQ.COM.EnterpriseMessageTransit")     // ← clé de filtrage
        .AddAzureMonitorMetricExporter(o =>
        {
            o.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        }));
```

### BackgroundService — Prometheus

```csharp
// Program.cs
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("RAMQ.COM.EnterpriseMessageTransit")
        .AddPrometheusExporter());

// Exposition de l'endpoint /metrics pour Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

---

## Requêtes KQL — Azure Application Insights

### Toutes les métriques EMT — dernière heure

```kusto
customMetrics
| where name startswith "messages_" or name startswith "circuit_" 
       or name startswith "send_" or name startswith "receive_"
       or name startswith "saga_" or name startswith "claim_check_"
       or name startswith "journal_" or name startswith "retry_"
       or name startswith "duplicate_"
| where timestamp > ago(1h)
| summarize avg(value), max(value), sum(valueCount) by name, bin(timestamp, 5m)
| order by timestamp desc
```

### Circuit breakers ouverts en ce moment

```kusto
customMetrics
| where name == "circuit_state"
| where value >= 1
| summarize arg_max(timestamp, value) by tostring(customDimensions["entity"])
| project entity = tostring(customDimensions_entity), 
          state = iff(value == 1, "Open", "HalfOpen"),
          lastSeen = timestamp
```

### Taux de DLQ par entité — dernières 6 heures

```kusto
customMetrics
| where name == "messages_dlq_total"
| where timestamp > ago(6h)
| summarize sum(value) by 
    entity = tostring(customDimensions["entity"]), 
    reason = tostring(customDimensions["reason"]),
    bin(timestamp, 15m)
| order by timestamp desc
```

### Percentiles de latence d'envoi

```kusto
customMetrics
| where name == "send_duration_ms"
| where timestamp > ago(1h)
| summarize 
    p50 = percentile(value, 50),
    p95 = percentile(value, 95),
    p99 = percentile(value, 99),
    max = max(value)
  by bin(timestamp, 5m)
| order by timestamp desc
```

### Saga — avancement par type

```kusto
customMetrics
| where name == "saga_stage_advance_total"
| where timestamp > ago(24h)
| summarize total = sum(value) by 
    saga_type   = tostring(customDimensions["saga_type"]),
    from_stage  = tostring(customDimensions["from_stage"]),
    to_stage    = tostring(customDimensions["to_stage"])
| order by total desc
```
