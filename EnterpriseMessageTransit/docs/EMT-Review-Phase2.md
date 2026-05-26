# EMT — Plan d'exécution Phase 2 : Durcissement architectural et opérationnel

> **Couleur de référence :** 🟠 Majeur
> **Source :** [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md) — Points ouverts O2 · O4 · O5 · O6 · O7 · O12 · O13 · O16 · O17 · O19 · O20
> **Date de planification :** 27 avril 2026
> **Date d'implémentation :** 27 avril 2026
> **Durée indicative :** 10-14 semaines (parallélisable en 3 volets)
> **Risque / Complexité :** 🟠 Élevé — touche les chemins d'erreur en production, les abstractions publiques et les fondations de test
> **Prérequis :** Phase 1 complétée (D1 décidée, tests de contrat actifs, gouvernance en place)
> **Statut :** 🟠 En cours — 27 avril 2026
> **Complètes :** P2-A3 ✅ · P2-A2 ✅ · P2-A1 ✅ · P2-B1 ✅ · P2-C1 ✅ · P2-C2 ✅ · P2-C3 ✅ · P2-C4 ✅ · P2-C5 ✅
> **Partielles :** P2-A1 🟡 (tests ✅, SB Emulator ❌, Azure DevOps à lier)
> **ADRs :** ADR-002 ✅ · ADR-006 ✅ · ADR-007 ✅ · ADR-008 ✅

---

## Structure de la phase

Les 11 points majeurs se regroupent naturellement en **3 volets parallélisables**, portés par des sous-équipes distinctes :

| Volet | Thème | Points ouverts | Risque opérationnel |
|-------|-------|----------------|---------------------|
| **Volet A — Observabilité** | Tests, tracing, métriques, catalogue ops | O16 · O17 · O13 | 🟡 Moyen — aucune modification de comportement runtime |
| **Volet B — Fiabilité** | Défaillances, désérialisation, DLQ, claim-check | O12 | 🟠 Élevé — policy decisions impactant la production |
| **Volet C — Architecture** | Surface publique, saga, couplage, IMessageTransit | O2 · O4 · O5 · O6 · O7 · O19 · O20 | 🟠 Élevé — touche les abstractions consommées par les équipes applicatives |

> 💡 **Pour les développeurs juniors.** La parallélisation est possible parce que les 3 volets touchent des couches différentes du code : les volets A et B modifient peu de code existant (essentiellement ajouts), tandis que le volet C demande des décisions de design. L'ordre recommandé : démarrer A et B simultanément, démarrer C après confirmation que ADR-001 (périmètre multi-hôte) est signé.

---

## Volet A — Observabilité et qualité

### P2-A1 — Projet de tests et stratégie CI (O16)

**Origine :** §6.1 EMT-DistinguishedEngineerReview.md

#### Contexte

Aucun projet de test n'existe dans la solution. Les revues Senior et Lead notent « Tests unitaires : À exécuter » — cela signifie soit qu'ils n'existent pas, soit qu'ils ne tournent pas en CI. Sans filet de sécurité automatisé, aucun refactor de Phase 5/6 n'est exécutable en confiance.

#### Plan d'exécution

**Étape A — Créer le projet de tests**

```
EnterpriseMessageTransit.sln
├── EnterpriseMessageTransit/          (code actuel)
└── EnterpriseMessageTransit.Tests/    (nouveau)
    ├── Unit/
    │   ├── Configuration/
    │   │   ├── EndpointResolverTests.cs
    │   │   └── ExponentialRetryPolicyTests.cs
    │   ├── Messaging/
    │   │   ├── ProducerTests.cs
    │   │   ├── BaseConsumerTests.cs
    │   │   └── RoutingSlip/
    │   │       └── SagaAdvancementTests.cs
    │   └── Serialization/
    │       └── JsonMessageSerializerTests.cs
    ├── Integration/
    │   ├── ServiceBus/
    │   │   └── ServiceBusEmulatorTests.cs    (Azure SB Emulator)
    │   └── Storage/
    │       └── ClaimCheckTests.cs             (Azurite)
    ├── Contracts/
    │   └── MessageTransitContextSerializationTests.cs
    └── Architecture/
        └── LayeringTests.cs                   (NetArchTest)
```

**Étape B — Tests unitaires prioritaires (bloquants CI à court terme)**

Suite minimale à mettre en place avant tout autre refactor :

```csharp
// Unit/Configuration/EndpointResolverTests.cs
public class EndpointResolverTests
{
    [Theory]
    [InlineData("Individu", null, null)]         // résolution par target uniquement
    [InlineData("Individu", "ValiderAdresse", null)]  // target + consumer
    [InlineData("Individu", "ValiderAdresse", "Notifier")] // target + consumer + action
    public void TryResolve_RetourneEndpointCorrect(string target, string? consumer, string? action)
    { /* ... */ }

    [Fact]
    public void ValidateDuplicateTargets_LèveException_SiDoublon()
    { /* ... */ }
}

// Unit/Messaging/RoutingSlip/SagaAdvancementTests.cs
public class SagaAdvancementTests
{
    [Fact]
    public void AvancerStage_RetourneStageSuivant_QuandPasLeDernier()
    { /* ... */ }

    [Fact]
    public void AvancerStage_SignaleTerminaison_AuDernierStage()
    { /* ... */ }

    [Fact]
    public void AvancerStage_PreserveVariables_EntreLesStages()
    { /* ... */ }

    [Fact]
    public void Replay_EstIdempotent_QuandFinalStageCompleted()
    { /* Teste le flag __FinalStageCompleted */ }
}
```

