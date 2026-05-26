# Guide d'extensibilité — EnterpriseMessageTransit

## Vue d'ensemble

EnterpriseMessageTransit (EMT) est conçu autour de quatre interfaces extensibles. Chaque interface représente un point de variation que vous pouvez remplacer sans modifier le code de la bibliothèque.

```
IMessagingProvider       — Transport des messages (Service Bus, autre broker...)
IMessageSerializer       — Sérialisation du payload (JSON, MessagePack, Avro...)
IStorageProvider         — Stockage Claim-Check (Azure Blob, S3, système de fichiers...)
IMetricsProvider         — Télémétrie (OpenTelemetry, Prometheus, Application Insights...)
```

Tous ces points d'extension s'enregistrent dans le conteneur DI via `AddEnterpriseMessageTransit(...)`.

---

## 1. `IMessagingProvider` — Transport personnalisé

### Quand implémenter ?

- Remplacer Azure Service Bus par RabbitMQ, Kafka, ou un broker maison
- Implémenter un transport en mémoire pour les tests de bout-en-bout

### Interface (résumé)

```csharp
public interface IMessagingProvider
{
    EndpointSettings Resolve(string? target = null);
    Task SendAsync<T>(MessageTransitContext<T> context, MessagingOptions options, CancellationToken ct) where T : class;
    Task SendBatchAsync<T>(IEnumerable<MessageTransitContext<T>> contexts, MessagingOptions options, CancellationToken ct) where T : class;
    void BindContext(object message, object actions);
    Task CompleteMessageAsync(CancellationToken ct);
    Task DeadLetterMessageAsync(Exception exception, CancellationToken ct);
    Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken ct);
    Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken ct);
    // ... et méthodes de désérialisation
}
```

### Exemple minimal — Transport en mémoire

```csharp
public sealed class InMemoryMessagingProvider : IMessagingProvider
{
    private readonly Queue<object> _queue = new();

    public EndpointSettings Resolve(string? target = null) => new()
    {
        Target   = target ?? "default",
        Endpoint = new TransportSettings
        {
            EntityName     = "in-memory",
            EntityType     = MessagingEntityType.Queue,
            PublishTimeout = TimeSpan.FromSeconds(5)
        }
    };

    public Task SendAsync<T>(MessageTransitContext<T> ctx, MessagingOptions opts, CancellationToken ct)
        where T : class
    {
        _queue.Enqueue(ctx.Message!);
        return Task.CompletedTask;
    }

    public Task SendBatchAsync<T>(IEnumerable<MessageTransitContext<T>> ctxs, MessagingOptions opts, CancellationToken ct)
        where T : class
    {
        foreach (var ctx in ctxs) _queue.Enqueue(ctx.Message!);
        return Task.CompletedTask;
    }

    // ... autres méthodes avec implémentation vide ou throw NotSupportedException
}
```

### Enregistrement

```csharp
services.AddSingleton<IMessagingProvider, InMemoryMessagingProvider>();
```

> **Note :** Si vous remplacez `IMessagingProvider`, vous devenez responsable de la propagation du `traceparent` (W3C Trace Context) dans vos ApplicationProperties ou équivalent.

---

## 2. `IMessageSerializer` — Sérialisation personnalisée

### Quand implémenter ?

- Utiliser MessagePack pour réduire la taille des payloads (≈ 20-30 % plus compact)
- Utiliser Apache Avro pour la compatibilité de schéma
- Ajouter du chiffrement transparent au niveau de la sérialisation

### Interface

```csharp
public interface IMessageSerializer
{
    string Serialize<T>(T obj) where T : class;
    T? Deserialize<T>(string data) where T : class;
    DeserializationResult<T> DeserializeSafe<T>(string? data) where T : class;
}
```

> **Contrat critique :** `DeserializeSafe<T>` ne doit **jamais lever d'exception**. Il retourne `DeserializationResult<T>.Malformed(ex)` en cas d'erreur — c'est ce résultat que `BaseConsumer` utilise pour appliquer la politique ADR-006 (dead-letter immédiat).

