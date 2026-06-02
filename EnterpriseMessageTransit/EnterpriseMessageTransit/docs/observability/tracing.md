# Tracing distribué — EnterpriseMessageTransit

> **Audience :** Développeur · SRE  
> **Standard :** [OpenTelemetry Semantic Conventions for Messaging 1.24](https://opentelemetry.io/docs/specs/semconv/messaging/)  
> **ActivitySource :** `RAMQ.COM.EnterpriseMessageTransit` (version `1.0.0`)

---

## Pourquoi le tracing distribué ?

Dans un système de messaging, une transaction métier peut traverser **plusieurs processus** (Activator → Service Bus → Consumer) sur des machines différentes. Sans tracing distribué, impossible de relier un appel d'entrée à une erreur 10 secondes plus tard dans un Consumer sans lire des dizaines de fichiers de logs.

Le tracing crée un **arbre de spans** liés par un `TraceId` unique — visible d'un bout à l'autre dans Azure Application Insights (Map des applications) ou Jaeger.

---

## ActivitySource EMT

```
Source  : RAMQ.COM.EnterpriseMessageTransit
Version : 1.0.0
Classe  : MessagingActivitySource (internal)
```

Le nom `RAMQ.COM.EnterpriseMessageTransit` est la clé de filtrage à utiliser dans la configuration OpenTelemetry côté application hôte.

---

## Points d'instrumentation

| # | Span name | ActivityKind | Tags ajoutés | Fichier |
|---|---|---|---|---|
| 1 | `messaging.publish` | `Producer` | `messaging.system`, `messaging.destination`, `messaging.message_id`, `messaging.session_id` | `Messaging/Producer/Producer.cs` |
| 2 | `messaging.send` | `Producer` | `messaging.system`, `messaging.destination`, `messaging.message_id`, `messaging.session_id` | `Messaging/Providers/Azure/AzureMessagingProvider.cs` |
| 3 | `messaging.consume` | `Consumer` | `messaging.system`, `messaging.correlation_id`, `messaging.consumer`, `messaging.target`, `messaging.action`, `messaging.status_code`, `messaging.mode` | `Messaging/Consumer/BaseConsumer.cs` |
| 4 | `messaging.deserialize` | `Consumer` | `messaging.message_id`, `messaging.session_id`, `messaging.destination`, `messaging.claimcheck`, `deserialization.failure_reason` (si erreur) | `Messaging/Consumer/BaseConsumer.cs` |

### Tags standard OpenTelemetry Messaging

| Tag | Valeur type | Description |
|---|---|---|
| `messaging.system` | `"azure_service_bus"` | Identifie le broker |
| `messaging.destination` | `"orders-queue"` | Nom de l'entité Service Bus |
| `messaging.message_id` | UUID v4 | `MessageTransitContext.MessageId` |
| `messaging.session_id` | UUID v4 | `MessageTransitContext.SessionId` |
| `exception.type` | `"Azure.Messaging.ServiceBus.ServiceBusException"` | Défini seulement en cas d'erreur |
| `exception.message` | texte libre | Défini seulement en cas d'erreur |

---

## Statuts de span

| Statut | Signification | Quand |
|---|---|---|
| `Ok` | Envoi réussi | `SendAsync` s'est terminé sans exception |
| `Error` | Envoi échoué | Exception capturée — tags `exception.*` ajoutés |

---

## Configuration côté application hôte

### Azure Functions (Isolated Worker)

```csharp
// Program.cs
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("RAMQ.COM.EnterpriseMessageTransit")   // ← clé de filtrage
        .AddAzureMonitorTraceExporter(o =>
        {
            o.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        }));
```

### BackgroundService (Worker / AKS)

```csharp
// Program.cs
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("RAMQ.COM.EnterpriseMessageTransit")
        .AddOtlpExporter(o =>              // vers Jaeger, Tempo, etc.
        {
            o.Endpoint = new Uri("http://otel-collector:4317");
        }));
```

> **Package requis :** `Azure.Monitor.OpenTelemetry.Exporter` ou `OpenTelemetry.Exporter.OpenTelemetryProtocol`.  
> La lib EMT elle-même n'a **aucune dépendance** OpenTelemetry — elle utilise uniquement `System.Diagnostics.ActivitySource` (BCL).

---

## Lire les traces dans Application Insights

### Query KQL — spans de publish lents (> 500 ms)

```kusto
dependencies
| where name == "messaging.publish"
| where duration > 500
| project timestamp, name, duration, resultCode, customDimensions
| order by duration desc
| take 50
```

### Query KQL — corrélation TraceId complet

```kusto
// Toutes les opérations liées à un même messageId
union requests, dependencies, traces
| where customDimensions["messaging.message_id"] == "{{MESSAGE_ID}}"
| project timestamp, itemType, name, message, duration
| order by timestamp asc
```

---

## Schéma d'arbre de spans (cible Phase 2→3)

```
[HTTP request] — Activator.PublishAsync
  └─ messaging.publish (ActivityKind.Producer)
       ├─ Tags: messaging.system=azure_service_bus
       └─ [propagé via Service Bus ApplicationProperties]

  [Azure Function trigger]
    └─ messaging.process (ActivityKind.Consumer)  ← Phase 3
         ├─ Tags: messaging.session_id, messaging.message_id
         └─ saga.stage.advance (Event)             ← Phase 3
```

---

## Propagation du contexte

> **Status Phase 2 :** La propagation de contexte W3C TraceContext entre Producer et Consumer via `ServiceBusMessage.ApplicationProperties` (`traceparent`, `tracestate`) **n'est pas encore implémentée**. Les spans Producer et Consumer sont actuellement deux arbres indépendants.  
> **Phase 3 :** Implémenter `W3C TraceContext propagation` pour relier les deux arbres en un seul arbre distribué.
