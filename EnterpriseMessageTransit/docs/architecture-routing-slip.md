---
### Note de référence EMT

> **Note : La structure du slip (SlipEnvelope, SlipStep, Subscription) et la logique de résolution automatique de Consumer/Action par le RoutingSlipBuilder/IEndpointResolver constituent désormais la référence officielle EMT pour tout workflow Routing Slip.**
>
> Toute évolution, outillage, test d’intégration ou documentation future doit s’aligner sur ce format : Consumer et Action sont portés explicitement dans chaque étape Topic du slip, et sont publiés comme Application Properties par le RoutingSlipExecutor lors de l’envoi sur Service Bus.
>
> Ce format garantit l’auditabilité, la portabilité et la cohérence inter-applications dans l’écosystème EMT.
#### Exemple JSON — slip avec Consumer et Action

Voici à quoi ressemble le slip (SlipEnvelope) sérialisé, avec Consumer et Action présents dans chaque étape Topic :

```json
{
    "slipId": "slip-001",
    "slipName": "TraiterDossier",
    "cursor": 1,
    "steps": [
        {
            "name": "ValiderAdmissibilite",
            "entityName": "queue-valider-admissibilite",
            "entityType": "Queue",
            "arguments": { "DossierId": "D-001" },
            "status": "Completed"
        },
        {
            "name": "EnrichirDonnees",
            "entityName": "topic-enrichir",
            "entityType": "Topic",
            "subscription": {
                "consumer": "EnrichirConsumer",
                "action": "Traiter"
            },
            "arguments": { "DossierId": "D-001" },
            "status": "Active"
        },
        {
            "name": "NotifierBeneficiaire",
            "entityName": "queue-notifier",
            "entityType": "Queue",
            "arguments": { "DossierId": "D-001" },
            "status": "Pending"
        }
    ],
    "variables": {}
}
```

> Dans cet exemple, l’étape "EnrichirDonnees" porte bien les propriétés `consumer` et `action` dans son objet `subscription`. Ces valeurs sont injectées automatiquement par le RoutingSlipBuilder via l’IEndpointResolver, et voyageront dans le message jusqu’au worker.
# Architecture — Routing Slip pour EnterpriseMessageTransit

> **Statut :** Approuvé — v2.0 (mai 2026)
> **Audience :** Développeur junior · Développeur intermédiaire · Lead technique
> **Date :** Mai 2026
> **Révision :** Aligné avec `EMT-DistinguishedEngineerReview.md` (révision 3, 24 avril 2026)

---

## Table des matières

