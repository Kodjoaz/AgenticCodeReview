# Target solution: multi-vendor split (full specification)

This document is the canonical target solution layout and file map for the multi-vendor refactor. It describes the projects, key files, csproj snippets, DI patterns, migration checklist and testing guidance required to split provider code (Azure) into a provider project and add a Confluent (Kafka) provider.

## High-level solution
- Solution: `EnterpriseMessageTransit.sln`
- Projects (target):
  - `EnterpriseMessageTransit.Core` (existing or minimal changes)
  - `EnterpriseMessageTransit.Abstractions` (new)
  - `EnterpriseMessageTransit.Azure` (migrated)
  - `EnterpriseMessageTransit.Confluent` (new)
  - `EnterpriseMessageTransit.Tests` (unit + integration)

## Responsibilities

- `EnterpriseMessageTransit.Abstractions`
  - Contains all transport-agnostic public interfaces, enums and DTOs used by Core and providers.
  - Minimal, stable API surface intended for package-level reuse.
  - Example files:
    - `Enums/TokenKind.cs`, `Enums/ProcessingEvent.cs`, `Enums/OperationMode.cs`
    - `ISystemClock.cs`, `DefaultSystemClock.cs`
    - `IMessageTransit.cs`, `IMessagingProvider.cs`, `IMessageProducer.cs`, `IMessageConsumer.cs`
    - `IStorageProvider.cs` (Upload/Download/Delete)
    - `IMessageSerializer.cs`
    - `IJournalProvider.cs`, `JournalEntry.cs`
    - `IBindContext.cs`, `BindContextExtensions.cs`

- `EnterpriseMessageTransit.Core`
  - Core message flow and shared implementations (refactored to depend only on Abstractions).
  - Key responsibilities:
    - `MessageTransitContext<T>` and `MessageTransitResponse`
    - Producers/Consumers base classes (`BaseProducer`, `BaseConsumer`)
    - Claim-check orchestration via `IStorageProvider`
    - Use `ISystemClock` for time
    - Batch API surface: `PublishBatchAsync` / `SendBatchAsync`

- `EnterpriseMessageTransit.Azure`
  - Contains Azure-specific code and package references.
  - Examples: `AzureMessagingProvider.cs`, `AzureStorageProvider.cs`, `AzureFunctionMessagingAdapter.cs`, `ServiceBusSenderCache.cs`.
  - DI extension: `AddAzureMessaging(IConfiguration)` to register Azure provider services and clients.

- `EnterpriseMessageTransit.Confluent`
  - New provider implementing `IMessagingProvider` using `Confluent.Kafka`.
  - Examples: `ConfluentMessagingProvider.cs`, `ConfluentProducerFactory.cs`, `ConfluentConsumerHost.cs`.
  - DI extension: `AddConfluentMessaging(IConfiguration)`.

## Project file examples

`EnterpriseMessageTransit.Abstractions.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>
    <LangVersion>11</LangVersion>
  </PropertyGroup>
</Project>
```

`EnterpriseMessageTransit.Azure.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\EnterpriseMessageTransit.Abstractions\EnterpriseMessageTransit.Abstractions.csproj" />
    <PackageReference Include="Azure.Messaging.ServiceBus" Version="x.y.z" />
    <PackageReference Include="Azure.Storage.Blobs" Version="x.y.z" />
  </ItemGroup>
</Project>
```

`EnterpriseMessageTransit.Confluent.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\EnterpriseMessageTransit.Abstractions\EnterpriseMessageTransit.Abstractions.csproj" />
    <PackageReference Include="Confluent.Kafka" Version="x.y.z" />
  </ItemGroup>
</Project>
```

## Core API and file map (target)

- Abstractions (public API):
  - `IMessageTransit` — factory/host surface
  - `IMessagingProvider` — provider contract: `SendAsync`, `SendBatchAsync`, `ReceiveAsync` signatures
  - `IStorageProvider` — `UploadAsync`, `DownloadAsync`, `DeleteAsync`
  - `IMessageSerializer` — `Serialize<T>`, `Deserialize<T>` and options factory
  - `IJournalProvider` — single `WriteRecordAsync(JournalEntry entry)`
  - `ISystemClock` — `DateTime UtcNow { get; }`

