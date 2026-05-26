# EMT — Plan d'exécution Phase 3 : Polissage et opportunités de qualité

> **Couleur de référence :** 🟡 Mineur
> **Source :** [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md) — Points ouverts O8 · O9 · O10 · O11 · O14 · O15
> **Date de planification :** 27 avril 2026
> **Date de démarrage :** 27 avril 2026
> **Durée indicative :** 3-5 semaines
> **Risque / Complexité :** 🟢 Faible — améliorations locales sans impact sur les contrats publics
> **Prérequis :** Phase 1 ✅ complétée · Phase 2 ✅ complétée (27 avril 2026)
> **Statut :** ✅ Clôturée — 27 avril 2026
> **Complètes :** P3-T1 ✅ · P3-T2 ✅ · P3-T3 ✅ · P3-T4 ✅ · P3-T5 ✅ · P3-T6 ✅

---

## Positionnement de la phase

> 💡 **Pour les développeurs juniors.** Une observation « mineure » en ingénierie n'est pas une observation sans importance : c'est une observation qui peut être traitée **sans bloquer les autres équipes** et **sans risque de régression en production**. Ce sont souvent des améliorations de performance, de testabilité ou de robustesse qui ont un rapport coût/bénéfice excellent une fois les phases critiques terminées.

La Phase 3 traite 6 points qui partagent un trait commun : chaque amélioration est **circonscrite à un fichier ou une méthode**, sans modification de contrat. Toutes peuvent être livrées en PRs indépendants, ce qui rend cette phase naturellement parallélisable entre plusieurs développeurs juniors ou en formation.

---

## Points ouverts traités dans cette phase

| ID | Intitulé | Section DE Review | Criticité |
|----|----------|-------------------|-----------|
| **O8** | Extensibilité fictive : 7 interfaces, 1 seule implémentation chacune | §2.2 | 🟡 Mineur |
| **O9** | Idempotence producteur non garantie (`RequiresDuplicateDetection` non prérequis) | §4.3 | 🟡 Mineur |
| **O10** | Timeout global `PublishAsync` non borné | §4.4 | 🟡 Mineur |
| **O11** | `DeserializationResult<T>` livré mais call-sites, métriques et politique DLQ manquants | §4.2 | 🟡 Mineur |
| **O14** | Journal synchrone O(n) dans `PublishBatchAsync` | §5.2 | 🟡 Mineur |
| **O15** | `EndpointResolver.TryResolve` — allocations LINQ répétées | §5.3 | 🟡 Mineur |

---

## Tâche P3-T1 — Résoudre l'extensibilité fictive (O8)

**Origine :** §2.2 EMT-DistinguishedEngineerReview.md

### Contexte

EMT expose 7 interfaces d'extension (`IMessagingProvider`, `IMessagingAdapter`, `IJournalProvider`, `IStorageProvider`, `IMessageSerializer`, `IRetryPolicyHandler`, `IMetricsProvider`) avec une seule implémentation chacune. La revue distinguée note que cette extensibilité est « fictive » : aucun test ne la valide, et les implémentations alternatives n'ont aucun point d'entrée documenté.

Ce point est à la fois une observation d'architecture et une opportunité de qualité :
- Si l'extensibilité est **une vraie promesse** (ex. remplacement du journal par Cosmos, du stockage claim-check par une autre implémentation), alors la valider par des tests de contrat d'interface.
- Si l'extensibilité est **une aspiration** non prioritaire, alors la documenter honnêtement et éviter les abstractions coûteuses sans bénéfice immédiat.

### Étapes d'exécution

#### Étape A — Décider quelles interfaces sont vraiment extensibles

Après la décision D1 (ADR-001), classifier les 7 interfaces :

| Interface | Extensibilité réelle | Preuve | Action |
|-----------|---------------------|--------|--------|
| `IMessageSerializer` | ✅ Oui — déjà utilisée par les équipes (ex. MessagePack) | Tests de contrat | Écrire les tests de contrat |
| `IJournalProvider` | ✅ Oui — remplacement par Table Storage → Cosmos planifié | Usage documenté | Écrire les tests de contrat |
| `IStorageProvider` | ✅ Oui — claim-check sur Blob ; autre implémentation possible (ex. File Share) | Usage documenté | Écrire les tests de contrat |
| `IMetricsProvider` | ✅ Oui — OTEL Prometheus vs Application Insights | Usage prévu | Écrire les tests de contrat |
| `IMessageSerializer` | ✅ Oui — déjà utilisée par les équipes (ex. MessagePack) | Tests de contrat | Écrire les tests de contrat |
| `IMessagingProvider` | 🔴 Non — `internal`, multi-broker hors périmètre (ADR-001) | ADR-001 | Documenter : « hors périmètre actuel » |
| `IMessagingAdapter` | 🔴 Non — `internal`, lié à IMessagingProvider | ADR-001 | Documenter : « hors périmètre actuel » |
| `IRetryPolicyHandler` | 🟡 Partielle — stratégie circuit-breaker alternative possible | Future | Documenter |

