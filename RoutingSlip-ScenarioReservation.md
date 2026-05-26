
# ⚙️ Sérialisation JSON des statuts (Status)

Depuis la version 2.0, tous les statuts d’étape (`Status` dans chaque `SlipStep`) sont **sérialisés en texte** dans le JSON (et non plus en entier). Cela garantit une interopérabilité maximale, une clarté pour les API consumers, et évite toute ambiguïté lors de l’inspection ou du débogage.

**Exemple de SlipEnvelope sérialisé :**

```json
{
  "header": {
    "SlipId": "0cce9c7c-5f91-4b13-8062-188e9842fc45",
    "SlipName": "Booking",
    "CorrelationId": "0cce9c7c-5f91-4b13-8062-188e9842fc45",
    "CreatedAt": "2026-05-20T22:45:59.4202894+00:00"
  },
  "steps": [
    { "Name": "ReserverVoiture", "EntityName": "sbq-rcp-routingslipcarreservation-unit", "EntityType": "Queue", "Status": "Active" },
    { "Name": "ReserverHotel",   "EntityName": "sbq-rcp-routingsliphotelreservation-unit", "EntityType": "Queue", "Status": "Pending" },
    { "Name": "ReserverVol",     "EntityName": "sbq-rcp-routingslipflightreservation-unit", "EntityType": "Queue", "Status": "Pending" }
  ],
  "cursor": 0,
  "variables": {}
}
```

**Valeurs possibles :**

- `"Pending"` : étape pas encore atteinte
- `"Active"` : étape en cours de traitement
- `"Completed"` : étape terminée avec succès
- `"Faulted"` : étape terminée en erreur (DLQ)

> **Remarque :** Toute inspection d’un message sur Service Bus, ou tout échange d’API, doit désormais s’attendre à voir les statuts sous forme de texte. Toute ancienne documentation ou exemple utilisant `"Status": 1` doit être considérée comme obsolète.

# RoutingSlip 2.0 — Scénarios de réservation voyage


## Résumé fonctionnement RoutingSlip Topic

### ⚠️ Validation stricte des propriétés Consumer/Action (pattern entreprise)

> **Depuis RoutingSlip 2.0, chaque étape Topic du slip doit porter explicitement les propriétés `Consumer` et `Action`, extraites de la configuration centrale et injectées dans le slip au moment de la construction.**

