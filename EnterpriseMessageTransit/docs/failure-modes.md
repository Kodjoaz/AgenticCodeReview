# Modes de défaillance — EnterpriseMessageTransit

> **Audience :** SRE · Lead technique · Développeur senior  
> **Version :** Phase 2 — 27 avril 2026  
> **Voir aussi :** [Matrice exception→action](#matrice-exception--action) · [ADR-005](adr/ADR-005-journal-hors-chemin-critique.md)

---

## 1. Circuit ouvert (`CircuitBreakerOpenException`)

**Symptôme observable**
- Log : `Circuit breaker OPEN for entity '{entity}'. Rejecting send.`
- Métrique : `circuit_state{entity} = 1` (Open)
- Alerte recommandée : `circuit_transitions_total{entity, from=Closed, to=Open}` rate > 0

**Cause probable**  
Indisponibilité temporaire de Service Bus sur l'entité ciblée : erreur réseau, throttling, quota dépassé, incident Azure.

**Conséquence métier**  
Messages **refusés côté Producer**. Aucune perte de données si le caller gère le rejet (Azure Functions re-déclenche l'invocation sur erreur non-gérée).

**Action corrective**
1. Vérifier l'état du namespace Service Bus dans le portail Azure (Azure Service Health).
2. Identifier si c'est du throttling (métrique `send_throttled_total` ou alertes Azure Monitor).
3. Attendre le passage automatique en Half-Open (durée configurable via `CircuitBreakerOptions.OpenDuration`, défaut : 30 s).
4. Si l'incident persiste : ouvrir un incident Azure Support.

---

## 2. Désérialisation échouée — `Malformed`

**Symptôme observable**
- Log : `Deserialization failed for type {TypeName}: {Message}` — `reason=Malformed`
- Métrique : `deserialization_failures_total{reason=Malformed}`

**Cause probable**  
Message produit par une version d'EMT incompatible, ou corruption accidentelle du corps JSON.

**Conséquence métier**  
Message envoyé en **Dead Letter Queue** (politique par défaut : Malformed → DLQ, cf. ADR-006 à signer).

**Action corrective**
1. Inspecter le message en DLQ via Service Bus Explorer.
2. Comparer le schéma JSON avec `docs/contracts/envelope-v1.md`.
3. Identifier la version d'EMT qui a produit le message (logs Producer côté émetteur).
4. Si régression de schéma EMT : rollback de la version et PR corrective.

---

## 3. Désérialisation échouée — `TooLarge`

**Symptôme observable**
- Log : `Deserialization failed — message body exceeds safe limit. reason=TooLarge`
- Métrique : `deserialization_failures_total{reason=TooLarge}`

**Cause probable**  
Payload supérieur au seuil de désérialisation en mémoire (distinct du seuil claim-check 256 Ko). Indique un Producer qui n'a pas activé le claim-check alors qu'il aurait dû.

**Conséquence métier**  
Message envoyé en DLQ + alerte opérationnelle recommandée.

**Action corrective**
1. Identifier le Producer concerné via le `MessageId` en DLQ.
2. Activer `ClaimCheckOptions` côté Producer ou baisser la taille du payload.

---

## 4. Désérialisation échouée — `Empty`

**Symptôme observable**
- Log : `Deserialization failed — empty message body. reason=Empty`
- Métrique : `deserialization_failures_total{reason=Empty}`

**Cause probable**  
Message envoyé avec un corps vide (bug Producer ou test manuel mal construit).

**Conséquence métier**  
Message **abandonné silencieusement** (Drop) — ne remplit pas les DLQ inutilement.

**Action corrective**
1. Identifier l'émetteur et corriger le Producer.
2. Si récurrent : ajouter une validation obligatoire du corps côté Producer.

---

## 5. MaxDeliveryCount dépassé

**Symptôme observable**
- Service Bus transfère automatiquement le message en DLQ avec `DeadLetterReason = MaxDeliveryCountExceeded`.
- Métrique : `messages_dlq_total{entity, reason=MaxDeliveryCountExceeded}`

**Cause probable**  
Le Consumer lève une exception non-gérée (ni `ImmediateRetryException`, ni `ImmediateDLQException`) à chaque invocation — et le message consomme toutes ses tentatives (configurable dans Service Bus, typiquement 10).

**Conséquence métier**  
Message perdu du circuit nominal. Visible en DLQ — traitement manuel ou replay nécessaire.

**Action corrective**
1. Inspecter les logs du Consumer sur la période d'erreur.
2. Si exception transiente répétée : vérifier la dépendance externe (base de données, API).
3. Si exception permanente : corriger le Consumer et rejouer le message depuis la DLQ.
4. Envisager `ImmediateDLQException` si le message est structurellement invalide.

---

## 6. Claim-check orphelin (`ClaimCheckOrphan`)

**Symptôme observable**
- Log : `Claim-check orphan: blob '{BlobName}' could not be cleaned up.`
- Métrique : `messages_dlq_total{entity, reason=ClaimCheckOrphan}`

**Cause probable**  
L'upload blob a réussi mais l'envoi Service Bus a échoué. La compensation automatique (suppression du blob) a elle-même échoué (Storage indisponible au moment du rollback).

**Conséquence métier**  
Blob orphelin en Azure Storage — accumulation de coût et d'espace si non nettoyé. Aucune perte de données métier (le message n'a pas été envoyé).

**Action corrective**
1. La lifecycle policy Blob (`claim-checks/` → delete après 30 jours) nettoie automatiquement.
2. Si le volume d'orphelins est anormal : vérifier la stabilité du namespace Service Bus.

---

## 7. Blob claim-check inaccessible (503 / 404)

**Symptôme observable**
- Log : `Failed to download claim-check blob '{Reference}': {Message}`
- Métrique : `claim_check_download_duration_ms` histogram spike ou absences

**Cause probable**  
Azure Blob Storage dégradé, ou blob supprimé avant que le Consumer ait pu le récupérer (lifecycle policy trop agressive, ou suppression manuelle).

**Conséquence métier**  
Consumer ne peut pas reconstituer le payload — lève une exception, le message entre en cycle de retry puis DLQ.

**Action corrective**
1. Vérifier l'état d'Azure Blob Storage (Azure Service Health).
2. Si blob supprimé : vérifier la lifecycle policy et la durée de rétention opérationnelle (30 jours par défaut).
3. Rejouer le message depuis la DLQ une fois le blob disponible, ou recontacter l'émetteur pour republier.

---

## 8. Journal indisponible (pattern A5)

**Symptôme observable**
- Log : `Journal failed (publish) — message sent but not journalized. MessageId={MessageId}` (WARNING)
- Métrique : `journal_write_duration_ms` absences ou pics anormaux

**Cause probable**  
Azure Table Storage indisponible ou dégradé au moment de l'écriture du journal.

**Conséquence métier**  
**Trou d'audit** : l'opération métier (publish/complete) a réussi mais n'est pas tracée dans le Message Transit Journal. Risque CAI si l'auditabilité est exigée en temps réel.

**Action corrective**
1. Alerte opérationnelle à configurer sur `LogWarning` avec template `Journal failed`.
2. Vérifier l'état d'Azure Table Storage.
3. Évaluer si un replay du journal depuis les logs Application Insights est nécessaire.
4. À long terme : évaluer le pattern outbox si l'auditabilité garantie devient une exigence (cf. ADR-005).

---

## 9. Timeout global Consumer

**Symptôme observable**
- Exception : `OperationCanceledException` propagée depuis `ConsumeAsync`.
- Log : dépend du caller (Azure Functions gère le CancellationToken de l'invocation).

**Cause probable**  
Traitement Consumer dépassant la durée maximale d'invocation (Azure Functions : 5-10 min selon le plan, configurable).

**Conséquence métier**  
Message non complété — Service Bus le re-livre après expiration du lock. Risque de traitement en double si le Consumer n'est pas idempotent.

**Action corrective**
1. Identifier l'opération longue dans le Consumer (requête base de données, appel HTTP).
2. Vérifier que le `CancellationToken` est propagé à toutes les opérations async.
3. Si le traitement est structurellement long : découper en plusieurs étapes saga.

---

## Matrice exception → action

> **Règle d'or :** utiliser `ImmediateRetryException` **seulement** quand on est confiant que le problème se résoudra de lui-même à court terme. Ne jamais l'utiliser pour une erreur de validation — sinon le message consomme tous ses `MaxDeliveryCount` inutilement.

| Situation | Exception à lever | Comportement EMT | `DeliveryCount` |
|---|---|---|---|
| Erreur **transiente** (BD indisponible, timeout réseau < 1 s) | `ImmediateRetryException` | Abandon immédiat, relivré par Service Bus après expiration du lock | Incrémenté |
| Erreur **récupérable avec délai** (fenêtre de traitement fermée, API en maintenance) | `ExponentialRetryException` | Planification différée : délai exponentiel (session) ou `ScheduleMessage` (non-session) | Incrémenté |
| Erreur **irrécupérable** (violation règle métier, schéma invalide) | `ImmediateDLQException` | Dead-lettering immédiat avec raison — bypass de tous les retries | Non incrémenté |
| Exception **non anticipée** (bug) | *(laisser remonter)* | EMT intercepte, log, incrémente `DeliveryCount`. Dead-letter après `MaxDeliveryCount`. | Incrémenté |
| Traitement **réussi** | *(aucune exception)* | `CompleteMessageAsync` — message retiré de la file | N/A |