#### Étape B — Tests de contrat d'interface pour les 4 extensibles confirmées

```csharp
// Tests/Contracts/IMessageSerializerContractTests.cs
// Hérité par tous les tests d'implémentation concrète
public abstract class IMessageSerializerContractTests
{
    protected abstract IMessageSerializer CreateSerializer();

    [Fact]
    public void Serialize_ProduitsJsonValide()
    {
        var serializer = CreateSerializer();
        var obj = new SamplePayload { Id = "123", Nom = "Test" };

        var json = serializer.Serialize(obj);

        Assert.NotEmpty(json);
        using var doc = JsonDocument.Parse(json); // ne doit pas lever d'exception
    }

    [Fact]
    public async Task Deserialize_RoundTrip_EstFidèle()
    {
        var serializer = CreateSerializer();
        var original = new SamplePayload { Id = "456", Nom = "Roundtrip" };

        var json = serializer.Serialize(original);
        var result = await serializer.DeserializeAsync<SamplePayload>(json);

        Assert.Equal(original.Id, result!.Id);
        Assert.Equal(original.Nom, result.Nom);
    }

    [Fact]
    public async Task Deserialize_RetourneNull_SurJsonVide()
    {
        var serializer = CreateSerializer();
        var result = await serializer.DeserializeAsync<SamplePayload>("");
        Assert.Null(result);
    }

    [Fact]
    public async Task Deserialize_RetourneNull_SurJsonMalformé()
    {
        var serializer = CreateSerializer();
        var result = await serializer.DeserializeAsync<SamplePayload>("{invalide");
        Assert.Null(result);
    }
}

// Tests/Unit/Serialization/JsonMessageSerializerTests.cs
// Hérite et valide l'implémentation concrète
public class JsonMessageSerializerTests : IMessageSerializerContractTests
{
    protected override IMessageSerializer CreateSerializer()
        => new JsonMessageSerializer();
}
```

Appliquer le même pattern pour `IJournalProvider`, `IStorageProvider`, `IMetricsProvider`.

#### Étape C — Documentation dans `docs/extensibility.md`

```markdown
# Guide d'extensibilité — EnterpriseMessageTransit

## Interfaces extensibles aujourd'hui

### IMessageSerializer
Implémentation par défaut : `JsonMessageSerializer` (System.Text.Json, options strictes).
Pour substituer : implémenter `IMessageSerializer` et enregistrer via DI :
`services.AddSingleton<IMessageSerializer, MonSerializer>();`

### IJournalProvider
Implémentation par défaut : `TableStorageJournalProvider` (Azure Table Storage).
Alternative Cosmos DB : prévu Phase 4 (ADR-009 en cours).

### IStorageProvider
Implémentation par défaut : `BlobStorageProvider` (Azure Blob Storage).

### IMetricsProvider
Implémentation par défaut : `MetricsProvider` (System.Diagnostics.Metrics — OTEL-compatible).
Alternative Application Insights custom events : voir `docs/observability/metrics.md`.

## Interfaces non extensibles (périmètre actuel)
- `IMessagingProvider`, `IMessagingAdapter`, `IRetryPolicyHandler`
- Ces interfaces sont `internal` (cf. ADR-007 + ADR-001). Le multi-broker est hors périmètre : elles ne seront pas ouvertes dans ce plan.
```

### Critère de sortie

- [x] Classification documentée des 7 interfaces (extensible / internal hors périmètre / future). *(ADR-007 enrichi — 27 avril 2026)*
- [x] Tests de contrat d'interface pour `IMessageSerializer`, `IJournalProvider`, `IStorageProvider`, `IMetricsProvider`. *(`Contracts/IMessageSerializerContractTests.cs` · `IJournalProviderContractTests.cs` · `IStorageProviderContractTests.cs` · `IMetricsProviderContractTests.cs` — 28 tests — 27 avril 2026)*
- [x] `docs/extensibility.md` publié. *(Phase 4 ✅ — 27 avril 2026 — `src/EnterpriseMessageTransit/docs/extensibility.md`)*

---

## Tâche P3-T2 — Idempotence producteur et `RequiresDuplicateDetection` (O9)

**Origine :** §4.3 EMT-DistinguishedEngineerReview.md

### Contexte

`PublishAsync` accepte un `MessageId` fourni par le caller (feature ajoutée et confirmée fonctionnelle lors de la revue Lead). Cependant, cette idempotence n'est **réelle** que si l'entité Service Bus est configurée avec `RequiresDuplicateDetection = true` (fenêtre de déduplication). Sans cette propriété sur l'entité, un message avec le même `MessageId` est accepté deux fois.

