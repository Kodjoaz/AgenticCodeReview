# ADR-007 — Frontière public/internal de l'assembly EMT

## Statut

Accepté

## Contexte

L'assembly `RAMQ.COM.EnterpriseMessageTransit` expose plus de 80 types `public`. Or, l'API réellement destinée aux applications clientes tient en **6 types** :

| Type | Rôle |
|---|---|
| `IMessageProducer<T>` | Publication de messages |
| `IMessageConsumer<T>` | Réception de messages |
| `MessageTransitContext<T>` | Enveloppe message |
| `PublishOptions` | Options de publication |
| `ClaimCheckOptions` | Options Claim Check |
| Extensions DI (`AddProducer<T>`, `AddConsumer<T>`) | Enregistrement DI |

Cette surface excessive (80+ types publics) :
- Expose des détails d'implémentation Azure (`ServiceBusSenderCache`, `AzureMessagingProvider`) que les consommateurs ne devraient jamais instancier.
- Complexifie les mises à jour (chaque changement interne devient un potentiel breaking change MAJOR).
- Rend difficile la génération d'une documentation API utile.

La Phase 1 a introduit `PublicAPI.Shipped.txt` (analyzer `Microsoft.CodeAnalysis.PublicApiAnalyzers`). Cette ADR documente les décisions de reclassification prises en Phase 2.

## Décision

### Types basculés en `internal` (Phase 2)

| Type | Namespace | Justification |
|---|---|---|
| `ServiceBusSenderCache` | `Messaging.Providers.Azure` | Cache de senders Azure SDK — détail d'implémentation, jamais instancié par les clients. |
| `RetryPolicyHandler` | `Messaging.Providers.Azure` | Stratégie de retry — implémentation liée à Azure SDK, non extensible par les clients. |
| `AzureFunctionMessagingAdapter` | `Messaging.Providers.Azure` | Adaptateur Azure Functions — couplé au SDK `Microsoft.Azure.Functions.Worker`, opaque pour les clients. |
| `CircuitBreakerManager` | `Messaging.Providers` | Circuit breaker interne — état machine interne, non exposable sans contrat stable. |
| `AzureMessagingProvider` | `Messaging.Providers.Azure` | Implémentation concrète de `IMessagingProvider` — les clients consomment l'interface, jamais la classe. |

### Types maintenus `public` (avec justification)

