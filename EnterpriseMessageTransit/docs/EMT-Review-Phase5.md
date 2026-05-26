# EMT — Plan d'exécution Phase 5 : Routing Slip natif (breaking change MAJOR v2.0)

> **Couleur de référence :** 🟠 Élevé
> **Source :** [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md) — §3.2 · §3.3 · §3.4 · §8.3 · [architecture-routing-slip.md](architecture-routing-slip.md)
> **Date de planification :** 11 mai 2026 (portée révisée)
> **Durée indicative :** 6-10 semaines
> **Risque / Complexité :** 🟠 Élevé / 🔴 Très élevée — breaking change MAJOR v2.0 assumé ; toutes les applications consommatrices multi-étapes doivent migrer
> **Prérequis :** Phase 4 ✅ complétée (27 avril 2026) · Fondations Phase 5 originale ✅ livrées (27 avril 2026)
> **Statut :** 🔄 Portée révisée — fondations livrées (27 avril 2026) · nouvelle portée v2.0 : ⬜ À démarrer

---

## Positionnement de la phase

> 💡 **Pour les développeurs juniors.** La Phase 5 est celle où EMT passe d'un design de routing implicite (l'itinéraire vit dans `appsettings.json`) à un **Routing Slip natif** (l'itinéraire voyage avec le message). C'est un **breaking change MAJOR délibéré** : les consumers multi-étapes existants doivent migrer. Ce choix est préférable à un refactoring silencieux car la casse est **visible** (erreur de compilation), **documentée** (guide de migration), et **coordonnée** (tag v2.0 annoncé).
>
> La règle d'or : **un breaking change assumé est moins dangereux qu'un refactoring silencieux.** Chaque étape additive d'abord — suppression seulement quand les remplaçants sont verts.

### Fondations déjà livrées (Phase 5 originale — 27 avril 2026)

Les travaux ci-dessous ont été livrés dans la session du 27 avril 2026. Ils constituent les **prérequis techniques** de la nouvelle portée v2.0 :

| Livrable | Statut | Rôle dans v2.0 |
|----------|--------|----------------|
| `IStageAdvancer` + `StageAdvancer` + 14 tests purs (T10) | ✅ | Fondation réutilisée par `RoutingSlipExecutor` |
| `InMemoryMessagingAdapter` + suite contrat `IMessagingAdapter` (10 tests T6) | ✅ | Provider de test pour valider `RoutingSlipExecutor` sans Service Bus |
| `stryker-config.json` + pipeline `mutation-tests.yml` (T16) | ✅ | Infrastructure mutation testing pour le nouveau code |
| `AzureFunctionMessagingAdapter` → namespace `Azure.Functions` + 2 règles architecture | ✅ | Prépare la scission Phase 6 |
| `IMessageTransit` enrichi (livré Phase 2, confirmé Phase 5) | ✅ | Intégré dans v2.0 |

### Nouvelle portée v2.0 — ce que la Phase 5 ajoute

La Phase 5 v2.0 implémente le design complet décrit dans `docs/architecture-routing-slip.md` :

- **Surface publique Routing Slip** : `IRoutingSlipActivity<TArgs>`, `ActivityContext<TArgs>`, `ActivityResult`, `RoutingSlipBuilder`, `SlipEnvelope`, `SlipStep`
- **Exécuteur interne** : `RoutingSlipExecutor<TArgs>` + extensions DI `AddRoutingSlipActivity` / `AddRoutingSlipActivityForTopic`
- **Breaking change v2.0** : suppression de `RouteToNextStageAsync`, `AppSettings.Itinerary` multi-étapes, `FindIndexFromStage`, `__FinalStageCompleted`, bump MAJOR → v2.0
- **`RawMessage` rendu `internal`** (objectif Phase 5 original — exécuté ici avec le bump MAJOR)

---

## Points ouverts traités dans cette phase

| ID | Intitulé | Source | Criticité | Statut |
|----|----------|--------|-----------|--------|
| **RS-1** | Implémentation surface publique Routing Slip | `architecture-routing-slip.md` §5-6 | 🔴 Critique | ⬜ À livrer |
| **RS-2** | `RoutingSlipExecutor<TArgs>` + DI extensions | `architecture-routing-slip.md` §6.6 | 🔴 Critique | ⬜ À livrer |
| **RS-3** | Tests intégration Routing Slip bout-en-bout | DE Review T10 révisé | 🟠 Élevé | ⬜ À livrer |
| **BC-1** | Breaking change v2.0 — suppression APIs saga `BaseConsumer` | DE Review §3.2.5 E4 | 🟠 Élevé | ⬜ À livrer |
| **BC-2** | `RawMessage` → `internal` + bump MAJOR v2.0 | DE Review §3.4 | 🟠 Élevé | ⬜ À livrer |
| ~~D-A~~ | ~~Saga dispersée — `IStageAdvancer`~~ | §3.2 | — | ✅ Fondation livrée 27 avr. |
| ~~D-B~~ | ~~`IMessageTransit` enrichi~~ | §3.4 | — | ✅ Livré Phase 2 |
| ~~D-C~~ | ~~`AzureFunctionMessagingAdapter` découplé~~ | §3.3 | — | ✅ Livré 27 avr. |
| ~~T6~~ | ~~Tests contrat `IMessagingAdapter` + `InMemoryAdapter`~~ | §6.1.1 | — | ✅ Livré 27 avr. |
| ~~T10~~ | ~~Tests `IStageAdvancer` purs~~ | §6.1.1 | — | ✅ Livré 27 avr. |
| ~~T16~~ | ~~Stryker config + pipeline nightly~~ | §6.1.1 | — | ✅ Livré 27 avr. |

---

## Tâche P5-T1 — Surface publique Routing Slip (types additifs) (RS-1)

**Origine :** `architecture-routing-slip.md` §5 et §6 · DE Review §3.2.4

### Contexte

Cette tâche crée la **surface publique** du Routing Slip. Tous les types sont **additifs** — aucune suppression ni modification. La casse arrive en P5-T4. L'ordre additif-d'abord permet de valider le design complet avant de retirer les anciens mécanismes.

### Dossier cible

```
Messaging/RoutingSlip/                        ← dossier existant (E1 livré 27 avr.)
  ← Existants (fondation, gardés) :
  IStageAdvancer.cs                           ← réutilisé internement par RoutingSlipExecutor
  StageAdvancer.cs
  RoutingSlip.cs  StageIdentifier.cs  etc.

  ← Nouveaux (cette tâche) :
  IRoutingSlipActivity.cs                     ← interface publique : votre code métier
  ActivityContext.cs                          ← paramètre de ExecuteAsync()
  ActivityResult.cs                           ← Next() | Complete() | Fault(ex)
  RoutingSlipBuilder.cs                       ← fluent builder côté activateur
  SlipEnvelope.cs   SlipStep.cs   SlipHeader.cs   SlipStepStatus.cs
```

### Contrats à implémenter

```csharp
// IRoutingSlipActivity.cs — ce que le développeur implémente
public interface IRoutingSlipActivity<TArgs> where TArgs : class
{
    Task<ActivityResult> ExecuteAsync(ActivityContext<TArgs> ctx, CancellationToken ct);
}

// ActivityResult.cs — signal retourné au framework
public abstract class ActivityResult
{
    public static ActivityResult Next(Action<IDictionary<string, object>>? enrich = null);
    public static ActivityResult Complete();
    public static ActivityResult Fault(Exception exception);
}

// ActivityContext.cs — contexte fourni par le framework
public sealed class ActivityContext<TArgs> where TArgs : class
{
    public required TArgs   Arguments     { get; init; }
    public IReadOnlyDictionary<string, object> Variables { get; init; }
    public required string  SlipId        { get; init; }
    public string?          CorrelationId { get; init; }
    public required string  StepName      { get; init; }
    public int              StepIndex     { get; init; }
    public int              TotalSteps    { get; init; }
}
```

### Résultat visé

```csharp
// v2.0 — activité métier pure : zéro dépendance EMT
public class ValiderAdmissibiliteActivity : IRoutingSlipActivity<ValiderArgs>
{
    public async Task<ActivityResult> ExecuteAsync(ActivityContext<ValiderArgs> ctx, CancellationToken ct)
    {
        var ok = await _service.ValiderAsync(ctx.Arguments.DossierId, ct);
        if (!ok) return ActivityResult.Fault(new InvalidOperationException("Non admissible"));
        return ActivityResult.Next(vars => vars["DateValidation"] = DateTime.UtcNow);
    }
}
// Testable avec new ValiderAdmissibiliteActivity(stub) — zéro mock Service Bus
```

### Critère de sortie

- [ ] `IRoutingSlipActivity<TArgs>`, `ActivityContext<TArgs>`, `ActivityResult` dans namespace `Messaging.RoutingSlip` (public).
- [ ] `RoutingSlipBuilder` avec `AddStep<TArgs>` et `AddStepTopic<TArgs>` (public).
- [ ] `SlipEnvelope`, `SlipStep`, `SlipHeader`, `SlipStepStatus` (public).
- [ ] `PublicAPI.Unshipped.txt` mis à jour avec les nouveaux types.
- [ ] Build 0 erreur — aucun type existant modifié.

---

## Tâche P5-T2 — `RoutingSlipExecutor<TArgs>` + DI extensions (RS-2)

**Origine :** `architecture-routing-slip.md` §6.6 · DE Review §3.2.4 · dépend P5-T1

### Contexte

L'executor est le **chef d'orchestre interne** : il est `internal`, n'est jamais instancié par le développeur. Il réutilise l'`InMemoryMessagingAdapter` livré en Phase 5 originale pour les tests — c'est exactement le cas d'usage pour lequel il a été créé.

### Responsabilités de `RoutingSlipExecutor<TArgs>`

```
ProcessAsync(SlipEnvelope, CancellationToken)
  1. Vérifier Cursor == stepIndex attendu (idempotence)
  2. Désérialiser Steps[Cursor].Arguments en TArgs
  3. Construire ActivityContext<TArgs>
  4. Appeler IRoutingSlipActivity<TArgs>.ExecuteAsync()
  5. Selon ActivityResult :
       Next()     → Cursor++, Steps[Cursor].Status = Active, publier via IMessageProducer<SlipEnvelope>
       Complete() → CompleteMessageAsync() (settlement EMT existant)
       Fault(ex)  → DeadLetterMessageAsync(ex)
  6. Écrire entrée IJournalProvider
  7. Émettre span ActivitySource : "messaging.routing_slip.step"
  8. Incrémenter métriques routing_slip_*
```

### Extensions DI

```csharp
// Enregistrement côté application — deux surcharges
services.AddRoutingSlipActivity<ValiderAdmissibiliteActivity, ValiderArgs>("queue-valider");
services.AddRoutingSlipActivityForTopic<EnrichirDonneesActivity, EnrichirArgs>(
    topic: "topic-enrichir", consumer: "EnrichirConsumer", action: "Traiter");

// Ce que ça fait internement :
// 1. services.AddScoped<IRoutingSlipActivity<TArgs>, TActivity>()
// 2. services.AddScoped<IRoutingSlipExecutor>( sp => new RoutingSlipExecutor<TArgs>(...) )
```

### Cas de test `RoutingSlipExecutorTests` (Category=Unitaire, InMemoryMessagingAdapter)

```csharp
[Fact] void Execute_AvanceCurseur_EtPublieAuSuivant()
[Fact] void Execute_CompleteMsgAuDernierStep_Next()
[Fact] void Execute_CompleteMsgAuDernierStep_Complete()
[Fact] void Execute_DeadLetterMsg_SiFault()
[Fact] void Execute_PropageVariables_EntreSteps()
[Fact] void Execute_EstIdempotent_SiStepDejaDone() // redelivery
[Fact] void Execute_CursorMismatch_LèveException()  // message routé à la mauvaise étape
[Fact] void Execute_SlipVide_LèveException()
```

### Critère de sortie

- [ ] `RoutingSlipExecutor<TArgs>` `internal` — implémente `IRoutingSlipExecutor`.
- [ ] `AddRoutingSlipActivity<TActivity, TArgs>(queue)` + `AddRoutingSlipActivityForTopic<TActivity, TArgs>(topic, consumer, action?)` dans extensions DI.
- [ ] ≥ 8 tests `RoutingSlipExecutorTests` — `Category=Unitaire`, tous avec `InMemoryMessagingAdapter`.
- [ ] Span `messaging.routing_slip.step` émis et testé via `ActivityListener`.

---

## Tâche P5-T3 — Tests d'intégration Routing Slip bout-en-bout (RS-3)

**Origine :** DE Review T10 révisé · `architecture-routing-slip.md` §8 · dépend P5-T2

### Contexte

Les tests unitaires de P5-T2 valident l'executor en isolation. Cette tâche valide le **flux complet** sur le Service Bus Emulator (infrastructure créée en P4-T1) : activateur → queue 1 → queue 2 → queue 3, avec propagation des variables et idempotence.

### Scénarios obligatoires (`Category=Integration`)

```csharp
public class RoutingSlipIntegrationTests : IClassFixture<ServiceBusEmulatorFixture>
{
    // Scénario 1 — slip 3 étapes Queue séquentiel
    [Fact] Task Slip_3Etapes_Queue_ExecuteComplet()

    // Scénario 2 — slip avec Topic à l'étape 2
    [Fact] Task Slip_TopicEtape2_SubscriptionReçoitLeMessage()

    // Scénario 3 — Variables propagées bout-en-bout
    [Fact] Task Slip_Variables_PropageesEntreSteps()

    // Scénario 4 — Fault à l'étape 1 → DLQ, étapes suivantes non exécutées
    [Fact] Task Slip_FaultEtape1_MessageEnDLQ_SuivantsPasTouches()

    // Scénario 5 — Idempotence : redelivery de l'étape 2 → effet une seule fois
    [Fact] Task Slip_RedeliveryEtape2_EffetUneFois()

    // Scénario 6 — Claim-Check automatique si SlipEnvelope > 256 Ko
    [Fact] Task Slip_GrandPayload_ClaimCheckTransparent()
}
```

### Critère de sortie

- [ ] ≥ 6 tests `Category=Integration` verts sur Service Bus Emulator.
- [ ] Tests ajoutés au pipeline CI nightly (`ci-tests.yml`).
- [ ] Trace complète visible dans les spans `messaging.routing_slip.step` (test via `ActivityListener`).
- [ ] Journal EMT contient une entrée par hop (début + fin de chaque step).

## Tâche P5-T4 — Breaking change v2.0 — suppression des APIs saga de `BaseConsumer` (BC-1 · BC-2)

**Origine :** DE Review §3.2.5 E4 · `architecture-routing-slip.md` §13

> ⚠️ **Cette tâche est irréversible une fois le tag v2.0 publié.** Coordonner avec toutes les équipes consommatrices multi-étapes AVANT d'exécuter le bump MAJOR. Les consumers simples (non-saga) ne sont pas impactés.

### Contexte

Les fondations livrées en Phase 5 originale (E1-E3 : `IStageAdvancer`, délégation `BaseConsumer`) et les nouveaux types P5-T1 + P5-T2 permettent de retirer sans régression les APIs de routing implicite. Cette tâche est la **coupure nette** qui marque v2.0.

### Suppressions (breaking change)

| Élément supprimé | Raison | Remplacé par |
|-----------------|--------|--------------|
| `BaseConsumer<T>.RouteToNextStageAsync()` | Routing implicite couplé à la config | `IRoutingSlipActivity<TArgs>` + `RoutingSlipExecutor` |
| `BaseConsumer<T>.FindIndexFromStage()` | 3 stratégies ambiguës | Curseur `int` dans `SlipEnvelope` |
| `BaseConsumer<T>.ResolveEffectiveCurrentStage()` | Dépend de `FindIndexFromStage` | Supprimé avec |
| `Variables["__FinalStageCompleted"]` flag | État caché dans un dictionnaire libre | `Cursor == Steps.Length` (automatique) |
| `AppSettings.Itinerary[]` multi-étapes | Config global fragile, dérive entre apps | `RoutingSlipBuilder` côté activateur |
| `RawMessage` exposé public | Fuite de l'abstraction Service Bus | Rendu `internal` |

### Procédure

```
1. Vérifier P5-T2 et P5-T3 : tous les tests verts sur InMemory ET Emulator.
2. Annoncer la date du tag v2.0 à toutes les équipes consommatrices.
3. Supprimer RouteToNextStageAsync + FindIndexFromStage + ResolveEffectiveCurrentStage de BaseConsumer.
4. Supprimer la lecture/écriture de Variables["__FinalStageCompleted"].
5. Retirer la section Itinerary[] multi-étapes de AppSettings / ConsumerConfiguration.
6. Rendre RawMessage internal.
7. Bumper le <Version> en 2.0.0 dans EnterpriseMessageTransit.csproj.
8. Mettre à jour PublicAPI.Shipped.txt (types retirés) et PublicAPI.Unshipped.txt.
9. Publier le CHANGELOG v2.0 avec section ### Changements cassants complète.
10. Créer le tag git v2.0.0 et publier le package NuGet.
```

### Critère de sortie

- [ ] `RouteToNextStageAsync`, `FindIndexFromStage`, `ResolveEffectiveCurrentStage` absents de `BaseConsumer`.
- [ ] `__FinalStageCompleted` introuvable dans la codebase (`grep` vide).
- [ ] `AppSettings.Itinerary` supporte uniquement 0 ou 1 entrée pour les consumers simples (ou supprimé selon décision).
- [ ] `RawMessage` rendu `internal` — `PublicAPI.Shipped.txt` mis à jour.
- [ ] `<Version>2.0.0</Version>` dans le `.csproj`.
- [ ] `CHANGELOG.md` section `## [2.0.0]` avec tous les breaking changes documentés.
- [ ] CI verte : 0 erreur de build, tous les tests existants (adaptés à la nouvelle API) passent.

---

## Tâche P5-T5 — Mise à jour Stryker.NET pour les nouveaux modules (T16)

**Origine :** §6.1.1 T16 · Infrastructure livrée Phase 5 originale (27 avr. 2026)

### Contexte

La configuration Stryker existe déjà (`stryker-config.json` + `pipelines/mutation-tests.yml`). Cette tâche **étend les modules ciblés** pour couvrir le nouveau code du Routing Slip natif. Objectif maintenu : ≥ 70 % de mutation score sur les modules critiques.

### Mise à jour des modules ciblés

| Module | Mutants à tuer | Statut |
|--------|----------------|--------|
| `RoutingSlip/RoutingSlipExecutor.cs` | Cursor non incrémenté, mauvais step sélectionné, Next vs Complete inversé | ⬜ Ajouter |
| `RoutingSlip/StageAdvancer.cs` | Inversion `IsFinal`, décalage index | ✅ Déjà configuré |
| `RetryPolicyHandler.Classify` | Mauvaise classification retry vs DLQ | ✅ Déjà configuré |
| `CircuitBreakerManager.RecordFailure` | Seuil `FailureThreshold` inversé | ✅ Déjà configuré |
| `Producer.PublishCoreAsync` — timeout | Timeout désactivé silencieusement | ✅ Déjà configuré |

### Modification de `stryker-config.json`

```json
// Ajouter dans la liste "mutate" :
"Messaging/RoutingSlip/RoutingSlipExecutor.cs"
```

### Critère de sortie

- [ ] `stryker-config.json` mis à jour — `RoutingSlipExecutor.cs` dans la liste `mutate`.
- [ ] Score ≥ 70 % sur `RoutingSlipExecutor` (mesuré après P5-T2).
- [ ] Rapport HTML du premier run archivé en artefact CI.

---

## Tâche P5-T6 — Documentation et guide de migration (v2.0) ✅ Partiellement livré

**Origine :** `architecture-routing-slip.md` §13 · DE Review §3.2.5

### Contexte

Le guide de migration et la section §13 de `architecture-routing-slip.md` ont déjà été mis à jour (11 mai 2026) pour refléter le breaking change v2.0. Cette tâche couvre la documentation complémentaire à maintenir à jour au moment du bump v2.0.

### Documents à mettre à jour lors du bump v2.0 (P5-T4)

| Document | Mise à jour requise |
|----------|--------------------|
| `architecture-routing-slip.md` §13 | ✅ Mis à jour 11 mai 2026 |
| `EMT-DistinguishedEngineerReview.md` §8.3.1 Phase 5 | ✅ Mis à jour 11 mai 2026 |
| `CHANGELOG.md` — section `## [2.0.0]` | ⬜ À rédiger lors du bump |
| `CONTRIBUTING.md` — section « Routing Slip » | ⬜ À mettre à jour |
| `PublicAPI.Shipped.txt` | ⬜ Mis à jour avec P5-T4 |
| `README.md` — exemple de démarrage rapide | ⬜ Exemple avec `IRoutingSlipActivity<TArgs>` |
| `docs/adr/ADR-004-routing-slip.md` (à créer) | ⬜ Documenter la décision breaking change v2.0 |

### Critère de sortie

- [ ] ADR-004 créé — décision breaking change v2.0 signée.
- [ ] `CONTRIBUTING.md` mis à jour avec section « Comment écrire une activité Routing Slip ».
- [ ] `README.md` exemple de démarrage rapide utilise `IRoutingSlipActivity<TArgs>` (plus `RouteToNextStageAsync`).

---

## Résumé des tâches

### Fondations livrées (Phase 5 originale — 27 avril 2026)

| ID | Tâche | Statut |
|----|-------|--------|
| ~~P5-T1 orig~~ | ~~`IStageAdvancer` + `StageAdvancer` + `RoutingSlip` module (9 types)~~ | ✅ Livré 27 avr. |
| ~~P5-T2 orig~~ | ~~`StageAdvancerTests` — 14 tests purs (T10)~~ | ✅ Livré 27 avr. |
| ~~P5-T3 orig~~ | ~~`IMessageTransit` enrichi — 5 propriétés (livré Phase 2)~~ | ✅ Livré |
| ~~P5-T4 orig~~ | ~~`InMemoryAdapter` + suite contrat `IMessagingAdapter` — 10 tests (T6)~~ | ✅ Livré 27 avr. |
| ~~P5-T5 orig~~ | ~~Stryker config + `mutation-tests.yml` (T16)~~ | ✅ Livré 27 avr. |
| ~~P5-T6 orig~~ | ~~`AzureFunctionMessagingAdapter` découplé — namespace + règles archi~~ | ✅ Livré 27 avr. |

### Nouvelle portée v2.0 — à livrer

| ID | Tâche | Priorité | Durée est. | Prérequis | Statut |
|----|-------|----------|------------|-----------|--------|
| P5-T1 | Surface publique Routing Slip — `IRoutingSlipActivity<TArgs>`, `SlipEnvelope`, `RoutingSlipBuilder` | 🔴 Haute | 1-2 semaines | Fondations ✅ | ⬜ À démarrer |
| P5-T2 | `RoutingSlipExecutor<TArgs>` + DI extensions + 8 tests unitaires | 🔴 Haute | 1-2 semaines | P5-T1 | ⬜ À démarrer |
| P5-T3 | Tests intégration bout-en-bout (SB Emulator) — 6 scénarios | 🟠 Haute | 1 semaine | P5-T2 | ⬜ À démarrer |
| P5-T4 | Breaking change v2.0 — suppression APIs saga + bump MAJOR | 🟠 Haute | 1 semaine | P5-T3 + coordination | ⬜ À démarrer |
| P5-T5 | Stryker — ajout `RoutingSlipExecutor` dans modules ciblés | 🟡 Moyenne | ½ jour | P5-T2 | ⬜ À démarrer |
| P5-T6 | Documentation et guide de migration v2.0 | 🟡 Moyenne | ½ jour | P5-T4 | ⬜ À démarrer |

---

## Ordre d'exécution recommandé

```
Semaine 1-2 : TYPES ADDITIFS — aucun risque de régression
  P5-T1  Surface publique : IRoutingSlipActivity<TArgs>, SlipEnvelope, RoutingSlipBuilder
  P5-T2  RoutingSlipExecutor<TArgs> + DI + tests unitaires (InMemoryMessagingAdapter)

Semaine 2-3 : VALIDATION — intégration réelle
  P5-T3  Tests intégration bout-en-bout (Service Bus Emulator)
  P5-T5  Stryker — ajout RoutingSlipExecutor dans les modules ciblés

Semaine 3-4 : COORDINATION — avant breaking change
  Annoncer la date du tag v2.0 à toutes les équipes consommatrices
  Accompagner les équipes dans la migration (architecture-routing-slip.md §13)

Semaine 4-5 : BREAKING CHANGE — après confirmation de toutes les équipes
  P5-T4  Suppression APIs saga + bump MAJOR v2.0
  P5-T6  CHANGELOG v2.0, ADR-004, CONTRIBUTING.md, README.md mis à jour
```

---

## Condition de clôture Phase 5

### Fondations (livrées 27 avril 2026)

1. [x] `IStageAdvancer.Advance()` — fonction pure, 14 tests verts. ✅
2. [x] `BaseConsumer` délègue à `IStageAdvancer` (E3). ✅
3. [x] `InMemoryMessagingAdapter` + 10 tests de contrat `IMessagingAdapter`. ✅
4. [x] Stryker infra créée — pipeline nightly configuré. ✅
5. [x] `AzureFunctionMessagingAdapter` `internal`, namespace `Azure.Functions` séparé. ✅
6. [x] `IMessageTransit` enrichi (`ApplicationProperties`, `DeliveryCount`, `EnqueuedTimeUtc`, `CorrelationId`, `ReplyTo`). ✅

### Nouvelle portée v2.0 (à valider)

7. [ ] `IRoutingSlipActivity<TArgs>`, `ActivityContext<TArgs>`, `ActivityResult` publics et compilant.
8. [ ] `RoutingSlipBuilder` produit un `SlipEnvelope` valide — testé unitairement.
9. [ ] `RoutingSlipExecutor<TArgs>` — 8 tests unitaires verts sur `InMemoryMessagingAdapter`.
10. [ ] ≥ 6 tests d'intégration `Category=Integration` verts sur Service Bus Emulator.
11. [ ] `RouteToNextStageAsync`, `FindIndexFromStage`, `__FinalStageCompleted` absents de la codebase.
12. [ ] `AppSettings.Itinerary` multi-étapes supprimé.
13. [ ] `RawMessage` rendu `internal`.
14. [ ] `<Version>2.0.0</Version>` dans le `.csproj` · `CHANGELOG.md` section `## [2.0.0]` complète.
15. [ ] Stryker score ≥ 70 % sur `RoutingSlipExecutor`.
16. [ ] `PublicAPI.Shipped.txt` à jour — aucun type public supprimé silencieusement.

> **Jalon de décision Phase 5 → Phase 6 :** (a) `IMessageTransit` couvre-t-il 100 % des besoins (zéro cast `RawMessage`) ? (b) Toutes les équipes consommatrices multi-étapes ont-elles migré ou planifié leur migration vers `IRoutingSlipActivity<TArgs>` ? Si non sur l'un des deux, ne pas lancer la Phase 6. — *EMT-DistinguishedEngineerReview §8.3.3*
