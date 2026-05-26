# Migration notes — vNext

This file lists the minimal, high-impact changes introduced in the recent refactor so consumers can upgrade quickly.

## Breaking API / config changes

- Terminology renames (code + config):
  - `AudienceSettings` -> `EndpointSettings`
  - `EndpointInfoSettings` -> `TransportSettings`
  - `AudienceResolver` -> `EndpointResolver`
  - `IAudienceResolver` -> `IEndpointResolver`

- Configuration key name changes (example `appsettings.json`):
  - Old:
    ```json
    "AppSettings": {
      "Itinerary": [
        { "Target": "foo", "Endpoint": { "EntityName": "sbq-foo", "EntityType": "Queue" } }
      ]
    }
    ```
  - New:
    ```json
    "AppSettings": {
      "Itinerary": [
        { "Target": "foo", "Transport": { "EntityName": "sbq-foo", "EntityType": "Queue" } }
      ]
    }
    ```

- `IMessagingProvider.Resolve(string? target)` now returns `EndpointSettings` (formerly `AudienceSettings`). Update callers and using directives.

### Journal API

- `IJournalProvider.WriteRecordAsync` signature changed to accept a single `JournalEntry` record instead of multiple primitive parameters. Update custom `IJournalProvider` implementations and call sites to construct a `JournalEntry` (see `Messaging/Providers/JournalEntry.cs`).

### Storage provider API

- `IStorageProvider` now adds two methods: `Task<Stream> DownloadAsync(string reference, CancellationToken)` and `Task DeleteAsync(string reference, CancellationToken)`. If you maintain a custom storage provider, implement these methods so consumers can resolve claim-check tokens. The default `AzureStorageProvider` implementation supports both absolute blob URIs and relative `container/blob` references.

Migration note: `BaseConsumer.DeserializeMessage` will attempt to download a message payload when a message token exists and `Message` is null. If your provider's `DownloadAsync` differs in semantics, adapt accordingly.

### Testable time (no more DateTime.UtcNow)

- The library now introduces `ISystemClock` with a `DefaultSystemClock` registered in DI. Callers that previously relied on `DateTime.UtcNow` now receive `ISystemClock.UtcNow` via DI; providers and producers use this for deterministic timestamps. Update tests to mock `ISystemClock` when asserting time-dependent behavior.

## Small runtime fixes

- `ExponentialRetryPolicy.InitialDelay` default corrected: previously the default used an `HttpStatusCode` constant (500) indirectly; it now explicitly defaults to `TimeSpan.FromMilliseconds(500)`.

## Recent bugfixes and formatting

- `AzureMessagingProvider.SendAsync` — fixed incorrect property assignment when reading send `Properties`: `Target` now correctly maps to `effectiveTarget`, and `Consumer` maps to `effectiveConsumer`. This prevents incorrect audience resolution when explicit properties are present.

- Repository formatting: added an `.editorconfig` and applied `dotnet format` to normalize brace placement, indentation, and spacing across the codebase. This may produce many whitespace-only edits; review formatting diffs before committing to your main branch.

