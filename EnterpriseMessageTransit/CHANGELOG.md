# Changelog — RAMQ.COM.EnterpriseMessageTransit

Toutes les modifications notables de ce projet sont documentées ici.
Format : [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/) · [SemVer strict](https://semver.org/).

> **Incrément PATCH** : correction de bug sans changement de comportement observable.  
> **Incrément MINOR** : fonctionnalité additive rétrocompatible.  
> **Incrément MAJOR** : breaking change (surface publique, schéma sérialisé, comportement observable).

---

## [Unreleased]

### Ajouté — Observabilité OpenTelemetry (surface publique)

- **`EMTInstrumentation`** (classe publique, `Messaging/Telemetry/EMTInstrumentation.cs`) : expose la constante publique `SourceName = "RAMQ.COM.EnterpriseMessageTransit"` pour permettre aux hôtes applicatifs d'enregistrer la source de traces EMT dans leur `TracerProvider` sans avoir à connaître la valeur exacte. Usage : `.AddSource(EMTInstrumentation.SourceName)` dans `Program.cs`. Cette classe complète `MessagingActivitySource` (qui reste `internal`) en exposant uniquement la constante nécessaire à la configuration OTel côté hôte.

### Ajouté — Routing Slip v2.0 (pattern workflow complet)

- **`IRoutingSlipActivity<TArgs>`** : interface à implémenter pour chaque étape du workflow. L'activité est un orchestrateur de transit — toute logique métier est déléguée à une API externe. Voir `docs/architecture-routing-slip.md §6.1`.

- **`ActivityContext<TArgs>`** : contexte fourni par le framework à chaque exécution d'activité. Expose `Arguments`, `Variables` (partagées entre étapes), `ClaimCheckToken`, `SlipId`, `CorrelationId`, `Attempt`, `StepName`, `StepIndex`, `TotalSteps` et la méthode `GetVariable<T>(key)`.

- **`ActivityResult`** : résultat retourné par une activité. Factory methods statiques :
  - `ActivityResult.Next(enrichVariables?)` — passer à l'étape suivante (optionnellement enrichir les variables partagées)
  - `ActivityResult.Complete()` — terminer le workflow explicitement
  - `ActivityResult.Fault(exception)` — erreur permanente → DLQ
  - `ActivityResult.RetryImmediate(reason)` — erreur transitoire courte → relivraison immédiate
  - `ActivityResult.RetryExponential(reason, innerException?)` — erreur transitoire longue → backoff exponentiel

- **`SlipEnvelope`** : enveloppe auto-porteuse qui voyage de queue en queue. Contient `SlipHeader`, `SlipStep[]`, `Cursor` et `Variables`. Sérialisée comme payload du `MessageTransitContext<SlipEnvelope>`.

- **`SlipHeader`** : métadonnées du slip (`SlipId`, `SlipName`, `CorrelationId`, `CreatedAt`). Immuable après création.

- **`SlipStep`** (sealed record) : une étape auto-porteuse avec `Name`, `EntityName`, `EntityType`, `Subscription?`, `Arguments` (JsonElement), `Status`.

- **`SlipTopicSubscription`** (sealed record) : abonnement cible pour les étapes Topic (`Consumer`, `Action?`). Publié comme Application Properties Service Bus pour le filtrage SQL.

- **`SlipStepStatus`** (enum) : `Pending`, `Active`, `Completed`, `Faulted`.

- **`RoutingSlipBuilder`** : construit un `SlipEnvelope` depuis des noms logiques (stepName = Target dans `AppSettings.Endpoints`). Résout l'EntityName physique via `IEndpointResolver` — jamais de noms d'entités dans le code. Méthodes : `AddStep<TArgs>(stepName, arguments)` et `Build()`. Lève `TransitItineraryException` si un stepName est inconnu.

- **`IRoutingSlipExecutor`** (interface interne) : `ProcessAsync(provider, ct)` pour les étapes Queue, `ExecuteAsync(provider, ct)` pour les étapes Topic.

- **`RoutingSlipExecutor<TArgs>`** (classe interne) : orchestre le flux complet d'une étape — désérialisation du `SlipEnvelope`, désérialisation des arguments, appel de l'activité, traitement du résultat (Next → envoie l'étape suivante, Complete → complète, Fault → DLQ, RetryImmediate/RetryExponential → lève l'exception EMT correspondante).

- **`AddRoutingSlipActivity<TActivity, TArgs>(target)`** (extension DI) : enregistre une activité Routing Slip pour une étape Queue. `target` = valeur du champ `Target` dans `AppSettings.Endpoints`.

- **`AddRoutingSlipActivityForTopic<TActivity, TArgs>(target)`** (extension DI) : idem pour une étape Topic.

- **Exemple complet** (`src/Exemples/RAMQ.Samples.Queue.RoutingSlip.*`) : scénario 3 étapes (Valider → Enrichir → Notifier) avec :
  - `RAMQ.Samples.Queue.RoutingSlip.Message/` — types `TArgs` indépendants par activité
  - `RAMQ.Samples.Queue.RoutingSlip.Activateur/` — Azure Function HTTP POST qui construit et publie le slip
  - `RAMQ.Samples.Queue.RoutingSlip.Worker/` — 3 Azure Functions (une par queue) + 3 activités

- **33 nouveaux tests unitaires** (`EnterpriseMessageTransit.Tests/RoutingSlip/`) : `ActivityResultTests`, `RoutingSlipBuilderTests`, `RoutingSlipExecutorTests`.

### Modifié

- **`AppSettings.Itinerary` renommé `AppSettings.Endpoints`** (breaking change) : la propriété `List<EndpointSettings> Itinerary` est renommée `Endpoints`. Ce renommage reflète la sémantique exacte : une liste de points de terminaison connus de l'application, pas un itinéraire global de workflow (responsabilité du `RoutingSlipBuilder`). **Migration** : renommer `"Itinerary"` → `"Endpoints"` dans tous les `appsettings.json`.

- **`BaseConsumer.DeserializeMessageAsync` — suppression de l'auto-download Claim-Check** (breaking change) : le téléchargement automatique depuis Azure Blob Storage est supprimé. Le token Claim-Check est propagé tel quel dans `MessageTransitContext.Tokens` via `ctx.ClaimCheckToken`. Le consumer ou son API downstream appelle `_storageProvider.DownloadAsync(token.Reference, ct)` explicitement si nécessaire.

- `architecture-routing-slip.md` : document complet du Routing Slip v2.0 (~3300 lignes, §6.1–§16).
- `architecture-technique.md` : §4.2, §9.4 et §13 mis à jour (Itinerary → Endpoints, Routing Slip v2.0).
- `architecture-resume.md` : §4, §9, §13 mis à jour.
- Test `Patron92_ClaimCheck_ConsumerRoundTrip_TelechargeBlob` mis à jour pour refléter la suppression de l'auto-download.

### Changements cassants

- **`AppSettings.Itinerary` → `AppSettings.Endpoints`** : renommer la clé JSON dans tous les `appsettings.json`. Toutes les classes EMT (`EndpointResolver`, `BaseMessageTransit`, `BaseConsumer`, `IdempotenceValidationService`) utilisent désormais `Endpoints`.
- **`BaseConsumer.DeserializeMessageAsync` ne télécharge plus automatiquement les blobs Claim-Check** : les consumers qui comptaient sur l'auto-hydration doivent désormais appeler `_storageProvider.DownloadAsync()` explicitement. `DownloadCount` passe de 1 à 0 dans les tests existants.

 : `SendBatchAsync` utilise désormais un seul `ServiceBusMessageBatch` — envoi atomique (tout passe ou rien). Si un message ne rentre pas dans le batch, une `ArgumentException` est levée avant tout envoi, avec la liste des MessageIds en cause.
- **`PublishBatchAsync` — validation en deux étapes** :
  1. Limite applicative configurable via `TransportSettings.MaxMessageSizeKb` — rejet fail-fast des messages dont le corps dépasse la limite organisationnelle (réseau, métier, etc.) **avant** d'appeler le broker.
  2. Limite broker via `ServiceBusMessageBatch.TryAddMessage` — détecte le dépassement collectif (overhead headers inclus).
- **`TransportSettings.MaxMessageSizeKb`** (`int`, défaut `0`) : limite applicative de la taille du corps d'un message dans un batch, indépendante de la limite Service Bus. `0` = aucune limite applicative. Exemple : `256` (Standard), `1024` (Premium), ou toute valeur organisationnelle inférieure.
- **`PublishBatchAsync` — Claim-Check interdit** : `NotSupportedException` levée immédiatement si `PublishOptions.ClaimCheck.ForceClaimCheck == true` ou `ClaimCheck.FileStream != null`. Raison : un upload Blob réussi suivi d'un échec Service Bus produirait des blobs orphelins non compensables de façon fiable dans un contexte batch. Utiliser `PublishAsync` en boucle pour les messages nécessitant le Claim-Check.
- `IMetricsProvider` + `MetricsProvider` : compteurs, histogrammes et jauges via `System.Diagnostics.Metrics`.
- `ServiceBusHealthCheck` : vérification de connectivité Service Bus au démarrage de l'application.
- Factory methods `JournalEntry.ForPublish()`, `.ForRetry()`, `.ForDLQ()` : construction lisible des entrées de journal.
- `IRetryPolicyHandler` + `RetryPolicyHandler` : extraction de la logique de retry (SRP — Single Responsibility Principle).
- `ProducerSendRetryPolicy` : politique de retry côté Producer, distincte du Consumer.
- `ServiceBusSenderCache.ReplaceSender` : remplacement synchrone du sender AMQP sur erreur fatale.
- `CircuitBreakerManager` + `CircuitBreakerOptions` : disjoncteur configurable par entité Service Bus.
- `DeserializationResult<T>` + `DeserializationFailureReason` : résultat structuré de désérialisation (remplacement des exceptions silencieuses).
- `IMessageActions` : interface découplant les actions Service Bus du Consumer (complétion, retry, DLQ).
- **Phase 1 — Gouvernance** :
  - `PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt` : surface publique figée via `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
  - `docs/adr/` : 5 ADRs rétrospectifs (ADR-001 à ADR-005).
  - `docs/contracts/envelope-v1.md` : contrat de schéma `MessageTransitContext<T>` v1.
  - `CONTRIBUTING.md` : conventions de contribution.
  - `<Version>0.9.0</Version>` dans le `.csproj`.
- **Phase 2 — Observabilité et architecture** :
  - `Messaging/Telemetry/MessagingActivitySource.cs` : `ActivitySource` statique `"RAMQ.COM.EnterpriseMessageTransit"` pour le tracing distribué OpenTelemetry. Le span `messaging.publish` est instrumenté sur chaque `PublishCoreAsync` avec les tags sémantiques `messaging.system`, `messaging.destination`, `messaging.message_id`, `messaging.session_id`.
  - `IMetricsProvider` : 8 nouvelles métriques — `SetCircuitState`, `IncrementCircuitTransition`, `IncrementDeserializationFailure`, `RecordClaimCheckUploadDuration`, `RecordClaimCheckDownloadDuration`, `RecordJournalWriteDuration`, `IncrementSagaStageAdvance`, `IncrementDuplicateDetected`.
  - `MetricsProvider` : implémentation des 8 nouvelles métriques via `Counter`, `Histogram` et `ObservableGauge` multi-entité pour l'état du circuit breaker.
  - `IMessageTransit` : 5 nouvelles propriétés exposées — `DeliveryCount`, `EnqueuedTime`, `CorrelationId`, `ReplyTo`, `ApplicationProperties` — éliminant le besoin de caster vers `AzureFunctionMessageTransit` côté applicatif.
  - `docs/failure-modes.md` : 9 modes de défaillance documentés avec symptômes, causes, conséquences métier, actions correctives et matrice exception→action.
  - `docs/observability/tracing.md` : guide de configuration OpenTelemetry pour Azure Functions et BackgroundService, table des points d'instrumentation et requêtes KQL.
  - `docs/observability/metrics.md` : catalogue des 18 métriques avec seuils d'alerte recommandés et 5 requêtes KQL Application Insights.
  - `[assembly: InternalsVisibleTo("RAMQ.COM.EnterpriseMessageTransit.Tests")]` et `DynamicProxyGenAssembly2` dans `GlobalSuppressions.cs` : permet aux tests d'accéder aux types `internal`.
- **Phase 3 — Qualité et résilience** :
  - `TransportSettings.PublishTimeout` : timeout borné sur `PublishAsync`/`PublishBatchAsync` — `MessageSendException` levée au dépassement (défaut : 30 secondes).
  - `DeserializationResult<T>` intégré dans `BaseConsumer.DeserializeMessageAsync` : métriques `deserialization_failures_total{reason}` incrémentées sur chaque chemin d'échec — (**note :** le settlement est transféré au consumer applicatif — voir section *Changements cassants* ci-dessous).
  - `Producer.PublishBatchAsync` : journal parallélisé via `Task.WhenAll` — suppression du blocage O(n).
  - `EndpointResolver` : listes pré-calculées via `Lazy<T>` + index `Dictionary<string, EndpointSettings>` pour `TryResolve` O(1).
  - Tests de contrat : 28 tests (`IMessageSerializer` × 10, `IJournalProvider` × 3, `IStorageProvider` × 5, `IMetricsProvider` × 10).
  - `TransportSettings.RequiresDuplicateDetection` : opt-in DuplicateDetection.
  - `IdempotenceValidationService` (`IHostedService`) : validation au démarrage que les entités avec `RequiresDuplicateDetection = true` ont `RequiresDuplicateDetection` activé côté Service Bus — fast-fail au lieu de comportement silencieux.
  - `IHasRawServiceBusMessage` (interface `internal`) : élimine le cast direct vers `AzureFunctionMessageTransit` dans `AzureFunctionMessagingAdapter.BindContext` — `AzureFunctionMessageTransit` implémente désormais `IMessageTransit, IHasRawServiceBusMessage`.
  - `docs/adr/ADR-006-politique-dlq-deserialisation.md` (superseded) · `docs/idempotence.md`.
- **Phase 4 — Performance et enveloppe opérationnelle** :
  - `AzureMessagingProvider.SendAsync`/`SendBatchAsync` : propagation W3C Trace Context (`traceparent`, `tracestate`) dans `ApplicationProperties` Service Bus.
  - `BaseConsumer.DeserializeMessageAsync` : lecture du `traceparent` W3C depuis `ApplicationProperties` du message reçu — création du span `messaging.consume` (`ActivityKind.Consumer`) avec corrélation cross-service. Complète la propagation bout-en-bout : le span `messaging.deserialize` devient automatiquement un enfant de `messaging.consume`.
  - `IMessagingAdapter.GetTraceparent()` / `IMessagingProvider.GetTraceparent()` : méthode d'interface avec implémentation par défaut `null` — non-breaking pour les implémenteurs existants.
  - `AzureFunctionMessagingAdapter.GetTraceparent()` / `AzureMessagingProvider.GetTraceparent()` : implémentations concrètes lisant `ApplicationProperties["traceparent"]` du message Service Bus.
  - `TracingTests.cs` : 3 nouveaux tests consumer — span `messaging.consume` présent, `messaging.deserialize` est enfant de `messaging.consume`, comportement sans traceparent.
  - `BaseConsumer` : injection `IMetricsProvider? metricsProvider` (paramètre optionnel) — `_metrics?.IncrementDeserializationFailure(reason)` sur chaque chemin ADR-006.
  - `IMetricsProviderContractTests` : 3 tests contrat `IncrementDeserializationFailure_NeLevePas_*` + 4 tests T12 `TracingTests.cs`.
  - `EnterpriseMessageTransit.Benchmarks` : projet BenchmarkDotNet (`SerializerBenchmarks` 1/10/256 Ko, `EndpointResolverBenchmarks` 1/10/50 audiences).
  - `tests/docker-compose.servicebus.yml` + `servicebus-emulator-config.json` + `tests/ServiceBusEmulatorTests.cs` (skeleton `Category=Integration`).
  - `docs/extensibility.md` : guide intégrateurs 4 interfaces avec exemples DI.
  - `docs/operational-envelope.md` : limites Azure SB, recommandations config, placeholders baseline.
- **Phase 5 — Refactoring architectural interne** :
  - `Messaging/RoutingSlip/` : 9 nouveaux types (`RoutingSlip`, `SlipStage`, `StageIdentifier`, `RoutingSlipResult`, `IStageAdvancer`, `StageAdvancer`, `IItineraryPlanner`, `ItineraryPlanner`, `SagaStageValidationException`) — logique saga pure, zéro I/O.
  - `BaseConsumer.FindIndexFromStage` délègue à `IStageAdvancer` (E3) — comportement identique, couplage éliminé.
  - `StageAdvancerTests` : 14 tests purs `Category=Unitaire` (T10 DE Review).
  - `InMemoryMessagingAdapter` + `IMessagingAdapterContractTests` + `InMemoryAdapterContractTests` : suite de contrat (T6 DE Review) — 10 tests.
  - `stryker-config.json` : configuration Stryker.NET modules critiques (threshold 70 %).
  - `pipelines/mutation-tests.yml` : pipeline nightly Azure DevOps — Stryker.NET.
  - `AzureFunctionMessagingAdapter` : namespace déplacé vers `Messaging.Providers.Azure.Functions`.
  - `LayeringTests` : 2 nouvelles règles d'architecture — `ProvidersAzureCore_NeDependPasDe_FunctionsWorker` + `RoutingSlipNamespace_NeDependPasDe_AzureSDK`.

### Modifié
- `JsonMessageSerializer.Deserialize` : suppression de la pré-validation `JsonDocument.Parse` — passe unique.
- `IMessageSerializer` enregistré en `Singleton` (était `Scoped`).
- `BaseConsumer.DeserializeMessageAsync` : version `async` — `.GetAwaiter().GetResult()` éliminé.
- Identifiants FR→EN : `ObtenirTopicStage` → `GetTopicStage`, `ObtenerContexteMessage` → `GetMessageContext`.
- `AzureFunctionMessageTransit` : implémente les 5 nouvelles propriétés de `IMessageTransit` par délégation vers `ServiceBusReceivedMessage`.
- `Producer.PublishCoreAsync` : instrumentation OpenTelemetry — chaque envoi produit un span `messaging.publish` avec statut `Ok` ou `Error` et tags d'exception en cas d'échec.

### Rétrogradé en `internal` (réduction de surface publique — P2-C1)
> ⚠️ **Breaking change potentiel** pour tout code externe qui instancierait directement ces types. Ces classes n'ont jamais été documentées comme API publique stable — leur utilisation directe était déconseillée. La surface publique stable reste les interfaces (`IMessagingAdapter`, `IRetryPolicyHandler`, `IMessagingProvider`).
- `ServiceBusSenderCache` : cache de senders AMQP — détail d'implémentation interne.
- `RetryPolicyHandler` : handler de retry — utiliser `IRetryPolicyHandler` via DI.
- `CircuitBreakerManager` : disjoncteur — détail d'implémentation interne.
- `AzureMessagingProvider` : fournisseur Azure Service Bus — détail d'implémentation interne.
- `AzureFunctionMessagingAdapter` : adaptateur Azure Functions — détail d'implémentation interne.

### Corrigé
- Fuite de sender AMQP dans `ExponentialRetryAsync` (branche sans session).
- `ValidateDuplicateTargets` déplacé du chemin critique vers l'initialisation (démarrage).
- **Phase 1** : `TokenKind` — ajout de `[JsonConverter(typeof(JsonStringEnumConverter))]` : sérialisé en chaîne (`"Message"`, `"File"`) au lieu d'entier (`0`, `1`).
- **Phase 1** : `MessageTransitContext.CurrentStage` — `internal set` remplacé par `public set` : la désérialisation depuis un assembly externe fonctionnait silencieusement mais ignorait la valeur désérialisée (saga routing affecté).
- **Révision SOLID — réduction de surface publique et élimination de code mort** *(mai 2026)* :
  - `BaseConsumer` : paramètres constructeur `IStageAdvancer stageAdvancer` et `IItineraryPlanner itineraryPlanner` supprimés — ces dépendances n'étaient pas utilisées dans le constructeur. Le constructeur accepte maintenant 5 paramètres obligatoires + `IMetricsProvider?` optionnel.
  - `BaseConsumer.Stamp()` renommé `ResetInvocationMetadata()` — nom descriptif du comportement réel (remise à zéro des métadonnées d'invocation entre messages).
  - `IMessagingProvider` : méthodes `DeserializeMessage<T>()` et `TryDeserialize<T>()` supprimées — code mort non utilisé par `BaseConsumer` depuis l'introduction de `DeserializationResult<T>`.
  - `IRetryPolicyHandler` : méthode `HandleDeadLetterAsync` supprimée — violation OCP (la logique de DLQ est propre à chaque consumer applicatif).
  - `RetryPolicyHandler` : `SafeWriteJournalAsync` ajoutée (Pattern A5 — swallow des erreurs de journalisation) — les 7 appels journal wrappés évitent qu'une erreur de journal ne masque l'erreur métier originale.
  - `CircuitBreakerManager` : `CircuitState` rétrogradé en `internal enum` — détail d'implémentation non nécessaire dans la surface publique.
  - `AzureJournalProvider` : suppression du `using Azure.Identity` non utilisé (avertissement IDE0005).
  - `GlobalSuppressions.cs` : 2 suppressions `SuppressMessage` mortes (pour `TryDeserialize`) supprimées.

### Changements cassants

> ⚠️ Ces changements nécessitent une adaptation des consumers applicatifs existants.

- **`BaseConsumer.DeserializeMessageAsync<T>()` — type de retour modifié** *(8 mai 2026)*  
  Retourne désormais `DeserializationResult<MessageTransitContext<T>>` au lieu de `MessageTransitContext<T>?`.  
  EMT n'effectue **plus aucun settlement** (Complete / DeadLetter) lors d'un échec de désérialisation — c'est le consumer applicatif qui inspecte `result.IsSuccess` et `result.FailureReason` pour décider.  
  **Migration :**
  ```csharp
  // Avant
  var ctx = await _consumer.DeserializeMessageAsync<MonMessage>(ct);
  if (ctx is null) return; // EMT avait déjà settled le message
  await _consumer.ConsumeAsync(ctx, ct);

  // Après
  var result = await _consumer.DeserializeMessageAsync<MonMessage>(ct);
  if (!result.IsSuccess)
  {
      await _consumer.DeadLetterMessageAsync(result.Exception!, ct); // ou Complete selon le contexte
      return;
  }
  await _consumer.ConsumeAsync(result.Value!, ct);
  ```

- **`IMessageConsumer<TMessage>.TryDeserializeMessageAsync` — supprimé** *(8 mai 2026)*  
  Utiliser `DeserializeMessageAsync` et inspecter `result.IsSuccess`.

---

## [0.8.0] — 2026-04-23

### Ajouté
- `ProducerSendRetryPolicy` : politique de retry dédiée côté Producer.
- `ServiceBusSenderCache.ReplaceSender` : remplacement synchrone du sender AMQP sur erreur fatale.

---

## [0.7.0] — 2026-02-14

### Ajouté
- `MessageTransitContext<T>.CopyWithResponse<TResponse>` : propagation automatique des métadonnées.
- Support du patron Sequential Convoy via `SessionId` sur `MessageTransitContext<T>`.

### Modifié
- `CurrentStage` remplace définitivement `CurrentTarget` (migration interne — non-breaking pour les consommateurs).

---

## [0.6.0] — 2025-11-30

### Ajouté
- Pattern Claim-Check : dépôt automatique en Azure Blob Storage si payload > seuil configurable (256 Ko par défaut).
- `ClaimCheckOptions` : configuration du seuil et du conteneur Blob.
- `IStorageProvider` + `AzureStorageProvider` : abstraction du stockage Blob.

---

## [0.5.0] — 2025-09-15

### Ajouté
- Pattern Request/Reply via session Service Bus (`RequestReplyOptions`).
- Pattern Saga / Routage Saga : `RouteToNextStageAsync` dans `BaseConsumer`.

---

## [0.4.0] — 2025-07-01

### Ajouté
- Message Transit Journal (Azure Table Storage) : écriture automatique à chaque opération Producer/Consumer.
- `AzureJournalProvider` + `IJournalProvider`.

### Corrigé
- `ExponentialRetryAsync` : calcul du délai corrigé (overflow sur `MaxDelay`).

---

## [0.3.0] — 2025-04-20

### Ajouté
- `BaseConsumer<TMessage>` + `IMessageConsumer<TMessage>` : patron Consumer avec gestion DLQ / retry.
- `IConsumerConfigurationService` : configuration de l'itinéraire Consumer.

---

## [0.2.0] — 2025-02-10

### Ajouté
- `Producer<TMessage>` + `IMessageProducer<TMessage>` : patron Producer générique.
- `IMessageTargetMap` + `MessageTargetMap` : résolution du target via l'itinéraire.
- `ProducerServiceCollectionExtensions.AddProducer<TMessage>` : enregistrement DI simplifié.

---

## [0.1.0] — 2024-12-01

### Ajouté
- Version initiale : `MessageTransitContext<TMessage>`, `PublishOptions`, `IMessagingProvider`.
- Support Azure Service Bus (file d'attente et rubrique).
- Authentification Managed Identity (`DefaultAzureCredential`).
