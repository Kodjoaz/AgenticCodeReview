# Idempotence producteur — Guide

> **Origine :** O9 — EMT-DistinguishedEngineerReview.md §4.3  
> **Implémenté en :** P3-T2 (27 avril 2026)

---

## 1. Comment ça marche

EMT permet au caller de fournir un `MessageId` dans `MessageTransitContext<T>.MessageId`.  
Si ce champ est renseigné, EMT l'utilise directement comme `MessageId` du message Service Bus.  
Si le champ est vide (cas par défaut), EMT génère un `Guid` unique.

```csharp
var ctx = new MessageTransitContext<MonMessage>
{
    Payload   = new MonMessage { ... },
    MessageId = "reservation-42-v1"   // fourni par le caller → idempotence possible
};
await producer.PublishAsync(ctx, cancellationToken: ct);
```

Service Bus déduplique les messages ayant le même `MessageId` dans la **fenêtre de déduplication** configurée sur l'entité (défaut : 10 minutes). Un message en double dans cette fenêtre est silencieusement rejeté — le producteur reçoit un accusé de réception normal.

---

## 2. Condition préalable — côté Service Bus

L'entité Service Bus (queue ou topic) doit avoir `RequiresDuplicateDetection = true`.  
Cette propriété est **configurée à la création de l'entité** (elle ne peut pas être activée sur une entité existante sans la recréer).

### Bicep

```bicep
resource maQueue 'Microsoft.ServiceBus/namespaces/queues@2022-01-01-preview' = {
  name: 'ma-queue-idempotente'
  properties: {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'  // 10 minutes (défaut)
  }
}
```

### Azure CLI

```bash
az servicebus queue create \
  --name ma-queue-idempotente \
  --namespace-name mon-namespace \
  --resource-group mon-rg \
  --enable-duplicate-detection true \
  --duplicate-detection-history-time-window PT10M
```

---

## 3. Activation côté EMT — `RequiresDuplicateDetection`

Par défaut, EMT **ne vérifie pas** si l'entité a la déduplication activée.  
Pour activer la vérification au démarrage :

```json
// appsettings.json
{
  "TransportSettings": {
    "EntityName": "ma-queue-idempotente",
    "RequiresDuplicateDetection": true
  }
}
```

Avec `RequiresDuplicateDetection = true`, EMT appelle `ServiceBusHealthCheck.ValidateIdempotenceAsync()` au démarrage. Si l'entité n'a pas `RequiresDuplicateDetection = true`, une `ConfigurationException` est levée avant que la première publication soit tentée (**fast-fail**).

```
ConfigurationException: L'entité 'ma-queue' n'a pas RequiresDuplicateDetection activé.
Activez RequiresDuplicateDetection sur l'entité Service Bus,
ou désactivez RequiresDuplicateDetection dans TransportSettings.
```

### Wiring manuel dans `Program.cs` / `Startup.cs`

```csharp
// À appeler après la résolution des services, avant le premier publish
await ServiceBusHealthCheck.ValidateIdempotenceAsync(
    adminClient:              adminClient,
    entityName:               transportSettings.EntityName,
    entityType:               transportSettings.EntityType,
    requiresDuplicateDetection: transportSettings.RequiresDuplicateDetection,
    cancellationToken:        ct);
```

---

## 4. Limites

| Limite | Détail |
|--------|--------|
| **Fenêtre bornée** | 10 minutes par défaut, configurable jusqu'à 7 jours. Au-delà, un message avec le même `MessageId` est accepté à nouveau. |
| **Best-effort** | La déduplication n'est pas garantie en cas de migration d'entité, de failover ou de compaction. |
| **Topic uniquement sur le namespace** | La déduplication s'applique par topic/queue — pas entre entités distinctes. |
| **Consumer non couvert** | EMT garantit que le *producteur* n'envoie pas deux fois le même message dans la fenêtre. La gestion des doublons côté consumer (traitement idempotent) reste la responsabilité de l'application. |
| **Coût réseau** | `RequiresDuplicateDetection = true` émet 1 appel HTTP de gestion par entité au démarrage. Négligeable en production, à désactiver dans les tests unitaires. |

---

## 5. Idempotence côté consumer

EMT ne fournit pas de mécanisme d'idempotence côté consumer. Le pattern recommandé :

```csharp
// Dans votre consumer
protected override async Task ExecuteAsync(
    MessageTransitContext<MonMessage> ctx,
    CancellationToken ct)
{
    // 1. Vérifier si déjà traité (par MessageId dans une table idempotence)
    if (await _idempotenceStore.AlreadyProcessedAsync(ctx.MessageId!, ct))
    {
        await CompleteMessageAsync(ct);   // acquitter sans retraiter
        return;
    }

    // 2. Traitement métier
    await TraiterAsync(ctx.Payload!, ct);

    // 3. Marquer comme traité
    await _idempotenceStore.MarkProcessedAsync(ctx.MessageId!, ct);
    await CompleteMessageAsync(ct);
}
```

---

## 6. Références

- [ADR-006 — Politique de désérialisation](adr/ADR-006-politique-deserialisation.md)
- [failure-modes.md — Modes d'échec producteur](failure-modes.md)
- `Configuration/TransportSettings.cs` — `RequiresDuplicateDetection`
- `Configuration/ServiceBusHealthCheck.cs` — `ValidateIdempotenceAsync` / `ValidateIdempotenceCoreAsync`
