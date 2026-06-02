# ADR-004 — Adoption du pattern Claim-Check et seuil 256 Ko

## Statut

Accepté

## Contexte

Azure Service Bus Premium limite la taille d'un message à **1 Mo**. Les pièces jointes médicales
(documents PDF, images DICOM, résultats de laboratoire) traitées par les systèmes RAMQ peuvent
dépasser ce seuil.

De plus, la contrainte réglementaire RAMQ R2 impose une **rétention légale** sur certains documents
médicaux : ceux-ci doivent être stockés dans un support auditable (Azure Blob Storage) et non dans
le bus de messages (volatile, TTL configurable).

## Décision

Adopter le **pattern Claim-Check** (Enterprise Integration Patterns, Hohpe & Woolf) pour gérer les messages
et pièces jointes dépassant les limites de taille :

### Mécanisme de tokenization

La solution supporte **un message ET une pièce jointe** simultanément :

1. **Message volumétrique** — Si le payload dépasse le seuil configuré (**256 Ko par défaut**) :
   - EMT dépose le corps du message dans Azure Blob Storage
   - Remplace le payload par un **jeton de référence** (`TokenMessage` avec `Kind = TokenKind.Message`)

2. **Pièce jointe** — Indépendamment de sa taille :
   - Toujours traitée comme un token avec `TokenKind.File`
   - Permet de séparer la pièce jointe du contexte métier du message

3. **Configuration** :
   - Le seuil est configurable via `ClaimCheckOptions.ThresholdBytes` (défaut : 262 144 octets)
   - Le stockage cible est configuré via `BlobStorageSetting` (conteneur dédié par application)

4. **Récupération** — Le Consumer reçoit le jeton(s), récupère le(s) blob(s) via `IStorageProvider`,
  et reconstruit le contexte complet.

## Alternatives écartées

| Alternative | Raison du rejet |
|---|---|
| **Passer le payload complet dans le message** | Dépasse la limite Service Bus pour les pièces jointes médicales. |
| **Référence URL externe dans le message** | Couplage au système source, problèmes de durabilité et de droits d'accès. |
| **Limiter la taille des payloads applicativement** | Incompatible avec les exigences métier RAMQ (pièces jointes médicales de taille variable). |

## Conséquences

- Les Consumers doivent implémenter la logique de récupération des blobs lorsque des tokens sont présents
  (`TokenKind.Message` pour un message volumétrique, `TokenKind.File` pour une pièce jointe).
- `IStorageProvider` et `AzureStorageProvider` font partie de la surface publique nécessaire.
- RBAC requis pour le Claim-Check : `Storage Blob Data Contributor` sur le conteneur dédié.
- La politique de rétention des blobs est **distincte** de celle du bus — configurer le lifecycle Azure Storage
  selon les exigences légales RAMQ par domaine (notamment pour les pièces jointes).
- Support du traitement parallèle : un message peut contenir à la fois un token `TokenKind.Message` et un ou plusieurs
  tokens `TokenKind.File`.

> **Contrainte ajoutée — 8 mai 2026 :** Le Claim-Check est **interdit dans `PublishBatchAsync`**.
> `PublishBatchAsync` garantit l'atomicité (tout passe ou rien via `ServiceBusMessageBatch`). Un upload Blob réussi
> suivi d'un échec Service Bus produirait des blobs orphelins non compensables de façon fiable pour N messages.
> `NotSupportedException` levée immédiatement si `ClaimCheck.ForceClaimCheck == true` ou `ClaimCheck.FileStream != null`.
> **Alternative** : utiliser `PublishAsync` en boucle pour les messages nécessitant le Claim-Check.

## Conditions de révision

- Évolution de la limite de taille Service Bus Premium par Microsoft.
- Migration vers Azure Event Grid ou autre broker imposant des contraintes différentes.

## Signataires et date

- ___________ (Architecte) — 2026-04-27
- ___________ (Lead technique) — 2026-04-27
