# ADR-003 — Stratégie de versioning du contrat `MessageTransitContext<T>`

## Statut

Accepté

## Contexte

`MessageTransitContext<TMessage>` est le **schéma pivot** de la plateforme. Il transporte des données
sérialisées en JSON entre des services potentiellement à des versions différentes (Producer d'une version,
Consumer d'une version antérieure ou postérieure).

Le type mélange actuellement :
- Champs **transportés** (sérialisés JSON) : `MessageId`, `SessionId`, `CurrentStage`, `Tokens`, `Variables`, etc.
- Champs **runtime** (`[JsonIgnore]`) : `TransportMessage`, `SerializedPayload`, `IsClaimCheckApplied`.
- **Comportement** : `GetVariable<T>`, `CopyWithResponse`, `SetCurrentStage`.

Sans protection, un PR qui renomme un champ sérialisé ou modifie le format de `Variables` peut casser
silencieusement un parcours saga en production, sans que la CI ne le détecte.

## Décision

1. **Toute modification de la sérialisation** de `MessageTransitContext<T>` (renommage de champ JSON,
   suppression, ajout de champ obligatoire, changement de format) est un **MAJOR SemVer bump**.

2. **Procédure de transition** pour les champs supprimés :
   - Version N : déprécier le champ (`[Obsolete]`), le maintenir désérialisable (read-only).
   - Version N+1 : supprimer le champ, documenter dans `CHANGELOG.md §Supprimé`.

3. **Filet de sécurité CI** : des tests de contrat snapshot (`MessageTransitContextSerializationTests`)
   constituent la source de vérité. Tout changement de sortie JSON échoue la CI.

4. **SchemaVersion** : l'introduction d'un champ `SchemaVersion` dans le JSON est prévue en **Phase 3**
   (migration vers `MessageEnvelope`). Elle n'est **pas** réalisée dans cette phase — observer uniquement.

5. **Règle d'ajout** :
   - Champ optionnel (nullable, valeur par défaut) : MINOR SemVer.
   - Champ obligatoire : MAJOR SemVer.

## Alternatives écartées

| Alternative | Raison du rejet |
|---|---|
| **Versionner le type** (`MessageTransitContextV2`) | Crée une prolifération de types et complique les mappings dans les Consumers existants. |
| **Ne pas versionner** (statu quo) | Un rename silencieux en PR casse la production sans alerte. Inacceptable pour un schéma pivot. |
| **Schéma JSON séparé (JSON Schema / OpenAPI)** | Sur-ingénierie pour l'horizon actuel. Utile si EMT évolue vers un hub inter-organisations. |

## Conséquences

- Les tests snapshot (`Verify.Xunit`) sont **bloquants en CI** dès la Phase 1.
- Toute PR touchant `MessageTransitContext<T>` doit être accompagnée d'une mise à jour du fichier
  `docs/contracts/envelope-v1.md`.
- La réforme complète du type (séparation transport/runtime/comportement) est planifiée en **Phase 3**.

## Conditions de révision

- Migration vers `MessageEnvelope` (Phase 3) — à ce moment, cet ADR est remplacé par ADR-003-v2.
- Adoption de CloudEvents comme format d'enveloppe (conditionnel à ADR-001 révision).

## Signataires et date

- GitHub Copilot (Lead technique) — 2026-04-27
- GitHub Copilot (Senior engineer) — 2026-04-27
