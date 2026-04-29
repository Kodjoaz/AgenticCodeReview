
**Changes Made**
- **Blob URL leakage fix**: `Messaging/Producer/BaseProducer.cs` — normalized blob references so tokens store only the relative `container/blob` path (strips host and query/SAS). Rationale: avoid leaking SAS or full URLs in messages; consumers should reconstruct full URLs with `TokenCredential`.

- **Claim-check download & cleanup**: `Messaging/Providers/IStorageProvider.cs` — added `DownloadAsync(string reference, CancellationToken)` and `DeleteAsync(string reference, CancellationToken)`. `Messaging/Providers/Azure/AzureStorageProvider.cs` implements both (supports absolute blob URIs and relative `container/blob` references). `Messaging/Consumer/BaseConsumer.cs` now attempts to automatically download and hydrate the message payload when a message-token is present and the inline `Message` is null. Rationale: symmetrical claim-check support for consumers and producers.

	Notes:
	- The consumer currently performs a synchronous download to preserve the existing API shape; consider converting `DeserializeMessage` to an async API to avoid blocking I/O.
	- `DeleteAsync` is implemented as a best-effort helper; automatic deletion after successful consumption is intentionally opt-in (not enabled by default).

- **Harden JSON deserialization**: `Serialization/JsonMessageSerializer.cs` — added pre-parse validation, `MaxDepth` limits, max payload size check, and logging of deserialization failures. Injected `ILogger<JsonMessageSerializer>` and replaced silent exceptions with logged warnings/errors. Rationale: mitigate deserialization vulnerabilities and improve observability.

- **Log swallowed exceptions**:
	- `Messaging/Providers/Azure/AzureMessagingProvider.cs` — `DeserializeMessage<T>()` now logs exceptions at Warning instead of swallowing them.
	- `Messaging/Consumer/BaseConsumer.cs` — replace silent catches in `AlignStage` and `CompleteMessageAsync` with `Logger.LogWarning(...)` so failures are visible.
	- `Messaging/Producer/BaseProducer.cs` — log exceptions when determining `fileStream.Length` fallback.

- **Added thread-safe jitter**: `Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs` — replaced `new Random(seed)` with `Random.Shared.NextDouble()` to provide thread-safe, less predictable jitter.

- **Session retry backoff (non-blocking)**: `Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs` — replaced blocking `Task.Delay` in the session retry path with a schedule-and-complete approach: the adapter now schedules a cloned message for the same `SessionId` at the computed backoff `scheduledTime` and completes the current message. Rationale: avoids holding the Azure Functions worker during backoff and reduces resource consumption.

	Recommendation: for more advanced orchestration of long backoffs use Durable Functions (timer orchestrations) or an external scheduler. If you keep scheduling messages directly, consider the effect on message retention/TTL and instrument `ReferralCount` for observability.

- **Removed connection string from options**: `Messaging/Providers/Azure/AzureServiceBusProviderOptions.cs` — removed `ConnectionString` property to avoid accidental use of connection strings; prefer `TokenCredential`-based auth (configured in `Configuration/Extensions/ConfigurerProviders.cs`).

- **Transport abstraction improvements**:
	- `Messaging/MessageTransitContext.cs` — removed direct exposure of `ServiceBusReceivedMessage` and added `IMessageTransit? TransportMessage` to decouple transport.
	- `Messaging/Providers/Azure/AzureFunctionMessageTransit.cs` — added `RawMessage` property exposing the underlying `ServiceBusReceivedMessage` for Azure-only adapters.
	- `Messaging/Producer/BaseProducer.cs` and `Messaging/Providers/Azure/AzureMessagingProvider.cs` — updated mappings to set `TransportMessage` using `AzureFunctionMessageTransit`.

- **Typed BindContext overload (non-breaking)**:
	- `Messaging/Providers/IMessageActions.cs` — added `void BindContext(IMessageTransit message, object actions);` as a preferred, non-breaking overload.
	- `Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs` and `Messaging/Providers/Azure/AzureMessagingProvider.cs` — implemented the new overload to accept `IMessageTransit` and extract the raw Azure message when appropriate.
	- `Messaging/Consumer/BaseConsumer.cs` — `BindContext(object, object)` now prefers the typed overload when `message is IMessageTransit` and falls back to the object overload for compatibility.

- **Miscellaneous**: small logging and error-handling improvements across providers and adapters; all changes were validated with a full `dotnet build` (solution compiles successfully).

