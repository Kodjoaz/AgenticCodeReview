# Migration — TDF.SeqCon → TDF.Integration Architecture

## Overview

This migration restructures the TDF projects to align with the target architecture documented in `TDFPoC-Spec.md` v3.0 and implements clean separation of concerns:

**Key Changes:**
- `RAMQ.Samples.Queue.TDF.SeqCon.*` → `RAMQ.Samples.Queue.TDF.Integration.*`
- **NEW:** `RAMQ.Samples.Queue.TDF.Integration.Producer` (HTTP Trigger with clean EMT abstraction)
- Frontend delegates to Producer via `ITdfProducerHttpClient` (Refit-based)
- Producer handles message validation, enrichment, and Service Bus publishing

---

## Architecture: Frontend → Producer → Service Bus

### Components

**Producer (TDF.Integration.Producer)**
- HTTP Azure Function (v4)
- Exposes `/api/tdf/transaction/initial` and `/api/tdf/transaction/correlation` endpoints
- Implements `ITdfProducerService` for internal validation/enrichment (VE pattern)
- Exposes `ITdfProducerHttpClient` interface for Frontend consumption
- Manages EMT producer and Service Bus integration

**Frontend (TDF.Integration.Frontend)**
- Calls Producer via `ITdfProducerHttpClient` (Refit HTTP client)
- Handles business logic: generate sessionId, upload files, orchestrate requests
- Decoupled from Service Bus infrastructure
- Can be .NET Framework BackgroundService or Azure Function

**Shared Consumer Library (TDF.Integration.Consumer)**
- Message contracts (`TdfTransactionCommand`, etc.)
- No changes to fundamental logic

---

## Phase 0: Producer Implementation ✓ COMPLETED

### 0.1 Created Producer Project Structure
```
RAMQ.Samples.Queue.TDF.Integration.Producer/
├── RAMQ.Samples.Queue.TDF.Integration.Producer.csproj
├── Program.cs (DI setup with Refit and EMT)
├── Functions/
│   └── TdfProducerFunction.cs (HTTP Trigger endpoints)
├── Services/
│   ├── ITdfProducerService.cs (internal validation/enrichment)
│   ├── TdfProducerService.cs (implementation)
│   ├── ITdfProducerHttpClient.cs (Refit client interface)
│   └── [Request/Response DTOs]
├── Telemetry/
│   └── ProducerTelemetryInitializer.cs
└── local.settings.json
```

### 0.2 Clean Abstraction Pattern

**ITdfProducerService** (server-side)
- `PublishInitialTransactionAsync(TdfTransactionRequest)`
- `PublishCorrelationAsync(TdfCorrelationRequest)`
- Handles: validation, enrichment, EMT producer calls, error handling

**ITdfProducerHttpClient** (client-side, Refit)
- Same signatures as ITdfProducerService
- Frontend uses this to call Producer via HTTP
- Automatically handles serialization/deserialization

**Benefits:**
- Clear contract between Frontend and Producer
- Easy to test (mock ITdfProducerHttpClient in Frontend tests)
- Type-safe HTTP communication
- Decoupled from HTTP implementation details

---

## Phase 1: Rename TDF.SeqCon.* → TDF.Integration.*

### 1.1 Folder/Project Renaming
```powershell
# In RAMQ.Samples.Queue.TDF.SeqCon/
mv "RAMQ.Samples.Queue.TDF.SeqCon.Worker" "RAMQ.Samples.Queue.TDF.Integration.Frontend"
mv "RAMQ.Samples.Queue.TDF.SeqCon.Subscriber" "RAMQ.Samples.Queue.TDF.Integration.Subscriber"
mv "RAMQ.Samples.Queue.TDF.SeqCon.Consumer" "RAMQ.Samples.Queue.TDF.Integration.Consumer"
mv "RAMQ.Samples.Queue.TDF.SeqCon.StateFul" "RAMQ.Samples.Queue.TDF.Integration.Orchestrator"
```

### 1.2 Update .csproj Files
- Rename: `RAMQ.Samples.Queue.TDF.SeqCon.Worker.csproj` → `RAMQ.Samples.Queue.TDF.Integration.Frontend.csproj`
- Repeat for each project
- Update ProjectReference paths in dependent projects

