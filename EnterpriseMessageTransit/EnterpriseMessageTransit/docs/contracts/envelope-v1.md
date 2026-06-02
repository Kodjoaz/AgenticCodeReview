# Contrat de schéma — `MessageTransitContext<T>` v1

> **Version du schéma :** 1 (implicite — `SchemaVersion` introduit en Phase 3)
> **Type C# :** `RAMQ.COM.EnterpriseMessageTransit.Messaging.MessageTransitContext<TMessage>`
> **Sérialiseur :** `System.Text.Json` — options par défaut + `[JsonPropertyName]` explicites
> **Date de gel :** 2026-04-27 (Phase 1)
> **Voir :** [ADR-003](../adr/ADR-003-versioning-contrat-MessageTransitContext.md)

---

## 1. Champs transportés (sérialisés en JSON)

Ces champs **voyagent** dans le message Service Bus. Ils sont désérialisés par le Consumer.
Toute modification de nom JSON, de type ou de suppression est un **MAJOR SemVer bump**.

| Champ C# | Nom JSON | Type JSON | Obligatoire | Description |
|---|---|---|---|---|
| `MessageType` | `MessageType` | `string \| null` | Non | Discriminant de type métier. Utilisé pour le routage ou la désérialisation polymorphique. |
| `Message` | `Message` | `object \| null` | Non | Payload métier (`TMessage`). Null si le Claim-Check est appliqué (payload déporté en Blob). |
| `MessageId` | `MessageId` | `string \| null` | **Oui (en pratique)** | Identifiant unique du message — clé de corrélation bout-en-bout dans le Journal. Généré par le Producer. |
| `SessionId` | `SessionId` | `string \| null` | Non | Identifiant de session Service Bus. Obligatoire uniquement pour le patron Sequential Convoy. Défini par le développeur avant `PublishAsync`. |
| `SequenceNumber` | `SequenceNumber` | `integer (int64)` | Non | Numéro de séquence Service Bus. Affecté par Service Bus à la réception — non significatif côté Producer. |
| `Attempt` | `Attempt` | `integer (int32)` | Non | Numéro de tentative courante (retry). Incrémenté par EMT à chaque cycle de retry. |
| `CurrentStage` | `CurrentStage` | `string \| null` | Non | Étape courante dans l'itinéraire (ex. `"Individu.ValiderAdresse"`). Affecté par EMT via `SetCurrentStage` — ne pas affecter manuellement. |
| `Tokens` | `Tokens` | `array \| null` | Non | Liste de jetons `TokenMessage`. Contient le jeton Claim-Check si `IsClaimCheckApplied = true`. |
| `Variables` | `Variables` | `object \| null` | Non | Dictionnaire clé/valeur applicatif propagé de bout en bout. Utilisé pour le contexte saga ou le passage de métadonnées. |

---

## 2. Champs runtime (`[JsonIgnore]` — ne voyagent PAS)

Ces champs sont présents dans le type C# mais **absents du JSON sérialisé**. Ils ne doivent **jamais**
apparaître dans un message Service Bus. Les tests snapshot valident cette garantie.

| Champ C# | Raison de l'exclusion |
|---|---|
| `TransportMessage` | Référence vers le message Service Bus natif (`IMessageTransit`). Valide uniquement pendant le traitement en mémoire. Non sérialisable. |
| `SerializedPayload` | Contenu brut du payload avant désérialisation. Utilisé en interne par EMT pour le Claim-Check. Exposer ce champ en JSON serait une double-sérialisation. |
| `IsClaimCheckApplied` | Flag interne indiquant que le payload a été déporté en Blob. L'information voyage via `Tokens` (présence d'un `TokenMessage` de kind `File`), pas via ce flag. |

---

## 3. Exemple JSON canonique

```json
{
  "MessageType": "MessageDispensateur",
  "Message": {
    "NoDossier": "D-2026-04-0042",
    "NoAssure": "A98765"
  },
  "MessageId": "d8f3c2a0-4e21-4b15-9c77-3a1b9e0c7f12",
  "SessionId": "session-001",
  "SequenceNumber": 0,
  "Attempt": 0,
  "CurrentStage": "Dispensateur.CreerDispensation",
  "Tokens": [],
  "Variables": {
    "DossierId": "D-2026-04-0042"
  }
}
```

> **Absent du JSON :** `TransportMessage`, `SerializedPayload`, `IsClaimCheckApplied`.

---

## 4. Exemple JSON — Claim-Check appliqué

Lorsque le payload dépasse le seuil (256 Ko par défaut), `Message` est `null` et `Tokens` contient
un `TokenMessage` de `Kind = "File"` pointant vers le blob.

```json
{
  "MessageType": "MessageDocumentMedical",
  "Message": null,
  "MessageId": "f1a2b3c4-0000-0000-0000-000000000001",
  "SessionId": null,
  "SequenceNumber": 0,
  "Attempt": 0,
  "CurrentStage": "Radiologie.TraiterDocument",
  "Tokens": [
    {
      "Kind": "File",
      "Reference": "https://monstorage.blob.core.windows.net/emt-claimcheck/f1a2b3c4-0000-0000-0000-000000000001.json"
    }
  ],
  "Variables": {}
}
```

---

## 5. Politique d'évolution

| Type de changement | Impact SemVer | Procédure |
|---|---|---|
| Ajout d'un champ nullable / optionnel | MINOR | PR + mise à jour de ce document + `PublicAPI.Unshipped.txt` |
| Renommage d'un champ JSON | **MAJOR** | PR + ADR de migration + `CHANGELOG.md §Supprimé` + période de transition (champ ancien read-only pendant 1 version) |
| Suppression d'un champ | **MAJOR** | Dépréciation (`[Obsolete]`) en version N, suppression en N+1 |
| Ajout d'un champ obligatoire | **MAJOR** | PR + ADR + migration des producers/consumers existants |
| Modification du type JSON d'un champ | **MAJOR** | PR + ADR de migration |

> **Règle CI :** les tests de contrat snapshot (`MessageTransitContextSerializationTests`) sont
> **bloquants**. Tout changement du JSON sérialisé échoue la CI tant que le snapshot n'est pas
> approuvé manuellement (`dotnet verify accept`).

---

## 6. Phase 3 — Migration prévue

En Phase 3, `MessageTransitContext<T>` sera refactoré pour séparer :
- **Enveloppe de transport** (`MessageEnvelope`) : champs transportés uniquement.
- **Contexte runtime** : champs `[JsonIgnore]` et comportement déplacés dans une classe interne.

Ce contrat v1 sera alors remplacé par `envelope-v2.md`. La transition sera documentée dans un ADR
de migration et maintenue rétrocompatible pendant une version de transition.