EMT ne vérifie pas si `RequiresDuplicateDetection` est activé sur l'entité — et ne le documente pas.

### Étapes d'exécution

#### Étape A — Validation au démarrage (fast-fail)

```csharp
// Dans ServiceBusHealthCheck.CheckHealthAsync ou dans ConfigurerProviders.ValidateConfiguration
private static async Task ValidateDuplicateDetectionIfRequiredAsync(
    ServiceBusAdministrationClient adminClient,
    string entityPath,
    bool requiresDeduplication,
    CancellationToken cancellationToken)
{
    if (!requiresDeduplication)
        return;

    // Obtenir les propriétés de l'entité (queue ou topic)
    bool hasDuplicateDetection;

    if (entityPath.Contains('/'))
    {
        // Topic
        var topicName = entityPath.Split('/')[0];
        var props = await adminClient.GetTopicAsync(topicName, cancellationToken);
        hasDuplicateDetection = props.Value.RequiresDuplicateDetection;
    }
    else
    {
        // Queue
        var props = await adminClient.GetQueueAsync(entityPath, cancellationToken);
        hasDuplicateDetection = props.Value.RequiresDuplicateDetection;
    }

    if (!hasDuplicateDetection)
    {
        throw new ConfigurationException(
            $"Entity '{entityPath}' does not have RequiresDuplicateDetection enabled. " +
            $"Cannot guarantee idempotent publish when MessageId is provided by caller. " +
            $"Enable RequiresDuplicateDetection on the entity, or do not rely on MessageId for idempotence.");
    }
}
```

**Note architecturale :** Cette validation est optionnelle et doit être activée via `TransportSettings.EnforceIdempotentPublish = true` (opt-in) pour ne pas casser les projets qui ne font pas d'idempotence. C'est une contrainte coûteuse (1 appel HTTP de gestion par démarrage) et inutile si le caller ne fournit pas de `MessageId`.

#### Étape B — Documentation dans `docs/idempotence.md`

```markdown
# Idempotence producteur — Guide

## Comment ça marche
EMT permet au caller de fournir un `MessageId` dans `MessageTransitContext<T>.MessageId`.
Si ce champ est renseigné, EMT l'utilise comme `MessageId` du message Service Bus.

Service Bus déduplique les messages avec le même `MessageId` dans la fenêtre configurée
(par défaut : 10 minutes). Un message en double dans cette fenêtre est silencieusement rejeté.

## Condition préalable
L'entité Service Bus doit avoir `RequiresDuplicateDetection = true` (propriété de l'entité,
configurée à la création ou via le portail Azure / Bicep / az CLI).

## Activation côté EMT
```json
"TransportSettings": {
  "EnforceIdempotentPublish": true
}
```
Avec cette option, EMT vérifie au démarrage que les entités cibles ont la déduplication activée.

## Limites
- La fenêtre de déduplication est bornée (10 min par défaut, max 7 jours).
- La déduplication est best-effort : elle ne garantit pas l'idempotence en cas de compaction
  ou de migration d'entité.
- Ne remplace pas l'idempotence côté consumer (le consumer doit vérifier s'il a déjà traité
  le message — c'est la responsabilité de l'application, pas d'EMT).
```

### Critère de sortie

- [x] `TransportSettings.EnforceIdempotentPublish` (opt-in, défaut : `false`). *(`TransportSettings.cs` — 27 avril 2026)*
- [x] Validation au démarrage si `EnforceIdempotentPublish = true`. *(`ServiceBusHealthCheck.ValidateIdempotenceCoreAsync` + `ValidateIdempotenceAsync` — 27 avril 2026)*
- [x] `IdempotenceValidationService` (`IHostedService`) enregistré via `ConfigurerProviders` : branche la validation au lifecycle .NET (StartAsync) — n'était pas appelée avant. *(8 mai 2026 — 8 tests dans `IdempotenceValidationServiceTests.cs`)*
- [x] Test : exception levée au démarrage si `EnforceIdempotentPublish = true` ET entité sans déduplication. *(`ServiceBusHealthCheckIdempotenceTests.cs` — 5 tests — 27 avril 2026)*
- [x] `docs/idempotence.md` publié. *(`docs/idempotence.md` — 27 avril 2026)*

---

## Tâche P3-T3 — Timeout global `PublishAsync` borné (O10)

**Origine :** §4.4 EMT-DistinguishedEngineerReview.md

### Contexte