- Le `RoutingSlipBuilder` (ou l'activateur) résout, pour chaque étape de type Topic, les propriétés `Consumer` et `Action` via l'`IEndpointResolver`.
- **Si l'une de ces propriétés est absente ou vide, l'activateur lève une exception et refuse de publier le slip.**
- Le `Producer` et l'`AzureMessagingProvider` propagent ces propriétés comme Application Properties sur Service Bus.
- Les workers consomment ces propriétés via Application Properties, sans jamais dépendre de variables de contexte.

**Exemple de validation stricte dans l'activateur :**

```csharp
if (firstStep.EntityType == MessagingEntityType.Topic)
{
  if (string.IsNullOrWhiteSpace(firstStep.Subscription?.Consumer))
    throw new InvalidOperationException($"Consumer manquant pour l'étape topic '{firstStep.Name}'");
  if (string.IsNullOrWhiteSpace(firstStep.Subscription?.Action))
    throw new InvalidOperationException($"Action manquante pour l'étape topic '{firstStep.Name}'");
}
```

**Pourquoi cette validation stricte ?**
- Garantit le routage correct par Service Bus (règles SQL DevOps).
- Évite les erreurs silencieuses et les messages orphelins.
- Permet de détecter toute erreur de configuration dès la publication du slip.
- Simplifie le code des workers : ils reçoivent toujours les bonnes propriétés, sans logique supplémentaire.

**Anti-patterns à éviter :**
- ❌ Injecter dynamiquement `Consumer` ou `Action` via des variables de contexte ou des arguments d'API.
- ❌ Laisser le worker/consumer deviner ou reconstruire ces propriétés à partir du message ou d'une logique locale.
- ❌ Publier un slip sans vérifier la présence de ces propriétés pour chaque étape Topic.

---

> **Pour bien comprendre l’exemple Topic (Azure Service Bus) :**
>
> - **Un seul topic** (`sbt-rcp-routingslipreservation-unit`) transporte toutes les étapes du RoutingSlip (voiture, hôtel, vol).
> - **Chaque étape** cible une **subscription** différente, filtrée par les propriétés `Consumer` et `Action` (ex : `Consumer = 'Car' AND Action = 'Execute'`).
> - **La configuration** (`local.settings.json`) doit renseigner, pour chaque Target, le bloc `Subscription` avec les bons champs :
>   - `Consumer` : "Car", "Hotel", "Flight"
>   - `Action` : "Execute"
> - **Le RoutingSlipBuilder** copie ces valeurs dans le message publié (`SlipEnvelope` → `SlipStep` → `SlipTopicSubscription`).
> - **Les workers** consomment les messages selon la règle SQL de leur subscription (ex : `Consumer = 'Car' AND Action = 'Execute'`).
> - **Diagnostic** : Si un worker ne reçoit pas de message, vérifier que la subscription cible le bon couple `Consumer`/`Action` et que la règle SQL correspond bien à la config.
>
> Ce pattern permet de router dynamiquement chaque étape vers le bon abonné, tout en gardant un seul topic pour l’ensemble du workflow.

> **Audience :** développeurs juniors souhaitant comprendre chaque fonctionnalité de RoutingSlip 2.0 à travers des exemples concrets et exécutables.

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Architecture des projets](#2-architecture-des-projets)
3. [Entités Azure Service Bus réelles](#3-entités-azure-service-bus-réelles)
4. [Conventions de simulation (préfixes)](#4-conventions-de-simulation-préfixes)
5. [Mécanisme ctx.Attempt et budget de retry](#5-mécanisme-ctxattempt-et-budget-de-retry)
6. [ActivityResult.Complete() — arrêt anticipé vs fin normale](#6-activityresultcomplete--arrêt-anticipé-vs-fin-normale)
7. [Compensation best-effort — pattern COMPFAIL-](#7-compensation-best-effort--pattern-compfail-)
8. [Les 10 scénarios en détail](#8-les-10-scénarios-en-détail)
9. [Comment déclencher un scénario](#9-comment-déclencher-un-scénario)
10. [Traces de logs attendues par scénario](#10-traces-de-logs-attendues-par-scénario)
11. [Matrice des fonctionnalités couvertes](#11-matrice-des-fonctionnalités-couvertes)
12. [Configuration locale (local.settings.json)](#12-configuration-locale-localsettingsjson)
13. [Observabilité bout-à-bout (OpenTelemetry)](#13-observabilité-bout-à-bout-opentelemetry)
    - 13.1 [Architecture de traçage](#131-architecture-de-traçage)
    - 13.2 [Sources de traces](#132-sources-de-traces)
    - 13.3 [Tags sur les spans métier](#133-tags-sur-les-spans-métier)
    - 13.4 [Événements sur les spans](#134-événements-sur-les-spans)
    - 13.5 [Observabilité locale avec Jaeger](#135-observabilité-locale-avec-jaeger)
    - 13.6 [Configuration Program.cs](#136-configuration-programcs--explication-détaillée)
    - 13.7 [Propagation W3C Trace Context](#137-propagation-w3c-trace-context--comment-ça-marche)
    - 13.8 [Production — Azure Monitor](#138-production--azure-monitor--application-insights)
14. [Test graduel pas à pas — procédure complète](#14-test-graduel-pas-à-pas--procédure-complète)
    - 14.1 [Pourquoi tester pas à pas ?](#141-pourquoi-tester-pas-à-pas-)
    - 14.2 [Prérequis — éliminer les processus orphelins](#142-prérequis--éliminer-les-processus-orphelins)
    - 14.3 [Étape 1 — Démarrer l'Activateur seul](#143-étape-1--démarrer-lactivateur-seul)
    - 14.4 [Étape 2 — Publier le SlipEnvelope et inspecter la file voiture](#144-étape-2--publier-le-slipenveloppe-et-inspecter-la-file-voiture)
    - 14.5 [Étape 3 — Exécuter ReserverVoiture seulement](#145-étape-3--exécuter-reservervoiture-seulement)
    - 14.6 [Étape 4 — Inspecter la file hôtel et exécuter ReserverHotel](#146-étape-4--inspecter-la-file-hôtel-et-exécuter-reserverhotel)
    - 14.7 [Étape 5 — Inspecter la file vol et finaliser avec ReserverVol](#147-étape-5--inspecter-la-file-vol-et-finaliser-avec-reservervol)
    - 14.8 [Anatomie du SlipEnvelope à chaque étape](#148-anatomie-du-slipenveloppe-à-chaque-étape)
    - 14.9 [Référence rapide — commandes de gestion des processus](#149-référence-rapide--commandes-de-gestion-des-processus)

---

## 1. Vue d'ensemble

Ce dossier d'exemples illustre un flux de **réservation voyage** (voiture → hôtel → vol) implémenté avec le pattern **RoutingSlip 2.0**. L'objectif est de couvrir **toutes les fonctionnalités** du pattern :

| Fonctionnalité RoutingSlip | Scénario(s) associé(s) |
|---|---|
| `ActivityResult.Next()` — succès, passer à l'étape suivante | Tous les scénarios heureux |
| `ActivityResult.Fault()` — échec terminal → DLQ | `echec-voiture`, `echec-hotel`, `echec-vol` |
| `ActivityResult.RetryExponential()` — retry avec backoff croissant | `retry-transitoire-voiture`, `retry-transitoire-hotel` |
| `ActivityResult.RetryImmediate()` — retry sans délai | `retry-immediat-vol` |
| `ActivityResult.RetryExponential()` jamais résolu → **DLQ après épuisement** | `retry-epuise` |
| `ActivityResult.Complete()` — **arrêt anticipé** depuis une étape intermédiaire | `court-circuit-vip` |
| Compensation LIFO (rollback ordonné) | `echec-vol`, `echec-hotel` |
| Compensation best-effort partielle | `echec-compensation` |
| Variables partagées entre étapes (`ctx.Variables`) | Tous les scénarios |
| `ctx.Attempt` pour piloter les retries sans état statique | `retry-transitoire-*`, `retry-immediat-vol`, `retry-epuise` |

---

## 2. Architecture des projets

```
src/Exemples/
├── RAMQ.Samples.RoutingSlip.Booking.Message/          ← Contrats partagés (BookingRequest, CompensationLogEntry, ...)
│
├── RAMQ.Samples.Queue.RoutingSlip.Booking.Activateur/ ← HTTP API pour déclencher les scénarios (Queue)
├── RAMQ.Samples.Queue.RoutingSlip.Booking.Worker/     ← Azure Function traitant chaque étape (Queue)
│   ├── Activities/
│   │   ├── BookCarActivity.cs      ← Étape 1 : réservation voiture
│   │   ├── BookHotelActivity.cs    ← Étape 2 : réservation hôtel
│   │   └── BookFlightActivity.cs   ← Étape 3 : réservation vol
│   ├── Services/
│   │   ├── IBookingCompensationService.cs
│   │   └── BookingCompensationService.cs ← Logique de rollback LIFO best-effort
│   └── Functions/
│       └── BookingFunctions.cs     ← Triggers ServiceBus
│
├── RAMQ.Samples.Topic.RoutingSlip.Booking.Activateur/ ← Même chose, variante Topic
└── RAMQ.Samples.Topic.RoutingSlip.Booking.Worker/     ← Même logique, Topic routing
    └── Activities/
        └── BookingActivities.cs    ← Toutes les 3 activités dans un seul fichier
```

**Différence Queue vs Topic :** uniquement dans les triggers Functions et la configuration. La logique métier des activités est identique.

---

## 3. Entités Azure Service Bus réelles

### Variante Queue

| Étape | Nom de la queue |
|---|---|
| Réserver voiture | `sbq-rcp-routingslipcarreservation-unit` |
| Réserver hôtel   | `sbq-rcp-routingsliphotelreservation-unit` |
| Réserver vol     | `sbq-rcp-routingslipflightreservation-unit` |

### Variante Topic

| Étape | Topic | Subscription | Filtre SQL |
|---|---|---|---|
| Réserver voiture | `sbt-rcp-routingslipreservation-unit` | `sbts-RCP-RoutingSlipReservationAbonmCar`    | `Consumer = 'Car' AND Action = 'Execute'`    |
| Réserver hôtel   | `sbt-rcp-routingslipreservation-unit` | `sbts-RCP-RoutingSlipReservationAbonmHotel`  | `Consumer = 'Hotel' AND Action = 'Execute'`  |
| Réserver vol     | `sbt-rcp-routingslipreservation-unit` | `sbts-RCP-RoutingSlipReservationAbonmFlight` | `Consumer = 'Flight' AND Action = 'Execute'` |

---

## 4. Conventions de simulation (préfixes)

Pour éviter tout état statique (`static int _counter`), les scénarios sont pilotés par des **préfixes sur les valeurs de champs** du `BookingRequest`. L'activité lit le préfixe et choisit le comportement à simuler.

| Préfixe | Champ concerné | Comportement simulé | `ActivityResult` retourné |
|---|---|---|---|
| `INDISPO-`  | `CarModel`  | Voiture introuvable dès le 1er appel | `Fault()` |
| `COMPLET-`  | `HotelName` | Hôtel complet (toutes chambres réservées) | `Fault()` + compensation |
| `ANNULE-`   | `FlightName` | Vol annulé par la compagnie | `Fault()` + compensation |
| `TRANSIENT-` | `CarModel` ou `HotelName` | Panne réseau temporaire (HTTP 503) — guérit au 3e essai | `RetryExponential()` (essais 1 et 2), puis `Next()` |
| `THROTTLE-` | `FlightName` | API vol surchargée (HTTP 429) — guérit au 2e essai | `RetryImmediate()` (essai 1), puis `Next()` |
| `CRASH-`    | `CarModel`  | Panne permanente — `RetryExponential()` à **chaque** tentative, sans résolution | `RetryExponential()` → DLQ après épuisement |
| `COMPFAIL-` | `HotelName` | Réservation hôtel réussie, mais l'annulation ultérieure échoue | `Next()` (succès) + exception dans `AnnulerHotelAsync()` |
| `VIP-`      | `CarModel`  | Package pré-confirmé — arrêt anticipé du slip dès l'étape 1 | `Complete()` (hôtel et vol jamais appelés) |

---

## 5. Mécanisme ctx.Attempt et budget de retry

### Pourquoi ctx.Attempt ?

En RoutingSlip 2.0, quand une activité retourne `RetryExponential()` ou `RetryImmediate()`, le message est **remis sur le broker** avec son compteur de livraison incrémenté. L'activité est réinstanciée de zéro à chaque livraison — il n'y a **aucun état statique** à maintenir.

La propriété `ctx.Attempt` expose ce compteur (valeur 1-based venant du broker). Elle permet de savoir combien de fois le message a déjà été traité **sans avoir besoin de persistance externe**.

### Exemple dans BookCarActivity.cs

```csharp
// TRANSIENT- : échoue les 2 premiers essais, réussit au 3e
if (ctx.Arguments.CarModel.StartsWith("TRANSIENT-", ...)
    && ctx.Attempt <= 2)
{
    return ActivityResult.RetryExponential(
        $"Service indisponible — tentative {ctx.Attempt}",
        new HttpRequestException("HTTP 503 (SIMULATION)"));
}

// CRASH- : échoue à TOUTES les tentatives, sans condition sur ctx.Attempt
if (ctx.Arguments.CarModel.StartsWith("CRASH-", ...))
{
    return ActivityResult.RetryExponential(
        $"Panne permanente simulée (tentative {ctx.Attempt})",
        new TimeoutException("HTTP 504 (SIMULATION CRASH-)"));
}
```

### Différence RetryExponential vs RetryImmediate

| | `RetryExponential` | `RetryImmediate` |
|---|---|---|
| **Délai entre tentatives** | Croissant (1s, 4s, 16s, ...) | Nul (réessai immédiat) |
| **Cas d'usage** | Panne réseau, service down (attend que ça revienne) | Throttling HTTP 429 (capacité libérée en quelques ms) |
| **Scénarios** | `retry-transitoire-voiture`, `retry-transitoire-hotel`, `retry-epuise` | `retry-immediat-vol` |

### Que se passe-t-il quand les retries s'épuisent ?

Chaque queue/topic Service Bus a un paramètre **`MaxDeliveryCount`** (typiquement 10 par défaut). Quand le nombre de livraisons dépasse ce seuil, **Service Bus envoie le message automatiquement en Dead Letter Queue (DLQ)** — sans que l'activité ne fasse quoi que ce soit de spécial. L'activité continue juste de retourner `RetryExponential()`.

```
Tentative 1  → RetryExponential → attente ~1s
Tentative 2  → RetryExponential → attente ~4s
Tentative 3  → RetryExponential → attente ~16s
...
Tentative 10 → RetryExponential → Service Bus envoie en DLQ
               ↳ DeadLetterReason = "MaxDeliveryCountExceeded"
               ↳ DeadLetterErrorDescription = "Message has been dead-lettered..."
```

> **En production, la DLQ doit être monitorée.** Un message qui y arrive signifie qu'un problème n'a pas pu être résolu automatiquement et nécessite une intervention humaine ou un processus de repair.

Le scénario `retry-epuise` illustre exactement ce comportement : `BookCarActivity` retourne `RetryExponential()` à chaque tentative, sans jamais réussir.

---

## 6. ActivityResult.Complete() — arrêt anticipé vs fin normale

### La règle fondamentale : une activité ne sait pas qu'elle est la dernière

**Une activité retourne toujours `Next()`** pour signaler que son travail est terminé. Elle ne sait pas — et ne doit pas savoir — si elle est la dernière étape du slip ou non.

C'est le **framework** (`RoutingSlipExecutor`) qui détecte automatiquement la fin du workflow :

```csharp
// Dans RoutingSlipExecutor.HandleNextAsync (code interne EMT)
if (envelope.IsLastStep)
{
    // L'activité a retourné Next() sur la dernière étape
    // → le framework complète le message automatiquement
    await provider.CompleteMessageAsync(ct);
    return;
}
// Sinon, forward vers l'étape suivante...
```

Donc sur le chemin heureux :

```
BookCarActivity    → Next()   (framework : "pas la dernière, je forward vers Hotel")
BookHotelActivity  → Next()   (framework : "pas la dernière, je forward vers Vol")
BookFlightActivity → Next()   (framework : "c'est la dernière → CompleteMessage automatique")
```

### Alors à quoi sert Complete() ?

`Complete()` sert à **interrompre le slip en avance**, depuis une étape **intermédiaire**, de manière propre et sans erreur. C'est un **court-circuit volontaire** : "je stoppe le workflow maintenant, il ne faut pas exécuter les étapes restantes, et ce n'est pas une erreur".

```
BookCarActivity → Complete()
                  ↳ BookHotelActivity ne sera JAMAIS déclenchée
                  ↳ BookFlightActivity ne sera JAMAIS déclenchée
                  ↳ Le message est consommé normalement (pas de DLQ)
```

### Comparaison des résultats possibles

| Résultat | Étapes suivantes | Erreur ? | Message final |
|---|---|---|---|
| `Next()` | Exécutées normalement | Non | Complété (si dernière étape) ou forwardé |
| `Complete()` | **Toutes annulées** | **Non** | Complété immédiatement |
| `Fault()` | **Toutes annulées** | **Oui** | Envoyé en **DLQ** |

> **Différence clé entre `Complete()` et `Fault()`** : les deux stoppent le slip, mais `Fault()` envoie le message en DLQ (erreur à traiter), `Complete()` le consomme normalement (tout va bien, on s'arrête juste avant la fin prévue).

### Quand utiliser Complete() en pratique ?

| Situation | Pourquoi Complete() |
|---|---|
| **Idempotence** : l'activité détecte que le travail a déjà été fait lors d'une run précédente | Éviter de retraiter sans erreur |
| **Condition d'arrêt métier** : un client VIP a un contrat cadre — pas besoin de réserver hôtel et vol séparément | Sortir proprement sans erreur |
| **A/B routing conditionnel** : une règle métier indique que ce slip ne s'applique pas à ce type de message | Court-circuiter sans signaler d'erreur |
| **Découpage de workflow** : une étape intermédiaire réalise que tout le reste est inutile | Éviter du travail inutile |

### Ce que Complete() ne fait PAS

- Il **n'exécute pas de compensation** (contrairement à `Fault()`).
- Il **n'envoie pas en DLQ**.
- Il **ne signale pas d'erreur** dans les métriques ou les alertes.

### Exemple dans BookCarActivity.cs (scénario VIP-)

```csharp
// VIP- : package pré-confirmé — on stoppe le slip dès l'étape 1
if (ctx.Arguments.CarModel.StartsWith("VIP-", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogInformation(
        "[{Step}] Package VIP pré-confirmé — slip terminé en avance (Complete). "
        + "Hôtel et vol ne seront pas appelés. SlipId={SlipId}",
        ctx.StepName, ctx.SlipId);
    return ActivityResult.Complete();
}
```

Quand cette activité retourne `Complete()`, les triggers des queues `sbq-rcp-routingsliphotelreservation-unit` et `sbq-rcp-routingslipflightreservation-unit` **ne recevront jamais de message** pour ce slip.

---

## 7. Compensation best-effort — pattern COMPFAIL-

### Principe

La compensation est exécutée en **ordre LIFO** (Last In, First Out) : si le vol échoue après que la voiture et l'hôtel ont été réservés, on annule d'abord l'hôtel, puis la voiture.

Le `BookingCompensationService` applique une stratégie **best-effort** :
- Chaque étape de compensation est dans un `try/catch` indépendant.
- Si une annulation échoue, l'erreur est **loggée en Critical** et la compensation **continue** pour les étapes restantes.
- La méthode retourne la liste des étapes qui ont **échoué** (`IReadOnlyList<CompensationLogEntry>`).

### Pourquoi best-effort ?

En cas de panne du service hôtel au moment de la compensation, bloquer toute la compensation serait pire : la voiture resterait réservée sans raison. On essaie de tout annuler, on signale les échecs, on continue.

### Extrait de BookingCompensationService.cs

```csharp
public async Task<IReadOnlyList<CompensationLogEntry>> CompensateAsync(
    IEnumerable<CompensationLogEntry> log, string slipId, CancellationToken ct)
{
    var echecs = new List<CompensationLogEntry>();

    foreach (var entry in log.Reverse())   // LIFO
    {
        try { await AnnulerAsync(entry, ct); }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[Compensation] ÉCHEC de l'annulation de {Step} — ConfirmationId={Id}, SlipId={SlipId}",
                entry.StepName, entry.ConfirmationId, slipId);
            echecs.Add(entry);
        }
    }
    return echecs;
}
```

### Signal COMPFAIL- transporté dans le ConfirmationId

Lors de l'étape hôtel avec `COMPFAIL-`, le `ConfirmationId` est généré ainsi :

```csharp
var confirmation = ctx.Arguments.HotelName.StartsWith("COMPFAIL-", ...)
    ? $"COMPFAIL-HTL-{ctx.Arguments.ReservationId:N}"   // ← signal intégré dans le token
    : $"HTL-{ctx.Arguments.ReservationId:N}";
```

Quand `AnnulerHotelAsync` reçoit un `ConfirmationId` commençant par `COMPFAIL-`, elle simule une indisponibilité de l'API d'annulation.

---

## 8. Les 10 scénarios en détail

### Scénario 1 : `succes-complet`

**Objectif :** Valider le chemin nominal — les 3 étapes réussissent. Illustre que la dernière activité retourne `Next()`, pas `Complete()` : c'est le framework qui détecte la fin et complète automatiquement.

**Payload :**
```json
{ "carModel": "Toyota Camry", "hotelName": "Marriott Centre-Ville",
  "hotelRoomPreference": "Standard", "flightName": "AC421 Montréal→Paris" }
```

**Séquence :**
1. `BookCarActivity`    → `Next()` + `CompensationLog[0]`
2. `BookHotelActivity`  → `Next()` + `CompensationLog[1]`
3. `BookFlightActivity` → `Next()` → **framework : dernière étape → CompleteMessage automatique**

**`ActivityResult` exercés :** `Next()` (complétion implicite par le framework sur la dernière étape)

---

### Scénario 2 : `echec-voiture`

**Objectif :** Fault direct à l'étape 1 — aucune compensation nécessaire (rien n'a été réservé).

**Payload :**
```json
{ "carModel": "INDISPO-Ferrari", "hotelName": "Delta Montréal",
  "hotelRoomPreference": "Standard", "flightName": "WS432 Montréal→Vancouver" }
```

**Séquence :**
1. `BookCarActivity` → détecte `INDISPO-` → `Fault()` → message en DLQ

**`ActivityResult` exercés :** `Fault()`

---

### Scénario 3 : `echec-hotel`

**Objectif :** Fault à l'étape 2 avec compensation de l'étape 1.

**Payload :**
```json
{ "carModel": "Ford Explorer", "hotelName": "COMPLET-Hilton Laval",
  "hotelRoomPreference": "Standard", "flightName": "PC101 Québec→Toronto" }
```

**Séquence :**
1. `BookCarActivity`   → `Next()` + `CompensationLog[0]`
2. `BookHotelActivity` → détecte `COMPLET-` → lit log → annule voiture → `Fault()` → DLQ

**`ActivityResult` exercés :** `Next()`, `Fault()`

---

### Scénario 4 : `echec-vol`

**Objectif :** Fault à l'étape 3 avec compensation LIFO des étapes 1 et 2.

**Payload :**
```json
{ "carModel": "Honda Civic", "hotelName": "Sheraton Vieux-Montréal",
  "hotelRoomPreference": "Suite", "flightName": "ANNULE-RR990 Montréal→Rome" }
```

**Séquence :**
1. `BookCarActivity`    → `Next()` + `CompensationLog[0]`
2. `BookHotelActivity`  → `Next()` + `CompensationLog[1]`
3. `BookFlightActivity` → détecte `ANNULE-` → annule hôtel puis voiture (LIFO) → `Fault()` → DLQ

**`ActivityResult` exercés :** `Next()`, `Fault()`

---

### Scénario 5 : `retry-transitoire-voiture`

**Objectif :** Démontrer `RetryExponential` avec résolution — panne transitoire guérit au 3e essai.

**Payload :**
```json
{ "carModel": "TRANSIENT-Nissan Rogue", "hotelName": "Marriott Centre-Ville",
  "hotelRoomPreference": "Standard", "flightName": "AC421 Montréal→Paris" }
```

**Séquence :**
1. `BookCarActivity` — `ctx.Attempt = 1` → `RetryExponential()` (délai ~1s)
2. `BookCarActivity` — `ctx.Attempt = 2` → `RetryExponential()` (délai ~4s)
3. `BookCarActivity` — `ctx.Attempt = 3` → `Next()` (succès)
4. `BookHotelActivity`  → `Next()`
5. `BookFlightActivity` → `Next()` → framework : CompleteMessage automatique

**`ActivityResult` exercés :** `RetryExponential()`, `Next()`

---

### Scénario 6 : `retry-transitoire-hotel`

**Objectif :** Même démonstration à l'étape 2 — la voiture est déjà réservée entre les tentatives, l'étape 1 n'est pas rejouée.

**Payload :**
```json
{ "carModel": "Hyundai Tucson", "hotelName": "TRANSIENT-Fairmont Le Reine Elizabeth",
  "hotelRoomPreference": "Deluxe", "flightName": "WJ456 Montréal→Miami" }
```

**Séquence :**
1. `BookCarActivity`   → `Next()` (une seule fois — le message est sur la queue hôtel dès ici)
2. `BookHotelActivity` — `ctx.Attempt = 1` → `RetryExponential()`
3. `BookHotelActivity` — `ctx.Attempt = 2` → `RetryExponential()`
4. `BookHotelActivity` — `ctx.Attempt = 3` → `Next()`
5. `BookFlightActivity` → `Next()` → framework : CompleteMessage automatique

**`ActivityResult` exercés :** `RetryExponential()`, `Next()`

---

### Scénario 7 : `retry-immediat-vol`

**Objectif :** Démontrer `RetryImmediate` — API vol HTTP 429 au 1er essai, réussit immédiatement au 2e.

**Payload :**
```json
{ "carModel": "Toyota RAV4", "hotelName": "Delta Montréal-Laval",
  "hotelRoomPreference": "Standard", "flightName": "THROTTLE-PC101 Montréal→Toronto" }
```

**Séquence :**
1. `BookCarActivity`    → `Next()`
2. `BookHotelActivity`  → `Next()`
3. `BookFlightActivity` — `ctx.Attempt = 1` → `RetryImmediate()` (sans délai)
4. `BookFlightActivity` — `ctx.Attempt = 2` → `Next()` → framework : CompleteMessage automatique

**`ActivityResult` exercés :** `RetryImmediate()`, `Next()`

---

### Scénario 8 : `echec-compensation`

**Objectif :** Compensation partielle — l'annulation hôtel échoue (COMPFAIL-), voiture annulée quand même (best-effort), log Critical émis.

**Payload :**
```json
{ "carModel": "Honda CR-V", "hotelName": "COMPFAIL-Fairmont Le Château Frontenac",
  "hotelRoomPreference": "Standard", "flightName": "ANNULE-AC888 Québec→New York" }
```

**Séquence :**
1. `BookCarActivity`    → `Next()` + `CompensationLog[{ReserverVoiture, CAR-xxx, Voiture}]`
2. `BookHotelActivity`  → `COMPFAIL-` → `ConfirmationId = "COMPFAIL-HTL-xxx"` → `Next()` + log
3. `BookFlightActivity` → `ANNULE-` → compensation LIFO :
   - annuler hôtel (`COMPFAIL-HTL-xxx`) → **exception** → `echecs.Count = 1` → log Critical
   - annuler voiture (`CAR-xxx`) → succès
   - → `Fault()` → DLQ

**`ActivityResult` exercés :** `Next()`, `Fault()`

---

### Scénario 9 : `retry-epuise`

**Objectif :** Démontrer l'épuisement du budget de retry → DLQ. `BookCarActivity` retourne **toujours** `RetryExponential()`, quelle que soit la valeur de `ctx.Attempt`. Il n'y a pas de condition de résolution dans le code de l'activité. C'est **Service Bus** qui met fin au cycle quand `DeliveryCount > MaxDeliveryCount`.

**Payload :**
```json
{ "carModel": "CRASH-Lamborghini Urus", "hotelName": "Marriott Centre-Ville",
  "hotelRoomPreference": "Standard", "flightName": "AC421 Montréal→Paris" }
```

**Séquence :**
```
Livraison 1  → RetryExponential("tentative 1") → délai ~1s
Livraison 2  → RetryExponential("tentative 2") → délai ~4s
Livraison 3  → RetryExponential("tentative 3") → délai ~16s
...
Livraison 10 → RetryExponential("tentative 10")
              → Service Bus : DeliveryCount(10) > MaxDeliveryCount(10)
              → Message automatiquement envoyé en DLQ
              → DeadLetterReason = "MaxDeliveryCountExceeded"
```

**Point clé :** Le code de l'activité ne contient aucune logique "après N essais, faire X". La DLQ est entièrement gérée par le broker. L'activité dit juste "pas encore disponible, réessaie" et c'est tout.

**`ActivityResult` exercés :** `RetryExponential()` (jamais résolu)

---

### Scénario 10 : `court-circuit-vip`

**Objectif :** Démontrer `Complete()` comme **arrêt anticipé volontaire** depuis une étape intermédiaire. `BookCarActivity` (étape 1) détecte un package VIP pré-confirmé et retourne `Complete()`. `BookHotelActivity` et `BookFlightActivity` ne seront **jamais déclenchées**.

**Payload :**
```json
{ "carModel": "VIP-Mercedes S-Class", "hotelName": "Ritz-Carlton Montréal",
  "hotelRoomPreference": "Suite Présidentielle", "flightName": "AC001 Montréal→Paris (Première Classe)" }
```

**Séquence :**
```
BookCarActivity → Complete()
                  ↳ Framework : CompleteMessage immédiat (pas de DLQ, pas d'erreur)
                  ↳ Aucun message envoyé sur sbq-rcp-routingsliphotelreservation-unit
                  ↳ Aucun message envoyé sur sbq-rcp-routingslipflightreservation-unit
```

**Pourquoi pas `Next()` ici ?**
- `Next()` dirait au framework "mon travail est fait, passe à l'étape suivante" → l'hôtel serait réservé normalement.
- `Complete()` dit "mon travail est fait ET le slip s'arrête ici" → les étapes suivantes sont ignorées.

**Pourquoi pas `Fault()` ?**
- `Fault()` enverrait le message en DLQ et signalerait une erreur.
- Ici c'est un succès métier — le client VIP est servi, il n'y a aucune erreur.

**`ActivityResult` exercés :** `Complete()`

---

## 9. Comment déclencher un scénario

### Étape 1 — Lister les scénarios disponibles

```http
GET http://localhost:7071/api/bookings/scenarios
```

Réponse :
```json
{
  "succes-complet":            "Les 3 étapes réussissent → booking confirmé.",
  "echec-voiture":             "Voiture indisponible dès l'étape 1 → Fault direct → DLQ.",
  "echec-hotel":               "Voiture réservée, hôtel complet → compensation : voiture annulée → DLQ.",
  "echec-vol":                 "Voiture + hôtel réservés, vol annulé → compensation LIFO → DLQ.",
  "retry-transitoire-voiture": "Panne transitoire voiture → RetryExponential 2 fois, succès au 3e.",
  "retry-transitoire-hotel":   "Panne transitoire hôtel → RetryExponential 2 fois, succès au 3e.",
  "retry-immediat-vol":        "API vol saturée (HTTP 429) → RetryImmediate, succès au 2e essai.",
  "echec-compensation":        "Vol annulé + annulation hôtel en échec → compensation partielle, log Critical.",
  "retry-epuise":              "Panne permanente voiture → RetryExponential à chaque tentative → DLQ après MaxDeliveryCount.",
  "court-circuit-vip":         "Package VIP pré-confirmé → Complete() dès l'étape 1, hôtel et vol jamais appelés."
}
```

### Étape 2 — Déclencher un scénario prédéfini

```http
POST http://localhost:7071/api/bookings/scenarios/retry-epuise
```

### Étape 3 — Ou déclencher avec un payload personnalisé

```http
POST http://localhost:7071/api/bookings
Content-Type: application/json

{
  "carModel": "CRASH-Kia EV6",
  "hotelName": "Novotel Montréal",
  "hotelRoomPreference": "Standard",
  "flightName": "AC421 Montréal→Paris"
}
```

---

## 10. Traces de logs attendues par scénario

### succes-complet

```
[ReserverVoiture] Tentative 1 — Modèle=Toyota Camry, SlipId=abc123
[ReserverVoiture] Voiture réservée. ConfirmationId=CAR-abc123
[ReserverHotel]   Tentative 1 — Hôtel=Marriott Centre-Ville, SlipId=abc123
[ReserverHotel]   Hôtel réservé. ConfirmationId=HTL-abc123
[ReserverVol]     Tentative 1 — Vol=AC421 Montréal→Paris, SlipId=abc123
[ReserverVol]     Vol réservé. ConfirmationId=FLT-abc123
INFO  RoutingSlipExecutor: dernière étape 'ReserverVol' terminée, slip complet. SlipId=abc123
```

### retry-epuise

```
[ReserverVoiture] ERROR  Service voiture en panne permanente (CRASH-) — tentative 1, SlipId=abc123
                  → RetryExponential (délai ~1s)
[ReserverVoiture] ERROR  Service voiture en panne permanente (CRASH-) — tentative 2, SlipId=abc123
                  → RetryExponential (délai ~4s)
...
[ReserverVoiture] ERROR  Service voiture en panne permanente (CRASH-) — tentative 10, SlipId=abc123
                  → RetryExponential
// Le framework ne voit plus le message — Service Bus l'a envoyé en DLQ
// Dead Letter Queue : sbq-rcp-routingslipcarreservation-unit/$DeadLetterQueue
// DeadLetterReason  : MaxDeliveryCountExceeded
```

### court-circuit-vip

```
[ReserverVoiture] Tentative 1 — Modèle=VIP-Mercedes S-Class, SlipId=abc123
[ReserverVoiture] INFO  Package VIP pré-confirmé détecté — slip terminé en avance (Complete).
                         Hôtel et vol ne seront pas appelés. SlipId=abc123
INFO  RoutingSlipExecutor: Complete explicite à l'étape 'ReserverVoiture', SlipId=abc123
// Aucun message sur sbq-rcp-routingsliphotelreservation-unit
// Aucun message sur sbq-rcp-routingslipflightreservation-unit
```

### echec-compensation

```
[ReserverVoiture] Voiture réservée. CompensationLog=[{ReserverVoiture,CAR-xxx,Voiture}]
[ReserverHotel]   Hôtel réservé avec ConfirmationId=COMPFAIL-HTL-xxx
[ReserverVol]     WARN  Vol annulé, déclenchement compensation
[Compensation]    CRIT  ÉCHEC de l'annulation de ReserverHotel
                         ConfirmationId=COMPFAIL-HTL-xxx
                         InvalidOperationException: API annulation hôtel indisponible (SIMULATION COMPFAIL-)
[Compensation]    INFO  Annulation voiture OK — ConfirmationId=CAR-xxx
[ReserverVol]     CRIT  [Compensation partielle] 1 annulation(s) ont échoué pour SlipId=abc123
```

---

## 11. Matrice des fonctionnalités couvertes

| Fonctionnalité | `succes` | `ech-voit` | `ech-hot` | `ech-vol` | `ret-voit` | `ret-hot` | `ret-vol` | `ech-comp` | `ret-epuis` | `vip` |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `Next()` | ✅ | — | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | — |
| `Fault()` | — | ✅ | ✅ | ✅ | — | — | — | ✅ | — | — |
| `RetryExponential()` résolu | — | — | — | — | ✅ | ✅ | — | — | — | — |
| `RetryImmediate()` résolu | — | — | — | — | — | — | ✅ | — | — | — |
| `RetryExponential()` épuisé → DLQ | — | — | — | — | — | — | — | — | ✅ | — |
| `Complete()` arrêt anticipé | — | — | — | — | — | — | — | — | — | ✅ |
| `ctx.Attempt` | — | — | — | — | ✅ | ✅ | ✅ | — | ✅ | — |
| Compensation LIFO | — | — | ✅ | ✅ | — | — | — | ✅ | — | — |
| Variables entre étapes | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | — |
| Best-effort + log Critical | — | — | — | — | — | — | — | ✅ | — | — |

---

## 12. Configuration locale (local.settings.json)

### Worker Queue (`RAMQ.Samples.Queue.RoutingSlip.Booking.Worker`)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "<votre-connection-string-ici>"
  },
  "AppSettings": {
    "ServiceBusConnection": "<votre-connection-string-ici>",
    "Endpoints": [
      { "Target": "ReserverVoiture", "Endpoint": { "EntityName": "sbq-rcp-routingslipcarreservation-unit",    "EntityType": "Queue" } },
      { "Target": "ReserverHotel",   "Endpoint": { "EntityName": "sbq-rcp-routingsliphotelreservation-unit",  "EntityType": "Queue" } },
      { "Target": "ReserverVol",     "Endpoint": { "EntityName": "sbq-rcp-routingslipflightreservation-unit", "EntityType": "Queue" } }
    ]
  }
}
```


### Worker Topic (`RAMQ.Samples.Topic.RoutingSlip.Booking.Worker`)

> **Rappel : Pour chaque Target de type Topic, la configuration doit obligatoirement fournir les champs `Consumer` et `Action`. Toute absence ou incohérence doit être détectée par l'activateur avant publication du slip.**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "<votre-connection-string-ici>"
  },
  "AppSettings": {
    "ServiceBusConnection": "<votre-connection-string-ici>",
    "Endpoints": [
      { "Target": "ReserverVoiture", "Endpoint": { "EntityName": "sbt-rcp-routingslipreservation-unit", "EntityType": "Topic", "Subscription": "sbts-RCP-RoutingSlipReservationAbonmCar"    } },
      { "Target": "ReserverHotel",   "Endpoint": { "EntityName": "sbt-rcp-routingslipreservation-unit", "EntityType": "Topic", "Subscription": "sbts-RCP-RoutingSlipReservationAbonmHotel"  } },
      { "Target": "ReserverVol",     "Endpoint": { "EntityName": "sbt-rcp-routingslipreservation-unit", "EntityType": "Topic", "Subscription": "sbts-RCP-RoutingSlipReservationAbonmFlight" } }
    ]
  }
}
```

> **Pour développement local :** remplacez `<votre-connection-string-ici>` par la chaîne de connexion du namespace Service Bus de développement, ou utilisez l'émulateur Service Bus local avec `tests/docker-compose.servicebus.yml`.

---

## 13. Observabilité bout-à-bout (OpenTelemetry)

### 13.1 Architecture de traçage

Les exemples Booking implémentent une **observabilité enterprise complète** basée sur OpenTelemetry (OTel). Deux sources de traces coexistent et forment une hiérarchie cohérente dans Jaeger ou Azure Monitor :

```
[messaging.send]                               ← EMT : activateur publie le SlipEnvelope
  └─ [routing_slip.step : ReserverVoiture]     ← EMT : RoutingSlipExecutor par étape
       └─ [booking.car.reserve]                ← Métier : BookCarActivity
  └─ [routing_slip.step : ReserverHotel]
       └─ [booking.hotel.reserve]              ← Métier : BookHotelActivity
            └─ [booking.compensate]            ← Métier : si Fault(), rollback LIFO
  └─ [routing_slip.step : ReserverVol]
       └─ [booking.flight.reserve]             ← Métier : BookFlightActivity
            └─ [booking.compensate]            ← Métier : si Fault(), rollback LIFO
```

La continuité de la trace entre les processus est assurée par la propagation **W3C Trace Context** (`traceparent` / `tracestate`) dans les `ApplicationProperties` du message Service Bus — gérée automatiquement par EMT.

### 13.2 Sources de traces

| Source | Constante | Qui émet | Spans émis |
|---|---|---|---|
| EMT | `EMTInstrumentation.SourceName` | Librairie EMT | `messaging.send`, `messaging.consume`, `routing_slip.step` |
| Booking (métier) | `BookingTelemetry.SourceName` | Activités d'exemple | `booking.car.reserve`, `booking.hotel.reserve`, `booking.flight.reserve`, `booking.compensate` |

### 13.3 Tags sur les spans métier

Chaque span `booking.*.reserve` expose les tags suivants :

| Tag | Type | Exemple |
|---|---|---|
| `booking.slip_id` | string | `"3fa85f64-5717-4562-b3fc-2c963f66afa6"` |
| `booking.reservation_id` | string | `"a1b2c3d4-..."` |
| `booking.step` | string | `"ReserverVoiture"` |
| `booking.attempt` | int | `2` |
| `booking.car.model` | string | `"TRANSIENT-Renault Clio"` |
| `booking.car.confirmation_id` | string | `"CAR-a1b2c3d4..."` *(si succès)* |
| `booking.car.available` | bool | `true` / `false` |
| `booking.retry.type` | string | `"RetryExponential"` / `"RetryImmediate"` |
| `booking.retry.reason` | string | `"HTTP 503 Service Unavailable"` |
| `booking.car.vip_package` | bool | `true` *(scénario VIP-)* |
| `error.type` | string | `"Fault"` *(si Fault())* |

Le span `booking.compensate` ajoute :

| Tag | Type | Description |
|---|---|---|
| `booking.compensation.entries_count` | int | Nombre d'entrées à compenser |
| `booking.compensation.failures` | int | Nombre d'annulations en échec |

### 13.4 Événements sur les spans

Certaines transitions importantes sont enregistrées comme **ActivityEvent** dans le span :

| Événement | Déclencheur | Tags associés |
|---|---|---|
| `retry.scheduled` | `RetryExponential` ou `RetryImmediate` | `attempt`, `max_attempts` |
| `dlq.budget_consumption` | CRASH- (retry sans résolution) | `attempt` |
| `booking.vip.court_circuit_complete` | VIP- (`Complete()`) | *(aucun)* |

### 13.5 Observabilité locale avec Jaeger

#### 13.5.1 Qu'est-ce que Jaeger ?

**Jaeger** est un outil open-source de **tracing distribué**, créé par Uber et maintenant hébergé par la Cloud Native Computing Foundation (CNCF). Son rôle est de collecter, stocker et visualiser les traces qui traversent plusieurs services d'une application.

**Analogie pour comprendre :** imaginez que vous envoyez un colis par La Poste. Le colis passe par plusieurs centres de tri avant d'arriver chez le destinataire. Chaque centre de tri tamponne le bon de livraison avec l'heure et l'endroit. À la fin, vous pouvez reconstituer l'itinéraire complet. Jaeger fait exactement cela avec vos requêtes logicielles : il reconstitue le trajet d'une opération à travers vos services.

**La différence avec les logs :**

| Logs | Traces (Jaeger) |
|---|---|
| Ligne de texte indépendante par processus | Vue hiérarchique à travers tous les processus |
| Corrélation manuelle par SlipId dans des fichiers séparés | Une seule vue avec toutes les étapes en ordre chronologique |
| Difficile de voir la durée d'une étape | Chaque span a une durée mesurée à la milliseconde |
| Impossible de voir quel service a appelé quel autre | Graphe de dépendances entre services (Service Map) |
| Pas de notion de succès/échec standardisée | Statut `Ok` / `Error` avec description sur chaque span |

**Dans notre cas :** sans Jaeger, déboguer un scénario `retry-epuise` implique d'ouvrir les logs de l'Activateur et du Worker dans deux terminaux séparés, puis de chercher manuellement le SlipId pour corréler les entrées. Avec Jaeger, on voit d'un coup d'œil : `messaging.send` → 10 × `routing_slip.step[ReserverVoiture]` avec statut `Error` à chaque fois, puis rien (DLQ).

#### 13.5.2 Vocabulaire Jaeger / OpenTelemetry

Avant d'utiliser l'interface, comprendre ces termes est essentiel :

| Terme | Définition | Dans notre contexte |
|---|---|---|
| **Trace** | L'ensemble du trajet d'une opération, de bout en bout, avec un identifiant unique (`TraceId`) | Une réservation voyage complète, de l'appel HTTP jusqu'à la confirmation vol |
| **Span** | Une unité de travail dans la trace, avec une durée et des métadonnées | `messaging.send`, `routing_slip.step`, `booking.car.reserve`, `booking.compensate` |
| **TraceId** | Identifiant hexadécimal de 32 caractères partagé par tous les spans d'une trace | `3fa85f645717...` — le même pour l'activateur ET les 3 workers |
| **SpanId** | Identifiant de 16 caractères propre à chaque span | Chaque `booking.car.reserve` a son propre SpanId |
| **Parent span** | Le span qui a déclenché un span enfant | `routing_slip.step` est le parent de `booking.car.reserve` |
| **Tag** | Paire clé-valeur attachée à un span (ex : `booking.attempt = 2`) | `booking.car.model`, `error.type`, `booking.retry.reason` |
| **Event (Log dans Jaeger)** | Point temporel dans un span (ex : `retry.scheduled`) | `dlq.budget_consumption`, `booking.vip.court_circuit_complete` |
| **Statut** | `Ok` (succès) ou `Error` (échec) avec message | Affiché en rouge dans Jaeger si `Error` |
| **Service** | Un processus applicatif déclaré dans `AddService(serviceName: ...)` | `booking-queue-activateur`, `booking-queue-worker` |

#### 13.5.3 Architecture locale : comment les traces arrivent dans Jaeger

```
┌─ booking-queue-activateur ─────────────────────────────────────────────┐
│  Program.cs : t.AddOtlpExporter(o => o.Endpoint = new Uri("http://    │
│                                        localhost:4317"))               │
│  → À chaque span émis, l'OTel SDK l'envoie en gRPC vers Jaeger        │
└────────────────────────────────────────────────────────────────────────┘
                     │ gRPC OTLP (port 4317)
                     ▼
┌─ Jaeger (docker container) ─────────────────────────────────────────────┐
│  Reçoit les spans OTLP                                                  │
│  Les stocke en mémoire                                                  │
│  Expose l'interface web sur http://localhost:16686                      │
└─────────────────────────────────────────────────────────────────────────┘
                     ↑ même chose
┌─ booking-queue-worker ──────────────────────────────────────────────────┐
│  Même configuration OTel → même exporteur OTLP → même Jaeger           │
│  Les spans du Worker ont le même TraceId que l'Activateur               │
│  (grâce à la propagation W3C via Service Bus)                           │
└─────────────────────────────────────────────────────────────────────────┘
```

**Point important :** les spans de l'Activateur et du Worker se retrouvent dans la **même trace** parce qu'EMT propage le `traceparent` dans les `ApplicationProperties` du message Service Bus. Quand Jaeger reçoit un span du Worker avec le même `TraceId` que celui de l'Activateur, il les assemble automatiquement dans une seule vue.

#### 13.5.4 Démarrage et vérification

**Prérequis :** Docker Desktop installé et démarré.

**Étape 1 — Lancer Jaeger :**

```bash
# Depuis la racine du repo
docker compose -f tests/docker-compose.observability.yml up -d
```

Sortie attendue :
```
✔ Container jaeger-booking  Started
```

**Vérifier que Jaeger est sain :**

```bash
# La commande doit retourner "healthy" après quelques secondes
docker inspect jaeger-booking --format="{{.State.Health.Status}}"
```

Ou simplement ouvrir [http://localhost:16686](http://localhost:16686) — la page d'accueil Jaeger doit apparaître avec le logo Jaeger en haut à gauche.

**Ports exposés par le conteneur :**

| Port | Protocole | Utilisation |
|---|---|---|
| `4317` | gRPC | Réception des spans OTLP (utilisé par les Azure Functions) |
| `4318` | HTTP | Réception OTLP HTTP (alternatif si gRPC bloqué) |
| `16686` | HTTP | Interface utilisateur Jaeger |
| `14268` | HTTP | Récepteur Thrift legacy (compatibilité anciens agents) |

**Étape 2 — Configurer `local.settings.json` :**

Les 4 fichiers `local.settings.json` ont déjà été mis à jour. Vérifier la présence de ces deux clés dans chacun :

```json
{
  "Values": {
    "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
  }
}
```

> **`APPLICATIONINSIGHTS_CONNECTION_STRING` vide** : si vide, l'exporteur Azure Monitor ne s'active pas. Seul Jaeger reçoit les traces. C'est l'état correct pour le développement local.

**Étape 3 — Démarrer les Azure Functions :**

```powershell
# Terminal 1 — Worker Queue
cd src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Worker
func start --port 7072

# Terminal 2 — Activateur Queue
cd src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Activateur
func start --port 7071
```

> **Pourquoi deux ports différents ?** Les deux projets sont des Azure Functions qui démarrent sur le port 7071 par défaut. Il faut les différencier avec `--port`.

**Étape 4 — Déclencher un scénario :**

```bash
# Scénario succès nominal
curl -X POST http://localhost:7071/api/BookingActivateur \
  -H "Content-Type: application/json" \
  -d "{\"scenarioName\": \"succes-complet\"}"

# Scénario avec retry
curl -X POST http://localhost:7071/api/BookingActivateur \
  -H "Content-Type: application/json" \
  -d "{\"scenarioName\": \"retry-transitoire-voiture\"}"
```

#### 13.5.5 Navigation dans l'interface Jaeger

**Vue principale — Search :**

Aller sur [http://localhost:16686](http://localhost:16686).

La page de recherche a ces champs :

| Champ | Que mettre | Pour notre cas |
|---|---|---|
| **Service** | Nom du service | `booking-queue-activateur` (point d'entrée) |
| **Operation** | Nom du span racine | `messaging.send` ou laisser vide |
| **Tags** | Filtre sur les tags | `booking.slip_id=xxx` ou `error=true` |
| **Lookback** | Fenêtre temporelle | `Last 1 hour` pour les tests |
| **Min/Max Duration** | Filtrer par durée | Utile pour trouver les retries (durée > 5s) |
| **Limit Results** | Nombre de résultats | 20 par défaut |

**Cliquer sur "Find Traces"** — la liste des traces apparaît à droite. Chaque ligne représente une trace complète (une réservation). On voit :
- La durée totale de la trace
- Le nombre de spans
- La timeline colorée (vert = succès, rouge = erreur)

**Vue détail d'une trace :**

En cliquant sur une trace, on accède à la **vue Waterfall** (cascade) qui montre tous les spans dans l'ordre chronologique, avec leur durée représentée par une barre horizontale.

```
booking-queue-activateur   messaging.send                    ████░░░░░░░░░░░░░░░░░░░░ 12ms
booking-queue-worker         routing_slip.step[Voiture]        ████████████████████████ 58ms
booking-queue-worker           booking.car.reserve               ████░░░░░░░░░░░░░░░░ 52ms
booking-queue-worker         routing_slip.step[Hotel]          ████████████████████████ 75ms
booking-queue-worker           booking.hotel.reserve             ████████░░░░░░░░░░░░ 71ms
booking-queue-worker         routing_slip.step[Vol]            ████████████████████████ 103ms
booking-queue-worker           booking.flight.reserve            ████████████████████ 98ms
```

**Lire un span individuel :**

Cliquer sur une ligne (span) ouvre un panneau de détail avec :

- **Tags** : tous les attributs (ex : `booking.attempt = 2`, `booking.car.model = TRANSIENT-...`)
- **Logs (Events)** : les ActivityEvents (ex : `retry.scheduled` avec `attempt = 1`)
- **Process** : métadonnées du service (`serviceName`, `serviceVersion`, `deployment.environment`)

**Ce que chaque scénario montre dans Jaeger :**

| Scénario | Ce qu'on voit dans la timeline |
|---|---|
| `succes-complet` | 7 spans en vert, hiérarchie propre, durée totale ~200ms |
| `echec-voiture` | `booking.car.reserve` en rouge, statut `Error: Voiture indisponible — Fault → DLQ` |
| `retry-transitoire-voiture` | 2 spans `booking.car.reserve` en rouge (attempts 1 et 2), le 3e en vert |
| `echec-compensation` | `booking.compensate` en rouge avec tag `booking.compensation.failures = 1` |
| `retry-epuise` | 10 spans `booking.car.reserve` tous en rouge — pas de suite (DLQ) |
| `court-circuit-vip` | `booking.car.reserve` en vert avec event `booking.vip.court_circuit_complete`, plus aucun span après |

**Rechercher par SlipId :**

Dans le champ **Tags**, saisir :
```
booking.slip_id=3fa85f64-5717-4562-b3fc-2c963f66afa6
```
→ Retrouve directement la trace correspondant à ce slip, peu importe quel service l'a émis.

**Service Map :**

Aller dans l'onglet **System Architecture** → **DAG** (Directed Acyclic Graph). Jaeger dessine automatiquement le graphe des appels entre services : `booking-queue-activateur` → `booking-queue-worker`. Cela confirme que la propagation de trace fonctionne correctement entre les processus.

#### 13.5.6 Arrêt de Jaeger

```bash
docker compose -f tests/docker-compose.observability.yml down
```

> **Note :** Jaeger stocke les traces en mémoire (`SPAN_STORAGE_TYPE: memory`). Toutes les traces sont perdues à l'arrêt du conteneur. C'est intentionnel pour le développement local — aucune persistence n'est nécessaire.



### 13.6 Configuration Program.cs — explication détaillée

```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: "booking-queue-worker",  // nom du service dans Jaeger
            serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = ctx.HostingEnvironment.EnvironmentName,
        }))
    .WithTracing(t =>
    {
        // ① Source EMT : routing_slip.step, messaging.send, messaging.consume
        t.AddSource(EMTInstrumentation.SourceName);

        // ② Source métier : booking.*.reserve, booking.compensate
        t.AddSource(BookingTelemetry.SourceName);

        // ③ Spans HTTP sortants (appels aux APIs de réservation en production)
        t.AddHttpClientInstrumentation();

        // ④ Exporteur local : Jaeger via OTLP gRPC
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));

        // ⑤ Exporteur production : Azure Monitor / Application Insights
        if (!string.IsNullOrWhiteSpace(aiConnectionString))
            t.AddAzureMonitorTraceExporter(o => o.ConnectionString = aiConnectionString);
    });
```

**Point clé :** les deux exporteurs sont mutuellement exclusifs par configuration — vide `APPLICATIONINSIGHTS_CONNECTION_STRING` en local, vide `OTEL_EXPORTER_OTLP_ENDPOINT` en production Azure.

### 13.7 Propagation W3C Trace Context — comment ça marche

```
┌─ Activateur ────────────────────────────────────────────────────────────┐
│  Activity.Current = messaging.send (TraceId: abc123, SpanId: def456)    │
│  AzureMessagingProvider.PublishAsync()                                   │
│    → message.ApplicationProperties["traceparent"] = "00-abc123-def456-01"│
└──────────────────────────────────────────────────────────────────────────┘
                          ▼  Service Bus  ▼
┌─ Worker — BookCarActivity ───────────────────────────────────────────────┐
│  BaseConsumer.ProcessAsync()                                              │
│    → lit ApplicationProperties["traceparent"]                            │
│    → restaure le contexte parent W3C                                     │
│    → Activity.Current = routing_slip.step (parent = messaging.send)      │
│         → BookingTelemetry.Source.StartActivity("booking.car.reserve")   │
│              → span enfant du routing_slip.step                          │
└──────────────────────────────────────────────────────────────────────────┘
```

**Résultat :** une **trace unique** s'étend de l'activateur jusqu'au dernier Worker, traversant 3 processus distincts et Service Bus. C'est la valeur fondamentale du tracing distribué.

### 13.8 Production — Azure Monitor / Application Insights

#### 13.8.1 Qu'est-ce qu'Azure Monitor et Application Insights ?

**Azure Monitor** est la plateforme d'observabilité native d'Azure. Elle centralise logs, métriques et traces provenant de tous les services Azure ainsi que de vos applications.

**Application Insights** est le composant d'Azure Monitor dédié aux applications. Il reçoit les traces OpenTelemetry de vos services et les stocke dans un **Log Analytics Workspace**, qui est une base de données cloud requêtable en **KQL** (Kusto Query Language).

**Différences clés avec Jaeger :**

| Jaeger (local) | Azure Monitor (production) |
|---|---|
| Stockage en mémoire — traces perdues au redémarrage | Stockage persistant jusqu'à 90 jours (configurable) |
| Pas d'authentification | Sécurisé via Azure RBAC |
| Aucun coût | Coût à l'ingestion et à la rétention |
| Interfaces : Jaeger UI | Interfaces : Azure Portal, workbooks, alertes |
| Recherche par tag | Requêtes KQL puissantes avec jointures |
| Pas d'alertes natives | Alertes configurable sur n'importe quelle condition |
| Dev local uniquement | Staging, production, multi-région |

#### 13.8.2 Comment Application Insights reçoit nos traces

L'exporteur `Azure.Monitor.OpenTelemetry.Exporter` remplace OTLP par un protocole propriétaire d'Azure. La configuration `Program.cs` est identique mais l'exporteur change :

```csharp
// En local : OTLP → Jaeger
t.AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"));

// En production : Azure Monitor Exporter → Application Insights
t.AddAzureMonitorTraceExporter(o =>
    o.ConnectionString = "InstrumentationKey=xxx;IngestionEndpoint=https://...");
```

La `ConnectionString` est disponible dans le portail Azure : **Application Insights** → **Overview** → **Connection String**.

#### 13.8.3 Mapping des concepts OTel → Application Insights

Dans Application Insights, les spans OpenTelemetry sont stockés dans différentes tables selon leur `ActivityKind` :

| ActivityKind OTel | Table Application Insights | Exemple de nos spans |
|---|---|---|
| `Server` ou `Consumer` | `requests` | `messaging.consume` (BaseConsumer) |
| `Client` | `dependencies` | `booking.car.reserve`, `booking.hotel.reserve`, `booking.flight.reserve` |
| `Internal` | `dependencies` | `routing_slip.step`, `booking.compensate` |
| *(producer)* | `dependencies` | `messaging.send` |

Les **tags** OTel deviennent des colonnes dans `customDimensions`. Par exemple, `booking.slip_id` devient `customDimensions["booking.slip_id"]` dans KQL.

#### 13.8.4 Configuration

**1. Variables d'environnement Azure Functions (portail ou `appsettings.json` en staging) :**

```json
{
  "Values": {
    "OTEL_EXPORTER_OTLP_ENDPOINT": "",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/"
  }
}
```

> Laisser `OTEL_EXPORTER_OTLP_ENDPOINT` vide en production — l'exporteur OTLP ne s'active pas si la chaîne est vide.

**2. Vérification du bon fonctionnement :**

Dans le portail Azure, aller dans **Application Insights** → **Transaction Search**. Saisir le SlipId dans le champ de recherche libre. Si les traces arrivent, elles apparaissent en quelques secondes (latence d'ingestion ~10-30s).

#### 13.8.5 Requêtes KQL pour l'observabilité opérationnelle

KQL (Kusto Query Language) est le langage de requête d'Azure Monitor. Sa syntaxe ressemble à un pipeline de traitement de données : chaque `|` passe les résultats à l'opération suivante.

**Retrouver toute la trace d'un slip par son SlipId :**

```kql
dependencies
| where customDimensions["booking.slip_id"] == "3fa85f64-5717-4562-b3fc-2c963f66afa6"
| project timestamp, name, duration, success, customDimensions
| order by timestamp asc
```

Ce que chaque colonne signifie :
- `timestamp` : heure du début du span
- `name` : nom du span (`booking.car.reserve`, `routing_slip.step`, etc.)
- `duration` : durée en millisecondes
- `success` : `true` si statut `Ok`, `false` si statut `Error`
- `customDimensions` : dictionnaire de tous les tags OTel

**Voir toutes les compensations des dernières 24h :**

```kql
dependencies
| where name == "booking.compensate"
| where timestamp > ago(24h)
| extend slip_id    = tostring(customDimensions["booking.slip_id"]),
         n_echecs   = toint(customDimensions["booking.compensation.failures"]),
         n_entrees  = toint(customDimensions["booking.compensation.entries_count"]),
         step       = tostring(customDimensions["booking.step"])
| project timestamp, step, slip_id, n_entrees, n_echecs, success
| order by timestamp desc
```

**Détecter les compensations partielles (échecs critiques) :**

```kql
dependencies
| where name == "booking.compensate"
| where toint(customDimensions["booking.compensation.failures"]) > 0
| extend slip_id  = tostring(customDimensions["booking.slip_id"]),
         n_echecs = toint(customDimensions["booking.compensation.failures"]),
         step     = tostring(customDimensions["booking.step"])
| project timestamp, step, slip_id, n_echecs
| order by timestamp desc
```

> **Ce résultat nécessite une intervention humaine.** Une compensation partielle signifie qu'une réservation externe n'a pas pu être annulée — il faut traiter manuellement.

**Toutes les réservations ayant fini en DLQ (Fault) :**

```kql
dependencies
| where name in ("booking.car.reserve", "booking.hotel.reserve", "booking.flight.reserve")
| where tostring(customDimensions["error.type"]) == "Fault"
| extend slip_id = tostring(customDimensions["booking.slip_id"]),
         step    = tostring(customDimensions["booking.step"]),
         modele  = tostring(customDimensions["booking.car.model"])
| project timestamp, name, step, slip_id, modele
| order by timestamp desc
```

**Analyser les retries sur une période :**

```kql
dependencies
| where timestamp > ago(1h)
| where isnotempty(customDimensions["booking.retry.type"])
| extend slip_id     = tostring(customDimensions["booking.slip_id"]),
         retry_type  = tostring(customDimensions["booking.retry.type"]),
         retry_raison = tostring(customDimensions["booking.retry.reason"]),
         attempt     = toint(customDimensions["booking.attempt"])
| project timestamp, name, slip_id, attempt, retry_type, retry_raison
| order by slip_id asc, attempt asc
```

**Durée moyenne par étape du RoutingSlip (dernière heure) :**

```kql
dependencies
| where timestamp > ago(1h)
| where name in ("booking.car.reserve", "booking.hotel.reserve", "booking.flight.reserve")
| summarize avg_duration_ms = avg(duration),
            p95_duration_ms = percentile(duration, 95),
            count_ok   = countif(success == true),
            count_err  = countif(success == false)
            by name
| order by name asc
```

**Reconstituer une trace complète par `operation_Id` (TraceId) :**

```kql
// Trouver d'abord le TraceId d'un slip :
dependencies
| where customDimensions["booking.slip_id"] == "3fa85f64-5717-4562-b3fc-2c963f66afa6"
| project operation_Id
| limit 1

// Puis récupérer tous les spans de cette trace :
dependencies
| where operation_Id == "3fa85f645717456289fc2c963f66afa6"
| union (requests | where operation_Id == "3fa85f645717456289fc2c963f66afa6")
| project timestamp, itemType, name, duration, success, cloud_RoleName
| order by timestamp asc
```

#### 13.8.6 Alertes recommandées

Ces alertes doivent être créées dans **Azure Monitor** → **Alerts** → **Create Alert Rule** avec une requête KQL comme condition.

| Alerte | Condition KQL | Sévérité | Action |
|---|---|---|---|
| Compensation partielle | `booking.compensation.failures > 0` | Sev 1 (Critical) | Ticket P1 automatique |
| Réservation en DLQ | `error.type == "Fault"` | Sev 2 (Error) | Notification équipe |
| Taux de retry élevé | `>10% des spans booking.*.reserve ont retry.type` | Sev 3 (Warning) | Notification astreinte |
| Durée p95 > seuil | `p95 > 5000ms` sur un span booking | Sev 3 (Warning) | Notification performance |

**Exemple de règle d'alerte pour compensation partielle :**

```kql
// Requête à coller dans l'alert rule (évaluation toutes les 5 minutes)
dependencies
| where timestamp > ago(5m)
| where name == "booking.compensate"
| where toint(customDimensions["booking.compensation.failures"]) > 0
| count
```
Condition : `count > 0` → déclenche l'alerte.

---

## 14. Test graduel pas à pas — procédure complète

### 14.1 Pourquoi tester pas à pas ?

Le pattern RoutingSlip achemine un message à travers plusieurs files Service Bus de manière séquentielle. Tester le flux complet d'un coup est pratique, mais ne permet pas d'**observer l'état du SlipEnvelope entre chaque étape**. Le test graduel consiste à :

1. Démarrer l'Activateur seul et publier un message.
2. Inspecter le message brut sur la file Service Bus avant qu'il soit consommé.
3. Démarrer le Worker avec **un seul trigger** à la fois (`--functions NomDeLaFonction`).
4. Arrêter le Worker après chaque étape, inspecter la file suivante, puis donner le signal de continuer.

**Ce que cela permet :**
- Voir le `cursor`, le `Status` de chaque étape et les `variables` accumulées évoluer en temps réel.
- Confirmer que chaque étape lit et écrit correctement dans le `SlipEnvelope`.
- Déboguer une étape précise sans que les étapes suivantes la consomment immédiatement.

**Prérequis :** accès à l'interface Service Bus Explorer du namespace Azure ou tout autre outil permettant de **peek** (lire sans consommer) les messages d'une file.

---

### 14.2 Prérequis — éliminer les processus orphelins

> **Problème fréquent :** un processus Worker d'une session précédente reste connecté aux triggers Service Bus et consomme silencieusement les messages publiés. Les files semblent vides alors que les messages ont bien été reçus par l'ancien Worker.

**Avant chaque session de test**, exécuter cette commande pour lister tous les processus `dotnet` et `func` actifs avec leur ligne de commande :

```powershell
Get-Process -Name "dotnet","func" -EA SilentlyContinue | ForEach-Object {
    $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine
    [PSCustomObject]@{ PID=$_.Id; Name=$_.Name; Cmd=$cmdline }
} | Format-Table -AutoSize
```

Si un processus `RAMQ.Samples.Queue.RoutingSlip.Booking.Worker.dll` apparaît sans avoir été démarré intentionnellement, le tuer :

```powershell
Stop-Process -Id <PID> -Force
```

**Libérer les ports 7071 et 7072 (tuer n'importe quel processus qui les occupe) :**

```powershell
# Libérer le port 7071 (Activateur)
$p = Get-NetTCPConnection -LocalPort 7071 -State Listen -EA SilentlyContinue
if ($p) { Stop-Process -Id $p.OwningProcess -Force; "Activateur arrêté" } else { "Port 7071 libre" }

# Libérer le port 7072 (Worker)
$p = Get-NetTCPConnection -LocalPort 7072 -State Listen -EA SilentlyContinue
if ($p) { Stop-Process -Id $p.OwningProcess -Force; "Worker arrêté" } else { "Port 7072 déjà libre" }
```

---

### 14.3 Étape 1 — Démarrer l'Activateur seul

Ouvrir un terminal dédié et démarrer **uniquement l'Activateur** :

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Activateur
func start --port 7071
```

Sortie attendue :
```
Functions:
        BookingActivateur: [GET,POST] http://localhost:7071/api/BookingActivateur

[...] Host lock lease acquired by instance ID '...'
```

> **Le Worker n'est pas démarré.** Les messages publiés resteront sur les files Service Bus jusqu'à ce que vous décidiez de les consommer.

---

### 14.4 Étape 2 — Publier le SlipEnvelope et inspecter la file voiture

**Publier un scénario nominal :**

```powershell
Invoke-RestMethod -Uri "http://localhost:7071/api/BookingActivateur" `
                  -Method POST `
                  -ContentType "application/json" `
                  -Body '{"scenarioName": "succes-complet"}'
```

Ou avec `curl` :

```bash
curl -X POST http://localhost:7071/api/BookingActivateur \
  -H "Content-Type: application/json" \
  -d "{\"scenarioName\": \"succes-complet\"}"
```

L'Activateur logue le SlipId assigné :
```
[BookingActivateur] SlipId=0cce9c7c-5f91-4b13-8062-188e9842fc45 publié sur ReserverVoiture
```

**Inspecter la file voiture dans Service Bus Explorer :**

File : `sbq-rcp-routingslipcarreservation-unit`

État attendu du `SlipEnvelope` (cursor=0, seule `ReserverVoiture` a Status=1) :

```json
{
  "header": {
    "SlipId": "0cce9c7c-5f91-4b13-8062-188e9842fc45",
    "SlipName": "Booking",
    "CorrelationId": "0cce9c7c-5f91-4b13-8062-188e9842fc45",
    "CreatedAt": "2026-05-20T22:45:59.4202894+00:00"
  },
  "steps": [
    { "Name": "ReserverVoiture", "EntityName": "sbq-rcp-routingslipcarreservation-unit", "EntityType": "Queue", "Status": 1 },
    { "Name": "ReserverHotel",   "EntityName": "sbq-rcp-routingsliphotelreservation-unit", "EntityType": "Queue", "Status": 0 },
    { "Name": "ReserverVol",     "EntityName": "sbq-rcp-routingslipflightreservation-unit", "EntityType": "Queue", "Status": 0 }
  ],
  "cursor": 0,
  "variables": {}
}
```

> **Clés à observer :** `cursor=0` (on est à l'étape 0), `ReserverVoiture.Status=1` (Pending), les deux autres à `Status=0` (NotStarted), `variables` vide.

---

### 14.5 Étape 3 — Exécuter ReserverVoiture seulement

Ouvrir un **deuxième terminal** et démarrer le Worker avec uniquement le trigger `ReserverVoiture` :

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Worker
func start --port 7072 --functions ReserverVoiture
```

Sortie attendue (extrait) :
```
Functions:
        ReserverVoiture: serviceBusTrigger

[...] Executing 'Functions.ReserverVoiture' (Reason='(null)', Id=...)
[...] Trigger Details: MessageId: 0cce9c7c-..., DeliveryCount: 1, EnqueuedTimeUtc: ...
[...] Executed 'Functions.ReserverVoiture' (Succeeded, Id=..., Duration=10689ms)
```

Le flag `--functions ReserverVoiture` indique à `func` de ne charger **que ce trigger**. Les triggers `ReserverHotel` et `ReserverVol` existent dans le même projet mais ne sont **pas enregistrés** dans cet hôte — le message publié sur la file hôtel restera intact jusqu'au prochain démarrage.

**Arrêter le Worker** après la confirmation `Succeeded` :

```powershell
$p = Get-NetTCPConnection -LocalPort 7072 -State Listen -EA SilentlyContinue
if ($p) { Stop-Process -Id $p.OwningProcess -Force; "Worker arrêté" }
```

---

### 14.6 Étape 4 — Inspecter la file hôtel et exécuter ReserverHotel

**Inspecter la file hôtel dans Service Bus Explorer :**

File : `sbq-rcp-routingsliphotelreservation-unit`

État attendu du `SlipEnvelope` (cursor=1, `ReserverVoiture` terminée, `ReserverHotel` Pending) :

```json
{
  "steps": [
    { "Name": "ReserverVoiture", "Status": 2 },
    { "Name": "ReserverHotel",   "Status": 1 },
    { "Name": "ReserverVol",     "Status": 0 }
  ],
  "cursor": 1,
  "variables": {
    "ConfirmationVoiture": "CAR-8768452dfe794091ae9fa0080d1bec8b",
    "DateReservationVoiture": "2026-05-20T22:54:33.0518153Z",
    "CompensationLog": [
      { "StepName": "ReserverVoiture", "ConfirmationId": "CAR-8768452dfe794091ae9fa0080d1bec8b", "ServiceType": "Voiture" }
    ]
  }
}
```

> **Clés à observer :** `cursor=1`, `ReserverVoiture.Status=2` (Completed), `ReserverHotel.Status=1` (Pending), `variables` contiennent maintenant `ConfirmationVoiture`, `DateReservationVoiture` et une entrée dans `CompensationLog`. C'est le log de compensation qui permettrait d'annuler la voiture si l'hôtel ou le vol échouaient.

**Démarrer ReserverHotel seulement :**

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Worker
func start --port 7072 --functions ReserverHotel
```

Sortie attendue :
```
Functions:
        ReserverHotel: serviceBusTrigger

[...] Executed 'Functions.ReserverHotel' (Succeeded, Id=..., Duration=10447ms)
```

**Arrêter le Worker** dès que `Succeeded` apparaît (même commande que précédemment).

---

### 14.7 Étape 5 — Inspecter la file vol et finaliser avec ReserverVol

**Inspecter la file vol dans Service Bus Explorer :**

File : `sbq-rcp-routingslipflightreservation-unit`

État attendu du `SlipEnvelope` (cursor=2, les deux premières étapes terminées, `ReserverVol` Pending) :

```json
{
  "steps": [
    { "Name": "ReserverVoiture", "Status": 2 },
    { "Name": "ReserverHotel",   "Status": 2 },
    { "Name": "ReserverVol",     "EntityName": "sbq-rcp-routingslipflightreservation-unit", "Status": 1 }
  ],
  "cursor": 2,
  "variables": {
    "ConfirmationVoiture": "CAR-8768452dfe794091ae9fa0080d1bec8b",
    "DateReservationVoiture": "2026-05-20T22:54:33.0518153Z",
    "CompensationLog": [
      { "StepName": "ReserverHotel", "ConfirmationId": "CAR-8768452dfe794091ae9fa0080d1bec8b", "ServiceType": "Voiture" }
    ]
  }
}
```

> **Clés à observer :** `cursor=2` (dernière étape), `ReserverVoiture.Status=2` et `ReserverHotel.Status=2`, `ReserverVol.Status=1`. Le `CompensationLog` s'est enrichi avec l'entrée de l'hôtel.

**Démarrer ReserverVol (étape finale) :**

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Worker
func start --port 7072 --functions ReserverVol
```

Sortie attendue :
```
Functions:
        ReserverVol: serviceBusTrigger

[...] Executing 'Functions.ReserverVol' (Reason='(null)', Id=...)
[...] Trigger Details: MessageId: 0cce9c7c-..., DeliveryCount: 1, EnqueuedTimeUtc: ...
[...] Executed 'Functions.ReserverVol' (Succeeded, Id=..., Duration=5039ms)
```

`ReserverVol` est la **dernière étape** du slip. Quand elle retourne `Next()`, le framework EMT détecte automatiquement qu'il n'y a plus d'étapes suivantes et **complète le message** (`CompleteMessageAsync`). Le slip est terminé avec succès — aucun message ne reste sur aucune file.

---

### 14.8 Anatomie du SlipEnvelope à chaque étape

Ce tableau résume l'évolution du `SlipEnvelope` tout au long du test graduel pour le scénario `succes-complet` :

| Moment | File | `cursor` | `ReserverVoiture.Status` | `ReserverHotel.Status` | `ReserverVol.Status` | Variables notables |
|---|---|:---:|:---:|:---:|:---:|---|
| Après publication | `sbq-rcp-routingslipcarreservation-unit` | 0 | 1 (Pending) | 0 (NotStarted) | 0 (NotStarted) | *(vide)* |
| Après ReserverVoiture | `sbq-rcp-routingsliphotelreservation-unit` | 1 | 2 (Completed) | 1 (Pending) | 0 (NotStarted) | `ConfirmationVoiture`, `DateReservationVoiture`, `CompensationLog[0]` |
| Après ReserverHotel | `sbq-rcp-routingslipflightreservation-unit` | 2 | 2 (Completed) | 2 (Completed) | 1 (Pending) | + `ConfirmationHotel`, `DateReservationHotel`, `CompensationLog[1]` |
| Après ReserverVol | *(aucun message restant)* | — | 2 | 2 | 2 (Completed) | + `ConfirmationVol`, `DateReservationVol` |

**Signification des valeurs de `Status` :**

| Valeur | Nom | Signification |
|:---:|---|---|
| 0 | `NotStarted` | L'étape n'a pas encore été rencontrée par le framework |
| 1 | `Pending` | Le message est sur la file, en attente de traitement |
| 2 | `Completed` | L'activité a retourné `Next()` avec succès |
| 3 | `Failed` | L'activité a retourné `Fault()` → message en DLQ |
| 4 | `Skipped` | L'étape a été ignorée (suite à `Complete()` sur une étape précédente) |

---

### 14.9 Référence rapide — commandes de gestion des processus

Ces commandes PowerShell couvrent toutes les opérations nécessaires au test graduel.

**Lister les processus func/dotnet actifs avec leur ligne de commande :**

```powershell
Get-Process -Name "dotnet","func" -EA SilentlyContinue | ForEach-Object {
    $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine
    [PSCustomObject]@{ PID=$_.Id; Name=$_.Name; Cmd=$cmdline }
} | Format-Table -AutoSize
```

**Tuer un processus par PID :**

```powershell
Stop-Process -Id <PID> -Force
```

**Libérer le port 7072 (Worker) :**

```powershell
$p = Get-NetTCPConnection -LocalPort 7072 -State Listen -EA SilentlyContinue
if ($p) { Stop-Process -Id $p.OwningProcess -Force; "Worker arrêté" } else { "Port 7072 déjà libre" }
```

**Démarrer l'Activateur :**

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Activateur
func start --port 7071
```

**Démarrer le Worker pour une étape spécifique :**

```powershell
cd D:\source\RCP-AzureMessageTransit\src\Exemples\RAMQ.Samples.Queue.RoutingSlip.Booking.Worker

# Étape 1 seulement
func start --port 7072 --functions ReserverVoiture

# Étape 2 seulement
func start --port 7072 --functions ReserverHotel

# Étape 3 seulement (finalisation)
func start --port 7072 --functions ReserverVol

# Toutes les étapes en même temps (test complet non graduel)
func start --port 7072
```

**Publier un scénario depuis PowerShell :**

```powershell
Invoke-RestMethod -Uri "http://localhost:7071/api/BookingActivateur" `
                  -Method POST `
                  -ContentType "application/json" `
                  -Body '{"scenarioName": "succes-complet"}'
```

**Files Service Bus à inspecter entre chaque étape :**

| Étape exécutée | File suivante à inspecter |
|---|---|
| *(aucune — juste après publication)* | `sbq-rcp-routingslipcarreservation-unit` |
| `ReserverVoiture` terminée | `sbq-rcp-routingsliphotelreservation-unit` |
| `ReserverHotel` terminée | `sbq-rcp-routingslipflightreservation-unit` |
| `ReserverVol` terminée | *(aucun message restant)* |

> **Rappel :** l'authentification locale utilise `VisualStudioCredential` (Azure Identity). S'assurer que Visual Studio ou le CLI Azure (`az login`) est connecté au bon tenant avant de démarrer les Azure Functions.

---

## ⚠️ Problèmes courants — Fichiers verrouillés lors du build

**Symptôme** :  
- Erreur MSB3021/MSB3026 « Impossible de copier le fichier ... car il est utilisé par un autre processus » lors de `dotnet build` ou `func start`.
- Le worker ne démarre pas, ou la recompilation échoue en boucle.

**Causes fréquentes** :  
- Un processus `dotnet` ou `func` (Azure Functions) est encore actif et garde les DLLs verrouillées dans `bin/output`.
- Un explorateur de fichiers ou un antivirus scanne le dossier de build.
- Un crash ou une fermeture brutale du terminal laisse un processus orphelin.

**Solution rapide** :  
1. Fermer tous les terminaux Azure Functions.
2. Tuer tous les processus .NET/func restants :
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like '*dotnet*' -or $_.ProcessName -like '*func*' } | Stop-Process -Force
   ```
3. Nettoyer les dossiers `bin` et `obj` du projet concerné :
   ```powershell
   Remove-Item -Recurse -Force .\bin\*; Remove-Item -Recurse -Force .\obj\*
   ```
4. Relancer la compilation et le worker.

**Astuce** :  
Éviter d’ouvrir le dossier `bin` dans l’explorateur Windows pendant le build.

