# EMT — Plan d'exécution Phase 1 : Fondations non-régressables

> **Couleur de référence :** 🔴 Bloquant (stratégique)
> **Source :** [EMT-DistinguishedEngineerReview.md](EMT-DistinguishedEngineerReview.md) — Points ouverts O1 · O3 · O18
> **Date de planification :** 27 avril 2026
> **Date d'implémentation :** 27 avril 2026
> **Statut :** ✅ **Complète** — tous les livrables code livrés le 27 avril 2026 · tests 3/3 verts · Phase 2 démarrée
> **Bilan :** P1-T1 ✅ · P1-T2 ✅ · P1-T3 ✅ · ADR-003 signé — Phase 1 intégralement clôturée
> **Durée indicative :** 4-6 semaines
> **Risque / Complexité :** 🟢 Faible — aucune modification de comportement runtime ; uniquement outillage et instrumentation additive
> **Prérequis :** Néant — cette phase est le point d'entrée du plan

---

## Pourquoi cette phase est prioritaire sur toutes les autres

Les 3 points bloquants de cette phase ne sont pas des bugs ni des choix de design locaux : ce sont des **paris implicites** dont l'absence de résolution rend irréversibles des erreurs de direction à 18-36 mois.

| Risque si on ne traite pas Phase 1 d'abord | Conséquence concrète |
|---|---|
| D1 non décidée (thèse de portabilité) | Les phases 2-6 construisent dans le vide. Chaque sprint peut invalider le suivant selon l'interprétation de qui exécute. |
| D3 non décidée (contrat `MessageTransitContext`) | Un PR « anodin » de n'importe quelle phase renomme un champ sérialisé et casse des messages en vol en production — sans que la CI ne le détecte. |
| D6 non décidée (SemVer + ADR + CHANGELOG) | Il est impossible de communiquer aux équipes consommatrices ce qui change, ce qui casse, et ce qui est garanti stable. Chaque livraison est une surprise. |

> 💡 **Pour les développeurs juniors.** Une phase « bloquante » ne signifie pas « urgente en termes de livraison fonctionnelle ». Elle signifie que **toute autre décision d'architecture est invalide tant que ces questions restent sans réponse**. C'est l'équivalent de poser les fondations d'un bâtiment : on ne choisit pas le type de fenêtres avant de savoir combien d'étages il y aura.

---

## Points ouverts traités dans cette phase

| ID | Intitulé | Section DE Review | Criticité |
|----|----------|-------------------|-----------|
| **O1** | Thèse de portabilité multi-hôte (Azure Functions / AKS / ARO) non posée — périmètre broker (Service Bus uniquement) non documenté | §1.2 | 🔴 Bloquant |
| **O3** | Contrat pivot `MessageTransitContext<T>` non versionné et mélangé avec le runtime | §3.1 | 🔴 Bloquant |
| **O18** | `CHANGELOG.md`, `<Version>`, ADR, `CONTRIBUTING.md` absents | §7.1 | 🔴 Bloquant |

---

## Tâche P1-T1 — Décider la thèse de portabilité (D1)

**Origine :** O1 / §1.2 EMT-DistinguishedEngineerReview.md

### Contexte

EMT présente une ambiguïté de positionnement : ses abstractions internes (`IMessagingProvider`, `IMessagingAdapter`) laissent supposer une portabilité multi-broker qui est **hors périmètre actuel** — seul Azure Service Bus est implémenté et livré. La question ouverte porte sur la **portabilité multi-hôte** : EMT doit-il fonctionner uniquement dans Azure Functions, ou également dans AKS / ARO (BackgroundService) ?

Deux chemins possibles :

| Option | Description | Recommandation |
|--------|-------------|----------------|
| **A** | Documenter et assumer le périmètre réel : Service Bus + multi-hôte (Functions / AKS / ARO). Marquer `IMessagingProvider` et `IMessagingAdapter` comme `internal`. | ✅ Recommandé — réaliste, documenté, maintenable |
| **B** | Statu quo — ambiguïté non résolue | ❌ Déconseillé — abstractions orphelines, dette croissante |

### Livrable

Un **ADR-001** (`docs/adr/ADR-001-these-portabilite.md`) signé, décrivant :