- **Batch publishing support**: Added batching APIs to improve high-throughput scenarios.
	- `Messaging/Producer/IMessageProducer.cs` now exposes `PublishBatchAsync(IEnumerable<MessageTransitContext<T>> contexts, ...)`.
	- `Messaging/Providers/IMessagingProvider.cs` adds `SendBatchAsync(IEnumerable<MessageTransitContext<T>> contexts, MessagingOptions options, ...)`.
	- `Messaging/Producer/BaseProducer.cs` includes a default `PublishBatchAsync` which prepares claim-check tokens for each context and delegates to the provider.
	- `Messaging/Providers/Azure/AzureMessagingProvider.cs` implements `SendBatchAsync` using `ServiceBusMessageBatch` and the `ServiceBusSenderCache` to group messages per entity and send efficient batches.

	Notes:
	- The current batch implementation applies common `MessagingOptions.Properties` to all messages in the batch. If per-message properties are required, pass them inside each `MessageTransitContext` and extend the batching logic accordingly.
	- The provider falls back to single-message sends for oversized messages that don't fit into an empty batch.
	- Tests are recommended to validate batching, ordering, and oversized-message behavior.

- **Retry policy fix**: `Configuration/ExponentialRetryPolicy.cs` — corrected `InitialDelay` default to `TimeSpan.FromMilliseconds(500)` (was inadvertently using an HTTP status code value). Rationale: avoids confusing/default value sourced from unrelated constant.

- **ServiceBusSender reuse and DI registration**: the library now includes a thread-safe singleton `ServiceBusSenderCache` that centralizes creation and reuse of `ServiceBusSender` instances per entity name. Changes:
	- `Messaging/Providers/Azure/ServiceBusSenderCache.cs` — new singleton cache that returns a `ServiceBusSender` for a given `ServiceBusClient` and entity name and disposes them when the host shuts down.
	- `Configuration/Extensions/ConfigurerProviders.cs` — registers `ServiceBusSenderCache` as a singleton in the DI container.
	- `Messaging/Providers/Azure/AzureMessagingProvider.cs` — updated to use `ServiceBusSenderCache.GetOrCreate(...)` instead of creating a new sender per send. This reduces allocations and improves throughput, and is appropriate for stateless hosts (Azure Functions).

Recommendation: prefer using the provided `ServiceBusSenderCache` for high-throughput or serverless deployments. If you need an alternate lifetime or eviction policy, consider implementing a custom cache or extending the provided one.

- **DI improvement: `IEndpointResolver`**: Introduced `Configuration/IEndpointResolver.cs` and made `EndpointResolver` implement it. Registered the implementation in DI (`ConfigurerProviders.cs`) and updated Azure adapters/providers to accept `IEndpointResolver` via constructor injection instead of instantiating `EndpointResolver` directly. Rationale: improves testability and adheres to dependency inversion.

- **Testable system clock**: Introduced `Configuration/ISystemClock.cs` and `DefaultSystemClock`. `ISystemClock.UtcNow` is injected where deterministic timestamps are required (used in `BaseProducer`, `AzureJournalProvider`, `AzureFunctionMessagingAdapter`). This replaces direct `DateTime.UtcNow` calls and enables mocking time in unit tests.

- **Journal API refactor**: Added `Messaging/Providers/JournalEntry.cs` (a `record`) and refactored `IJournalProvider.WriteRecordAsync` to accept a `JournalEntry` instead of a long parameter list. Updated `AzureJournalProvider` and all internal call sites to construct and pass `JournalEntry`. If you implement a custom `IJournalProvider`, adapt to the new signature.

- **Global suppressions cleaned**: Removed `RAMQ0108` suppressions relating to `DateTime.UtcNow` usage because the code now uses `ISystemClock` and journal signature refactor removed several multi-parameter methods.

**Next Recommendations**

- Update documentation/README to mark the old `BindContext(object, object)` as deprecated and recommend the typed overload for new providers.
- Add configuration knobs for JSON safety limits (`MaxDepth`, `MaxPayloadSize`) in `AppSettings` if needed.
- Ensure all non-Azure providers implement the typed `BindContext(IMessageTransit, object)` overload to complete the transport-agnostic transition.

---

If you want, I can: (a) add the deprecation note to the API docs, (b) implement typed overloads for other providers, or (c) create a PR with these changes. Which should I do next?
