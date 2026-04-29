# Revue d'ingénierie — EnterpriseMessageTransit (Distinguished Engineer Review)

> **Date :** 24 avril 2026 (révision 3 — focus sur les points encore ouverts)
> **Périmètre :** Librairie `RAMQ.COM.EnterpriseMessageTransit` (net8.0)
> **Angle :** Revue stratégique, au-delà de la ligne de code — alignement produit, portabilité, risque long terme, gouvernance
> **Conventions :** 🔴 Bloquant (stratégique) · 🟠 Majeur · 🟡 Mineur · 🟢 Positif (à préserver) · 🧭 Orientation
> **Lecteurs cibles :** Lead & Senior engineers (exécution), architectes & principal engineers (orientations), développeurs juniors (sections pédagogiques identifiées par 💡)
>
> **Parti pris de cette révision.** Les points déjà corrigés suite aux revues Senior et Lead ne sont **plus** mentionnés — ils sortent du périmètre d'analyse pour que l'équipe puisse concentrer son énergie sur les **sujets encore ouverts**. Toute mention subsiste seulement si elle éclaire un sujet ouvert (ex. un pattern réussi qu'il ne faut pas casser pendant un refactor à venir).

---

## 0. Lecture de cette revue

Les revues [EMT-SeniorEngineerReview.md](EMT-SeniorEngineerReview.md) et [EMT-LeadEngineerReview.md](EMT-LeadEngineerReview.md) couvrent déjà, avec un grand sérieux, le plan **code et conception locale** (bugs, SRP, performance ponctuelle, testabilité). Cette revue **ne les reproduit pas**. Elle se place volontairement à deux niveaux au-dessus :

| Niveau | Question posée | Revue dédiée |
|---|---|---|
| **Code** | « Est-ce que cette ligne est correcte ? » | Senior review |
| **Conception locale** | « Est-ce que ce module est bien découpé ? » | Lead review |
| **Architecture applicative** | « Est-ce que cette librairie fait ce qu'elle promet, de manière robuste ? » | Lead review (partiellement) |
| **Plateforme & produit** | « Est-ce que cette librairie doit exister sous cette forme dans 3 ans ? À quel coût organisationnel ? Qu'est-ce qui la rendra inutilisable ou indispensable ? » | **Cette revue** |

Les recommandations sont classées par **horizon** (immédiat / 6 mois / 12-24 mois), et non uniquement par sévérité, car le critère pertinent à ce niveau est la **fenêtre de décision** : certaines orientations coûtent peu aujourd'hui et deviennent irréversibles dans un an.

💡 **Pour les développeurs juniors** — ce document parle peu de code et beaucoup de *décisions*. Chaque décision d'architecture est un pari qui se paye avec le temps. L'objectif de cette revue n'est pas de trouver des bugs mais d'identifier *les paris qui sont en train d'être pris implicitement*, et de rendre explicite leur coût futur.

### 0.1 Dashboard des points ouverts (24 avril 2026)

Cette révision **ne liste que les points encore ouverts**. Les corrections livrées suite aux revues Senior et Lead sont considérées comme acquises et sortent du périmètre d'analyse.

| # | Sujet ouvert | Criticité | Section détaillée |
|---|---|---|---|
| O1 | Thèse de portabilité multi-hôte (Azure Functions / AKS / ARO) et multi-broker (Service Bus / Kafka Confluent / RabbitMQ) non posée — architecture cible absente | 🔴 Bloquant | [§1.2](#12--bloquant-stratégique--absence-de-thèse-de-portabilité-et-darchitecture-cible) |
| O2 | Positionnement vs écosystème non documenté (rationale anti-MassTransit, absence CloudEvents) | 🟠 Majeur | [§1.3](#13--positionnement-vis-à-vis-de-lécosystème) |
| O3 | Contrat pivot `MessageTransitContext<T>` non versionné et mélangé avec le runtime | 🔴 Bloquant | [§3.1](#31--pari-risqué--messagetransitcontextt-est-un-contrat-sérialisé-et-un-modèle-runtime) |
| O4 | Saga / routing-slip mélangée avec l'infrastructure messaging (`BaseConsumer` god-class) | 🟠 Majeur | [§3.2](#32--la-saga-routing-slip-mérite-son-propre-module) |
| O5 | Couplage `AzureFunctionMessagingAdapter` → `Microsoft.Azure.Functions.Worker` | 🟠 Majeur | [§3.3](#33--couplage-azurefunctionmessagingadapter--microsoftazurefunctionsworker) |
| O6 | `IMessageTransit` sous-dimensionné — abstraction fuite via cast `RawMessage` | 🟠 Majeur | [§3.4](#34--imessagetransit-sous-dimensionné--symptôme-dabstraction-prématurée) |
| O7 | Surface publique non maîtrisée (80+ types `public` sans `InternalsVisibleTo`) | 🟠 Majeur | [§2.1](#21--surface-dapi-publique-non-maîtrisée) |
| O8 | Extensibilité fictive (7 interfaces, 1 implémentation chacune) | 🟡 Mineur | [§2.2](#22--extensibilité-par-interface-non-utilisée--extensibilité-fictive) |
| O9 | Idempotence producteur non garantie (`RequiresDuplicateDetection` non prérequis) | 🟡 Mineur | [§4.3](#43--idempotence-producteur-non-garantie) |
| O10 | Timeout global `PublishAsync` non borné | 🟡 Mineur | [§4.4](#44--timeout-global-publishasync-non-borné) |
| O11 | `DeserializationResult<T>` livré mais call-sites, métriques et politique DLQ manquants | 🟡 Mineur | [§4.2](#42--déserialisation--propagation-du-type-result-à-finaliser) |
| O12 | Modèle de défaillance sans runbook ni matrice de décision ; claim-checks orphelins non nettoyés | 🟠 Majeur | [§4.1](#41--modèle-de-défaillance-implicite--à-rendre-explicite) |
| O13 | Enveloppe opérationnelle non documentée (débit, latence p99, seuils) | 🟠 Majeur | [§5.1](#51--absence-denveloppe-de-fonctionnement-documentée) |
| O14 | Journal synchrone O(n) dans `PublishBatchAsync` | 🟡 Mineur | [§5.2](#52--journal-synchrone-sur-chemin-batch--on-appels-table-storage) |
| O15 | `EndpointResolver.TryResolve` — allocations LINQ répétées | 🟡 Mineur | [§5.3](#53--endpointresolvertryresolve--allocations-linq-répétées) |
| O16 | Aucun projet de tests, pas de Service Bus Emulator, pas de tests de contrat | 🟠 Majeur | [§6.1](#61--testabilité--inventaire-des-seams-mais-absence-de-stratégie) |
| O17 | Distributed tracing absent (pas d'`ActivitySource`) | 🟠 Majeur | [§6.2](#62--observabilité--socle-métriques-en-place-distributed-tracing-toujours-absent) |
| O18 | `CHANGELOG.md`, `<Version>`, ADR, `CONTRIBUTING.md` absents | 🔴 Bloquant | [§7.1](#71--absence-de-semver-de-changelog-et-dadr) |
| O19 | Stratégie NuGet / publication / support N, N-1 non documentée | 🟠 Majeur | [§7.2](#72--ecosystème-nuget-et-stratégie-de-publication) |
| O20 | Stratégie de dépréciation `[Obsolete]` non formalisée | 🟠 Majeur | [§7.3](#73--stratégie-de-dépréciation-manquante) |

💡 **Pour un développeur junior — comment utiliser ce tableau.** Chaque ligne est un **sujet à traiter**, avec un identifiant stable (O1…O20) que tu retrouveras dans la feuille de route ([§8.3](#83-plan-progressif-par-phases)). La **criticité** répond à la question « si on ne fait rien, qu'est-ce qui casse et quand ? » :
>
> - 🔴 Bloquant = doit être traité avant les autres phases, sinon tout le plan s'effondre.
> - 🟠 Majeur = peut attendre une phase dédiée, mais **pas reporté indéfiniment**.
> - 🟡 Mineur = à traiter quand une phase l'emporte avec elle naturellement.

---

## Table des matières

1. [Thèse produit et positionnement](#1-thèse-produit-et-positionnement)
2. [Qualité du code — perspective plateforme](#2-qualité-du-code--perspective-plateforme)
3. [Design et architecture — paris structurants](#3-design-et-architecture--paris-structurants)
4. [Robustesse et fiabilité — modèle de défaillance](#4-robustesse-et-fiabilité--modèle-de-défaillance)
5. [Performance et scalabilité — enveloppe de fonctionnement](#5-performance-et-scalabilité--enveloppe-de-fonctionnement)
6. [Testabilité et observabilité — contrats opérationnels](#6-testabilité-et-observabilité--contrats-opérationnels)
7. [Gouvernance, versioning, écosystème](#7-gouvernance-versioning-écosystème)
8. [Synthèse, feuille de route et décisions à prendre](#8-synthèse-feuille-de-route-et-décisions-à-prendre)

---

## 1. Thèse produit et positionnement

### 1.1 🧭 Ce que EMT est réellement aujourd'hui

En dépouillant le code et les revues existantes, EMT n'est **pas** une « librairie Azure Service Bus ». C'est, de fait, **trois produits superposés** dans un même assembly :

| Produit implicite | Matérialisation dans le code | Maturité |
|---|---|---|
| **P1 — Un SDK de messaging abstrait** | `IMessagingProvider`, `IStorageProvider`, `IJournalProvider`, `IMessageTransit`, `MessagingEntityType` (Queue/Topic/Exchange/Channel) | Moyenne |
| **P2 — Un adapter opinioné Azure Functions ↔ Service Bus** | `AzureFunctionMessagingAdapter`, `AzureMessagingProvider`, `ServiceBusSenderCache`, `RetryPolicyHandler` | Élevée |
| **P3 — Un moteur de routing-slip / saga avec claim-check et itinéraire** | `MessageTransitContext` (`CurrentStage`, `Tokens`, `Variables`), `BaseConsumer.RouteToNextStageAsync`, `AppSettings.Itinerary`, `TransitItineraryException` | Élevée, fortement couplée RAMQ |

**Pourquoi c'est important :** ces trois produits ont des cycles de vie, des publics, et des contraintes de compatibilité *différents*. Les empiler dans un seul package force le plus fragile (P3, spécifique au métier RAMQ) à dicter le rythme d'évolution du plus générique (P1). C'est la cause racine de plusieurs observations faites par la revue Lead (`MessageTransitContext` comme « god object », couplage `BaseConsumer` ↔ saga, `BindContext(object, object)` fragile) : ce sont des symptômes d'une **frontière de produit manquante**, pas des défauts locaux.

#### 1.1.1 Pourquoi les patterns portés par EMT sont *spécifiques RAMQ* — analyse point par point

Cette sous-section répond à la question : « En quoi ces patterns ne sont-ils pas simplement du messaging générique ? Pourquoi parler de *patterns d'intégration RAMQ* ? »

💡 **Pour les développeurs juniors.** Un pattern est « spécifique » non pas parce qu'il est unique au monde, mais parce qu'**il répond à une contrainte que tout le monde n'a pas**. Un hôpital et une application mobile de livraison utilisent tous les deux du messaging, mais les contraintes réglementaires, de rejeu et de confidentialité de l'hôpital imposent des choix qu'un livreur n'aurait jamais pris.

| # | Pattern / choix de EMT | Contrainte RAMQ à l'origine | Pourquoi ce n'est pas « du messaging générique » |
|---|---|---|---|
| **R1** | **Itinéraire déclaratif multi-étapes** (`AppSettings.Itinerary` avec liste de `EndpointSettings` résolues par `CurrentStage`) | RAMQ opère des **processus métier inter-domaines** (ex. traitement d'une demande d'assurance, coordination pharmacie/individu/dispensateur). Un même message doit voyager séquentiellement ou conditionnellement à travers plusieurs domaines *aux périmètres de sécurité distincts*, chacun avec ses propres règles de validation. | Un bus générique (Service Bus, Kafka) ne porte pas de notion d'itinéraire. Les frameworks génériques (MassTransit sagas, NServiceBus) modélisent l'itinéraire comme un **graphe en mémoire côté orchestrateur**. EMT porte l'itinéraire **dans le message lui-même** (pattern *Routing Slip*) parce qu'aucun domaine RAMQ ne peut héberger l'orchestrateur central sans violer les cloisonnements de sécurité. |
| **R2** | **Claim-check systématique au-delà de 256 Ko** (`Messaging/ClaimCheckOptions.cs`, seuil aligné sur la limite Service Bus Standard) | Les messages RAMQ transportent souvent des **pièces jointes médicales** (formulaires scannés, résultats de laboratoire, images) qui dépassent couramment les 1-10 Mo. Ces pièces doivent aussi être stockées dans un **store auditable soumis à rétention légale** distincte du bus. | Un système e-commerce générique évite simplement les gros messages côté producteur. RAMQ *doit* les transporter, *doit* les auditer séparément du canal de transit, et *doit* appliquer une politique de rétention cohérente avec la loi sur la santé et les services sociaux. Le claim-check n'est pas une optimisation — c'est un **requirement réglementaire** déguisé en pattern technique. |
| **R3** | **Journal systématique des envois/réceptions** (`IJournalProvider`, `AzureJournalProvider` → Azure Table, `JournalEntry` avec `CorrelationId`, `MessageId`, `Consumer`, `Action`, `Target`) hors chemin critique (pattern A5) | Les échanges inter-domaines RAMQ sont **auditables** : il faut pouvoir reconstruire *qui a envoyé quoi à qui, quand, et avec quel résultat*, pendant plusieurs années, pour répondre à une requête CAI (Commission d'accès à l'information) ou une enquête interne. | Les librairies génériques considèrent la journalisation comme *optionnelle* (sidecar observabilité). EMT en fait un **citoyen de première classe**, journalisé à chaque envoi/réception, avec des champs dictés par le métier RAMQ (Consumer/Action) plutôt que par l'infrastructure (`messaging.destination`, `messaging.system`). |
| **R4** | **Conventions de nommage `Consumer.Action`** (`MessagePropertyKeys`, propriétés routées sur Service Bus via `Subscription.Consumer` / `Subscription.Action`, `$filter` côté SB) | Chaque domaine RAMQ (assurance, pharmacie, dispensateur, individu, régie) expose un **contrat d'intégration stable** en termes métier (`Consumer = "Individu"`, `Action = "ValiderAdresse"`), indépendant du topic physique. Ce niveau d'indirection permet de **refactoriser la topologie Service Bus sans casser les consommateurs applicatifs**. | Un projet générique utiliserait directement le nom du topic/queue dans le code applicatif. EMT ajoute une couche d'indirection (`IEndpointResolver`, `EndpointSettings.Target`, `Subscription`) parce que la **topologie Service Bus est gérée par une équipe infra distincte** de celles qui écrivent les consumers, et que les noms physiques évoluent indépendamment. |
| **R5** | **Sessions Service Bus activées par endpoint** (`EndpointSettings.Endpoint.EnableSession`, `SessionId` obligatoire à la publication quand activé) | Certains flux RAMQ exigent un **traitement ordonné par entité métier** (ex. toutes les actions concernant un même assuré doivent être traitées dans l'ordre par un seul worker). Les sessions Service Bus apportent cette garantie sans orchestrateur externe. | D'autres plateformes activent les sessions au cas par cas ou les évitent (coût opérationnel). RAMQ en fait une **propriété déclarative de l'endpoint**, transparente pour le producteur (qui fournit juste un `SessionId`). |
| **R6** | **`MessageTransitContext.Variables` comme porte-clés typé** (`GetVariable<T>`, `GetApplicationPropertyValue<T>`) | Un processus inter-domaines RAMQ accumule du **contexte métier** au fil des stages (ex. `DemandeId`, `AssureId`, `DateEffet`, flags de validation franchis) qui doit accompagner le message sans être connu du transport. | Une librairie générique ne modélise pas ce porte-clés ; l'application le réinvente systématiquement. EMT le standardise, ce qui a un coût (sérialisation du dictionnaire) mais évite qu'on invente 15 formats dans 15 consumers. |
| **R7** | **Drapeau d'idempotence `__FinalStageCompleted` dans `Variables`** pour éviter la double complétion d'une saga RAMQ | Les sagas RAMQ peuvent être **rejouées** (replay depuis DLQ, replay depuis archive) sans que cela déclenche deux fois l'effet final métier (ex. double émission d'un paiement). Un flag applicatif est nécessaire car Service Bus seul ne garantit pas l'exactly-once. | Les frameworks génériques s'en remettent à l'idempotence du *handler* applicatif. RAMQ ajoute une **protection au niveau de la saga** parce que les handlers finaux invoquent souvent des systèmes **legacy WCF** non-idempotents (voir [docs/sender.md](sender.md) pour le scénario TDF). |
| **R8** | **Intégration WCF / legacy adapter** (absent du code ouvert mais omniprésent dans les scénarios d'intégration documentés, cf. [docs/ScenarioIntegration-TDF.md](EnterpriseMessageTransit/ScenarioIntegration-TDF.md)) | Une part significative des domaines RAMQ expose encore des services **WCF SOAP** non-migrables à court terme. EMT doit pouvoir alimenter un adapter qui traduit un message transit → appel WCF et garde la trace d'audit. | Aucun framework .NET moderne ne cible ce cas d'usage. C'est un **différenciateur RAMQ spécifique** qui justifie à lui seul l'existence d'EMT. |
| **R9** | **Claim-check + sessions + routing-slip cohabitent** dans le même pipeline | Les scénarios RAMQ inter-domaines cumulent les trois contraintes : gros payload (R2), ordre garanti (R5), multi-étapes (R1). | Aucune librairie générique ne combine les trois nativement sans écrire soi-même la glu. EMT livre cette glu. |

**Conclusion.** EMT porte une **combinaison** de patterns (Routing Slip + Claim-Check + Journal d'audit + Sessions ordonnées + Nommage métier Consumer/Action + intégration WCF legacy) qui n'a pas d'équivalent clé-en-main dans l'écosystème .NET. Chaque pattern isolé existe ailleurs ; c'est **leur combinaison**, motivée par les contraintes réglementaires (santé, CAI, rétention) et techniques (WCF legacy, cloisonnement multi-domaine) de RAMQ, qui définit l'identité du produit.

💡 **Pour un développeur junior — ce que cela change pour toi.** Quand tu écris du code dans EMT, garde en tête que **les patterns que tu vois ne sont pas décoratifs**. Ils existent parce que quelqu'un ne peut pas juste « faire simple ». Avant de proposer un raccourci (« pourquoi ne pas faire un `SendMessageAsync` direct ? »), demande à quel pattern RAMQ (R1-R9) ton raccourci renonce.

### 1.2 🔴 Bloquant stratégique — Absence de thèse de portabilité et d'architecture cible

EMT revendique une architecture « transport-agnostic » (abstractions `IMessagingProvider`, `IStorageProvider`, `IJournalProvider`, `IMetricsProvider`, `IRetryPolicyHandler`) mais son code courant **ne tient pas cette promesse** :

- Le `.csproj` exclut `Messaging/Providers/Kafka/**` ; aucun autre transport n'est implémenté.
- L'enum `MessagingEntityType` réserve `Exchange` (RabbitMQ/AMQP) et `Channel` (Kafka/NATS) mais les valeurs ne sont câblées nulle part.
- Le *contrat public* fuit Service Bus partout où ça compte :
  - `IMessagingAdapter.BindContext(object, object)` attend un `ServiceBusReceivedMessage` et un `ServiceBusMessageActions` (cast `as ServiceBusReceivedMessage` explicite dans `AzureFunctionMessagingAdapter`).
  - `AzureFunctionMessageTransit.RawMessage` expose directement la `ServiceBusReceivedMessage`.
  - `AzureFunctionMessagingAdapter` dépend de `Microsoft.Azure.Functions.Worker` (`ServiceBusMessageActions`).
  - `RetryPolicyHandler.HandleImmediateRetryAsync(ServiceBusReceivedMessage, object, …)` **signe** Service Bus dans son contrat public.
- Le *packaging* est monolithique : un seul assembly (`RAMQ.COM.EnterpriseMessageTransit`) porte abstractions, adapter Functions, provider Azure, journal Azure Table, storage Azure Blob et circuit breaker.

💡 **Pour les développeurs juniors — pourquoi « fuite » ?** Une abstraction *fuit* quand son consommateur ne peut pas l'utiliser sans *connaître* l'implémentation concrète. Ici : un consumer applicatif qui veut lire `DeliveryCount` ou `ApplicationProperties` **doit caster** `IMessageTransit` en `AzureFunctionMessageTransit` pour atteindre `RawMessage.DeliveryCount`. À partir de ce cast, tout le code applicatif dépend de Service Bus — même s'il a été écrit « contre une interface ».

#### 1.2.1 Le besoin réel de portabilité RAMQ

La stratégie RAMQ pour les 24-36 mois à venir, telle qu'elle ressort des discussions d'architecture et des documents de référence, tient sur deux axes **simultanés** :

**Axe A — Portabilité d'hébergement.** Une même application d'intégration doit pouvoir être déployée indifféremment sur :

| Hôte cible | Contexte d'utilisation | Contrainte dérivée sur EMT |
|---|---|---|
| **Azure Function App** (Consumption, Premium, Flex) | Charges événementielles, scale-to-zero, coût à l'usage. Cas d'usage : triggers Service Bus classiques, flux à débit variable. | Nécessite l'adapter `Microsoft.Azure.Functions.Worker` actuel. Doit rester le *chemin chaud* car c'est le plus utilisé aujourd'hui. |
| **Conteneurs sur AKS** (Azure Kubernetes Service) | Charges à haut débit soutenu, co-localisation avec d'autres services RAMQ, contrôle fin du réseau (private endpoints, NSG). | Nécessite un adapter `BackgroundService` / `IHostedService` qui consomme directement le SDK (Service Bus, Kafka ou RabbitMQ) sans dépendre de Functions Worker. |
| **Conteneurs sur ARO** (Azure Red Hat OpenShift) | Exigences de souveraineté ou de cohérence avec d'autres charges OpenShift existantes, besoin d'un plan de reprise hors Azure Functions. | Idem AKS. De plus, l'image conteneur doit être **multi-architecture** et ne pas dépendre de binaires Windows-only. |

💡 **Pour un junior — pourquoi trois hôtes ?** Azure Functions est pratique mais limité (cold start, quotas, modèle worker spécifique). AKS donne le contrôle mais coûte plus cher en infra. ARO est un choix politique/réglementaire (cohérence OpenShift, portabilité éventuelle hors Azure). RAMQ veut pouvoir **choisir au cas par cas** selon le profil de charge, sans réécrire le code applicatif — c'est l'essence même du besoin de portabilité d'hôte.

**Axe B — Portabilité de transport (messaging backend).** Les brokers supportés doivent à terme inclure :

| Broker cible | Justification RAMQ | Contrainte dérivée sur EMT |
|---|---|---|
| **Azure Service Bus** (actuel) | Topologie déjà en place, sessions + duplicate detection, intégration native Functions. | Doit rester le chemin de référence. |
| **Kafka Confluent** (Cloud ou Platform) | Débit élevé soutenu, rétention configurable (hours → weeks), replay natif, standard de facto pour les flux analytiques et inter-plateformes. Cas d'usage : publication d'événements métier RAMQ vers un data lake / plateforme analytique. | Modèle de consommation différent (pull, offsets, consumer groups). `IMessagingProvider` doit exposer un contrat qui fonctionne *des deux côtés*. |
| **RabbitMQ** (on-prem ou AmazonMQ, via AMQP 0.9.1 ou 1.0) | Scénarios d'intégration avec des partenaires qui imposent AMQP / RabbitMQ (ex. certains fournisseurs du réseau de la santé). Topologie *exchange* / *routing key* / *queue*. | Nécessite de câbler l'enum `MessagingEntityType.Exchange` actuellement inerte, et de respecter la sémantique AMQP (ack/nack/reject, reenqueue). |

**Axe C — Format de message portable.** Aucun des deux axes précédents n'est tenable si le format de message est spécifique à un transport. EMT doit adopter une **enveloppe neutre**, versionnée, interopérable : c'est le rôle de CloudEvents 1.0 (détaillé en [§1.3](#13--positionnement-vis-à-vis-de-lécosystème)).

#### 1.2.2 Architecture cible — vue d'ensemble

```
┌───────────────────────────────────────────────────────────────────────────────┐
│                         Code applicatif RAMQ                                   │
│           (Producers + Consumers des domaines métier)                          │
│                                                                                │
│   ne référence QUE :   RAMQ.Integration.Abstractions                           │
│                        RAMQ.Integration.Envelope                               │
└───────────────────────────────────────────────────────────────────────────────┘
                 │                                     │
                 ▼                                     ▼
┌──────────────────────────────┐        ┌──────────────────────────────────┐
│ RAMQ.Integration.Abstractions │        │ RAMQ.Integration.Envelope         │
│ ---------------------------- │        │ --------------------------------- │
│  IMessageTransit (enrichi)   │        │  MessageEnvelope (CloudEvents)   │
│  IMessagingProvider          │        │  SchemaVersion, Headers, Id, ... │
│  IStorageProvider             │        │  Sérialisation JSON stable       │
│  IJournalProvider             │        │                                   │
│  IMetricsProvider / Tracing  │        │                                   │
│  IMessageActions (typée)      │        │                                   │
└──────────────────────────────┘        └──────────────────────────────────┘
       ▲        ▲        ▲                            ▲
       │        │        │                            │
       │        │        └──────────────┐             │
       │        │                       │             │
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ RAMQ.Integration │   │ RAMQ.Integration │   │ RAMQ.Integration │
│ .RoutingSlip     │   │ .Transport       │   │ .Transport       │
│                  │   │ .ServiceBus      │   │ .Kafka           │
│ Saga engine pur, │   │                  │   │                  │
│ indépendant du   │   │ Provider SB      │   │ Provider Kafka   │
│ transport        │   │ + claim-check    │   │ Confluent        │
│                  │   │ + retry          │   │ + partitions     │
└──────────────────┘   └──────────────────┘   └──────────────────┘
                                ▲                        ▲
                                │                        │
                       ┌──────────────────┐   ┌──────────────────┐
                       │ RAMQ.Integration │   │ RAMQ.Integration │
                       │ .Transport       │   │ .Storage         │
                       │ .RabbitMq        │   │ .AzureBlob       │
                       │                  │   │                  │
                       │ AMQP 0.9/1.0     │   │ Claim-check blob │
                       │ + exchange       │   │                  │
                       └──────────────────┘   └──────────────────┘
       ▲
       │ consommés par les hôtes
       ▼
┌──────────────────────────┐   ┌──────────────────────────────────┐
│ RAMQ.Integration         │   │ RAMQ.Integration                  │
│ .Hosting.Functions       │   │ .Hosting.AspNetCore               │
│                          │   │                                    │
│ Adapter Azure Functions  │   │ BackgroundService / IHostedService │
│ (ServiceBusTrigger,      │   │ consommant directement les SDK    │
│ Functions Worker)        │   │ (SB, Kafka, RabbitMQ) — pour      │
│                          │   │ AKS / ARO                          │
└──────────────────────────┘   └──────────────────────────────────┘
```

**Lecture du diagramme.** Le code applicatif (en haut) ne référence **que** deux packages : les abstractions et l'enveloppe. Tous les autres packages sont des **plug-ins** injectés par la DI de l'hôte. Changer de broker = changer la référence `Transport.*` et la configuration. Changer d'hôte = changer la référence `Hosting.*`. Le reste du code applicatif ne bouge pas.

#### 1.2.3 Détail des assemblies cibles

| Assembly | Contenu | Dépend de | Exemples de types |
|---|---|---|---|
| `RAMQ.Integration.Abstractions` | Contrats purs (interfaces, records immuables, enums) — **aucune** dépendance externe autre que `Microsoft.Extensions.Logging.Abstractions` et `Microsoft.Extensions.DependencyInjection.Abstractions`. | BCL seulement | `IMessageTransit`, `IMessagingProvider`, `IStorageProvider`, `IJournalProvider`, `IMetricsProvider`, `IMessageActions`, `MessagingEntityType`, `DeserializationResult<T>` |
| `RAMQ.Integration.Envelope` | Enveloppe CloudEvents 1.0, sérialisation JSON stable, versioning explicite, helpers de mapping vers/depuis `ApplicationProperties` des transports. | Abstractions, `System.Text.Json` | `MessageEnvelope`, `CloudEventAttributes`, `EnvelopeSerializer`, `SchemaVersion` |
| `RAMQ.Integration.RoutingSlip` | Moteur saga : itinéraire, avancement de stage, validation, exceptions. Pas de dépendance à un broker. | Abstractions, Envelope | `IItineraryPlanner`, `IStageAdvancer`, `RoutingSlip`, `StageDescriptor`, `SagaStageValidationException` |
| `RAMQ.Integration.Transport.ServiceBus` | Provider Service Bus : `ServiceBusSenderCache`, `RetryPolicyHandler`, `CircuitBreakerManager`, `ServiceBusMessagingProvider`, mapping enveloppe ↔ `ServiceBusMessage`/`ServiceBusReceivedMessage`. | Abstractions, Envelope, `Azure.Messaging.ServiceBus` | `ServiceBusMessagingProvider`, `ServiceBusMessageActions` adapter |
| `RAMQ.Integration.Transport.Kafka` | Provider Kafka Confluent : consumer groups, offsets, idempotent producer, mapping enveloppe ↔ `Message<TKey, TValue>`. | Abstractions, Envelope, `Confluent.Kafka` | `KafkaMessagingProvider`, `KafkaMessageActions`, `KafkaTopicResolver` |
| `RAMQ.Integration.Transport.RabbitMq` | Provider RabbitMQ : exchanges, bindings, `IModel` / channels, mapping enveloppe ↔ `IBasicProperties`. | Abstractions, Envelope, `RabbitMQ.Client` | `RabbitMqMessagingProvider`, `RabbitMqMessageActions` |
| `RAMQ.Integration.Storage.AzureBlob` | Implémentation `IStorageProvider` sur Azure Blob (claim-check). | Abstractions, `Azure.Storage.Blobs` | `AzureBlobStorageProvider` |
| `RAMQ.Integration.Journal.AzureTable` | Implémentation `IJournalProvider` sur Azure Table. | Abstractions, `Azure.Data.Tables` | `AzureTableJournalProvider` |
| `RAMQ.Integration.Hosting.Functions` | Adapter Azure Functions : `ServiceBusTrigger`, binding du `ServiceBusMessageActions` vers `IMessageActions`, extensions DI. | Abstractions, `Microsoft.Azure.Functions.Worker.*` | `FunctionsMessagingAdapter`, `AddFunctionsMessaging()` |
| `RAMQ.Integration.Hosting.AspNetCore` | Adapter `BackgroundService` consommant les SDK directement — pour AKS/ARO. | Abstractions, `Microsoft.Extensions.Hosting` | `BackgroundMessagingService`, `AddHostedMessaging()` |

💡 **Pour un junior — pourquoi ce découpage ?** Trois règles :
>
> 1. **Les abstractions ne connaissent personne** (pas de référence à Service Bus, Kafka ou ASP.NET Core). C'est ce qui garantit que le code applicatif reste portable.
> 2. **Chaque transport est isolé** : ajouter Kafka ne modifie **pas** le package Service Bus. Chaque transport a son cycle de version propre.
> 3. **L'hôte ne dicte pas le transport** : une Azure Function peut parler à Kafka, un BackgroundService sur AKS peut parler à Service Bus. Cela résulte mécaniquement du fait que `Hosting.*` référence les abstractions (pas un transport précis).

#### 1.2.4 Matrice combinaisons hôte × transport

| Hôte \ Transport | Service Bus | Kafka Confluent | RabbitMQ |
|---|---|---|---|
| **Azure Functions** | ✅ Cas chaud actuel (trigger SB natif) | ✅ Via extension Kafka Azure Functions, ou BackgroundService dans Functions Premium | ⚠️ Techniquement possible (polling via timer trigger ou BackgroundService), mais rarement pertinent — préférer ACA/AKS |
| **Conteneur AKS** | ✅ `BackgroundService` + SDK SB | ✅ Cas naturel Kafka (co-localisation broker + consommateur) | ✅ Cas naturel RabbitMQ |
| **Conteneur ARO** | ✅ Idem AKS | ✅ Idem AKS | ✅ Idem AKS |

**Important.** Toutes les cellules ✅ partagent **le même code applicatif**. Seules les références NuGet (`Hosting.*` et `Transport.*`) et la configuration changent. C'est le test décisif de la portabilité réelle.

#### 1.2.5 Exemple d'usage côté consommateur (code applicatif RAMQ)

Le même consumer applicatif s'écrit de manière identique quelle que soit la combinaison choisie :

```csharp
public class ValiderAdresseConsumer : BaseConsumer<DemandeValidationAdresse>
{
    public ValiderAdresseConsumer(/* dépendances injectées */) : base(/* ... */) { }

    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<DemandeValidationAdresse> context,
        CancellationToken cancellationToken)
    {
        // Logique métier pure — aucune référence à Service Bus, Kafka ou RabbitMQ
        var adresseId = context.GetVariable<string>("AdresseId");
        // ...
        return context.CopyWithResponse(new MessageTransitResponse { /* ... */ });
    }
}
```

Seule la **composition au démarrage** change :

```csharp
// Variante Azure Functions + Service Bus (aujourd'hui)
builder.Services
    .AddRamqIntegrationAbstractions()
    .AddServiceBusTransport(options => options.ConnectionString = "...")
    .AddAzureBlobStorage(...)
    .AddAzureTableJournal(...)
    .AddFunctionsMessaging()                         // hôte = Functions
    .AddConsumer<ValiderAdresseConsumer>();

// Variante AKS + Kafka Confluent (demain)
builder.Services
    .AddRamqIntegrationAbstractions()
    .AddKafkaTransport(options => options.BootstrapServers = "...")
    .AddAzureBlobStorage(...)
    .AddAzureTableJournal(...)
    .AddHostedMessaging()                            // hôte = BackgroundService
    .AddConsumer<ValiderAdresseConsumer>();

// Variante ARO + RabbitMQ (partenaire externe)
builder.Services
    .AddRamqIntegrationAbstractions()
    .AddRabbitMqTransport(options => options.Uri = "...")
    .AddAzureBlobStorage(...)
    .AddAzureTableJournal(...)
    .AddHostedMessaging()
    .AddConsumer<ValiderAdresseConsumer>();
```

💡 **Pour un junior — pourquoi c'est important ?** Le jour où RAMQ décide de déplacer un domaine de Functions vers AKS, ou de remplacer Service Bus par Kafka pour un flux à haut débit, **aucune ligne de code métier ne doit changer**. Seule la configuration de démarrage bouge. C'est la définition concrète de la portabilité.

#### 1.2.6 Décision à prendre explicitement, maintenant

- **Option A — Assumer le lock-in Azure.** Renommer le package `RAMQ.Azure.Messaging.ServiceBus.Transit`, retirer les abstractions prétendument génériques, simplifier le modèle. Coût faible, honnêteté élevée. **Incompatible avec la stratégie RAMQ multi-hôte et multi-broker** décrite aux §1.2.1-1.2.2.
- **Option B — Tenir la promesse de portabilité.** Scinder en les assemblies décrits au §1.2.3, adopter CloudEvents comme enveloppe (§1.3), enrichir `IMessageTransit`, supprimer `BindContext(object, object)` au profit d'une fabrique typée. Coût moyen, dette maîtrisée, aligné avec la stratégie. **Recommandation forte.**
- **Option C — Statu quo.** Coût invisible aujourd'hui, facture à 18-36 mois sous forme d'impossibilité de migrer vers AKS/ARO ou d'ajouter Kafka/RabbitMQ sans réécrire le domaine applicatif. Déconseillé.

Aucune de ces options n'est bonne ou mauvaise dans l'absolu ; ce qui est bloquant, c'est **l'absence de choix explicite** inscrit dans un ADR et dans le packaging. Voir [§7.1](#71--absence-de-semver-de-changelog-et-dadr).

### 1.3 🧭 Positionnement vis-à-vis de l'écosystème

💡 **Pour les développeurs juniors** — avant d'écrire une librairie, on doit savoir ce qu'elle remplace ou complète. Voici le paysage :

| Solution | Rôle | Comparaison à EMT |
|---|---|---|
| **SDK natif `Azure.Messaging.ServiceBus`** | Client bas niveau | EMT ajoute : claim-check, itinéraire saga, journal, retry/DLQ conventionnel, intégration Functions Worker |
| **MassTransit** (OSS, standard .NET pour messaging) | Framework complet : sagas, request/reply, scheduling, OpenTelemetry natif, multi-transport | Écarté avant le démarrage d'EMT — cf. §1.3.1 |
| **NServiceBus** (Particular Software) | Framework commercial équivalent | Écarté (coût de licence, dépendance éditeur externe, équivalent MassTransit sur les limitations Functions) |
| **Dapr Pub/Sub + Workflow** | Sidecar pub/sub portable | Alternative viable sur AKS/ACA, non sur Azure Functions ; à ré-examiner lors de la Phase 6 si D1 = option B |
| **CloudEvents 1.0** (CNCF) | Spécification de *message envelope* portable | **À adopter activement** — cf. §1.3.2 |

#### 1.3.1 Pourquoi MassTransit a été écarté avant le démarrage d'EMT

Cette décision n'est **pas** documentée dans le dépôt. Elle doit être formalisée en ADR (voir [§7.1](#71--absence-de-semver-de-changelog-et-dadr)) avec les éléments ci-dessous, tels qu'ils ressortent des discussions d'architecture initiales.

**Limite L1 — Absence de support Azure Functions.** MassTransit cible `IHostedService` / `BackgroundService` et ne supporte pas (ou très partiellement) le modèle de trigger Azure Functions. Or **la majorité des charges RAMQ actuelles tournent sur Azure Functions** (Consumption et Premium) pour des raisons de scale-to-zero et de facturation à l'usage. Choisir MassTransit imposait soit de migrer tout de suite vers des conteneurs (décision non prise à l'époque), soit d'écrire soi-même l'intégration Functions — exactement le travail qu'EMT a fini par faire.

💡 **Pour un junior — pourquoi c'est structurant ?** Un framework de messaging non-compatible avec ton hôte d'exécution *te force* à changer d'hôte, même si ce n'est pas ce que tu voulais. C'est un **couplage caché** : l'outil pilote l'infra au lieu de l'inverse. MassTransit supporte bien les *Container Apps* et AKS, ce qui convient à la partie B de la stratégie (§1.2.1), mais **ne couvre pas** la partie A (Functions). Pour RAMQ qui veut les deux, c'est éliminatoire en l'état.

**Limite L2 — Granularité de la sécurité Azure Service Bus.** RAMQ applique des politiques de sécurité **à l'entité** (queue, topic, subscription) — pas au namespace. Chaque domaine a des identités managées avec des rôles Azure RBAC scoping précisément le droit de `Send` ou `Listen` sur une entité donnée (`Azure Service Bus Data Sender` / `Data Receiver` scopés). MassTransit conçoit plutôt une **topologie auto-gérée** : le framework crée les queues/topics manquants au démarrage et s'attend à des droits larges (`Manage` au niveau namespace) pour le faire. Ces droits sont incompatibles avec la politique RAMQ *least privilege* sur le SB.

💡 **Pour un junior — exemple concret.** Dans MassTransit, tu écris `EndpointConvention.Map<CreerDossier>(new Uri("queue:creer-dossier"))` et le framework crée la queue si elle n'existe pas. Pour que cela fonctionne, l'identité managée de l'application doit pouvoir **administrer** le namespace. Chez RAMQ, une identité ne peut que `Send` sur `queue:creer-dossier` (et rien d'autre). MassTransit ne peut pas fonctionner dans ce modèle sans pré-provisionnement manuel externe, ce qui annule une partie de sa valeur.

**Limite L3 — Roadmap MassTransit peu claire et dépendance OSS unique.** Le projet MassTransit a changé plusieurs fois de modèle de financement et de support commercial (v7, v8, transition v9 prévue puis remaniée). Pour une librairie plateforme **interne RAMQ** qui doit être supportée sur 5-10 ans avec des engagements de type N/N-1, dépendre d'une OSS à gouvernance volatile est un risque réglementaire et opérationnel jugé inacceptable. La décision a été de porter cette dépendance *en interne* (EMT), avec les bénéfices (contrôle total, alignement patterns RAMQ) et les coûts (maintenance) que cela implique.

**Bilan.** Les trois limites sont cumulatives et **toujours valides aujourd'hui** — elles doivent être inscrites dans un ADR (ADR-001 suggéré : « Pourquoi EMT et pas MassTransit ») pour éviter qu'un nouveau venu re-pose la question tous les 18 mois. Cet ADR doit aussi préciser **sous quelles conditions la décision serait revisitée** (ex. si Functions est abandonné, si MassTransit publie un support Functions first-class, si RAMQ assouplit sa politique RBAC).

🟢 **Point fort à préserver :** cette décision a été cohérente à l'époque et **reste cohérente aujourd'hui** avec la stratégie multi-hôte (§1.2). La condition pour qu'elle le demeure, c'est que l'équipe EMT tienne la charge d'absorber ce que MassTransit aurait fourni gratuitement — c'est tout l'enjeu des phases 1-5 de la feuille de route ([§8.3](#83-plan-progressif-par-phases)).

#### 1.3.2 Adoption active de CloudEvents 1.0

**Ce qu'est CloudEvents 1.0** ([cloudevents.io](https://cloudevents.io), spec CNCF). Une **spécification d'enveloppe** neutre pour les événements distribués, avec un vocabulaire fixe (obligatoire + optionnel) et des *bindings* pour différents transports (HTTP, AMQP, Kafka, MQTT, JSON). L'idée : un même événement, produit par un service, peut voyager sur HTTP vers un webhook, puis sur Kafka vers un consommateur analytique, puis sur AMQP vers RabbitMQ, **sans transformation du corps** — seul le binding change. Interopérable avec Azure Event Grid, AWS EventBridge, Google Cloud Eventarc.

**Attributs obligatoires de CloudEvents :**

| Attribut | Type | Rôle | Équivalent RAMQ proposé |
|---|---|---|---|
| `specversion` | string (`"1.0"`) | Version de la spec CloudEvents | Fixe `"1.0"` |
| `id` | string | Identifiant unique de l'événement (producer-scoped) | `MessageTransitContext.MessageId` (déjà présent) |
| `source` | URI | Identité logique du producteur (ex. `"/ramq/individu/valider-adresse"`) | Nouveau — à formaliser (schéma URN interne) |
| `type` | string | Type d'événement métier (ex. `"ca.gouv.ramq.individu.adresse.validee.v1"`) | Nouveau — aujourd'hui approximé par `Consumer.Action` |

**Attributs optionnels pertinents pour RAMQ :**

| Attribut | Utilisation RAMQ |
|---|---|
| `subject` | Identité métier de l'entité concernée (`AssureId`, `DossierId`). Permet un routing/filtering côté consommateur sans inspecter le payload. |
| `time` | Horodatage d'émission — aligne sur `EnqueuedTimeUtc` côté Service Bus. |
| `datacontenttype` | `application/json` pour le payload transporté. |
| `dataschema` | URI vers le JSON Schema du payload → permet le versioning fin des contrats métier (différent du `SchemaVersion` de l'enveloppe). |
| Extensions (`ramqitinerary`, `ramqclaimcheck`, `ramqcorrelationid`, `traceparent`) | Conteneurs pour l'itinéraire saga, le pointeur claim-check, le `CorrelationId` RAMQ et la propagation OpenTelemetry. Les extensions CloudEvents sont prévues pour cela — on les nomme en minuscules, stables. |

**Exemple concret de message RAMQ encodé en CloudEvents (format JSON) :**

```json
{
  "specversion": "1.0",
  "id": "d8f3c2a0-4e21-4b15-9c77-3a1b9e0c7f12",
  "source": "/ramq/individu/valider-adresse",
  "type": "ca.gouv.ramq.individu.adresse.demandevalidation.v1",
  "subject": "assure:12345678",
  "time": "2026-04-24T13:42:11.234Z",
  "datacontenttype": "application/json",
  "dataschema": "https://schemas.ramq.gouv.qc.ca/individu/adresse/demande-validation/v1.json",
  "ramqitinerary": {
    "currentstage": "Individu.ValiderAdresse",
    "stages": ["Individu.ValiderAdresse", "Postes.VerifierCodePostal", "Individu.Notifier"],
    "variables": { "DossierId": "D-2026-04-0042" }
  },
  "ramqclaimcheck": {
    "blobcontainer": "claim-checks",
    "blobname": "2026/04/24/d8f3c2a0-...pdf",
    "sizebytes": 1048576
  },
  "ramqcorrelationid": "corr-7f92a1",
  "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "data": {
    "assureId": "12345678",
    "nouvelleAdresse": { "ligne1": "...", "codePostal": "..." }
  }
}
```

**Bindings de transport — ce que cela veut dire concrètement.** CloudEvents définit deux modes de mapping sur un transport :

- **Mode *structured*** : l'enveloppe complète (attributs + `data`) est sérialisée dans le **corps** du message transport, généralement en JSON. C'est le mode par défaut recommandé pour EMT — même représentation côté Service Bus, Kafka, RabbitMQ.
- **Mode *binary*** : les attributs CloudEvents sont mappés sur les **headers/application properties** du transport (ex. `ce-id`, `ce-source`, `ce-type`), et seul `data` est dans le corps. Optimisation utile pour les gros payloads ou l'intégration avec Event Grid natif.

RAMQ adoptera **structured** par défaut (simplicité, interopérabilité maximale, audit plus simple), avec possibilité de passer en binary pour les flux haute performance (à décider cas par cas en Phase 4 / Phase 6).

**Bénéfices concrets pour EMT :**

| Bénéfice | Détail |
|---|---|
| **Portabilité de transport** | Même enveloppe sur Service Bus, Kafka, RabbitMQ. Un message produit sur SB peut être relu sur Kafka sans transformation. Support de la stratégie Axe B ([§1.2.1](#121-le-besoin-réel-de-portabilité-ramq)). |
| **Versioning explicite** | `dataschema` pointe vers le JSON Schema du payload → chaque version métier est traçable, les consumers peuvent refuser les versions non-supportées. |
| **Interop Event Grid / EventBridge** | Si un jour un flux RAMQ doit exposer des événements à un SaaS externe ou à Azure Event Grid, aucune couche de traduction n'est nécessaire. |
| **Outillage existant** | Bibliothèques officielles .NET (`CloudNative.CloudEvents` et adaptateurs `Azure.Messaging.ServiceBus`, `Confluent.Kafka`, AMQP). Pas besoin de réinventer la sérialisation. |
| **Propagation tracing native** | `traceparent` (format W3C Trace Context) est prévu comme extension standard → cohérent avec le tracing OpenTelemetry visé en [§6.2](#62--observabilité--socle-métriques-en-place-distributed-tracing-toujours-absent). |
| **Audit réglementaire simplifié** | Le journal RAMQ enregistre directement les attributs CloudEvents standards → outils de recherche / corrélation communs cross-système. |

**Coût et plan d'adoption.** L'adoption de CloudEvents **ne casse rien aujourd'hui** si on la structure ainsi :
>
> 1. **Phase 3** ([§8.3](#83-plan-progressif-par-phases)) : définir `MessageEnvelope` comme record *strictement* conforme CloudEvents 1.0 ; migrer la sérialisation de `MessageTransitContext` vers cette enveloppe (le runtime `MessageTransitContext<T>` reste, mais n'est plus sérialisé tel quel) ; ajouter les extensions `ramq*`.
> 2. **Tests de contrat snapshot** sur l'enveloppe pour figer le format.
> 3. **Phase 6** : les providers Kafka/RabbitMQ utilisent les bindings CloudEvents officiels → interopérabilité immédiate.

💡 **Pour un junior — pourquoi adopter un standard plutôt que notre propre format ?** Trois raisons :
>
> 1. **Les standards survivent aux équipes.** Dans 10 ans, ton équipe aura tourné. Un message au format RAMQ propriétaire nécessitera de retrouver la doc RAMQ. Un message CloudEvents sera compris par n'importe quel développeur qui a lu la spec CNCF.
> 2. **L'outillage existe déjà.** Exporter vers un data lake, router via un broker externe, traduire vers HTTP — tout est déjà écrit pour CloudEvents. Pour ton format propre, tu écris la glu à chaque fois.
> 3. **Debug et audit sont plus simples.** Quand un incident traverse 3 systèmes, avoir le même vocabulaire (`id`, `source`, `type`) partout accélère la corrélation d'ordres de grandeur.

---

## 2. Qualité du code — perspective plateforme

Les revues Senior et Lead couvrent exhaustivement le plan local. Je m'en tiens ici à **ce qui est invisible à ce niveau** : la qualité *vue de l'extérieur*, par un consommateur de la librairie, et vue sur la durée.

### 2.1 🟠 Surface d'API publique non maîtrisée

**Constat.** En comptant les types `public` dans l'assembly, la surface publique est large (au-delà de 80 types) alors que l'API réellement destinée à l'application cliente tient en **4 à 6 types** : `IMessageProducer<T>`, `IMessageConsumer<T>`, `MessageTransitContext<T>`, `PublishOptions`, `ClaimCheckOptions`, et les extensions `AddProducer<T>` / `AddConsumer<T>`.

Tout le reste est de l'**infrastructure**, qui devrait être `internal` :

- `IProducerPatterns` (déjà noté 🔴 par la revue Senior)
- `ServiceBusSenderCache`, `RetryPolicyHandler`, `AzureFunctionMessagingAdapter`, `AzureMessagingProperties`
- `EndpointResolver`, `MessageTargetMap`, `ExponentialRetryPolicy`
- `IRetryPolicyHandler`, `IMessageTransit`, `IMessagingAdapter` (extensibilité réelle ? cf §2.2)
- La classe abstraite `BaseMessageTransit<T>` et potentiellement `BaseConsumer<T>` (à discuter)

**Pourquoi ça compte.** Une API publique est un **contrat opposable** : toute modification devient un breaking change qui bloquera un consommateur, et dans une organisation avec plusieurs équipes, cela fige des détails d'implémentation pendant des années. La revue Lead documente 10 actions déjà appliquées et 3 sprints futurs : chaque modification interne *devrait* être transparente pour l'application cliente. Aujourd'hui, elle ne l'est pas garantie, faute d'un périmètre public défini.

💡 **Pour les développeurs juniors** — la règle saine : **tout ce qui est `public` par défaut doit se justifier**, pas l'inverse. En .NET, `internal` est le défaut sémantiquement correct pour une classe d'infrastructure d'une librairie. `InternalsVisibleTo` permet d'ouvrir l'accès aux tests sans exposer au monde.

💡 **Exemple concret.** Prenons `ServiceBusSenderCache`. Il est `public` aujourd'hui. Imagine qu'une équipe applicative y accède directement dans son code Functions (parce que c'est pratique). Six mois plus tard, la revue Lead décide de renommer `ReplaceSender` en `ReplaceSenderOnFatalError`. **Ce renommage devient un breaking change pour cette équipe**, ce qui bloque la montée de version de toute la plateforme. Si `ServiceBusSenderCache` avait été `internal`, le renommage aurait été un détail d'implémentation invisible.

**Recommandation (horizon immédiat).**

1. Inventorier les types publics et décider, pour chacun : `public` (contrat stable destiné aux applications), `public sealed` (exposé mais non-extensible), ou `internal` (implémentation).
2. Ajouter `[assembly: InternalsVisibleTo("RAMQ.COM.EnterpriseMessageTransit.Tests")]` dans un fichier `AssemblyInfo.cs` ou directement dans le `.csproj`, puis basculer ~70 % des types en `internal`. Candidats immédiats : `ServiceBusSenderCache`, `RetryPolicyHandler`, `AzureFunctionMessagingAdapter`, `AzureFunctionMessageTransit`, `CircuitBreakerManager`, `EndpointResolver`, `MessageTargetMap`, `ExponentialRetryPolicy`, `AzureMessagingProperties`.
3. Publier un **API surface report** (`Microsoft.DotNet.ApiCompat` ou `PublicApiAnalyzers`) qui échoue la CI si la surface publique change sans changelog explicite.

### 2.2 🟡 Extensibilité par interface non utilisée = extensibilité fictive

**Observation.** `IMessagingProvider`, `IStorageProvider`, `IJournalProvider`, `IMessagingAdapter`, `IRetryPolicyHandler`, `ISystemClock`, `IMessageSerializer` — sept interfaces, **une seule implémentation concrète par interface**, et le code de wiring (`ConfigurerProviders`) connaît les implémentations Azure directement.

Ces interfaces servent **uniquement** au mocking en test. C'est une motivation légitime, mais elle crée une **extensibilité fantôme** : toute personne lisant le code croira qu'elle peut substituer un `JournalProvider` SQL ou un `StorageProvider` S3, alors que la charge de rendre cela réellement opérationnel (DI, configuration, validation) n'a pas été portée.

🧭 **Orientation.** Deux voies cohérentes :

- **Assumer le test-only.** Marquer les interfaces `internal`, garder les implémentations `public`. Les tests utilisent `InternalsVisibleTo`. L'architecture devient honnête sur sa nature "concrète avec seams de test".
- **Tenir la promesse d'extensibilité.** Documenter explicitement les points d'extension, fournir au moins un 2ᵉ provider (même minimal — un `InMemoryJournalProvider` pour les tests d'intégration des consommateurs) et un pattern officiel d'ajout de provider (guide + ADR).

La voie la plus coûteuse est de **rester dans l'ambiguïté**.

### 2.3 🟡 Convention de langue : debt sémantique résiduelle

La revue Lead note l'uniformisation FR→EN des identifiants (action n°6, appliquée). Une convention durable reste à formaliser :

| Élément | Convention recommandée | Rationale |
|---|---|---|
| Identifiants code (classes, méthodes, variables) | **Anglais** | Interopérable, cherchable, conforme BCL |
| Documentation XML `///` | **Français** (convention RAMQ) | Public interne, pédagogie |
| Messages d'exception | **Anglais** | Logs agrégés (Application Insights/Kusto) — recherche cross-système |
| Logs structurés (keys + messages) | **Anglais** | Idem |
| Noms de propriétés JSON sérialisées | **Anglais, stable** (`[JsonPropertyName]` explicite partout) | Compatibilité cross-version et cross-service |
| Documents `docs/*.md` | **Français** | Lectorat interne |

À inscrire dans un fichier `CONTRIBUTING.md` ou `docs/conventions.md`. La règle la plus importante est **« les chaînes qui peuvent fuiter en dehors du dépôt sont en anglais »** (messages d'exception, keys de log, propriétés JSON).

### 2.4 🟢 Invariants de qualité à maintenir

À ne pas perdre quand la surface publique sera réduite (§2.1) et les assemblies scindés (§1.2) :

- `Nullable enable` actif sur tout le projet.
- `records` et `IValidatableObject` pour les classes de configuration — à répliquer dans les futurs packages `Transport.Kafka`, `Transport.RabbitMq`.
- Conventions de nommage .NET (`I`-préfixe, `Async`-suffixe, PascalCase).
- Hiérarchie d'exceptions dédiée avec `StatusCode` — à étendre, pas à remplacer, pour les nouveaux transports.

---

## 3. Design et architecture — paris structurants

### 3.1 🔴 Pari risqué — `MessageTransitContext<T>` est un contrat sérialisé *et* un modèle runtime

**Constat.** `MessageTransitContext<T>` porte simultanément :

- des données **transportées** entre services (`MessageId`, `SessionId`, `CurrentStage`, `Tokens`, `Variables`) → sérialisées JSON et consommées par d'autres processus, potentiellement d'autres équipes ;
- des données **runtime** propres à une invocation (`TransportMessage`, `SerializedPayload`, `IsClaimCheckApplied`) → ignorées à la sérialisation via `[JsonIgnore]` ;
- un **comportement** (`GetVariable<T>`, `CopyWithResponse`, `SetCurrentStage`).

**Pourquoi c'est un pari risqué.** Tout ce qui est sérialisé devient un **contrat inter-services immuable** : l'ajout d'une propriété requise est un breaking change pour tous les consommateurs actuels et historiques (messages dormant en DLQ, replays). Aujourd'hui ce contrat n'a pas de version, pas de schéma (JSON Schema / Avro), pas d'ADR le formalisant, et se mélange avec des champs runtime modifiables sans impact — ce qui **masque** le fait que `MessageTransitContext<T>` est en réalité le **schéma pivot** de toute la plateforme d'intégration RAMQ.

**Risque à 12-24 mois.** Un PR innocent qui renomme `CurrentStage` ou modifie la sérialisation de `Variables` **casse silencieusement** un parcours saga en production, avec un message qui échoue au milieu de son itinéraire après l'étape 3 sur 5.

**Recommandation.**

1. **Séparer le schéma pivot** du modèle runtime. Introduire un `MessageEnvelope` (record, sérialisé, versionné) et un `MessageTransitContext<T>` (runtime, non sérialisé).
2. **Versionner explicitement** le schéma (`SchemaVersion: "1.0"` dans l'enveloppe) et le documenter sous `docs/contracts/envelope-v1.md`.
3. **Ajouter des tests de contrat** (snapshot de la sérialisation JSON d'un message canonique) qui échouent toute modification non-intentionnelle.
4. **Considérer CloudEvents 1.0** comme base de `MessageEnvelope`. Le surcoût est minime, le gain (interopérabilité, outillage, standard CNCF) est réel.

💡 **Pour les développeurs juniors** — un message envoyé sur un bus est un **message-en-vol**. Il peut survivre à la version de l'application qui l'a émis. Le format de ce message est donc un **contrat externe** aussi sérieux qu'une API REST publique. C'est une règle non évidente quand on débute avec du messaging.

### 3.2 🟠 La saga (routing-slip) mérite son propre module

#### 3.2.1 Qu'est-ce qu'une saga, et qu'est-ce qu'un routing-slip ?

💡 **Pour les développeurs juniors.**

Une **saga** est un processus métier qui se déroule en **plusieurs étapes** (*stages*), chacune pouvant échouer indépendamment des autres. Exemple typique RAMQ : *« traiter une demande d'assurance »* peut enchaîner (1) valider l'identité de l'individu, (2) vérifier l'éligibilité auprès du dispensateur, (3) émettre un numéro de dossier, (4) notifier le demandeur. Chaque étape est un *appel distant* à un autre domaine, qui peut réussir, échouer, ou prendre du temps.

Deux grands patrons existent pour implémenter une saga :

- **Orchestration centralisée.** Un service « chef d'orchestre » garde en mémoire (ou en base) l'état de la saga et appelle chaque étape dans l'ordre. Exemple : Azure Durable Functions, MassTransit Saga State Machine. Avantage : lisibilité de l'état global. Inconvénient : point central de défaillance, et le chef d'orchestre doit pouvoir appeler tous les domaines — ce qui **casse les cloisonnements de sécurité** (le chef voit tout).
- **Routing-slip (« bon de routage »).** Le message **porte lui-même** la liste des étapes à franchir et son état courant. Chaque service qui reçoit le message lit *quelle est l'étape courante*, fait son travail, avance l'étape et fait suivre au service suivant. Pas de chef d'orchestre. Avantage : aucun service ne connaît les autres, les cloisonnements sont respectés, le message est autoporteur (on peut le rejouer à partir de n'importe où). Inconvénient : l'état est distribué, donc plus difficile à visualiser sans un outil d'audit adéquat.

**EMT implémente le routing-slip**, principalement motivé par le pattern R1 ([§1.1.1](#111-pourquoi-les-patterns-portés-par-emt-sont-spécifiques-ramq--analyse-point-par-point)) : les domaines RAMQ ont des périmètres de sécurité distincts et aucun ne peut tenir le rôle de chef d'orchestre.

#### 3.2.2 Comment la saga est implémentée aujourd'hui (état du code courant)

La mécanique du routing-slip EMT est éclatée dans **cinq emplacements** du code, ce qui rend le comportement difficile à tracer :

| Élément | Emplacement | Rôle |
|---|---|---|
| **Déclaration de l'itinéraire** | Configuration (`AppSettings.Itinerary`, `EndpointSettings.Target`, `SubscriptionInfoSettings.Consumer/Action`) | L'ordre des étapes et l'endpoint de chacune. |
| **État courant** | `MessageTransitContext<T>.CurrentStage` (sérialisé JSON, voyage avec le message) | L'étape en cours d'exécution. |
| **Variables métier** | `MessageTransitContext<T>.Variables` (dictionnaire sérialisé) | Contexte accumulé par les étapes précédentes. |
| **Exécution de l'avancement** | `BaseConsumer<T>.RouteToNextStageAsync`, `ResolveEffectiveCurrentStage`, `FindIndexFromStage` | Logique pour déterminer quelle est l'étape suivante et lui envoyer le message. |
| **Arrêt idempotent** | Flag `__FinalStageCompleted` dans `Variables` | Empêche qu'un rejeu de la dernière étape déclenche deux fois l'effet métier final. |
| **Erreur d'itinéraire** | `TransitItineraryException` | Stage introuvable, itinéraire incohérent, etc. |

**Concrètement, le flux d'un consumer ressemble à ceci (pseudo-code) :**

```
1. BaseConsumer reçoit un message Service Bus
2. Désérialise → MessageTransitContext<T> (contient CurrentStage = "Individu.ValiderAdresse")
3. Appelle la méthode métier ConsumeAsync(context) de la classe dérivée
4. Si ConsumeAsync réussit → RouteToNextStageAsync :
   a. ResolveEffectiveCurrentStage(context) → détermine le stage courant
   b. FindIndexFromStage → trouve l'index dans AppSettings.Itinerary
   c. Si ce n'est pas le dernier stage → incrémenter, envoyer au stage suivant
   d. Si c'est le dernier stage → positionner Variables["__FinalStageCompleted"] = true,
      (optionnellement) envoyer une réponse
5. Complete le message Service Bus
```

#### 3.2.3 Pourquoi c'est un problème — symptômes observables dans le code

**S1. `BaseConsumer` est une god-class pour tous les consumers, même ceux qui ne font **pas** de saga.**

Un consumer qui traite un événement unique (non-saga) hérite néanmoins de :
- `ResolveEffectiveCurrentStage`, `FindIndexFromStage` — code mort pour lui.
- `RouteToNextStageAsync` — jamais appelé dans son cas.
- La dépendance à `AppSettings.Itinerary` — il doit quand même configurer un itinéraire de longueur 1.

💡 *Pour un junior :* c'est le symptôme classique d'une **responsabilité unique violée**. La classe fait « consumer » ET « moteur de saga ». Le principe SRP (*Single Responsibility Principle*) dit qu'elle ne devrait faire qu'une seule chose.

**S2. La saga ne peut pas être testée sans infrastructure messaging.**

Pour tester que *« après le stage 2, on avance vers le stage 3 »*, il faut aujourd'hui instancier un `BaseConsumer` complet avec son `IMessagingProvider`, son `IStorageProvider`, son `IJournalProvider`, son `ILogger`, etc. Or la logique de saga est **pure** (avancer un index dans une liste) — elle ne devrait rien avoir à faire d'un broker. C'est le symptôme d'un couplage trop fort.

**S3. L'état de la saga vit dans deux endroits hétérogènes.**

- `CurrentStage` (sérialisé, voyage avec le message) : l'étape en cours.
- `Variables["__FinalStageCompleted"]` (sérialisé aussi, dans un dictionnaire `object`) : flag de terminaison.

Deux conséquences :
1. Pour savoir *où en est* une saga, il faut lire deux champs qui ne sont pas côte à côte.
2. `Variables` est un `Dictionary<string, object>` — **n'importe quel consumer applicatif** peut écrire la clé `__FinalStageCompleted` par erreur (ou par collision de nommage), corrompant l'invariant de terminaison.

**S4. La résolution de stage est ambiguë (`FindIndexFromStage` a 3 stratégies).**

`BaseConsumer.FindIndexFromStage` essaie successivement :
1. Match direct sur `Target`.
2. Si `CurrentStage` est `"Consumer.Action"`, tester la partie avant le `.`.
3. Parcourir toutes les subscriptions pour trouver un match `Consumer[.Action]`.

Trois stratégies = trois chemins possibles, donc trois bugs potentiels en fonction de la forme du stage courant. C'est un symptôme d'**absence de modèle de domaine** : on travaille directement sur des strings au lieu d'un type `StageIdentifier` qui saurait son format.

**S5. `TransitItineraryException` est levée loin de la cause.**

Quand un stage est introuvable, l'exception remonte depuis `FindIndexFromStage` → `RouteToNextStageAsync` → la méthode template du consumer. Le diagnostic opérationnel est difficile : le message parle de *« Stage 'X' not found »* mais sans contexte sur *qui* l'a cherché (quel consumer, quel itinéraire configuré, quelles alternatives ont été essayées).

#### 3.2.4 Cible architecturale — un module `RAMQ.Integration.RoutingSlip` autonome

L'idée : extraire la saga dans un module **sans dépendance au messaging**, testable en isolation, réutilisable par tout consumer.

```
┌──────────────────────────────────────────────────────┐
│  RAMQ.Integration.RoutingSlip  (module à créer)      │
│                                                       │
│  Types publics :                                      │
│  - RoutingSlip (record, versionné, sérialisable)     │
│  - StageIdentifier (record, "{Consumer}.{Action}")   │
│  - StageDescriptor (target + endpoint + metadata)    │
│  - IItineraryPlanner (construit l'itinéraire depuis   │
│    la config)                                         │
│  - IStageAdvancer (calcule le stage suivant,          │
│    détecte la terminaison)                            │
│  - SagaStageValidationException                       │
│  - RoutingSlipResult (record : NextStage?, IsFinal,   │
│    VariablesSnapshot)                                 │
│                                                       │
│  Pas de dépendance à IMessagingProvider, aucun SDK    │
│  de broker, aucun logger spécifique — juste BCL.      │
└──────────────────────────────────────────────────────┘
        ▲ consommé par
┌──────────────────────────────────────────────────────┐
│  RAMQ.Integration.Abstractions                       │
│  - BaseConsumer<T> minimal (désérialisation +        │
│    settlement seulement)                              │
│  - AdvancingConsumer<T> : BaseConsumer<T> qui        │
│    utilise IStageAdvancer en option                  │
└──────────────────────────────────────────────────────┘
```

**Points clés de la refonte :**

1. **`RoutingSlip` est un record immuable versionné**, sérialisé en tant qu'extension CloudEvents (`ramqitinerary`, cf [§1.3.2](#132-adoption-active-de-cloudevents-10)). Il **ne vit plus** dans `MessageTransitContext.CurrentStage` + `Variables["__FinalStageCompleted"]` : un seul porteur d'état, unifié.

2. **`StageIdentifier` est un record** avec format explicite `{Consumer}.{Action}`. Parsing centralisé, une seule stratégie de résolution. Fini les 3 stratégies de `FindIndexFromStage`.

3. **`IItineraryPlanner`** construit un `RoutingSlip` depuis `AppSettings.Itinerary` **une seule fois au démarrage** (pas à chaque message). Validation du graphe faite au démarrage.

4. **`IStageAdvancer`** est une **fonction pure** :
   ```csharp
   RoutingSlipResult Advance(RoutingSlip slip, StageIdentifier completedStage);
   ```
   Testable par dizaines de cas unitaires sans aucune I/O. Retourne *« stage suivant X »* ou *« terminaison finale »*, jamais ne fait de publish.

5. **Le consumer applicatif** ne hérite plus d'un `BaseConsumer` god-class. S'il veut de la saga, il hérite de `AdvancingConsumer<T>` qui injecte `IStageAdvancer`. S'il ne veut pas, il hérite de `BaseConsumer<T>` nu.

6. **La publication du message au stage suivant** reste dans le layer messaging, mais l'**ordre des responsabilités** devient :
   ```
   (1) BaseConsumer  → désérialise, invoque ConsumeAsync métier
   (2) IStageAdvancer → calcule NextStage (pur, testable)
   (3) IMessageProducer → publie vers NextStage si non-final
   (4) BaseConsumer  → settle le message d'entrée
   ```

#### 3.2.5 Plan de migration en 5 étapes (pour un développeur junior)

Cette refonte **ne doit pas** casser l'API cliente. Voici comment procéder étape par étape :

| Étape | Action | Risque |
|---|---|---|
| **E1** | **Créer le folder `Messaging/RoutingSlip/`** dans l'assembly courant (pas encore d'assembly séparé). Déplacer / créer `RoutingSlip`, `StageIdentifier`, `IItineraryPlanner`, `IStageAdvancer` et leurs implémentations. | Faible — code additif. |
| **E2** | **Écrire les tests unitaires** de `StageAdvancer` : transition stage → stage+1, détection du stage final, erreur si stage courant absent de l'itinéraire. Pas de I/O, 100 % déterministe. | Nul — tests purs. |
| **E3** | **Faire en sorte que `BaseConsumer` délègue** `FindIndexFromStage` et `ResolveEffectiveCurrentStage` au nouveau `IStageAdvancer`, **sans** changer le comportement externe. | Moyen — suivre par tests d'intégration existants. |
| **E4** | **Unifier l'état** : introduire un nouveau champ sérialisé `RoutingSlip` dans l'enveloppe (Phase 3, cf [§8.3](#83-plan-progressif-par-phases)) qui contient `StageIdentifier` courant, liste des stages, et flag de terminaison. Garder `CurrentStage` et `__FinalStageCompleted` pendant une période de transition (lire les deux formats, écrire uniquement le nouveau). | Moyen — nécessite une stratégie de déprécation sur 1-2 versions. |
| **E5** | **Supprimer `FindIndexFromStage` et le flag `__FinalStageCompleted`** de `BaseConsumer`. Le consumer non-saga ne les voit plus jamais. Quand D1 = option B, extraire `RAMQ.Integration.RoutingSlip` en assembly séparé (Phase 6). | Faible si E1-E4 sont validés. |

💡 **Pour un junior — la règle d'or de cette refonte.** *« Ne jamais changer la sémantique et la structure en même temps. »* Étape E3 = même comportement, nouvelle structure. Étape E4 = nouveau format sérialisé en plus de l'ancien. Jamais on ne casse les messages en vol.

#### 3.2.6 Bénéfices attendus

- **Un consumer simple (non-saga) n'hérite plus d'une mécanique dont il n'a pas besoin** → lisibilité + baisse du taux d'erreur de configuration.
- **La saga peut évoluer** (stages dynamiques, branches conditionnelles, retry policy par stage) sans toucher au layer messaging.
- **La saga peut être testée** avec un broker in-memory, sans Service Bus — réduit le temps de feedback à quelques millisecondes.
- **Si un jour une équipe veut réutiliser le pattern saga sans Service Bus** (ex. workflow local, test de charge scriptable), elle le peut.
- **Le diagnostic opérationnel devient cohérent** : un seul champ `routingslip` dans les logs, une seule exception bien contextualisée.

**Coût :** moyen (refactor ciblé, pas de breaking change pour les applications clientes si l'interface `IMessageConsumer<T>` reste inchangée). Cible : **Phase 5** ([§8.3](#83-plan-progressif-par-phases)).

### 3.3 🟠 Couplage `AzureFunctionMessagingAdapter` → `Microsoft.Azure.Functions.Worker`

Ce point est déjà relevé par la revue Senior (§2.3). Je le re-qualifie ici en termes de **dette de portabilité** : tant que ce couplage existe dans l'assembly principal, **EMT n'est utilisable que depuis une Azure Function**. Ni depuis un Worker Service BackgroundService, ni depuis une API ASP.NET Core qui voudrait consommer Service Bus directement via le SDK, ni depuis une console d'administration.

**Recommandation (alignée avec §1.2) :** scinder en `RAMQ.Messaging.Transit.ServiceBus` (pur SDK Service Bus) et `RAMQ.Messaging.Transit.Functions` (adapter Azure Functions). Les applications clientes choisissent ce qu'elles consomment.

### 3.4 🟡 `IMessageTransit` sous-dimensionné — symptôme d'abstraction prématurée

💡 **Pour les développeurs juniors** — une abstraction est « prématurée » quand on a créé une interface **sans en avoir plusieurs implémentations réelles**. On croit généraliser, mais on ne fait que projeter la seule implémentation qu'on connaît.

`IMessageTransit` expose `MessageId`, `Content`, `SequenceNumber`, `SessionId`. Dans les faits, les applications clientes ont besoin au minimum de :

- `ApplicationProperties` (métadonnées métier : `Consumer`, `Action`, `CorrelationId`, idempotency key)
- `DeliveryCount` (pour diagnostic et décisions de retry applicatives)
- `EnqueuedTimeUtc` (pour SLA et âge du message)
- `CorrelationId` et `ReplyTo` (pour request/reply et traçabilité)

Aujourd'hui, pour obtenir ces informations, elles font un cast vers `AzureFunctionMessageTransit` → `RawMessage` → propriétés `ServiceBusReceivedMessage`. C'est la **preuve** que l'abstraction est inadéquate : elle est contournée à chaque utilisation non triviale.

**Recommandation.** Compléter `IMessageTransit` avec les propriétés ci-dessus, ou bien (option plus radicale alignée avec §1.2 A) supprimer l'interface et exposer directement `ServiceBusReceivedMessage`. L'option intermédiaire actuelle est la pire.

### 3.5 🟢 Patterns à ne pas casser pendant la Phase 5/6

À préserver sous leur forme actuelle (ou équivalent) quand les refactors architecturaux (§3.2, §1.2) seront exécutés :

- **Clé composite `{FullyQualifiedNamespace}|{entityName}`** du cache de senders — invariant de séparation multi-namespace à répliquer dans tout nouveau provider Kafka/RabbitMQ.
- **Délégation de la retry policy à un handler isolé** (`IRetryPolicyHandler`) — modèle reproductible pour le claim-check et le journal.
- **Résolution target via `IMessageTargetMap`** (`AddProducer<TMessage>("target")`) — API consommateur à étendre aux nouveaux transports, pas à remplacer.
- **Transitions de circuit breaker verrouillées par entité** — l'état par-entité doit survivre à la scission en assemblies. Si Kafka/RabbitMQ ont besoin d'un circuit breaker analogue, réutiliser la même mécanique.

---

## 4. Robustesse et fiabilité — modèle de défaillance

Les revues Senior et Lead listent les retries, DLQ, idempotence settlement, journal hors chemin critique. Je m'intéresse ici au **modèle mental global de défaillance** : est-ce que EMT sait *ce qui peut mal tourner* et *comment il réagit* ?

### 4.1 🟠 Modèle de défaillance implicite — à rendre explicite

Les mécanismes de gestion d'erreur sont en place dans le code (retry policies, DLQ, circuit breaker par entité, désérialisation typée). Ce qui reste **ouvert**, c'est la **documentation opérationnelle** qui permet à une équipe SRE d'en tirer profit :

1. **Un `docs/failure-modes.md` n'existe pas**. Chaque mode de défaillance devrait y être décrit avec : comportement observable par un opérateur (log, métrique), conséquence métier, action corrective attendue. Sans ce document, un log `CircuitBreakerOpenException` ou un pic `messages_dlq_total` n'est pas interprétable à 3h du matin.
2. **Aucune matrice de décision** n'explique au développeur applicatif quand lever `ImmediateRetryException`, `ExponentialRetryException` ou `ImmediateDLQException`. Le choix repose sur de la tradition orale.
3. **Les claim-checks orphelins ne sont pas nettoyés** — aucun lifecycle Blob automatique, aucune compensation explicite si l'envoi Service Bus échoue après un upload Blob réussi. 💡 *Pour un junior :* un « claim-check orphelin » est un fichier dans le blob storage qui n'est référencé par aucun message vivant. Sans nettoyage, (a) le coût de stockage augmente indéfiniment et (b) les fichiers de données médicales séjournent **au-delà** de leur durée de rétention légale — c'est un risque de conformité CAI.
4. **Les nouvelles fonctionnalités déployées ne sont pas observées** : aucun compteur `circuit_state{entity}`, `circuit_transitions_total{entity,from,to}`, ni `deserialization_failures_total{reason}`. Ces métriques sont à ajouter au `MetricsProvider`.
5. **La panne Blob en *download* côté consumer n'a pas de politique explicite** : aujourd'hui l'exception remonte telle quelle. Doit-on retenter ? Aller directement en DLQ ? Cela dépend du type d'erreur (404 = corruption, 503 = transitoire). À trancher.
6. **Le retry storm** est mitigé côté consumer par le circuit breaker, mais aucun *backpressure* côté producteur : un producteur peut saturer un broker dégradé tant que sa propre instance est saine. À évaluer cas par cas — probablement pas un sujet Phase 1-2.

### 4.2 🟡 Désérialisation — propagation du type `Result` à finaliser

Le type `Serialization/DeserializationResult.cs` est en place (cas `Success`, `EmptyPayload`, `PayloadTooLarge`, `Malformed`, `UnexpectedError`). Il reste à :

1. **Auditer tous les call-sites de désérialisation** (`BaseConsumer.DeserializeMessageAsync`, `AzureMessagingProvider`, tout point qui désérialise un payload reçu). Aucun ne doit encore retourner `MessageTransitContext<T>?` *null* implicitement ; tous doivent convertir vers `DeserializationResult<MessageTransitContext<T>>` et propager la raison.
2. **Instrumentation** : `MetricsProvider` doit exposer un counter `deserialization_failures_total{reason}` avec le label `reason` = valeur de `DeserializationFailureReason`. Sans ce counter, la dégradation silencieuse reste invisible en production.
3. **Politique DLQ explicite** : décider par code, pas par convention, que `Malformed` → DLQ immédiat, `EmptyPayload` → comportement configurable, `PayloadTooLarge` → DLQ + alerte (c'est souvent le signe d'une erreur d'émission amont).
4. **Test de contrat** sur chaque branche du résultat (voir §6.1 et Phase 1 de la feuille de route §8.3).

💡 **Pour un junior — pourquoi finaliser la propagation ?** Un type `Result` qui n'est **utilisé que sur 80 % du chemin** n'apporte pas 80 % des bénéfices : il apporte 0. Les 20 % restants continueront à retourner `null` et à masquer les erreurs. Un filet de sécurité doit couvrir *tout le plancher*, pas 4 carreaux sur 5.

### 4.3 🟡 Idempotence producteur non garantie

#### 4.3.1 Pourquoi ce sujet existe — le problème à résoudre

💡 **Pour les développeurs juniors.** Quand un producteur envoie un message à Service Bus (ou à Kafka, ou à n'importe quel broker), il peut arriver que :

1. Le message **a bien été reçu et stocké** par le broker, mais…
2. La **réponse d'accusé-réception** (l'ACK) n'arrive jamais au producteur (timeout réseau, redémarrage du worker, …), donc…
3. Le producteur **croit avoir échoué** et **retente** l'envoi.

Résultat : **le broker a maintenant deux exemplaires du même message**. Le consumer le traite deux fois. Effet métier déclenché deux fois. C'est le problème classique de **l'exactly-once delivery** : la garantie *« exactement une fois »* n'existe pas naturellement dans les systèmes distribués. Ce qu'on peut obtenir, c'est :

- **at-least-once** : au moins une fois (peut-être plus). C'est ce que donne Azure Service Bus sans précaution.
- **at-most-once** : au plus une fois (peut-être zéro). Pas acceptable pour RAMQ (messages perdus).
- **effectively-once** : *effectivement* une fois, en combinant at-least-once + **détection/rejet des doublons**. C'est ce qu'on veut.

La détection des doublons peut se faire à deux endroits :
- **Côté broker** — le broker mémorise les IDs des messages reçus récemment et rejette silencieusement ceux qu'il a déjà vus.
- **Côté consumer** — le consumer applicatif tient lui-même une liste des IDs traités (table de déduplication) et ignore les doublons.

EMT choisit la voie **broker** via la fonctionnalité `RequiresDuplicateDetection` de Service Bus. C'est la plus simple à opérer mais elle **exige une configuration infrastructurelle correcte** qui n'est aujourd'hui documentée nulle part.

#### 4.3.2 Comment EMT utilise cette fonctionnalité aujourd'hui (état du code)

Dans `Producer.PublishCoreAsync` :

```csharp
if (string.IsNullOrWhiteSpace(context.MessageId))
{
    context.MessageId = Guid.NewGuid().ToString("N");
}
```

Le `MessageId` est **toujours présent** à l'envoi — soit fourni par le caller (ex. hash déterministe d'une clé métier), soit généré aléatoirement. Ce `MessageId` est ensuite utilisé par le SDK Service Bus comme propriété du message envoyé.

**Et c'est tout.** EMT ne fait rien d'autre. Il compte sur Service Bus pour détecter les doublons.

#### 4.3.3 `RequiresDuplicateDetection` — ce que fait Service Bus, concrètement

**Définition.** `RequiresDuplicateDetection` (aussi appelé *duplicate detection* ou *requires-duplicate-detection*) est une option que l'on active **au moment de la création d'une queue ou d'un topic Service Bus** (pas pour une subscription). Une fois activée :

1. Service Bus tient en interne, pour chaque entité, une **fenêtre glissante** (paramétrée par `DuplicateDetectionHistoryTimeWindow`, 10 minutes par défaut, max 7 jours, min 20 secondes) dans laquelle il mémorise les `MessageId` des messages récemment acceptés.
2. Quand un nouveau message arrive avec un `MessageId` **déjà présent** dans la fenêtre, Service Bus **accepte l'envoi côté SDK (il n'y a pas d'erreur pour le producteur)** mais **ne le stocke pas** dans la queue/topic. Du point de vue du consumer, le doublon n'a jamais existé.

💡 **Pour un junior — les trois points à retenir :**

1. **C'est une option d'entité, pas de message.** On ne l'active pas par envoi — elle est configurée sur la queue ou le topic elle-même, côté infrastructure (Bicep, Terraform, ou portail Azure).
2. **Elle est immuable après création.** On **ne peut pas** activer `RequiresDuplicateDetection` sur une queue existante. Il faut recréer l'entité. C'est pourquoi il faut la configurer **dès le départ**, avant toute mise en production.
3. **La fenêtre a un coût et une limite.** Plus la fenêtre est longue, plus Service Bus doit stocker d'IDs → plus le débit max de l'entité baisse (impact modéré, mais réel). La valeur par défaut 10 minutes est généreuse pour la plupart des cas, mais **insuffisante pour les longues sagas RAMQ** où un message peut être rejoué après plusieurs heures (ex. après déblocage d'un incident, replay depuis DLQ, ou lecture d'une archive).

#### 4.3.4 Ce qui manque aujourd'hui dans EMT et pourquoi c'est un risque

**Manque M1 — Aucune exigence infrastructurelle documentée.** Rien dans [docs/architecture-technique.md](architecture-technique.md) ou ailleurs ne dit : *« toute queue/topic consommée par EMT doit avoir `RequiresDuplicateDetection = true` »*. Une équipe qui provisionne une nouvelle entité Service Bus pour EMT peut très légitimement la créer **sans** cette option, et EMT enverra alors des messages avec `MessageId` sans aucun effet de déduplication — on retombe en **at-least-once**, avec les conséquences métier que cela implique (double paiement, double notification, etc.).

**Manque M2 — Aucun test de démarrage qui vérifie la config.** EMT ne lit pas la propriété `RequiresDuplicateDetection` de l'entité au démarrage pour prévenir *« attention, cette queue n'a pas la dedup activée »*. Résultat : le défaut de configuration se découvre **en incident de production**.

**Manque M3 — Aucune guidance sur la fenêtre de dedup.** 10 minutes (défaut) convient pour un producteur qui retente en quelques secondes. Pour une saga RAMQ où le retry peut venir d'un **replay DLQ 2 heures plus tard**, la fenêtre est insuffisante — le doublon passe.

**Manque M4 — Aucune stratégie explicite pour un `MessageId` déterministe.** Aujourd'hui, si le caller ne fournit rien, EMT génère `Guid.NewGuid()`. Le problème : si le même caller retente l'envoi (ex. rejeu applicatif après timeout), il va générer un **nouveau GUID** — Service Bus ne voit pas le doublon car les IDs sont différents. La vraie protection ne marche **que** si le caller utilise un `MessageId` **déterministe** (ex. hash de la clé métier + de l'intention) qui sera le même entre deux envois du *même* événement logique.

💡 **Pour un junior — résumé de la logique :**

> L'idempotence producteur repose sur **un triangle à trois côtés**. Si un seul côté manque, elle ne marche pas :
>
> 1. **Côté infra** : `RequiresDuplicateDetection = true` sur la queue/topic, avec une fenêtre alignée sur le délai max de retry attendu.
> 2. **Côté code** : `MessageId` systématiquement renseigné (c'est fait dans EMT).
> 3. **Côté métier** : `MessageId` **déterministe** pour les scénarios où le caller retente (pas `Guid.NewGuid()` aveugle dans ces cas-là).

#### 4.3.5 Recommandation claire — ce qu'il faut faire, étape par étape

**R1 — Section « Prérequis infrastructure » dans `docs/architecture-technique.md`.**

Y lister explicitement, pour toute queue/topic consommée par EMT :

| Option Service Bus | Valeur exigée | Justification |
|---|---|---|
| `requiresDuplicateDetection` | `true` | Protection contre les doublons producteur (ce §). |
| `duplicateDetectionHistoryTimeWindow` | `PT30M` (30 minutes) minimum, `PT1H` recommandé pour les sagas | Couvre les retries applicatifs courants et les replays DLQ manuels courts. Pour les sagas longues, envisager jusqu'à `PT24H`. |
| `deadLetteringOnMessageExpiration` | `true` | Messages expirés vont en DLQ (audit, non-perte silencieuse). |
| `maxDeliveryCount` | Aligné avec `ExponentialRetryPolicy.MaxDeliveryCount` de EMT (défaut 10) | Cohérence entre SB et EMT. Un écart provoque des doublons DLQ (si SB < EMT) ou des messages bloqués (si SB > EMT). |
| `lockDuration` | `PT1M` minimum | Donne au consumer le temps de traiter un message sans perdre le lock. |
| `enableBatchedOperations` | `true` | Performance. |
| `requiresSession` | Selon endpoint (cf `EndpointSettings.Endpoint.EnableSession`) | Aligner infra sur config applicative. |

Fournir des **extraits Bicep et Terraform** prêts à copier, par exemple :

```bicep
resource sbQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: '${sbNamespace.name}/individu-valider-adresse'
  properties: {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT1H'
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    enableBatchedOperations: true
    requiresSession: false
  }
}
```

**R2 — Validation au démarrage par EMT.**

Ajouter dans le `ServiceBusHealthCheck` existant (ou dans un nouveau check `ServiceBusConfigurationHealthCheck`) une vérification qui interroge l'API d'administration Service Bus pour lire les propriétés des entités consommées et **logue un warning** (ou **échoue le démarrage** en mode strict) si :

- `RequiresDuplicateDetection = false` sur une entité consommée.
- `DuplicateDetectionHistoryTimeWindow < 30 minutes`.
- `MaxDeliveryCount` en désaccord avec `ExponentialRetryPolicy.MaxDeliveryCount`.

💡 *Pour un junior :* c'est le principe du **fail-fast** — mieux vaut échouer au démarrage avec un message clair que de découvrir le problème 3 mois plus tard à cause d'un doublon métier en production.

**R3 — Guidance API : exposer une surcharge `PublishAsync` qui impose un `MessageId` déterministe.**

Aujourd'hui EMT génère `Guid.NewGuid()` par défaut. C'est acceptable pour des événements *émis une seule fois* (ex. nouvel enregistrement). Pour les scénarios de retry/rejeu applicatif, documenter et encourager un pattern explicite :

```csharp
// Bonne pratique : MessageId déterministe lors d'un rejeu applicatif
context.MessageId = IdempotencyKeyBuilder.For(
    operation: "ValiderAdresse",
    businessKey: demande.DossierId,
    intent: demande.IntentHash);   // stable entre deux émissions de la même demande

await producer.PublishAsync(context, ...);
```

Publier dans le `docs/` un ADR (« ADR-002 : Stratégie d'idempotence producteur ») qui trace cette politique.

**R4 — Métrique et alerting.**

Exposer dans `MetricsProvider` un compteur `duplicate_detected_total{entity}` — Service Bus publie un compteur équivalent au niveau namespace que l'on peut agréger via Azure Monitor. Une hausse anormale est **le signal** qu'un producteur est en train de boucler sur un retry mal configuré.

**R5 — Alignement avec CloudEvents.**

Dans l'enveloppe CloudEvents ([§1.3.2](#132-adoption-active-de-cloudevents-10)), l'attribut `id` remplit exactement le rôle de `MessageId` Service Bus. La guidance R3 ci-dessus doit expliciter que **`id` CloudEvents = `MessageId` transport**, et qu'il doit être déterministe pour les rejeux.

#### 4.3.6 Ce qui reste **hors** de la garantie, même si R1-R5 sont appliqués

Même avec `RequiresDuplicateDetection = true` + fenêtre longue + `MessageId` déterministe, il reste des cas où l'exactly-once n'est pas tenu :

- **Fenêtre dépassée.** Si un rejeu intervient au-delà de `DuplicateDetectionHistoryTimeWindow`, Service Bus ne « se souvient » plus → le doublon passe. Mitigation : tables de déduplication **applicatives** sur les opérations critiques (pattern *idempotent consumer*), en complément.
- **Cross-transport.** Si un jour EMT pousse le même événement sur Service Bus ET sur Kafka en double-publish, chaque broker a sa propre dedup — il faut une stratégie au niveau applicatif (clé de corrélation persistée).
- **Effets de bord non transactionnels.** Si le handler du consumer appelle un service tiers non-idempotent (ex. un SOAP legacy WCF, pattern R8), le double traitement reste un risque même avec dedup parfaite. Mitigation : pattern *Outbox* / *transactional handler* côté consumer.

💡 **Pour un junior — leçon à retenir.** L'idempotence n'est *jamais* résolue en un seul endroit. C'est une **chaîne** — producteur, broker, consumer, systèmes aval. `RequiresDuplicateDetection` en résout **un maillon**. Les autres maillons doivent être traités explicitement. EMT doit au minimum : documenter ce maillon (R1), vérifier qu'il est en place (R2), donner les outils pour les autres (R3-R5), et signaler ses limites (ce §).

### 4.4 🟡 Timeout global `PublishAsync` non borné

Aucun `PublishAsync` n'impose de timeout par défaut au-delà du `CancellationToken` fourni par l'appelant. En pratique, beaucoup d'Azure Functions invoquent sans token ou avec le token d'invocation (qui peut être très long). Un `CreateSenderAsync` lent + un `SendMessageAsync` sur une entité dégradée peuvent consommer plusieurs minutes silencieusement.

**Recommandation.** Imposer un timeout défensif (ex. 30s) côté provider, configurable, documenté. Émettre une métrique `send_timeout_total` quand il se déclenche.

### 4.5 🟢 Invariants de résilience à ne pas casser pendant les refactors

À préserver tel quel pendant les phases de refactor à venir (§8.3) :

- Backoff exponentiel **avec jitter** (`Random.Shared`) dans la logique de retry — évite le *thundering herd* lors d'une reprise après panne.
- Journal **hors chemin critique** (pattern A5) — une panne Azure Table ne doit jamais bloquer un envoi métier.
- Flag de terminaison saga vérifié avant ré-émission — invariant à conserver lors de la refonte RoutingSlip (§3.2), même sous un nouveau nom.

---

## 5. Performance et scalabilité — enveloppe de fonctionnement

### 5.1 🟠 Absence d'enveloppe de fonctionnement documentée

Une librairie à ce niveau de maturité devrait déclarer son **enveloppe** : limites testées, comportement hors enveloppe, goulots connus. Aujourd'hui :

- Pas de chiffre sur le débit maximum (msg/s) par instance.
- Pas de limite explicite sur `PublishBatchAsync` (relevé Lead §4.3).
- Pas de référence sur le coût de la journalisation Azure Table par message (latence p50/p99).
- Pas d'indication sur le nombre de sessions concurrentes (`MaxConcurrentSessions = 10` par défaut — visible code, pas doc).
- Pas de chiffre sur le seuil claim-check (256 Ko par défaut) ni sur le rapport coût/latence Blob vs inline.

**Recommandation (horizon 6 mois).**

1. Benchmarks reproductibles (`BenchmarkDotNet` pour sérialisation, k6/NBomber pour intégration Service Bus).
2. Doc `docs/operational-envelope.md` : "EMT tient X msg/s par instance Function sur SKU Y, avec latence p99 < Z ms au-delà il faut…".
3. Alarmes et SLO documentés en face (p99 latence journal, taux DLQ, âge max message).

### 5.2 🟡 Journal synchrone sur chemin batch — O(n) appels Table Storage

Dans `PublishBatchAsync`, chaque message du batch déclenche une écriture Table Storage séquentielle. Pour un batch de 100 messages, c'est 100 appels HTTP séquentiels à Azure Table. Latence probable 200-500ms → **le journal double ou triple le temps d'un batch**.

**Pistes :**

- Remplacer par `TableClient.SubmitTransactionAsync` (jusqu'à 100 entités par transaction, même PartitionKey).
- Ou batcher en background via un `Channel<JournalEntry>` vidé par un background service (décorréle totalement journalisation et envoi).
- Ou accepter le coût et documenter l'enveloppe.

Le choix dépend de la contrainte d'**auditabilité** : est-ce que RAMQ exige un journal synchrone (garantie qu'une entrée existe avant retour du `PublishAsync`) ou asynchrone acceptable ? C'est une question métier à trancher et documenter.

### 5.3 🟡 `EndpointResolver.TryResolve` — allocations LINQ répétées

Déjà noté par la revue Lead. À l'échelle d'une Function traitant 10 msg/s pendant 24h, c'est ~860 000 résolutions/jour avec des allocations LINQ. Au niveau code, cela ne dérange pas. Au niveau **coût d'exécution Functions Consumption**, 1 Go·s supplémentaire par invocation × 860k = ~860 Go·s/jour supplémentaires facturables. C'est marginal mais **instrumentable** : un benchmark devrait le quantifier avant de le traiter comme prioritaire.

**Recommandation.** Cache des résolutions (`ConcurrentDictionary<(target, consumer, action), EndpointSettings>`) invalidé au reload de configuration. Gain probable : 95 % des allocations de ce chemin.

### 5.4 🟢 Acquis performance à ne pas régresser

À surveiller lors des refactors Phase 5/6 via les benchmarks de la Phase 4 :

- Cache de `ServiceBusSender` éliminant la création répétée de connexions AMQP.
- `JsonSerializerOptions` statiques (pas d'allocations d'options par message).
- Validation des doublons de targets au démarrage uniquement.
- `IMessageSerializer` enregistré Singleton.
- Seuil claim-check 256 Ko aligné sur la limite Service Bus Standard.

---

## 6. Testabilité et observabilité — contrats opérationnels

### 6.1 🟠 Testabilité : inventaire des "seams" mais absence de stratégie

Les interfaces permettent le mocking, mais il manque :

- **Une stratégie de test documentée** (`docs/testing-strategy.md`) : quels tests unitaires, quels tests d'intégration, quel périmètre de chaque.
- **Un projet de test** dans la solution (`EnterpriseMessageTransit.Tests` visible dans `EnterpriseMessageTransit.sln` ? à vérifier). La revue Lead note « Tests unitaires : À exécuter » — cela signifie soit qu'ils n'existent pas, soit qu'ils ne tournent pas en CI. Dans les deux cas, **la surface critique est non-régressionnée**.
- **Des test fixtures pour Service Bus** : `Testcontainers.ServiceBus` n'existe pas, mais `Azure Service Bus Emulator` (disponible depuis 2024) permet des tests d'intégration locaux. Aucune trace de son intégration.
- **Des tests de contrat** sur la sérialisation `MessageTransitContext` (cf. §3.1) : non présents.

**Recommandation (horizon immédiat).**

1. Créer / inventorier `EnterpriseMessageTransit.Tests` et exiger ≥ 70 % de couverture sur `Messaging/` et `Configuration/`.
2. Ajouter un job CI dédié (`dotnet test --collect:"XPlat Code Coverage"`), bloquant sur PR.
3. Tests d'intégration avec Azure Service Bus Emulator dans un conteneur de test — documenté et exécutable en CI.
4. Tests de contrat JSON snapshot sur `MessageTransitContext` et `MessagingOptions`.

#### 6.1.1 Stratégie de tests — tableau récapitulatif des types de tests à mettre en œuvre sur EMT

> 💡 **Pour les développeurs juniors.** Une bibliothèque comme EMT ne se valide **pas** avec un seul type de test. Chaque type couvre une classe de défauts différente : un test unitaire ne détecte pas une régression de schéma sérialisé, un test de charge ne détecte pas un bug logique dans `Advance(stage)`. Le tableau ci-dessous décrit la **pyramide de tests** spécifique à EMT, en partant des tests les plus rapides et nombreux (haut de pyramide) vers les plus lents et coûteux (base, exécutés moins fréquemment).

**Légende fréquence d'exécution :** *PR* = à chaque pull request (CI bloquante) ; *nightly* = build quotidien planifié ; *release* = uniquement avant publication NuGet ; *on-demand* = sur déclenchement manuel ou pré-déploiement DEV/REC/PROD.

| # | Type de test | Outils recommandés | Périmètre EMT | Exemples concrets de cas à couvrir | Phase d'introduction | Fréquence CI | Bloquant ? |
|---|---|---|---|---|---|---|---|
| T1 | **Tests unitaires purs** (logique sans I/O) | `xUnit` + `FluentAssertions` + `NSubstitute` | Fonctions pures et classes de logique : `EndpointResolver`, `MessageTargetMap`, `ExponentialRetryPolicy`, `ProducerSendRetryPolicy`, `JsonMessageSerializer`, `RetryPolicyHandler`, futur `RoutingSlip.Advance` (§3.2.4). | Résolution endpoint avec key composite ; backoff exponentiel respecte la borne max ; sérialiseur tolère un champ inconnu ; classifieur de retry distingue `MessagingEntityNotFoundException` (DLQ immédiat) de `ServiceBusException IsTransient=true` (retry). | Phase 1 | PR | ✅ |
| T2 | **Tests unitaires de concurrence** | `xUnit` + `Nito.AsyncEx` + boucles de stress (10k+ itérations) | `CircuitBreakerManager`, `ServiceBusSenderCache`, `Interlocked.CompareExchange` settlement dans `BaseConsumer`. | Deux threads tentent de fermer un message en même temps : un seul réussit ; `GetOrAdd` du cache de senders ne crée pas deux instances pour la même clé ; transitions `Closed → Open → HalfOpen` cohérentes sous concurrence. | Phase 1 | PR | ✅ |
| T3 | **Tests d'architecture (fitness functions)** | `NetArchTest.Rules` ou `ArchUnitNET` | Surface publique, dépendances entre namespaces. | `Configuration.*` ne dépend pas de `Messaging.Providers.Azure.*` ; aucune classe `public` ajoutée hors liste `PublicAPI.Shipped.txt` ; futur `RAMQ.Integration.Abstractions` n'a aucune référence à `Azure.Messaging.ServiceBus` (Phase 6). | Phase 1 | PR | ✅ |
| T4 | **Tests de contrat (snapshot JSON)** | `Verify.Xunit` ou `Snapshooter` | Forme sérialisée du `MessageEnvelope` (à introduire Phase 3), `MessagingOptions`, payload journal A5, attributs CloudEvents (Phase 6). | Toute modification accidentelle d'un nom de champ JSON casse la PR ; ajout d'un nouveau champ doit explicitement valider le snapshot. **Indispensable pour les messages en vol** (rétention longue durée RAMQ). | Phase 3 | PR | ✅ |
| T5 | **Tests de désérialisation défensive** | `xUnit` paramétré + corpus de payloads malformés (`testdata/*.json`) | `JsonMessageSerializer` + `DeserializationResult<T>`. | Payload tronqué → `Failure` non-`Throw` ; champ obligatoire absent → erreur classifiée ; type incompatible → erreur classifiée ; payload vide ; payload avec BOM ; encodage non-UTF8. | Phase 1 (renforcé Phase 3) | PR | ✅ |
| T6 | **Tests de contrat des interfaces de transport** | `xUnit` + suite partagée appliquée à 2+ implémentations | `IMessagingAdapter`, `IMessageTransit`, futurs adapters Kafka / RabbitMQ / InMemory. | Même suite de tests passe sur `AzureServiceBusAdapter`, `InMemoryAdapter` et (Phase 6) `KafkaAdapter`/`RabbitMqAdapter`. C'est l'**oracle de portabilité** : si une suite passe sur InMemory mais échoue sur Kafka, c'est l'adapter Kafka qui est faux, pas la suite. | Phase 5 (préparé) → Phase 6 (généralisé) | PR | ✅ |
| T7 | **Tests d'intégration broker — Service Bus Emulator** | `Azure Service Bus Emulator` (conteneur Docker officiel, dispo 2024+) + `Testcontainers` | Flux end-to-end producteur → broker → consommateur, sessions, dead-letter, `requiresDuplicateDetection`, abandon/complete/dead-letter. | Message dupliqué dans la fenêtre de 10 min → un seul consommé ; session ordonnée par `SessionId` ; après `MaxDeliveryCount` dépassements, message en DLQ avec headers attendus ; abandon explicite incrémente `DeliveryCount`. | Phase 1 (squelette) → Phase 2 (étoffé) | PR (suite courte) + nightly (suite complète) | ✅ PR / nightly informatif |
| T8 | **Tests d'intégration claim-check** | `Azurite` (émulateur Blob) + `Testcontainers` | `BaseMessageTransit` + claim-check upload/download, threshold 256 KB. | Payload < 256 KB → pas d'upload Blob ; payload > 256 KB → upload + référence dans le message ; claim-check orphelin (consommation échouée) → trace métrique `claim_check_orphan_total`. | Phase 2 | nightly | ❌ informatif (PR pour smoke) |
| T9 | **Tests d'intégration journal A5** | `Azurite` (émulateur Tables) | `Journal` pattern A5, écriture asynchrone hors chemin critique. | Indisponibilité du journal n'échoue pas le message ; format `PartitionKey/RowKey` stable ; latence journal n'impacte pas p99 du send. | Phase 2 | nightly | ❌ informatif |
| T10 | **Tests de saga (RoutingSlip)** | `xUnit` + provider InMemory | Logique d'avancement entre étapes, idempotence `__FinalStageCompleted`, compensation, ré-entrée après crash. | Étape N exécutée 2 fois (redelivery) → effet une seule fois ; compensation déclenchée sur erreur métier ; saut d'étape rejeté ; itinéraire vide rejeté ; `Variables` propagés bout-en-bout. | Phase 1 (avec implé actuelle) → Phase 5 (refactor pur) | PR | ✅ |
| T11 | **Tests de configuration et de validation au démarrage** | `xUnit` + `Microsoft.Extensions.Hosting` + `IOptionsValidator` | `MessagingOptions`, `EndpointSettings`, validation `RequiresDuplicateDetection`, validation `MaxDeliveryCount`, validation présence des connection strings / managed identity. | Démarrage sans `requiresDuplicateDetection=true` → log d'avertissement (R3 §4.3.5) ; configuration incohérente → exception explicite avec message actionnable. | Phase 2 | PR | ✅ |
| T12 | **Tests de tracing / observabilité** | `xUnit` + `ActivityListener` + `MeterListener` | `ActivitySource "RAMQ.COM.EnterpriseMessageTransit"`, métriques `MetricsProvider`. | Un publish émet ≥ 3 spans corrélés (publish, send, claim-check.upload si > 256 KB) ; `traceparent` propagé dans `ApplicationProperties` ; compteurs `circuit_state`, `deserialization_failures_total`, `duplicate_detected_total` incrémentés selon attendu. | Phase 1 | PR | ✅ |
| T13 | **Benchmarks de performance** | `BenchmarkDotNet` | `JsonMessageSerializer`, `EndpointResolver`, claim-check upload, `ServiceBusSenderCache.GetOrAdd`. | Sérialisation 10 KB < seuil documenté ; résolution endpoint < N ns ; allocation par send < seuil. | Phase 4 | release + on-demand | ❌ comparaison vs baseline |
| T14 | **Tests de charge / stress** | `k6` ou `NBomber` + Service Bus Emulator (DEV) ou namespace dédié (REC) | Producer batch ; consumer parallel processing ; saga sous charge soutenue. | 1k msg/s pendant 10 min sans dégradation p99 ; saga 5 étapes à 100 msg/s sans race condition ; circuit breaker s'ouvre au seuil prévu sous panne simulée. | Phase 4 | nightly (volume réduit) + on-demand pré-release | ❌ informatif |
| T15 | **Tests de chaos / résilience** | Toxiproxy (latence/coupures) ou `Polly.Simmy` ou défaillances Service Bus injectées | Chemins de retry, circuit breaker, timeout global, recovery après partition réseau. | Latence broker injectée 5 s → timeout global respecté ; coupure complète 30 s → circuit ouvre puis half-open puis close ; perte de connexion pendant batch → retry transparent. | Phase 2 (basique) → Phase 4 (étoffé) | nightly | ❌ informatif |
| T16 | **Tests de mutation** | `Stryker.NET` | Code critique : `RoutingSlip.Advance`, `RetryPolicyHandler.Classify`, `CircuitBreakerManager.RecordFailure`, settlement `Interlocked.CompareExchange`. | Un mutant survivant indique un test manquant : ex. inverser `>=` en `>` sur le seuil claim-check doit être tué. | Phase 5 | nightly (sur modules ciblés) | ❌ informatif (objectif ≥ 70 % mutation score) |
| T17 | **Tests de compatibilité ascendante (back-compat)** | `Microsoft.CodeAnalysis.PublicApiAnalyzers` + corpus de messages v1 archivés | Lecture par EMT v(N) d'un message produit par EMT v(N-1) ; lecture des entrées journal A5 antérieures ; lecture claim-checks blob existants. | Aucune régression silencieuse pour les messages déjà persistés (rétention RAMQ pluriannuelle) ; surface publique sans suppression non-documentée. | Phase 3 | PR (analyzers) + release (corpus) | ✅ analyzers / ❌ corpus informatif |
| T18 | **Tests de portabilité multi-hôte / multi-broker** | Suite T6 généralisée + matrice CI (`matrix: [hosts × transports]`) | Phase 6 uniquement : valider qu'un même consumer applicatif fonctionne sur les 9 combinaisons (3 hôtes × 3 brokers). | Test de portabilité de référence (cf §1.2.5) exécuté sur (Functions+SB), (AKS+Kafka), (ARO+RabbitMQ) ; FIFO par session/partition garanti ; idempotence préservée. | Phase 6 | nightly | ✅ pour la matrice cœur (3 combinaisons), informatif pour les 6 autres |
| T19 | **Tests d'intégration CloudEvents 1.0** | Suites du SDK CloudEvents officiel + golden files | Bindings structured et binary (Phase 6), extensions `ramqitinerary` / `ramqclaimcheck` / `ramqcorrelationid` / `traceparent`. | Sérialisation conforme CNCF ; round-trip structured ↔ binary identique ; extensions RAMQ acceptées par parsers tiers. | Phase 6 | PR | ✅ |
| T20 | **Smoke tests post-déploiement** | Script `dotnet`/`pwsh` exécutant un flow minimal | DEV / REC / PROD : envoyer 1 message, vérifier réception + journal + métrique exposée. | Détecte les erreurs d'**infrastructure** (RBAC manquant, connection string périmée, namespace pas créé) que les autres tests ne couvrent pas. | Phase 2 | post-deploy automatique | ✅ déploiement |

**Comment lire ce tableau.**

- Les lignes **T1-T3, T5, T10-T12** forment la **base de la CI bloquante** (Phase 1) : sans elles, aucune autre phase n'est exécutable en confiance.
- Les lignes **T7-T9** sont des **tests d'intégration broker / stockage** : ils nécessitent des conteneurs locaux mais doivent au minimum tourner sur la CI nightly. Le **Service Bus Emulator** (T7) rend possible aujourd'hui ce qui était impossible en 2023.
- Les lignes **T13-T16** sont les **tests de qualité long-cycle** : ils ne tournent pas à chaque PR, mais leur absence rend impossible la Phase 4 (perf) et la Phase 5 (refactor sûr).
- Les lignes **T4, T6, T17, T18, T19** sont les **tests de garantie pour les contrats publics et la portabilité** : ils sont la seule défense contre la rupture silencieuse de l'écosystème consommateur.
- La ligne **T20** est l'unique test qui s'exécute en environnement réel (REC/PROD) et confirme que l'infrastructure correspond à ce que la librairie attend.

**Ratio cible (heuristique).** Pour une librairie de transit messaging comme EMT, viser approximativement :

- **70 % du temps CI** sur T1-T3, T5, T10-T12 (rapides, par PR).
- **20 %** sur T7-T9 (intégration broker, nightly).
- **10 %** sur T13-T19 (qualité long-cycle, planifiés).

Ce qui change vs une application métier classique : le **poids des tests de contrat (T4, T17, T19) est volontairement élevé** parce que le coût d'un message cassé en production en RAMQ (rétention légale, audit CAI, traçabilité pluriannuelle) est très supérieur au coût d'un bug fonctionnel ponctuel.

### 6.2 🟠 Observabilité — distributed tracing absent et catalogue de métriques non documenté

Les compteurs et histogrammes *techniques* sont en place dans `MetricsProvider`. Ce qui reste à livrer :

- **Un catalogue de métriques documenté** (`docs/observability/metrics.md`) : nom, type, unité, labels, utilisation opérationnelle attendue (alertes, dashboards). Sans ce catalogue, chaque équipe opérationnelle bricole ses propres requêtes KQL.
- **Des métriques métier manquantes** (alignement avec §4.1, §4.3) : `circuit_state{entity}`, `circuit_transitions_total{entity,from,to}`, `deserialization_failures_total{reason}`, `duplicate_detected_total{entity}`, `claim_check_upload_duration_ms`, `claim_check_download_duration_ms`, `journal_write_duration_ms`, `saga_stage_advance_total{from,to}`.
- **Les dashboards de référence** (Azure Monitor Workbook, fichier JSON exportable) : livrable avec la librairie.
- **Un `ActivitySource` / distributed tracing** (OpenTelemetry-compatible) : **absent** — aucune occurrence d'`ActivitySource` ou `StartActivity` dans le code. Criticité haute parce que :
  - Le SDK Azure Service Bus instrumente déjà `Activity` sur send/receive. EMT **casse la chaîne** en n'attachant pas ses propres spans (claim-check upload/download, saga stage advance, retry decisions, circuit transitions).
  - Ajouter `ActivitySource.StartActivity` dans 8-10 points clés a un coût marginal et un gain opérationnel immédiat.
  - Sans cela, en production, on sait qu'une requête est lente mais pas *où*.
  - C'est le **prérequis** pour l'adoption CloudEvents (§1.3.2) puisque `traceparent` voyagera comme extension CloudEvents.

💡 **Pour les développeurs juniors** — l'observabilité n'est pas *ajouter des logs*. C'est **trois piliers** : **traces** (le chemin d'une requête à travers N services, avec leurs latences), **métriques** (les tendances agrégées : « on perd 2 % des messages depuis 10h »), **logs** (les événements discrets pour le debug ponctuel). EMT a aujourd'hui les métriques et les logs ; il lui manque les **traces**, qui sont le seul pilier permettant de comprendre un incident *cross-service*. Un message qui traverse 5 stages de saga sans trace partagée, c'est 5 fichiers de log à corréler à la main.

**Recommandation (horizon immédiat).**

1. Ajouter `ActivitySource "RAMQ.COM.EnterpriseMessageTransit"` exposé statiquement (et récupérable par l'hôte pour l'exportation OpenTelemetry).
2. Placer `StartActivity` sur : `publish`, `send`, `claim-check.upload`, `claim-check.download`, `deserialize`, `saga.advance`, `retry.schedule`, `dlq`, `circuit.open`, `circuit.close`.
3. Propager `traceparent` via `ApplicationProperties` du message (Service Bus préserve nativement ces propriétés). En mode CloudEvents, `traceparent` devient une extension standard de l'enveloppe.
4. Documenter dans `docs/observability/tracing.md` les attributs standards attachés à chaque span (`messaging.system`, `messaging.destination`, `messaging.operation`, etc., suivant les *semantic conventions* OpenTelemetry pour messaging).

### 6.3 🟢 Fondations observabilité à étendre

À conserver et étendre, pas à remplacer :

- Logs structurés avec placeholders nommés (compatibles App Insights / Kusto).
- `MessageId` et `CorrelationId` systématiquement présents dans les logs critiques.
- Health check Service Bus (`ServiceBusHealthCheck`) — à **étendre** (§4.3 R2) pour valider aussi `RequiresDuplicateDetection` et la fenêtre de dedup.
- `Meter` statique exposé — branchable sur n'importe quel exportateur OpenTelemetry (OTLP, Prometheus, Azure Monitor).

---

## 7. Gouvernance, versioning, écosystème

C'est la section la plus spécifique d'une revue distinguished : **qu'est-ce qui fait qu'une librairie réussit son cycle de vie sur 3-5 ans dans une organisation ?**

### 7.1 🔴 Absence de SemVer, de changelog, et d'ADR

Indicateurs vérifiés dans le dépôt actuel :

| Artefact de gouvernance | Présent ? |
|---|---|
| `CHANGELOG.md` à la racine | ❌ Absent |
| Version sémantique dans le `.csproj` (`<Version>`) | ❌ Absent |
| Politique SemVer documentée | ❌ Absent |
| ADR (Architecture Decision Records) | ❌ Absent |
| `CONTRIBUTING.md` | ❌ Absent |
| Issue/PR templates | ❌ (à vérifier hors `src/`) |
| Documentation de support (versions supportées, EOL) | ❌ Absent |

**Conséquences concrètes :**

- Aucun consommateur ne peut savoir *pourquoi* une version `1.3.0` casse son code.
- Aucune trace de *pourquoi* on a choisi de ne pas utiliser MassTransit, d'implémenter claim-check, de coupler à Functions Worker.
- La connaissance des décisions est dans la tête des contributeurs et dans des revues de code dispersées.

**Recommandation (horizon immédiat — 1-2 jours de travail).**

1. Établir `<Version>` dans le `.csproj` (ex. `0.9.0` pour signifier « proche de GA mais pas encore »), appliquer SemVer strict.
2. Créer `CHANGELOG.md` en format [Keep a Changelog](https://keepachangelog.com/).
3. Créer `docs/adr/` et y loger rétrospectivement les 5-10 décisions structurantes (claim-check threshold, Azure Functions couplage, Itinerary flat vs graph, journal Table Storage, pas de MassTransit, etc.).
4. Créer `CONTRIBUTING.md` listant les conventions de langue, le flow de PR, la politique SemVer.

### 7.2 🟠 Ecosystème NuGet et stratégie de publication

Indéterminé dans le code actuel : package privé (Azure DevOps Artifacts / GitHub Packages interne) ? Comment les applications clientes le consomment ? À quelle cadence ?

Sans cela, une librairie « plateforme » devient un **copier-coller glorifié** : chaque équipe a sa version. C'est observable dans l'écosystème .NET d'entreprise et le signe avant-coureur, c'est généralement l'absence de feed privé documenté.

**Recommandation.** Section `docs/consumption.md` précisant : feed NuGet (URL), cadence de release, politique de support (N, N-1), procédure d'upgrade, contact équipe propriétaire.

### 7.3 🟠 Stratégie de dépréciation manquante

La revue Senior mentionne `ServiceBusEntityType` marqué `[Obsolete]` mais non supprimé. La revue Lead mentionne des méthodes `DeserializeMessage` marquées `[Obsolete]` après la refonte async.

Sans stratégie explicite :
- On ne sait pas **quand** ces éléments seront supprimés.
- On ne sait pas **quelle version** fera le retrait.
- Les consommateurs ne sont pas incités à migrer.

**Recommandation.** Convention RAMQ-interne : `[Obsolete("Message explicite. Sera retiré en vX.Y.", error: false)]` → puis `error: true` une version plus tard → puis suppression. Documenté dans `CONTRIBUTING.md`.

### 7.4 🟢 Discipline de revue à institutionnaliser

Les revues Senior et Lead existantes ([EMT-SeniorEngineerReview.md](EMT-SeniorEngineerReview.md), [EMT-LeadEngineerReview.md](EMT-LeadEngineerReview.md)) sont un socle utile. À faire évoluer **en registre continu** (ADR + CHANGELOG + tableau de dette technique versionné) plutôt qu'en snapshots ponctuels, afin que les choix d'architecture restent traçables indépendamment du calendrier des revues.

---

## 8. Synthèse, feuille de route et décisions à prendre

### 8.1 Évaluation globale — sujets ouverts

| Axe | Note | Commentaire sur ce qui reste ouvert |
|---|---|---|
| Thèse produit | ⭐⭐ | Non articulée. Trois produits superposés sans frontière explicite (§1.1). Stratégie multi-hôte + multi-broker non posée (§1.2). |
| Qualité du code | ⭐⭐⭐⭐ | Dette de surface publique (~80 types `public`, pas d'`InternalsVisibleTo`) (§2.1). Extensibilité fictive sur 7 interfaces (§2.2). |
| Design & architecture | ⭐⭐⭐ | Contrat pivot non versé (§3.1), saga couplée à `BaseConsumer` (§3.2), `IMessageTransit` sous-dimensionné (§3.4), couplage Functions Worker (§3.3). |
| Robustesse & fiabilité | ⭐⭐⭐⭐ | Briques en place, mais aucun runbook, ni métriques dédiées (§4.1). Idempotence producteur sans prérequis infra documenté (§4.3). Timeout global non borné (§4.4). |
| Performance & scalabilité | ⭐⭐⭐ | Pas d'enveloppe opérationnelle documentée (§5.1). Journal synchrone O(n) en batch (§5.2). Allocations LINQ résolveur (§5.3). |
| Testabilité & observabilité | ⭐⭐⭐ | Aucun projet de tests (§6.1). Distributed tracing absent (§6.2). Catalogue métriques non documenté. |
| Gouvernance & écosystème | ⭐⭐ | `CHANGELOG.md`, `<Version>`, ADR, `CONTRIBUTING.md` absents (§7.1). Stratégie NuGet / dépréciation non formalisée (§7.2, §7.3). |

### 8.2 Décisions structurantes à prendre (ne peuvent pas attendre)

Ces décisions ne sont pas du ressort d'un sprint : elles engagent le produit pour 2-3 ans. À documenter en **ADR** avec signataires.

| # | Décision | Options | Criticité | Statut |
|---|---|---|---|---|
| D1 | Thèse de portabilité (§1.2) | A. Assumer Azure · B. Scinder en 3 assemblies · C. Statu quo | 🔴 Critique | Ouverte |
| D2 | Frontière public/internal (§2.1) | Périmètre explicite des types publics, `InternalsVisibleTo` ciblé | 🟠 Haute | Ouverte |
| D3 | Contrat de sérialisation `MessageTransitContext` (§3.1) | Envelope versionné / CloudEvents / statu quo | 🔴 Critique | Ouverte |
| D4 | Séparation saga / messaging (§3.2) | Module dédié `RoutingSlip` ou inchangé | 🟠 Haute | Ouverte |
| D5 | Stratégie de test et d'intégration (§6.1) | Projet tests + emulator Service Bus + CI bloquante | 🟠 Haute | Ouverte |
| D6 | SemVer + ADR + CHANGELOG (§7.1) | Adoption immédiate | 🟠 Haute | Ouverte |
| D7 | Distributed tracing OpenTelemetry (§6.2) | `ActivitySource` + propagation `traceparent` | 🟠 Haute | Ouverte |
| D8 | Politique de classification des échecs de désérialisation (§4.2) | Matrice `DeserializationFailureReason` → action (DLQ / drop / alerte) | 🟡 Moyenne | Ouverte (type en place, politique manquante) |
| D9 | Runbook du circuit breaker (§4.1) | Seuils ops, alertes, métriques dédiées | 🟡 Moyenne | Ouverte |

### 8.3 Plan progressif par phases

💡 **Pour les développeurs juniors — comment lire ce plan.** Chaque phase est une **itération cohérente** : elle produit une valeur autoportante et ne dépend pas de la phase suivante pour être utile. L'ordre choisi privilégie ce qui :
1. **rend la plateforme observable et non-régressable** d'abord (on ne refactore pas ce qu'on ne mesure pas) ;
2. **solidifie les contrats** ensuite (une fois qu'on voit ce qui se passe) ;
3. **restructure l'intérieur** (saga, API, perf) une fois les filets en place ;
4. **et seulement alors** ouvre la portabilité \u2014 décision la plus lourde, celle qui profite le plus des trois étapes précédentes.

La portabilité **doit** venir en dernier : scinder en plusieurs assemblies sans tests, sans CHANGELOG, sans ActivitySource et sans contrat pivot versionné, c'est *figer la dette en la répliquant dans 3 packages*.

#### 8.3.1 Tableau de synthèse des phases

> 💡 **Lecture de la colonne « Risque / Complexité ».** *Risque* mesure l'**impact d'un échec ou d'une régression** sur la production et sur les applications consommatrices. *Complexité* mesure l'**effort technique et la coordination** requis (équipes impliquées, surface modifiée, dépendances externes). Les deux sont notés sur une échelle 🟢 *faible* — 🟡 *moyen* — 🟠 *élevé* — 🔴 *très élevé*.

| Phase | Objectif central | Durée indicative | Risque / Complexité | Livrables principaux | Décisions dépendantes | Condition de sortie |
|---|---|---|---|---|---|---|
| **Phase 1 — Fondations non-régressables** | Rendre la plateforme observable, versionnée, testée, et figer la surface publique. | 4-6 semaines | **Risque 🟢 faible** — aucune modification de comportement runtime ; uniquement outillage et instrumentation additive. **Complexité 🟡 moyenne** — coordination DevOps (CI bloquante), discipline ADR, choix initial de la stratégie de tests (cf §6.1.1). Principal piège : sous-dimensionner la suite T1-T6 et T10-T12 et payer la dette dès la Phase 2. | Projet `EnterpriseMessageTransit.Tests` + CI bloquante ; `CHANGELOG.md` + `<Version>` dans le `.csproj` ; `docs/adr/` (D1-D7) ; `PublicApiAnalyzers` ; `InternalsVisibleTo` ; `ActivitySource` + propagation `traceparent` ; catalogue métriques `docs/observability/metrics.md`. | D2, D6, D7 | CI verte, surface publique figée (diff visible en PR), chaque message produit au moins 3 spans corrélés. |
| **Phase 2 — Durcissement opérationnel** | Transformer les briques récemment livrées (circuit breaker, `DeserializationResult<T>`) en outils opérationnels utilisables par le SRE. | 4-6 semaines | **Risque 🟡 moyen** — touche les chemins d'erreur en production : un mauvais seuil de circuit breaker ou un timeout global mal calibré peut bloquer du trafic légitime. **Complexité 🟡 moyenne** — coordination forte avec SRE/exploitation (runbooks, alerting), prérequis infra Service Bus (`requiresDuplicateDetection`) qui implique parfois la **recréation** d'entités existantes (cf §4.3.3). Atténuation : feature flags + déploiement progressif DEV→REC→PROD, fenêtre de bake nightly avant promotion. | Politique classification désérialisation (D8) ; compteurs `deserialization_failures_total`, `circuit_state`, `circuit_transitions_total` ; timeout global `PublishAsync` configurable (§4.4) ; runbook circuit breaker (D9) ; nettoyage claim-checks orphelins (lifecycle Blob + compensation explicite) ; `docs/failure-modes.md` ; prérequis infra Service Bus (`requiresDuplicateDetection`, `maxDeliveryCount`). | D8, D9 | Tout mode de défaillance du §4 a une métrique, un log structuré, un runbook. |
| **Phase 3 — Contrats et schéma pivot** | Empêcher la casse silencieuse des messages en vol. | 6-8 semaines | **Risque 🟠 élevé** — toute erreur de migration enveloppe v1→v2 peut **casser des messages persistés** (rétention RAMQ pluriannuelle, journal A5, claim-checks Blob existants). **Complexité 🟠 élevée** — nécessite tests de contrat T4 et back-compat T17 *avant* tout changement, coordination avec **toutes les applications consommatrices** pour la fenêtre de transition v1+v2. Atténuation : période de coexistence v1/v2 obligatoire, version pivot lue par les deux runtimes, gel des autres modifications de surface pendant la migration. | ADR D3 signé ; `MessageEnvelope` versionné séparé du runtime `MessageTransitContext<T>` ; tests de contrat JSON snapshot sur l'enveloppe et `MessagingOptions` ; guide de migration enveloppe v1 → v2 ; conventions de sérialisation FR/EN (`docs/conventions.md`). | D3 | Tout changement de forme sérialisée casse la CI via un snapshot test ; champ `SchemaVersion` obligatoire. |
| **Phase 4 — Performance et enveloppe** | Déclarer et tenir une enveloppe opérationnelle chiffrée. | 4-6 semaines | **Risque 🟡 moyen** — décider tôt d'une enveloppe peut figer des limites trop basses ou trop optimistes ; un changement de stratégie journal (sync→async, §5.2) modifie la garantie d'auditabilité (impact CAI). **Complexité 🟡 moyenne** — infra de bench dédiée (T13), pipelines de charge (T14) reproductibles, accord SLO avec les domaines consommateurs. Atténuation : publier les chiffres avec leurs **conditions de validité** explicites (taille payload, version SDK, SKU broker). | Benchmarks `BenchmarkDotNet` (sérialisation, EndpointResolver, claim-check) ; tests de charge k6/NBomber sur Functions + Service Bus Emulator ; `docs/operational-envelope.md` ; cache `EndpointResolver` (§5.3) ; journal batch / async (§5.2) selon décision d'auditabilité ; SLO et alertes en face. | — | Chiffres p50/p99 publiés ; limite `PublishBatchAsync` documentée ; décision audit journal tracée. |
| **Phase 5 — Refactoring architectural interne** | Isoler la saga et enrichir `IMessageTransit` **sans casser l'API cliente**. | 6-10 semaines | **Risque 🟠 élevé** — refactor du **cœur** de la librairie (saga, contrat consumer) ; toute régression touche immédiatement chaque consommateur applicatif. **Complexité 🔴 très élevée** — extraction d'un saga aujourd'hui dispersé sur 5 emplacements (cf §3.2.2), équivalence comportementale à démontrer (T16 mutation testing), enrichissement d'`IMessageTransit` sans rupture binaire des consumers existants. Atténuation : plan en 5 étapes E1-E5 (§3.2.5), facade `[Obsolete]` pour les anciennes APIs, période de coexistence ≥ 1 release. | Module `RAMQ.Integration.RoutingSlip` extrait (folder + namespace, pas encore assembly séparé) ; `IMessageTransit` enrichi (`ApplicationProperties`, `DeliveryCount`, `EnqueuedTimeUtc`, `CorrelationId`, `ReplyTo`) ; `IMessagingAdapter.BindContext` typé (suppression du `BindContext(object, object)` au profit d'une fabrique) ; `RawMessage` rendu `internal`. | D4 | Aucun cast `IMessageTransit` → `AzureFunctionMessageTransit` nécessaire côté consommateur ; saga testable sans Service Bus. |
| **Phase 6 — Portabilité et ouverture d'écosystème** | *Seulement* si D1 = option B. Rendre possible l'usage multi-hôte (Azure Functions / AKS / ARO) et multi-broker (Service Bus / Kafka Confluent / RabbitMQ) décrits à [§1.2](#12--bloquant-stratégique--absence-de-thèse-de-portabilité-et-darchitecture-cible). | 10-16 semaines | **Risque 🔴 très élevé** — **breaking changes NuGet majeurs** (10 packages au lieu d'un, renommages de namespaces) impactant chaque application consommatrice ; chaque transport additionnel (Kafka, RabbitMQ) introduit ses propres modes de défaillance et sémantiques (offsets vs sequence numbers, partitions vs sessions). **Complexité 🔴 très élevée** — multiplication des pipelines CI (matrice T18 hôte×transport), des ADR, du support N/N-1, des compétences (équipe doit maîtriser Kafka *et* RabbitMQ *et* Service Bus en plus d'AKS et ARO). Atténuation : ne lancer **que** si la Phase 5 est passée verte, valider en premier la combinaison `InMemoryAdapter` (T6) avant tout adapter réel, livrer un transport à la fois (Kafka **ou** RabbitMQ d'abord, jamais les deux en parallèle). | Scission en assemblies : `RAMQ.Integration.Abstractions`, `.Envelope`, `.RoutingSlip`, `.Transport.ServiceBus`, `.Transport.Kafka`, `.Transport.RabbitMq`, `.Storage.AzureBlob`, `.Journal.AzureTable`, `.Hosting.Functions`, `.Hosting.AspNetCore` ; adoption CloudEvents 1.0 pour le `MessageEnvelope` (cf §1.3.2) ; 2ᵉ provider de test (`InMemoryMessagingProvider`) pour valider l'abstraction ; guide d'intégration BackgroundService pour AKS/ARO ; guide d'intégration Kafka Confluent et RabbitMQ ; intégration des bindings CloudEvents officiels (structured mode par défaut). | D1, D3 | Un même consumer applicatif passe les mêmes tests de contrat sur (a) Functions + Service Bus, (b) BackgroundService AKS + Kafka, (c) BackgroundService ARO + RabbitMQ, **sans modification de code métier**. |

#### 8.3.2 Logique d'enchaînement (pourquoi cet ordre)

💡 **Pour les développeurs juniors**

- **Phase 1 avant tout le reste** : sans tests, chaque phase ultérieure est un pari. Sans `ActivitySource`, la Phase 2 ne peut pas *mesurer* si elle a amélioré quoi que ce soit. Sans ADR, les phases suivantes re-négocient à chaque PR les décisions déjà prises implicitement.
- **Phase 2 juste après** : les briques livrées dans le code (`CircuitBreakerManager`, `DeserializationResult<T>`) ne deviennent *vraiment* utiles qu'instrumentées et routées par une politique. Sans Phase 2, elles risquent de devenir de la dette invisible.
- **Phase 3 avant la refonte interne** : le schéma pivot `MessageTransitContext` **voyage** entre services. Toute modification de son code (même « mineure ») pendant la Phase 5 risque de casser un message en vol si on n'a pas posé la frontière enveloppe/runtime et les tests de contrat d'abord.
- **Phase 4 avant la Phase 5** : le refactoring architectural (Phase 5) doit être validé contre des chiffres de performance existants (Phase 4). Sans benchmarks de référence, on ne peut pas démontrer qu'un refactor n'a pas dégradé la latence p99.
- **Phase 5 avant la Phase 6** : c'est en Phase 5 que `IMessageTransit` devient *réellement* portable (aujourd'hui il ne l'est pas, malgré son apparence). Scinder en assemblies (Phase 6) avant d'avoir corrigé la fuite Service Bus dans `IMessageTransit` reviendrait à scinder *l'apparence* de portabilité, pas la chose elle-même.
- **Phase 6 en dernier** : c'est la phase la plus lourde et la plus risquée (breaking changes NuGet pour toutes les applications clientes, multiplication des pipelines, des ADR, du support N/N-1). Elle ne vaut le coût que si l'option B de D1 est activement retenue *et* si les phases 1-5 ont produit les filets permettant d'exécuter la scission sans régression.

#### 8.3.3 Jalons de décision (go / no-go entre phases)

| Après phase | Décision à (re)prendre | Impact si no-go |
|---|---|---|
| Phase 1 | Continuer avec le plan ou ré-ouvrir D1 (option A : assumer Azure) | Si option A retenue : on saute directement les phases 5 et 6, on consolide sous un nom `RAMQ.Azure.Messaging.ServiceBus.Transit`, fin de plan. |
| Phase 2 | La politique de classification des échecs est-elle opérable (alertes sans faux positifs) ? | Ajuster seuils avant Phase 3. |
| Phase 3 | L'enveloppe versionnée est-elle adoptée sans résistance applicative ? | Sinon, figer en v1 et documenter une politique de dépréciation explicite. |
| Phase 4 | L'enveloppe opérationnelle tient-elle face au trafic réel ? | Sinon, Phase 5 doit intégrer des optimisations ciblées (journal async, cache Resolver). |
| Phase 5 | `IMessageTransit` couvre-t-il 100 % des besoins des consommateurs applicatifs (zéro cast `RawMessage`) ? | Sinon, ne pas lancer la Phase 6 \u2014 la portabilité ne tiendra pas. |
| Phase 6 | Un cas d'usage non-Azure concret existe-t-il ? | Sinon, ne pas exécuter. La préparation des phases 1-5 couvre déjà 80 % de la valeur. |

### 8.4 Ce qu'il ne faut **pas** faire

- ❌ **Ne pas** entamer la Phase 6 (portabilité / scission en assemblies) avant d'avoir validé les Phases 1 à 5. Scinder sans tests, sans `ActivitySource`, sans contrat pivot versionné et sans `IMessageTransit` enrichi revient à **répliquer la dette dans plusieurs packages**.
- ❌ **Ne pas** renommer ou modifier `MessageTransitContext` avant d'avoir terminé la Phase 3 (enveloppe versionnée + tests de contrat) — risque de casse silencieuse en production sur messages en vol.
- ❌ **Ne pas** introduire de nouveaux `public` sans justification explicite dans la PR ni basculement d'un `public` existant en `internal` de manière compensatoire.
- ❌ **Ne pas** considérer `DeserializationResult<T>` et `CircuitBreakerManager` comme « terminés » : ce sont des briques, pas des fonctionnalités. Sans les métriques, runbooks et politiques de la Phase 2, elles restent de la complexité non-observable.
- ❌ **Ne pas** différer les ADR au motif qu'on « écrit du code ». C'est l'inverse : c'est *parce qu'on a écrit du code à valeur stratégique* qu'il faut documenter les décisions.
- ❌ **Ne pas** ajouter de nouvelles fonctionnalités transverses (ex. backpressure, scheduling, deferred delivery) avant la fin de la Phase 3. Chaque feature ajoute de la surface ; sans frontière stable, cette surface devient de la dette.

### 8.5 Points forts fondamentaux à préserver

L'exécution au niveau ligne et module est solide. Les éléments ci-dessous sont à **ne pas perdre** pendant les refactors :

- La séparation `Configuration / Messaging / Providers` est saine et doit servir de modèle pour la scission en assemblies (Phase 6, §1.2.3).
- Le pattern A5 (journal découplé du chemin critique) est à répliquer tel quel dans les futurs providers Kafka/RabbitMQ.
- L'adoption de `IAsyncDisposable`, `ConcurrentDictionary`, `Interlocked.CompareExchange` aux bons endroits témoigne d'une maîtrise de la concurrence .NET — mêmes standards attendus sur les nouveaux packages.
- La capacité de l'équipe à absorber des recommandations structurantes en un cycle (cf livraisons post-Senior/Lead) est le meilleur prédicteur de la réussite du plan par phases ci-dessus.

---

---

## Annexe — Glossaire distinguished-level

💡 **Pour les développeurs juniors**

| Terme | Définition opérationnelle |
|---|---|
| **ADR** (Architecture Decision Record) | Document court (1 page) qui fige *pourquoi* un choix a été fait, *quelles alternatives* ont été écartées, *quand* il pourra être revisité. Format standard : titre, statut, contexte, décision, conséquences. |
| **SemVer** (Semantic Versioning) | Convention `MAJOR.MINOR.PATCH` où MAJOR = breaking change, MINOR = ajout rétrocompatible, PATCH = correction. Indispensable dès qu'une librairie est consommée par plusieurs équipes. |
| **Surface publique** (Public API Surface) | Ensemble des types et membres `public` de l'assembly. Chacun est un contrat. La règle d'or : *small public surface, large internal surface*. |
| **Contrat pivot** (Canonical Schema) | Format de données partagé par plusieurs services. Un message en vol pendant un déploiement = un contrat pivot. Versionner explicitement est obligatoire. |
| **Enveloppe de fonctionnement** (Operational Envelope) | Déclaration formelle : « ce logiciel tient X req/s sous condition Y, avec latence p99 < Z ». En dehors, comportement non garanti. |
| **Test de contrat** (Contract Test / Snapshot Test) | Test qui compare la sérialisation d'un objet à une référence stockée en fichier. Échoue si le format de sortie change. |
| **CloudEvents** | Spécification CNCF d'un format d'enveloppe pour événements distribués. Neutre vis-à-vis du transport (HTTP, Kafka, MQTT, AMQP). Interopérable avec Azure Event Grid, AWS EventBridge. |
| **OpenTelemetry** (OTel) | Standard CNCF pour traces, métriques, logs. `System.Diagnostics.Activity` en .NET y est directement compatible. |
| **Circuit breaker** | Pattern qui *arrête* les tentatives d'appel vers un service manifestement en panne, laisse passer quelques appels de test, réactive. Évite l'amplification de panne. |
| **Backpressure** | Mécanisme qui ralentit le producteur quand le consommateur est saturé. Sans backpressure, le système accumule du travail latent jusqu'à OOM. |
| **Claim-check pattern** | Pattern EIP : le payload lourd est stocké en blob, seul un pointeur circule sur le bus. Permet de contourner les limites de taille de message. |
| **Routing slip / Saga** | Pattern où le message porte lui-même son itinéraire (liste d'étapes) au lieu qu'un orchestrateur central le dirige. Plus résilient, plus difficile à changer globalement. |

---

**Fin de revue — révision 3.**

> Cette revue ne remplace pas les revues Senior et Lead — elle les **complète** en traitant le plan *stratégique et organisationnel* qu'elles n'avaient pas vocation à couvrir. Les trois documents ([EMT-SeniorEngineerReview.md](EMT-SeniorEngineerReview.md), [EMT-LeadEngineerReview.md](EMT-LeadEngineerReview.md), [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md)) doivent être lus ensemble pour avoir la vision complète de l'état de la librairie.
>
> **Révision 3 (24 avril 2026)** — focus sur les sujets encore ouverts :
>
> - Dashboard O1-O20 des points ouverts (§0.1), les points réglés ne sont plus listés.
> - Patterns d'intégration spécifiques RAMQ expliqués point par point (R1-R9, §1.1.1).
> - Architecture cible de portabilité multi-hôte (Azure Functions / AKS / ARO) et multi-broker (Service Bus / Kafka Confluent / RabbitMQ) de bout en bout (§1.2.1 à §1.2.6).
> - Rationale anti-MassTransit (L1-L3) et plan d'adoption CloudEvents 1.0 (§1.3.1, §1.3.2).
> - Analyse approfondie Saga/RoutingSlip avec plan de migration en 5 étapes (§3.2).
> - Idempotence producteur pédagogique (`RequiresDuplicateDetection` expliqué, recommandation en 5 actions concrètes, §4.3).