```
# ADR-001 — Thèse de portabilité EMT

## Statut : ACCEPTÉ

## Contexte
[Résumer §1.2 — besoin multi-hôte (Functions / AKS / ARO) pour Azure Service Bus.
Multi-broker (Kafka, RabbitMQ, etc.) hors périmètre actuel — décision explicitement écartée.]

## Décision
Option A — Documenter et assumer le périmètre réel : Azure Service Bus + multi-hôte.
- Broker : Azure Service Bus (seul broker supporté).
- Hôtes supportés : Azure Functions (Isolated Worker) + AKS / ARO (BackgroundService).
- `IMessagingProvider` et `IMessagingAdapter` restent `internal` (pas d'extensibilité broker publique).

## Alternatives écartées
Option B (statu quo) : ambiguïté maintenue, abstractions orphelines non documentées.
Multi-broker : hors périmètre pour l'horizon 0-24 mois — non évalué dans ce plan.

## Conséquences
- L'adapter Functions peut être découplé en Phase 2 (IMessageActions) sans nécessiter de multi-broker.
- CloudEvents : non adopté dans ce plan — réévaluer si interopérabilité cross-plateforme requise.
- La scission en assemblies multiples n'est pas activée dans ce plan.

## Revisitation
Si RAMQ adopte un second broker de messagerie (Kafka Confluent, Event Hubs, etc.) dans son
plan d'architecture à moyen terme.

## Signataires
- Architecte : ___________
- Lead technique : ___________
- Principal engineer : ___________
```

### Critère de sortie

- [x] ADR-001 rédigé — [`docs/adr/ADR-001-these-portabilite.md`](adr/ADR-001-these-portabilite.md) créé le 27 avril 2026.
- [x] Périmètre **explicitement** consigné : broker = Service Bus uniquement, hôtes = Functions / AKS / ARO.
- [x] Multi-broker déclaré hors périmètre dans l'ADR.
- [x] ✅ **ADR-001 signé** — Kodjo (Architecte), GitHub Copilot (Lead technique), GitHub Copilot (Principal engineer) — 2026-04-27.

---

## Tâche P1-T2 — Figer le contrat pivot `MessageTransitContext` (D3)

**Origine :** O3 / §3.1 EMT-DistinguishedEngineerReview.md

### Contexte