### 1.3 Update Namespaces
```csharp
// Before
namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker
namespace RAMQ.Samples.Queue.TDF.SeqCon.Consumer

// After
namespace RAMQ.Samples.Queue.TDF.Integration.Frontend
namespace RAMQ.Samples.Queue.TDF.Integration.Consumer
```

### 1.4 Update Function Names
```csharp
// Before
[Function(nameof(TdfSeqConWorkerFunction))]
public async Task Run([TimerTrigger(...)] TimerInfo timer)

// After
[Function(nameof(TdfIntegrationFrontendFunction))]
public async Task Run([TimerTrigger(...)] TimerInfo timer)
```

### 1.5 Update Solution File
```xml
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Frontend/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Producer/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Subscriber/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Consumer/..." />
<Project Path="RAMQ.Samples.Queue.TDF.Integration.Orchestrator/..." />
```

---

## Phase 2: HOA5 Component Refactoring

### 2.1 Split HOA5.Consumer
Create two separate projects from `RAMQ.Samples.Queue.HOA5.Consumer`:

**RAMQ.Samples.Queue.HOA5.Integration.Subscriber**
- Azure Function with Service Bus session trigger
- Subscribes to HOA5 messages
- Delegates to Consumer library

**RAMQ.Samples.Queue.HOA5.Integration.Consumer**
- Library project with business logic
- API client for HOA5 backend
- Message processing contracts

### 2.2 Rename HOA5.Backend
```
RAMQ.Samples.Queue.HOA5.Backend 
  ↓
RAMQ.Samples.Queue.HOA5.Integration.Backend
```

---

## Phase 3: Frontend-to-Producer Integration

### 3.1 Update Frontend to Use Producer

**Before (direct to Service Bus):**
```csharp
var producer = services.GetRequiredService<IMessageProducer<TdfTransactionCommand>>();
await producer.PublishAsync(context);
```

**After (via Producer HTTP):**
```csharp
var httpClient = services.GetRequiredService<ITdfProducerHttpClient>();
await httpClient.PublishInitialTransactionAsync(
    new TdfTransactionRequest(sessionId, correlationId, ...));
```

### 3.2 Configuration
```json
{
  "Producer": {
    "BaseUrl": "http://localhost:7071"
  }
}
```

---

## Phase 4: Testing & Validation

### 4.1 Build Verification
```bash
dotnet build
```

### 4.2 Namespace Verification
```bash
grep -r "namespace RAMQ.Samples.Queue.TDF.SeqCon" --include="*.cs"
grep -r "ProjectReference.*TDF.SeqCon" --include="*.csproj"
```

### 4.3 Solution File Validation
```bash
# All projects should resolve
dotnet list package --outdated
```

### 4.4 Integration Tests
```bash
# Ensure Frontend → Producer → Service Bus flow works
dotnet test
```

---

## Final Structure (Target State)

```
RAMQ.Samples.Queue.TDF.SeqCon/
├── RAMQ.Samples.Queue.TDF.Integration.Frontend/
│   ├── Functions/TdfIntegrationFrontendFunction.cs
│   └── ...
├── RAMQ.Samples.Queue.TDF.Integration.Producer/
│   ├── Functions/TdfProducerFunction.cs
│   ├── Services/ITdfProducerService.cs
│   ├── Services/ITdfProducerHttpClient.cs
│   └── ...
├── RAMQ.Samples.Queue.TDF.Integration.Subscriber/
│   └── ...
├── RAMQ.Samples.Queue.TDF.Integration.Consumer/
│   └── ...
├── RAMQ.Samples.Queue.TDF.Integration.Orchestrator/
│   └── ...
├── RAMQ.Samples.Queue.HOA5.Integration.Subscriber/
│   └── ...
├── RAMQ.Samples.Queue.HOA5.Integration.Consumer/
│   └── ...
└── RAMQ.Samples.Queue.HOA5.Integration.Backend/
    └── ...
```

---

## Rollback Strategy

Git tags for safety:
```bash
git tag pre-migration-v3.0
git tag producer-phase-complete
git tag integration-phase-complete
```

If issues arise:
```bash
git reset --hard pre-migration-v3.0
```

---

## Notes

- **Timing:** Execute migration in dedicated branch `refactor/tdf-integration-rename`
- **Communication:** Notify team before starting
- **Testing:** Run full test suite after each phase
- **Documentation:** Update API documentation for Producer endpoints
- **Observability:** Verify Application Insights tracing across Frontend→Producer→Bus