- Core implementation files (examples):
  - `MessageTransitContext.cs` (add `SerializedPayload` cache and token helpers)
  - `BaseProducer.cs` (prepare context, cache serialization, Publish/PublishBatch)
  - `BaseConsumer.cs` (hydrate claim-check tokens via `IStorageProvider.DownloadAsync`)
  - `JsonMessageSerializer.cs` (cached `JsonSerializerOptions`)

## Provider patterns and DI

- Provider DI extension pattern (recommended):
  - In provider project implement `public static IServiceCollection AddAzureMessaging(this IServiceCollection services, IConfiguration config)`
  - This method registers provider concrete types and configuration objects and any provider-scoped factories.

Example startup snippet (app selects provider):
```csharp
services.AddSingleton<ISystemClock, DefaultSystemClock>();
services.AddSingleton<IJournalProvider, TableJournalProvider>();
// Pick provider at startup
services.AddAzureMessaging(Configuration.GetSection("Azure"));
// or
services.AddConfluentMessaging(Configuration.GetSection("Confluent"));
```

## BindContext adapter

- Keep a small, version-stable `IBindContext` in Abstractions. Provide provider-specific interfaces (e.g. `IAzureBindContext`, `IConfluentBindContext`) implemented by provider contexts.
- Add extension helpers in Abstractions: `AsAzure()`, `TryAsAzure()`, `AsConfluent()` to avoid direct casts.

## Confluent provider design (summary)

- Mapping decisions:
  - Topic: `TransportSettings.EntityName`
  - Key: `MessageTransitContext.MessageId` when partitioning required
  - Headers: use Kafka headers for message properties
- Producer: use `ProducerBuilder<string, byte[]>` or `ProducerBuilder<Null, byte[]>`, serialize payload via `IMessageSerializer`
- Consumer: use `ConsumerBuilder` with manual commit on success
- Retry/scheduling: Kafka lacks delayed message primitives — implement retry topics with backoff or use external scheduler

## Claim-check and storage

- `IStorageProvider` exposes `DownloadAsync` and `DeleteAsync` so `BaseConsumer` can hydrate payloads independent of the transport.
- Providers implement `IStorageProvider` using provider SDKs (Azure uses `BlobClient`, Confluent provider delegates to whichever storage provider selected).

## Tests and CI

- Unit tests: Core and Abstractions-level unit tests (mock provider interfaces)
- Integration tests:
  - Confluent: use Redpanda or Testcontainers in CI
  - Azure: recorded fixtures or a dedicated Azure integration job using environment secrets

## Migration checklist (detailed)

1. Create `EnterpriseMessageTransit.Abstractions` and move enums, small DTOs and interfaces.
2. Update `EnterpriseMessageTransit.Core` to reference `Abstractions` only and remove direct Azure package usage.
3. Move Azure provider code into `EnterpriseMessageTransit.Azure` project and add provider packages to its csproj.
4. Create `EnterpriseMessageTransit.Confluent` and implement provider interfaces.
5. Replace direct `BindContext` casts with adapter helpers and refactor usages.
6. Add DI extension methods and update sample startup code.
7. Add integration tests for each provider.

## Deliverables (what will appear in the repo)

- `EnterpriseMessageTransit.Abstractions/` — csproj + public API files
- `EnterpriseMessageTransit.Azure/` — existing Azure provider code moved and cleaned
- `EnterpriseMessageTransit.Confluent/` — new provider
- Updated `EnterpriseMessageTransit.Core/` — core changes
- `samples/` demonstrating provider selection
- Updated `docs/feature-multivendor.md` (this file)

---

If you want, I will now commit this doc change, create a branch and open a PR with the target-solution spec, or scaffold the `Abstractions` project and initial files. Which should I do next?