`PublishAsync` prend un `CancellationToken` mais ne pose **aucun timeout maximal** interne. Si le caller passe `CancellationToken.None` (cas fréquent dans les Azure Functions Consumption où le CT n'est pas propagé), un envoi bloqué peut faire expirer le host tout entier, ce qui provoque un message orphelin (ni envoyé ni abandonné — visible comme perte en production).

### Étapes d'exécution

#### Étape A — `PublishTimeout` dans `TransportSettings`

```csharp
// Configuration/TransportSettings.cs — nouvelle propriété
/// <summary>
/// Délai maximal d'un appel PublishAsync avant annulation.
/// Défaut : 30 secondes. Valeur 0 désactive le timeout (déconseillé).
/// </summary>
public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(30);
```

#### Étape B — Appliquer le timeout dans `Producer.PublishCoreAsync`

```csharp
// Messaging/Producer/Producer.cs
public async Task<MessageTransitContext<MessageTransitResponse>> PublishAsync(
    MessageTransitContext<TMessage> context,
    PublishOptions? options = null,
    CancellationToken cancellationToken = default)
{
    var timeoutSeconds = _transportSettings.PublishTimeout;
    using var timeoutCts = timeoutSeconds > TimeSpan.Zero
        ? new CancellationTokenSource(timeoutSeconds)
        : null;

    using var linkedCts = timeoutCts is not null
        ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
        : null;

    var effectiveCt = linkedCts?.Token ?? cancellationToken;

    try
    {
        return await PublishCoreAsync(context, options, effectiveCt);
    }
    catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
    {
        throw new MessageSendException(
            $"PublishAsync timed out after {timeoutSeconds.TotalSeconds:F0}s for target='{_resolvedTarget}'.");
    }
}
```

**Même pattern** à appliquer dans `PublishBatchAsync`.

### Critère de sortie

- [x] `TransportSettings.PublishTimeout` avec défaut de 30 secondes. *(`TransportSettings.cs` — 27 avril 2026)*
- [x] Timeout appliqué dans `PublishAsync` et `PublishBatchAsync` via `CancellationTokenSource` lié. *(27 avril 2026)*
- [x] Test : `PublishAsync` lève `MessageSendException` si le fournisseur est bloqué au-delà du timeout. *(`Phase3QualityTests.cs` — 27 avril 2026)*
- [x] Test : `PublishAsync` respecte le `CancellationToken` caller même si le timeout n'est pas atteint. *(`Phase3QualityTests.cs` — 27 avril 2026)*
- [x] Entrée CHANGELOG : `TransportSettings.PublishTimeout` documenté dans `[Unreleased] / Phase 3`. *(8 mai 2026)*

---

## Tâche P3-T4 — Compléter `DeserializationResult<T>` (O11)

**Origine :** §4.2 EMT-DistinguishedEngineerReview.md

### Contexte

`DeserializationResult<T>` existe dans le code et expose `FailureReason`, `IsSuccess`, `Value`. La revue distinguée note que le type est **livré mais incomplet** : les sites d'appel (call-sites) dans `BaseConsumer` n'utilisent pas le `FailureReason` pour :
1. Incrémenter la métrique `deserialization_failures_total{reason=...}`.
2. Décider de la politique DLQ (Malformed → DLQ immédiat, Empty → Drop silencieux, TooLarge → DLQ + alerte).

### Étapes d'exécution

#### Étape A — ADR-006 : politique par `DeserializationFailureReason`

```markdown
# ADR-006 — Politique DLQ par DeserializationFailureReason

## Décision

| FailureReason | Action | Justification |
|---------------|--------|---------------|
| `Malformed`   | DLQ immédiat | Message structurellement invalide. Aucun retry ne le corrigera. Préserver en DLQ pour forensic. |
| `Empty`       | Drop silencieux + log Warning | Message vide = artefact d'infrastructure (probe, heartbeat). Pas de valeur forensic. |
| `TooLarge`    | DLQ + alerte métriques | Violation de contrat de taille. Indique un bug côté producteur. Alerte équipe. |
| `TypeMismatch`| DLQ + alerte + log enrichi | Producteur et consommateur désynchronisés. Indique une version incompatible. |
```

#### Étape B — Implémenter la politique dans `BaseConsumer`

> ⚠️ **Breaking change 8 mai 2026** — Le code ci-dessous représente l'**intention originale** de P3-T4 (settlement automatique par EMT). Cette approche a été **inversée** : EMT n'effectue plus aucun settlement. Le consumer applicatif décide. ADR-006 marqué *Superseded*. Voir `CHANGELOG.md / ### Changements cassants`.

**Code original (superseded — settlement automatique) :**
```csharp
// ❌ Ce code N'EXISTE PLUS dans BaseConsumer
// Logique HandleDeserializationResultAsync — settlement automatique retiré le 8 mai 2026
switch (result.FailureReason)
{
    case DeserializationFailureReason.Empty:
        await CompleteMessageAsync(actions, cancellationToken);   // ← supprimé
        return;

    case DeserializationFailureReason.Malformed:
    case DeserializationFailureReason.TooLarge:
        await _retryPolicyHandler.HandleDeadLetterAsync(...);     // ← supprimé
        return;
}
```

**Code réel (8 mai 2026) :**
```csharp
// Messaging/Consumer/BaseConsumer.cs — DeserializeMessageAsync<T>()
var result = MessagingProvider.DeserializeMessageSafe<TAnyMessage>();

if (!result.IsSuccess)
{
    activity?.SetStatus(ActivityStatusCode.Error, result.FailureReason.ToString());
    consumeActivity?.SetStatus(ActivityStatusCode.Error, result.FailureReason.ToString());
    _metrics?.IncrementDeserializationFailure(result.FailureReason.ToString()); // ← seule action d'EMT
    Logger.LogWarning("DeserializeMessageAsync: échec ({FailureReason}) — le consumer décide du settlement.",
        result.FailureReason, result.ErrorMessage);
    return result; // ← le consumer inspecte result.IsSuccess et décide
}
```

**Pattern attendu dans le consumer applicatif :**
```csharp
var result = await _consumer.DeserializeMessageAsync<MonMessage>(ct);
if (!result.IsSuccess)
{
    if (result.FailureReason == DeserializationFailureReason.EmptyPayload)
        await _consumer.CompleteMessageAsync(ct);   // drop silencieux
    else
        await _consumer.DeadLetterMessageAsync(result.Exception!, ct);
    return;
}
var ctx = result.Value!;
```

### Critère de sortie

- [x] ADR-006 signé : politique DLQ par `DeserializationFailureReason`. *(`docs/adr/ADR-006-politique-dlq-deserialisation.md` — clôturé en Phase 2 — 27 avril 2026)*
- [x] `BaseConsumer` utilise `result.FailureReason` pour décider de la politique (settlement avant retour). *(`BaseConsumer.DeserializeMessageAsync<T>()` — 27 avril 2026)*
- [x] `IMessagingProvider.DeserializeMessageSafe<T>()` exposé + implémenté dans `AzureMessagingProvider`. *(27 avril 2026)*
- [x] Métrique `deserialization_failures_total{reason=<reason>}` incrémentée sur chaque échec. *(Phase 4 ✅ — 27 avril 2026 — `BaseConsumer._metrics?.IncrementDeserializationFailure(...)`)*
- [x] Tests : Malformed → DLQ, Empty → Complete (drop), TooLarge → DLQ. *(Phase 4 ✅ — 27 avril 2026 — 3 tests contrat `IMetricsProviderContractTests`)*
> ⚠️ **Breaking change — 8 mai 2026 :** L'implémentation ci-dessus (settlement automatique par EMT) a été inversée. `DeserializeMessageAsync` retourne désormais `DeserializationResult<MessageTransitContext<T>>` sans settlement — le consumer applicatif décide. `TryDeserializeMessageAsync` supprimé. ADR-006 marqué *Superseded*. Voir section *Changements cassants* dans `CHANGELOG.md`.
---

## Tâche P3-T5 — Journal asynchrone dans `PublishBatchAsync` (O14)

**Origine :** §5.2 EMT-DistinguishedEngineerReview.md

### Contexte

`PublishBatchAsync` appelle le journal **de façon séquentielle et synchrone** pour chaque message, dans la boucle d'envoi :

```csharp
// Code actuel (reconstitué)
foreach (var context in contexts)
{
    await _messagingProvider.SendAsync(context, options, cancellationToken);
    await _journalProvider.WriteAsync(JournalEntry.ForPublish(...)); // appel réseau bloquant dans la boucle
}
```

Pour 100 messages, cela produit 100 appels réseau séquentiels vers Table Storage. La latence totale est O(n × latence_journal). Pour un batch de 1000 messages : si la latence journal = 5ms → 5 secondes ajoutées inutilement.

### Stratégie corrigée : journal en parallèle après le batch

```csharp
// Messaging/Producer/Producer.cs — PublishBatchAsync corrigé
public async Task<IReadOnlyList<MessageTransitContext<MessageTransitResponse>>> PublishBatchAsync(
    IReadOnlyList<MessageTransitContext<TMessage>> contexts,
    PublishOptions? options = null,
    CancellationToken cancellationToken = default)
{
    // 1. Envoyer tous les messages d'abord (critique : chemin d'envoi non perturbé)
    var results = await SendBatchInternalAsync(contexts, options, cancellationToken);

    // 2. Journaliser en parallèle hors du chemin critique
    //    Ne bloque pas le retour au caller. Les erreurs sont loggées, pas propagées (pattern A5).
    _ = WriteJournalBatchAsync(results, cancellationToken);

    return results;
}

private async Task WriteJournalBatchAsync(
    IReadOnlyList<MessageTransitContext<MessageTransitResponse>> results,
    CancellationToken cancellationToken)
{
    var journalTasks = results.Select(result => WriteJournalSafeAsync(result, cancellationToken));

    try
    {
        await Task.WhenAll(journalTasks);
    }
    catch (Exception ex)
    {
        // Isolation complète : journal ne doit jamais impacter le caller
        Logger.LogWarning(ex, "Journal batch write partially failed. {Count} entries may be missing.",
            results.Count);
    }
}

private async Task WriteJournalSafeAsync(
    MessageTransitContext<MessageTransitResponse> result,
    CancellationToken cancellationToken)
{
    try
    {
        var entry = JournalEntry.ForPublish(
            consumer:         _consumer,
            action:           _action,
            messageId:        result.MessageId,
            correlationId:    result.CorrelationId,
            target:           _resolvedTarget,
            sessionId:        result.SessionId,
            applicationName:  _applicationName);

        await _journalProvider.WriteAsync(entry, cancellationToken);
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex,
            "Journal write failed for MessageId={MessageId}. Non-blocking.",
            result.MessageId);
    }
}
```

**Considération de backpressure :** Pour des batches très larges (> 500 messages), limiter le parallélisme du journal :

```csharp
// Option : SemaphoreSlim pour limiter les appels réseau simultanés vers Table Storage
private static readonly SemaphoreSlim _journalSemaphore = new(maxConcurrency: 50);

private async Task WriteJournalSafeAsync(
    MessageTransitContext<MessageTransitResponse> result,
    CancellationToken cancellationToken)
{
    await _journalSemaphore.WaitAsync(cancellationToken);
    try { /* écriture journal */ }
    finally { _journalSemaphore.Release(); }
}
```

### Critère de sortie

- [x] Journal dans `PublishBatchAsync` parallélisé via `Task.WhenAll` (pattern A5 maintenu). *(27 avril 2026)*
- [x] Méthode helper `WriteJournalEntrySafeAsync` extraite dans `Producer.cs`. *(27 avril 2026)*
- [x] Test : `PublishBatchAsync` avec 10 messages — toutes les entrées de journal écrites. *(`Phase3QualityTests.cs` — 27 avril 2026)*
- [ ] `SemaphoreSlim` avec parallélisme max configurable (`TransportSettings.JournalMaxConcurrency`, défaut : 50). *(Différé Phase 4 — utile uniquement si batches > 500 messages)*
- [ ] Métrique `journal_write_duration_ms` — durée totale du batch journal. *(Différé Phase 4)*

---

## Tâche P3-T6 — Optimiser `EndpointResolver.TryResolve` (O15)

**Origine :** §5.3 EMT-DistinguishedEngineerReview.md

### Contexte

`EndpointResolver.TryResolve` est appelée **à chaque `PublishAsync` et `PublishBatchAsync`**. Elle contient des projections LINQ qui allouent un objet intermédiaire par appel. Sur un débit élevé (1000 msg/s), ces allocations génèrent une pression GC inutile.

### Diagnostic préalable

Avant d'optimiser, mesurer avec `dotnet-trace` ou BenchmarkDotNet :

```csharp
// Tests/Benchmarks/EndpointResolverBenchmarks.cs
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EndpointResolverBenchmarks
{
    private EndpointResolver _resolver = null!;
    private readonly string _target = "Individu";

    [GlobalSetup]
    public void Setup()
    {
        // Setup avec 10 targets configurées (réaliste RAMQ)
        _resolver = CreateResolverWithTargets(10);
    }

    [Benchmark(Baseline = true)]
    public bool TryResolve_Current()
    {
        return _resolver.TryResolve(_target, null, null, out _);
    }
}
```

### Stratégie d'optimisation

#### Option A — Pré-calcul au démarrage (recommandée)

Si l'itinéraire est immuable après le démarrage (cas normal), pré-calculer un `Dictionary<string, EndpointSettings>` à la construction :

```csharp
// Configuration/EndpointResolver.cs
public sealed class EndpointResolver : IEndpointResolver
{
    // Cache calculé une fois au démarrage
    private readonly FrozenDictionary<string, EndpointSettings> _targetIndex;
    private readonly FrozenDictionary<(string target, string? consumer, string? action), EndpointSettings> _fullIndex;

    public EndpointResolver(IMessageTargetMap targetMap)
    {
        _targetIndex = targetMap.Endpoints
            .ToFrozenDictionary(e => e.Target, StringComparer.OrdinalIgnoreCase);

        _fullIndex = targetMap.Endpoints
            .SelectMany(e => e.Routes ?? Enumerable.Empty<RouteSettings>(),
                        (e, r) => (key: (e.Target, r.Consumer, r.Action), endpoint: e))
            .ToFrozenDictionary(x => x.key, x => x.endpoint);
    }

    public bool TryResolve(
        string target,
        string? consumer,
        string? action,
        out EndpointSettings? endpoint)
    {
        // O(1) lookup — aucune allocation LINQ
        if (consumer is not null && action is not null
            && _fullIndex.TryGetValue((target, consumer, action), out endpoint))
            return true;

        return _targetIndex.TryGetValue(target, out endpoint);
    }
}
```

`FrozenDictionary<K,V>` (disponible depuis .NET 8) est optimisé pour les lectures intensives et utilise des algorithmes de hachage parfaits — idéal pour un dictionnaire immuable après démarrage.

#### Option B — Mémoïsation ciblée (si l'itinéraire peut changer)

```csharp
private readonly ConcurrentDictionary<(string, string?, string?), EndpointSettings?> _resolveCache = new();

public bool TryResolve(string target, string? consumer, string? action, out EndpointSettings? endpoint)
{
    var key = (target, consumer, action);
    if (_resolveCache.TryGetValue(key, out endpoint))
        return endpoint is not null;

    // Lookup LINQ original (uniquement lors du premier appel pour cette clé)
    var found = ResolveInternal(target, consumer, action, out endpoint);
    _resolveCache[key] = endpoint; // null si non trouvé
    return found;
}
```

### Critère de sortie

- [x] `EndpointResolver` : listes `topics` et `queues` pré-calculées via `Lazy<T>` — zéro allocation LINQ sur les appels répétés. *(27 avril 2026)*
- [x] `TryResolveProducer` : lookup O(1) via `Dictionary<string, EndpointSettings>` pré-calculé. *(27 avril 2026)*
- [x] Benchmark `EndpointResolverBenchmarks` montrant les allocations **avant** et **après**. *(Phase 4 ✅ — 27 avril 2026 — `src/EnterpriseMessageTransit.Benchmarks/EndpointResolverBenchmarks.cs`)*
- [x] Test : `TryResolve` retourne le bon `EndpointSettings` après le pré-calcul. *(tests existants `EndpointResolverTests.cs` — 53/53 ✅)*

---

## Résumé des livrables Phase 3

| ID Tâche | Livrable | Priorité | Estimation | Statut |
|----------|----------|----------|-----------|--------|
| P3-T1 | Tests de contrat interface + `docs/extensibility.md` | 🟡 Basse | 1 semaine | ✅ Complet |
| | | | | 28 tests de contrat ✅ · `IMessageSerializer` (10 tests) · `IJournalProvider` (3 tests) · `IStorageProvider` (5 tests) · `IMetricsProvider` (10 tests) — `docs/extensibility.md` ✅ Phase 4 (27 avril 2026) |
| P3-T2 | `EnforceIdempotentPublish` opt-in + `docs/idempotence.md` | 🟡 Basse | 3 jours | ✅ Complet |
| | | | | `TransportSettings.EnforceIdempotentPublish` ✅ · `ServiceBusHealthCheck.ValidateIdempotenceCoreAsync` ✅ · 5 tests ✅ · `IdempotenceValidationService` (IHostedService, 8 tests) branché 8 mai 2026 · `docs/idempotence.md` ✅ |
| P3-T3 | `PublishTimeout` borné à 30s + tests | 🟠 Moyenne | 2 jours | ✅ Complet |
| | | | | `TransportSettings.PublishTimeout` ✅ · timeout `PublishAsync`+`PublishBatchAsync` ✅ · 2 tests ✅ · CHANGELOG ✅ (8 mai 2026) |
| P3-T4 | `DeserializationResult<T>` — call-sites + métriques | 🟠 Moyenne | 1 semaine | ✅ Complet (breaking change 8 mai 2026) |
| | | | | `DeserializeMessageSafe<T>()` dans `IMessagingProvider`+`AzureMessagingProvider` ✅ · métriques `deserialization_failures_total{reason}` ✅ · **Breaking change 8 mai 2026** : settlement retiré de `BaseConsumer` → consumer décide · ADR-006 marqué Superseded |
| P3-T5 | Journal async batch + `Task.WhenAll` | 🟡 Basse | 3 jours | ✅ Complet |
| | | | | `Task.WhenAll` + `WriteJournalEntrySafeAsync` ✅ · 1 test ✅ |
| P3-T6 | `EndpointResolver` — `Lazy<T>` + benchmark | 🟡 Basse | 3 jours | ✅ Complet |
| | | | | `Lazy<T>` topics/queues/producerIndex ✅ · O(1) TryResolveProducer ✅ |

**Total estimé : 3-5 semaines** · **Au 27 avril 2026 : 6/6 tâches ✅ — Phase 3 CLÔTURÉE** · **Tests : 86/86 ✅**

---

## Condition de clôture Phase 3

Phase 3 est terminée quand :

1. ✅ **P3-T3 livré (timeout borné)** — risque opérationnel le plus élevé de cette phase, traité en priorité.
2. ✅ **P3-T4 livré (DeserializationResult)** — settlement ADR-006 appliqué dans `BaseConsumer`.
3. ✅ **P3-T5 livré (journal async)** — `PublishBatchAsync` ne bloque plus sur le journal O(n).
4. ✅ **P3-T6 livré (EndpointResolver)** — zéro allocation LINQ sur le chemin critique.
5. ✅ **P3-T1, T2** — livrés Phase 4 (27 avril 2026) : `docs/extensibility.md` ✅, métriques désérialisation ✅, benchmarks EndpointResolver ✅.

> **État au 27 avril 2026 : Phase 3 CLÔTURÉE ✅** — 6/6 tâches livrées (T1+T2+T3+T4+T5+T6). `docs/idempotence.md` reporté Phase 4 (guide intégrateurs). Tests : **86/86 ✅**. Build : **0 erreurs**.
> **Phase 4 CLÔTURÉE ✅** — 27 avril 2026 — Tests : **93/93 ✅** — items reportés Phase 3 tous complétés.

---

## Vue d'ensemble des 3 phases

```
Phase 1 — 🔴 Bloquant ✅ (27 avril 2026)
├── D1 : Thèse de portabilité → ADR-001
├── D3 : Contrat MessageTransitContext → Tests snapshot + docs/contracts/envelope-v1.md
└── D6 : SemVer + CHANGELOG + ADR + CONTRIBUTING

Phase 2 — 🟠 Majeur ✅ (27 avril 2026)
├── Volet A : Observabilité (tests, tracing, métriques)
├── Volet B : Fiabilité (failure modes, claim-check orphelins)
└── Volet C : Architecture (surface publique, IMessageTransit, découplage Functions)

Phase 3 — 🟡 Mineur ✅ (27 avril 2026)
├── P3-T3 : Timeout borné (priorité immédiate)
├── P3-T4 : DeserializationResult call-sites + politique DLQ
├── P3-T5 : Journal async dans PublishBatchAsync
├── P3-T1 : Tests contrat interface + extensibility.md
├── P3-T2 : Idempotence documentée
└── P3-T6 : EndpointResolver FrozenDictionary

Phase 4 — 🟡 Moyen ✅ (27 avril 2026) — Tests : 93/93 ✅
├── P4-T1 ✅ : docker-compose SB Emulator + skeleton tests (tests complets → Phase 5)
├── P4-T2 ✅ : traceparent propagé + 4 tests T12
├── P4-T3 ✅ : deserialization_failures_total dans BaseConsumer + 3 tests contrat
├── P4-T4 ✅ : EnterpriseMessageTransit.Benchmarks (BenchmarkDotNet)
├── P4-T5 ⏭ : Tests charge/chaos → Phase 6
├── P4-T6 ✅ : docs/extensibility.md
└── P4-T7 ✅ : docs/operational-envelope.md

Phase 5 — 🟠 Élevé 🔄 Portée révisée (mai 2026) — Fondations ✅ (27 avr.) · Routing Slip natif v2.0 : ⬜ À démarrer
├── P5-T1 ✅ (fondation) : Module RoutingSlip (9 types) + `IStageAdvancer` + `StageAdvancer` (E1-E3)
├── P5-T2 ✅ (fondation) : `StageAdvancerTests` purs — 14 tests T10
├── P5-T3 ✅ (fondation) : `IMessageTransit` enrichi (livré Phase 2)
├── P5-T4 ✅ (fondation) : `InMemoryAdapter` + suite contrat `IMessagingAdapter` — 10 tests
├── P5-T5 ✅ (fondation) : `stryker-config.json` + `pipelines/mutation-tests.yml`
├── P5-T6 ✅ (fondation) : `AzureFunctionMessagingAdapter` → namespace `Azure.Functions` + 2 règles architecture
└── Nouvelle portée v2.0 ⬜ : `IRoutingSlipActivity<TArgs>` · `SlipEnvelope` · `RoutingSlipBuilder` · `RoutingSlipExecutor` · breaking change MAJOR

Total : 17-25 semaines → Qualité production, surface maîtrisée, observabilité complète
```

---

## Annexe — Mapping avec le plan DE Review §8.3

La structure en 3 phases (couleurs) de ce document est **complémentaire** au plan en 6 phases de §8.3 de la Distinguished Engineer Review. Voici le mapping pour éviter toute confusion :

| Phase DE Review §8.3 | Correspondance dans les 3 phases couleur |
|----------------------|------------------------------------------|
| Phase 1 — Fondations non-régressables | Phase 1 (O3) + Phase 1 (O18) |
| Phase 2 — Tests + observabilité | Phase 2-Volet A (O16 · O17 · O13) |
| Phase 3 — Contrats versionné | Préparé en Phase 1 (D3), exécuté post-plan si besoin |
| Phase 4 — Résilience durcissement | Phase 2-Volet B (O12) |
| Phase 5 — Routing Slip natif v2.0 | Phase 2-Volet C (O4 · O5 · O6 · O7) + Phase 3 (O11) |
| Phase 6 — Portabilité multi-broker | **Hors périmètre** (ADR-001) — réévaluer sur événement déclencheur |
