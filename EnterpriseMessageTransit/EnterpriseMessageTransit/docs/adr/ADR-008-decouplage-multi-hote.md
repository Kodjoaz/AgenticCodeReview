# ADR-008 — Découplage multi-hôte : EMT sur Azure Functions (consumer) et conteneurs AKS/ARO (producer)

## Statut

Accepté — mis à jour le 2026-05-08

## Contexte

En Phase 2 (P2-C5), `ServiceBusMessageActionsAdapter` a isolé l'implémentation `Microsoft.Azure.Functions.Worker` derrière l'interface `IMessageSettlementActions`. L'assembly principal n'a désormais qu'un seul point de couplage direct avec le SDK Azure Functions Worker : `AzureFunctionMessagingAdapter` (déjà `internal`) et `AzureFunctionMessageTransit` (actuellement `public`).

**Clarification architecturale (8 mai 2026) :** Le scénario multi-hôte pour RAMQ est asymétrique :
- Le **consumer** reste exclusivement sur **Azure Functions** — le couplage au SDK Functions Worker est donc permanent et assumé côté settlement.
- Le **producer** peut être hébergé dans un **conteneur AKS/ARO** (ex. : service .NET exécuté dans un `BackgroundService` ou une API ASP.NET Core conteneurisée). Le producer n'interagit qu'avec `IMessageProducer<T>` — **aucun couplage aux types Azure Functions**.

## Contexte opérationnel RAMQ

| Rôle EMT | Hôte | Utilisation actuelle | Horizon |
|---|---|---|---|
| **Consumer** | Azure Functions Isolated | ✅ Hôte unique et définitif | Permanent |
| **Producer** | Azure Functions Isolated | ✅ Hôte actuel | Court terme |
| **Producer** | Conteneur AKS/ARO (`BackgroundService` / API ASP.NET Core) | ❌ En évaluation | Moyen terme (12-24 mois) |

> Le scénario **Consumer sur AKS/ARO** (avec KEDA) **n'est pas retenu** pour RAMQ. Le settlement des messages (Complete/Abandon/DeadLetter) reste géré par le runtime Azure Functions.

## Décision

### Principe retenu : producer portable, consumer fixe sur Functions

Le **producer** est déjà portable sans aucune modification d'EMT :
- `IMessageProducer<T>` ne dépend que de `Azure.Messaging.ServiceBus` (SDK stable).
- `ConfigureAzureProviders()` s'enregistre dans n'importe quel hôte .NET générique (`HostBuilder`, `WebApplicationBuilder`).
- Aucune référence à `Microsoft.Azure.Functions.Worker` n'est requise côté producer.

Le **consumer** conserve le couplage Functions — c'est un choix délibéré qui simplifie la gestion du settlement et l'intégration avec le trigger Service Bus natif.

EMT **ne déplace pas** `AzureFunctionMessagingAdapter` dans un assembly séparé. Le split d'assembly n'apporterait aucun bénéfice puisque le consumer reste sur Functions.

### Producer dans un conteneur AKS/ARO — aucun adaptateur supplémentaire requis

```csharp
// Program.cs — conteneur AKS/ARO (BackgroundService ou API ASP.NET Core)
var builder = WebApplication.CreateBuilder(args);  // ou Host.CreateDefaultBuilder()

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddSingleton<IMessageTransitConfigurationService, MyConfigurationService>();
builder.Services.AddSingleton<IProducerConfigurationService, MyConfigurationService>();

// Même appel que dans une Azure Function — aucune différence
builder.Services.ConfigureAzureProviders();  // utilise ManagedIdentityCredential en prod

builder.Services.AddTransient<IMessageProducer<MonMessage>>();
```

L'authentification en production utilise `ManagedIdentityCredential` via l'identité du pod (Workload Identity AKS).

### Condition de split d'assembly (invalidée)

La condition de split documentée initialement (un second hôte consumer en production) **ne s'applique plus** — le consumer ne migrera pas vers AKS/ARO. La suppression de la dépendance transitive `Microsoft.Azure.Functions.Worker` côté producer peut être envisagée via un package séparé si la dépendance devient problématique, mais ce n'est pas le cas aujourd'hui.

## Conséquences

- `AzureFunctionMessageTransit` reste `public` — les consumers Functions l'utilisent directement.
- `AzureFunctionMessagingAdapter` reste `internal` dans l'assembly principal.
- `IMessageSettlementActions`, `IMessagingAdapter`, `IMessagingProvider` restent stables — aucune modification requise pour le producer conteneurisé.
- **Aucun nouvel adaptateur** (`WorkerServiceMessagingAdapter`, `KedaMessagingAdapter`) n'est nécessaire.
- Les exemples existants `RAMQ.Samples.Queue.*.Worker` (basés sur `HostBuilder`) illustrent déjà le pattern producer en hôte générique.

## Conditions de révision

- Si un consumer doit migrer hors Azure Functions (décision d'architecture RAMQ) : rouvrir pour décider du split d'assembly.
- Si `Microsoft.Azure.Functions.Worker` introduit des dépendances transitives incompatibles avec un projet producer conteneurisé : envisager un package `EnterpriseMessageTransit.Producer` allégé.
- Révision annuelle : 8 mai 2027.

## Signataires

| Rôle | Nom | Date |
|---|---|---|
| Architecte responsable | GitHub Copilot | 2026-04-27 |
| Approbation technique | — | En attente |
