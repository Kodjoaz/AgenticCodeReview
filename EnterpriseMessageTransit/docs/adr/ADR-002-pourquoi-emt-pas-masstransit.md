# ADR-002 — Pourquoi EnterpriseMessageTransit et pas MassTransit

## Statut

Accepté

## Contexte

Lors de la conception initiale d'EMT, l'équipe a évalué l'adoption de **MassTransit** comme framework de
messagerie généraliste, plutôt que de construire une bibliothèque interne.

MassTransit est une bibliothèque OSS mature, multi-broker, avec support Service Bus, Saga, outbox, etc.

## Décision

**Construire EnterpriseMessageTransit** — bibliothèque interne RAMQ — plutôt qu'adopter MassTransit.

## Alternatives écartées

| Limite MassTransit | Impact RAMQ |
|---|---|
| **L1 — Azure Functions Isolated Worker** : MassTransit cible `IHostedService`/`BackgroundService`. Son intégration dans le modèle Azure Functions Isolated Worker requiert un host builder complet — ce qui contredit le modèle scale-to-zero et la facturation à l'usage propres aux Functions. Le package `MassTransit.Azure.Functions` existe mais reste expérimental (pas de support LTS garanti). | Les équipes RAMQ développent exclusivement en Azure Functions Isolated. Adopter MassTransit aurait imposé soit de migrer vers des conteneurs (décision non retenue), soit d'opérer sur un support non garanti. |
| **L2 — Politique RBAC « least privilege »** : MassTransit attend des droits `Manage` au niveau du namespace Service Bus pour créer les entités (queues, topics, subscriptions) au démarrage. RAMQ applique une politique de moindre privilège : une identité managée ne reçoit que `Send` ou `Listen` sur une entité spécifique, jamais `Manage` sur le namespace. Ces droits sont structurellement incompatibles. | Incompatibilité non contournable sans fork du code de topologie MassTransit. |
| **L3 — Gouvernance OSS à cadence volatile** : MassTransit a modifié son modèle de financement et ses conventions d'API à chaque version majeure (v7→v8→v9). EMT est une bibliothèque plateforme RAMQ ciblant un support 5–10 ans. Une dépendance OSS à gouvernance externe introduit un risque de version lock ou de re-migration non planifiée. | RAMQ préfère maîtriser le contrat de sa bibliothèque de messagerie critique. |
| **L4 — Journal d'audit CAI** : MassTransit ne propose pas de journal d'audit natif répondant aux exigences CAI de RAMQ (traçabilité bout-en-bout, corrélation `MessageId`/`SessionId`, auditabilité réglementaire persistée). | Le Message Transit Journal est une exigence réglementaire, pas un choix de confort. |

## Conséquences

- EMT est une bibliothèque interne — RAMQ en assume la gouvernance complète (breaking changes, SemVer, tests).
- Les fonctionnalités de MassTransit non couvertes (outbox, saga distribué multi-service) sont hors périmètre
  EMT pour l'horizon 0–24 mois.
- La dette principale de ce choix est le coût de maintenance à long terme d'une bibliothèque maison.

## Conditions de révision

Cette décision doit être réévaluée si **l'une** des conditions suivantes est satisfaite :

1. MassTransit publie un support Azure Functions Isolated Worker **first-class** avec LTS garanti (surveiller v10+).
2. RAMQ abandonne Azure Functions comme modèle d'hôte principal au profit de conteneurs (AKS/ARO fulltime).
3. RAMQ assouplit sa politique RBAC Service Bus pour autoriser `Manage` sur namespace en Production.
4. Le coût de maintenance interne d'EMT (incidents × temps résolution) dépasse le coût d'adoption + migration MassTransit sur une période glissante de 12 mois.

**Fréquence de révision recommandée :** annuelle, synchronisée avec la planification des phases EMT.

## Signataires et date

- ___________ (Architecte) — 2026-04-27
- ___________ (Lead technique) — 2026-04-27