| Type | Namespace | Catégorie | Raison de maintien |
|---|---|---|---|
| `IMessageProducer<T>` | `Messaging.Producer` | **Contrat client** | Interface principale pour les producers applicatifs. |
| `IMessageConsumer<T>` | `Messaging.Consumer` | **Contrat client** | Interface principale pour les consumers applicatifs. |
| `MessageTransitContext<T>` | `Messaging` | **Contrat client** | Enveloppe message transportée entre toutes les couches. |
| `PublishOptions` | `Messaging.Producer` | **Contrat client** | Options de publication — API stable consommée par tous les producers. |
| `ClaimCheckOptions` | `Messaging.Producer` | **Contrat client** | Options claim-check — API stable. |
| `RequestReplyOptions` | `Messaging.Producer` | **Contrat client** | Options request/reply — API stable. |
| `BaseConsumer<T>` | `Messaging.Consumer` | **Contrat client** | Classe de base héritée par toutes les applications consumer. Breaking change MAJOR si `internal`. Candidat `public sealed` + `AdvancingConsumer<T>` en Phase 5. |
| `BaseMessageTransit<T>` | `Messaging` | **Contrat client** | Classe de base partagée par `Producer<T>` et `BaseConsumer<T>`. Même contrainte d'héritage. |
| `MessageTransitResponse` | `Messaging` | **Contrat client** | Type de retour de `ConsumeAsync` — utilisé par toutes les applications. |
| `MessagingOptions` | `Messaging` | **Contrat client** | Record d'options passé au provider — utilisé dans les extensions DI. |
| `TokenMessage` | `Messaging` | **Contrat client** | Token claim-check — fait partie du contrat `MessageTransitContext<T>`. |
| `IMessageTransit` | `Messaging.Providers` | **Contrat client** | Abstraction du message reçu (P2-C4) — exposée aux consumers pour éviter le cast Azure SDK. |
| `IMessageSettlementActions` | `Messaging.Providers` | **Contrat client** | Abstraction du règlement (P2-C5) — exposée pour tests et hôtes alternatifs. |
| `IMessageActions` | `Messaging.Providers` | **Contrat client** | Héritée par `IMessagingProvider` et `IMessagingAdapter` — fait partie du contrat de binding. |
| `IMessageConsumer<T>` | `Messaging.Consumer` | **Contrat client** | Contrat consumer — méthodes `ConsumeAsync`, `DeserializeMessageAsync`. |
| `AzureFunctionMessageTransit` | `Messaging.Providers.Azure` | **Contrat client — débat** | Instancié dans les fonctions via DI. À rendre `sealed` en Phase 3. Exposé pour accès à `RawMessage`. |
| `AppSettings` | `Configuration` | **Configuration** | Options racine de configuration — liées via `IOptions<AppSettings>` dans les hôtes. |
| `BlobStorageSetting` | `Configuration` | **Configuration** | Paramètres Blob Storage — liés dans la configuration hôte. |
| `EndpointSettings` | `Configuration` | **Configuration** | Segment d'itinéraire — référencé dans `AppSettings.Itinerary`. |
| `TransportSettings` | `Configuration` | **Configuration** | Paramètres de transport (EntityName, EntityType) — dans `EndpointSettings`. |
| `SubscriptionInfoSettings` | `Configuration` | **Configuration** | Paramètres de souscription Topic — dans `EndpointSettings`. |
| `ExponentialRetryPolicy` | `Configuration` | **Configuration** | Politique de retry exponentiel — configurée dans `AppSettings`. |
| `ProducerSendRetryPolicy` | `Configuration` | **Configuration** | Politique de retry d'envoi — configurée dans `AppSettings`. |
| `ReplyToEndpointInfo` | `Configuration` | **Configuration** | Endpoint de réponse (Request/Reply) — dans `AppSettings`. |
| `AzureServiceBusProviderOptions` | `Messaging.Providers.Azure` | **Configuration** | Options spécifiques au provider Azure SB — enregistrées dans DI. |
| `IMessageTransitConfigurationService` | `Configuration` | **Infrastructure DI** | Interface de configuration injectée dans les providers — exposée pour tests. |
| `IProducerConfigurationService` | `Configuration` | **Infrastructure DI** | Interface producer config — injectée via DI. |
| `IConsumerConfigurationService` | `Configuration` | **Infrastructure DI** | Interface consumer config — injectée via DI. |
| `IEndpointResolver` | `Configuration` | **Infrastructure DI** | Interface de résolution d'endpoint — exposée pour les tests et extensions. |
| `EndpointResolver` | `Configuration` | **Infrastructure DI — débat** | Implémentation concrète — exposée pour les tests d'intégration. Candidat `internal` + `InternalsVisibleTo` en Phase 3. |
| `IMessageTargetMap` | `Configuration` | **Infrastructure DI** | Interface de mapping target → type — exposée pour les tests. |
| `MessageTargetMap` | `Configuration` | **Infrastructure DI** | Implémentation `sealed` du mapping. |
| `MessageTargetMapOptions` | `Configuration` | **Infrastructure DI** | Options de mapping — enregistrées dans DI. |
| `ISystemClock` | `Configuration` | **Infrastructure DI** | Abstraction horloge — exposée pour les tests (injection de temps simulé). |
| `DefaultSystemClock` | `Configuration` | **Infrastructure DI** | Implémentation de `ISystemClock` via `DateTimeOffset.UtcNow`. |
| `IJournalProvider` | `Messaging.Providers` | **Extension** | Interface du journal — exposée pour les implémentations alternatives (Cosmos, SQL). |
| `IStorageProvider` | `Messaging.Providers` | **Extension** | Interface du stockage claim-check — extensible (Azure Blob, S3, etc.). |
| `IMessageSerializer` | `Serialization` | **Extension** | Interface de sérialisation — extensible (JSON, Protobuf, etc.). |
| `IMessagingProvider` | `Messaging.Providers` | **Extension — débat** | Interface du provider de messaging — exposée pour les tests et hôtes alternatifs. À évaluer `internal` en Phase 3. |
| `IMessagingAdapter` | `Messaging.Providers` | **Extension — débat** | Interface de l'adaptateur — exposée pour les tests. À évaluer `internal` en Phase 3. |
| `IRetryPolicyHandler` | `Messaging.Providers` | **Extension — débat** | Interface de retry — exposée pour les tests. À évaluer `internal` en Phase 3. |
| `IMetricsProvider` | `Messaging.Providers` | **Extension** | Interface métriques — extensible pour les équipes qui intègrent leurs propres métriques. |
| `MetricsProvider` | `Messaging.Providers.Azure` | **Extension — débat** | Implémentation concrète des métriques. Candidat `internal` en Phase 3. |
| `AzureJournalProvider` | `Messaging.Providers.Azure` | **Extension — débat** | Implémentation concrète du journal Blob. Candidat `internal` en Phase 3. |
| `AzureStorageProvider` | `Messaging.Providers.Azure` | **Extension — débat** | Implémentation concrète du stockage claim-check. Candidat `internal` en Phase 3. |
| `JsonMessageSerializer` | `Serialization` | **Extension — débat** | Implémentation concrète JSON. Candidat `internal` si `IMessageSerializer` est l'extension point. |
| `JournalEntry` | `Messaging.Providers` | **Données** | Record du journal — utilisé par les équipes de logging et monitoring. |
| `OperationMode` | `Messaging.Enum` | **Données** | Enum de mode d'opération — dans `JournalEntry`. |
| `MessagingEntityType` | `Messaging.Enum` | **Données** | Queue vs Topic — utilisé dans la configuration et les tests. |
| `ProcessingEvent` | `Messaging.Enum` | **Données** | Événement de traitement — dans le journal. |
| `TokenKind` | `Messaging.Enum` | **Données** | Type de token claim-check — dans `TokenMessage`. |
| `DeserializationResult<T>` | `Serialization` | **Données** | Résultat de désérialisation typé — utilisé dans les consumers. |
| `DeserializationFailureReason` | `Serialization` | **Données** | Enum de raison d'échec — référencé dans ADR-006. |
| `CircuitBreakerOpenException` | `Messaging.Providers` | **Exception** | Levée lorsque le circuit est ouvert — les callers peuvent la catcher. |
| `CircuitBreakerOptions` | `Messaging.Providers` | **Configuration** | Options du circuit breaker — configurées dans `AppSettings`. |
| `CircuitState` | `Messaging.Providers` | **Données** | État du circuit — utile pour le monitoring. |
| `ConfigurationException` | `Exceptions` | **Exception** | Toutes les exceptions sont `public` — interface applicative. |
| `DeadLetteringFailedException` | `Exceptions` | **Exception** | Idem. |
| `ExponentialRetryException` | `Exceptions` | **Exception** | Idem. |
| `ImmediateDLQException` | `Exceptions` | **Exception** | Idem. |
| `ImmediateRetryException` | `Exceptions` | **Exception** | Idem. |
| `MessageCompletionException` | `Exceptions` | **Exception** | Idem. |
| `MessageSendException` | `Exceptions` | **Exception** | Idem. |
| `RoutingSlipException` | `Exceptions` | **Exception** | Idem. |
| `TransitItineraryException` | `Exceptions` | **Exception** | Idem. |
| `ServiceBusHealthCheck` | `Configuration` | **Infrastructure** | Health check ASP.NET Core — enregistré dans DI pour les endpoints `/health`. |
| `HealthStatus` | `Configuration` | **Infrastructure** | Enum associé au health check. |
| `HealthCheckResult` | `Configuration` | **Infrastructure** | Record de résultat du health check. |
| `IsExternalInit` | — | **Compatibilité** | Polyfill .NET 5 pour les record — peut être supprimé si net8.0 est la cible unique. |