- `ServiceBusSender` disposal: the Azure provider now disposes the `ServiceBusSender` with `await using` in both `SendAsync` and `RequestReplyAsync` to avoid leaking connections. For higher throughput and reduced allocation overhead, consider implementing a cached sender pool keyed by entity name (the SDK's `ServiceBusSender` instances are thread-safe and safe to reuse). A simple cache with TTL or application-lifetime singletons per entity is recommended.


## DI and startup

- `ConfigurerProviders` now registers `IEndpointResolver`/`EndpointResolver`. Update any manual registrations or tests that referenced the old types.
 - `ConfigurerProviders` also registers `ISystemClock` (`DefaultSystemClock`) as a singleton. If you need deterministic time in tests, provide a test implementation of `ISystemClock` in your test DI setup.

## Code migration checklist

- Update `using` and type references from *Audience* → *Endpoint* and *EndpointInfo* → *Transport* (IDE rename is the fastest and safest).
- Update `AppSettings` configuration keys: replace `Endpoint` child with `Transport` under each itinerary entry.
- Update any tests or mocks that construct `AudienceSettings`/`EndpointInfoSettings`.
- If you implemented custom `IAudienceResolver`, rename and adapt to `IEndpointResolver`.
 - Update any custom `IJournalProvider` implementations to the new `JournalEntry`-based signature. Replace multiple parameter calls with a single `JournalEntry` instance.
 - Replace any direct `DateTime.UtcNow` usages in custom code with an injected `ISystemClock` implementation when integrating with the library.

## Runtime notes

- Claim-check blob references are now stored as relative `container/blob` paths (SAS/query stripped). Consumers must reconstruct full blob URI using configured `BlobServiceUri` + `TokenCredential` when downloading.

### ServiceBusSender reuse

- For Azure Service Bus senders, the library now uses a singleton `ServiceBusSenderCache` to reuse `ServiceBusSender` instances across invocations (registered in DI in `ConfigurerProviders`). This is important when the library is used from stateless hosts such as Azure Functions, which create new scopes per invocation. Reusing `ServiceBusSender` improves throughput and avoids creating many short-lived sender instances.

- Implementation notes:
  - `ServiceBusSenderCache` is registered as a singleton; it exposes `GetOrCreate(ServiceBusClient, string entityName)` to return a cached `ServiceBusSender` for a given entity name.
  - `AzureMessagingProvider` uses the singleton cache instead of creating a new sender per call.
  - The cache disposes its senders when the host shuts down (it implements `IAsyncDisposable`).

Recommendation: if your integration created its own sender-per-call, prefer using the provided cache or a similar singleton factory to improve performance in serverless or high-throughput scenarios.

### Session retry backoff

- The session-mode exponential retry no longer performs a blocking `Task.Delay` inside the Function invocation. Instead, the adapter now schedules a cloned message for the same `SessionId` at the computed retry time and completes the current message. This avoids keeping the Azure Functions worker occupied during backoff periods.

- Implementation notes:
  - `Messaging/Providers/Azure/AzureFunctionMessagingAdapter.cs` now schedules a follow-up message (preserving `SessionId` and key properties) for session-mode retries.
  - The adapter marks `ReferralCount` on scheduled messages for observability and sets `MessageId` to a new value to avoid de-duplication issues.

Recommendation: For long or complex backoff workflows consider Durable Functions (timer orchestrations) or an external scheduler. If you schedule messages directly, ensure `TimeToLive` and retention policies align with your backoff windows.

### Batch publishing

- New APIs are available to publish messages in batches for high-throughput scenarios:
  - `IMessageProducer<T>.PublishBatchAsync(IEnumerable<MessageTransitContext<T>> contexts, ...)` — convenience method implemented by `BaseProducer` to prepare claim-check tokens and forward to the provider.
  - `IMessagingProvider.SendBatchAsync(IEnumerable<MessageTransitContext<T>> contexts, MessagingOptions options, ...)` — provider-level implementation (see `Messaging/Providers/Azure/AzureMessagingProvider.cs` for the Azure implementation using `ServiceBusMessageBatch`).

Migration notes:
  - If you previously sent messages one-by-one in a hot loop, consider migrating to `PublishBatchAsync` for throughput improvements. Be aware of the Service Bus batch size limits and per-message metadata requirements.
  - The Azure provider groups messages by `EntityName` and uses `ServiceBusMessageBatch`. Oversized single messages are sent individually.

## Recommended quick steps

1. Pull latest changes.
2. Run an IDE-level rename for `AudienceSettings` → `EndpointSettings` if you still have local branches referencing old names.
3. Search/replace config files: `"Endpoint": {` → `"Transport": {` under `Itinerary` entries.
4. Rebuild and run unit tests.

---
If you want, I can: (a) run a repo-wide IDE-style rename for any remaining occurrences, (b) update sample `appsettings.*.json` files, or (c) create a short migration PR branch with these changes applied. Which would you like next?