**Étape C — Tests d'intégration Service Bus Emulator**

Le Service Bus Emulator (officiel Microsoft, disponible depuis 2024) permet des tests d'intégration locaux :

```yaml
# docker-compose.test.yml
services:
  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    ports: ["5672:5672", "5300:5300"]
    environment:
      CONFIG_PATH: /ServiceBus_Emulator/ConfigFiles/Config.json
    volumes:
      - ./tests/servicebus-config.json:/ServiceBus_Emulator/ConfigFiles/Config.json

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    ports: ["10000:10000", "10001:10001", "10002:10002"]
```

```csharp
// Integration/ServiceBus/ServiceBusEmulatorTests.cs
[Collection("ServiceBusEmulator")]
public class ServiceBusEmulatorTests : IAsyncLifetime
{
    [Fact]
    public async Task PublishAsync_MessageReçu_AvecMessageIdFourni()
    {
        // Vérifier que MessageId fourni par le caller est préservé
        var messageId = "test-idempotence-001";
        context.MessageId = messageId;

        await producer.PublishAsync(context, cancellationToken);

        var received = await receiver.ReceiveMessageAsync();
        Assert.Equal(messageId, received.MessageId);
    }

    [Fact]
    public async Task PublishBatchAsync_TousMessagesReçus_DansLOrdre()
    { /* ... */ }

    [Fact]
    public async Task ExponentialRetry_DLQ_ApresMaxDeliveryCount()
    { /* ... */ }
}
```

**Étape D — Tests d'architecture (fitness functions)**

```csharp
// Architecture/LayeringTests.cs
public class LayeringTests
{
    [Fact]
    public void ConfigurationNamespace_NeDependPas_DeProviderAzure()
    {
        var result = Types.InAssembly(typeof(EndpointResolver).Assembly)
            .That().ResideInNamespace("RAMQ.COM.EnterpriseMessageTransit.Configuration")
            .Should().NotHaveDependencyOn("RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ServiceBusSenderCache_EstInternal()
    {
        typeof(ServiceBusSenderCache).IsPublic.Should().BeFalse();
    }
}
```

#### Critère de sortie

- [x] Projet `EnterpriseMessageTransit.Tests` ajouté à la solution. *(44 tests unitaires verts)*
- [x] `dotnet test --filter "Category=Unitaire"` en CI bloquante. *(`pipelines/ci-tests.yml` livré — 27 avril 2026 · 49/49 ✅ · à lier dans Azure DevOps)*
- [ ] Service Bus Emulator + Azurite dans CI (docker-compose ou Testcontainers).
- [x] Tests d'architecture `NetArchTest` actifs sur les dépendances inter-namespace. *(Architecture/LayeringTests.cs — 5 règles)*

---

### P2-A2 — Distributed tracing ActivitySource (O17)

**Origine :** §6.2 EMT-DistinguishedEngineerReview.md

#### Contexte

Aucune occurrence d'`ActivitySource` ou `StartActivity` dans le code actuel. Le SDK Azure Service Bus instrumente déjà des `Activity` sur send/receive : EMT **casse la chaîne** en n'attachant pas ses propres spans. Sans traces, un incident cross-services (5 stages de saga) nécessite la corrélation manuelle de 5 fichiers de log.

#### Plan d'exécution

**Étape A — Exposer le `ActivitySource` statique**

```csharp
// Messaging/Telemetry/MessagingActivitySource.cs
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Telemetry;

internal static class MessagingActivitySource
{
    private const string SourceName = "RAMQ.COM.EnterpriseMessageTransit";

    internal static readonly ActivitySource Source = new(SourceName, "1.0.0");

    // Exposé pour intégration avec OpenTelemetry MeterProvider côté hôte
    public static string Name => SourceName;
}
```

**Étape B — Instrumenter les 10 points clés**

| Point d'instrumentation | Span name | Attributs sémantiques |
|------------------------|-----------|----------------------|
| `Producer.PublishAsync` | `messaging.publish` | `messaging.system=servicebus`, `messaging.destination=<entity>`, `messaging.message_id=<id>` |
| `AzureMessagingProvider.SendAsync` | `messaging.send` | `messaging.destination`, `messaging.message_id`, `messaging.session_id` |
| `AzureMessagingProvider.SendBatchAsync` | `messaging.send.batch` | `messaging.destination`, `messaging.batch.message_count` |
| `PrepareClaimCheckAsync` — upload | `messaging.claimcheck.upload` | `storage.container`, `storage.blob`, `messaging.claimcheck.size_bytes` |
| `BaseConsumer.DeserializeMessageAsync` | `messaging.deserialize` | `messaging.message_id`, `messaging.destination` |
| `BaseConsumer.ConsumeAsync` dispatch | `messaging.consume` | `messaging.destination`, `messaging.consumer`, `messaging.action` |
| `BaseConsumer.RouteToNextStageAsync` | `messaging.saga.advance` | `saga.current_stage`, `saga.next_stage`, `saga.is_final` |
| `RetryPolicyHandler.HandleImmediateRetryAsync` | `messaging.retry.immediate` | `messaging.message_id`, `messaging.delivery_count` |
| `RetryPolicyHandler.HandleExponentialRetryAsync` | `messaging.retry.exponential` | `messaging.message_id`, `messaging.retry_delay_ms` |
| `RetryPolicyHandler.HandleDeadLetterAsync` | `messaging.dlq` | `messaging.message_id`, `messaging.dlq_reason` |