### Types `internal` (Phase 2 et antérieur)

| Type | Justification |
|---|---|
| `ServiceBusSenderCache` | Cache interne Azure SDK — opaque. |
| `RetryPolicyHandler` | Stratégie de retry Azure SDK — opaque. |
| `AzureFunctionMessagingAdapter` | Adaptateur Functions Azure SDK — opaque. |
| `CircuitBreakerManager` | State machine interne — opaque. |
| `AzureMessagingProvider` | Implémentation concrète du provider — les clients consomment l'interface. |
| `ServiceBusMessageActionsAdapter` | Adaptateur Actions Azure SDK livré en P2-C5 — toujours `internal`. |
| `MessagingActivitySource` | Source de tracing interne — exposée via `MessagingActivitySource.Name` uniquement. |

### `InternalsVisibleTo` accordé

```csharp
[assembly: InternalsVisibleTo("EnterpriseMessageTransit.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // NSubstitute / Moq
```

## Conséquences

- Les types `internal` ne peuvent plus être instanciés directement en dehors de l'assembly — les tests unitaires existants passant par NSubstitute bénéficient de `DynamicProxyGenAssembly2`.
- `PublicAPI.Shipped.txt` doit être mis à jour pour supprimer les 5 types basculés (déjà retiré en Phase 2).
- Toute ré-exposition d'un type `internal` en `public` nécessite une entrée dans `PublicAPI.Unshipped.txt` et un bump de version MINOR minimum.
- La surface publique cible (< 20 types) sera atteinte progressivement — Phase 3 pour `BaseMessageTransit`, Phase 5 pour `BaseConsumer<T>`.

## Conditions de révision

- Nouvelle exigence d'extensibilité externe documentée (ex. : besoin de remplacer `AzureMessagingProvider` par un provider custom hors-assembly).
- Phase 5 : revue complète de `BaseConsumer<T>` et `BaseMessageTransit`.

## Références

- `PublicAPI.Shipped.txt` — surface publique actuelle post-Phase 2
- `GlobalSuppressions.cs` — `InternalsVisibleTo` déclarations
- P2-C1 dans `docs/EMT-Review-Phase2.md`
- [NetArchTest — Architecture/LayeringTests.cs](../../EnterpriseMessageTransit.Tests/Architecture/LayeringTests.cs)

## Signataires et date

- ___________ (Architecte) — 2026-04-27
- ___________ (Lead technique) — 2026-04-27
