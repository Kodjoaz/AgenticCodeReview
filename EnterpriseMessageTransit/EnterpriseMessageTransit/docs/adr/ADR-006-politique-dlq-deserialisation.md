# ADR-006 — Politique de DLQ par raison de désérialisation

## Statut

**Superseded — 8 mai 2026**

> Cette ADR a été invalidée par le breaking change de la session du 8 mai 2026. La décision originale confiait à EMT la responsabilité du settlement (Complete / DeadLetter) lors d'une désérialisation échouée. Après révision architecturale, cette responsabilité a été transférée **au consumer applicatif**, conformément au principe que la bibliothèque de transport ne doit jamais prendre de décision métier sur l'état des messages.
>
> **Nouvelle architecture :**
> - `BaseConsumer.DeserializeMessageAsync<T>()` retourne désormais `DeserializationResult<MessageTransitContext<T>>` (jamais null, jamais de settlement automatique).
> - `TryDeserializeMessageAsync` a été supprimé.
> - Le consumer applicatif inspecte `result.IsSuccess`, `result.FailureReason`, `result.Exception` et décide du settlement (Complete, DeadLetter, Abandon) selon son contexte métier.
>
> **Référence :** voir les recommandations dans `docs/failure-modes.md` — le tableau `DeserializationFailureReason` y est conservé comme guide (non-contraignant) pour les consumers.

~~Accepté~~

## Contexte

`DeserializationResult<T>` et `DeserializationFailureReason` ont été livrés en Phase 1. Trois raisons de défaillance sont définies :

| Raison | Description |
|---|---|
| `Malformed` | JSON invalide, champ obligatoire manquant, type incompatible |
| `Empty` | Message reçu vide (body null ou whitespace) |
| `TooLarge` | Payload dépasse le seuil configuré (ex. > 256 Ko sans claim-check) |

Aucune politique n'a été définie sur le comportement EMT pour chacune : faut-il dead-letter, abandonner (retry), ou ignorer silencieusement ?

## Décision

### Règle par raison

| Raison | Action EMT | Justification |
|---|---|---|
| `Malformed` | **Dead Letter immédiat** (`ImmediateDLQException`) | Un message malformé ne se réparera pas par re-livraison. Le retenter consomme des retries inutilement. Le DLQ preserve le message pour analyse forensic. |
| `Empty` | **Drop silencieux** (Complete + log Warning) | Un message vide n'a aucune valeur métier récupérable. Le dead-letter génère du bruit opérationnel sans bénéfice. Métrique `deserialization_failures_total{reason=Empty}` incrémentée. |
| `TooLarge` | **Dead Letter + alerte** (`ImmediateDLQException` + métrique `deserialization_failures_total{reason=TooLarge}`) | Indique une violation de la convention claim-check (ADR-004). Le message doit être analysé — le DLQ le préserve. L'alerte cible l'équipe productrices pour correction. |

### Seuil `TooLarge`

Configurable via `AppSettings.MaxPayloadSizeBytes` (défaut : `262144` — 256 Ko). Toute charge dépassant ce seuil sans token claim-check est `TooLarge`.

### Comportement sur `CircuitBreakerOpenException` pendant désérialisation

Non applicable — le circuit breaker est activé sur les opérations d'envoi (producer), pas sur la désérialisation (consumer). Les appels Storage pour le download claim-check utilisent un try/catch avec fallback Log + `Malformed`.

## Conséquences (historiques — superseded)

- ~~`BaseConsumer.DeserializeMessageAsync` doit appliquer la politique avant de retourner `null`.~~
- ~~Métriques `deserialization_failures_total{reason}` incrémenter pour chaque raison.~~
- ~~Les implémentations concrètes (`ConsumeAsync`) **ne doivent pas** recevoir un contexte `null` — EMT gère le settlement avant de retourner.~~
- ADR-003 (versioning contrat) reste applicable : un `Malformed` causé par un changement de schéma non rétrocompatible doit d'abord être traité via la politique de migration (N/N-1).

> ℹ️ Les métriques `deserialization_failures_total{reason}` sont toujours incrémentées par EMT — seul le settlement a été transféré au consumer.

## Conditions de révision

- Si le volume de messages `Empty` devient significatif (> 1 % du trafic) → envisager DLQ pour analyse.
- Si la politique RAMQ de rétention DLQ change (actuellement sans limitation explicite) → ajuster.
- Si un nouveau type de raison est introduit dans `DeserializationFailureReason` → cette ADR doit être mise à jour.

## Signataires

| Rôle | Nom | Date |
|---|---|---|
| Architecte responsable | GitHub Copilot | 2026-04-27 |
| Approbation technique | — | En attente |