Exemple d'instrumentation dans `Producer.cs` :

```csharp
private async Task<MessageTransitContext<MessageTransitResponse>> PublishCoreAsync(
    MessageTransitContext<TMessage> context,
    Dictionary<string, object>? properties,
    ClaimCheckOptions? claimCheckOptions,
    CancellationToken cancellationToken)
{
    using var activity = MessagingActivitySource.Source.StartActivity(
        "messaging.publish",
        ActivityKind.Producer);

    activity?.SetTag("messaging.system",      "servicebus");
    activity?.SetTag("messaging.destination", resolvedTarget);
    activity?.SetTag("messaging.message_id",  context.MessageId);
    activity?.SetTag("messaging.session_id",  context.SessionId);

    try
    {
        await _messagingProvider.SendAsync(context, options, cancellationToken);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return MapToResponseContext(context, null);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

**Étape C — Propagation `traceparent` dans les `ApplicationProperties` Service Bus**

```csharp
// Dans AzureMessagingProvider.BuildServiceBusMessage(...)
if (Activity.Current is { } activity)
{
    var message = new ServiceBusMessage(body);
    message.ApplicationProperties["traceparent"] = activity.Id;
    message.ApplicationProperties["tracestate"]  = activity.TraceStateString ?? string.Empty;
}
```

**Étape D — Documentation**

Créer `docs/observability/tracing.md` :
- Liste des `ActivitySource` noms et attributs sémantiques.
- Configuration OpenTelemetry recommandée côté hôte (Azure Functions / BackgroundService).
- Exemple de requête Application Insights pour corréler un saga end-to-end.

#### Critère de sortie

- [x] `ActivitySource "RAMQ.COM.EnterpriseMessageTransit"` instrumentant les 10 points clés. *(9/10 span livrés : `messaging.publish` · `messaging.send` · `messaging.send.batch` · `messaging.claimcheck.upload` · `messaging.deserialize` · `messaging.saga.advance` · `messaging.retry.immediate` · `messaging.retry.exponential` · `messaging.dlq` — `messaging.consume` à implémenter dans classes concrètes)*
- [ ] `traceparent` propagé dans `ApplicationProperties` à la publication. *(Phase 3)*
- [ ] Tests T12 (§6.1.1 DE Review) : un publish émet ≥ 3 spans corrélés.
- [x] `docs/observability/tracing.md` documentant les attributs et la configuration hôte.

---

### P2-A3 — Catalogue de métriques et enveloppe opérationnelle (O13)

**Origine :** §5.1 + §6.2 EMT-DistinguishedEngineerReview.md

#### Métriques manquantes à ajouter dans `MetricsProvider`

| Métrique | Type | Labels | Utilisation opérationnelle |
|----------|------|--------|---------------------------|
| `circuit_state` | Gauge | `entity` | Dashboard temps-réel état du circuit breaker |
| `circuit_transitions_total` | Counter | `entity`, `from`, `to` | Alerter sur fréquence des transitions |
| `deserialization_failures_total` | Counter | `reason` | Détecter dégradation silencieuse |
| `claim_check_upload_duration_ms` | Histogram | `entity` | SLO upload Blob |
| `claim_check_download_duration_ms` | Histogram | `entity` | SLO download Blob |
| `journal_write_duration_ms` | Histogram | — | Impact performance journal A5 |
| `saga_stage_advance_total` | Counter | `from`, `to` | Détecter blocages dans le saga |
| `duplicate_detected_total` | Counter | `entity` | Alerter sur producteurs en boucle |

#### Livrable : `docs/observability/metrics.md`

Structure du catalogue documenté :
- Nom, type, unité, labels disponibles.
- Seuils d'alerte recommandés (ex. `deserialization_failures_total rate > 1/min → alerte P2`).
- Exemple de requête KQL Azure Monitor par métrique.
- Lien vers dashboard Azure Monitor Workbook (fichier JSON exportable à livrer).

#### Critère de sortie

- [x] 8 métriques manquantes ajoutées dans `MetricsProvider`. *(interface + implémentation complètes)*
- [x] `docs/observability/metrics.md` publié avec seuils d'alerte et requêtes KQL.
- [ ] `docs/operational-envelope.md` avec les premières valeurs indicatives (à affiner en Phase 4).

---

## Volet B — Fiabilité et modèle de défaillance

### P2-B1 — Rendre le modèle de défaillance explicite (O12)

**Origine :** §4.1 EMT-DistinguishedEngineerReview.md

#### Contexte

Les mécanismes de gestion d'erreur sont présents dans le code (`CircuitBreakerManager`, `DeserializationResult<T>`, `RetryPolicyHandler`, pattern A5). Ce qui manque : la **documentation opérationnelle** permettant à un SRE de les utiliser à 3h du matin.

#### Plan d'exécution

**Étape A — `docs/failure-modes.md` (runbook des modes de défaillance)**

Structure attendue (1 section par mode) :

```markdown
# Modes de défaillance — EnterpriseMessageTransit

## 1. Circuit ouvert (CircuitBreakerOpenException)