1. [C'est quoi un Routing Slip ? (analogie)](#1-cest-quoi-un-routing-slip--analogie)
2. [Pourquoi on en a besoin dans EMT](#2-pourquoi-on-en-a-besoin-dans-emt)
3. [La configuration `Itinerary` existante — ce qu'elle était](#3-la-configuration-itinerary-existante--ce-quelle-était)
4. [Problèmes du design actuel — explication détaillée](#4-problèmes-du-design-actuel--explication-détaillée)
5. [Design cible — vue d'ensemble](#5-design-cible--vue-densemble)
6. [Les pièces du puzzle — chaque composant expliqué](#6-les-pièces-du-puzzle--chaque-composant-expliqué)
   - [6.1 `IRoutingSlipActivity<TArgs>` — votre code métier](#61-iroutingslipactivitytargs--votre-code-métier)
   - [6.2 `ActivityContext<TArgs>` — ce que le framework vous donne](#62-activitycontexttargs--ce-que-le-framework-vous-donne)
   - [6.3 `ActivityResult` — ce que vous retournez](#63-activityresult--ce-que-vous-retournez)
   - [6.4 `RoutingSlipBuilder` — côté activateur](#64-routingslipbuilder--côté-activateur)
   - [6.5 `SlipEnvelope` — le message qui circule sur Service Bus](#65-slipenvelope--le-message-qui-circule-sur-service-bus)
   - [6.6 `RoutingSlipExecutor` — le chef d'orchestre (interne EMT)](#66-routingslipexecutor--le-chef-dorchestre-interne-emt)
   - [6.7 `MessageEnvelope` — l'enveloppe unifiée enterprise](#67-messageenvelope--lenveloppe-unifiée-enterprise)
7. [Workers, RoutingSlipExecutor et intégration Service Bus](#7-workers-routingslipexecutor-et-intégration-service-bus)
   - [7.1 Relation Worker → RoutingSlipExecutor → Activity](#71-relation-worker--routingslipexecutor--activity)
   - [7.2 Consumer et Action — propriétés de message, pas de l'infrastructure](#72-consumer-et-action--propriétés-de-message-pas-de-linfrastructure)
   - [7.3 Worker Queue — ProcessAsync](#73-worker-queue--processasync)
   - [7.4 Worker Topic — ExecuteAsync](#74-worker-topic--executeasync)
   - [7.5 Pattern IServiceScopeFactory — Captive Dependency](#75-pattern-iservicescopefactory--captive-dependency)
   - [7.6 Configuration DI et local.settings.json](#76-configuration-di-et-localsettingsjson)
8. [Comment ça s'appuie sur EMT existant](#8-comment-ça-sappuie-sur-emt-existant)
9. [Flux pas-à-pas — ce qui se passe vraiment](#9-flux-pas-à-pas--ce-qui-se-passe-vraiment)
10. [Format du message sur Service Bus (wire format v2.0)](#10-format-du-message-sur-service-bus-wire-format-v20)
11. [Exemple complet — traitement d'un dossier RAMQ](#11-exemple-complet--traitement-dun-dossier-ramq)
12. [Observabilité — comment voir ce qui se passe](#12-observabilité--comment-voir-ce-qui-se-passe)
13. [Gestion des erreurs et retry natif EMT](#13-gestion-des-erreurs-et-retry-natif-emt)
14. [Compensation — annuler ce qui a déjà été fait](#14-compensation--annuler-ce-qui-a-déjà-été-fait)
15. [Exemple complet bout-en-bout avec retry et compensation](#15-exemple-complet-bout-en-bout-avec-retry-et-compensation)
16. [Migration depuis le design actuel](#16-migration-depuis-le-design-actuel)
    - [Scénarios de déploiement continu (rolling deployment)](#scénarios-de-déploiement-continu-rolling-deployment)
17. [Ce qui est hors périmètre](#17-ce-qui-est-hors-périmètre)
18. [Glossaire](#18-glossaire)


---

## 1. C'est quoi un Routing Slip ? (analogie)

### L'analogie du bon de livraison interne

Imagine un document papier qui circule dans les bureaux d'une organisation. Quand quelqu'un reçoit ce document, il fait son travail (valider, signer, enrichir), puis transmet le document au bureau suivant. **La liste complète des bureaux à visiter est écrite sur le document lui-même** — pas dans un registre central.

```
Document (avec itinéraire écrit dessus)
│
├── Bureau 1 : Valider le dossier  ✅ (fait)
│       └── Transmet au Bureau 2
├── Bureau 2 : Enrichir les données  ← ICI MAINTENANT
│       └── Transmettra au Bureau 3
└── Bureau 3 : Notifier le bénéficiaire  (à faire)
```

C'est exactement le **Routing Slip** en messagerie :
- Le message porte l'itinéraire complet
- Chaque service fait SON travail uniquement
- Le framework s'occupe de passer le message au suivant

### Pourquoi c'est différent d'une simple chaîne de messages

| | Chaîne simple | Routing Slip |
|--|---------------|-------------|
| Qui connaît l'itinéraire ? | Chaque service individuel | Le message lui-même |
| Pour changer l'ordre | Modifier chaque service | Modifier l'activateur seulement |
| Plusieurs itinéraires différents | Difficile | Naturel |
| Testabilité | Nécessite toute la chaîne | Chaque étape testable seule |

---

## 2. Pourquoi on en a besoin dans EMT

### Le scénario typique à RAMQ

```
Réception d'une demande de bénéficiaire
    ↓
1. Valider l'admissibilité du dossier
    ↓
2. Enrichir avec les données du registre
    ↓
3. Calculer les prestations
    ↓
4. Notifier le bénéficiaire
```

Ce type de traitement multi-étapes séquentiel est exactement le cas d'usage du Routing Slip.

### Ce que ça n'est PAS

- **Pas une saga stateful** : si vous avez besoin de mémoriser l'état pendant plusieurs jours, d'attendre une réponse humaine, ou de compenser des étapes déjà faites → **Azure Durable Functions**
- **Pas un orchestrateur central** : il n'y a pas de service qui surveille l'avancement de toutes les instances en parallèle
- **Pas du Request/Reply** : le Routing Slip est orienté traitement en arrière-plan (fire and forget avec traçabilité)

---

## 3. La configuration `Itinerary` existante — ce qu'elle était

### La vision originale d'EMT

La configuration `AppSettings.Itinerary` dans `appsettings.json` était **pensée dès le départ pour faire du Routing Slip**. La preuve : elle n'est pas une simple liste de endpoints — elle encode un **itinéraire ordonné avec des étapes**.

Voici comment elle est définie dans `AppSettings.cs` :

```csharp
public class AppSettings : IValidatableObject
{
    public string ServiceBusNamespace  { get; set; }
    public string ApplicationName      { get; set; }
    // ...
    public List<EndpointSettings> Itinerary { get; set; }  // ← l'itinéraire Routing Slip
}
```

Et `EndpointSettings` avec `TransportSettings` représente chaque étape :

```csharp
public class EndpointSettings
{
    public string?           Target   { get; set; }   // nom logique de l'étape (ex: "ValiderAdmissibilite")
    public TransportSettings Endpoint { get; set; }   // détails transport (queue, topic, subscription)
}

public class TransportSettings
{
    public string              EntityName   { get; set; }   // nom de la queue ou du topic Service Bus
    public MessagingEntityType EntityType   { get; set; }   // Queue ou Topic
    public SubscriptionInfoSettings? Subscription { get; set; } // si Topic : Consumer + Action
    public TimeSpan            PublishTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public class SubscriptionInfoSettings
{
    public string  Consumer { get; set; }  // nom du consumer abonné
    public string? Action   { get; set; }  // action optionnelle (ex: "Traiter")
}
```

### Exemple de configuration actuelle

```json
{
  "ConsumerConfiguration": {
    "ServiceBusNamespace": "mon-namespace.servicebus.windows.net",
    "ApplicationName": "TraiterDossierApp",
    "Itinerary": [
      {
        "Target": "ValiderAdmissibilite",
        "Endpoint": {
          "EntityName": "queue-valider",
          "EntityType": "Queue"
        }
      },
      {
        "Target": "EnrichirDonnees",
        "Endpoint": {
          "EntityName": "queue-enrichir",
          "EntityType": "Queue"
        }
      },
      {
        "Target": "NotifierBeneficiaire",
        "Endpoint": {
          "EntityName": "queue-notifier",
          "EntityType": "Queue"
        }
      }
    ]
  }
}
```

### Ce que le code actuel fait avec cette config

L'`ItineraryPlanner` transforme la liste `EndpointSettings` en un `RoutingSlip` (objet en mémoire) **au démarrage de l'application** :

```csharp
// ItineraryPlanner.cs — construit le slip depuis la config au démarrage
public RoutingSlip Plan(IReadOnlyList<EndpointSettings> itinerary)
{
    var stages = new SlipStage[itinerary.Count];
    for (int i = 0; i < itinerary.Count; i++)
    {
        var stageId = ResolveStageId(itinerary[i]); // "ValiderAdmissibilite", "EnrichirDonnees", etc.
        stages[i] = new SlipStage(target: itinerary[i].Target, stageId: stageId);
    }
    return new RoutingSlip(stages);
}
```

Et dans `BaseConsumer`, `FindIndexFromStage()` cherche l'étape courante dans ce `RoutingSlip` en mémoire pour savoir où on en est dans l'itinéraire.

L'`EndpointResolver` utilise lui aussi cet itinéraire avec un cache `Lazy<T>` (pré-calculé à la première résolution) pour résoudre les endpoints de façon performante :

```csharp
// EndpointResolver.cs — cache O(1) pré-calculé au premier accès
private readonly Lazy<(List<EndpointSettings> Topics, List<EndpointSettings> Queues,
    Dictionary<string, EndpointSettings> ProducerIndex)> _cache;
```

### Conclusion : l'intention était bonne, l'exécution incomplète

La config `Itinerary` était bien pensée pour du Routing Slip. Le problème : **l'itinéraire reste dans l'application** (`appsettings.json` + mémoire) alors qu'il devrait **voyager avec le message**. C'est la seule correction fondamentale à apporter.

---

## 4. Problèmes du design actuel — explication détaillée

### 4.1 L'itinéraire est splitté en deux endroits

Comme expliqué en section 3, la config `Itinerary` encode l'itinéraire côté application. Le message ne porte que `CurrentStage` — l'endroit où il en est. Ces deux informations doivent être cohérentes à chaque saut.

#### Pourquoi les configs dérivent — même si on fait attention

Un lecteur attentif pense : *"on n'a qu'à copier le même fichier partout"*. Le problème est que dans une architecture de microservices, chaque application est **déployée indépendamment**, par des équipes différentes, à des moments différents. La dérive n'est pas une faute intentionnelle — c'est une conséquence inévitable des déploiements découplés.

Le cas le plus dangereux est le **rolling deployment** : pendant les quelques minutes où une nouvelle version se déploie, certaines instances tournent avec l'ancienne config et d'autres avec la nouvelle. Un message reçu par une ancienne instance sera routé vers l'ancien prochain step. Il n'y a pas de verrou, pas de coordination — la fenêtre d'incohérence est silencieuse.

```
Déploiement en cours — T+2 minutes

Instance A (ancienne config)          Instance B (nouvelle config)
────────────────────────────          ──────────────────────────────────
Itinerary:                            Itinerary:
  1. ValiderAdmissibilite               1. ValiderAdmissibilite
  2. EnrichirDonnees          ≠         2. NotifierBeneficiaire  ← ordre changé
  3. NotifierBeneficiaire               3. EnrichirDonnees

Message reçu par Instance A : route vers EnrichirDonnees
Message reçu par Instance B : route vers NotifierBeneficiaire ← sans enrichissement !
```

**Aucune exception n'est levée.** Le message est simplement routé vers la mauvaise étape ou complété prématurément. L'erreur ne sera détectée qu'en production, bien après le déploiement.

De plus, les messages **déjà en vol** dans les queues au moment du déploiement portent un `CurrentStage` basé sur l'ancien itinéraire. Si la nouvelle config change l'ordre, ces messages en transit sont immédiatement mal routés dès que la nouvelle version prend en charge la queue.

### 4.2 Le consumer est couplé au framework

Le vrai problème de couplage n'est pas que le consumer « connaît » le nom de l'étape suivante — `RouteToNextStageAsync` utilise l'itinéraire de config pour trouver la suivante automatiquement. Le problème est que le consumer **doit hériter de `BaseConsumer<T>`** et appeler des méthodes EMT (`RouteToNextStageAsync`, `CompleteMessageAsync`, `DeadLetterMessageAsync`). Il ne peut pas exister sans l'infrastructure EMT complète.

```csharp
// Design actuel — ValiderConsumer.cs
// ↓ héritage obligatoire — impossible de tester ce consumer sans toute l'infra EMT
public class ValiderConsumer : BaseConsumer<DossierMessage>
{
    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<DossierMessage> context,
        CancellationToken cancellationToken)
    {
        // Logique métier
        await _serviceValidation.ValiderAsync(context.Message.DossierId);

        // RouteToNextStageAsync lit l'Itinerary de config pour trouver l'étape suivante.
        // Le consumer ne hardcode pas "EnrichirDonnees" — mais il APPELLE une méthode EMT.
        // Sans BaseConsumer, sans IMessagingProvider, sans AppSettings : ce code ne compile pas.
        await RouteToNextStageAsync(
            context,
            ctx => new EnrichirMessage { DossierId = ctx.Message.DossierId },
            cancellationToken);
    }
}
```

Conséquences concrètes :
- **En test** : pour tester `ValiderAsync()`, il faut mocker `IMessagingProvider`, `IStorageProvider`, `AppSettings` avec un `Itinerary` valide, `ILogger`, etc. — même si votre logique métier n'a besoin de rien de tout ça.
- **En réutilisation** : ce consumer est lié à UN itinéraire précis. Si vous voulez faire la même validation dans un workflow différent (autre ordre, autre étape suivante), vous devez créer un deuxième consumer.
- **En lecture de code** : pour comprendre ce que fait ce consumer, il faut remonter jusqu'à `BaseConsumer<T>` et comprendre comment `RouteToNextStageAsync` fonctionne — ce n'est pas visible dans le consumer lui-même.

### 4.3 Un seul itinéraire par application

La config `Itinerary` est globale dans `AppSettings` — une seule liste par instance d'application.

Cas concret : une même Azure Function App reçoit des messages pour deux workflows :
- **Traitement standard** : Valider → Enrichir → Notifier (3 étapes)
- **Traitement urgent** : Valider → NotifierImmédiatement (2 étapes, sans enrichissement)

Avec le design actuel, c'est **impossible dans la même app** — il n'y a qu'un seul `Itinerary` dans `appsettings.json`. Il faudrait déployer deux applications séparées avec deux configs distinctes, même si le code est identique à 90 %. Avec le Routing Slip, le slip lui-même porte son itinéraire : la même Function App peut recevoir les deux types de messages et chacun suivra son propre chemin.

### 4.4 Résumé — ce qui change dans le nouveau design

| Problème | Design actuel | Design cible |
|----------|---------------|--------------|
| Où vit l'itinéraire ? | `appsettings.json` (chaque app) | Dans le message lui-même (`SlipEnvelope`) |
| Qui fait le routing ? | Le consumer via `RouteToNextStageAsync()` | Le framework (`RoutingSlipExecutor`) |
| Plusieurs workflows par app | Impossible | Naturel — chaque slip est indépendant |
| Cohérence inter-déploiements | Fragile (configs doivent être identiques) | Garantie (le slip est auto-portant) |
| Testabilité | Nécessite toute l'infra EMT | Activité POCO testable seule |

---

## 5. Design cible — vue d'ensemble

---

## 5.1 Nouveau design centré sur RoutingSlipBuilder — Solution claire et découplée

### Objectif
Le RoutingSlipBuilder devient le **seul responsable** de la construction de l’itinéraire et de la définition des propriétés de routage (Consumer, Action) dans le message. Le producteur (Producer<T>) n’a plus à connaître la logique de routage ni à manipuler les propriétés de message — il se contente de publier le slip construit.

### Schéma d’architecture

flowchart TD
A["Activateur (HTTP/API)"] --> B[SlipEnvelope complet]
B --> C[Service Bus]
C --> D[Worker / Function]
D --> E["IRoutingSlipActivity<TArgs> (votre code métier)"]
E --> D
D --> C


### Pseudocode — Construction et publication d’un Routing Slip

```csharp
// 1. L’activateur construit le slip avec toutes les étapes et arguments
var slip = new RoutingSlipBuilder("TraiterDossier", endpointResolver)
    .AddStep("ValiderAdmissibilite", new ValiderArgs { DossierId = "D-001" })
    .AddStep("EnrichirDonnees", new EnrichirArgs { DossierId = "D-001" })
    .AddStep("NotifierBeneficiaire", new NotifierArgs { DossierId = "D-001" })
    .Build();

// 2. Le slip contient déjà toutes les infos de routage (EntityName, Consumer, Action)
//    résolues via IEndpointResolver à la construction.

// Exemple de structure d'une étape dans slip.Steps :
// slip.Steps[1] = new SlipStep {
//     Name = "EnrichirDonnees",
//     EntityName = "topic-enrichir",
//     EntityType = MessagingEntityType.Topic,
//     Subscription = new SlipTopicSubscription {
//         Consumer = "EnrichirConsumer",
//         Action = "Traiter"
//     },
//     Arguments = ...,
//     Status = SlipStepStatus.Pending
// }

// 3. Publication du slip sur Service Bus
await _producer.PublishAsync(
    new MessageTransitContext<SlipEnvelope> { Message = slip },
    new PublishOptions { Target = slip.Steps[0].EntityName }
);
```

> ℹ️ **Où sont renseignés Consumer et Action ?**
>
> Lors de la construction du slip, le RoutingSlipBuilder utilise l'IEndpointResolver pour chaque étape. Si l'étape cible une Topic, il récupère automatiquement les propriétés Subscription.Consumer et Subscription.Action depuis la configuration centralisée (AppSettings.Endpoints). Ces valeurs sont insérées dans chaque SlipStep du slip : elles voyagent donc dans le message, et sont publiées comme Application Properties lors de l'envoi sur Service Bus par le RoutingSlipExecutor.

### Pseudocode — Worker/Function qui traite une étape

```csharp
public class ValiderAdmissibiliteFunction
{
    private readonly IMessagingProvider _messagingProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public ValiderAdmissibiliteFunction(IMessagingProvider messagingProvider, IServiceScopeFactory scopeFactory)
    {
        _messagingProvider = messagingProvider;
        _scopeFactory = scopeFactory;
    }

    [Function("ValiderAdmissibiliteFunction")]
    public async Task Run(
        [ServiceBusTrigger("queue-valider-admissibilite", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
        _messagingProvider.BindContext(message, actions);
        await executor.ProcessAsync(_messagingProvider, ct);
    }
}
```

### Points clés pédagogiques

- **Découplage total** : Le RoutingSlipBuilder centralise la logique de routage, le producteur ne fait que publier.
- **Clarté** : Toute l’architecture de l’itinéraire est visible dans le slip, pas dans la config locale du worker.
- **Testabilité** : Les activités sont des POCO, testables sans dépendance à l’infrastructure EMT.
- **Évolutivité** : Plusieurs workflows peuvent coexister dans la même application, chaque message porte son propre itinéraire.
- **Robustesse** : Plus de dérive de configuration entre déploiements, le message est auto-portant.

---

## 5.2 Exemple complet — du builder à l’exécution

```csharp
// Activateur : construit et publie le slip
var slip = new RoutingSlipBuilder("TraiterDossier", endpointResolver)
    .AddStep("ValiderAdmissibilite", new ValiderArgs { DossierId = "D-001" })
    .AddStep("EnrichirDonnees", new EnrichirArgs { DossierId = "D-001" })
    .AddStep("NotifierBeneficiaire", new NotifierArgs { DossierId = "D-001" })
    .Build();
await _producer.PublishAsync(
    new MessageTransitContext<SlipEnvelope> { Message = slip },
    new PublishOptions { Target = slip.Steps[0].EntityName }
);

// Worker : reçoit le message, exécute l’étape courante
public class EnrichirDonneesFunction
{
    private readonly IMessagingProvider _messagingProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrichirDonneesFunction(IMessagingProvider messagingProvider, IServiceScopeFactory scopeFactory)
    {
        _messagingProvider = messagingProvider;
        _scopeFactory = scopeFactory;
    }

    [Function("EnrichirDonneesFunction")]
    public async Task Run(
        [ServiceBusTrigger("topic-enrichir", "EnrichirConsumer.Traiter", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
        _messagingProvider.BindContext(message, actions);
        await executor.ExecuteAsync(_messagingProvider, ct);
    }
}
```

---

## 5.3 Résumé — Avantages du pattern RoutingSlipBuilder

- **Lisibilité** : L’itinéraire est construit explicitement, chaque étape est visible dans le code de l’activateur.
- **Maintenance** : Ajouter, retirer ou réordonner une étape ne nécessite pas de modifier la config des workers.
- **Sécurité** : Les propriétés de routage (Consumer, Action) sont garanties cohérentes, car résolues une seule fois à la construction.
- **Interopérabilité** : Le message est auto-descriptif, prêt pour l’audit, le replay, ou l’intégration multi-broker.

---

### L'idée centrale : le slip voyage avec le message

```
AVANT (design actuel)
──────────────────────────────────────────────────────────────────────────────
appsettings.json (App A)                  Message sur Service Bus
────────────────────────                  ───────────────────────────────────
Itinerary:                                {
  1. ValiderAdmissibilite                   CurrentStage: "EnrichirDonnees",
  2. EnrichirDonnees          ← SÉPARÉS     Variables: { DossierId: "D-001" }
  3. NotifierBeneficiaire                 }

appsettings.json (App B) — doit être identique, sinon chaos silencieux
────────────────────────────────────────────────────────────────────────

APRÈS (design cible)
──────────────────────────────────────────────────────────────────────────────
Message sur Service Bus — TOUT est dans le message
──────────────────────────────────────────────────
MessageTransitContext {
  Message: SlipEnvelope {
    Header:  { SlipId: "slip-001", SlipName: "TraiterDossier" },
    Steps: [
      { Name: "ValiderAdmissibilite", EntityName: "queue-valider",  EntityType: "Queue",
                                    Status: "Completed" },
      { Name: "EnrichirDonnees",      EntityName: "topic-enrichir", EntityType: "Topic",
                                    Subscription: { Consumer: "EnrichirConsumer", Action: "Traiter" },
                                    Status: "Active"    },  ← Cursor=1
      { Name: "NotifierBeneficiaire", EntityName: "queue-notifier", EntityType: "Queue",
                                    Status: "Pending"   }
    ],
    Cursor: 1,
    Variables: { DossierId: "D-001", DateValidation: "2026-05-08" }
  }
}
```

**L'application qui reçoit le message n'a pas besoin d'`appsettings.json` avec un itinéraire.** Elle reçoit le slip complet et sait exactement où elle en est sans aucune config locale.

### La séparation des responsabilités

```
┌─────────────────────────────────────────────────────────┐
│  ACTIVATEUR (Azure Function HTTP)                       │
│  "Je démarre le workflow"                               │
│  Responsabilité : construire le slip avec toutes        │
│  les étapes et publier le premier message               │
└────────────────────────────┬────────────────────────────┘
                             │ publie via IMessageProducer<SlipEnvelope>
                             ▼
┌─────────────────────────────────────────────────────────┐
│  ROUTING SLIP EXECUTOR (interne EMT)                    │
│  "Je fais avancer le slip automatiquement"              │
│  Responsabilité : appeler l'activité, avancer le        │
│  curseur, envoyer au prochain step                      │
└────────────────────────────┬────────────────────────────┘
                             │ appelle
                             ▼
┌─────────────────────────────────────────────────────────┐
│  ACTIVITÉ (votre code métier)                           │
│  "Je fais UNE chose et je retourne un résultat"         │
│  Responsabilité : logique métier UNIQUEMENT             │
│  Ne sait PAS qu'elle est dans un routing slip           │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Les pièces du puzzle — chaque composant expliqué

### 6.1 `IRoutingSlipActivity<TArgs>` — votre code métier

C'est l'interface que vous implémentez. Elle ressemble à une simple fonction : elle reçoit des arguments et retourne un résultat.

```csharp
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Une étape du routing slip. Implémentez cette interface pour chaque étape de votre workflow.
    ///
    /// RÈGLE IMPORTANTE : votre implémentation ne doit contenir QUE un appel vers un service
    /// ou une API métier externe — pas de logique de traitement directement ici.
    /// Ne jamais appeler de méthodes EMT (CompleteMessageAsync, RouteToNextStageAsync, etc.) ici.
    /// Le framework s'occupe du routing automatiquement.
    /// </summary>
    /// <typeparam name="TArgs">
    /// Le type de vos arguments. Exemple : si votre étape a besoin d'un DossierId et d'un NomBeneficiaire,
    /// créez une classe ValiderArgs { string DossierId; string NomBeneficiaire; }
    /// </typeparam>
    public interface IRoutingSlipActivity<TArgs> where TArgs : class
    {
        Task<ActivityResult> ExecuteAsync(ActivityContext<TArgs> ctx, CancellationToken ct);
    }
}
```

#### Règle architecturale — l'activité appelle une API, ne fait pas le traitement

Dans l'architecture RAMQ, **l'activité est un orchestrateur de transit, pas un moteur métier**. Toute la logique de traitement doit être déléguée à un service ou une API externe (REST, gRPC, internal service, etc.).

```
❌ MAUVAIS — l'activité fait le traitement elle-même
public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
{
    // Logique métier directement dans l'activité — éviter absolument
    var dossier = await _db.GetDossierAsync(ctx.Arguments.DossierId, ct);
    dossier.Statut = "Validé";
    dossier.DateValidation = DateTime.UtcNow;
    await _db.SaveAsync(dossier, ct);
    return ActivityResult.Next();
}

✅ BON — l'activité délègue à une API
public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
{
    // L'activité est un simple dispatcher : construit la requête, appelle l'API, interprète la réponse
    var response = await _validationApi.ValiderAsync(
        new ValidationRequest { DossierId = ctx.Arguments.DossierId },
        ct);

    if (!response.IsValid)
        return ActivityResult.Fault(new ValidationException(response.ErrorMessage));

    return ActivityResult.Next(vars => vars["DateValidation"] = response.ValidatedAt);
}
```

**Pourquoi ?**
- Les APIs sont indépendantes d'EMT — testables, versionnables, réutilisables hors routing slip.
- L'activité reste un POCO pur : testable sans mock EMT, sans Service Bus.
- Si l'API répond différemment selon le contexte (claim-check, token d'authentification), elle reçoit exactement ce dont elle a besoin — le routing slip ne fait qu'acheminer.

#### Claim-Check — propagation du token vers l'API downstream

Quand le `SlipEnvelope` dépasse 256 Ko, EMT active automatiquement le Claim-Check : le corps du message est stocké dans Azure Blob Storage et seul un **token de référence** voyage dans Service Bus.

**Comportement depuis la v0.8 :** `DeserializeMessageAsync` **ne télécharge plus automatiquement** le blob. Il propage le token tel quel — c'est l'activité (ou son API downstream) qui décide comment le consommer.

```csharp
public async Task<ActivityResult> ExecuteAsync(ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
{
    // Cas 1 — le slip n'est PAS en mode claim-check : les données voyagent normalement
    // ctx.Arguments est rempli normalement — rien de spécial à faire

    // Cas 2 — le slip est en mode claim-check
    // ctx.ClaimCheckToken contient la référence blob (non null si claim-check actif)
    if (ctx.ClaimCheckToken != null)
    {
        // Option A — l'API downstream sait récupérer depuis le blob elle-même
        var response = await _enrichmentApi.EnrichirAsync(
            new EnrichirRequest
            {
                DossierId       = ctx.Arguments.DossierId,
                BlobReference   = ctx.ClaimCheckToken.Reference,  // ← l'API récupère depuis le blob
                BlobContentType = ctx.ClaimCheckToken.ContentType
            },
            ct);
        return ActivityResult.Next();
    }

    // Option B — l'activité télécharge elle-même si l'API ne supporte pas les blobs
    // À n'utiliser que si l'API downstream ne peut pas accéder au blob directement
    using var stream = await _storageProvider.DownloadAsync(ctx.ClaimCheckToken!.Reference, ct);
    var payload = await DeserializePayloadAsync<EnrichirPayload>(stream);
    var result = await _enrichmentApi.EnrichirAsync(new EnrichirRequest { Payload = payload }, ct);
    return ActivityResult.Next();
}
```

> 💡 **Pourquoi ce changement ?** L'auto-download forcé dans le framework créait une dépendance invisible : l'activité recevait des données "magiquement" sans savoir qu'elles provenaient d'un blob. En mode API-first, c'est l'API downstream qui doit décider si elle accède au blob directement (performance) ou via l'activité (cas de fallback). Le token est propagé fidèlement — la décision appartient au domaine métier, pas au framework de messaging.

### 6.2 `ActivityContext<TArgs>` — ce que le framework vous donne

Quand le framework appelle votre activité, il vous donne ce contexte. Vous n'avez pas à le construire vous-même.

```csharp
public sealed class ActivityContext<TArgs> where TArgs : class
{
    /// <summary>
    /// Vos arguments spécifiques à cette étape.
    /// Définis par l'activateur au moment de construire le slip — immuables.
    /// Exemple : ctx.Arguments.DossierId
    /// </summary>
    public required TArgs Arguments { get; init; }

    /// <summary>
    /// Variables partagées entre TOUTES les étapes sous forme de JsonElement.
    /// Utilisez toujours GetVariable&lt;T&gt; pour lire une variable typée —
    /// NE CASTEZ JAMAIS directement ctx.Variables["clé"] en type primitif :
    /// après un round-trip JSON, la valeur est un JsonElement, pas un string ou DateTime.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Variables { get; init; }
        = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Lit une variable partagée de façon typée et sécurisée.
    /// Retourne null si la clé est absente ou si la valeur est null JSON.
    ///
    /// Exemple d'utilisation :
    ///   var date = ctx.GetVariable&lt;DateTime&gt;("DateValidation");
    ///   var nom  = ctx.GetVariable&lt;string&gt;("NomBeneficiaire");
    /// </summary>
    public T? GetVariable<T>(string key)
    {
        if (!Variables.TryGetValue(key, out var element)) return default;
        return element.Deserialize<T>(_jsonOptions);
    }

    /// <summary>
    /// Identifiant unique du slip — le même du début à la fin.
    /// Utilisez-le pour les logs afin de pouvoir retracer tout le workflow.
    /// </summary>
    public required string SlipId { get; init; }

    /// <summary>Identifiant de corrélation EMT — propagé automatiquement.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Nombre de tentatives de livraison pour l'étape courante (1-basé).
    /// Hydraté depuis DeliveryCount du broker — jamais dans l'enveloppe sérialisée.
    /// Utile pour un log de diagnostic : si Attempt > 1, c'est un rejeu.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Nom de cette étape (défini par l'activateur).
    /// Utile pour les logs : _logger.LogInformation("Étape {Step}", ctx.StepName)
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>Index 0-basé. StepIndex=0 = première étape, StepIndex=2 = troisième étape.</summary>
    public int StepIndex { get; init; }

    /// <summary>Nombre total d'étapes. Permet de savoir si on est presque à la fin.</summary>
    public int TotalSteps { get; init; }
}
```

> 💡 **Pour un junior — pourquoi `GetVariable<T>` et pas un cast direct ?**
> Les `Variables` voyagent sérialisées en JSON dans le `SlipEnvelope`. Quand le message revient du bus, chaque valeur est un `JsonElement` — pas un `DateTime`, pas un `string`. Un cast direct (`(DateTime)ctx.Variables["DateValidation"]`) lève une `InvalidCastException` en runtime. `GetVariable<T>` désérialise proprement via `System.Text.Json` avec les mêmes options que le reste d'EMT.

### 6.3 `ActivityResult` — ce que vous retournez

Trois choix possibles pour indiquer au framework quoi faire après votre étape :

```csharp
public abstract class ActivityResult
{
    /// <summary>
    /// "J'ai terminé, passe au suivant."
    /// Le paramètre optionnel permet d'enrichir les variables partagées
    /// que les étapes suivantes pourront lire.
    ///
    /// Exemple d'utilisation :
    ///   return ActivityResult.Next(vars => vars["DateValidation"] = DateTime.UtcNow);
    ///
    /// Les étapes suivantes peuvent alors lire :
    ///   var date = ctx.Variables["DateValidation"];
    /// </summary>
    public static ActivityResult Next(Action<IDictionary<string, object>>? enrichVariables = null);

    /// <summary>
    /// "J'ai terminé ET c'est la fin du workflow."
    /// À utiliser si votre activité décide elle-même de terminer le slip
    /// avant la dernière étape (ex : dossier déjà traité, rien à faire).
    /// Dans la plupart des cas, le framework détecte la fin automatiquement.
    /// </summary>
    public static ActivityResult Complete();

    /// <summary>
    /// "Une erreur permanente est survenue, envoie ce message en DLQ."
    /// Les étapes suivantes NE SERONT PAS exécutées.
    /// Si des étapes précédentes ont des compensateurs enregistrés, ils sont déclenchés
    /// en ordre inverse avant le dead-lettering.
    /// </summary>
    public static ActivityResult Fault(Exception exception);

    /// <summary>
    /// "Erreur transitoire — réessaie immédiatement (ImmediateRetry EMT)."
    /// Le message reste sur la queue. Service Bus le redelivre sans délai.
    /// À utiliser pour des erreurs de courte durée : lock optimiste perdu,
    /// contention DB transitoire, etc.
    /// Correspond à <see cref="ImmediateRetryException"/> dans EMT.
    /// </summary>
    public static ActivityResult RetryImmediate(string reason);

    /// <summary>
    /// "Erreur transitoire — réessaie avec backoff exponentiel (ExponentialRetry EMT)."
    /// Le message est abandonné (Abandon) et Service Bus applique le délai configuré
    /// dans <see cref="ExponentialRetryPolicy"/> avant de le relivrer.
    /// À utiliser pour des erreurs qui prennent du temps à se résoudre :
    /// service tiers indisponible, timeout HTTP, quota dépassé.
    /// Correspond à <see cref="ExponentialRetryException"/> dans EMT.
    /// </summary>
    public static ActivityResult RetryExponential(string reason, Exception? innerException = null);
}
```

### 6.4 `RoutingSlipBuilder` — côté activateur

C'est ce que l'activateur (Azure Function HTTP, par exemple) utilise pour créer et démarrer un slip.

> 💡 **Principe fondamental — aucun nom d'entité Service Bus dans le code.**
> Le `RoutingSlipBuilder` utilise uniquement des **noms logiques** (`stepName`) — les mêmes que ceux déclarés comme `Target` dans `AppSettings.Endpoints` de l'activateur. **`stepName` est exactement la valeur du champ `Target` dans la config.** L'`EntityName` Service Bus (queue ou topic physique) est résolu par l'`IEndpointResolver` **depuis la configuration centralisée d'EMT**, exactement comme le fait `BaseConsumer.RouteToNextStageAsync()`. Cela évite tout couplage entre le code applicatif et les noms d'infrastructure.

```csharp
public sealed class RoutingSlipBuilder
{
    /// <param name="slipName">
    /// Nom lisible du workflow. Apparaît dans les logs, métriques et traces.
    /// Exemple : "TraiterDossierBeneficiaire"
    /// </param>
    public RoutingSlipBuilder(string slipName, IEndpointResolver endpointResolver);

    /// <summary>
    /// Ajoute une étape au slip en utilisant son nom logique (Target).
    /// L'EntityName Service Bus est résolu depuis AppSettings.Endpoints via IEndpointResolver,
    /// exactement comme BaseConsumer.RouteToNextStageAsync() le fait actuellement.
    /// Les étapes seront exécutées DANS L'ORDRE où vous les ajoutez.
    /// </summary>
    /// <param name="stepName">
    /// Nom logique de l'étape. DOIT correspondre à la valeur du champ Target
    /// dans AppSettings.Endpoints de l'activateur.
    /// stepName == Target : ce sont exactement la même chose.
    /// Exemple : "ValiderAdmissibilite" correspond à Endpoints[i].Target == "ValiderAdmissibilite".
    /// </param>
    /// <param name="arguments">Arguments que cette étape recevra. Sera sérialisé en JSON.</param>
    public RoutingSlipBuilder AddStep<TArgs>(string stepName, TArgs arguments) where TArgs : class;

    /// <summary>Construit le SlipEnvelope prêt à être publié.</summary>
    /// <exception cref="TransitItineraryException">
    /// Si un stepName ne correspond à aucun Target dans AppSettings.Endpoints.
    /// </exception>
    public SlipEnvelope Build();
}
```

> 💡 **Pour un junior — pourquoi seulement `stepName` et non `queue` ou `topic` ?** L'`IEndpointResolver` (déjà existant dans EMT) sait résoudre un `Target` logique vers un `EndpointSettings` complet (`EntityName`, `EntityType`, `Subscription`). C'est exactement ce que `BaseConsumer.RouteToNextStageAsync()` fait à chaque hop. Le `RoutingSlipBuilder` délègue ce travail au même mécanisme — il n'y a pas deux systèmes de résolution, il y en a un seul.

> 💡 **Chaque activité a ses propres arguments — `TArgs` est indépendant par étape.**
> Les activités d'un même slip n'ont pas à partager le même type de message. `ValiderDemandeActivity` reçoit `ValiderArgs`, `ReserverMontantActivity` reçoit `ReserverArgs`, etc. Les arguments de chaque étape sont définis au moment du `AddStep<TArgs>()` et voyagent sérialisés dans le `SlipEnvelope`. Les données partagées entre activités (résultats, identifiants) transitent via `Variables` et sont lues avec `ctx.GetVariable<T>()`.

### 6.5 `SlipEnvelope` — le message qui circule sur Service Bus

C'est le type de message transporté à travers toutes les queues. Vous ne le manipulez jamais directement dans vos activités — le framework s'occupe.

> ⚠️ **Résolution à la construction, portée dans le slip.** Quand `RoutingSlipBuilder.Build()` est appelé, l'`IEndpointResolver` résout chaque `Target` (= `stepName`) en `EndpointSettings` complet depuis `AppSettings.Endpoints`. L'`EntityName` résolu est ensuite **écrit dans le `SlipEnvelope`** qui voyage avec le message. Chaque `SlipStep` dans le slip est donc entièrement autonome — il contient `EntityName`, `EntityType` et `Subscription` résolus. Les workers en aval lisent directement cette information du slip reçu, sans consulter de config locale.

```csharp
/// <summary>
/// Le "bon de livraison" qui voyage de queue en queue.
/// Contient l'itinéraire complet, le curseur actuel, et les variables partagées.
/// </summary>
public sealed class SlipEnvelope
{
    /// <summary>Métadonnées du slip : identifiant, nom, date de création.</summary>
    public SlipHeader Header { get; init; }

    /// <summary>
    /// Toutes les étapes, dans l'ordre.
    /// Status de chaque étape : Pending → Active → Completed (ou Faulted)
    ///
    /// Chaque <see cref="SlipStep"/> contient :
    ///   Name         — nom logique de l'étape = Target dans AppSettings.Endpoints = stepName donné à AddStep()
    ///   EntityName   — nom physique résolu depuis Endpoints[i].Endpoint.EntityName
    ///   EntityType   — "Queue" ou "Topic" (depuis Endpoints[i].Endpoint.EntityType)
    ///   Subscription — (Topic) { Consumer, Action? } depuis Endpoints[i].Endpoint.Subscription
    ///   Arguments    — paramètres JSON sérialisés, immuables depuis l'activateur
    ///   Status       — Pending | Active | Completed | Faulted
    /// </summary>
    public SlipStep[] Steps { get; init; }

    /// <summary>
    /// Index (0-basé) de l'étape en cours.
    /// Steps[Cursor] = étape courante.
    /// Steps[Cursor + 1] = étape suivante.
    /// </summary>
    public int Cursor { get; init; }

    /// <summary>
    /// Variables partagées entre toutes les étapes.
    /// Fusionnées à chaque étape qui appelle Next(vars => ...)
    /// </summary>
    public Dictionary<string, JsonElement> Variables { get; init; }
}
```

#### `SlipStep` — structure d'une étape (résolue depuis `AppSettings.Endpoints`)

```csharp
public sealed record SlipStep
{
    /// <summary>
    /// Nom logique de l'étape = valeur du champ Target dans AppSettings.Endpoints.
    /// Identique au paramètre stepName passé à RoutingSlipBuilder.AddStep().
    /// Exemple : "ValiderAdmissibilite"
    /// C'est le nom qui apparaît dans les logs, métriques et traces.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Nom physique de l'entité Service Bus — résolu depuis Endpoints[i].Endpoint.EntityName.
    /// Exemple : "queue-valider-admissibilite" ou "topic-enrichir"
    /// Jamais renseigné à la main dans le code — toujours via IEndpointResolver.
    /// </summary>
    public required string EntityName { get; init; }

    /// <summary>
    /// Type d'entité Service Bus — résolu depuis Endpoints[i].Endpoint.EntityType.
    /// </summary>
    public MessagingEntityType EntityType { get; init; }

    /// <summary>
    /// Abonnement cible — résolu depuis Endpoints[i].Endpoint.Subscription.
    /// Non null uniquement si EntityType == Topic.
    ///
    /// Consumer : nom du consumer abonné (ex: "EnrichirConsumer").
    /// Action   : action optionnelle (ex: "Traiter"). Null = action par défaut.
    ///
    /// Ces deux valeurs sont publiées comme Application Properties Service Bus
    /// pour le filtrage par les règles SQL des abonnements (gérées par la pipeline DevOps).
    /// </summary>
    public SlipTopicSubscription? Subscription { get; init; }

    /// <summary>
    /// Arguments sérialisés en JSON pour cette étape.
    /// Définis une fois pour toutes par l'activateur — immuables pendant le voyage du slip.
    /// Chaque étape a son propre type TArgs — elles n'ont pas à partager le même type de message.
    /// </summary>
    public JsonElement Arguments { get; init; }

    /// <summary>Pending → Active → Completed (ou Faulted).</summary>
    public SlipStepStatus Status { get; init; }
}

/// <summary>
/// Abonnement Service Bus pour une étape Topic — type propre au routing slip,
/// distinct de SubscriptionInfoSettings (config infrastructure).
/// </summary>
public sealed record SlipTopicSubscription
{
    /// <summary>
    /// Nom du consumer abonné au topic. Publié comme Application Property "Consumer" sur le message.
    /// La pipeline DevOps a configuré une règle SQL sur l'abonnement qui filtre sur cette valeur.
    /// Exemple : "EnrichirConsumer"
    /// </summary>
    public required string Consumer { get; init; }

    /// <summary>
    /// Action optionnelle. Null = action par défaut (abonnement sans suffixe d'action).
    /// Publié comme Application Property "Action" sur le message.
    /// La pipeline DevOps a configuré la règle SQL correspondante.
    /// Exemple : "Traiter"
    ///
    /// Abonnement Service Bus résultant (nom géré par DevOps pipeline) :
    ///   Action = null      → "{Consumer}"           (ex: "EnrichirConsumer")
    ///   Action = "Traiter" → "{Consumer}.{Action}"  (ex: "EnrichirConsumer.Traiter")
    /// </summary>
    public string? Action { get; init; }
}
```

### 6.6 `RoutingSlipExecutor` — le chef d'orchestre (interne EMT)

Vous n'interagissez jamais avec cette classe directement. C'est le framework qui l'utilise. Elle fait le travail de routing automatiquement après chaque étape.

Ce qu'elle fait concrètement :
1. Désérialise le `SlipEnvelope` reçu depuis Service Bus
2. Résout votre activité depuis la DI (`IRoutingSlipActivity<TArgs>`)
3. **Désérialise les `Arguments` depuis `JsonElement` vers votre type fort** (`TArgs`)
4. Construit l'`ActivityContext<TArgs>`
5. Appelle `ExecuteAsync()`
6. Selon le résultat :
   - `Next()` → incrémente le curseur, sérialise le nouveau slip, publie via `IMessageProducer<SlipEnvelope>`
   - `Complete()` → appelle `CompleteMessageAsync()` (EMT existant)
   - `Fault(ex)` → appelle `DeadLetterMessageAsync(ex)` (EMT existant)
7. Écrit dans le journal EMT (`IJournalProvider`)
8. Émet un span OpenTelemetry (`ActivitySource`) : `messaging.routing_slip.step`
9. Incrémente les métriques (`IMetricsProvider`) : `routing_slip_step_duration_ms`

#### Désérialisation des Arguments — du JsonElement vers TArgs

Les `Arguments` de chaque étape voyagent dans le `SlipEnvelope` sérialisé en JSON (type `JsonElement`). Quand l'executor appelle votre activité, il doit les désérialiser dans le bon type fort :

```csharp
// Dans RoutingSlipExecutor.ExecuteStepAsync<TArgs>() (code interne)
var argsJson = envelope.Steps[cursor].Arguments; // JsonElement
var args     = argsJson.Deserialize<TArgs>(_jsonOptions); // ← System.Text.Json
                                                           // même IJsonMessageSerializer qu'EMT

var ctx = new ActivityContext<TArgs>
{
    Arguments  = args!,
    Variables  = BuildVariablesDictionary(envelope.Variables), // JsonElement → object
    SlipId     = envelope.Header.SlipId,
    CorrelationId = envelope.Header.CorrelationId,
    StepName   = envelope.Steps[cursor].Name,
    StepIndex  = cursor,
    TotalSteps = envelope.Steps.Length
};

var result = await activity.ExecuteAsync(ctx, ct);
```

**Point important** : les options de sérialisation JSON utilisées sont les mêmes que celles d'EMT (`camelCase`, `JsonStringEnumConverter`, etc.). Il n'y a pas de config JSON séparée à gérer.

#### Enregistrement DI — comment l'executor trouve votre activité

> 💡 **Cohérence avec le pattern EMT existant.** `AddRoutingSlipActivity` fonctionne comme `services.AddProducer<TMessage>("target")` : vous passez le **nom logique** (`Target`), le framework résout l'`EntityName` depuis `AppSettings.Endpoints` via `IEndpointResolver`. Pas de nom physique d'entité dans le code C#.

```csharp
// --- Étape Queue — utiliser le Target logique, pas l'EntityName physique ---
// EMT résout "ValiderAdmissibilite" → AppSettings.Endpoints.Find(Target=="ValiderAdmissibilite")
//                                   → Endpoint.EntityName  = "queue-valider-admissibilite"
//                                   → Endpoint.EntityType  = Queue
services.AddRoutingSlipActivity<ValiderAdmissibiliteActivity, ValiderArgs>(
    target: "ValiderAdmissibilite");

// --- Étape Topic — même logique, Subscription résolu depuis Endpoints[i].Endpoint.Subscription ---
// "EnrichirDonnees" → Endpoint.EntityName  = "topic-enrichir"
//                   → Endpoint.EntityType  = Topic
//                   → Endpoint.Subscription.Consumer = "EnrichirConsumer"
//                   → Endpoint.Subscription.Action   = "Traiter"
// Lors de Next(), RoutingSlipExecutor publie Consumer + Action comme Application Properties SB.
services.AddRoutingSlipActivity<EnrichirDonneesActivity, EnrichirArgs>(
    target: "EnrichirDonnees");

// Ce que ça fait en interne :
// 1. Résout EndpointSettings depuis IEndpointResolver.TryResolve("EnrichirDonnees", ...)
// 2. Enregistre l'activité comme Scoped
// 3. Enregistre RoutingSlipExecutor<TArgs> avec EntityType/EntityName/Subscription résolus
//    services.AddScoped<IRoutingSlipExecutor>(sp =>
//        new RoutingSlipExecutor<TArgs>(
//            activity:     sp.GetRequiredService<IRoutingSlipActivity<TArgs>>(),
//            producer:     sp.GetRequiredService<IMessageProducer<SlipEnvelope>>(),
//            journal:      sp.GetRequiredService<IJournalProvider>(),
//            metrics:      sp.GetRequiredService<IMetricsProvider>(),
//            endpoint:     endpointResolver.Resolve("EnrichirDonnees")
//            //            ↑ EntityName + EntityType + Subscription déjà résolus
//        ));
```

#### Résolution de la cible au moment de l'avance — Queue vs Topic

Quand l'executor fait avancer le slip vers l'étape suivante, il calcule la cible Service Bus selon `EntityType` :

```csharp
// Dans RoutingSlipExecutor.ResolveTarget(SlipStep nextStep) (code interne)
private static string ResolveTarget(SlipStep step) => step.EntityType switch
{
    MessagingEntityType.Queue =>
        // Cible directe : nom de la queue
        step.EntityName,                                           // ex: "queue-notifier"

    MessagingEntityType.Topic =>
        // Cible dérivée de l'abonnement : "Consumer" ou "Consumer.Action"
        // Réutilise la même logique que BaseConsumer.GetTopicStage()
        string.IsNullOrEmpty(step.Subscription?.Action)
            ? step.Subscription!.Consumer                         // ex: "EnrichirConsumer"
            : $"{step.Subscription!.Consumer}.{step.Subscription.Action}", // ex: "EnrichirConsumer.Traiter"

    _ => throw new InvalidOperationException($"EntityType inconnu : {step.EntityType}")
};
```

**Pourquoi Scoped (et non Singleton) ?** Parce que vos activités peuvent injecter des services Scoped (ex : `DbContext`, services métier par requête). Le framework crée un scope par message reçu, exactement comme le fait `BaseConsumer<T>` existant.

---

### 6.7 `MessageEnvelope` — l'enveloppe unifiée enterprise

#### Le problème : deux formats = deux silos

Sans révision de `MessageTransitContext<T>`, la v2.0 créerait **deux formats sérialisés distincts** coexistant sur Service Bus :

| Cas d'usage | Format sérialisé sur le wire | Problème |
|---|---|---|
| Consumer simple (`BaseConsumer<T>`, non-saga) | `MessageTransitContext<T>` JSON — mélange champs sérialisés + `[JsonIgnore]` | Contrat implicite, non-versionné |
| Routing slip (`IRoutingSlipActivity<TArgs>`) | `SlipEnvelope` JSON — vocabulaire entièrement différent | Outillage d'audit distinct, règles de replay distinctes |

Deux enveloppes = deux silos. L'outillage de monitoring doit connaître deux structures. Les outils de replay doivent supporter deux grammaires. Les tests de contrat sont doublés. Ce n'est pas une solution enterprise — c'est de la fragmentation.

> 💡 **Pour un junior — qu'est-ce qu'un silo ici ?** Un silo apparaît quand deux systèmes utilisent des formats incompatibles pour représenter la même réalité. Ici : un message EMT standard et un message routing slip sont tous les deux des « messages qui voyagent sur Service Bus », mais ils ont des structures JSON totalement différentes. Un outil d'audit qui veut lire les deux doit avoir deux parseurs, deux logiques, deux stratégies de replay. C'est exactement ce qu'une architecture enterprise cherche à éviter.

#### La solution : `MessageEnvelope` RAMQ interne

Un **seul record sérialisé**, propre à RAMQ, discriminé par `MessageKind`. Pas de dépendance externe. Compatible CloudEvents en Phase 6 si ce besoin émerge (voir note en fin de section).

```csharp
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// Enveloppe unifiée — UNIQUE format sérialisé sur le wire pour tous les messages EMT.
    ///
    /// Remplace la sérialisation directe de MessageTransitContext&lt;T&gt; (v1.x).
    /// MessageTransitContext&lt;T&gt; devient un objet runtime pur, jamais sérialisé.
    ///
    /// Versioning : toute modification de ce record est un breaking change MAJOR.
    /// Voir ADR-003 (mis à jour en v2.0).
    /// </summary>
    public sealed record MessageEnvelope
    {
        // ── Versioning explicite ─────────────────────────────────────────────
        /// <summary>
        /// Version du schéma de l'enveloppe. Permet aux consommateurs de refuser
        /// les versions non supportées avant même de tenter la désérialisation.
        /// Valeur courante : "2.0". Ne jamais changer sans bump MAJOR.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public required string SchemaVersion { get; init; }  // = "2.0"

        // ── Discriminant — détermine comment "data" est interprété ──────────
        /// <summary>
        /// Détermine le type de contenu de <see cref="Data"/> et le handler à utiliser.
        ///   Message    → désérialisé en TMessage → BaseConsumer&lt;TMessage&gt;
        ///   RoutingSlip → désérialisé en SlipEnvelope → RoutingSlipExecutor&lt;TArgs&gt;
        /// </summary>
        [JsonPropertyName("kind")]
        public required MessageKind Kind { get; init; }

        // ── Identité et corrélation ──────────────────────────────────────────
        /// <summary>
        /// Identifiant unique du message. Correspond à l'ancien MessageTransitContext.MessageId.
        /// Utilisé pour la déduplication Service Bus (DuplicateDetection).
        /// </summary>
        [JsonPropertyName("messageId")]
        public required string MessageId { get; init; }

        /// <summary>
        /// Identifiant de corrélation immuable — ne change jamais, même lors des retries.
        /// Permet de retracer le parcours complet d'un message de bout en bout.
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Identifiant de session Service Bus. Null si l'endpoint n'utilise pas les sessions.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; init; }

        // ── Type métier (messages simples uniquement) ────────────────────────
        /// <summary>
        /// Nom du type de message métier. Exemple : "DemandeValidationAdresse".
        /// Null pour Kind = RoutingSlip (le type est dans SlipEnvelope.SlipName).
        /// Correspond à l'ancien MessageTransitContext.MessageType.
        /// </summary>
        [JsonPropertyName("messageType")]
        public string? MessageType { get; init; }

        // ── Tokens Claim-Check (optionnel) ───────────────────────────────────
        /// <summary>
        /// Tokens de claim-check si le payload dépasse 256 Ko.
        /// Null si aucun claim-check appliqué.
        /// Identique à l'ancien MessageTransitContext.Tokens.
        /// </summary>
        [JsonPropertyName("tokens")]
        public List<TokenMessage>? Tokens { get; init; }

        // ── Variables partagées (messages simples) ───────────────────────────
        /// <summary>
        /// Variables métier accumulées au fil du traitement.
        /// Pour Kind = RoutingSlip, les variables sont dans SlipEnvelope.Variables.
        /// Pour Kind = Message, correspond à l'ancien MessageTransitContext.Variables.
        /// </summary>
        [JsonPropertyName("variables")]
        public Dictionary<string, JsonElement>? Variables { get; init; }

        // ── Traçabilité distribuée ───────────────────────────────────────────
        /// <summary>
        /// W3C Trace Context traceparent. Propagé automatiquement à chaque hop.
        /// Format : "00-{traceId}-{parentId}-{flags}"
        /// </summary>
        [JsonPropertyName("traceparent")]
        public string? Traceparent { get; init; }

        // ── Payload ─────────────────────────────────────────────────────────
        /// <summary>
        /// Contenu du message, sérialisé en JSON.
        ///   Kind = Message     → TMessage sérialisé
        ///   Kind = RoutingSlip → SlipEnvelope sérialisé
        /// </summary>
        [JsonPropertyName("data")]
        public required JsonElement Data { get; init; }
    }

    /// <summary>Discriminant du contenu de MessageEnvelope.Data.</summary>
    public enum MessageKind
    {
        /// <summary>Message métier standard — traité par BaseConsumer&lt;TMessage&gt;.</summary>
        Message,

        /// <summary>Routing slip — traité par RoutingSlipExecutor&lt;TArgs&gt;.</summary>
        RoutingSlip
    }
}
```

#### Format JSON sur le wire — les deux cas

```json
// ─── Kind = Message (BaseConsumer<T>, non-saga) ───────────────────────────
{
  "schemaVersion": "2.0",
  "kind": "Message",
  "messageId": "msg-abc123",
  "correlationId": "corr-001",
  "sessionId": null,
  "messageType": "DemandeValidationAdresse",
  "tokens": null,
  "variables": { "DossierId": "D-001" },
  "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "data": {
    "assureId": "12345678",
    "nouvelleAdresse": { "ligne1": "123 rue Principale", "codePostal": "G1R 4S4" }
  }
}

// ─── Kind = RoutingSlip (IRoutingSlipActivity<TArgs>) ─────────────────────
// data = SlipEnvelope complet (itinéraire + curseur + variables + args par étape)
{
  "schemaVersion": "2.0",
  "kind": "RoutingSlip",
  "messageId": "slip-def456",
  "correlationId": "corr-002",
  "sessionId": null,
  "messageType": null,
  "tokens": null,
  "variables": null,
  "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b8-01",
  "data": {
    "slipId": "slip-def456",
    "slipName": "TraiterRemboursement",
    "cursor": 1,
    "steps": [
      { "name": "Valider",  "queue": "queue-valider",  "status": "Completed", "args": { "demandeId": "D-001", "montant": 250.00 } },
      { "name": "Reserver", "queue": "queue-reserver", "status": "Active",    "args": { "demandeId": "D-001", "compte": "CAD-001" } },
      { "name": "Emettre",  "queue": "queue-emettre",  "status": "Pending",   "args": { "demandeId": "D-001" } }
    ],
    "variables": { "DateValidation": "2026-05-11T10:30:00Z", "NomBeneficiaire": "Jean Tremblay" }
  }
}
```

Le framework lit `kind` pour dispatcher — **un seul point de décision** :

```csharp
// Dans AzureMessagingProvider.DispatchAsync() (code interne)
var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body, _jsonOptions);

// Vérification de version avant tout
if (envelope.SchemaVersion != "2.0")
    return DeserializationResult.Unsupported($"SchemaVersion '{envelope.SchemaVersion}' non supportée");

return envelope.Kind switch
{
    MessageKind.RoutingSlip => await _slipDispatcher.DispatchAsync(envelope, ct),
    MessageKind.Message     => await _messageDispatcher.DispatchAsync(envelope, ct),
    _                       => DeserializationResult.Malformed($"Kind inconnu : {envelope.Kind}")
};
```

#### `MessageTransitContext<T>` devient un objet runtime pur

Avec `MessageEnvelope` comme unique contrat sérialisé, `MessageTransitContext<T>` n'est **plus jamais sérialisé**. C'est un objet de confort construit par le framework à la réception :

```csharp
// ─── AVANT (v1.x) : mélange sérialisé + runtime ─────────────────────────
public class MessageTransitContext<TMessage>
{
    public TMessage? Message { get; set; }                      // ← sérialisé
    public string? MessageId { get; set; }                      // ← sérialisé
    public string? CurrentStage { get; set; }                   // ← sérialisé (saga)
    public Dictionary<string, object>? Variables { get; set; } // ← sérialisé

    [JsonIgnore] public IMessageTransit? TransportMessage { get; set; } // ← runtime
    [JsonIgnore] public string? SerializedPayload { get; set; }         // ← runtime
    [JsonIgnore] public bool IsClaimCheckApplied { get; set; }          // ← runtime
    // ↑ Les [JsonIgnore] sont le signal d'alarme : deux responsabilités dans une classe
}

// ─── APRÈS (v2.0) : runtime uniquement, jamais sérialisé ─────────────────
public sealed class MessageTransitContext<TMessage> where TMessage : class
{
    // Hydraté depuis MessageEnvelope.Data désérialisé en TMessage
    public required TMessage Message { get; init; }

    // Hydraté depuis MessageEnvelope.MessageId
    public required string MessageId { get; init; }

    // Hydraté depuis MessageEnvelope.CorrelationId
    public string? CorrelationId { get; init; }

    // Hydraté depuis MessageEnvelope.SessionId
    public string? SessionId { get; init; }

    // Hydraté depuis DeliveryCount du broker (Service Bus) — jamais dans l'enveloppe
    public int Attempt { get; init; }

    // Runtime depuis IMessageTransit.SequenceNumber — jamais dans l'enveloppe
    public long SequenceNumber { get; init; }

    // Hydraté depuis MessageEnvelope.Variables (Kind = Message)
    public IReadOnlyDictionary<string, object> Variables { get; init; }
        = new Dictionary<string, object>();

    // Runtime — accès au message transport natif si besoin exceptionnel
    public required IMessageTransit TransportMessage { get; init; }

    // ✅ Aucun [JsonIgnore] — cette classe n'est PLUS JAMAIS sérialisée
    // ✅ Immuable après construction — init-only properties
    // ✅ ADR-003 s'applique désormais à MessageEnvelope, pas à MessageTransitContext<T>

    // ─── Helpers inchangés — compatibilité totale avec le code applicatif ──
    public TData? GetVariable<TData>(string key) { ... }
    public TData? GetApplicationPropertyValue<TData>(string key, TData? defaultValue = default) { ... }
    public MessageTransitContext<TResponse> CopyWithResponse<TResponse>(TResponse response) { ... }
}
```

#### Mapping complet — champ par champ de v1.x vers v2.0

| Champ `MessageTransitContext<T>` v1.x | Emplacement dans `MessageEnvelope` v2.0 | Remarque |
|---|---|---|
| `MessageId` | `messageId` | Inchangé, renommé camelCase |
| `MessageType` | `messageType` | Inchangé |
| `CorrelationId` | `correlationId` | Inchangé |
| `SessionId` | `sessionId` | Inchangé |
| `Tokens` (claim-check) | `tokens` | Inchangé |
| `Variables` | `variables` (comme `Dictionary<string, JsonElement>`) | Conservé — type JSON plus précis |
| `CurrentStage` (saga) | Absent — remplacé par `SlipEnvelope.Cursor` dans `data` | **Supprimé du contrat** |
| `Message` (payload) | `data` (JsonElement) | Désérialisé en `TMessage` par le framework |
| `Attempt` | **Runtime** — depuis `DeliveryCount` broker, jamais sérialisé | Simplifié |
| `SequenceNumber` | **Runtime** — depuis `IMessageTransit`, jamais sérialisé | Simplifié |
| `TransportMessage` | **Runtime** — non sérialisé (était déjà `[JsonIgnore]`) | `[JsonIgnore]` supprimé |
| `IsClaimCheckApplied` | **Runtime** — non sérialisé (était déjà `[JsonIgnore]`) | `[JsonIgnore]` supprimé |
| `SerializedPayload` | **Runtime** — non sérialisé (était déjà `[JsonIgnore]`) | `[JsonIgnore]` supprimé |

#### Tests de contrat — un seul snapshot à maintenir

```csharp
// MessageEnvelopeContractTests.cs
// Ce test est BLOQUANT en CI — toute modification de MessageEnvelope le casse.
// Voir ADR-003 (mis à jour v2.0) pour la procédure de modification.

[Fact]
public void MessageEnvelope_Message_SerializationStable()
{
    var envelope = new MessageEnvelope
    {
        SchemaVersion = "2.0",
        Kind          = MessageKind.Message,
        MessageId     = "msg-test-001",
        CorrelationId = "corr-test-001",
        MessageType   = "DemandeValidationAdresse",
        Data          = JsonSerializer.SerializeToElement(new { assureId = "12345678" })
    };

    var json = JsonSerializer.Serialize(envelope, _jsonOptions);

    // Snapshot figé — ne doit JAMAIS changer sans bump MAJOR + mise à jour de ce snapshot
    json.Should().MatchSnapshot("MessageEnvelope_Message_v2.0.json");
}

[Fact]
public void MessageEnvelope_RoutingSlip_SerializationStable()
{
    // Idem pour Kind = RoutingSlip
    // Un seul test de contrat couvre TOUS les types de messages EMT
}
```

**Avant (v1.x) :** deux classes à snapshot-tester séparément — `MessageTransitContext<T>` et (futur) `SlipEnvelope`.
**Après (v2.0) :** **un seul snapshot** pour `MessageEnvelope`, quel que soit le `Kind`.

#### Ce que ça change pour un développeur applicatif

**Quasiment rien.** Les méthodes `GetVariable<T>`, `GetApplicationPropertyValue<T>`, `CopyWithResponse<T>` restent identiques. La signature de `BaseConsumer<T>.ConsumeAsync(MessageTransitContext<T>, CancellationToken)` reste identique. Le code qui compile aujourd'hui continuera de compiler en v2.0.

Ce qui change est **sous le capot** du framework :

| Avant (v1.x) | Après (v2.0) |
|---|---|
| `AzureMessagingProvider` sérialise `MessageTransitContext<T>` | Sérialise `MessageEnvelope` |
| `AzureMessagingProvider` désérialise `MessageTransitContext<T>` | Désérialise `MessageEnvelope`, construit `MessageTransitContext<T>` |
| Contrat sérialisé : `MessageTransitContext<T>` (classe mutable) | Contrat sérialisé : `MessageEnvelope` (record immuable) |
| Versioning : aucun champ `schemaVersion` | Versioning : `schemaVersion: "2.0"` sur chaque message |
| Tests de contrat : portent sur `MessageTransitContext<T>` | Tests de contrat : portent sur `MessageEnvelope` uniquement |
| Outillage audit/replay : doit gérer deux structures JSON | Outillage audit/replay : **une seule structure JSON** |

> 💡 **Pour un junior — le principe « contrat vs runtime »**. Un contrat est ce qui traverse une frontière (le réseau, le temps). Un objet runtime est ce qui existe en mémoire le temps d'un traitement. Les mélanger dans la même classe est une erreur classique — le symptôme est les `[JsonIgnore]`. La règle saine : **tout ce qui voyage est un record immuable versionné, tout ce qui vit en mémoire est un objet sans attributs de sérialisation**.

#### Note — CloudEvents pour la Phase 6

`MessageEnvelope` v2.0 est conçue pour être **compatible CloudEvents 1.0 sans dépendance externe**. La migration Phase 6 (multi-broker) se limite à :

| Champ `MessageEnvelope` v2.0 | Attribut CloudEvents 1.0 équivalent |
|---|---|
| `schemaVersion: "2.0"` | `specversion: "1.0"` (renommage) |
| `messageId` | `id` (renommage) |
| `messageType` | `type` (renommage + convention URN) |
| `correlationId` | extension `ramqcorrelationid` |
| `sessionId` | extension `ramqsessionid` |
| `traceparent` | extension standard W3C (déjà identique) |
| `data` | `data` (identique) |

Quand Kafka ou un broker externe entre en jeu (Phase 6), la transition est mécanique — un seul adaptateur de sérialisation à écrire, pas de refactoring du modèle.

---

## 7. Workers, RoutingSlipExecutor et intégration Service Bus

Cette section décrit les **types de workers** supportés, la relation entre le worker, le `RoutingSlipExecutor` et vos activités, et le rôle de `Consumer`/`Action` dans le routage des étapes Topic.

### 7.1 Relation Worker → RoutingSlipExecutor → Activity

Un **worker** est tout hôte .NET capable de recevoir un message Service Bus et de déléguer son traitement au `RoutingSlipExecutor`. Le `RoutingSlipExecutor` est un composant DI pur — il n'a aucune dépendance sur le type de worker.

```
┌─────────────────────────────────────────────────────────────────────┐
│                     WORKER (tout hôte .NET)                          │
│      Azure Function App / .NET Worker Service / ACA / etc.          │
│                                                                       │
│  [ServiceBusTrigger] ou ServiceBusProcessor reçoit ServiceBusMessage │
│                │                                                      │
│                ▼                                                      │
│  RoutingSlipExecutor.ProcessAsync(message, actions, ct)              │   Queue
│  RoutingSlipExecutor.ExecuteAsync(message, actions, ct)              │   Topic
│                │                                                      │
│    ┌───────────┼──────────────────────────────────────────────────┐  │
│    │           ▼          RoutingSlipExecutor (interne EMT)        │  │
│    │  1. Désérialise SlipEnvelope depuis le corps du message      │  │
│    │  2. Résout l'étape courante : envelope.Steps[cursor]         │  │
│    │  3. Résout IRoutingSlipActivity<TArgs> depuis DI             │  │
│    │  4. Désérialise Steps[cursor].Arguments → TArgs              │  │
│    │  5. Construit ActivityContext<TArgs>                         │  │
│    │  6. Appelle activity.ExecuteAsync(ctx, ct)                   │  │
│    │            │                                                  │  │
│    │            ▼  Votre code métier retourne ActivityResult       │  │
│    │                                                               │  │
│    │  7. Traite le résultat :                                      │  │
│    │     Next()     → avance curseur, publie vers étape suivante   │  │
│    │     Complete() → CompleteMessageAsync()                       │  │
│    │     Fault(ex)  → compensateurs + DeadLetterMessageAsync()     │  │
│    │     Retry*()   → ImmediateRetry ou ExponentialRetry (EMT)     │  │
│    └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

| Type de worker | Cas d'usage recommandé |
|---|---|
| **Azure Function App** (SB Trigger) | Event-driven, serverless, scale automatique, facturation par exécution |
| **.NET Worker Service** (BackgroundService) | Traitement continu, contrôle fin du cycle de vie, déploiement sur VM/AKS |
| **Azure Container Apps** | Worker Service conteneurisé avec scale-to-zero |


### 7.2 Consumer et Action — propriétés de message, validation stricte et pattern entreprise

> ⚠️ **NOUVEAU (v2.0) : Validation stricte obligatoire côté activateur**

#### Règle d'or : chaque étape Topic du Routing Slip doit porter explicitement les propriétés `Consumer` et `Action` (si applicable), extraites de la configuration centrale (AppSettings.Endpoints) et injectées dans le slip au moment de la construction.

**Pattern correct :**

- Le `RoutingSlipBuilder` (ou l'activateur) résout, pour chaque étape de type Topic, les propriétés `Consumer` et `Action` via l'`IEndpointResolver`.
- Ces propriétés sont insérées dans le `SlipStep.Subscription` du slip.
- **Si l'une de ces propriétés est absente ou vide, l'activateur lève une exception et refuse de publier le slip.**
- Le `Producer` et l'`AzureMessagingProvider` propagent ces propriétés comme Application Properties sur Service Bus.
- Le worker/consumer ne doit plus jamais dépendre de `context.Variables` pour ces propriétés.

#### Exemple de validation stricte dans l'activateur (BookingActivateur)

```csharp
private static Dictionary<string, object> BuildInitialProperties(SlipStep firstStep)
{
    // Validation stricte : Consumer et Action sont obligatoires pour les topics
    if (firstStep.EntityType == MessagingEntityType.Topic)
    {
        if (string.IsNullOrWhiteSpace(firstStep.Subscription?.Consumer))
            throw new InvalidOperationException($"Consumer manquant pour l'étape topic '{firstStep.Name}'");
        if (string.IsNullOrWhiteSpace(firstStep.Subscription?.Action))
            throw new InvalidOperationException($"Action manquante pour l'étape topic '{firstStep.Name}'");
    }
    var props = new Dictionary<string, object>();
    if (firstStep.Subscription != null)
    {
        props["Consumer"] = firstStep.Subscription.Consumer;
        props["Action"] = firstStep.Subscription.Action!;
    }
    return props;
}
```

**Ce pattern garantit :**
- Que chaque message publié sur un topic porte les propriétés nécessaires au routage par Service Bus (règles SQL configurées par la pipeline DevOps).
- Qu'aucun message ne peut être publié sans ces propriétés, évitant ainsi les erreurs silencieuses et les messages orphelins.
- Que le code du worker/consumer est simplifié : il reçoit toujours les bonnes propriétés via Application Properties, sans avoir à les extraire ou à les deviner.

#### Anti-patterns à proscrire

- ❌ Injecter dynamiquement `Consumer` ou `Action` via des variables de contexte ou des arguments d'API.
- ❌ Laisser le worker/consumer deviner ou reconstruire ces propriétés à partir du message ou d'une logique locale.
- ❌ Publier un slip sans vérifier la présence de ces propriétés pour chaque étape Topic.

#### Bloc pédagogique — Pourquoi cette validation stricte ?

> **Pour un développeur junior :**
>
> - Si tu oublies d'injecter `Consumer` ou `Action`, le message ne sera pas routé correctement par Service Bus. Il risque d'être perdu, ignoré, ou de rester bloqué dans le topic sans jamais atteindre le bon abonné.
> - La validation stricte côté activateur permet de détecter l'erreur immédiatement, avant même que le message ne parte sur le bus.
> - Cela aligne le code applicatif avec la configuration d'infrastructure (règles SQL créées par DevOps), et évite les bugs difficiles à diagnostiquer en production.
> - Pour vérifier que ton slip est correct, tu dois : (1) t'assurer que chaque étape Topic a bien un `Consumer` et un `Action` dans la config, (2) que le builder les injecte dans le slip, (3) que le test unitaire de l'activateur échoue si tu retires l'une de ces propriétés.

**Résumé :**

- **Pattern entreprise :** validation stricte, injection explicite, aucune tolérance à l'absence de Consumer/Action pour les topics.
- **Pattern pédagogique :** toujours vérifier la présence de ces propriétés dans les tests unitaires et lors de la revue de code.

---

Le reste du pipeline (Producer, AzureMessagingProvider, worker) reste inchangé : il propage et consomme ces propriétés comme Application Properties, sans logique supplémentaire.

---

> ⚠️ **Responsabilité claire — à lire attentivement.**
> Le composant Routing Slip **ne crée pas, ne gère pas et ne configure jamais les abonnements Service Bus**. La création des abonnements et de leurs règles SQL de filtre est la responsabilité de la **pipeline DevOps** (Bicep, Terraform, etc.).
>
> Le rôle du Routing Slip est uniquement de **publier le message avec `Consumer` et `Action` comme Application Properties** Service Bus. Service Bus lit ces propriétés et route le message vers l'abonnement dont la règle SQL correspond.

```
Routing Slip publie sur topic "topic-enrichir"
avec Application Properties :
    Consumer = "EnrichirConsumer"
    Action   = "Traiter"
              │
              ▼
  ┌─── Service Bus Topic "topic-enrichir" ───┐
  │                                           │
  │  Abonnement "EnrichirConsumer.Traiter"   │
  │  Règle SQL (créée par DevOps pipeline) : │
  │    Action = 'Traiter'                     │
  │    AND Consumer = 'EnrichirConsumer'      │
  │               │                           │
  └───────────────┼───────────────────────────┘
                  ▼
        Worker reçoit le message
              ↓
       RoutingSlipExecutor.ExecuteAsync()
```

**Exemple de règle SQL créée par la pipeline DevOps :**

```sql
-- Règle de filtre sur l'abonnement (gérée par DevOps, pas par EMT)
Action = 'MajNoTrnsm' AND (Consumer = 'PPP')

-- Multi-consumer sur la même action :
Action = 'Traiter' AND (Consumer = 'EnrichirConsumer' OR Consumer = 'AutreConsumer')
```

**Ce que le `RoutingSlipExecutor` fait côté publication (code interne simplifié) :**

```csharp
// Quand l'étape suivante est un Topic, le RoutingSlipExecutor
// ajoute Consumer et Action comme Application Properties Service Bus.
// C'est la seule interaction avec Consumer/Action — AUCUNE gestion de subscription.
private async Task PublishNextStepAsync(SlipEnvelope envelope, int nextCursor, CancellationToken ct)
{
    var nextStep = envelope.Steps[nextCursor];

    MessagingOptions options;
    if (nextStep.EntityType == MessagingEntityType.Topic)
    {
        // Publication Topic : Consumer + Action → Application Properties
        // → Service Bus route vers l'abonnement dont la règle SQL correspond
        options = new MessagingOptions
        {
            Target     = nextStep.EntityName,         // "topic-enrichir"
            Properties = new Dictionary<string, object>
            {
                ["Consumer"] = nextStep.Subscription!.Consumer, // "EnrichirConsumer"
                ["Action"]   = nextStep.Subscription.Action ?? string.Empty // "Traiter"
            }
        };
    }
    else
    {
        // Publication Queue : pas de propriétés de routage nécessaires
        options = new MessagingOptions { Target = nextStep.EntityName };
    }

    await _producer.PublishAsync(updatedEnvelope, options, ct);
}
```

### 7.3 Worker Queue — `ProcessAsync`

Pour une étape **Queue**, le worker appelle `RoutingSlipExecutor.ProcessAsync()`. Le message arrive directement dans la bonne queue — aucun filtrage supplémentaire.

> 💡 **Pourquoi `IServiceScopeFactory` et non l'injection directe ?** `IRoutingSlipExecutor` est enregistré `Scoped` par `AddRoutingSlipActivity`. Dans Azure Functions Isolated, les classes Function peuvent être traitées comme des **Singletons** par le host — injecter directement un service Scoped dans un Singleton provoque un **Captive Dependency** : l'executor capturé ne serait jamais recréé, causant des problèmes d'état entre invocations. La solution correcte : injecter `IServiceScopeFactory` (Singleton) et créer un scope explicite par invocation avec `CreateAsyncScope()`. Voir [§7.5 Pattern IServiceScopeFactory](#75-pattern-iservicescopefactory--captive-dependency) pour l'explication détaillée.

```csharp
// ── Azure Function App (Isolated Worker) — Queue ──────────────────────
// PATTERN CORRECT : IServiceScopeFactory + scope par invocation
public class ValiderAdmissibiliteFunction
{
    private readonly IMessagingProvider  _messagingProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public ValiderAdmissibiliteFunction(
        IMessagingProvider messagingProvider,
        IServiceScopeFactory scopeFactory)
    {
        _messagingProvider = messagingProvider;
        _scopeFactory      = scopeFactory;
    }

    [Function(nameof(ValiderAdmissibiliteFunction))]
    public async Task Run(
        [ServiceBusTrigger("queue-valider-admissibilite",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        CancellationToken         ct)
    {
        // Scope explicite par invocation — garantit la durée de vie Scoped correcte
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
        _messagingProvider.BindContext(message, actions);
        await executor.ProcessAsync(_messagingProvider, ct);
    }
}

// ── .NET Worker Service (BackgroundService) — Queue ───────────────────
// Le ServiceBusProcessor crée déjà un scope par événement via son propre mécanisme
// → utiliser IServiceProvider directement (pas besoin de IServiceScopeFactory)
public class ValiderAdmissibiliteWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IServiceProvider _services;
    private readonly IConfiguration   _config;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var queueName = _config["ValiderAdmissibilite_Queue"]!;
        var processor = _client.CreateProcessor(queueName);

        processor.ProcessMessageAsync += async args =>
        {
            await using var scope = _services.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
            await executor.ProcessAsync(args.Message, args, ct);
        };
        processor.ProcessErrorAsync += args => { /* log */ return Task.CompletedTask; };

        await processor.StartProcessingAsync(ct);
        await Task.Delay(Timeout.Infinite, ct);
    }
}
```

### 7.4 Worker Topic — `ExecuteAsync`

Pour une étape **Topic**, le worker appelle `RoutingSlipExecutor.ExecuteAsync()`. L'abonnement Service Bus a **déjà filtré** le message via la règle SQL (Consumer + Action configurée par DevOps) — le worker n'a qu'à exécuter l'activité.

Le **même pattern `IServiceScopeFactory`** s'applique, avec `ExecuteAsync` à la place de `ProcessAsync`.

```csharp
// ── Azure Function App (Isolated Worker) — Topic ──────────────────────
// PATTERN CORRECT : IServiceScopeFactory + scope par invocation
public class EnrichirDonneesFunction
{
    private readonly IMessagingProvider   _messagingProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrichirDonneesFunction(
        IMessagingProvider messagingProvider,
        IServiceScopeFactory scopeFactory)
    {
        _messagingProvider = messagingProvider;
        _scopeFactory      = scopeFactory;
    }

    [Function(nameof(EnrichirDonneesFunction))]
    public async Task Run(
        [ServiceBusTrigger(
            topicName:        "topic-enrichir",
            subscriptionName: "sub-enrichir",
            Connection        = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        CancellationToken         ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
        _messagingProvider.BindContext(message, actions);
        await executor.ExecuteAsync(_messagingProvider, ct);
        // ↑ même pipeline interne que ProcessAsync —
        //   la distinction est sémantique : les messages Topic sont pré-filtrés par SB
    }
}

// ── .NET Worker Service (BackgroundService) — Topic ───────────────────
public class EnrichirDonneesWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IServiceProvider _services;
    private readonly IConfiguration   _config;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var topicName        = _config["EnrichirDonnees_Topic"]!;
        var subscriptionName = _config["EnrichirDonnees_Subscription"]!;
        var processor = _client.CreateProcessor(topicName, subscriptionName);

        processor.ProcessMessageAsync += async args =>
        {
            await using var scope = _services.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
            await executor.ExecuteAsync(args.Message, args, ct);
        };
        processor.ProcessErrorAsync += args => { /* log */ return Task.CompletedTask; };

        await processor.StartProcessingAsync(ct);
        await Task.Delay(Timeout.Infinite, ct);
    }
}
```

> 💡 **`ProcessAsync` vs `ExecuteAsync` — quelle différence ?**
> Les deux méthodes exécutent le **même pipeline interne** du `RoutingSlipExecutor`. La distinction est **sémantique et documentaire** :
> - `ProcessAsync` → étape **Queue** : le message arrive directement à la bonne entité, sans filtrage externe.
> - `ExecuteAsync` → étape **Topic** : le message a été pré-filtré par la règle SQL de l'abonnement (Consumer + Action). Le slip est simplement exécuté.

### 7.5 Pattern `IServiceScopeFactory` — Captive Dependency

#### Le problème : Captive Dependency

Dans Azure Functions Isolated, le host .NET peut traiter les classes Function comme des **Singletons**. Or, `AddRoutingSlipActivity` enregistre `IRoutingSlipExecutor` comme **Scoped** (1 instance par invocation). Si on injecte directement `IRoutingSlipExecutor` dans le constructeur d'une classe Function Singleton, on obtient un **Captive Dependency** :

```
Singleton (Function class)
    └─ capture → IRoutingSlipExecutor (Scoped)
                     └─ capture → IRoutingSlipActivity<TArgs> (Scoped)
                                      └─ capture → vos dépendances métier (Scoped)
                                                       ex: DbContext, HttpClient scopé, etc.

✗ Problème : l'executor et toutes ses dépendances Scoped sont gelés
  dans la durée de vie du Singleton.
  - Le DbContext ne sera jamais recréé entre les invocations.
  - Un HttpClient scopé gardera des connexions périmées.
  - L'état Scoped "fuit" entre les invocations → bugs intermittents très difficiles à reproduire.
```

#### La solution : `IServiceScopeFactory`

```csharp
// ✗ ANTI-PATTERN — injection directe d'un Scoped dans un Singleton
public class BookingFunctions
{
    private readonly IRoutingSlipExecutor _executor; // ← Scoped capturé dans un Singleton
    public BookingFunctions(IRoutingSlipExecutor executor) { _executor = executor; }

    [Function("ReserverVoiture")]
    public async Task Run(ServiceBusReceivedMessage msg, ServiceBusMessageActions actions, CancellationToken ct)
    {
        // _executor utilise TOUJOURS la même instance Scoped depuis le démarrage → bug silencieux
        await _executor.ProcessAsync(_messagingProvider, ct);
    }
}

// ✅ PATTERN CORRECT — IServiceScopeFactory crée un scope frais par invocation
public class BookingFunctions
{
    private readonly IMessagingProvider   _messagingProvider;
    private readonly IServiceScopeFactory _scopeFactory; // ← Singleton, sûr à injecter

    public BookingFunctions(IMessagingProvider messagingProvider, IServiceScopeFactory scopeFactory)
    {
        _messagingProvider = messagingProvider;
        _scopeFactory      = scopeFactory;
    }

    [Function("ReserverVoiture")]
    public async Task Run(ServiceBusReceivedMessage message, ServiceBusMessageActions actions, CancellationToken ct)
    {
        // Scope frais créé à chaque invocation → nouvelles instances Scoped garanties
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>();
        //             ↑ IRoutingSlipExecutor + IRoutingSlipActivity<TArgs> + vos dépendances :
        //               tous créés frais dans ce scope, disposés à la fin du using
        _messagingProvider.BindContext(message, actions);
        await executor.ProcessAsync(_messagingProvider, ct);
        // À la fin du using : scope.DisposeAsync() → dispose toutes les dépendances Scoped proprement
    }
}
```

#### Tableau récapitulatif des durées de vie

| Service | Durée de vie | Pourquoi |
|---|---|---|
| `IServiceScopeFactory` | **Singleton** | Service fondamental du runtime DI — sûr à capturer dans un Singleton |
| `IMessagingProvider` | **Singleton ou Scoped** | Lié au transport SB — injecté directement dans la classe Function |
| `IRoutingSlipExecutor` | **Scoped** | Porte l'état de l'exécution courante (curseur, variables) |
| `IRoutingSlipActivity<TArgs>` | **Scoped** | Peut injecter des services Scoped (DbContext, HttpClient scopé, etc.) |
| Vos dépendances métier | **Scoped ou Transient** | À choisir selon leur nature — resolues dans le scope créé par `_scopeFactory` |

#### `InternalsVisibleTo` — accès à `IRoutingSlipExecutor`

`IRoutingSlipExecutor` est `internal` dans EMT (principe de surface publique réduite, ADR-007). Les projets Workers Exemples y accèdent via `InternalsVisibleTo` déclaré dans `GlobalSuppressions.cs` :

```csharp
// GlobalSuppressions.cs dans EnterpriseMessageTransit
[assembly: InternalsVisibleTo("RAMQ.Samples.Queue.RoutingSlip.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Queue.RoutingSlip.Booking.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Topic.RoutingSlip.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Topic.RoutingSlip.Booking.Worker")]
```

> 💡 **Pour les projets applicatifs réels** (hors Exemples), la résolution via `IServiceScopeFactory` et `GetRequiredService<IRoutingSlipExecutor>()` fonctionne à l'exécution même si `IRoutingSlipExecutor` est `internal` — le DI framework résout par le type enregistré, pas par le nom de l'interface en C#. L'`InternalsVisibleTo` n'est nécessaire que pour référencer le type par son nom dans le code source (déclaration de variable, paramètre de méthode générique). Si votre projet applicatif se contente de `var executor = scope.ServiceProvider.GetRequiredService<IRoutingSlipExecutor>()` (résolution late-binding), l'`InternalsVisibleTo` n'est pas requis.

### 7.6 Configuration DI et `local.settings.json`

```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.ConfigureAzureProviders(ctx.Configuration);
        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));

        // Étape Queue — target logique == Target dans AppSettings.Endpoints
        services.AddRoutingSlipActivity<ValiderAdmissibiliteActivity, ValiderArgs>(
            target: "ValiderAdmissibilite");

        // Étape Topic — même API
        // AppSettings.Endpoints.Find("EnrichirDonnees").Endpoint.Subscription
        //   → Consumer="EnrichirConsumer", Action="Traiter"
        // → lors de Next(), RoutingSlipExecutor publie avec ces valeurs comme Application Properties
        services.AddRoutingSlipActivity<EnrichirDonneesActivity, EnrichirArgs>(
            target: "EnrichirDonnees");

        services.AddRoutingSlipActivity<NotifierBeneficiaireActivity, NotifierArgs>(
            target: "NotifierBeneficiaire");
    })
    .Build();
```

**`appsettings.json` — source de vérité unique :**

```json
{
  "AppSettings": {
    "ServiceBusNamespace": "mon-namespace.servicebus.windows.net",
    "ApplicationName":     "TraiterDossierWorker",
    "EnableJsonIndentation": false,
    "MessageTransitJournalName":    "COMJournalAIS",
    "MessageTransitJournalStoreUri": "https://storageramq.table.core.windows.net/",
    "Endpoints": [
      {
        "Target":   "ValiderAdmissibilite",
        "Endpoint": { "EntityName": "queue-valider-admissibilite", "EntityType": "Queue" }
      },
      {
        "Target":   "EnrichirDonnees",
        "Endpoint": {
          "EntityName":   "topic-enrichir",
          "EntityType":   "Topic",
          "Subscription": { "Consumer": "EnrichirConsumer", "Action": "Traiter" }
        }
      },
      {
        "Target":   "NotifierBeneficiaire",
        "Endpoint": { "EntityName": "queue-notifier", "EntityType": "Queue" }
      }
    ]
  }
}
```

> 💡 **`EnableJsonIndentation`** : EMT sérialise les messages en JSON compact par défaut (`false`). Mettre `true` uniquement en développement pour faciliter le débogage. Le `SlipEnvelope` utilise le même paramètre.

**`local.settings.json` — aliases de triggers Azure Functions :**

```json
{
  "Values": {
    "ServiceBusConnection__fullyQualifiedNamespace": "mon-namespace.servicebus.windows.net",
    "ValiderAdmissibilite_Queue":   "queue-valider-admissibilite",
    "EnrichirDonnees_Topic":        "topic-enrichir",
    "EnrichirDonnees_Subscription": "EnrichirConsumer.Traiter",
    "NotifierBeneficiaire_Queue":   "queue-notifier"
  }
}
```

> ⚠️ **Règle de cohérence obligatoire.** La valeur de `ValiderAdmissibilite_Queue` dans `Values` **doit correspondre** à l'`EntityName` de l'entrée `Target == "ValiderAdmissibilite"` dans `AppSettings.Endpoints`. La convention `{Target}_Queue` / `{Target}_Topic` / `{Target}_Subscription` rend ce lien explicite.

---

## 8. Comment ça s'appuie sur EMT existant

Le Routing Slip n'invente pas de nouveau transport. Il réutilise toutes les features existantes d'EMT.

### `IMessageProducer<SlipEnvelope>` — envoi à l'étape suivante

```
RoutingSlipExecutor.AdvanceAsync()
    │
    └─► IMessageProducer<SlipEnvelope>.PublishAsync(
            new MessageTransitContext<SlipEnvelope> { Message = slipAvecCursorIncrementé },
            new PublishOptions { Target = ResolveTarget(steps[cursor + 1]) }
            //  Queue → steps[cursor + 1].EntityName          (ex: "queue-notifier")
            //  Topic → "{Consumer}" ou "{Consumer}.{Action}" (ex: "EnrichirConsumer.Traiter")
        )
            │
            └─► AzureMessagingProvider.SendAsync()  ← EXISTANT, inchangé
                    │
                    └─► ServiceBusSenderCache        ← EXISTANT, réutilisé
                            (senders mis en cache, pas de reconnexion à chaque hop)
```

### Claim-Check — propagation du token, décision à l'activité

Les `Variables` du slip peuvent grossir au fil des étapes (données enrichies, documents, résultats de calcul). Si le `SlipEnvelope` dépasse 256 Ko, le `Producer` existant active automatiquement le **Claim-Check** :

```
SlipEnvelope (trop grand > 256 Ko)
    └─► Producer existant détecte le dépassement
    └─► Uploade le corps dans Azure Blob Storage (IStorageProvider existant)
    └─► Envoie sur Service Bus un message léger avec un token de référence (BlobReference)
    └─► L'étape suivante reçoit le token dans ctx.ClaimCheckToken — elle décide quoi en faire
```

**Comportement depuis la v0.8 :** `DeserializeMessageAsync` **ne télécharge plus automatiquement** le blob. Le token est propagé fidèlement dans `ActivityContext.ClaimCheckToken`. C'est l'activité (ou son API downstream) qui décide comment consommer le payload :

| Scénario | Qui accède au blob | Comment |
|---|---|---|
| API downstream capable de lire le blob | L'API elle-même | L'activité passe `ctx.ClaimCheckToken.Reference` dans la requête API |
| API downstream ne supporte pas les blobs | L'activité | `_storageProvider.DownloadAsync(ctx.ClaimCheckToken.Reference, ct)` |
| Pas de claim-check actif | N/A | `ctx.ClaimCheckToken == null` — les données voyagent normalement |

> ⚠️ **Breaking change v0.8** : si votre activité comptait sur le fait que `ctx.Arguments` était auto-hydraté depuis le blob, vous devez maintenant gérer explicitement le cas `ctx.ClaimCheckToken != null`. Voir §6.1 pour les deux patterns (API-first et fallback self-download).

### `IJournalProvider` — trace complète du workflow

À chaque hop, le framework écrit une entrée dans le **Message Transit Journal** existant :

```csharp
// Ce que le framework écrit automatiquement à chaque transition
await _journal.WriteRecordAsync(JournalEntry.ForPublish(
    consumer:     currentStep.Name,           // ex: "ValiderAdmissibilite"
    action:       $"RoutingSlip.{slipName}",  // ex: "RoutingSlip.TraiterDossier"
    messageId:    envelope.Header.SlipId,
    correlationId: envelope.Header.CorrelationId,
    target:       nextStep != null ? ResolveTarget(nextStep) : "COMPLETED"
    //              Queue → EntityName          Topic → Consumer[.Action]
));
```

Résultat dans le journal : vous pouvez retracer le chemin exact de chaque dossier à travers toutes les étapes.

### `ActivitySource` — traces distribuées W3C

Le `traceparent` W3C est propagé automatiquement à chaque hop via `ApplicationProperties` de Service Bus (feature existante). Résultat dans Application Insights :

```
Trace complète d'un dossier (TraceId: abc123)
│
├─ [queue-valider]   messaging.consume  200ms
│   └─ messaging.routing_slip.step  step=ValiderAdmissibilite  85ms
│
├─ [queue-enrichir]  messaging.consume  340ms
│   └─ messaging.routing_slip.step  step=EnrichirDonnees  310ms
│
└─ [queue-notifier]  messaging.consume  120ms
    └─ messaging.routing_slip.step  step=NotifierBeneficiaire  95ms  status=Completed
```

### `IMetricsProvider` — 3 nouvelles métriques

S'ajoutent aux 17 métriques existantes :

| Métrique | Ce qu'elle mesure | Exemple d'alerte |
|----------|-------------------|-----------------|
| `routing_slip_started_total` | Nombre de slips démarrés | Volume anormal de démarrages |
| `routing_slip_step_duration_ms` | Durée de chaque étape (p50/p95/p99) | Étape qui prend trop de temps |
| `routing_slip_completed_total` | Slips terminés (Completed ou Faulted) | Taux d'erreur élevé |

---

## 9. Flux pas-à-pas — ce qui se passe vraiment

Prenons un exemple concret : traitement d'un dossier avec 3 étapes.

### Étape 0 : l'activateur démarre le slip

L'activateur déclare **l'itinéraire complet dans son `appsettings.json`** via la clé `Endpoints`. Le `RoutingSlipBuilder` résout les `EntityName` via `IEndpointResolver` — aucun nom physique d'entité dans le code.

#### Types de host supportés pour l'activateur

L'activateur est un composant ordinaire — il n'y a pas de contrainte sur le type de host. Tous les types de triggers EMT ou ASP.NET Core peuvent être utilisés :

| Type de host | Quand l'utiliser | Exemple |
|---|---|---|
| **Azure Function HTTP Trigger** | Démarrage synchrone déclenché par API REST ou webhook | Une requête HTTP crée le slip et retourne `202 Accepted` |
| **Azure Function Timer Trigger** | Démarrage planifié (batch, nuit, hebdomadaire) | Un timer CRON lance le slip chaque nuit à 02:00 |
| **Azure Function Service Bus Trigger** | Démarrage déclenché par un autre message (chaîne de workflows) | Un message "DossierReçu" déclenche le slip d'approbation |
| **Worker Service (`IHostedService`)** | Traitement continu ou polling | Un service lit une base de données et crée des slips pour les nouveaux dossiers |
| **Application console** | Scripts ponctuels, migration, tests | Chargement en lot depuis un fichier CSV |

**Quel que soit le type de host, le pattern est identique :**

```csharp
// Pattern activateur — identique pour HTTP Trigger, Timer Trigger, Worker, console
var slip = new RoutingSlipBuilder("NomWorkflow", _endpointResolver)
    .AddStep<ValiderArgs>  ("ValiderAdmissibilite", new ValiderArgs  { DossierId = id })
    .AddStep<EnrichirArgs> ("EnrichirDonnees",      new EnrichirArgs { DossierId = id })
    .AddStep<NotifierArgs> ("NotifierBeneficiaire", new NotifierArgs { DossierId = id })
    .Build();

// Pré-validation : vérifier que tous les endpoints existent dans Service Bus
// avant de publier — évite les slips qui échouent immédiatement
await slip.ValidateEndpointsAsync(_serviceBusAdminClient, ct);

await _producer.PublishAsync(slip, ct);
```

#### Pré-validation des entités Service Bus

Avant de publier un slip, l'activateur doit s'assurer que toutes les entités Service Bus référencées dans le slip (queues et topics) existent réellement. Une publication vers une entité inexistante échoue silencieusement ou lève une exception tardive difficile à diagnostiquer.

**Pattern recommandé :**

```csharp
// Dans l'activateur — au démarrage (IHostedService.StartAsync) ou au moment de la publication
public async Task ValidateEndpointsAsync(SlipEnvelope slip, ServiceBusAdministrationClient admin, CancellationToken ct)
{
    var missingEntities = new List<string>();

    foreach (var step in slip.Steps)
    {
        if (step.EntityType == EntityType.Queue)
        {
            if (!await admin.QueueExistsAsync(step.EntityName, ct))
                missingEntities.Add($"Queue '{step.EntityName}' (étape '{step.Name}') introuvable");
        }
        else // Topic
        {
            if (!await admin.TopicExistsAsync(step.EntityName, ct))
                missingEntities.Add($"Topic '{step.EntityName}' (étape '{step.Name}') introuvable");
        }
    }

    if (missingEntities.Count > 0)
        throw new InvalidOperationException(
            $"Entités Service Bus manquantes pour le workflow '{slip.WorkflowName}':\n" +
            string.Join("\n", missingEntities));
}
```

> 💡 **Quand valider ?** L'idéal est de valider **au démarrage** de l'activateur (dans `StartAsync` ou l'équivalent) — pas à chaque publication. Cela permet de détecter une mauvaise configuration au déploiement, avant que les premiers messages soient publiés.

**`appsettings.json` de l'activateur :**
```json
{
  "AppSettings": {
    "ServiceBusNamespace": "mon-namespace.servicebus.windows.net",
    "ApplicationName": "DossierActivateur",
    "Endpoints": [
      { "Target": "ValiderAdmissibilite", "Endpoint": { "EntityName": "queue-valider",  "EntityType": "Queue" } },
      { "Target": "EnrichirDonnees",      "Endpoint": { "EntityName": "topic-enrichir", "EntityType": "Topic",
          "Subscription": { "Consumer": "EnrichirConsumer", "Action": "Traiter" } } },
      { "Target": "NotifierBeneficiaire", "Endpoint": { "EntityName": "queue-notifier", "EntityType": "Queue" } }
    ]
  }
}
```

```csharp
// Azure Function HTTP Trigger — DossierActivateur.cs
[Function("DemarrerTraitement")]
public async Task<IActionResult> Run([HttpTrigger] HttpRequest req)
{
    var demande = await req.ReadFromJsonAsync<DemandeDossier>();

    // RoutingSlipBuilder reçoit IEndpointResolver par DI — résout EntityName depuis Endpoints
    var slip = new RoutingSlipBuilder("TraiterDossier", _endpointResolver)
        .AddStep<ValiderArgs>  ("ValiderAdmissibilite",
            new ValiderArgs { DossierId = demande.DossierId, NAS = demande.NAS })
        .AddStep<EnrichirArgs> ("EnrichirDonnees",
            new EnrichirArgs { DossierId = demande.DossierId })
        .AddStep<NotifierArgs> ("NotifierBeneficiaire",
            new NotifierArgs { Canal = "email", Destinataire = demande.Email })
        .Build();
    // ↑ Build() → IEndpointResolver.TryResolve("ValiderAdmissibilite") → EntityName="queue-valider-admissibilite"
    //             IEndpointResolver.TryResolve("EnrichirDonnees")      → EntityName="topic-enrichir"
    //                                                                      + Subscription{Consumer,Action}
    //             IEndpointResolver.TryResolve("NotifierBeneficiaire") → EntityName="queue-notifier"

    // Publier via IMessageProducer — target = nom logique de la première étape
    await _producer.PublishAsync(
        new MessageTransitContext<SlipEnvelope>
        {
            MessageId = slip.Header.SlipId,
            Message   = slip
        },
        new PublishOptions { Target = "ValiderAdmissibilite" }, // ← Target logique, résolu par IEndpointResolver
        cancellationToken);

    return new OkObjectResult(new { SlipId = slip.Header.SlipId });
}
```

### Étape 1 : `queue-valider` reçoit le message

```
Service Bus → queue-valider

SlipEnvelope {
  Header: { SlipId: "slip-001", SlipName: "TraiterDossier" },
  Steps: [
    { Name: "ValiderAdmissibilite", EntityName: "queue-valider",  EntityType: "Queue",
                                    Status: Active   },  ← Cursor=0
    { Name: "EnrichirDonnees",      EntityName: "topic-enrichir", EntityType: "Topic",
                                    Subscription: { Consumer: "EnrichirConsumer", Action: "Traiter" },
                                    Status: Pending  },
    { Name: "NotifierBeneficiaire", EntityName: "queue-notifier", EntityType: "Queue",
                                    Status: Pending  }
  ],
  Cursor: 0,
  Variables: {}
}
```

> 💡 **Comment le worker sait où router sans connaître la config de l'activateur ?**
> Chaque `SlipStep` dans le `SlipEnvelope` contient déjà `EntityName`, `EntityType` et `Subscription` — écrits par le `RoutingSlipBuilder` au moment du `Build()`. Le worker reçoit le slip complet, lit `Steps[Cursor + 1].EntityName` et publie vers cette entité. Il n'a jamais besoin de consulter `AppSettings.Endpoints` de l'activateur ni de résoudre un `Target` — l'information de routing est auto-portée par le slip lui-même.

Le `RoutingSlipExecutor` (EMT) :
1. Désérialise le slip
2. Résout `IRoutingSlipActivity<ValiderArgs>` depuis la DI
3. Construit `ActivityContext<ValiderArgs>` avec `Arguments.DossierId = "D-001"`, `StepIndex = 0`, `TotalSteps = 3`
4. Appelle `ValiderAdmissibiliteActivity.ExecuteAsync(ctx, ct)`

Votre activité :
```csharp
public class ValiderAdmissibiliteActivity : IRoutingSlipActivity<ValiderArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        // Logique métier pure — aucune dépendance EMT
        var isEligible = await _serviceAdmissibilite.ValiderAsync(ctx.Arguments.DossierId, ctx.Arguments.NAS, ct);

        if (!isEligible)
            return ActivityResult.Fault(new InvalidOperationException($"Dossier {ctx.Arguments.DossierId} non admissible"));

        // Enrichir les variables pour les étapes suivantes
        return ActivityResult.Next(vars =>
        {
            vars["DateValidation"]  = JsonSerializer.SerializeToElement(DateTime.UtcNow);
            vars["EstAdmissible"]   = JsonSerializer.SerializeToElement(true);
        });
    }
}
```

Le `RoutingSlipExecutor` reçoit `Next()` :
- Met `Steps[0].Status = Completed`
- Incrémente `Cursor` à 1
- Met `Steps[1].Status = Active`
- Publie le slip mis à jour vers `queue-enrichir`

### Étape 2 : `queue-enrichir` reçoit le message

```
SlipEnvelope {
  Steps: [
    { Name: "ValiderAdmissibilite", Status: Completed },
    { Name: "EnrichirDonnees",      Status: Active    },  ← Cursor=1
    { Name: "NotifierBeneficiaire", Status: Pending   }
  ],
  Cursor: 1,
  Variables: { "DateValidation": "2026-05-07T14:30:01Z", "EstAdmissible": true }
             ↑ ajoutées par l'étape précédente
}
```

Votre activité peut lire les variables de l'étape précédente :
```csharp
public class EnrichirDonneesActivity : IRoutingSlipActivity<EnrichirArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
    {
        // Lire une variable mise par l'étape précédente
        var dateValidation = (DateTime)ctx.Variables["DateValidation"];

        var nomBeneficiaire = await _registre.ObtenirNomAsync(ctx.Arguments.DossierId, ct);

        return ActivityResult.Next(vars => vars["NomBeneficiaire"] = nomBeneficiaire);
    }
}
```

### Étape 3 : `queue-notifier` reçoit le message (dernière étape)

```
SlipEnvelope {
  Steps: [
    { Name: "ValiderAdmissibilite", Status: Completed },
    { Name: "EnrichirDonnees",      Status: Completed },
    { Name: "NotifierBeneficiaire", Status: Active    }   ← Cursor=2, dernière étape
  ],
  Cursor: 2,
  Variables: { "DateValidation": "...", "EstAdmissible": true, "NomBeneficiaire": "Jean Tremblay" }
}
```

```csharp
public class NotifierBeneficiaireActivity : IRoutingSlipActivity<NotifierArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<NotifierArgs> ctx, CancellationToken ct)
    {
        var nom = ctx.Variables["NomBeneficiaire"].ToString();

        await _emailService.EnvoyerAsync(ctx.Arguments.Destinataire,
            $"Bonjour {nom}, votre dossier a été traité.", ct);

        return ActivityResult.Next(); // ou ActivityResult.Complete() — les deux sont équivalents ici
        // Le framework détecte que Cursor == Steps.Length - 1 → CompleteMessageAsync() automatique
    }
}
```

---

## 10. Format du message sur Service Bus (wire format v2.0)

> ⚠️ **Breaking change v2.0.** En v1.x, le format sur le wire était `MessageTransitContext<T>` sérialisé directement (champs `MessageType`, `CurrentStage`, `Message` en PascalCase). En v2.0, l'unique format sérialisé est `MessageEnvelope` (champs `schemaVersion`, `kind`, `data` en camelCase). Les consumers doivent lire `schemaVersion` avant de désérialiser `data`.

Voici exactement ce qui circule sur Service Bus entre chaque étape (format v2.0) :

```json
{
  "schemaVersion": "2.0",
  "kind": "RoutingSlip",
  "messageId": "slip-001",
  "correlationId": "slip-001",
  "sessionId": null,
  "messageType": null,
  "tokens": null,
  "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "data": {
    "header": {
      "slipId":        "slip-001",
      "slipName":      "TraiterDossier",
      "correlationId": "slip-001",
      "createdAt":     "2026-05-07T14:30:00Z"
    },
    "steps": [
      {
        "name":       "ValiderAdmissibilite",
        "entityName": "queue-valider",
        "entityType": "Queue",
        "subscription": null,
        "status":     "Completed",
        "arguments":  { "dossierId": "D-001", "nas": "123456789" }
      },
      {
        "name":         "EnrichirDonnees",
        "entityName":   "topic-enrichir",
        "entityType":   "Topic",
        "subscription": { "consumer": "EnrichirConsumer", "action": "Traiter" },
        "status":       "Active",
        "arguments":    { "dossierId": "D-001" }
      },
      {
        "name":       "NotifierBeneficiaire",
        "entityName": "queue-notifier",
        "entityType": "Queue",
        "subscription": null,
        "status":     "Pending",
        "arguments":  { "canal": "email", "destinataire": "jean.tremblay@ramq.gouv.qc.ca" }
      }
    ],
    "cursor": 1,
    "variables": {
      "dateValidation": "2026-05-07T14:30:01Z",
      "estAdmissible":  true
    }
  }
}
```

**Points importants :**
- `schemaVersion: "2.0"` — toujours lire ce champ en premier avant de désérialiser `data`
- `kind: "RoutingSlip"` — distingue un message routing slip d'un message simple (`kind: "Message"`)
- `data.steps[cursor].subscription.consumer` et `.action` — correspondent aux Application Properties Service Bus pour le filtrage des abonnements Topic (voir §7)
- `data.variables` — toujours désérialisés via `GetVariable<T>` (round-trip JSON, pas de cast direct)
- `traceparent` — propagation W3C automatique à chaque hop pour le tracing distribué
- Si `data` dépasse 256 Ko → le Claim-Check s'active automatiquement : `tokens` est renseigné et `data` est remplacé par un pointeur blob

---

## 11. Exemple complet — traitement d'un dossier RAMQ

### Structure du projet

```
RAMQ.TraiterDossier/
├── Activateur/
│   └── DossierActivateur.cs             ← Azure Function HTTP qui démarre le slip
├── Functions/
│   ├── ValiderAdmissibilitéFunction.cs  ← Trigger Queue (EntityName depuis config)
│   ├── EnrichirDonneesFunction.cs       ← Trigger Topic (EntityName depuis config)
│   └── NotifierBeneficiaireFunction.cs  ← Trigger Queue (EntityName depuis config)
├── Activities/
│   ├── ValiderAdmissibiliteActivity.cs
│   ├── EnrichirDonneesActivity.cs
│   └── NotifierBeneficiaireActivity.cs
├── Args/
│   ├── ValiderArgs.cs
│   ├── EnrichirArgs.cs
│   └── NotifierArgs.cs
├── appsettings.json                     ← Itinerary[] — source de vérité des entités SB
├── local.settings.json                  ← Aliases triggers (%Target_Queue/Topic/Subscription%)
└── Program.cs
```

### appsettings.json — source de vérité des entités Service Bus

```json
{
  "AppSettings": {
    "ServiceBusNamespace":          "mon-namespace.servicebus.windows.net",
    "ApplicationName":              "TraiterDossierWorker",
    "MessageTransitJournalName":    "COMJournalAIS",
    "MessageTransitJournalStoreUri": "https://storageramq.table.core.windows.net/",
    "Endpoints": [
      {
        "Target":   "ValiderAdmissibilite",
        "Endpoint": { "EntityName": "queue-valider-admissibilite", "EntityType": "Queue" }
      },
      {
        "Target":   "EnrichirDonnees",
        "Endpoint": {
          "EntityName":  "topic-enrichir",
          "EntityType":  "Topic",
          "Subscription": { "Consumer": "EnrichirConsumer", "Action": "Traiter" }
        }
      },
      {
        "Target":   "NotifierBeneficiaire",
        "Endpoint": { "EntityName": "queue-notifier", "EntityType": "Queue" }
      }
    ]
  }
}
```

### local.settings.json — aliases de triggers Azure Functions

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage":          "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME":     "dotnet-isolated",
    "ServiceBusConnection__fullyQualifiedNamespace": "mon-namespace.servicebus.windows.net",

    "ValiderAdmissibilite_Queue":   "queue-valider-admissibilite",
    "EnrichirDonnees_Topic":        "topic-enrichir",
    "EnrichirDonnees_Subscription": "EnrichirConsumer.Traiter",
    "NotifierBeneficiaire_Queue":   "queue-notifier"
  }
}
```

> ⚠️ **Règle : les valeurs `_Queue/_Topic/_Subscription` dans `local.settings.json` DOIVENT correspondre aux `EntityName` et `Subscription` dans `AppSettings.Endpoints`.** Ce sont deux façons de lire la même information : EMT la lit via `IEndpointResolver` (runtime), Azure Functions la lit via `%...%` (bind-time). La convention `{Target}_Queue`, `{Target}_Topic`, `{Target}_Subscription` rend le lien entre les deux évident.

### Program.cs — enregistrement DI complet

```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        // ── Configuration EMT — lié à la section "AppSettings" dans appsettings.json ─
        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));

        // ── Service de configuration (même pattern que les exemples existants EMT) ──
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());

        // ── Providers EMT — lit ServiceBusNamespace depuis IConsumerConfigurationService ─
        services.ConfigureAzureProviders();

        // ── Activités Routing Slip — target logique uniquement ──────────────────────
        // L'EntityName est résolu depuis AppSettings.Endpoints par IEndpointResolver
        // Même mécanique que services.AddProducer<TMessage>("target") dans EMT
        services.AddRoutingSlipActivity<ValiderAdmissibiliteActivity, ValiderArgs>(
            target: "ValiderAdmissibilite");
        //  ↑ IEndpointResolver.TryResolve("ValiderAdmissibilite") → EntityName="queue-valider-admissibilite"

        services.AddRoutingSlipActivity<EnrichirDonneesActivity, EnrichirArgs>(
            target: "EnrichirDonnees");
        //  ↑ IEndpointResolver.TryResolve("EnrichirDonnees") → EntityName="topic-enrichir"
        //                                                      + Subscription{Consumer="EnrichirConsumer", Action="Traiter"}

        services.AddRoutingSlipActivity<NotifierBeneficiaireActivity, NotifierArgs>(
            target: "NotifierBeneficiaire");
    })
    .Build();
```

### Azure Functions triggers — un fichier par étape

```csharp
// Functions/ValiderAdmissibilitéFunction.cs — Trigger Queue
// %ValiderAdmissibilite_Queue% = "queue-valider-admissibilite" (lu depuis local.settings.json)
public class ValiderAdmissibilitéFunction
{
    private readonly IRoutingSlipExecutor _executor;
    public ValiderAdmissibilitéFunction(IRoutingSlipExecutor executor) => _executor = executor;

    [Function(nameof(ValiderAdmissibilitéFunction))]
    public async Task Run(
        [ServiceBusTrigger("%ValiderAdmissibilite_Queue%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        CancellationToken         cancellationToken)
        => await _executor.ProcessAsync(message, actions, cancellationToken);
}

// Functions/EnrichirDonneesFunction.cs — Trigger Topic
// %EnrichirDonnees_Topic%        = "topic-enrichir"           (depuis local.settings.json)
// %EnrichirDonnees_Subscription% = "EnrichirConsumer.Traiter" (depuis local.settings.json)
public class EnrichirDonneesFunction
{
    private readonly IRoutingSlipExecutor _executor;
    public EnrichirDonneesFunction(IRoutingSlipExecutor executor) => _executor = executor;

    [Function(nameof(EnrichirDonneesFunction))]
    public async Task Run(
        [ServiceBusTrigger("%EnrichirDonnees_Topic%", "%EnrichirDonnees_Subscription%",
            Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        CancellationToken         cancellationToken)
        => await _executor.ProcessAsync(message, actions, cancellationToken);
}

// Functions/NotifierBeneficiaireFunction.cs — Trigger Queue
// %NotifierBeneficiaire_Queue% = "queue-notifier" (depuis local.settings.json)
public class NotifierBeneficiaireFunction
{
    private readonly IRoutingSlipExecutor _executor;
    public NotifierBeneficiaireFunction(IRoutingSlipExecutor executor) => _executor = executor;

    [Function(nameof(NotifierBeneficiaireFunction))]
    public async Task Run(
        [ServiceBusTrigger("%NotifierBeneficiaire_Queue%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions  actions,
        CancellationToken         cancellationToken)
        => await _executor.ProcessAsync(message, actions, cancellationToken);
}
```

### `ValiderArgs.cs` — définition des arguments

```csharp
// Les arguments sont de simples POCO — aucune dépendance EMT
// Toujours utiliser [JsonPropertyName] explicite pour la stabilité du wire format
public sealed class ValiderArgs
{
    [JsonPropertyName("dossierId")]
    public required string DossierId { get; init; }

    // ⚠️ Données sensibles : NAS est une donnée protégée (LSSSS).
    // Si le slip risque de dépasser 256 Ko, le Claim-Check protège automatiquement
    // le payload dans Azure Blob. Pour les champs très sensibles, envisager
    // de ne passer qu'un identifiant et de lire la donnée depuis un store sécurisé
    // dans l'activité elle-même.
    [JsonPropertyName("nas")]
    public required string NAS { get; init; }
}
```

### Test unitaire d'une activité

```csharp
// ValiderAdmissibiliteActivityTests.cs
// ✅ Aucun mock EMT requis — l'activité est un POCO testable
[Fact]
public async Task ExecuteAsync_DossierAdmissible_RetourneNext()
{
    var serviceStub = new AdmissibiliteServiceStub(retourneAdmissible: true);
    var activity    = new ValiderAdmissibiliteActivity(serviceStub);

    var ctx = new ActivityContext<ValiderArgs>
    {
        Arguments  = new ValiderArgs { DossierId = "D-001", NAS = "123456789" },
        Variables  = new Dictionary<string, JsonElement>(),
        SlipId     = "slip-test",
        CorrelationId = "corr-test",
        Attempt    = 1,
        StepName   = "ValiderAdmissibilite",
        StepIndex  = 0,
        TotalSteps = 3
    };

    var result = await activity.ExecuteAsync(ctx, CancellationToken.None);

    Assert.IsType<ActivityResult.NextResult>(result);
}
```

---

## 12. Observabilité — comment voir ce qui se passe

### Dans Application Insights — Application Map

Vous verrez chaque queue/abonnement comme un nœud distinct avec les flèches de flux :
```
DossierActivateur → queue-valider → topic-enrichir/EnrichirConsumer.Traiter → queue-notifier
```

### Requête KQL — retracer un dossier complet

```kql
// Toutes les étapes d'un slip spécifique
dependencies
| where customDimensions["slip.id"] == "slip-001"
| project
    timestamp,
    etape     = tostring(customDimensions["slip.step_name"]),
    duree_ms  = duration,
    statut    = tostring(customDimensions["slip.status"]),
    success
| order by timestamp asc
```

### Requête KQL — slips en erreur aujourd'hui

```kql
customMetrics
| where name == "routing_slip_completed_total"
| where customDimensions["status"] == "Faulted"
| where timestamp > ago(24h)
| summarize total = sum(value) by
    slip_name = tostring(customDimensions["slip_name"]),
    bin(timestamp, 1h)
| order by timestamp desc
```

### Requête KQL — durée par étape (p95)

```kql
customMetrics
| where name == "routing_slip_step_duration_ms"
| where timestamp > ago(1h)
| summarize p95 = percentile(value, 95) by
    etape = tostring(customDimensions["step_name"])
| order by p95 desc
```

---

## 13. Gestion des erreurs et retry natif EMT

### Les trois mécanismes de retry EMT — rappel

EMT expose trois exceptions que le `RetryPolicyHandler` intercepte pour décider du sort d'un message. Dans le contexte du Routing Slip, votre activité **ne lève jamais ces exceptions directement** — elle retourne un `ActivityResult` et c'est le `RoutingSlipExecutor` qui les traduit.

```
Votre activité                     RoutingSlipExecutor              RetryPolicyHandler → Service Bus
─────────────────                  ────────────────────             ────────────────────────────────
ActivityResult.RetryImmediate()  → ImmediateRetryException      →  Complete + requeue immédiat
ActivityResult.RetryExponential()→ ExponentialRetryException    →  Abandon + délai exponentiel
ActivityResult.Fault(ex)         → (appel direct DLQ)           →  DeadLetterMessageAsync()
ActivityResult.Next()            → (avance le curseur)          →  Complete + publish vers step suivant
ActivityResult.Complete()        → (fin de slip)                →  Complete
```

> 💡 **Pour un junior — pourquoi ce découplage ?** Si votre activité levait `ImmediateRetryException` directement, elle aurait une dépendance sur le runtime EMT et ne pourrait plus être testée sans toute l'infrastructure. En retournant un `ActivityResult`, votre activité reste un POCO pur — testable avec un simple `new`.

| `ActivityResult` | Exception EMT levée | Comportement Service Bus | Quand l'utiliser |
|---|---|---|---|
| `RetryImmediate(reason)` | `ImmediateRetryException` | Complete + requeue immédiat | Contention DB courte, lock optimiste perdu |
| `RetryExponential(reason, ex?)` | `ExponentialRetryException` | Abandon → délai exponentiel (backoff + jitter) | Service tiers indisponible, timeout HTTP, 503/429 |
| `Fault(ex)` | — (DLQ direct) | `DeadLetterMessageAsync()` + compensation | Données irréparables, règle métier définitive |
| `Next(enrichVariables?)` | — | Complete + publish vers step suivant | Succès — passe à l'étape suivante |
| `Complete()` | — | Complete (fin de slip) | Succès — fin anticipée du slip |

### `ActivityResult.RetryImmediate` — retry sans délai

```csharp
// Cas d'usage : lock optimiste perdu sur la base de données
public async Task<ActivityResult> ExecuteAsync(ActivityContext<ReserverArgs> ctx, CancellationToken ct)
{
    try
    {
        await _db.ReserverPlaceAsync(ctx.Arguments.DossierId, ctx.Arguments.DateReservation, ct);
        return ActivityResult.Next();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Lock optimiste — un autre processus a modifié la ligne entre le SELECT et l'UPDATE.
        // RetryImmediate → RoutingSlipExecutor lève ImmediateRetryException
        //   → RetryPolicyHandler.HandleImmediateRetryAsync()
        //   → Service Bus : Complete du message courant + requeue immédiat sans délai
        //   → DeliveryCount incrémenté par Service Bus à la prochaine livraison
        return ActivityResult.RetryImmediate("Conflit de concurrence DB — retry immédiat");
    }
}
```

### `ActivityResult.RetryExponential` — backoff exponentiel

```csharp
// Cas d'usage : API tierce indisponible (503 / réseau)
public async Task<ActivityResult> ExecuteAsync(ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
{
    // Log de diagnostic : si Attempt > 1, c'est un rejeu
    if (ctx.Attempt > 1)
        _logger.LogWarning("Tentative {Attempt}/{Max} — étape {Step} — dossier {Id}",
            ctx.Attempt, ctx.TotalSteps, ctx.StepName, ctx.Arguments.DossierId);

    HttpResponseMessage reponse;
    try
    {
        reponse = await _httpClient.GetAsync(
            $"/api/beneficiaire/{ctx.Arguments.DossierId}", ct);
    }
    catch (HttpRequestException ex)
    {
        // Panne réseau — RetryExponential → ExponentialRetryException
        //   → RetryPolicyHandler.HandleExponentialRetryAsync()
        //   → Service Bus : Abandon → délai = InitialDelay × 2^(Attempt-1) + jitter
        return ActivityResult.RetryExponential("Réseau indisponible", ex);
    }

    return reponse.StatusCode switch
    {
        // 503 / 429 : service surchargé → backoff exponentiel
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests =>
            ActivityResult.RetryExponential(
                $"API enrichissement {(int)reponse.StatusCode} — dossier {ctx.Arguments.DossierId}"),

        // 200 : succès
        HttpStatusCode.OK => await TraiterReponseAsync(reponse, ctx, ct),

        // Autre (4xx, 5xx inconnu) : erreur permanente → DLQ
        _ => ActivityResult.Fault(
            new HttpRequestException(
                $"API enrichissement réponse inattendue : {(int)reponse.StatusCode}"))
    };
}

private async Task<ActivityResult> TraiterReponseAsync(
    HttpResponseMessage response, ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
{
    var complement = await response.Content.ReadFromJsonAsync<ComplementDossier>(ct);
    // ✅ Écriture dans ActivityResult.Next() — les étapes suivantes lisent via GetVariable<T>()
    return ActivityResult.Next(vars =>
    {
        vars["NomBeneficiaire"]     = JsonSerializer.SerializeToElement(complement!.Nom);
        vars["AdresseBeneficiaire"] = JsonSerializer.SerializeToElement(complement.Adresse);
    });
}
```

### `ActivityResult.Fault` — erreur permanente (DLQ)

```csharp
// Cas d'usage : validation métier irréparable
public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
{
    if (string.IsNullOrEmpty(ctx.Arguments.NAS))
    {
        // Données invalides à la source — un retry ne changera rien.
        // Fault → RoutingSlipExecutor :
        //   1. Déclenche les compensateurs en ordre inverse (si enregistrés)
        //   2. Appelle DeadLetterMessageAsync(reason, description)
        //   3. Écrit JournalEntry Mode=DLQ
        //   4. Incrémente routing_slip_completed_total{status=Faulted}
        return ActivityResult.Fault(new ArgumentException("NAS manquant ou invalide"));
    }

    bool admissible = await _service.ValiderAsync(ctx.Arguments.DossierId, ctx.Arguments.NAS, ct);
    if (!admissible)
        return ActivityResult.Fault(
            new InvalidOperationException($"Dossier {ctx.Arguments.DossierId} non admissible"));

    return ActivityResult.Next(vars =>
        vars["DateValidation"] = JsonSerializer.SerializeToElement(DateTime.UtcNow));
}
```

### Retry — politique globale EMT

Le Routing Slip **réutilise exclusivement** la `ExponentialRetryPolicy` globale définie dans `AppSettings.RetryPolicy`. Il n'y a pas de politique de retry spécifique par étape dans le slip — cela simplifierait l'implémentation et évite que des messages forgés puissent contourner les limites de retry opérationnelles.

```json
// AppSettings dans appsettings.json — une seule politique pour toutes les étapes
{
  "AppSettings": {
    "RetryPolicy": {
      "InitialDelay":     "00:00:01",
      "MaxDelay":         "00:05:00",
      "UseJitter":        true
    }
  }
}
```

Le `MaxDeliveryCount` est configuré directement sur l'entité Service Bus (queue ou abonnement de topic) lors du provisionnement par la pipeline DevOps. Le `RoutingSlipExecutor` lit ce compte depuis le broker au moment de l'exécution (`ctx.Attempt`).

### Comportement à la dernière tentative — comment Fault est déclenché sur MaxDeliveryCount

> ⚠️ **Mécanisme important.** Quand `MaxDeliveryCount` est atteint, **Service Bus ne livre plus le message au consumer** — il le déplace directement en DLQ. Le `RoutingSlipExecutor` n'est jamais invoqué pour cette dernière tentative.
>
> La compensation est donc déclenchée **à la (N-1)ème tentative**, pas à la Nème. Le `RoutingSlipExecutor` détecte lors de chaque exécution si `ctx.Attempt >= MaxDeliveryCount - 1` et, si l'activité retourne un `RetryExponential`, convertit silencieusement en `Fault` pour déclencher la compensation avant que Service Bus n'envoie le message en DLQ.

```csharp
// Dans RoutingSlipExecutor.ProcessAsync() — logique interne (simplifiée)
var result = await activity.ExecuteAsync(ctx, ct);

// MaxDeliveryCount est configuré sur l'entité SB par DevOps — le broker fournit la valeur.
// ctx.Attempt est hydraté depuis DeliveryCount du message Service Bus reçu.
// La détection est donc : "à quelle tentative est-on par rapport à la limite SB ?"
bool isLastValidAttempt = ctx.Attempt >= maxDeliveryCountFromBroker - 1;

if (result is RetryResult && isLastValidAttempt)
{
    // Convertir en Fault pour déclencher la compensation AVANT que SB move en DLQ
    _logger.LogWarning(
        "MaxDeliveryCount imminent pour {Step} (attempt={Attempt}) — conversion en Fault pour compensation",
        ctx.StepName, ctx.Attempt);
    result = ActivityResult.Fault(
        new MaxDeliveryCountExceededException(ctx.StepName, ctx.Attempt));
}

// Traitement du résultat final
switch (result)
{
    case NextResult next:
        await AdvanceCursorAndPublishAsync(envelope, next, ct);
        await _actions.CompleteMessageAsync(message, ct);
        break;
    case FaultResult fault:
        await RunCompensatorsInReverseAsync(envelope, fault.Exception, ct);  // ← compensation
        await _actions.DeadLetterMessageAsync(message, fault.Exception.Message,
            fault.Exception.ToString(), ct);
        break;
    case RetryImmediateResult:
        throw new ImmediateRetryException();   // → RetryPolicyHandler → Complete+requeue
    case RetryExponentialResult retry:
        throw new ExponentialRetryException(retry.Reason, retry.InnerException); // → Abandon
}
```

---

## 14. Compensation — annuler ce qui a déjà été fait

### Qu'est-ce que la compensation ?

La compensation est l'opération **inverse** d'une étape déjà complétée. Dans un Routing Slip, si l'étape 3 échoue de façon permanente (`Fault`), les étapes 1 et 2 sont déjà terminées — elles ont peut-être modifié une base de données, envoyé une notification, réservé une ressource. La compensation permet d'**annuler ces effets de bord** de façon contrôlée, en ordre inverse.

> 💡 **Pour un junior — la différence avec une transaction distribuée.** Une transaction 2PC verrouille les ressources jusqu'à ce que tout le monde soit prêt. La compensation est différente : les étapes sont exécutées et validées **une par une**. Si une étape échoue, on n'annule pas de transaction — on exécute des opérations inverses explicites sur ce qui a déjà été fait. C'est le pattern **Saga avec compensation**.

> ⚠️ **Limites importantes de la compensation.**
> - **Best-effort, pas ACID.** Si le compensateur lui-même échoue, l'état peut rester incohérent → alarme + intervention manuelle.
> - **Opérations réversibles uniquement.** Annuler une réservation ✅ — envoyer un chèque physique ❌.
> - **Idempotence obligatoire.** Le compensateur peut être appelé plusieurs fois (retry). "Annuler ce qui n'existe pas" doit être une no-op, jamais une exception.

### Mécanisme de déclenchement — quand et comment

Le `RoutingSlipExecutor` déclenche la compensation dans **deux situations** :

| Situation | Déclencheur | Moment |
|---|---|---|
| `ActivityResult.Fault(ex)` retourné | Activité retourne une erreur permanente | Immédiatement, synchrone |
| `MaxDeliveryCount` atteint | Détecté à la (N-1)ème tentative avant que SB n'envoie en DLQ | À la dernière tentative valide |

> ⚠️ **Point technique important — MaxDeliveryCount et DLQ.** Quand Service Bus atteint `MaxDeliveryCount`, il déplace le message directement en DLQ **sans invocation du consumer**. Le `RoutingSlipExecutor` n'est jamais appelé pour cette Nème tentative. C'est pourquoi la compensation est déclenchée à la **(N-1)ème tentative** : le `RoutingSlipExecutor` convertit silencieusement un `RetryExponential` en `Fault` lorsque `ctx.Attempt >= MaxDeliveryCount - 1`, exécute la compensation, puis dead-letter manuellement. Service Bus reçoit alors un `DeadLetterMessageAsync` explicite plutôt qu'un déplacement automatique.

**Flux de déclenchement :**

```
Étape 3 (EnrichirDossier) — Fault ou MaxDeliveryCount atteint
    │
    └─► RoutingSlipExecutor.RunCompensatorsInReverseAsync(envelope, exception)
            │
            │  Parcourt les Steps en ordre inverse depuis cursor - 1 jusqu'à 0
            │
            ├─ Step[2] (EnrichirDossier, cursor courant) → skip (c'est l'étape en faute)
            ├─ Step[1] (ReserverMontant)   → a un compensateur ?
            │       OUI → AnnulerReservationCompensation.CompensateAsync(ctx, reason, ct)
            │             Journal : Mode=Compensation, Step=ReserverMontant
            │             Métrique : routing_slip_compensations_total{step=ReserverMontant}
            └─ Step[0] (ValiderDemande)    → a un compensateur ? NON → ignoré
            │
            └─► DeadLetterMessageAsync(exception.Message, exception.ToString())
                Journal : Mode=DLQ, Step=EnrichirDossier
                Métrique : routing_slip_completed_total{status=Faulted}
```

### `ICompensationActivity<TArgs>` — l'interface à implémenter

```csharp
namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Compensateur optionnel d'une étape du routing slip.
    /// Déclenché en ordre inverse si une étape ultérieure retourne ActivityResult.Fault()
    /// ou si MaxDeliveryCount est atteint.
    ///
    /// RÈGLE FONDAMENTALE : votre compensateur doit être idempotent.
    /// Il peut être appelé plusieurs fois. "Annuler ce qui n'existe pas" = no-op.
    /// </summary>
    public interface ICompensationActivity<TArgs> where TArgs : class
    {
        /// <param name="ctx">
        /// Contexte de l'activité originale — mêmes Arguments, mêmes Variables.
        /// Les Variables incluent tout ce qui a été accumulé jusqu'à l'étape en faute.
        /// Utilisez GetVariable&lt;T&gt;() pour lire les données posées par des étapes précédentes.
        /// </param>
        /// <param name="failureReason">Message d'erreur de l'étape qui a déclenché la compensation.</param>
        Task CompensateAsync(ActivityContext<TArgs> ctx, string failureReason, CancellationToken ct);
    }
}
```

### Enregistrement — compensateur optionnel par étape

```csharp
// Program.cs — enregistrement avec compensateur sur l'étape ReserverMontant
// target = Target logique dans AppSettings.Endpoints (même mécanique que AddProducer)
services.AddRoutingSlipActivity<ReserverMontantActivity, ReserverArgs>(
    target:      "ReserverMontant",
    compensator: typeof(AnnulerReservationCompensation));
//                ↑ déclaré ici, instancié par DI au moment du déclenchement

// Étape sans compensateur (irréversible) — le paramètre est optionnel
services.AddRoutingSlipActivity<EmettreVirementActivity, EmettreArgs>(
    target: "EmettreVirement");
// ← pas de compensateur : si une étape ultérieure échoue, le virement déjà émis
//   doit être traité manuellement (la compensation n'est pas universelle)
```

### Exemple complet — Demande de remboursement RAMQ

**Scénario :** traitement en 4 étapes avec compensation sur l'étape 2.

```
Valider (step 0)  → Réserver (step 1, + compensateur)  → Enrichir (step 2)  → Émettre (step 3)
                         │                                      │
                         │ si Enrichir échoue (Fault) :         │
                         └──────────────────────────────────────►AnnulerReservation.CompensateAsync()
                                                                  → puis DeadLetter sur queue-enrichir
```

**`ReserverMontantActivity.cs` — l'activité qui sera compensée :**

```csharp
public class ReserverMontantActivity : IRoutingSlipActivity<ReserverArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ReserverArgs> ctx, CancellationToken ct)
    {
        if (ctx.Attempt > 1)
            _logger.LogWarning("Retry {Attempt} — ReserverMontant — demande {Id}",
                ctx.Attempt, ctx.Arguments.DemandeId);

        try
        {
            var reservationId = await _financier.ReserverMontantAsync(
                ctx.Arguments.DemandeId,
                ctx.Arguments.Montant,
                ctx.Arguments.CompteBeneficiaire,
                ct);

            // Propager l'ID de réservation dans les Variables
            // → le compensateur le lira pour savoir quoi annuler
            return ActivityResult.Next(vars =>
                vars["ReservationId"] = JsonSerializer.SerializeToElement(reservationId));
        }
        catch (ServiceFinancierException ex) when (ex.EstTransitoire)
        {
            return ActivityResult.RetryExponential(
                $"Service financier indisponible — demande {ctx.Arguments.DemandeId}", ex);
        }
        catch (Exception ex)
        {
            return ActivityResult.Fault(ex);
        }
    }
}
```

**`AnnulerReservationCompensation.cs` — le compensateur :**

```csharp
/// <summary>
/// Compensateur de l'étape ReserverMontant.
/// Déclenché en ordre inverse si EnrichirDossier ou EmettreVirement échoue.
///
/// Prérequis : ReserverMontantActivity doit avoir propagé "ReservationId" dans Variables.
/// Idempotence : si la réservation est déjà absente, retourne sans erreur.
/// </summary>
public class AnnulerReservationCompensation : ICompensationActivity<ReserverArgs>
{
    private readonly IServiceFinancier _financier;
    private readonly ILogger<AnnulerReservationCompensation> _logger;

    public AnnulerReservationCompensation(
        IServiceFinancier financier,
        ILogger<AnnulerReservationCompensation> logger)
    {
        _financier = financier;
        _logger    = logger;
    }

    public async Task CompensateAsync(
        ActivityContext<ReserverArgs> ctx,
        string failureReason,
        CancellationToken ct)
    {
        // Lire l'ID de réservation posé par l'activité via GetVariable<T>()
        // ✅ CORRECT — utiliser GetVariable<T>() après round-trip JSON
        // ❌ INTERDIT — (string)ctx.Variables["ReservationId"] → InvalidCastException
        var reservationId = ctx.GetVariable<string>("ReservationId");

        if (reservationId is null)
        {
            // L'activité n'a peut-être jamais atteint la réservation
            _logger.LogWarning(
                "SlipId={SlipId} Step={Step} — ReservationId absent dans Variables, " +
                "compensation no-op (l'activité n'a probablement pas complété la réservation)",
                ctx.SlipId, ctx.StepName);
            return;
        }

        // Vérification idempotente : la réservation existe-t-elle encore ?
        var existe = await _financier.ReservationExisteAsync(reservationId, ct);
        if (!existe)
        {
            _logger.LogInformation(
                "SlipId={SlipId} — Réservation {Id} déjà absente — compensation no-op",
                ctx.SlipId, reservationId);
            return;
        }

        await _financier.AnnulerReservationAsync(reservationId, failureReason, ct);

        _logger.LogInformation(
            "SlipId={SlipId} — Réservation {Id} annulée. Raison : {Raison}",
            ctx.SlipId, reservationId, failureReason);
    }
}
```

### Que se passe-t-il si le compensateur lui-même échoue ?

```
AnnulerReservationCompensation.CompensateAsync() → Exception
    │
    ├─ Cause possible : DB indisponible, timeout réseau, service financier en panne
    │
    ├─ RoutingSlipExecutor log l'erreur avec contexte structuré :
    │     { slip_id, step=ReserverMontant, phase=Compensation, exception_type, message }
    │
    ├─ Métrique : routing_slip_compensation_failures_total{step=ReserverMontant}
    │
    ├─ Le slip est quand même dead-lettered dans la queue de l'étape en faute.$dlq
    │   La compensation échouée ne bloque pas le DLQ du slip principal.
    │   Raison DLQ = "CompensationFailed + " + exception originale
    │
    └─ ALERTE OPÉRATIONNELLE HAUTE SÉVÉRITÉ → intervention manuelle requise
         État de la ressource : AMBIGU
         Exemple : la réservation financière est peut-être encore active
                   bien que le dossier soit en DLQ.
         Actions possibles :
           1. Vérifier et annuler manuellement la réservation (via back-office)
           2. Rejouer le message en DLQ après rétablissement du service
              (le compensateur étant idempotent, il gère une double exécution)
           3. Investiguer dans Application Insights via slip_id
```

> ⚠️ **Règle opérationnelle critique.** Tout échec de compensateur doit déclencher une alerte de sévérité HAUTE. L'équipe doit traiter ces alertes comme des incidents en cours — pas comme de simples erreurs de log.

---

## 15. Exemple complet bout-en-bout avec retry et compensation

### Scénario — Traitement d'une demande de remboursement RAMQ

**Contexte :** un bénéficiaire soumet une demande de remboursement. Le traitement passe par 4 étapes :
1. **ValiderDemande** — validation des règles d'admissibilité
2. **ReserverMontant** — réservation du montant dans le système financier (réversible)
3. **EnrichirDossier** — appel à une API tierce pour compléter le dossier (indisponible parfois)
4. **EmettreVirement** — émission du virement bancaire (irréversible — pas de compensateur)

### Structure du projet

```
RAMQ.Remboursement/
├── Activateur/
│   └── DemandeActivateur.cs          ← HTTP Trigger : construit et publie le slip
├── Activities/
│   ├── ValiderDemandeActivity.cs     ← étape 1 : validation métier
│   ├── ReserverMontantActivity.cs    ← étape 2 : réservation (+ compensateur)
│   ├── EnrichirDossierActivity.cs    ← étape 3 : appel API tierce (retry expo)
│   └── EmettreVirementActivity.cs    ← étape 4 : virement (sans compensateur)
├── Compensateurs/
│   └── AnnulerReservationCompensation.cs  ← annule l'étape 2 si étape 3 ou 4 échoue
├── Args/
│   ├── ValiderArgs.cs  ReserverArgs.cs  EnrichirArgs.cs  EmettreArgs.cs
└── Program.cs
```

### `Program.cs` — enregistrement complet (target logique + config centralisée)

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        // ── Configuration EMT — lié à AppSettings.Endpoints dans appsettings.json ──
        services.Configure<AppSettings>(ctx.Configuration.GetSection("AppSettings"));
        services.Configure<BlobStorageSetting>(ctx.Configuration.GetSection("BlobStorageSetting"));
        services.AddSingleton<ConsumerConfigurationService>();
        services.AddSingleton<IMessageTransitConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());
        services.AddSingleton<IConsumerConfigurationService>(
            sp => sp.GetRequiredService<ConsumerConfigurationService>());

        services.ConfigureAzureProviders();

        // ── Activités — target logique = Target dans Itinerary ──────────────
        // EntityName résolu par IEndpointResolver depuis AppSettings.Endpoints
        // Étape 1 — Validation : retry immédiat, pas de compensateur
        services.AddRoutingSlipActivity<ValiderDemandeActivity, ValiderArgs>(
            target: "ValiderDemande");

        // Étape 2 — Réservation : avec compensateur
        services.AddRoutingSlipActivity<ReserverMontantActivity, ReserverArgs>(
            target:      "ReserverMontant",
            compensator: typeof(AnnulerReservationCompensation));

        // Étape 3 — API tierce : retry exponentiel
        services.AddRoutingSlipActivity<EnrichirDossierActivity, EnrichirArgs>(
            target: "EnrichirDossier");

        // Étape 4 — Virement : pas de compensateur (irréversible)
        services.AddRoutingSlipActivity<EmettreVirementActivity, EmettreArgs>(
            target: "EmettreVirement");

        // Compensateurs
        services.AddScoped<AnnulerReservationCompensation>();
    })
    .Build();
```

> 💡 La `RetryPolicy` globale de `AppSettings.RetryPolicy` s'applique uniformément à toutes les étapes. Il n'y a pas de politique par étape dans le slip.

**`appsettings.json` du worker — source de vérité des endpoints connus de ce worker :**

```json
{
  "AppSettings": {
    "ServiceBusNamespace":          "mon-namespace.servicebus.windows.net",
    "ApplicationName":              "RemboursementWorker",
    "EnableJsonIndentation":        false,
    "MessageTransitJournalName":    "COMJournalAIS",
    "MessageTransitJournalStoreUri": "https://storageramq.table.core.windows.net/",
    "Endpoints": [
      { "Target": "ValiderDemande",   "Endpoint": { "EntityName": "queue-valider-demande",   "EntityType": "Queue" } },
      { "Target": "ReserverMontant",  "Endpoint": { "EntityName": "queue-reserver-montant",  "EntityType": "Queue" } },
      { "Target": "EnrichirDossier",  "Endpoint": { "EntityName": "queue-enrichir-dossier",  "EntityType": "Queue" } },
      { "Target": "EmettreVirement",  "Endpoint": { "EntityName": "queue-emettre-virement",  "EntityType": "Queue" } }
    ]
  }
}
```

**`local.settings.json` — aliases triggers :**

```json
{
  "Values": {
    "ServiceBusConnection__fullyQualifiedNamespace": "mon-namespace.servicebus.windows.net",
    "ValiderDemande_Queue":  "queue-valider-demande",
    "ReserverMontant_Queue": "queue-reserver-montant",
    "EnrichirDossier_Queue": "queue-enrichir-dossier",
    "EmettreVirement_Queue": "queue-emettre-virement"
  }
}
```

### `DemandeActivateur.cs` — publication du slip (target logique)

```csharp
[Function("DemarrerRemboursement")]
public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
{
    var demande = await req.ReadFromJsonAsync<DemandeRemboursement>();

    // RoutingSlipBuilder résout les EntityName depuis AppSettings.Endpoints de l'activateur
    var slip = new RoutingSlipBuilder("TraiterRemboursement", _endpointResolver)
        .AddStep<ValiderArgs>(
            stepName: "ValiderDemande",
            args:     new ValiderArgs { DemandeId = demande.Id, NAS = demande.NAS,
                                        MontantDemande = demande.Montant })
        .AddStep<ReserverArgs>(
            stepName: "ReserverMontant",
            args:     new ReserverArgs { DemandeId = demande.Id, Montant = demande.Montant,
                                          CompteBeneficiaire = demande.CompteBancaire })
        .AddStep<EnrichirArgs>(
            stepName: "EnrichirDossier",
            args:     new EnrichirArgs { DemandeId = demande.Id, NAS = demande.NAS })
        .AddStep<EmettreArgs>(
            stepName: "EmettreVirement",
            args:     new EmettreArgs  { DemandeId = demande.Id })
        .Build();

    // Target = nom logique de la première étape — IEndpointResolver résout vers "queue-valider-demande"
    await _producer.PublishAsync(
        new MessageTransitContext<SlipEnvelope>
        {
            MessageId = slip.Header.SlipId,
            Message   = slip
        },
        new PublishOptions { Target = "ValiderDemande" }, // ← Target logique, résolu par IEndpointResolver
        cancellationToken);

    return new AcceptedResult("", new { SlipId = slip.Header.SlipId });
}
```

### `ValiderDemandeActivity.cs` — étape 1

```csharp
public class ValiderDemandeActivity : IRoutingSlipActivity<ValiderArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        // Erreur permanente — règle métier
        if (ctx.Arguments.MontantDemande <= 0)
            return ActivityResult.Fault(
                new ArgumentException($"Montant invalide : {ctx.Arguments.MontantDemande}"));

        bool admissible;
        try
        {
            admissible = await _serviceAdmissibilite.ValiderAsync(ctx.Arguments.NAS, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Contention DB : retry immédiat
            return ActivityResult.RetryImmediate("Concurrence DB sur vérification admissibilité");
        }

        if (!admissible)
            return ActivityResult.Fault(
                new InvalidOperationException($"NAS {ctx.Arguments.NAS} non admissible"));

        return ActivityResult.Next(vars =>
            vars["DateValidation"] = JsonSerializer.SerializeToElement(DateTime.UtcNow));
    }
}
```

### `ReserverMontantActivity.cs` — étape 2 (avec compensateur)

```csharp
public class ReserverMontantActivity : IRoutingSlipActivity<ReserverArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ReserverArgs> ctx, CancellationToken ct)
    {
        try
        {
            var reservationId = await _financier.ReserverMontantAsync(
                ctx.Arguments.DemandeId,
                ctx.Arguments.Montant,
                ctx.Arguments.CompteBeneficiaire,
                ct);

            // Propager l'ID de réservation pour que le compensateur puisse l'utiliser
            // ✅ OBLIGATOIRE : JsonSerializer.SerializeToElement — les Variables sont des JsonElement
            return ActivityResult.Next(vars =>
                vars["ReservationId"] = JsonSerializer.SerializeToElement(reservationId));
        }
        catch (ServiceFinancierException ex) when (ex.EstTransitoire)
        {
            // Système financier temporairement indisponible
            return ActivityResult.RetryExponential(
                $"Service financier indisponible — demande {ctx.Arguments.DemandeId}", ex);
        }
        catch (Exception ex)
        {
            // Erreur inconnue permanente
            return ActivityResult.Fault(ex);
        }
    }
}
```

### `AnnulerReservationCompensation.cs` — compensateur de l'étape 2

```csharp
public class AnnulerReservationCompensation : ICompensationActivity<ReserverArgs>
{
    public async Task CompensateAsync(ActivityContext<ReserverArgs> ctx,
                                      string failureReason, CancellationToken ct)
    {
        // Récupérer l'ID de réservation posé par l'activité en step 2
        // (propagé dans Variables par ActivityResult.Next())
        // ✅ CORRECT — utiliser GetVariable<T>() après round-trip JSON
        // ❌ INTERDIT — (string)ctx.Variables["ReservationId"] → InvalidCastException
        var reservationId = ctx.GetVariable<string>("ReservationId");
        if (reservationId is null)
        {
            // Pas de réservation à annuler (step 2 n'a peut-être pas atteint la réservation)
            _logger.LogWarning("Aucune réservation trouvée pour {DemandeId} — compensation no-op",
                ctx.Arguments.DemandeId);
            return;
        }

        // Idempotent : si déjà annulé, ne pas lever d'exception
        var existe = await _financier.ReservationExisteAsync(reservationId, ct);
        if (!existe)
        {
            _logger.LogInformation("Réservation {Id} déjà absente", reservationId);
            return;
        }

        await _financier.AnnulerReservationAsync(reservationId, failureReason, ct);
        _logger.LogInformation("Réservation {Id} annulée. Raison : {Raison}",
            reservationId, failureReason);
    }
}
```

### `EnrichirDossierActivity.cs` — étape 3 (retry exponentiel)

```csharp
public class EnrichirDossierActivity : IRoutingSlipActivity<EnrichirArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(
                $"/api/dossiers/{ctx.Arguments.NAS}/complement", ct);
        }
        catch (HttpRequestException ex)
        {
            // Réseau indisponible — retry exponentiel
            return ActivityResult.RetryExponential("Réseau indisponible — API enrichissement", ex);
        }

        return response.StatusCode switch
        {
            // 503 / 429 : service surchargé → retry exponentiel
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.TooManyRequests =>
                ActivityResult.RetryExponential(
                    $"API enrichissement {(int)response.StatusCode} — demande {ctx.Arguments.DemandeId}"),

            // 200 : succès
            System.Net.HttpStatusCode.OK => await TraiterReponseAsync(response, ctx, ct),

            // Autre : erreur permanente (4xx, 5xx inconnu)
            _ => ActivityResult.Fault(
                new HttpRequestException(
                    $"API enrichissement réponse inattendue : {(int)response.StatusCode}"))
        };
    }

    private async Task<ActivityResult> TraiterReponseAsync(
        HttpResponseMessage response, ActivityContext<EnrichirArgs> ctx, CancellationToken ct)
    {
        var complement = await response.Content.ReadFromJsonAsync<ComplementDossier>(ct);
        return ActivityResult.Next(vars =>
        {
            // ✅ OBLIGATOIRE : JsonSerializer.SerializeToElement pour stocker dans Variables
            vars["NomBeneficiaire"]     = JsonSerializer.SerializeToElement(complement!.Nom);
            vars["AdresseBeneficiaire"] = JsonSerializer.SerializeToElement(complement.Adresse);
        });
    }
}
```

### `EmettreVirementActivity.cs` — étape 4 (irréversible, sans compensateur)

```csharp
public class EmettreVirementActivity : IRoutingSlipActivity<EmettreArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<EmettreArgs> ctx, CancellationToken ct)
    {
        // Lire les variables enrichies par les étapes précédentes
        // ✅ CORRECT — GetVariable<T>() après round-trip JSON (les Variables sont des JsonElement)
        var nom     = ctx.GetVariable<string>("NomBeneficiaire") ?? "Inconnu";
        var adresse = ctx.GetVariable<string>("AdresseBeneficiaire");

        // Émettre le virement — opération irréversible
        // Idempotence garantie par le MessageId = SlipId (DeduplicateDetection Service Bus)
        var virementId = await _virements.EmettreAsync(
            demandeId:   ctx.Arguments.DemandeId,
            messageId:   ctx.SlipId, // ← clé d'idempotence : même slip = même virement
            nom:         nom,
            adresse:     adresse,
            ct);

        _logger.LogInformation(
            "Virement {VirementId} émis pour demande {DemandeId} — slip {SlipId}",
            virementId, ctx.Arguments.DemandeId, ctx.SlipId);

        // Dernière étape — ActivityResult.Next() et ActivityResult.Complete() sont équivalents ici
        return ActivityResult.Complete();
    }
}
```

### Flux complet — scénario heureux

```
HTTP POST /demarrer-remboursement
    │
    └─► DemandeActivateur
            │ RoutingSlipBuilder("TraiterRemboursement", _endpointResolver)
            │   .AddStep("ValiderDemande", ...)      → EntityName écrit dans le SlipStep depuis Endpoints
            │   .AddStep("ReserverMontant", ...)
            │   .AddStep("EnrichirDossier", ...)
            │   .AddStep("EmettreVirement", ...)
            └─► IMessageProducer → target: "ValiderDemande" → queue-valider-demande

[queue-valider-demande]   → trigger: %ValiderDemande_Queue%
    └─► ValiderDemandeActivity → Next() — vars["DateValidation"]
            └─► RoutingSlipExecutor : Cursor 0→1, publier → queue-reserver-montant

[queue-reserver-montant]  → trigger: %ReserverMontant_Queue%
    └─► ReserverMontantActivity → Next() — vars["ReservationId"]
            └─► RoutingSlipExecutor : Cursor 1→2, publier → queue-enrichir-dossier

[queue-enrichir-dossier]  → trigger: %EnrichirDossier_Queue%
    └─► EnrichirDossierActivity → Next() — vars["NomBeneficiaire"], vars["AdresseBeneficiaire"]
            └─► RoutingSlipExecutor : Cursor 2→3, publier → queue-emettre-virement

[queue-emettre-virement]  → trigger: %EmettreVirement_Queue%
    └─► EmettreVirementActivity → Complete()
            └─► RoutingSlipExecutor : CompleteMessageAsync() ✅ slip terminé
```

### Flux — scénario API tierce indisponible (retry exponentiel sur étape 3)

```
[queue-enrichir-dossier]  trigger: %EnrichirDossier_Queue%  — tentative 1
    └─► EnrichirDossierActivity → API 503
            │ ReturnVal : RetryExponential("API 503")
            └─► RoutingSlipExecutor → ExponentialRetryException
                    └─► RetryPolicyHandler → Abandon
                            └─► Service Bus : attente InitialDelay×2^1 + jitter (≈ 4s)

[queue-enrichir-dossier] — tentative 2
    └─► EnrichirDossierActivity → API 503
            └─► Abandon, attente ≈ 8s

... (jusqu'à MaxDeliveryCount=10)

[queue-enrichir-dossier] — tentative 9 (MaxDeliveryCount-1)
    └─► EnrichirDossierActivity → API 503
            │ RetryExponential — mais ctx.Attempt >= MaxDeliveryCount-1 ?
            └─► RoutingSlipExecutor : OUI → convertit en Fault (voir §14)
                    │
                    ├─► Déclenche compensateur AnnulerReservationCompensation (étape 2)
                    │       └─► GetVariable<string>("ReservationId") → financier.AnnulerReservationAsync()
                    │
                    └─► DeadLetterMessageAsync() — AVANT que SB n'atteigne tentative 10
                        Alerte : routing_slip_completed_total{status=Faulted, step=EnrichirDossier}

    [NOTE] La tentative 10 (MaxDeliveryCount) n'est JAMAIS exécutée par le consumer —
           Service Bus la déplace automatiquement en DLQ sans invocation. C'est pourquoi
           la compensation est déclenchée à N-1 (voir §14 — Mécanisme de déclenchement).
```

### Flux — scénario fault immédiat (données invalides sur étape 1)

```
[queue-valider-demande]  trigger: %ValiderDemande_Queue%  — tentative 1
    └─► ValiderDemandeActivity → Montant = 0
            │ ReturnVal : Fault(ArgumentException)
            └─► RoutingSlipExecutor
                    │
                    ├─► Aucun compensateur à l'étape 0 (Valider) → ignoré
                    │   (Les étapes 1, 2, 3 n'ont pas encore été exécutées)
                    │
                    └─► DeadLetterMessageAsync() → queue-valider-demande.$dlq
```

### Test unitaire du scénario avec compensateur

```csharp
[Fact]
public async Task SlipRemboursement_Etape3Fault_DeclenchementsCompensateur()
{
    // Arrange
    var financierMock   = new FinancierServiceStub();
    var inMemoryAdapter = new InMemoryMessagingAdapter();
    var reservationId   = "res-001";

    // Activité étape 2 : réussit et pose ReservationId dans Variables
    var reserver = new ReserverMontantActivity(financierMock);

    // Activité étape 3 : simule une erreur permanente
    var enrichir = new EnrichirDossierActivity(
        httpClient: HttpClientStubFactory.AlwaysFault(503));

    // Compensateur étape 2
    var compensateur = new AnnulerReservationCompensation(financierMock);

    var executor = RoutingSlipTestBuilder
        .ForSlip("TraiterRemboursement")
        .AddStep(reserver,     compensator: compensateur)
        .AddStep(enrichir)
        .BuildExecutor(inMemoryAdapter);

    // Act
    await executor.RunToFaultAsync(cancellationToken: default);

    // Assert
    Assert.True(financierMock.ReservationAnnulee,
        "Le compensateur aurait dû annuler la réservation");
    Assert.Empty(inMemoryAdapter.Published); // aucun message publié au-delà du slip
    Assert.Single(inMemoryAdapter.DeadLettered);
}
```

---

## 16. Migration depuis le design actuel

### Breaking change MAJOR — version 2.0

Cette phase introduit un **breaking change assumé** sur les anciens consumers multi-étapes. Il s'agit d'un bump de version **MAJOR (v2.0)**. Les équipes consommatrices doivent migrer explicitement avant d'adopter v2.0 — les consumers existants continueront de fonctionner tant qu'ils restent sur la branche v1.x.

> 💡 **Pour un junior — pourquoi accepter ce breaking change ?** Un breaking change déclaré et documenté est préférable à un refactor silencieux. Les applications clientes reçoivent une erreur de compilation claire, pas un bug comportemental découvert en production. La migration est mécanique et outillée.

**Ce qui est supprimé en v2.0 :**

| Supprimé | Remplacé par | Impact |
|----------|-------------|--------|
| `BaseConsumer<T>.RouteToNextStageAsync()` | `IRoutingSlipActivity<TArgs>` + `RoutingSlipExecutor` | **Breaking** — héritage actif de `BaseConsumer<T>` avec routing multi-étapes |
| `AppSettings.Itinerary` multi-étapes (v1.x) | `AppSettings.Endpoints` + `RoutingSlipBuilder` côté activateur | **Breaking** — propriété renommée, responsabilité déplacée vers le slip |
| `MessageTransitContext.CurrentStage` (usage saga) | `SlipEnvelope.Cursor` | **Breaking** — lecture/écriture directe du champ `CurrentStage` |
| `Variables["__FinalStageCompleted"]` | Détection automatique `Cursor == Steps.Length` | **Breaking** — écriture ou lecture de cette clé réservée |
| `FindIndexFromStage` / `ResolveEffectiveCurrentStage` | Curseur entier O(1) | Supprimé de la surface publique |

**Ce qui reste inchangé :**

- `BaseConsumer<T>` pour les consumers **simples** (non-saga, une seule étape) — désérialisation + settlement uniquement
- `IMessageProducer<T>`, `IMessageTransit`, `IMessagingProvider` — aucun impact
- `AppSettings` de base (`ServiceBusNamespace`, `ApplicationName`, `MessageTransitJournalName`, `MessageTransitJournalStoreUri`, etc.)
- Claim-check, circuit breaker, journal, retry, métriques — tous inchangés et transparents
- Consumers point-à-point existants qui n'utilisent pas `RouteToNextStageAsync` — **aucune modification requise**

### Quand utiliser quoi

| Situation | Approche recommandée |
|-----------|---------------------|
| Nouveau workflow multi-étapes | `IRoutingSlipActivity<TArgs>` ✅ |
| Consumer existant isolé (une seule étape, sans routing) | `BaseConsumer<T>` — aucun changement requis ✅ |
| Workflow existant multi-étapes (avec `RouteToNextStageAsync`) | **Migration requise** vers `IRoutingSlipActivity<TArgs>` ⚠️ |

### Comment migrer un consumer existant (migration obligatoire en v2.0)

Les consumers qui utilisent `RouteToNextStageAsync()` **doivent** migrer vers `IRoutingSlipActivity<TArgs>`. Voici la transformation exacte à appliquer.

**Avant (v1.x) — consumer couplé au routing :**

```csharp
// ValiderConsumer.cs — design v1.x (à migrer)
public class ValiderConsumer : BaseConsumer<DossierMessage>
{
    public override async Task<MessageTransitContext<MessageTransitResponse>> ConsumeAsync(
        MessageTransitContext<DossierMessage> context,
        CancellationToken ct)
    {
        // Logique métier
        await _serviceValidation.ValiderAsync(context.Message.DossierId);

        // Le consumer sait qu'il doit router vers "EnrichirConsumer"
        await RouteToNextStageAsync(context, "EnrichirConsumer",
            ctx => new EnrichirMessage { DossierId = ctx.Message.DossierId }, ct);

        return new MessageTransitContext<MessageTransitResponse>();
    }
}
```

**Après (v2.0) — activité pure :**

```csharp
// ValiderActivity.cs — design v2.0
public class ValiderActivity : IRoutingSlipActivity<ValiderArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        // Exactement la même logique métier
        await _serviceValidation.ValiderAsync(ctx.Arguments.DossierId);

        // Le routing est géré par le framework — vous ne savez pas quelle est l'étape suivante
        return ActivityResult.Next();
    }
}
```

**Et côté activateur (v2.0) :**

```csharp
// v1.x : l'itinéraire était dans appsettings.json — global, implicite, fragile
// v2.0 : l'activateur déclare l'itinéraire complet dans SON propre appsettings.json
//        et RoutingSlipBuilder résout les EntityName via IEndpointResolver
//        (depuis la configuration centralisée d'EMT)

// appsettings.json de l'activateur v2.0 :
// "Endpoints": [
//   { "Target": "Valider",  "Endpoint": { "EntityName": "queue-valider",  "EntityType": "Queue" } },
//   { "Target": "Enrichir", "Endpoint": { "EntityName": "queue-enrichir", "EntityType": "Queue" } }
// ]

var slip = new RoutingSlipBuilder("TraiterDossier", _endpointResolver)
    .AddStep<ValiderArgs> ("Valider",  new ValiderArgs  { DossierId = id })
    .AddStep<EnrichirArgs>("Enrichir", new EnrichirArgs { DossierId = id })
    .Build();
// ↑ IEndpointResolver résout "Valider" → EntityName="queue-valider" (depuis Endpoints)
//   Aucun nom physique d'entité dans le code C#
```

### Tableau de mapping — ancienne config v1.x → nouveau builder v2.0

> ⚠️ **Breaking change** : la propriété `AppSettings.Itinerary` (v1.x) est **renommée `AppSettings.Endpoints`** en v2.0 pour reflecter sa vraie sémantique : une liste de points de terminaison connus de l'application, et non un itinéraire global de workflow. En v2.0, chaque application consumer ne déclare que les endpoints dont elle a besoin — l'itinéraire complet est porté par le `SlipEnvelope`.

Voici la correspondance exacte entre les propriétés `appsettings.json` v1.x et le `RoutingSlipBuilder` v2.0 :

| `appsettings.json` (ancienne config) | `RoutingSlipBuilder` (nouveau design) | Notes |
|--------------------------------------|---------------------------------------|-------|
| `Itinerary[i].Target` (v1.x) | `AddStep(stepName: "...")` | `stepName` == `Target` == nom logique de l'étape |
| `Itinerary[i].Endpoint.EntityName` (v1.x) | Config activateur `Endpoints[i].Endpoint.EntityName` → résolu via `IEndpointResolver` | EntityName jamais dans le code C#, toujours dans la config |
| `Itinerary[i].Endpoint.EntityType` (v1.x) | Résolu depuis `Endpoints[i].Endpoint.EntityType` | Queue ET Topic supportés |
| `Itinerary[i].Endpoint.Subscription.Consumer` (v1.x) | Résolu depuis `Endpoints[i].Endpoint.Subscription.Consumer` via `IEndpointResolver` | Publié comme Application Property Service Bus par `RoutingSlipExecutor` |
| `Itinerary[i].Endpoint.Subscription.Action` (v1.x) | Résolu depuis `Endpoints[i].Endpoint.Subscription.Action` via `IEndpointResolver` | Idem |
| `Itinerary[i].Endpoint.TTL` | `AddStep(..., options: new StepOptions { TTL = ... })` | Optionnel — surcharge par étape |
| `Itinerary[i].Endpoint.PublishTimeout` | `AddStep(..., options: new StepOptions { PublishTimeout = ... })` | Optionnel — surcharge par étape |
| `AppSettings.ApplicationName` | `new RoutingSlipBuilder(slipName: "...")` | Devient le nom du workflow dans les métriques/logs |
| _(absent — globale par app)_ | Chaque appel `RoutingSlipBuilder` est indépendant | **Avantage** : plusieurs workflows par app |

### Exemple complet de migration — appsettings.json v1.x vers RoutingSlipBuilder v2.0

**Config v1.x (section `Itinerary[]` supprimée en v2.0) :**

```json
{
  "ConsumerConfiguration": {
    "ServiceBusNamespace": "ramq-dev.servicebus.windows.net",
    "ApplicationName": "TraiterDossierApp",
    "MessageTransitJournalName": "journal",
    "MessageTransitJournalStoreUri": "https://storageramq.table.core.windows.net/",
    "Itinerary": [
      {
        "Target": "ValiderAdmissibilite",
        "Endpoint": { "EntityName": "queue-valider", "EntityType": "Queue" }
      },
      {
        "Target": "EnrichirDonnees",
        "Endpoint": {
          "EntityName":   "topic-enrichir",
          "EntityType":   "Topic",
          "Subscription": { "Consumer": "EnrichirConsumer", "Action": "Traiter" }
        }
      },
      {
        "Target": "NotifierBeneficiaire",
        "Endpoint": { "EntityName": "queue-notifier", "EntityType": "Queue" }
      }
    ]
  }
}
```

**Équivalent en RoutingSlipBuilder (dans l'activateur) :**

```csharp
// v2.0 : le code C# ne contient AUCUN nom physique d'entité Service Bus.
// Les EntityName sont dans l'appsettings.json de l'activateur (clé Endpoints[]).
// IEndpointResolver résout chaque Target (= stepName) vers son EndpointSettings.

var slip = new RoutingSlipBuilder("TraiterDossier", _endpointResolver)  // ← AppSettings.ApplicationName
    .AddStep<ValiderArgs>  (stepName: "ValiderAdmissibilite", args: new ValiderArgs  { ... })
    // ↑ IEndpointResolver.TryResolve("ValiderAdmissibilite") → EntityName="queue-valider"
    .AddStep<EnrichirArgs> (stepName: "EnrichirDonnees",      args: new EnrichirArgs { ... })
    // ↑ IEndpointResolver.TryResolve("EnrichirDonnees")      → EntityName="topic-enrichir"
    //                                                          + Subscription{Consumer, Action}

---

### Scénarios de déploiement continu (rolling deployment)

Le rolling deployment est l'une des situations les plus délicates en messaging. Avec l'ancien design (`AppSettings.Itinerary`), c'était une source **silencieuse** de bugs (section 4.1). Voici comment le nouveau design les gère — et pourquoi il est fondamentalement plus robuste.

#### Règle fondamentale : un slip est immuable après sa création

Un `SlipEnvelope` est construit par l'activateur au moment de la publication. Il contient l'itinéraire **complet et figé** : toutes les étapes, dans l'ordre, avec leurs arguments. Aucune application consumer n'a besoin de connaître l'itinéraire global — elle exécute simplement l'étape courante (cursor) et laisse le `RoutingSlipExecutor` avancer.

**Conséquence directe :** changer une application consumer n'affecte pas les slips déjà en vol. Un slip créé avec l'activateur v1 sera traité correctement par les workers v2, même pendant le rolling deployment.

---

#### Scénario A — Modification de la logique métier d'une activité (TArgs inchangé)

C'est le cas le plus fréquent et le plus simple.

```
Worker ValiderDemandeActivity v1 → v2 (nouvelle règle de validation)

Slips en vol (créés avant le déploiement) :
→ Traités par v1 ou v2 selon quelle instance les reçoit
→ Aucune différence dans le SlipEnvelope — seule la logique interne change
→ Risque : AUCUN (si TArgs n'a pas changé)

Slips créés après le déploiement :
→ Toujours traités par v2
```

**Règle clé :** si `TArgs` (les arguments) ne change pas, le rolling deployment est entièrement transparent. Le slip ne sait pas quelle version de l'activité l'a traité.

---

#### Scénario B — Ajout d'une étape à la fin du slip

```
Avant (activateur v1) :     Après (activateur v2) :
 1. Valider                   1. Valider
 2. Réserver                  2. Réserver
 3. Émettre                   3. Émettre
                              4. Archiver  ← nouvelle étape ajoutée

Slips en vol créés par v1 (3 étapes) :
→ Se terminent normalement à l'étape 3
→ L'étape 4 n'existe pas dans ces slips → aucun impact

Slips créés par v2 (4 étapes) :
→ Arrivent en queue-archiver après l'étape 3
→ Le worker "ArchivalActivity" doit être déployé AVANT l'activateur v2

Ordre de déploiement obligatoire :
  1️⃣ Déployer le worker ArchivalActivity (écoute queue-archiver)
  2️⃣ Déployer l'activateur v2 (produit des slips à 4 étapes)
  → Fenêtre d'incohérence : AUCUNE
```

---

#### Scénario C — Rolling deployment de l'activateur (deux versions en parallèle)

C'est exactement le problème documenté en section 4.1, **résolu** par le nouveau design.

```
Instance A de l'activateur (ancienne)        Instance B de l'activateur (nouvelle)
─────────────────────────────────────        ──────────────────────────────────────
Produit des slips avec itinéraire v1 :       Produit des slips avec itinéraire v2 :
  1. Valider                                   1. Valider
  2. Enrichir                                  2. Enrichir
  3. Émettre                                   3. Archiver  ← étape ajoutée
                                               4. Émettre   ← ordre changé

═══════════════════════════════════════════════════════════════════
  ANCIEN DESIGN (appsettings.Itinerary) — PROBLÈME :
  Les workers lisaient l'itinéraire depuis leur config locale.
  → Un message créé par Instance A peut être reçu par un worker configuré v2
  → Ce worker cherche "Archiver" après "Enrichir" → mauvais routing
  → Erreur SILENCIEUSE, détectée en production seulement

  NOUVEAU DESIGN (SlipEnvelope) — SAFE :
  L'itinéraire voyage AVEC le message. Un slip créé par Instance A porte
  l'itinéraire v1 BAKED IN. Il sera traité selon v1 du début à la fin,
  peu importe quelle instance de worker le reçoit à chaque étape.
  Un slip créé par Instance B porte l'itinéraire v2. Idem.
  → Pas de croisement possible entre les deux itinéraires.
  → Rolling deployment de l'activateur est safe par construction.
═══════════════════════════════════════════════════════════════════
```

---

#### Scénario D — Changement des arguments d'une étape (compatibilité TArgs)

Les arguments (`TArgs`) sont sérialisés **dans le `SlipEnvelope`** au moment de la création du slip. Un slip créé par l'activateur v1 a les anciens arguments baked in. Le worker v2 doit pouvoir les désérialiser.

```
SlipEnvelope (créé par activateur v1) :
  steps[1].args = { "demandeId": "D-001", "compte": "CAD-001" }

ReserverArgs v2 (dans le nouveau worker) :
  public class ReserverArgs
  {
      public string DemandeId { get; set; }    // ← inchangé ✅
      public string? Compte { get; set; }       // ← inchangé ✅
      public string? Devise { get; set; }       // ← NOUVEAU (nullable) ✅
      // "devise" absent dans v1 → désérialisé comme null → acceptable
  }
```

**Règles de compatibilité des arguments `TArgs` :**

| Changement | Compatible avec les slips en vol ? | Stratégie |
|---|---|---|
| Ajouter une propriété nullable | ✅ Oui | Valeur `null` pour les slips créés avant le déploiement |
| Ajouter une propriété avec valeur par défaut | ✅ Oui | `[JsonPropertyName]` + valeur par défaut dans le setter |
| Renommer une propriété (ancienne → nouvelle) | ⚠️ Avec précaution | Garder `[JsonPropertyName("ancienNom")]` pendant 1 cycle de déploiement |
| Supprimer une propriété | ⚠️ Avec précaution | La propriété devient ignorée (ok) — supprimer seulement si plus lue |
| Changer le type d'une propriété (ex. `string` → `Guid`) | ❌ Breaking | Créer un **nouveau stepName** — ne pas recycler l'ancien |
| Renommer la propriété sans alias JSON | ❌ Breaking | Toujours utiliser `[JsonPropertyName]` sur les TArgs |

**Stratégie de renommage sécurisé :**
```csharp
public class ReserverArgs
{
    // Cycle de déploiement 1 : garder l'ancien nom JSON pour les slips en vol
    [JsonPropertyName("compte")]          // ← nom JSON v1 (slips en vol le lisent ici)
    public string? CompteV1 { get; set; } // propriété transitoire

    [JsonPropertyName("iban")]            // ← nom JSON v2 (nouveaux slips)
    public string? Iban { get; set; }

    // Le code de l'activité lit les deux :
    // var iban = ctx.Arguments.Iban ?? ctx.Arguments.CompteV1;

    // Cycle de déploiement 2 (après épuisement des slips v1) :
    // → Supprimer CompteV1, garder seulement Iban
}
```

---

#### Scénario E — Suppression d'une étape

```
Avant (activateur v1) :     Après (activateur v2) :
 1. Valider                   1. Valider
 2. Vérifier          ←       2. Émettre   (Vérifier supprimé)
 3. Émettre

Slips en vol créés par v1 :
→ Contiennent l'étape "Vérifier" → le worker VérifierActivity doit rester actif
  jusqu'à ce que TOUS les slips v1 en vol soient terminés (ou en DLQ)

Ordre de déploiement recommandé :
  1️⃣ Déployer l'activateur v2 (arrête de créer des slips avec "Vérifier")
  2️⃣ Attendre que la métrique routing_slip_step_duration_ms{step="Vérifier"} = 0
     (plus aucun slip actif sur cette étape)
  3️⃣ Décommissioner le worker VérifierActivity

Comment monitorer l'épuisement des slips v1 ?
→ Métrique : routing_slip_step_duration_ms{step="Vérifier"} — Watch in Application Insights
→ Journal EMT : aucune entrée en Mode=Active avec step="Vérifier" depuis N minutes
→ Queue Service Bus queue-verifier : Active Message Count = 0
```

---

#### Scénario F — Changement de queue ou renommage d'infrastructure

```
Avant : steps[0].queue = "queue-valider-admissibilite"
Après : steps[0].queue = "queue-validation"  (nom raccourci)

Problème : les slips en vol ont "queue-valider-admissibilite" baked in.
Le RoutingSlipExecutor publiera vers cette queue, même après le renommage.
Si le worker n'écoute plus cette queue → messages bloqués.

Option 1 — Période de transition (recommandée) :
  1️⃣ Déployer le nouveau worker sur "queue-validation"
  2️⃣ Garder l'ancien worker sur "queue-valider-admissibilite" actif en parallèle
  3️⃣ Déployer l'activateur v2 (produit des slips avec "queue-validation")
  4️⃣ Attendre l'épuisement des slips v1 (voir Scénario E)
  5️⃣ Décommissioner l'ancien worker

Option 2 — Forward Rule Service Bus (migration transparente) :
  Configurer une règle de forwarding Service Bus :
    "queue-valider-admissibilite" → forward automatiquement vers "queue-validation"
  → Transparent pour le slip (les messages arrivent quand même)
  → Recommandé pour les migrations d'infrastructure sans coordination applicative
```

---

#### Tableau de synthèse — risques par type de changement

| Type de changement | Slips en vol affectés ? | Risque | Action requise |
|---|---|---|---|
| Logique métier d'une activité (`TArgs` inchangé) | Non | ✅ Aucun | Déploiement standard |
| Ajout d'une étape à la fin | Non (nouveaux slips seulement) | ✅ Aucun | Déployer le worker **avant** l'activateur |
| Ajout d'une propriété nullable à `TArgs` | Non | ✅ Aucun | Déploiement standard |
| Rolling deployment de l'activateur | Non | ✅ Aucun | Safe par construction (slip immuable) |
| Renommage d'une propriété `TArgs` | ⚠️ Partiel | Faible | `[JsonPropertyName]` pendant 1 cycle |
| Suppression d'une étape | ⚠️ Oui (v1 en vol) | Modéré | Décommissioner **après** épuisement des slips v1 |
| Changement de queue | ⚠️ Oui (v1 en vol) | Modéré | Transition parallèle ou forwarding SB |
| Insertion d'une étape au milieu | ⚠️ Oui (v1 en vol) | Modéré | Uniquement pour les nouveaux slips — v1 suivent leur propre chemin |
| Changement du type d'un argument | ❌ Oui | Élevé | Nouveau `stepName` obligatoire |
| Renommage complet d'une étape | ❌ Oui | Élevé | Nouveau `stepName` + migration explicite |

> 💡 **Pour un junior — la règle des « deux déploiements »**. Quand vous faites un changement qui affecte les slips en vol, pensez toujours à **deux moments** : (1) quand le changement est **activé** dans l'activateur (les nouveaux slips utilisent le nouveau format), et (2) quand il peut être **nettoyé** (les anciens workers/queues peuvent être décommissionnés). La fenêtre entre ces deux moments est votre période de coexistence — gardez les deux versions actives pendant ce temps.

---

## 17. Ce qui est hors périmètre

### Ce que le Routing Slip EMT ne fait PAS

| Feature | Pourquoi absente | Alternative recommandée |
|---------|-----------------|------------------------|
| **Branches conditionnelles** — aller à l'étape A ou B selon une condition | Couplage sur l'orchestrateur. Le slip est linéaire. | Azure Durable Functions (Orchestrator avec logique if/else) |
| **Attente humaine** — pause le workflow jusqu'à validation manuelle | Nécessite persistance de l'état pendant des jours/semaines | Azure Durable Functions (Human Interaction pattern) |
| **Fan-out/fan-in** — exécuter 3 étapes en parallèle et attendre qu'elles soient toutes terminées | Synchronisation complexe | Azure Durable Functions (Fan-out pattern) |
| **Re-publication partielle** — reprendre depuis l'étape X après correction | Outillage opérationnel — à développer dans une prochaine phase | Manuel via Azure Portal (DLQ) |

> **Note :** La **compensation** (annuler les étapes déjà effectuées en cas d'échec) est **dans le périmètre** depuis la révision de mai 2026 — voir [section 14](#14-compensation--annuler-ce-qui-a-déjà-été-fait) et [section 15](#15-exemple-complet-bout-en-bout-avec-retry-et-compensation).

### Règle simple pour choisir

```
Le traitement est une séquence linéaire d'étapes ?
    ├─ OUI → Routing Slip EMT ✅
    └─ NON → Azure Durable Functions
              ├─ Branches conditionnelles
              ├─ Compensation non-idempotente ou inter-services long terme
              ├─ Attente humaine
              └─ Fan-out/fan-in
```

---

## 18. Glossaire

| Terme | Définition |
|-------|-----------|
| **Routing Slip** | Patron de messagerie où l'itinéraire complet voyage avec le message |
| **Activateur** | Service qui démarre un routing slip (ex : Azure Function HTTP) |
| **Activité** (`IRoutingSlipActivity<T>`) | Votre code métier pour une étape — ne sait pas qu'il est dans un slip |
| **Compensateur** (`ICompensationActivity<T>`) | Opération inverse optionnelle déclenchée en ordre inverse si `Fault()` est appelé |
| **Curseur** (`Cursor`) | Index 0-basé de l'étape en cours dans le tableau `Steps` |
| **Executor** (`RoutingSlipExecutor`) | Composant interne EMT qui orchestre l'appel à votre activité et le routing |
| **SlipEnvelope** | Le message JSON qui circule sur Service Bus entre chaque étape |
| **SlipStep** | Une étape dans le slip : `EntityName`, `EntityType`, `Subscription?`, `Arguments`, `Status` |
| **EntityType** | `Queue` ou `Topic` — détermine comment l'executor résout la cible de publication |
| **Subscription** (slip) | Pour une étape Topic : `{ Consumer, Action? }` identifiant l'abonnement Service Bus |
| **Variables** | Dictionnaire partagé entre toutes les étapes — enrichi à chaque `Next(vars => ...)` |
| **Arguments** | Données spécifiques à une étape, définies à la construction du slip, immuables |
| **Claim-Check** | Feature EMT existante — déporte le payload dans Azure Blob si > 256 Ko |
| **DLQ** | Dead Letter Queue — file d'attente des messages en erreur permanente |
| **W3C traceparent** | Identifiant de trace distribué propagé automatiquement à chaque hop |
| **`ImmediateRetryException`** | Exception EMT : retry sans délai (contention courte) |
| **`ExponentialRetryException`** | Exception EMT : Abandon + backoff exponentiel configurable |
| **`ImmediateDLQException`** | Exception EMT : dead-letter immédiat sans retry |
| **`ExponentialRetryPolicy`** | Config : `InitialDelay`, `MaxDelay`, `MaxDeliveryCount`, `UseJitter` |
| **`ActivityResult.Next()`** | "J'ai terminé, passe à l'étape suivante" |
| **`ActivityResult.Fault(ex)`** | "Erreur permanente, envoie en DLQ (+ compensation si enregistrée)" |
| **`ActivityResult.Complete()`** | "Termine le slip ici" |
| **`ActivityResult.RetryImmediate(reason)`** | "Erreur transitoire courte — retry sans délai" |
| **`ActivityResult.RetryExponential(reason)`** | "Erreur transitoire longue — backoff exponentiel" |
| **`MessageEnvelope`** | Enveloppe RAMQ interne — unique format sérialisé sur le wire (v2.0). Discriminé par `Kind`. Compatible CloudEvents Phase 6 sans réécriture. |
| **`MessageKind`** | Discriminant de `MessageEnvelope` : `Message` (BaseConsumer) ou `RoutingSlip` (RoutingSlipExecutor). |
| **`MessageTransitContext<T>`** | Objet runtime construit depuis `MessageEnvelope` à la réception — jamais sérialisé (v2.0). |
| **Contrat vs runtime** | Contrat = ce qui traverse le réseau (sérialisé, versionné). Runtime = ce qui vit en mémoire le temps d'un traitement. |
| **Rolling deployment** | Déploiement progressif : safe avec `SlipEnvelope` car l'itinéraire voyage avec le message. |
| **Coexistence de versions** | Période où slips v1 et v2 coexistent en vol. Durée = temps moyen de traitement d'un slip complet. |
