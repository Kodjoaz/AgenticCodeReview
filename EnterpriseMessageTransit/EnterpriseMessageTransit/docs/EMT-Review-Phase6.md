# EMT — Plan d'exécution Phase 6 : Portabilité et ouverture d'écosystème

> **Couleur de référence :** 🔴 Très élevé
> **Source :** [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md) — §1.2 · §1.3 · §3.3 · §6.1.1 (T6 · T18 · T19) · §8.3
> **Date de planification :** 27 avril 2026
> **Date de démarrage :** —
> **Durée indicative :** 10-16 semaines
> **Risque / Complexité :** 🔴 Très élevé / 🔴 Très élevée — breaking changes NuGet majeurs impactant toutes les applications consommatrices
> **Prérequis :** Phase 5 ✅ complétée · **ADR-001 option B retenue explicitement** · Cas d'usage non-Azure concret identifié
> **Statut :** ⛾ Non démarrée — **conditionnel à la décision D1 (ADR-001)**

---

## Avertissement — Phase conditionnelle

> ⚠️ **Cette phase ne doit PAS être démarrée sans décision D1 explicite.**
>
> Si ADR-001 retient l'**option A** (assumer le lock-in Azure), cette phase est **annulée**. Le package est renommé `RAMQ.Azure.Messaging.ServiceBus.Transit`, les abstractions prétendument génériques sont supprimées, et la roadmap s'arrête à la Phase 5.
>
> Si ADR-001 retient l'**option B** (portabilité active), cette phase est exécutée uniquement si **un cas d'usage non-Azure concret existe** (ex. migration d'un domaine vers AKS + Kafka en production). La préparation des phases 1-5 couvre déjà 80 % de la valeur — ne lancer la Phase 6 que si le besoin est réel.
>
> *— EMT-DistinguishedEngineerReview §8.3.3*

---

## Positionnement de la phase

> 💡 **Pour les développeurs juniors.** La Phase 6 est la phase la plus lourde et la plus risquée de toute la roadmap. Elle consiste à **éclater un assembly unique en 10 packages NuGet distincts** et à ajouter le support de deux nouveaux transports (Kafka, RabbitMQ). Chaque application consommatrice devra mettre à jour ses dépendances — c'est un **breaking change majeur** (SemVer : `v3.0.0`, le v2.0 étant réservé au Routing Slip natif livré en Phase 5). C'est pourquoi les phases 1-5 sont des prérequis : elles construisent les filets de sécurité (tests de contrat, benchmarks, mutation testing) sans lesquels la scission ne peut pas être faite sans régression.

La Phase 6 réalise la thèse de portabilité décrite dans la DE Review §1.2 : **le même consumer applicatif fonctionne sur Azure Functions + Service Bus, AKS + Kafka, et ARO + RabbitMQ, sans aucune modification de code métier**.

---

## Décision préalable obligatoire — ADR-001 révisé

Avant de démarrer la Phase 6, confirmer dans un ADR-001 mis à jour :

| Question | Réponse attendue |
|----------|-----------------|
| Option A ou B retenue ? | **Option B** — portabilité active |
| Cas d'usage non-Azure identifié ? | Migration d'un domaine RAMQ vers AKS + Kafka (nommer le domaine) |
| Équipe disponible pour Kafka + RabbitMQ ? | Oui — compétences validées |
| Stratégie N/N-1 NuGet définie ? | Oui — période de coexistence ≥ 1 release |
| DevOps OK pour matrice CI (3 hôtes × 3 brokers) ? | Oui — pipelines approuvés |

---

## Architecture cible — 10 packages NuGet

```
RAMQ.Integration.Abstractions       ← interfaces IMessageTransit, IMessageConsumer<T>
RAMQ.Integration.Envelope           ← MessageTransitContext<T>, CloudEvents 1.0
RAMQ.Integration.RoutingSlip        ← module Routing Slip natif (Phase 5 v2.0 → assembly séparé ici)
RAMQ.Integration.Transport.ServiceBus  ← SDK Azure.Messaging.ServiceBus
RAMQ.Integration.Transport.Kafka       ← SDK Confluent.Kafka
RAMQ.Integration.Transport.RabbitMq    ← SDK RabbitMQ.Client
RAMQ.Integration.Storage.AzureBlob    ← claim-check Blob
RAMQ.Integration.Journal.AzureTable   ← journal A5 Table Storage
RAMQ.Integration.Hosting.Functions    ← adapter Azure Functions Worker
RAMQ.Integration.Hosting.AspNetCore   ← adapter BackgroundService (AKS / ARO)
```

### Règles de dépendances entre packages

```
Abstractions     ← aucune dépendance externe Azure / Kafka
Envelope         ← dépend Abstractions + CloudNative.CloudEvents
RoutingSlip      ← dépend Abstractions seulement
Transport.*      ← dépend Abstractions + Envelope + SDK broker respectif
Storage.*        ← dépend Abstractions
Journal.*        ← dépend Abstractions
Hosting.*        ← dépend Abstractions + Transport.* choisi
```

---

## Tâche P6-T1 — Adoption CloudEvents 1.0 comme enveloppe (§1.3.2)

**Origine :** §1.3.2 EMT-DistinguishedEngineerReview.md

### Contexte

`MessageTransitContext<T>` est aujourd'hui sérialisé tel quel. CloudEvents 1.0 est la spécification CNCF standard pour les enveloppes d'événements — interopérable avec Azure Event Grid, AWS EventBridge, Kafka, RabbitMQ.

### Structure CloudEvents RAMQ

```json
{
  "specversion": "1.0",
  "id": "<MessageId>",
  "source": "/ramq/<domaine>/<operation>",
  "type": "ca.gouv.ramq.<domaine>.<evenement>.v1",
  "subject": "<entite-metier-id>",
  "time": "<EnqueuedTimeUtc>",
  "datacontenttype": "application/json",
  "dataschema": "https://schemas.ramq.gouv.qc.ca/<domaine>/<evenement>/v1.json",
  "ramqitinerary": { "currentstage": "...", "stages": [...], "variables": {} },
  "ramqclaimcheck": { "blobcontainer": "...", "blobname": "...", "sizebytes": 0 },
  "ramqcorrelationid": "<CorrelationId>",
  "traceparent": "<W3C-Trace-Context>",
  "data": { /* payload métier */ }
}
```

### Mode de transport

- **Mode structured** (défaut) : enveloppe complète sérialisée dans le corps du message
- **Mode binary** (optionnel, haute performance) : attributs en `ApplicationProperties`, seul `data` dans le corps

### Stratégie de migration

1. Introduire `MessageEnvelope` record conforme CloudEvents — coexistence avec `MessageTransitContext<T>` existant
2. Tests de contrat snapshot sur `MessageEnvelope` (T4 renforcé)
3. Période de transition v1 (actuel) + v2 (CloudEvents) — lire les deux, écrire v2 uniquement
4. Suppression de l'ancienne sérialisation après 1 release

### Critère de sortie

- [ ] `MessageEnvelope` record conforme CloudEvents 1.0 créé dans `RAMQ.Integration.Envelope`.
- [ ] Extensions `ramqitinerary`, `ramqclaimcheck`, `ramqcorrelationid` définies.
- [ ] Mode structured par défaut — mode binary configurable.
- [ ] Tests snapshot T4 sur `MessageEnvelope` — toute modification casse la PR.
- [ ] Round-trip structured ↔ binary identique.

---

## Tâche P6-T2 — Scission en assemblies (breaking change v3.0.0)

**Origine :** §1.2.3 EMT-DistinguishedEngineerReview.md

### Stratégie de scission

La scission se fait en **4 vagues** pour éviter un big-bang :

| Vague | Packages créés | Risque |
|-------|----------------|--------|
| **V1** | `Abstractions` + `Envelope` — types purs, zéro SDK | 🟢 Faible |
| **V2** | `RoutingSlip` (assembly séparé depuis Phase 5) + `Storage.AzureBlob` + `Journal.AzureTable` | 🟡 Moyen |
| **V3** | `Transport.ServiceBus` + `Hosting.Functions` — migration de l'assembly actuel | 🟠 Élevé |
| **V4** | `Transport.Kafka` + `Transport.RabbitMq` + `Hosting.AspNetCore` | 🟠 Élevé |

### Compatibilité descendante

- Package métapaquet `RAMQ.Integration` (v3.0.0) référençant tous les packages — migration transparente pour les projets qui veulent tout
- Période de coexistence v1 (assembly unique) + v2 (packages séparés) : ≥ 1 release NuGet
- Guides de migration publiés dans `docs/MIGRATION.md`

### Critère de sortie

- [ ] `RAMQ.Integration.Abstractions` compilant sans dépendance Azure SDK.
- [ ] `RAMQ.Integration.Transport.ServiceBus` compilant et passant les tests d'intégration T7 (émulateur).
- [ ] Package métapaquet `RAMQ.Integration` v3.0.0 publié.
- [ ] `docs/MIGRATION.md` mis à jour avec le guide v1 → v2.

---

## Tâche P6-T3 — Adapter Kafka Confluent (`Transport.Kafka`)

**Origine :** §1.2 EMT-DistinguishedEngineerReview.md

### Prérequis

- `IMessagingAdapter` passe la suite de contrat T6 sur `InMemoryAdapter` (Phase 5 ✅)
- `InMemoryAdapter` est l'oracle — si `KafkaAdapter` échoue là où `InMemoryAdapter` réussit, c'est `KafkaAdapter` qui est faux

### Différences sémantiques Service Bus ↔ Kafka à gérer

| Comportement | Service Bus | Kafka |
|---|---|---|
| **Settlement** | Complete / Abandon / DeadLetter | Commit offset uniquement |
| **Sessions (FIFO)** | `SessionId` natif | Partitions — `SessionId` → partition key |
| **DLQ** | File dédiée sur l'entité | Topic DLQ séparé (`<topic>-dlq`) |
| **Idempotence** | `RequiresDuplicateDetection` | Idempotent producer côté Kafka |
| **Retry** | `DeliveryCount` natif | Manuel — compteur dans le message |

### Critère de sortie

- [ ] `KafkaMessagingAdapter` implémentant `IMessagingAdapter`.
- [ ] Suite de contrat T6 verte sur `KafkaAdapter`.
- [ ] ADR documentant les différences sémantiques SB ↔ Kafka et les décisions prises.
- [ ] Tests d'intégration T7 équivalents avec Kafka (Testcontainers + image `confluentinc/cp-kafka`).

---

## Tâche P6-T4 — Adapter RabbitMQ (`Transport.RabbitMq`)

**Origine :** §1.2 EMT-DistinguishedEngineerReview.md · **à livrer après Kafka, jamais en parallèle**

### Différences sémantiques Service Bus ↔ RabbitMQ à gérer

| Comportement | Service Bus | RabbitMQ |
|---|---|---|
| **Settlement** | Complete / DeadLetter | Ack / Nack / Reject |
| **Sessions** | `SessionId` natif | Non natif — Consistent Hash Exchange |
| **DLQ** | Natif | Dead Letter Exchange (`x-dead-letter-exchange`) |
| **Idempotence** | `RequiresDuplicateDetection` | Idempotent consumer pattern |

### Critère de sortie

- [ ] `RabbitMqMessagingAdapter` implémentant `IMessagingAdapter`.
- [ ] Suite de contrat T6 verte sur `RabbitMqAdapter`.
- [ ] ADR documentant les différences sémantiques SB ↔ RabbitMQ.
- [ ] Tests d'intégration avec Testcontainers + `rabbitmq:3-management`.

---

## Tâche P6-T5 — Adapter BackgroundService AKS/ARO (`Hosting.AspNetCore`)

**Origine :** §1.2 EMT-DistinguishedEngineerReview.md

### Contexte

Aujourd'hui EMT est utilisable **uniquement** depuis une Azure Function (trigger Service Bus). `Hosting.AspNetCore` permet aux consumers de fonctionner comme `IHostedService` / `BackgroundService` dans un conteneur AKS ou ARO.

### Interface cible pour le consumer applicatif

```csharp
// Variante AKS + Kafka (identique au code client existant)
builder.Services
    .AddRamqIntegrationAbstractions()
    .AddKafkaTransport(options => options.BootstrapServers = "...")
    .AddAzureBlobStorage(...)
    .AddAzureTableJournal(...)
    .AddHostedMessaging()         // ← Hosting.AspNetCore
    .AddConsumer<ValiderAdresseConsumer>();
```

### Critère de sortie

- [ ] `AddHostedMessaging()` extension enregistrant les consumers comme `IHostedService`.
- [ ] Consumer applicatif **inchangé** entre la variante Functions et la variante BackgroundService.
- [ ] Tests d'intégration : même consumer passe sur `InMemoryAdapter` + `BackgroundService` host.

---

## Tâche P6-T6 — Tests de portabilité (T18 · T19)

**Origine :** §6.1.1 T18 · T19 EMT-DistinguishedEngineerReview.md

### T18 — Matrice de portabilité (3 hôtes × 3 brokers)

```yaml
# pipelines/portability-matrix.yml
strategy:
  matrix:
    functions-servicebus:
      host: functions
      transport: servicebus
    aks-kafka:
      host: aspnetcore
      transport: kafka
    aro-rabbitmq:
      host: aspnetcore
      transport: rabbitmq
```

**Critère de succès :** le même consumer `ValiderAdresseConsumer` passe les tests de contrat T6 sur les 3 combinaisons, sans modification de code métier.

### T19 — Tests CloudEvents 1.0

```csharp
[Trait("Category", "Unitaire")]
public class CloudEventsContractTests
{
    [Fact] public void MessageEnvelope_EstConformeCloudEvents_StructuredMode() { }
    [Fact] public void MessageEnvelope_RoundTrip_StructuredVsBinary_Identique() { }
    [Fact] public void ExtensionsRAMQ_SontAcceptées_ParParserTiers() { }
    [Fact] public void traceparent_EstPropagé_DansLEnveloppe() { }
}
```

### Critère de sortie

- [ ] Pipeline matrice CI (3 combinaisons cœur) — nightly bloquant.
- [ ] T19 CloudEvents : ≥ 4 tests `Category=Unitaire` verts.

---

## Résumé des tâches

| ID | Tâche | Priorité | Durée est. | Prérequis | Statut |
|----|-------|----------|------------|-----------|--------|
| P6-T1 | CloudEvents 1.0 — `MessageEnvelope` + extensions RAMQ | 🔴 Haute | 2 semaines | Phase 5 ✅ | ⛾ Non démarré |
| P6-T2 | Scission 10 packages NuGet (4 vagues) | 🔴 Haute | 4 semaines | P6-T1 | ⛾ Non démarré |
| P6-T3 | Adapter Kafka (`Transport.Kafka`) | 🟠 Haute | 3 semaines | P6-T2 V3 | ⛾ Non démarré |
| P6-T4 | Adapter RabbitMQ (`Transport.RabbitMq`) — **après Kafka** | 🟠 Haute | 3 semaines | P6-T3 ✅ | ⛾ Non démarré |
| P6-T5 | `Hosting.AspNetCore` BackgroundService | 🟡 Moyenne | 2 semaines | P6-T2 V3 | ⛾ Non démarré |
| P6-T6 | Tests portabilité T18 + CloudEvents T19 | 🟡 Moyenne | 2 semaines | P6-T3 + P6-T5 | ⛾ Non démarré |

**Total estimé : 10-16 semaines** · **Statut : ⛾ Conditionnel à ADR-001 option B**

---

## Ordre d'exécution recommandé

```
Semaine 1-2 :
  P6-T1 (CloudEvents 1.0 MessageEnvelope)

Semaine 2-5 :
  P6-T2 Vague 1 (Abstractions + Envelope)
  P6-T2 Vague 2 (RoutingSlip + Storage + Journal)
  P6-T2 Vague 3 (Transport.ServiceBus + Hosting.Functions)

Semaine 5-8 :
  P6-T3 (Adapter Kafka uniquement)

Semaine 8-11 :
  P6-T5 (Hosting.AspNetCore BackgroundService)
  P6-T6 (Tests portabilité matrice Functions+SB / AKS+Kafka)

Semaine 11-14 :
  P6-T4 (Adapter RabbitMQ — après validation Kafka)
  P6-T2 Vague 4 (Transport.RabbitMq)

Semaine 14-16 :
  P6-T6 complet (matrice 3×3 + T19 CloudEvents)
```

---

## Condition de clôture Phase 6

Phase 6 est terminée quand :

1. ⚾ 10 packages NuGet publiés (`v3.0.0`).
2. ⛾ `MessageEnvelope` conforme CloudEvents 1.0 — round-trip structured ↔ binary.
3. ⛾ `KafkaMessagingAdapter` passe la suite de contrat T6.
4. ⛾ `RabbitMqMessagingAdapter` passe la suite de contrat T6.
5. ⛾ `ValiderAdresseConsumer` (exemple) fonctionne sans modification sur les 3 combinaisons hôte×transport.
6. ⛾ Pipeline matrice CI (3 combinaisons cœur) vert en nightly.
7. ⛾ `docs/MIGRATION.md` v1 → v2 publié.

> **Condition de non-démarrage :** si ADR-001 conclut à l'option A (assumer le lock-in Azure) ou si aucun cas d'usage non-Azure concret n'est identifié, cette phase est **annulée**. Les phases 1-5 couvrent déjà 80 % de la valeur pour EMT dans son contexte actuel Azure Functions + Service Bus.

---

## Vue d'ensemble complète de la roadmap

```
Phase 1 ✅ — 🔴 Bloquant (4-6 sem.)
  ADRs · SemVer · CI bloquante · ActivitySource · Fitness functions

Phase 2 ✅ — 🟠 Majeur (10-14 sem.)
  Tests 86/86 · Tracing 9/10 spans · 8 métriques OTEL
  failure-modes.md · claim-check orphelin · IMessageSettlementActions

Phase 3 ✅ — 🟡 Mineur (3-5 sem.)
  Tests contrat interface · IdempotentPublish · PublishTimeout
  DeserializationResult · Journal Task.WhenAll · EndpointResolver O(1)

Phase 4 ⛾ — 🟡 Moyen (4-6 sem.)
  Service Bus Emulator · traceparent · deserialization_failures_total
  Benchmarks BenchmarkDotNet · Tests charge/chaos · extensibility.md
  operational-envelope.md

Phase 5 ⚾ — 🟠 Élevé (6-10 sem.) — Portée révisée mai 2026
  Fondations ✅ livrées (27 avr.) · Routing Slip natif v2.0 ⬜
  IRoutingSlipActivity<TArgs> · SlipEnvelope · RoutingSlipExecutor · breaking change MAJOR v2.0

Phase 6 ⛾ — 🔴 Très élevé (10-16 sem.) — conditionnel ADR-001 option B
  CloudEvents 1.0 · 10 packages NuGet (v3.0.0) · Kafka · RabbitMQ
  BackgroundService AKS/ARO · Matrice portabilité 3×3

Total roadmap : 37-57 semaines
```
