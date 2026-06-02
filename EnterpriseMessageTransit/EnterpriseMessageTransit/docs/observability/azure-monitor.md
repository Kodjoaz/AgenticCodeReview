# Observabilité Azure — Application Insights, Log Analytics et FinOps

> **Audience :** Développeur · Lead technique · Architecte · Gestionnaire de coûts  
> **Prérequis :** Notions de base en OpenTelemetry et en Azure

---

## Table des matières

1. [Comment les trois produits s'articulent](#1-comment-les-trois-produits-sarticulent)
2. [Application Insights — rôle et limites](#2-application-insights--rôle-et-limites)
3. [Log Analytics Workspace — rôle et limites](#3-log-analytics-workspace--rôle-et-limites)
4. [Configuration EMT dans l'hôte](#4-configuration-emt-dans-lhôte)
5. [FinOps — comprendre la facturation](#5-finops--comprendre-la-facturation)
6. [FinOps — stratégies de réduction des coûts](#6-finops--stratégies-de-réduction-des-coûts)

---

## 1. Comment les trois produits s'articulent

```
Application hôte (Azure Function / Worker)
    │
    │  System.Diagnostics.ActivitySource  ("RAMQ.COM.EnterpriseMessageTransit")
    │  System.Diagnostics.Metrics         ("RAMQ.COM.EnterpriseMessageTransit")
    │
    ▼
Azure Monitor OpenTelemetry Exporter
(NuGet : Azure.Monitor.OpenTelemetry.Exporter)
    │
    ▼
Application Insights (workspace-based)
    │  ← façade : portail, alertes, Live Metrics, Application Map
    │  ← ne stocke PAS les données lui-même
    │
    ▼
Log Analytics Workspace
    │  ← stockage réel des données (tables : dependencies, customMetrics…)
    │  ← requêtes KQL
    │  ← facturation (Go ingérés / jour)
    ▼
  Dashboards · Alertes · Classeurs Azure
```

**Points clés :**

- **Application Insights (workspace-based)** est une **façade de visualisation** par-dessus Log Analytics. Il ne stocke rien par lui-même.
- **Log Analytics Workspace** est le **moteur de stockage et de requêtes**. C'est lui qui facture.
- **EMT n'a aucune dépendance** sur ces produits. Il émet via les APIs standard BCL .NET. L'hôte choisit l'exporter.

### Pourquoi Log Analytics seul ne suffit pas

Log Analytics n'expose pas d'endpoint OTLP (`/v1/traces`, `/v1/metrics`). Il n'y a donc pas de chemin direct depuis le SDK OpenTelemetry vers Log Analytics sans passer par Application Insights ou par un OpenTelemetry Collector custom.

| Chemin | Faisabilité | Complexité |
|--------|-------------|------------|
| App → App Insights (workspace-based) → Log Analytics | ✅ Recommandé | Faible |
| App → OTel Collector → Logs Ingestion API → Log Analytics | ⚠️ Possible | Élevée |
| App → Log Analytics directement via OTLP | ❌ Impossible | — |

---

## 2. Application Insights — rôle et limites

### Ce qu'il fait

- **Application Map** : graphe des dépendances entre composants (très utile pour visualiser le flux Saga EMT)
- **Live Metrics** : métriques en temps réel avec latence < 1 seconde
- **Transaction Search** : navigation span par span dans une trace W3C
- **Smart Detection** : alertes automatiques sur anomalies (dégradation de perf, taux d'échec)
- **Availability Tests** : ping externe périodique (non applicable à EMT, utile pour les APIs REST associées)

### Ce qu'il ne fait pas

- Il ne stocke pas — toutes les données vont dans le Log Analytics Workspace lié
- Il n'est pas un OpenTelemetry Collector — il ne reçoit pas OTLP directement (l'exporter NuGet fait la conversion)
- Il ne remplace pas Grafana pour des dashboards très personnalisés

### Mode workspace-based vs classique

EMT cible exclusivement le mode **workspace-based** (disponible depuis 2021). Le mode "classique" est déprécié depuis le 29 février 2024.

| | Mode classique (déprécié) | Mode workspace-based ✅ |
|-|--------------------------|------------------------|
| Stockage | Silo Application Insights | Log Analytics Workspace partageable |
| Rétention configurable | Non | Oui |
| Requêtes cross-ressource | Non | Oui |
| Export continu | Via Storage Account | Via Log Analytics |
| Facturation unifiée | Non | Oui |

---

## 3. Log Analytics Workspace — rôle et limites

### Tables utilisées par EMT

| Table Log Analytics | Données EMT |
|--------------------|-------------|
| `dependencies` | Spans `messaging.publish`, `messaging.send`, `messaging.consume`, `messaging.deserialize` |
| `customMetrics` | Toutes les métriques `IMetricsProvider` (counters, histogrammes, gauges) |
| `traces` | Logs applicatifs (`ILogger`) |
| `exceptions` | Exceptions non gérées capturées par le SDK |

### Rétention par défaut

- **90 jours** de rétention interactive (requêtes KQL)
- **Archivage** : jusqu'à 7 ans à coût réduit (lecture seule, non requêtable en temps réel)

### Limites à connaître

| Limite | Valeur |
|--------|--------|
| Taille maximale d'un enregistrement | 30 Mo |
| Cardinalité maximale par table | 500 colonnes |
| Délai d'ingestion | 2 à 5 minutes (pas de temps réel, sauf Live Metrics) |
| Concurrence de requêtes par workspace | 200 requêtes simultanées |

---

## 4. Configuration EMT dans l'hôte

### NuGet requis

```xml
<!-- Azure.Monitor.OpenTelemetry.Exporter — exporter officiel Microsoft -->
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
<!-- ou pour les Workers sans ASP.NET Core -->
<PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.*" />
```

### Azure Function (Isolated Worker) — configuration minimale

```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("RAMQ.COM.EnterpriseMessageTransit")  // spans EMT
                .AddAzureMonitorTraceExporter(o =>
                {
                    o.ConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
                })
            )
            .WithMetrics(metrics => metrics
                .AddMeter("RAMQ.COM.EnterpriseMessageTransit")   // métriques EMT
                .AddAzureMonitorMetricExporter(o =>
                {
                    o.ConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
                })
            );
    })
    .Build();
```

### Worker Service (.NET 8)

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("RAMQ.COM.EnterpriseMessageTransit")
        .AddAzureMonitorTraceExporter()
    )
    .WithMetrics(metrics => metrics
        .AddMeter("RAMQ.COM.EnterpriseMessageTransit")
        .AddAzureMonitorMetricExporter()
    );
```

> La `ConnectionString` Application Insights peut être configurée via la variable d'environnement `APPLICATIONINSIGHTS_CONNECTION_STRING`. L'exporter la lit automatiquement si elle n'est pas passée explicitement.

### Configuration locale (développement)

En développement, aucun export Azure n'est nécessaire. L'activité est observable via :

```csharp
// appsettings.Development.json — désactiver l'export Azure
// ou simplement ne pas définir APPLICATIONINSIGHTS_CONNECTION_STRING

// Pour voir les traces en console :
.AddConsoleExporter()

// Pour voir les traces dans l'UI Aspire Dashboard :
.AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"))
```

---

## 5. FinOps — comprendre la facturation

### Principe fondamental

**C'est Log Analytics qui facture, pas Application Insights.** Application Insights workspace-based est gratuit comme façade.

### Modèle de tarification Log Analytics (région Canada Est / Est-US, mai 2026)

| Volume | Prix unitaire |
|--------|---------------|
| 0–5 Go/jour | **Gratuit** |
| Au-delà de 5 Go/jour | ~2,76 USD/Go |

> Les prix varient selon la région et sont susceptibles de changer. Toujours vérifier le [calculateur Azure](https://azure.microsoft.com/fr-ca/pricing/calculator/) avant de dimensionner.

### Estimation pour EMT

| Scénario | Volume estimé / jour | Coût estimé / mois |
|----------|---------------------|--------------------|
| Dev/Test (1 équipe) | < 100 Mo | **Gratuit** |
| Préproduction | ~500 Mo | **Gratuit** |
| Production légère (< 50 000 msg/jour) | ~1 Go | **Gratuit** |
| Production standard (< 500 000 msg/jour) | ~5 Go | Limite du tier gratuit |
| Production intensive (> 500 000 msg/jour) | > 5 Go | ~2,76 USD/Go dépassement |

*Hypothèse : un span `messaging.consume` pèse ~1–2 Ko, une métrique ~200 octets.*

### Commitment Tiers (réduction sur volume)

Pour les workspaces qui dépassent régulièrement 5 Go/jour, Microsoft propose des tarifs pré-engagés :

| Tier d'engagement | Réduction approximative |
|-------------------|------------------------|
| 100 Go/jour | ~15% vs pay-as-you-go |
| 200 Go/jour | ~22% |
| 300 Go/jour | ~30% |

Les commitment tiers sont configurables dans **Log Analytics Workspace → Utilisation et coûts estimés**.

---

## 6. FinOps — stratégies de réduction des coûts

### Stratégie 1 — Sampling adaptatif

Exporter un pourcentage des traces seulement. **Les métriques ne sont pas affectées** par le sampling (toujours 100%).

```csharp
// 10% des traces en production — toujours suffisant pour le diagnostic
.SetSampler(new TraceIdRatioBasedSampler(0.10))
```

Impact sur les coûts : **-85 à -90%** sur le volume de traces.

### Stratégie 2 — Toujours échantillonner les erreurs (sampler custom)

```csharp
/// <summary>
/// Exporte 100% des traces en erreur, et <paramref name="ratio"/> des traces normales.
/// </summary>
public sealed class PrioritizeErrorsSampler : Sampler
{
    private readonly double _ratio;

    public PrioritizeErrorsSampler(double ratio) => _ratio = ratio;

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        // Si le parent a déjà décidé d'enregistrer (ex. erreur propagée), on suit
        if (parameters.ParentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded))
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // Sinon, tirage aléatoire selon le ratio
        return new SamplingResult(
            Random.Shared.NextDouble() < _ratio
                ? SamplingDecision.RecordAndSample
                : SamplingDecision.Drop);
    }
}

// Enregistrement :
.SetSampler(new PrioritizeErrorsSampler(ratio: 0.10)) // 10% nominal, 100% erreurs
```

### Stratégie 3 — Filtrer les spans de faible valeur

```csharp
// Exemple : exclure les health checks et les spans de durée < 1 ms
.AddAspNetCoreInstrumentation(o =>
{
    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                   && !ctx.Request.Path.StartsWithSegments("/ready");
})
```

Pour EMT spécifiquement, il n'y a rien à filtrer par défaut — tous les spans (`messaging.publish`, `messaging.send`, `messaging.consume`, `messaging.deserialize`) ont une valeur opérationnelle.

### Stratégie 4 — Réduire la rétention

Par défaut : 90 jours. Réduire à 30 jours divise par 3 le coût de stockage des données historiques.

Configurable dans : **Log Analytics Workspace → Tables → (table) → Gérer la table → Période de rétention interactive**.

Recommandation EMT :

| Table | Rétention recommandée | Justification |
|-------|-----------------------|---------------|
| `dependencies` | 30 jours | Traces : utiles pour le debug récent |
| `customMetrics` | 90 jours | Métriques : utiles pour les tendances long terme |
| `traces` | 30 jours | Logs : debug récent uniquement |
| `exceptions` | 90 jours | Post-mortems : garder l'historique |

### Stratégie 5 — Archivage des données froides

Log Analytics permet d'archiver automatiquement les données après la période de rétention interactive (coût ~0,025 USD/Go/mois vs ~0,11 USD/Go/mois pour la rétention interactive).

Configurer via : **Log Analytics Workspace → Tables → Gérer la table → Période de rétention totale**.

### Résumé — impact combiné des stratégies

| Stratégie | Réduction volume | Complexité |
|-----------|-----------------|------------|
| Sampling 10% (traces) | -85% | Faible |
| Filtrage health checks | -2 à -5% | Faible |
| Rétention 30 jours (traces/logs) | -67% sur stockage | Faible |
| Archivage données froides | -77% sur stockage archivé | Faible |
| Commitment Tier (si > 100 Go/jour) | -15 à -30% | Nul (config portail) |

> **Recommandation pour EMT :** appliquer d'abord le **sampling 10%** et la **réduction de rétention** — ces deux mesures suffisent dans la grande majorité des cas pour rester sous le seuil gratuit de 5 Go/jour même en production standard.
