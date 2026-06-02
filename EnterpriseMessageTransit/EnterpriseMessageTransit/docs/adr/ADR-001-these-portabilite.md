# ADR-001 — Thèse de portabilité EnterpriseMessageTransit

## Statut

Accepté

## Contexte

EnterpriseMessageTransit expose des abstractions internes (`IMessagingProvider`, `IMessagingAdapter`,
`IMessagingAdapter`) qui pourraient laisser supposer une portabilité multi-broker (Kafka, RabbitMQ, Event Hubs…).
Cette abstraction est actuellement **non documentée et non livrée** : seul Azure Service Bus est implémenté.

La question ouverte concerne également le **périmètre des hôtes supportés** : la bibliothèque est utilisée dans
des Azure Functions (Isolated Worker), mais les équipes RAMQ envisagent des déploiements sur AKS / ARO
via `BackgroundService` pour certains contextes.

## Décision

**Option A — Documenter et assumer le périmètre réel.**

- **Broker unique supporté :** Azure Service Bus (Premium tier).
- **Hôtes supportés :** Azure Functions (Isolated Worker) **et** AKS / ARO (`BackgroundService`).
- `IMessagingProvider`, `IMessagingAdapter` restent `internal` : ils ne constituent **pas** une surface
  d'extensibilité publique. Un consommateur de la bibliothèque ne peut pas injecter un autre broker.
- **Multi-broker** (Kafka Confluent, Azure Event Hubs, RabbitMQ, etc.) : **hors périmètre** pour l'horizon
  0–24 mois. Décision explicitement écartée dans cette version.

## Alternatives écartées

| Alternative | Raison du rejet |
|---|---|
| **Statu quo** (Option B) — ambiguïté maintenue | Les abstractions orphelines créent de la dette, des PRs orphelins et des attentes non fondées chez les équipes consommatrices. |
| **Multi-broker public** — rendre `IMessagingAdapter` public | Nécessite un contrat d'extensibilité stable, des tests d'intégration multi-broker, et une gouvernance de dépendances que RAMQ n'est pas prête à assumer sur l'horizon 0–24 mois. |
| **CloudEvents** — adopter le format standard pour l'interopérabilité | Non retenu : EMT est une bibliothèque interne RAMQ. L'interopérabilité cross-plateforme n'est pas requise dans ce plan. Réévaluer si RAMQ adopte un hub d'événements inter-organisations. |

## Conséquences

- Le découplage de l'adapter Functions (P2 — `IMessageActions`) peut progresser sans impliquer de multi-broker.
- Les types `IMessagingProvider` et `IMessagingAdapter` doivent être marqués `internal` dans les phases suivantes
  (actuellement `public` par erreur — Phase 2 ou Phase 3).
- La scission en assemblies multiples n'est pas activée dans ce plan (réévaluation Phase 4+).
- Les équipes qui veulent déployer sur AKS / ARO doivent utiliser le `BackgroundService` et le configurer via
  `IConsumerConfigurationService` — aucun code EMT spécifique à AKS n'est requis.

## Conditions de révision

- RAMQ adopte un second broker de messagerie (Kafka Confluent, Azure Event Hubs en mode streaming, etc.)
  dans son plan d'architecture à moyen terme.
- Un besoin d'interopérabilité cross-organisations (CloudEvents) est formalisé.

## Signataires et date

- Kodjo (Architecte) — 2026-04-27
- GitHub Copilot (Lead technique) — 2026-04-27
- GitHub Copilot (Principal engineer) — 2026-04-27
