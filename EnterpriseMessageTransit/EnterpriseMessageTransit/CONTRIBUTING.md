# Guide de contribution — EnterpriseMessageTransit

> Ce document s'adresse aux développeurs qui contribuent à la bibliothèque `RAMQ.COM.EnterpriseMessageTransit`.
> Pour l'intégration dans une application consommatrice, voir [docs/Vue d'ensemble.md](docs/Vue%20d'ensemble.md).

---

## 1. Conventions de langue

| Élément | Convention | Raison |
|---|---|---|
| Identifiants C# (classes, méthodes, variables) | **Anglais** | Interopérable, cherchable, conforme BCL (.NET) |
| Documentation XML `///` | **Français** | Convention RAMQ — public interne |
| Messages d'exception | **Anglais** | Logs agrégés dans Application Insights / Kusto — recherche cross-service |
| Logs structurés (`ILogger`) | **Anglais** | Cross-service search |
| Propriétés JSON sérialisées | **Anglais stable** avec `[JsonPropertyName]` explicite | Compatibilité cross-version du schéma (ADR-003) |
| Documents dans `docs/` | **Français** | Audience interne RAMQ |

---

## 2. Politique de versioning (SemVer strict)

Voir `CHANGELOG.md` pour les incréments passés.

| Incrément | Condition | Exemples |
|---|---|---|
| `PATCH` (`0.9.X`) | Correction de bug sans changement de comportement observable | Fix fuite AMQP, fix calcul délai |
| `MINOR` (`0.X.0`) | Fonctionnalité additive rétrocompatible, paramètre optionnel | Nouveau compteur metrics, nouvelle overload nullable |
| `MAJOR` (`X.0.0`) | Tout changement de surface publique, schéma sérialisé, ou comportement observable | Renommage de champ JSON, suppression de méthode publique |

> **Règle schéma :** toute modification de la sérialisation de `MessageTransitContext<T>` est un **MAJOR** —
> voir [ADR-003](docs/adr/ADR-003-versioning-contrat-MessageTransitContext.md) et
> [docs/contracts/envelope-v1.md](docs/contracts/envelope-v1.md).

---

## 3. Politique de dépréciation

| Étape | Version | Action | Effet compilateur |
|---|---|---|---|
| 1 | N | `[Obsolete("Utiliser X. Sera retiré en vN+2.", error: false)]` | ⚠️ Warning CS0618 |
| 2 | N+1 | Passer `error: true` | ❌ Erreur CS0619 |
| 3 | N+2 | Supprimer + `### Supprimé` dans `CHANGELOG.md` | — |

**Exemple — dépréciation d'une méthode sync au profit d'une variante async :**

```csharp
// Étape 1 (version N) — warning uniquement
[Obsolete("Utiliser DeserializeMessageAsync à la place. Sera retiré en v0.8.", error: false)]
public MyResult DoSomething() { ... }

// Étape 2 (version N+1) — erreur de compilation
[Obsolete("Utiliser DeserializeMessageAsync à la place. Sera retiré en v0.8.", error: true)]
public MyResult DoSomething() { ... }

// Étape 3 (version N+2) — méthode supprimée, entrée dans CHANGELOG.md
```

> **Règle :** ne jamais sauter d'étape. Un membre en `error: true` doit rester **une version complète** avant suppression pour permettre aux consommateurs de s'adapter.

---

## 4. Surface publique figée

La surface publique est contrôlée par `Microsoft.CodeAnalysis.PublicApiAnalyzers` via les fichiers :
- `PublicAPI.Shipped.txt` — API considérée livrée (stable).
- `PublicAPI.Unshipped.txt` — API ajoutée depuis la dernière release.

**Workflow :**

```bash
# Après avoir ajouté un type ou membre public, construire pour voir les avertissements RS0016 :
dotnet build

# Appliquer le correctif automatique (ajoute les entrées dans Unshipped.txt) :
dotnet build /p:GenerateDocumentationFile=true
# OU : utiliser le code fix Visual Studio "Add X to public API"
```

> **Règle :** toute PR qui **ajoute, renomme ou supprime** un type ou membre `public` doit mettre à jour
> `PublicAPI.Unshipped.txt` (ajout) ou `PublicAPI.Shipped.txt` (suppression) et documenter dans `CHANGELOG.md`.

---

## 5. Tests

```bash
# Exécuter tous les tests
dotnet test

# Exécuter uniquement les tests de contrat snapshot (CI bloquante)
dotnet test --filter "Category=ContractSnapshot"

# Approuver un snapshot modifié (après vérification manuelle du diff)
dotnet verify accept
```

**Règle :** les tests de contrat snapshot (`MessageTransitContextSerializationTests`) sont **bloquants en CI**.
Tout changement de la sortie JSON doit être approuvé manuellement avant que la CI passe.

---

## 6. Workflow PR

| Étape | Obligation |
|---|---|
| Branche | `feature/P{phase}-T{tâche}-description-courte` ou `fix/description-courte` |
| Tests | `dotnet test` — CI bloquante |
| `CHANGELOG.md` | Mise à jour dans chaque PR (section `[Unreleased]`) |
| `PublicAPI.Unshipped.txt` | Mise à jour si la surface publique change |
| ADR | Requis pour tout changement d'architecture ou de surface publique (`docs/adr/ADR-NNN-*.md`) |
| Reviewer | 1 approbateur technique minimum |
| Architecte | Requis pour tout changement classé `ADR-requis` |

---

## 7. Structure du projet

```
EnterpriseMessageTransit/
├── Configuration/          # AppSettings, EndpointSettings, DI extensions
├── Exceptions/             # Exceptions typées (DLQ, retry, configuration)
├── Messaging/
│   ├── Consumer/           # BaseConsumer<T>, IMessageConsumer<T>
│   ├── Enum/               # OperationMode, TokenKind, MessagingEntityType…
│   ├── Producer/           # Producer<T>, IMessageProducer<T>, PublishOptions
│   └── Providers/          # IMessagingProvider, IJournalProvider, IMetricsProvider…
│       └── Azure/          # Implémentations Azure Service Bus
├── Serialization/          # IMessageSerializer, JsonMessageSerializer
├── docs/
│   ├── adr/                # Architecture Decision Records (ADR-001 à ADR-NNN)
│   └── contracts/          # Schémas de contrat (envelope-v1.md…)
├── CHANGELOG.md
├── CONTRIBUTING.md         # Ce fichier
├── PublicAPI.Shipped.txt
└── PublicAPI.Unshipped.txt
```

---

## 8. Architecture Decision Records (ADR)

Tout changement d'architecture doit être accompagné d'un ADR dans `docs/adr/`.
Utiliser le template `docs/adr/_template.md`.

ADRs existants :

| ADR | Titre | Statut |
|---|---|---|
| [ADR-001](docs/adr/ADR-001-these-portabilite.md) | Thèse de portabilité EMT | Accepté |
| [ADR-002](docs/adr/ADR-002-pourquoi-emt-pas-masstransit.md) | Pourquoi EMT et pas MassTransit | Accepté |
| [ADR-003](docs/adr/ADR-003-versioning-contrat-MessageTransitContext.md) | Stratégie de versioning du contrat `MessageTransitContext<T>` | Accepté |
| [ADR-004](docs/adr/ADR-004-pattern-claim-check.md) | Adoption du pattern Claim-Check et seuil 256 Ko | Accepté |
| [ADR-005](docs/adr/ADR-005-journal-hors-chemin-critique.md) | Journal systématique hors chemin critique (pattern A5) | Accepté |
| [ADR-006](docs/adr/ADR-006-politique-dlq-deserialisation.md) | Politique de DLQ par raison de désérialisation | Accepté |
| [ADR-007](docs/adr/ADR-007-frontiere-public-internal.md) | Frontière public/internal de l'assembly EMT | Accepté |
| [ADR-008](docs/adr/ADR-008-decouplage-multi-hote.md) | Découplage multi-hôte : EMT sur Azure Functions, Worker Service et AKS/ARO | Accepté |

---

## 9. Modes de défaillance et compensation

Voir [docs/failure-modes.md](docs/failure-modes.md) pour l'inventaire complet des modes de défaillance,
les stratégies de compensation (retry, DLQ, circuit breaker) et les exemples de code.

> **Règle :** tout nouveau scénario de défaillance ajouté à l'assembly doit être documenté dans `failure-modes.md`
> et accompagné d'un test unitaire `[Trait("Category","Unitaire")]`.