### Exemple — Sérialiseur MessagePack

```csharp
using MessagePack;

public sealed class MessagePackSerializer : IMessageSerializer
{
    // MessagePack utilise les bytes → encode en base64 pour stocker dans string
    public string Serialize<T>(T obj) where T : class
        => Convert.ToBase64String(MessagePackSerializer.Serialize(obj));

    public T? Deserialize<T>(string data) where T : class
        => MessagePackSerializer.Deserialize<T>(Convert.FromBase64String(data));

    public DeserializationResult<T> DeserializeSafe<T>(string? data) where T : class
    {
        if (string.IsNullOrWhiteSpace(data))
            return DeserializationResult<T>.EmptyPayload();
        try
        {
            var bytes = Convert.FromBase64String(data);
            return DeserializationResult<T>.Success(
                MessagePackSerializer.Deserialize<T>(bytes));
        }
        catch (Exception ex)
        {
            return DeserializationResult<T>.Malformed(ex);
        }
    }
}
```

### Enregistrement

```csharp
// Remplace le JsonMessageSerializer par défaut
services.AddSingleton<IMessageSerializer, MessagePackSerializer>();
```

---

## 3. `IStorageProvider` — Stockage Claim-Check personnalisé

### Quand implémenter ?

- Remplacer Azure Blob Storage par Amazon S3, Google Cloud Storage
- Stocker les payloads dans un système de fichiers local (tests, on-premise)
- Ajouter une couche de chiffrement côté client

### Interface

```csharp
public interface IStorageProvider
{
    Task<string> UploadAsync(string content, string fileName, CancellationToken ct);
    Task<string> UploadAsync(Stream stream, string fileName, CancellationToken ct);
    Task<Stream> DownloadAsync(string reference, CancellationToken ct);
    Task DeleteAsync(string reference, CancellationToken ct);
}
```

La valeur retournée par `UploadAsync` (la *référence*) est stockée dans le message Service Bus. Elle doit être stable et téléchargeable par le consumer via `DownloadAsync(reference, ct)`.

### Exemple — Stockage système de fichiers (tests locaux)

```csharp
public sealed class LocalFileStorageProvider : IStorageProvider
{
    private readonly string _baseDir;

    public LocalFileStorageProvider(string baseDir) => _baseDir = baseDir;

    public async Task<string> UploadAsync(string content, string fileName, CancellationToken ct)
    {
        var path = Path.Combine(_baseDir, fileName);
        await File.WriteAllTextAsync(path, content, ct);
        return path; // la référence = chemin absolu
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var path = Path.Combine(_baseDir, fileName);
        await using var dest = File.Create(path);
        await stream.CopyToAsync(dest, ct);
        return path;
    }

    public Task<Stream> DownloadAsync(string reference, CancellationToken ct)
        => Task.FromResult<Stream>(File.OpenRead(reference));

    public Task DeleteAsync(string reference, CancellationToken ct)
    {
        File.Delete(reference);
        return Task.CompletedTask;
    }
}
```

### Enregistrement

```csharp
services.AddSingleton<IStorageProvider>(
    new LocalFileStorageProvider(Path.GetTempPath()));
```

---

## 4. `IMetricsProvider` — Métriques personnalisées

### Quand implémenter ?

- Publier les métriques vers Prometheus (via `prometheus-net`)
- Envoyer vers Application Insights sans OpenTelemetry
- Implémenter une cible de métriques custom ou agrégée

### Interface (principale)

