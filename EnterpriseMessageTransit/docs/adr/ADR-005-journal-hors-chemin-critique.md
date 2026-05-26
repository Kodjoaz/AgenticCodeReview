# ADR-005 — Journal systématique hors chemin critique (pattern A5)

## Statut

Accepté

## Contexte

La contrainte réglementaire RAMQ R3 impose une **auditabilité CAI** : chaque opération de publication
et de consommation doit être tracée dans le Message Transit Journal (Azure Table Storage).

La question architecturale est : **que se passe-t-il si l'écriture du journal échoue ?**

Deux approches s'opposent :

| Approche | Description |
|---|---|
| **Journal bloquant** | L'opération (publish / complete / DLQ) échoue si l'écriture du journal échoue. |
| **Journal hors chemin critique** | L'opération réussit même si l'écriture du journal échoue. L'erreur est loguée en warning. |

## Décision

**Journal hors chemin critique (pattern A5)** :

```csharp
try
{
    await journalProvider.WriteAsync(entry, cancellationToken);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Échec d'écriture du journal — opération non bloquée.");
}
```

L'écriture du journal est découplée du résultat de l'opération métier. Un message peut être
complété même si le journal est temporairement indisponible (Azure Table Storage en mode dégradé).

## Alternatives écartées

| Alternative | Raison du rejet |
|---|---|
| **Journal bloquant** | Un incident Azure Storage provoquerait l'arrêt complet du traitement des messages — propagation en cascade inacceptable. |
| **Outbox pattern (journal transactionnel)** | Complexité accrue (besoin d'une base de données transactionnelle ou d'un mécanisme de rejeu). Hors périmètre de l'horizon actuel. |
| **Pas de journal** | Viole la contrainte réglementaire RAMQ R3 (auditabilité CAI). |

## Conséquences

- En cas de défaillance du journal, les opérations métier continuent — **risque d'un trou d'audit**.
- Une alerte opérationnelle sur les warnings de journal est recommandée (Application Insights / Azure Monitor).
- Le pattern outbox peut être réévalué en Phase 4+ si les exigences d'auditabilité s'intensifient
  (traçabilité temps-réel garantie).

## Conditions de révision

- Exigence CAI évoluant vers une auditabilité garantie (zéro trou admis) — nécessiterait le pattern outbox.
- Azure Table Storage remplacé par un support plus fiable (Cosmos DB, Azure SQL) dans l'architecture RAMQ.

## Signataires et date

- ___________ (Architecte) — 2026-04-27
- ___________ (Lead technique) — 2026-04-27
