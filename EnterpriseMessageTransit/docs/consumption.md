# Guide de consommation — RAMQ.COM.EnterpriseMessageTransit

> **Public cible :** développeurs intégrant la bibliothèque dans une application .NET (Azure Functions Isolated, Worker Service, AKS/ARO).

---

## 1. Référencer le feed NuGet Azure DevOps (RAMQ)

Le package est publié dans le feed NuGet privé RAMQ hébergé sur Azure DevOps.

### 1.1 Créer ou compléter `nuget.config` à la racine du projet consommateur

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- Feed privé RAMQ (COM) -->
    <add key="RAMQ-COM"
         value="https://pkgs.dev.azure.com/ramq/RAMQ/_packaging/RAMQ-COM/nuget/v3/index.json"
         protocolVersion="3" />
    <!-- nuget.org pour les dépendances publiques -->
    <add key="nuget.org"
         value="https://api.nuget.org/v3/index.json"
         protocolVersion="3" />
  </packageSources>
  <packageSourceCredentials>
    <RAMQ-COM>
      <!-- En CI (Azure DevOps) : utiliser la variable système $(System.AccessToken) -->
      <!-- En local : utiliser un PAT Azure DevOps (scope : Packaging > Read) -->
      <add key="Username" value="AzureDevOps" />
      <add key="ClearTextPassword" value="%RAMQ_NUGET_PAT%" />
    </RAMQ-COM>
  </packageSourceCredentials>
</configuration>
```

> **Sécurité :** ne jamais committer un PAT en clair. Utiliser une variable d'environnement (`%RAMQ_NUGET_PAT%` ou `$env:RAMQ_NUGET_PAT`) ou un secret pipeline Azure DevOps.

### 1.2 Authentification en CI/CD

Dans la tâche `NuGetAuthenticate@1` du pipeline :

```yaml
- task: NuGetAuthenticate@1
  inputs:
    nuGetServiceConnections: 'RAMQ-COM-ServiceConnection'
```

---

## 2. Installer le package

```bash
dotnet add package RAMQ.COM.EnterpriseMessageTransit
```

Ou dans le `.csproj` :

```xml
<PackageReference Include="RAMQ.COM.EnterpriseMessageTransit" Version="0.6.*" />
```

> **Recommandation :** utiliser une contrainte `0.6.*` (patch flottant) pour recevoir automatiquement les corrections de bugs sans risque de changement cassant.

---

## 3. Politique de support N/N-1

| Version | Statut | Fin de support |
|---|---|---|
| `0.6.x` (actuelle) | ✅ Supportée — correctifs et évolutions | Indéfini |
| `0.5.x` | 🟡 Maintenance uniquement — correctifs critiques | 3 mois après sortie de `0.7.0` |
| `0.4.x` et antérieur | ❌ Non supportée — mise à niveau requise | EOL |

**Règle N/N-1 :** seules les deux dernières versions **mineures** (N et N-1) reçoivent des correctifs. Les versions N-2 et antérieures ne sont plus maintenues. Planifiez les mises à niveau dans les 3 mois suivant une nouvelle version mineure.

> **Exception :** une faille de sécurité critique peut déclencher un correctif sur N-2 à la discrétion de l'équipe mainteneuse.

---

## 4. Dépréciation et breaking changes

### 4.1 Convention de dépréciation 3 étapes

| Étape | Action | Compilateur |
|---|---|---|
| **Version N** | `[Obsolete("Utiliser X. Sera retiré en vN+2.", error: false)]` | ⚠️ Warning CS0618 |
| **Version N+1** | `[Obsolete("...", error: true)]` | ❌ Erreur CS0619 — migration obligatoire |
| **Version N+2** | Suppression + entrée `### Supprimé` dans `CHANGELOG.md` | — |

### 4.2 Exemple de migration

Si vous voyez un warning CS0618 :

```
warning CS0618: 'BaseConsumer.DeserializeMessage<T>()' est obsolète :
'Utiliser DeserializeMessageAsync à la place.'
```

Remplacer :

```csharp
// Avant (sync — risque de deadlock)
var ctx = _consumer.DeserializeMessage<MyMessage>();
```

Par :

```csharp
// Après (async)
var ctx = await _consumer.DeserializeMessageAsync<MyMessage>();
```

---

## 5. Procédure d'upgrade

### Étape 1 — Mettre à jour la référence

```bash
dotnet add package RAMQ.COM.EnterpriseMessageTransit --version <cible>
```

### Étape 2 — Lire le CHANGELOG

Consulter [`CHANGELOG.md`](../../changelog.md) et la section `### Supprimé` pour identifier les APIs retirées.

### Étape 3 — Traiter les warnings [Obsolete]

```bash
dotnet build 2>&1 | grep "CS0618\|CS0619"
```

Corriger chaque CS0618 avant qu'il ne devienne CS0619 à la prochaine version.

### Étape 4 — Consulter MIGRATION.md pour les changements structurels

Voir [`docs/MIGRATION.md`](MIGRATION.md) pour les renommages de types et modifications de configuration.

### Étape 5 — Valider

```bash
dotnet build   # 0 erreur
dotnet test    # tous les tests verts
```

---

## 6. Configuration minimale (rappel)

```json
// appsettings.json
{
  "AppSettings": {
    "ServiceBusNamespace": "sbns-mon-app.servicebus.windows.net",
    "ApplicationName": "MonApplication",
    "Itinerary": [
      {
        "Target": "etape-1",
        "Endpoint": { "EntityName": "sbq-etape-1", "EntityType": "Queue" }
      }
    ]
  }
}
```

Voir [`docs/Vue d'ensemble.md`](Vue%20d'ensemble.md) et [`docs/integration.md`](integration.md) pour la configuration complète.

---

*Document maintenu par l'équipe EnterpriseMessageTransit — mis à jour le 2026-04-27.*