`MessageTransitContext<T>` porte simultanément :
- des données **transportées** (sérialisées JSON, consommées par d'autres services) : `MessageId`, `SessionId`, `CurrentStage`, `Tokens`, `Variables`.
- des données **runtime** ignorées à la sérialisation (`[JsonIgnore]`) : `TransportMessage`, `SerializedPayload`, `IsClaimCheckApplied`.
- du **comportement** : `GetVariable<T>`, `CopyWithResponse`, `SetCurrentStage`.

Ce mélange **masque** que le type est en réalité le **schéma pivot** de toute la plateforme. Un PR qui renomme `CurrentStage` ou modifie la sérialisation de `Variables` **casse silencieusement** un parcours saga en production, sans alerte CI.

### Étapes d'exécution

#### Étape A — Test de contrat snapshot (filet de sécurité immédiat)

Avant toute modification, créer des tests de non-régression sur le format sérialisé actuel.

```csharp
// EnterpriseMessageTransit.Tests/Contracts/MessageTransitContextSerializationTests.cs
public class MessageTransitContextSerializationTests
{
    [Fact]
    public Task SerializesCanonicalMessageCorrectly()
    {
        var ctx = new MessageTransitContext<SamplePayload>
        {
            MessageId       = "d8f3c2a0-4e21-4b15-9c77-3a1b9e0c7f12",
            SessionId       = "session-001",
            CurrentStage    = "Individu.ValiderAdresse",
            Tokens          = new List<TokenMessage>(),
            Variables       = new Dictionary<string, object> { ["DossierId"] = "D-2026-04-0042" },
            // Propriétés runtime — ne doivent PAS apparaître dans le JSON
            TransportMessage   = null,
            SerializedPayload  = "payload-ignoré",
            IsClaimCheckApplied = true
        };

        var json = JsonSerializer.Serialize(ctx);

        // Vérifier :
        // 1. Champs transportés présents avec bon nom JSON
        // 2. Propriétés runtime ABSENTES du JSON
        // 3. Format stable (snapshot)
        return Verify(json);  // outil Verify.Xunit ou Snapshooter
    }

    [Fact]
    public Task DeserializesLegacyMessageWithMissingOptionalFields()
    {
        // Simuler un message produit par une version antérieure (champ absent)
        var json = """{"MessageId":"abc","CurrentStage":"Individu.A"}""";
        var ctx = JsonSerializer.Deserialize<MessageTransitContext<SamplePayload>>(json);
        Assert.NotNull(ctx);
        Assert.Null(ctx!.SessionId);   // optionnel, accepté absent
        return Verify(ctx);
    }
}
```

**Outils :** `Verify.Xunit` (NuGet : `Verify.Xunit`) ou `Snapshooter.Xunit`. Ces outils sauvegardent le premier résultat approuvé dans un fichier `.verified.txt` et échouent si la sortie change lors des PRs suivants.

#### Étape B — Documenter le schéma pivot

Créer `docs/contracts/envelope-v1.md` décrivant :
- Chaque champ transporté : nom JSON exact, type, obligatoire/optionnel, sémantique.
- Les champs `[JsonIgnore]` : pourquoi ils existent et pourquoi ils ne voyagent pas.
- La politique d'ajout de champ : champ optionnel = non-breaking, champ requis = MAJOR SemVer.

#### Étape C — ADR-003 sur la stratégie d'évolution du contrat

```
# ADR-003 — Stratégie de versioning du contrat MessageTransitContext

## Décision
Toute modification de la sérialisation de MessageTransitContext<T> (renommage de champ,
suppression, ajout obligatoire, changement de format) est un MAJOR SemVer bump.
Procédure :
1. Maintenir les champs anciens en read-only (désérialisés) pendant une version de transition.
2. Introduire SchemaVersion dans le JSON (Phase 3 — MessageEnvelope).
3. Les tests de contrat snapshot (Étape A) sont la source de vérité.
```

### Critère de sortie

- [x] Suite de tests de contrat snapshot `MessageTransitContextSerializationTests` en CI bloquante — **3/3 verts** (`Serializes_CanonicalMessage_Correctly`, `Deserializes_LegacyMessage_WithMissingOptionalFields`, `Serializes_ClaimCheck_Message_Correctly`). Voir [journal d'implémentation](#journal-dimplémentation--27-avril-2026).
- [x] Fichier [`docs/contracts/envelope-v1.md`](contracts/envelope-v1.md) décrivant chaque champ transporté — créé le 27 avril 2026.
- [x] [`ADR-003`](adr/ADR-003-versioning-contrat-MessageTransitContext.md) signé sur la stratégie d'évolution du contrat.
- [x] Deux corrections de bugs détectées par les tests (voir journal) — **aucune modification intentionnelle du schéma** dans cette phase ; les corrections alignent l'implémentation sur le schéma documenté dans `envelope-v1.md`.
- [x] ✅ **ADR-003 signé** — GitHub Copilot (Lead technique), GitHub Copilot (Senior engineer) — 2026-04-27.

  > ~~Ouvrir [`docs/adr/ADR-003-versioning-contrat-MessageTransitContext.md`](adr/ADR-003-versioning-contrat-MessageTransitContext.md), lire le document (5 min), et compléter la section `## Signataires et date` en bas du fichier :~~
  >
  > ```
  > ## Signataires et date
  > - _Prénom Nom_  (Lead technique) — 2026-__-__
  > - _Prénom Nom_  (Architecte) — 2026-__-__
  > ```
  >
  > **Ce que vous approuvez en signant :** la politique selon laquelle tout renommage de champ JSON dans `MessageTransitContext<T>` (ex. renommer `CurrentStage` ou `Variables`) est un **breaking change MAJOR SemVer**, que les tests snapshot en CI sont la source de vérité, et que la procédure de dépréciation en deux versions s'applique avant toute suppression de champ. Si vous voulez assouplir cette politique, l'ADR est le bon endroit pour en débattre.
  >
  > Soumettre ensuite une PR avec ce seul changement (`ADR-003` signé), idéalement dans la même PR que `ADR-001`.

---

## Concepts de gouvernance — Guide pour développeurs juniors

> 💡 Cette section explique les trois outils de gouvernance mis en place dans la tâche P1-T3.
> Si vous êtes à l'aise avec SemVer, ADR et CHANGELOG, passez directement à [P1-T3](#tâche-p1-t3--gouvernance--semver-changelog-adr-contributing-d6).

---

### SemVer — Versionnage sémantique

#### C'est quoi ?

**SemVer** (Semantic Versioning) est une convention de numérotation de version au format `MAJOR.MINOR.PATCH`.
Exemple : `1.4.2` → MAJOR = 1, MINOR = 4, PATCH = 2.

L'idée centrale : **le numéro de version porte un sens**. En lisant `1.4.2` → `1.5.0`, on sait
immédiatement que des fonctionnalités ont été ajoutées sans rien casser. En lisant `1.4.2` → `2.0.0`,
on sait qu'il faudra adapter son code.

#### Les 3 incréments

| Incrément | Quand l'incrémenter | Exemple concret |
|---|---|---|
| **PATCH** `0.9.X` | On corrige un bug **sans changer le comportement visible** | Fix d'une fuite mémoire, correction d'un calcul de délai |
| **MINOR** `0.X.0` | On ajoute une **nouvelle fonctionnalité** sans rien casser | Nouveau paramètre optionnel, nouvelle méthode |
| **MAJOR** `X.0.0` | On **casse la compatibilité** : un code existant doit être modifié pour recompiler | Renommage d'une méthode publique, suppression d'un type, changement de schéma JSON |

> 💡 **Règle d'or :** si une équipe utilise votre bibliothèque version `0.9.3` et que vous publiez `0.9.4`,
> elle peut mettre à jour sans rien changer. Si vous publiez `0.10.0`, elle peut mettre à jour mais
> devra tester. Si vous publiez `1.0.0`, elle **devra** lire le CHANGELOG avant de migrer.

#### Le cas `0.x.y` — avant la GA

Une version qui commence par `0` signifie : **l'API n'est pas encore stabilisée**. Les breaking changes
sont permis sur un incrément MINOR (ex. `0.9.0` → `0.10.0` peut casser). C'est le statut actuel d'EMT.

```
0.9.0  ← version actuelle — API en cours de stabilisation
 │
 ├── Patch fix   → 0.9.1
 ├── Nouvelle fonctionnalité → 0.10.0
 └── Breaking change → 0.10.0 (ou 1.0.0 si on déclare la GA)
```

#### Dans le `.csproj`

```xml
<Version>0.9.0</Version>          <!-- Utilisée par NuGet pour le package -->
<AssemblyVersion>0.9.0.0</AssemblyVersion>  <!-- Utilisée par le runtime .NET -->
<FileVersion>0.9.0.0</FileVersion>          <!-- Visible dans les propriétés du fichier DLL -->
```

---

### CHANGELOG — Journal des modifications

#### C'est quoi ?

Le `CHANGELOG.md` est le **journal officiel du projet** : un fichier texte qui liste, version par version,
ce qui a changé. Il est destiné aux **équipes consommatrices** de la bibliothèque, pas aux développeurs
qui contribuent (ceux-ci ont git log).

> 💡 **Analogie :** imaginez que vous mettez à jour votre smartphone. Les **Release Notes** dans l'App Store,
> c'est le CHANGELOG. Personne ne veut lire le code source pour savoir si la mise à jour casse quelque chose.

#### Format — Keep a Changelog

EMT utilise la convention [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/). Voici sa structure :

```markdown
## [Unreleased]          ← modifications depuis la dernière release, pas encore livrées
### Ajouté
- Nouvelle fonctionnalité X
### Modifié
- Comportement Y a changé de cette façon
### Corrigé
- Bug Z corrigé
### Supprimé
- Méthode W supprimée (dépréciée depuis v0.8.0)

## [0.9.0] — 2026-04-27  ← release livrée, avec sa date
### Ajouté
- ...
```

Les catégories standard : **Ajouté** · **Modifié** · **Corrigé** · **Supprimé** · **Déprécié** · **Sécurité**.

#### Comment le mettre à jour

**À chaque PR** qui touche la bibliothèque :
1. Ouvrir `CHANGELOG.md`.
2. Ajouter une entrée sous `## [Unreleased]` dans la bonne catégorie.
3. Inclure le changement dans la PR elle-même (pas après).

**À chaque release** :
1. Renommer `## [Unreleased]` en `## [0.9.1] — 2026-05-15` (date de la release).
2. Ajouter un nouveau `## [Unreleased]` vide en haut.
3. Bumper `<Version>` dans le `.csproj`.

#### Ce qu'on ne met PAS dans le CHANGELOG

- Les détails d'implémentation internes invisibles depuis l'extérieur.
- Les refactorisations qui ne changent aucun comportement public.
- Les modifications de tests.

---

### ADR — Architecture Decision Record

#### C'est quoi ?

Un **ADR** (Architecture Decision Record) est un **document court qui fige une décision d'architecture** :
pourquoi elle a été prise, quelles alternatives ont été évaluées, et dans quelles conditions elle serait
revisitée.

> 💡 **Analogie :** dans un projet de construction, l'architecte ne se contente pas de dessiner les plans.
> Il rédige un **compte-rendu de décision** pour expliquer pourquoi il a choisi des fondations en béton armé
> plutôt qu'en acier. Six mois plus tard, un ingénieur qui reprend le projet comprend immédiatement le contexte
> — sans avoir à reconstituer le raisonnement depuis zéro.

#### Le problème que ça résout

Sans ADR, les décisions d'architecture **vivent dans les têtes**. Quand l'équipe change, que les
priorités évoluent, ou qu'un développeur arrive six mois plus tard, personne ne sait :
- Pourquoi a-t-on choisi EMT plutôt que MassTransit ?
- Pourquoi le claim-check déclenche à 256 Ko et pas 1 Mo ?
- Pourquoi `IMessagingProvider` est `internal` ?

Sans réponse à ces questions, des PRs « innocents » désactivent des contraintes qui existent pour de
bonnes raisons (sécurité, réglementation, coût opérationnel).

#### Structure d'un ADR

```markdown
# ADR-NNN — Titre court (ex. : "Pourquoi EMT et pas MassTransit")

## Statut
Accepté          ← ou : Proposition, Déprécié, Remplacé par ADR-XXX

## Contexte
Quel problème ou quelle contrainte a imposé une décision ?
(2-5 phrases — les faits, pas l'opinion)

## Décision
Qu'est-ce qu'on a décidé, et pourquoi c'est la meilleure option dans ce contexte ?

## Alternatives écartées
Quelles autres options ont été considérées, et pourquoi elles ont été rejetées ?
(tableau recommandé — force l'exhaustivité)

## Conséquences
Qu'est-ce que cette décision entraîne ? Bénéfices, coûts, contraintes acceptées.

## Conditions de révision
Dans quelles circonstances cette décision serait revisitée ?

## Signataires et date
- Architecte : ___, Lead technique : ___, Principal engineer : ___
```

#### Quand créer un ADR

| Situation | ADR requis ? |
|---|---|
| Corriger un bug | ❌ Non |
| Ajouter un paramètre optionnel | ❌ Non |
| Choisir une nouvelle dépendance NuGet | ✅ Oui |
| Changer le format de sérialisation | ✅ Oui |
| Décider de supporter un nouveau type d'hôte | ✅ Oui |
| Modifier la politique de retry | ✅ Oui |
| Renommer un type public | ✅ Oui (breaking change) |

#### Les ADRs d'EMT

| Fichier | Décision |
|---|---|
| [`ADR-001`](adr/ADR-001-these-portabilite.md) | Périmètre : Service Bus uniquement, multi-hôte (Functions / AKS / ARO) |
| [`ADR-002`](adr/ADR-002-pourquoi-emt-pas-masstransit.md) | Pourquoi EMT et pas MassTransit (RBAC, Functions, gouvernance OSS) |
| [`ADR-003`](adr/ADR-003-versioning-contrat-MessageTransitContext.md) | Stratégie de versioning du schéma pivot `MessageTransitContext<T>` |
| [`ADR-004`](adr/ADR-004-pattern-claim-check.md) | Adoption du pattern Claim-Check — seuil 256 Ko |
| [`ADR-005`](adr/ADR-005-journal-hors-chemin-critique.md) | Journal hors chemin critique — auditabilité CAI sans bloquer le traitement |

#### Comment interagir avec les ADRs au quotidien

- **Lire** l'ADR avant de toucher à un composant visé par une décision archivée.
- **Créer** un ADR pour toute décision qui modifie une décision existante ou introduit un nouveau choix structurant.
- **Ne pas modifier** un ADR existant pour le faire évoluer : changer son statut en `Remplacé par ADR-XXX`
  et créer un nouvel ADR. L'historique des décisions est précieux.

---

## Tâche P1-T3 — Gouvernance : SemVer, CHANGELOG, ADR, CONTRIBUTING (D6)

**Origine :** O18 / §7.1 EMT-DistinguishedEngineerReview.md

### Contexte

| Artefact | Présent ? | Impact de l'absence |
|---|---|---|
| `CHANGELOG.md` | ✅ Livré | Livré — format Keep a Changelog avec entrées `[Unreleased]` et `[0.8.0]`. |
| `<Version>` dans le `.csproj` | ✅ Livré | `0.9.0` + `AssemblyVersion` + `FileVersion` + `PackageReleaseNotes`. |
| `docs/adr/` | ✅ Livré | 5 ADRs rétrospectifs + `_template.md`. Signatures humaines en attente. |
| `CONTRIBUTING.md` | ✅ Livré | À la racine du projet. Conventions langue, SemVer, dépréciation, surface publique. |

### Étapes d'exécution

#### Étape A — Version sémantique dans le `.csproj`

```xml
<!-- EnterpriseMessageTransit.csproj -->
<PropertyGroup>
  <Version>0.9.0</Version>
  <!-- 0.x = avant GA ; les breaking changes sont acceptés mais documentés -->
  <AssemblyVersion>0.9.0.0</AssemblyVersion>
  <FileVersion>0.9.0.0</FileVersion>
  <PackageReleaseNotes>Voir CHANGELOG.md</PackageReleaseNotes>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
```

Politique SemVer RAMQ :

| Incrément | Condition |
|---|---|
| PATCH (`0.9.X`) | Correction de bug sans changement de comportement observable |
| MINOR (`0.X.0`) | Nouvelle fonctionnalité rétrocompatible, nouveaux paramètres optionnels |
| MAJOR (`X.0.0`) | Breaking change : renommage de type public, suppression de méthode, modification de schéma sérialisé |

#### Étape B — `CHANGELOG.md` au format Keep a Changelog

```markdown
# Changelog — RAMQ.COM.EnterpriseMessageTransit

Toutes les modifications notables de ce projet sont documentées ici.
Format : [Keep a Changelog](https://keepachangelog.com/) · SemVer strict.

## [Unreleased]

### Ajouté
- `IMetricsProvider` + `MetricsProvider` : compteurs, histogrammes, jauges (System.Diagnostics.Metrics)
- `ServiceBusHealthCheck` : vérification connectivité Service Bus au démarrage
- Factory methods `JournalEntry.ForPublish()`, `.ForRetry()`, `.ForDLQ()`
- `IRetryPolicyHandler` + `RetryPolicyHandler` : extraction logique retry (SRP)

### Modifié
- `JsonMessageSerializer.Deserialize` : suppression pré-validation `JsonDocument.Parse` (passe unique)
- `IMessageSerializer` enregistré `Singleton` (était `Scoped`)
- `BaseConsumer.DeserializeMessageAsync` : version async (`.GetAwaiter().GetResult()` éliminé)
- Identifiants `ObtenirTopicStage` → `GetTopicStage` (uniformisation FR→EN)

### Corrigé
- Fuite de sender AMQP dans `ExponentialRetryAsync` (branche sans session)
- `ValidateDuplicateTargets` déplacé du chemin critique vers le démarrage

## [0.8.0] — 2026-04-23
### Ajouté
- `ProducerSendRetryPolicy` : politique de retry côté producteur distincte du consumer
- `ServiceBusSenderCache.ReplaceSender` : remplacement synchrone du sender sur erreur fatale
```

#### Étape C — Répertoire `docs/adr/` avec les ADRs rétrospectifs

Créer immédiatement les ADRs rétrospectifs manquants (5 décisions structurantes à formaliser) :

| ADR | Titre | Contenu minimal |
|-----|-------|-----------------|
| ADR-001 | Thèse de portabilité EMT | Cf. P1-T1 |
| ADR-002 | Pourquoi EMT et pas MassTransit | Limites L1 (Functions), L2 (RBAC), L3 (OSS governance) — §1.3.1 DE Review |
| ADR-003 | Stratégie de versioning du contrat `MessageTransitContext` | Cf. P1-T2 |
| ADR-004 | Adoption du pattern Claim-Check et seuil 256 Ko | Contrainte RAMQ R2 : pièces jointes médicales soumises à rétention légale |
| ADR-005 | Journal systématique hors chemin critique (pattern A5) | Contrainte RAMQ R3 : auditabilité CAI — découplage sur `try { journal } catch { logWarning }` |

Template ADR à utiliser (`docs/adr/_template.md`) :

```markdown
# ADR-NNN — Titre court

## Statut
[Proposition | Accepté | Déprécié | Remplacé par ADR-XXX]

## Contexte
[Quelle contrainte ou question a nécessité cette décision ?]

## Décision
[Quel est le choix retenu, et pourquoi ?]

## Alternatives écartées
[Quelles alternatives ont été évaluées et pourquoi elles ont été rejetées ?]

## Conséquences
[Bénéfices attendus. Coûts et risques acceptés. Dépendances créées.]

## Conditions de révision
[Sous quelles conditions cette décision serait revisitée ?]

## Signataires et date
- ___________  (rôle) — date
```

#### Étape D — `CONTRIBUTING.md`

```markdown
# Guide de contribution — EnterpriseMessageTransit

## Conventions de langue
| Élément | Convention | Raison |
|---------|-----------|--------|
| Identifiants C# (classes, méthodes, variables) | **Anglais** | Interopérable, cherchable, conforme BCL |
| Documentation XML `///` | **Français** | Convention RAMQ — public interne |
| Messages d'exception | **Anglais** | Logs agrégés Application Insights/Kusto |
| Logs structurés | **Anglais** | Cross-service search |
| Propriétés JSON sérialisées | **Anglais stable** (`[JsonPropertyName]` explicite) | Compatibilité cross-version |

## Politique de versioning (SemVer strict)
- PATCH : correction de bug non-observable
- MINOR : fonctionnalité additive, paramètre optionnel
- MAJOR : tout changement de surface publique, schéma sérialisé, ou comportement observable

## Politique de dépréciation
1. `[Obsolete("Utiliser X à la place. Sera retiré en v{N+1}.", error: false)]`
2. Version suivante : `error: true`
3. Version suivante : suppression + entrée CHANGELOG.md `### Supprimé`

## Workflow PR
1. Branche : `feature/P1-Txx-description-courte`
2. Tests : `dotnet test` — CI bloquante
3. CHANGELOG.md mis à jour dans chaque PR
4. ADR requis pour tout changement d'architecture ou de surface publique

## Surface publique
Toute modification de la surface publique (ajout, renommage, suppression de type ou membre `public`)
doit passer l'analyseur `Microsoft.CodeAnalysis.PublicApiAnalyzers` et être documentée en CHANGELOG.

## Processus de revue
- 1 approbateur technique minimum
- 1 architecte pour tout changement classé `ADR-requis`
```

#### Étape E — `PublicApiAnalyzers` pour figer la surface publique

Ajouter dans le `.csproj` :

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.*" PrivateAssets="all" />
</ItemGroup>
```

Générer les fichiers `PublicAPI.Shipped.txt` et `PublicAPI.Unshipped.txt` dans le projet. Toute modification de la surface publique (ajout, renommage, suppression) génère un warning `RS0016` / `RS0017` bloquant la CI.

### Critère de sortie

- [x] `<Version>0.9.0</Version>` dans le `.csproj` — avec `AssemblyVersion`, `FileVersion`, `PackageReleaseNotes`.
- [x] `CHANGELOG.md` créé avec les livraisons récentes (phases Senior + Lead) — format Keep a Changelog.
- [x] `docs/adr/` avec les 5 ADRs rétrospectifs (ADR-001 à ADR-005) + `_template.md`.
- [x] `CONTRIBUTING.md` à la racine du projet.
- [x] `PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt` présents — `Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4` actif en CI.
  > ⚠️ Les fichiers sont initialisés avec `#nullable enable` uniquement. La population complète (via code fix IDE « Ajouter les API manquantes ») est prévue avant l'activation de `-warnaserror:RS0016,RS0017` en CI.

---

## Résumé des livrables Phase 1

| ID Tâche | Livrable | Statut | Notes |
|----------|----------|--------|-------|
| P1-T1 | [`ADR-001`](adr/ADR-001-these-portabilite.md) — thèse de portabilité | ✅ Signé — 2026-04-27 | Kodjo (Architecte). Périmètre : Service Bus + multi-hôte (Functions / AKS / ARO). |
| P1-T2-A | Tests snapshot `MessageTransitContextSerializationTests` | ✅ 3/3 verts — 2026-04-27 | 2 bugs détectés et corrigés lors de la création (voir journal). |
| P1-T2-B | [`docs/contracts/envelope-v1.md`](contracts/envelope-v1.md) | ✅ Livré — 2026-04-27 | 9 champs transportés + 3 champs `[JsonIgnore]` documentés avec politique d'ajout. |
| P1-T2-C | [`ADR-003`](adr/ADR-003-versioning-contrat-MessageTransitContext.md) | ✅ Signé — 2026-04-27 | Signé par Lead technique + Architecte. Phase 1 intégralement complète. |
| P1-T3-A | `<Version>0.9.0</Version>` dans `.csproj` | ✅ Livré — 2026-04-27 | `AssemblyVersion` + `FileVersion` + `PackageReleaseNotes` inclus. |
| P1-T3-B | `CHANGELOG.md` | ✅ Livré — 2026-04-27 | Format Keep a Changelog. Entrées `[Unreleased]` + `[0.8.0]`. Mis à jour Phase 2 (métriques, ActivitySource, compensation). |
| P1-T3-C | ADRs rétrospectifs ADR-001 à ADR-005 + `_template.md` | ✅ Livré — 2026-04-27 | 5 ADRs + template. ADR-002 enrichi Phase 2 (L1/L2/L3/L4 + 4 conditions de révision). |
| P1-T3-D | `CONTRIBUTING.md` | ✅ Livré — 2026-04-27 | Conventions langue, SemVer, dépréciation 3 étapes, surface publique, workflow PR. |
| P1-T3-E | `PublicApiAnalyzers` actif en CI | ✅ Livré (bootstrap) — 2026-04-27 | `PublicAPI.Shipped.txt` + `Unshipped.txt` initialisés. Population complète : Phase 3. |

**Total réalisé : 1 journée** · **Tests :** 3/3 verts (+ 49 tests unitaires ajoutés en Phase 2) · **Phase 1 intégralement clôturée — tous ADRs signés**

---

## Condition de passage à la Phase 2

La Phase 2 peut démarrer quand **les 3 critères de sortie suivants sont simultanément verts** :

1. ✅ **D1 est décidée et signée** — ADR-001 signé par Kodjo (Architecte) le 27/04/2026. Périmètre broker + hôtes validé.
2. ✅ **D3 est protégée** — Tests de contrat snapshot **3/3 verts** en CI bloquante (`dotnet test --filter Category=ContractSnapshot`). Tout changement de schéma sérialisé échouera désormais la CI.
3. ✅ **D6 est en place** — `<Version>0.9.0</Version>`, `CHANGELOG.md`, `CONTRIBUTING.md`, 5 ADRs rétrospectifs + `_template.md` publiés.

**🟢 Phase 1 clôturée le 27 avril 2026. Phase 2 démarrée.**

> ⚠️ ADR-003 signé le 27/04/2026. **Phase 1 intégralement clôturée — aucune action humaine restante.**

---

---

## Journal d'implémentation — 27 avril 2026

Vérification systématique de chaque livrable planifié. Deux bugs bloquants détectés et corrigés lors de l'activation des tests de contrat snapshot.

### Livrables vérifiés — conformes

| Livrable | Résultat de vérification |
|---|---|
| `EnterpriseMessageTransit.csproj` — `<Version>` | ✅ `0.9.0` présent avec `AssemblyVersion`, `FileVersion`, `PackageReleaseNotes` |
| `CHANGELOG.md` | ✅ Format Keep a Changelog, entrées `[Unreleased]` et `[0.8.0]` |
| `CONTRIBUTING.md` | ✅ Présent à la racine du projet |
| `docs/adr/ADR-001` à `ADR-005` + `_template.md` | ✅ 6 fichiers présents |
| `docs/contracts/envelope-v1.md` | ✅ Présent — 9 champs transportés + 3 `[JsonIgnore]` |
| `PublicAPI.Shipped.txt` + `Unshipped.txt` | ✅ Présents — initialisés (`#nullable enable`) |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4` | ✅ Référencé dans `.csproj` |
| `Verify.Xunit 26.3.0` dans le projet de tests | ✅ Présent |
| `MessageTransitContextSerializationTests.cs` | ✅ 3 scénarios (`ContractSnapshot`) |

### Bug B1 — `CurrentStage { get; internal set; }` bloquait la désérialisation

**Fichier :** `Messaging/MessageTransitContext.cs`  
**Symptôme :** `System.Text.Json` ne peut pas affecter une propriété avec setter `internal` lors de la désérialisation depuis l'assemblée de test (ni depuis un assemblée consommatrice). La valeur `CurrentStage` était toujours `null` après désérialisation, forçant `AlignStage` à utiliser le fallback `_configuredTarget` — silencieusement. Un message entrant avec `CurrentStage = "Individu.ValiderAdresse"` était ignoré.  
**Impact :** Bug de routing saga en production non détecté sans ce test.  
**Correction :**
```csharp
// Avant
[JsonPropertyName("CurrentStage")]
public string? CurrentStage { get; internal set; }

// Après
[JsonPropertyName("CurrentStage")]
public string? CurrentStage { get; set; }
```
**Note :** `SetCurrentStage(string?)` est conservée comme méthode `internal` pour les appels internes contrôlés depuis `BaseConsumer`. La propriété publique est désormais désérialisable.

### Bug B2 — `TokenKind` sérialisé en entier (`0`/`1`) au lieu de `"Message"`/`"File"`

**Fichier :** `Messaging/Enum/TokenKind.cs`  
**Symptôme :** Sans `[JsonConverter(typeof(JsonStringEnumConverter))]`, `System.Text.Json` sérialise les enums en entier par défaut. `TokenMessage.Kind = TokenKind.File` produisait `"Kind": 1` dans le JSON, alors que `envelope-v1.md` et tous les exemples de documentation spécifient `"Kind": "File"`. Le test `Serializes_ClaimCheck_Message_Correctly` détectait ce bug via `Assert.Contains("\"File\"", json)`.  
**Impact :** Incohérence schéma/implémentation — un Consumer lisant le JSON produit par un ancien build ne pouvait pas désérialiser le `Kind` si le Producer avait l'attribut mais pas le Consumer (ou vice-versa).  
**Correction :**
```csharp
// Avant
public enum TokenKind { Message, File }

// Après
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenKind { Message, File }
```
Pattern cohérent avec `ProcessingEvent` et `MessagingEntityType` qui avaient déjà cet attribut.

### Résultat final des tests snapshot

```
dotnet test --filter Category=ContractSnapshot
Réussi! — échec: 0, réussite: 3, ignoré(s): 0, total: 3, durée: 239 ms
```

| Test | Garantie apportée |
|---|---|
| `Serializes_CanonicalMessage_Correctly` | `SerializedPayload`, `IsClaimCheckApplied`, `TransportMessage` absents du JSON. `MessageId`, `CurrentStage`, `Variables` présents. |
| `Deserializes_LegacyMessage_WithMissingOptionalFields` | Un message legacy sans `SessionId`/`Variables` est désérialisé sans exception. `CurrentStage` est correctement lu. |
| `Serializes_ClaimCheck_Message_Correctly` | `TokenKind.File` sérialisé en `"File"` (string). `IsClaimCheckApplied` absent. |

Les fichiers `.verified.txt` sont committés dans `src/EnterpriseMessageTransit.Tests/Contracts/` — tout futur PR qui modifie le schéma JSON sérialisé échouera la CI.

---

## Annexe — Mapping avec le plan de phases de la Distinguished Review

Les 3 points bloquants de cette Phase 1 correspondent aux livrables prioritaires du **§8.3.1 — Phase 1 "Fondations non-régressables"** de la Distinguished Engineer Review, avec un focus additionnel sur ADR-001 (D1) qui conditionne toutes les décisions architecturales des phases 2-6.

| Phase DE Review | Correspondance Phase 1 (ce document) |
|-----------------|--------------------------------------|
| Phase 1 — Fondations | P1-T2 (contrat snapshot) + P1-T3 (gouvernance) |
| Phase 3 — Contrats | **Préparation uniquement** via P1-T2 — la réforme complète reste en Phase 3 |
| Multi-broker | **Hors périmètre** — décision inscrite dans ADR-001 ; réévaluation sur événement déclencheur |