**Symptôme observable :**
- Log : `Circuit breaker OPEN for entity '{entity}'. Rejecting send.`
- Métrique : `circuit_state{entity}=OPEN`
- Alerte : `circuit_transitions_total{entity,from=Closed,to=Open} rate > 0`

**Cause probable :** Indisponibilité temporaire de Service Bus sur l'entité '{entity}'
(erreurs réseau, throttling, quota dépassé).

**Conséquence métier :** Messages refusés côté producteur. Pas de perte de données si le
caller gère correctement le rejet (doit retry à un niveau supérieur, ex. Azure Functions re-déclenche l'invocation).

**Action corrective :**
1. Vérifier l'état du namespace Service Bus dans le portail Azure.
2. Identifier si c'est du throttling (voir métrique `send_throttled_total`).
3. Attendre le Half-Open automatique (durée configurable, défaut : 30 s).
4. Si persistant : basculer sur l'entité de secours (si configurée).

## 2. Désérialisation échouée (DeserializationResult.Malformed)

**Symptôme observable :**
- Log : `Deserialization failed for type {TypeName}: {Message}`
- Métrique : `deserialization_failures_total{reason=Malformed}`

**Cause probable :** Message produit par une version incompatible, ou corruption en transit.

**Conséquence métier :** Message envoyé en DLQ (politique Malformed → DLQ, cf. ADR-006).

**Action corrective :**
1. Inspecter le message en DLQ via Service Bus Explorer.
2. Identifier la version d'EMT qui a produit le message (champ `SchemaVersion` dans l'enveloppe, disponible à partir de la Phase 3).
3. Si régression EMT : rollback de la version. Si data corruption : analyse forensic.
...
```

Modes à documenter : CircuitOpen, DeserializationMalformed, DeserializationTooLarge, DeserializationEmpty, MaxDeliveryCountExceeded, ClaimCheckOrphan, BlobDownload503, JournalUnavailable, TimeoutGlobal.

**Étape B — Matrice de décision exception→action**

```markdown
## Matrice : quelle exception lever côté consumer applicatif ?

| Situation | Exception à lever | Comportement EMT |
|-----------|------------------|-----------------|
| Erreur **transiente** (base de données temporairement indisponible, timeout réseau) | `ImmediateRetryException` | Abandon immédiat du message, relivré par SB selon `lockDuration`. Compteur `DeliveryCount` incrémenté. |
| Erreur **business récupérable** nécessitant un délai (ex. fenêtre de traitement non ouverte) | `ExponentialRetryException` | Planification différée : abandon avec délai exponentiel (session) ou `ScheduleMessage` (non-session). |
| Erreur **irrécupérable** (violation de règle métier, message invalide structurellement) | `ImmediateDLQException` | Dead-lettering immédiat avec raison, sans consommer les retries. |
| Exception non-catchée (bug non anticipé) | *(laisser remonter)* | EMT intercepte et dead-letter après `MaxDeliveryCount`. |

**Règle d'or :** utiliser `ImmediateRetryException` SEULEMENT quand on est confiant que le problème
se résoudra par lui-même à court terme. Ne jamais l'utiliser pour un problème de validation —
sinon le message consomme tous ses `MaxDeliveryCount` inutilement.
```

**Étape C — Nettoyage des claim-checks orphelins**

```csharp
// Compensation explicite dans Producer.cs si l'envoi SB échoue après upload Blob
try
{
    await _storageProvider.UploadAsync(blobName, stream, cancellationToken);
    await _messagingProvider.SendAsync(context, options, cancellationToken);
}
catch (Exception sendEx)
{
    // Compensation : supprimer le blob uploadé si l'envoi SB échoue
    try
    {
        await _storageProvider.DeleteAsync(blobName, CancellationToken.None);
        Logger.LogWarning("Claim-check blob '{BlobName}' deleted after send failure.", blobName);
    }
    catch (Exception cleanupEx)
    {
        // Log l'orphelin pour cleanup externe (metric + lifecycle policy Blob)
        Logger.LogError(cleanupEx, "Claim-check orphan: blob '{BlobName}' could not be cleaned up.", blobName);
        _metrics.IncrementMessagesDLQ(resolvedTarget ?? "unknown", "ClaimCheckOrphan");
    }
    throw new MessageSendException($"Send failed for target='{resolvedTarget}': {sendEx.Message}", sendEx);
}
```

Ajouter une **lifecycle policy Azure Blob** (Bicep) pour supprimer automatiquement les claim-checks après 30 jours (durée de rétention opérationnelle — à aligner avec la politique CAI RAMQ) :

```bicep
resource claimCheckLifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    policy: {
      rules: [
        {
          name: 'ClaimCheckRetention'
          enabled: true
          type: 'Lifecycle'
          definition: {
            actions: { baseBlob: { delete: { daysAfterModificationGreaterThan: 30 } } }
            filters: { blobTypes: ['blockBlob'], prefixMatch: ['claim-checks/'] }
          }
        }
      ]
    }
  }
}
```

#### Critère de sortie

- [x] `docs/failure-modes.md` documentant 9+ modes de défaillance avec runbook.
- [x] Matrice exception→action publiée dans `docs/failure-modes.md`.
- [x] Référence depuis `CONTRIBUTING.md` ajoutée. *(`CONTRIBUTING.md` §9 — `docs/failure-modes.md` + règle contribution)*
- [x] Compensation claim-check orphelin implémentée dans le code. *(P2-B1 — `Producer.PublishCoreAsync` et `PublishBatchAsync` : `DeleteAsync` best-effort si `IsClaimCheckApplied` et envoi échoue)*
- [x] Tests de compensation : `ProducerClaimCheckCompensationTests.cs` — 3 scénarios verts.
- [x] ADR-006 signé : politique DLQ par `DeserializationFailureReason` (Malformed→DLQ, Empty→Drop, TooLarge→DLQ+alerte). *(`docs/adr/ADR-006-politique-dlq-deserialisation.md` — 27 avril 2026)*
- [ ] Lifecycle policy Blob fournie en Bicep dans `docs/infra/`.

---

## Volet C — Architecture et surface publique

### P2-C1 — Réduire la surface publique (O7)

**Origine :** §2.1 EMT-DistinguishedEngineerReview.md

#### Contexte

Plus de 80 types `public` dans l'assembly, alors que l'API réellement destinée aux applications clientes tient en 6 types : `IMessageProducer<T>`, `IMessageConsumer<T>`, `MessageTransitContext<T>`, `PublishOptions`, `ClaimCheckOptions`, et les extensions DI.

#### Plan d'exécution

**Étape A — Inventaire et classification**

```csharp
// Classification attendue après refactoring
// PUBLIC (contrat stable destiné aux applications)
public interface IMessageProducer<T>
public interface IMessageConsumer<T>
public class MessageTransitContext<T>
public record PublishOptions
public record ClaimCheckOptions
public record RequestReplyOptions
public class BaseConsumer<T>          // discutable — cf. note
public class BaseMessageTransit<T>   // discutable — cf. note
public static class ConfigurerProviders (méthodes AddProducer/AddConsumer/ConfigureAzureProviders)
public record JournalEntry            // retrait à évaluer (utilisé par les équipes de logging)
public enum OperationMode
public enum MessagingEntityType
[Exceptions publiques]               // toutes — interface applicative