```csharp
public interface IMetricsProvider
{
    void IncrementMessagesSent(string entityName, string entityType);
    void IncrementMessagesReceived(string entityName, string entityType);
    void IncrementMessagesDLQ(string entityName, string entityType);
    void IncrementDeserializationFailure(string reason);
    void RecordSendDuration(double milliseconds, string entityName);
    void RecordReceiveDuration(double milliseconds, string entityName);
    void RecordRetryDelay(double milliseconds, string retryType);
    void IncrementImmediateRetry(string entityName);
    void IncrementExponentialRetry(string entityName);
    void SetActiveSessions(int count);
    void SetCachedSenders(int count);
}
```

> **Contrat critique :** Aucune méthode ne doit lever d'exception. Le provider `NullMetricsProvider` (fourni par défaut) garantit que l'absence de métriques ne rompt jamais le flux de messages.

### Exemple — Prometheus (prometheus-net)

```csharp
using Prometheus;

public sealed class PrometheusMetricsProvider : IMetricsProvider
{
    private static readonly Counter MessagesSent =
        Metrics.CreateCounter("emt_messages_sent_total",
            "Nombre de messages envoyés",
            labelNames: ["entity_name", "entity_type"]);

    private static readonly Counter DeserializationFailures =
        Metrics.CreateCounter("emt_deserialization_failures_total",
            "Échecs de désérialisation",
            labelNames: ["reason"]);

    private static readonly Histogram SendDuration =
        Metrics.CreateHistogram("emt_send_duration_milliseconds",
            "Durée d'envoi en ms",
            labelNames: ["entity_name"],
            new HistogramConfiguration { Buckets = [10, 50, 100, 500, 1000, 5000] });

    // ... autres métriques

    public void IncrementMessagesSent(string entityName, string entityType)
        => MessagesSent.WithLabels(entityName, entityType).Inc();

    public void IncrementDeserializationFailure(string reason)
        => DeserializationFailures.WithLabels(reason).Inc();

    public void RecordSendDuration(double milliseconds, string entityName)
        => SendDuration.WithLabels(entityName).Observe(milliseconds);

    public void IncrementMessagesReceived(string entityName, string entityType) { /* ... */ }
    public void IncrementMessagesDLQ(string entityName, string entityType) { /* ... */ }
    public void RecordReceiveDuration(double ms, string entity) { /* ... */ }
    public void RecordRetryDelay(double ms, string retryType) { /* ... */ }
    public void IncrementImmediateRetry(string entityName) { /* ... */ }
    public void IncrementExponentialRetry(string entityName) { /* ... */ }
    public void SetActiveSessions(int count) { /* ... */ }
    public void SetCachedSenders(int count) { /* ... */ }
}
```

### Enregistrement

```csharp
// Remplace le NullMetricsProvider par défaut
services.AddSingleton<IMetricsProvider, PrometheusMetricsProvider>();
```

---

## Combinaisons recommandées

| Scénario | `IMessagingProvider` | `IMessageSerializer` | `IStorageProvider` | `IMetricsProvider` |
|---|---|---|---|---|
| Production Azure | `AzureMessagingProvider` (défaut) | `JsonMessageSerializer` (défaut) | `AzureStorageProvider` (défaut) | `MetricsProvider` (OTel) |
| Tests unitaires | Fake/Mock | Fake/Mock | Fake (in-memory) | `NullMetricsProvider` |
| Tests intégration | Emulateur SB | `JsonMessageSerializer` | `LocalFileStorageProvider` | `NullMetricsProvider` |
| Performance | Défaut | `MessagePackSerializer` | Défaut | `MetricsProvider` |
| On-premise | RabbitMQ custom | `JsonMessageSerializer` | Filesystem ou S3 | Prometheus custom |

---

## Points d'extension futurs (non encore couverts)

- **`IMessageRouter`** — Routage conditionnel basé sur le contenu du message (roadmap Phase 5)
- **`IRetryPolicy`** — Politique de retry entièrement pluggable (actuellement codée dans `RetryPolicyHandler`)
- **`ICircuitBreakerPolicy`** — Remplacement du circuit-breaker intégré

---

*Document généré dans le cadre de la Phase 4 — Performance et enveloppe opérationnelle.*