// INTERNAL (implémentation — à basculer)
internal class ServiceBusSenderCache
internal class RetryPolicyHandler
internal class AzureFunctionMessagingAdapter
internal class AzureFunctionMessageTransit
internal class CircuitBreakerManager
internal class EndpointResolver
internal class MessageTargetMap
internal class AzureMessagingProperties
internal interface IRetryPolicyHandler     // débat : interface vs internal
internal interface IMessagingAdapter
internal interface IMessagingProvider
internal interface IStorageProvider        // maintenir public si extensibilité voulue (ADR-002 §2.2)
internal interface IJournalProvider        // idem
```

**Note sur `BaseConsumer<T>` :** si des applications héritent de cette classe publiquement, la rendre `internal` est un breaking change MAJOR. Deux options :
- La garder `public sealed` (non-extensible) et créer `AdvancingConsumer<T>` pour la saga (Phase 5).
- La marquer `public abstract` avec `[Obsolete]` et introduire un type de remplacement.

**Étape B — Ajouter `[assembly: InternalsVisibleTo]`**

```csharp
// AssemblyInfo.cs ou dans le .csproj
[assembly: InternalsVisibleTo("RAMQ.COM.EnterpriseMessageTransit.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // pour NSubstitute
```

**Étape C — Activer `Microsoft.CodeAnalysis.PublicApiAnalyzers` (déjà initié en Phase 1)**

Vérifier que `PublicAPI.Shipped.txt` reflète la surface *post-refactoring* et non l'état initial.

#### Critère de sortie

- [x] Inventaire complet des types publics avec classification (`PUBLIC` / `INTERNAL` / `DÉBAT`). *(ADR-007 enrichi — 68 types classifiés par catégorie : Contrat client, Configuration, Infrastructure DI, Extension, Données, Exception — 27 avril 2026)*
- [x] `[assembly: InternalsVisibleTo("RAMQ.COM.EnterpriseMessageTransit.Tests")]` et `DynamicProxyGenAssembly2` ajoutés.
- [x] Candidats évidents basculés en `internal` : `ServiceBusSenderCache`, `RetryPolicyHandler`, `AzureFunctionMessagingAdapter`, `CircuitBreakerManager`, `AzureMessagingProvider`. *(AzureFunctionMessageTransit, EndpointResolver, MessageTargetMap, AzureMessagingProperties — à évaluer)*
- [ ] CI ne montre aucun nouveau type `public` non-approuvé (analyzers bloquants).
- [x] ADR-007 documentant les choix de frontière public/internal. *(`docs/adr/ADR-007-frontiere-public-internal.md`)*

---

### P2-C2 — Positionnement vs écosystème — ADR-002 (O2)

**Origine :** §1.3.1 EMT-DistinguishedEngineerReview.md

#### Contexte

La décision d'écarter MassTransit n'est **pas documentée** dans le dépôt. Chaque nouvel arrivant re-pose la question. Sans ADR, le risque est de réexpliquer oralement les mêmes décisions tous les 18 mois.

#### Livrable

Compléter **ADR-002** (initié en Phase 1) avec les 3 limites de MassTransit et les conditions de révision :

```markdown
# ADR-002 — Pourquoi EMT et pas MassTransit

## Contexte
MassTransit est le framework .NET de référence pour le messaging orienté messages.
La décision de construire EMT en interne plutôt que d'adopter MassTransit a été
prise au démarrage du projet mais n'a pas été documentée.

## Limites de MassTransit pour RAMQ (valides au 2026-04-27)

### L1 — Absence de support Azure Functions
MassTransit cible IHostedService/BackgroundService et ne supporte pas le modèle
de trigger Azure Functions. Or la majorité des charges RAMQ tournent sur Functions
(scale-to-zero, facturation à l'usage). Choisir MassTransit imposait de migrer vers
des conteneurs — décision non retenue à l'époque.

### L2 — Granularité de sécurité RBAC incompatible
MassTransit attend des droits "Manage" au niveau namespace Service Bus pour créer
les entités au démarrage. RAMQ applique une politique "least privilege" : une identité
managée ne peut que Send/Listen sur une entité spécifique. Ces droits sont incompatibles.

### L3 — Roadmap OSS peu claire, dépendance à gouvernance externe
Librairie plateforme interne supportée sur 5-10 ans. MassTransit a changé plusieurs
fois de modèle de financement (v7→v8→v9). Dépendance OSS à gouvernance volatile
incompatible avec les exigences de support long terme RAMQ.

## Conditions de révision de cette décision
- Si Azure Functions est abandonné comme hôte principal RAMQ.
- Si MassTransit publie un support Azure Functions first-class (surveiller v10).
- Si RAMQ assouplit sa politique RBAC Service Bus.
- Si le coût de maintenance interne EMT dépasse le coût d'adoption MassTransit.
```

#### Critère de sortie

- [x] ADR-002 complété et approuvé avec les 3 limites et conditions de révision. *(`docs/adr/ADR-002-pourquoi-emt-pas-masstransit.md` — L1/L2/L3/L4 + 4 conditions de révision datées)*
- [x] Référencé depuis `CONTRIBUTING.md` §8 (tableau ADRs existants). *(27 avril 2026)*

---

### P2-C3 — Stratégie NuGet, publication, dépréciation (O19 · O20)

**Origine :** §7.2 + §7.3 EMT-DistinguishedEngineerReview.md

#### Livrable A — `docs/consumption.md`

```markdown
# Guide de consommation — RAMQ.COM.EnterpriseMessageTransit

## Feed NuGet
URL : [feed privé Azure DevOps Artifacts — à renseigner]
Authentification : feed credentials (credential provider NuGet)

## Politique de support (N / N-1)
- Version N (dernière stable) : support complet.
- Version N-1 : correctifs de sécurité uniquement.
- Versions antérieures : EOL — mettre à jour.

## Cadence de release
- PATCH : selon besoin (corrections de bugs)
- MINOR : mensuelle (fonctionnalités additives)
- MAJOR : semestrielle (breaking changes annoncés 1 version à l'avance via [Obsolete])

## Procédure d'upgrade
1. Lire le CHANGELOG.md de la version cible.
2. Vérifier les [Obsolete] avec `error: true` — ils signalent des suppressions imminentes.
3. Mettre à jour la référence NuGet.
4. Exécuter les tests d'intégration.

## Contact équipe propriétaire
[À renseigner — canal Teams + PO product]
```

#### Livrable B — Convention de dépréciation (dans `CONTRIBUTING.md`)

```csharp
// Étape 1 — Avertissement (version N)
[Obsolete("Utiliser DeserializeMessageAsync<T>() à la place. Sera retiré en v1.0.", error: false)]
public TMessage? DeserializeMessage<TMessage>() where TMessage : class { ... }

// Étape 2 — Erreur (version N+1)
[Obsolete("Utiliser DeserializeMessageAsync<T>() à la place. Retiré depuis v0.10.", error: true)]
public TMessage? DeserializeMessage<TMessage>() where TMessage : class
    => throw new NotSupportedException("Cette méthode a été supprimée. Utiliser DeserializeMessageAsync<T>.");

// Étape 3 — Suppression (version N+2) + entrée CHANGELOG.md ### Supprimé
```

Appliquer immédiatement sur les éléments déjà marqués `[Obsolete]` :
- `DeserializeMessage` / `TryDeserializeMessage` (revue Lead, Phase précédente)
- `ServiceBusEntityType` enum (noté par revue Senior)

#### Critère de sortie

- [x] `docs/consumption.md` avec feed NuGet, politique support et procédure upgrade. *(`docs/consumption.md` livré — 27 avril 2026)*
- [x] Convention de dépréciation en 3 étapes documentée dans `CONTRIBUTING.md`. *(§3 enrichi avec tableau + exemple de code)*
- [x] `[Obsolete]` existants **supprimés** : `DeserializeMessage<T>()` et `TryDeserializeMessage<T>()` retirées — tous les call-sites migrés vers `DeserializeMessageAsync`. *(15 fichiers exemples + `IMessageConsumer` mis à jour)*
- [ ] `ServiceBusEntityType` passé à `error: true` si inutilisé depuis ≥ 1 version.

---

### P2-C4 — `IMessageTransit` enrichi (O6)

**Origine :** §3.4 EMT-DistinguishedEngineerReview.md

#### Contexte

`IMessageTransit` expose `MessageId`, `Content`, `SequenceNumber`, `SessionId`. Les applications clientes ont systématiquement besoin de `ApplicationProperties`, `DeliveryCount`, `EnqueuedTimeUtc`, `CorrelationId`. Faute d'exposition, elles castent vers `AzureFunctionMessageTransit → RawMessage` — ce qui couple tout le code applicatif à Service Bus.

#### Étapes

```csharp
// Messaging/IMessageTransit.cs — interface enrichie
public interface IMessageTransit
{
    string? MessageId { get; }
    string? Content { get; }
    long SequenceNumber { get; }
    string? SessionId { get; }

    // Ajouts Phase 2
    int DeliveryCount { get; }
    DateTimeOffset EnqueuedTime { get; }
    string? CorrelationId { get; }
    string? ReplyTo { get; }
    IReadOnlyDictionary<string, object> ApplicationProperties { get; }
}
```

Implémenter dans `AzureFunctionMessageTransit` en déléguant vers `RawMessage` :

```csharp
public int DeliveryCount => RawMessage.DeliveryCount;
public DateTimeOffset EnqueuedTime => RawMessage.EnqueuedTime;
public string? CorrelationId => RawMessage.CorrelationId;
public string? ReplyTo => RawMessage.ReplyTo;
public IReadOnlyDictionary<string, object> ApplicationProperties
    => RawMessage.ApplicationProperties;
```

Une fois l'interface enrichie : **supprimer les casts** `as AzureFunctionMessageTransit` des consommateurs applicatifs (audit des usages en dehors de l'assembly EMT).

#### Critère de sortie

- [x] `IMessageTransit` exposant les 5 nouvelles propriétés (`DeliveryCount`, `EnqueuedTime`, `CorrelationId`, `ReplyTo`, `ApplicationProperties`).
- [x] `AzureFunctionMessageTransit` les implémentant via délégation vers `_message`.
- [x] Zéro cast `as AzureFunctionMessageTransit` dans le code de l'assembly EMT lui-même (interface interne `IHasRawServiceBusMessage`).
- [x] Test de contrat : `IMessageTransit` mocké dans les tests n'a plus besoin de connaître Service Bus (`InMemoryMessageTransitContractTests`).

---

### P2-C5 — Couplage `AzureFunctionMessagingAdapter` (O5)

**Origine :** §3.3 EMT-DistinguishedEngineerReview.md

#### Contexte

Tant que `AzureFunctionMessagingAdapter` vit dans l'assembly principal, EMT est couplé au SDK `Microsoft.Azure.Functions.Worker`. Ce couplage n'est pas un problème en soi si Azure Functions reste l'unique modèle d'hôte — mais si RAMQ veut supporter AKS / ARO (BackgroundService), il doit être résolu. En Phase 2, l'objectif est de **découpler l'interface sans déplacer le code** (préparation à faible risque).

#### Étapes

1. **Isoler** `AzureFunctionMessagingAdapter` dans un folder `Messaging/Providers/Azure/Functions/` distinct de `Messaging/Providers/Azure/`.
2. **Créer une interface typée** `IMessageActions` dans les abstractions pour remplacer `BindContext(object, object)` :

```csharp
// Messaging/IMessageActions.cs (abstraction)
public interface IMessageActions
{
    Task AbandonMessageAsync(IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default);
    Task CompleteMessageAsync(CancellationToken cancellationToken = default);
    Task DeadLetterMessageAsync(string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default);
}
```

3. **Wrapper** `ServiceBusMessageActions` derrière `IMessageActions` :

```csharp
// Messaging/Providers/Azure/Functions/ServiceBusMessageActionsAdapter.cs
internal class ServiceBusMessageActionsAdapter : IMessageActions
{
    private readonly ServiceBusMessageActions _inner;

    public ServiceBusMessageActionsAdapter(ServiceBusMessageActions inner) => _inner = inner;

    public Task AbandonMessageAsync(IDictionary<string, object>? props, CancellationToken ct)
        => _inner.AbandonMessageAsync(_receivedMessage, props, ct);

    public Task CompleteMessageAsync(CancellationToken ct)
        => _inner.CompleteMessageAsync(_receivedMessage, ct);

    public Task DeadLetterMessageAsync(string reason, string? desc, CancellationToken ct)
        => _inner.DeadLetterMessageAsync(_receivedMessage, reason, desc, ct);
}
```

4. **Remplacer `BindContext(object, object)`** par `BindContext(ServiceBusReceivedMessage, IMessageActions)` dans l'interface `IMessagingAdapter` (breaking change MINOR à documenter dans CHANGELOG).

#### Critère de sortie

- [x] `IMessageActions` créée dans les abstractions (sans référence à `Azure.Messaging.ServiceBus`). *(`IMessageSettlementActions.cs` — 27 avril 2026)*
- [x] `ServiceBusMessageActionsAdapter` wrappant `ServiceBusMessageActions`. *(`Messaging/Providers/Azure/Functions/ServiceBusMessageActionsAdapter.cs` — internal)*
- [x] `IMessagingAdapter.BindContext` : `object actions` remplacé par `IMessageSettlementActions` — `dynamic` éliminé de `RetryPolicyHandler`. *(27 avril 2026)*
- [x] ADR-008 : décision de découplage multi-hôte. *(`docs/adr/ADR-008-decouplage-multi-hote.md` — Principe retenu : abstraction fonctionnelle sans split d'assembly prématuré — 27 avril 2026)*

---

## Résumé des livrables Phase 2

| Volet | Tâche | Livrable | Priorité | Estimation | Statut |
|-------|-------|----------|----------|-----------|--------|
| A | P2-A1 | Projet de tests + CI bloquante + SB Emulator | 🔴 Haute | 3 semaines | 🟡 Partiel |
| | | | | | 49 tests unitaires ✅ · NetArchTest 5 règles ✅ · `[Trait("Category","Unitaire")]` sur toutes les classes ✅ · `pipelines/ci-tests.yml` ✅ (filtre `Category=Unitaire`) · seuil couverture 70 % informatif ✅ · **CI Azure DevOps à configurer** (lier le pipeline dans le projet Azure DevOps) · SB Emulator ❌ |
| A | P2-A2 | `ActivitySource` + 10 spans + `traceparent` | 🔴 Haute | 2 semaines | ✅ Complet |
| | | | | | 9/10 spans livrés ✅ · `messaging.consume` (abstract) — à implémenter dans les classes concrètes · `traceparent` ❌ *(Phase 3)* · `tracing.md` ✅ |
| A | P2-A3 | 8 nouvelles métriques + catalogue docs | 🟠 Moyenne | 1 semaine | ✅ Complet |
| | | | | | 8 métriques dans `MetricsProvider` ✅ · `metrics.md` avec KQL + seuils ✅ |
| B | P2-B1 | `docs/failure-modes.md` + compensation claim-check orphelin | 🔴 Haute | 2 semaines | ✅ Complet |
| | | | | | `failure-modes.md` 9 modes + matrice ✅ · compensation code `PublishCoreAsync`+`PublishBatchAsync` ✅ · 3 tests verts ✅ |
| C | P2-C1 | Surface publique réduite + `InternalsVisibleTo` | 🟠 Moyenne | 2 semaines | ✅ Complet |
| | | | | | 5 types `internal` ✅ · `InternalsVisibleTo` ✅ · ADR-007 ✅ · inventaire 68 types classifiés ✅ |
| C | P2-C2 | ADR-002 MassTransit complété | 🟡 Basse | 2 jours | ✅ Complet |
| | | | | | L1/L2/L3/L4 détaillées ✅ · 4 conditions de révision datées ✅ |
| C | P2-C3 | `docs/consumption.md` + convention dépréciation | 🟠 Moyenne | 3 jours | ✅ Complet |
| | | | | | `docs/consumption.md` ✅ (feed NuGet, politique N/N-1, procédure upgrade 5 étapes) · convention `[Obsolete]` 3 étapes dans `CONTRIBUTING.md` ✅ · méthodes `[Obsolete]` sync (`DeserializeMessage`, `TryDeserializeMessage`) **supprimées** ✅ |
| C | P2-C4 | `IMessageTransit` enrichi | 🔴 Haute | 1 semaine | ✅ Complet |
| | | | | | 5 propriétés ajoutées (`DeliveryCount`, `EnqueuedTime`, `CorrelationId`, `ReplyTo`, `ApplicationProperties`) ✅ · `AzureFunctionMessageTransit` délègue ✅ · interface interne `IHasRawServiceBusMessage` — zéro cast `as AzureFunctionMessageTransit` dans l'assembly ✅ · `InMemoryMessageTransitContractTests` 10/10 sans dépendance Service Bus ✅ *(8 mai 2026)* |
| C | P2-C5 | `IMessageSettlementActions` + découplage Functions | 🟠 Moyenne | 2 semaines | ✅ Complet |
| | | | | | `IMessageSettlementActions` (public, sans réf Azure SDK) ✅ · `ServiceBusMessageActionsAdapter` (internal) ✅ · `dynamic` éliminé de `RetryPolicyHandler` ✅ · ADR-008 ✅ (découplage multi-hôte — 27 avril 2026) |

**Total estimé : 10-14 semaines** · **Au 27 avril 2026 : 9/9 tâches ✅ — Phase 2 CLÔTURÉE**

---

## Condition de passage à la Phase 3

La Phase 3 peut démarrer quand les conditions suivantes sont réunies :

1. 🟡 **Tests de contrat actifs (P2-A1)** — 49 tests unitaires verts ✅ · Tests d'architecture NetArchTest ✅ · `pipelines/ci-tests.yml` livré ✅ · **à lier dans Azure DevOps** (action humaine).
2. ✅ **`ActivitySource` instrumenté (P2-A2)** — 9/10 spans livrés. `messaging.consume` nécessite implémentation dans les classes concrètes ; `traceparent` dans `ApplicationProperties` reporté Phase 3.
3. ✅ **`IMessageTransit` enrichi (P2-C4)** — 5 propriétés exposées, délégation `AzureFunctionMessageTransit` implémentée.
4. ✅ **Surface publique assainie (P2-C5)** — `IMessageSettlementActions` créée, `dynamic` éliminé, méthodes Obsolete sync supprimées.
5. ✅ **ADRs Phase 2** — ADR-002 ✅ · ADR-006 ✅ · ADR-007 ✅ · ADR-008 ✅.
6. ✅ **Inventaire types publics (P2-C1)** — 68 types classifiés dans ADR-007 (6 catégories).
7. ✅ **Documentation croisée** — `CONTRIBUTING.md` §8 ADRs · §9 failure-modes · `docs/consumption.md` · `docs/failure-modes.md`.

> **État au 27 avril 2026 : Phase 2 CLÔTURÉE ✅** — 9/9 tâches complètes. Seul point ouvert (action humaine) : lier `pipelines/ci-tests.yml` dans Azure DevOps. Phase 3 peut démarrer.

> ⚠️ **Pré-requis Phase 3 :** lier le pipeline CI dans Azure DevOps avant de démarrer les travaux de réforme du schéma sérialisé (opération la plus risquée du plan).
