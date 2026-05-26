# Agentic BizTalk Application Migration – Complete Assessment Plan (v8)

## Audience and Intent

This document is written for a team with **no prior background in agentic systems**. It explains **what each concept means**, **why it exists**, and **how all parts fit together** in our BizTalk migration assessment project.

This is not an AI experiment. It is an **engineered assessment system** that uses AI reasoning in a controlled, auditable way.

---

## 1. What “Agentic” Means (in plain terms)

An *agentic system* is **not a single AI**.

It is a **coordinated system of small, specialized components**, each with **one clear responsibility**, working together under strict rules.

You should think of it like an industrial process:

- Each station does one job
- Stations do not improvise
- The assembly line controls the order
- The final product is predictable and reviewable

---

## 2. The Four Core Building Blocks

Everything in this project is built from **four roles**:

1. **Orchestrator** – controls the flow
2. **Agent** – reasons about one topic
3. **Skill** – extracts raw facts
4. **Prompt** – encodes expert rules

If you understand these four, you understand the whole system.

---

## 3. Orchestrator (The Control Plane)

### What it is

The **Orchestrator** is the component that:
- decides *what runs next*
- ensures steps happen in the right order
- passes outputs from one step to the next
- handles failures safely

It is similar to:
- a workflow engine
- a Durable Functions orchestrator
- a saga coordinator

### What it does NOT do

The Orchestrator:
- does **not** analyze BizTalk
- does **not** interpret meaning
- does **not** apply rules or judgments

It only controls execution.

### Example

```
Run Inventory Agent
If successful → Run Dependency Agent
If failed → Stop assessment safely
```

---

## 4. Agent (Reasoning Unit)

### What an Agent is

An **Agent**:
- focuses on exactly **one topic**
- uses expert reasoning to interpret facts
- produces a **structured output**

Examples in this project:
- Messaging Patterns Agent
- Security Architecture Agent
- Long‑Running Process Agent

Each agent answers one question:
> *“What does the collected data tell us about THIS aspect?”*

### What an Agent is NOT

- Not a workflow engine
- Not a file parser
- Not a data extractor

Agents **never call other agents**.

---

## 5. Skill (Fact Extraction Only)

### What a Skill is

A **Skill** is a simple, deterministic action, such as:
- parse a BizTalk binding file
- read adapter configuration
- extract orchestration shapes
- decompile a DLL

Skills:
- take input
- return raw data
- produce the same result every time

### Golden rule

> **If something “thinks”, it is NOT a skill.**

Skills are intentionally boring.

---

## 6. Prompt (Encoded Expertise)

### What a Prompt is

A **Prompt** defines *how an Agent reasons*.

It contains:
- domain rules (BizTalk patterns)
- classification logic
- conservative assessment principles

Example:
- “Parallel + Receive + Correlation = Parallel Convoy”
- “Kerberos dependency = hard identity coupling”

Prompts never:
- execute code
- control workflows
- access files

---

## 7. Workflow (The Plan)

A **Workflow** is a static definition of:
- which agents must run
- in what order
- with which dependencies

The workflow is:
- interpreted by the Orchestrator
- not executed on its own

Think of it as a *recipe*, not a *chef*.

---

## 8. Routing: Who Decides What

There are **three different routing responsibilities**:

### 8.1 Execution Routing

**Who runs next?**

✅ Handled only by the Orchestrator

Examples:
- Inventory → Dependency
- Skip Pipeline Agent if no pipelines exist

---

### 8.2 Semantic Routing

**What kind of thing is this?**

✅ Done inside individual Agents using Prompts

Examples:
- Sequential vs Parallel Convoy
- Acceptable vs risky authentication

---

### 8.3 Consolidation Routing

**What does everything mean together?**

✅ Done by a dedicated Consolidation Agent

The consolidation agent:
- merges outputs
- resolves overlaps
- produces final conclusions

---

## 9. Consolidation Agent (Final Synthesis)

### Purpose

This agent:
- collects all agent outputs
- normalizes them
- builds the final assessment summary

It does not discover new facts.

This is the **only agent allowed to produce the final report structure**.

---

## 10. Full Execution Flow (End‑to‑End)

```
Orchestrator
  ├─ Inventory Agent
  ├─ Dependency Agent
  ├─ External Systems Agent
  ├─ Adapter & Pipeline Agents
  ├─ Messaging Patterns Agent
  ├─ Long‑Running Semantics Agent
  ├─ Security Architecture Agent
  ├─ Anti‑Patterns Agent
  ├─ Complexity & Risk Agent
  └─ Consolidation Agent
        ↓
   Final Migration Assessment
```

---

## 11. Why This Structure Is Safe for a First Agentic Project

This design ensures:
- no hidden logic
- full replayability
- explainable decisions
- easy debugging
- gradual learning curve

You can inspect any stage independently.

---

## 12. Non‑Negotiable Rules

1. Orchestrator controls execution
2. Agents reason only about their domain
3. Skills extract facts only
4. Prompts contain all judgment logic
5. Consolidation happens once, at the end

If these rules are respected, the system remains stable.

---

## 13. One‑Line Mental Model (Memorize)

> **Orchestrator controls the flow, Agents interpret facts, Skills extract data, Prompts encode expertise, Consolidation synthesizes meaning.**

---

## 14. Final Note

This document exists so that **any new team member** can understand:
- what an agentic system is
- how ours works
- where each responsibility lives

That clarity is what makes this project sustainable.

---

# PART II — Generic BizTalk Migration Assessment Framework

> **Scope**: This part defines the **generic BizTalk migration assessment framework** that `AgenticAppMigration` implements. It is designed to work against **any BizTalk Server application**. The framework is application-agnostic: the agent specifications, skill definitions, and assessment rules apply regardless of domain, industry, or BizTalk version.
>
> Applying this framework to a specific application (such as the HOA5/TDF pilot — see Part III) should produce findings whose quality matches or exceeds that of a human-led architecture review. Every output produced by an agent running this framework must be verifiable against BizTalk source artifacts (binding XML, ODX files, BTM files, C# assemblies). Inference without artifact evidence is not acceptable.

---

## 15. Generic BizTalk Application Context Model

### 15.1 What BizTalk Does in Any Application

Microsoft BizTalk Server is a **message broker and process orchestration platform**. In every BizTalk application, it plays the mediator role: it sits between upstream message producers (inbound clients) and downstream message consumers (backend services or systems), transforming, routing, and coordinating messages according to defined rules.

The migration target for this framework is the **EMT (Enterprise Message Transit)** library pattern on Azure Service Bus, which replaces the BizTalk MessageBox, adapters, and orchestrations while preserving the integration contracts visible to external systems.

> **Pour les développeurs juniors**: BizTalk migration is not a rewrite from scratch. It is a component-by-component substitution. Every BizTalk capability (receive port, orchestration, send port) must have an Azure equivalent, and external systems must not be aware the migration happened.

### 15.2 The Standard BizTalk Layered Architecture

Every BizTalk application follows a predictable layered structure. Agents must map the application under assessment to this model before classifying any finding.

```
Layer 1 – Upstream Clients
  └─ External systems or applications sending messages to BizTalk
  └─ Protocol: WCF, HTTP, SFTP, FILE, MQ, etc.
  └─ Auth: Kerberos, Certificate, Anonymous, Basic

Layer 2 – BizTalk Receive Tier
  └─ Receive Ports + Receive Locations (adapter type + pipeline)
  └─ Entry point for all inbound messages
  └─ Message is written to MessageBox on arrival

Layer 3 – BizTalk MessageBox (SQL Server)
  └─ Central publish/subscribe engine
  └─ Routes messages to orchestrations and send ports via subscriptions
  └─ All routing is implicit (filter-based), not explicit

Layer 4 – BizTalk Orchestration Tier
  └─ Process logic: correlation, long-running transactions, loops, decisions
  └─ Can have atomic or long-running transaction scopes
  └─ Orchestrations subscribe to MessageBox and publish back to it

Layer 5 – BizTalk Send Tier
  └─ Send Ports + Send Pipelines (adapter type + transform maps)
  └─ Delivers processed messages to downstream systems
  └─ May use custom behavior extensions for credential bridging

Layer 6 – Downstream Backend Systems
  └─ Databases, APIs, file systems, queues
  └─ May have locked contracts that cannot be changed by the migration
```

> **Agent rule**: For every port found during inventory, classify it into the layer model above. Missing layers (e.g., no orchestration) must be explicitly noted as `LAYER_NOT_PRESENT`, not assumed.

### 15.3 Integration Constraints — What Must Not Change

Before any migration assessment begins, the framework must identify all **locked contracts** — interfaces that external systems depend on and that cannot be modified by the migration team.

**A contract is locked if any of the following is true**:
- It is consumed by an external organization that the migration team does not own
- It has been published as a stable API with versioning commitments
- It involves a government, regulatory, or compliance-mandated interface
- It has more than 10 consuming clients whose upgrade would require coordinated effort

Agents must flag any proposed migration change that touches a locked contract as `migrationRisk: "BLOCKER"` and require human approval before proceeding.

### 15.4 Backend Integration Atomicity Patterns

BizTalk orchestrations often call backends that perform multiple operations (e.g., file write + database insert). If these operations are not wrapped in a distributed transaction, they create **split-brain risk**: partial success leaves the system in an inconsistent state.

**Agent rule**: For every backend system identified in the Dependency Agent output, classify its write operations as:
- `ATOMIC` — all writes succeed or all fail together (DTC, 2-phase commit)
- `NON_ATOMIC` — writes are independent; partial success is possible
- `COMPENSATED` — no distributed transaction, but an explicit compensation mechanism exists (rollback operation, saga)
- `UNKNOWN` — insufficient source evidence to classify

Any `NON_ATOMIC` backend that is called from a BizTalk send port must be flagged with `severity: "CRITICAL"` in the Anti-Patterns Agent output, along with the specific orphaned-state risk it creates.

---

## 16. BizTalk Artifact Inventory (What Agents Must Scan)

Every agent receives the following artifact set as its input context. Skills produce this structured inventory before any agent runs.

### 16.1 Primary Artifact Types

| Artifact Type        | File Extension | Content                                                        |
|----------------------|----------------|----------------------------------------------------------------|
| Binding file         | `.btdf` / `.xml` | Receive/Send ports, adapters, filters, retry config           |
| Orchestration        | `.odx`         | Shapes, correlation sets, scope transactions, loop conditions  |
| Pipeline component   | `.btp`         | Pipeline stages, custom components, codec settings             |
| Schema               | `.xsd`         | Message structure, promoted properties, namespace              |
| Map                  | `.btm`         | Transform logic, XSLT functoids, cross-schema references       |
| C# assembly          | `.dll` / `.cs` | Custom pipeline components, behavior extensions, business logic|
| App config           | `*.config`     | Endpoint addresses, retry policies, credential configuration   |

### 16.2 How Skills Locate Primary Artifacts

For any BizTalk application, skills must search for artifacts using the following detection approach. If an artifact type is absent, it must be reported as `NOT_FOUND` — never assume defaults.

| Detection Method                                | What Skills Must Report if Missing |
|--------------------------------------------------|-------------------------------------|
| Binding XML in application folder               | `BINDING_NOT_FOUND`                 |
| `.odx` files in project or deployed assembly    | `NO_ORCHESTRATIONS`                 |
| `.btp` pipeline files or compiled pipeline DLLs | `NO_CUSTOM_PIPELINES`               |
| `.xsd` schema files or schema registry          | `NO_SCHEMAS`                        |
| `.btm` map files                                | `NO_MAPS`                           |
| GAC-registered assemblies matching app name     | `NO_GAC_ASSEMBLIES`                 |
| `*.config` files for custom components          | `NO_APP_CONFIG`                     |

> ⚠️ **Naming warning (applies to all applications)**: Port names in the binding file may be misleading. A port named `SendPort_<System>` may actually be a two-way request-response port. Agents must always verify the `IsTwoWay` attribute in the binding XML — never infer from name alone.

---

## 17. Agent Specifications (Full Domain Detail)

This section defines the exact responsibility, inputs, outputs, skills, and prompt rules for each of the 10 agents in the BizTalk migration assessment workflow.

---

### 17.1 Inventory Agent

**Responsibility**: Produce a complete, structured inventory of all BizTalk artifacts in the target application.

**Input artifacts**:
- BizTalk binding file (`.xml`)
- All `.odx` files in the application
- All `.btp`, `.xsd`, `.btm` files
- All referenced C# assemblies (`.dll` names + strong-name info)

**Output schema**:
```json
{
  "orchestrations": [{ "name": "string", "file": "string", "shapeSummary": "string" }],
  "receivePortCount": "integer",
  "sendPortCount": "integer",
  "pipelineComponents": [{ "name": "string", "stage": "string", "isCustom": "boolean" }],
  "maps": [{ "name": "string", "sourceSchema": "string", "targetSchema": "string" }],
  "schemas": [{ "name": "string", "namespace": "string", "promotedProperties": ["string"] }],
  "externalAssemblies": [{ "name": "string", "gacDeployed": "boolean" }],
  "confidence": "float (0-1)"
}
```

**Skills to invoke**:
- `ParseBizTalkBinding` → extract all port definitions
- `ListODXShapes` → enumerate all orchestration shapes from ODX XML
- `ExtractPromotedProperties` → identify all schema promoted properties
- `ListGACAssemblies` → detect GAC-deployed custom assemblies

**Prompt rules**:
- Count receive ports vs send ports separately; do not conflate
- Mark every custom pipeline component as `isCustom: true` regardless of whether it looks standard
- If any orchestration is found whose sole purpose appears to be routing messages to another orchestration (no business logic shapes), flag it with: "implicit routing orchestration detected — must be migrated as a unit with its target"
- Confidence < 0.8 if any `.odx` file fails to parse

---

### 17.2 Dependency Agent

**Responsibility**: Map all external dependencies (systems, databases, credential stores, infrastructure) that BizTalk communicates with.

**Input artifacts**:
- Output of Inventory Agent
- Binding file endpoint addresses
- App.config files for all custom assemblies
- IIS configuration (if available)

**Output schema**:
```json
{
  "externalEndpoints": [{
    "name": "string",
    "protocol": "string",
    "address": "string",
    "authMechanism": "string",
    "tlsRequired": "boolean",
    "migrationRisk": "LOW|MEDIUM|HIGH|BLOCKER"
  }],
  "databases": [{ "name": "string", "type": "Oracle|SQL|Other", "accessPattern": "string" }],
  "infrastructureDependencies": [{ "name": "string", "type": "string", "note": "string" }]
}
```

**Skills to invoke**:
- `ExtractEndpointAddresses` → parse all `Address` elements from binding XML
- `ExtractAuthConfig` → find `securityMode`, `credentialType` in binding XML
- `ScanAppConfig` → extract `<endpoint>` and `<connectionStrings>` from all config files

**Prompt rules**:
- Any endpoint using `securityMode="Transport"` with `clientCredentialType="Windows"` → `authMechanism: "Kerberos"` and `migrationRisk: "BLOCKER"` unless a non-Kerberos alternative is pre-approved
- Any Oracle database dependency → add note: "Oracle JDBC/ODP.NET not natively supported in Azure Service Bus flow; data layer must be preserved in the backend service"
- Any endpoint whose contract is identified as locked (see §15.3) must be listed with `note: "locked contract — migration must preserve this interface exactly"`

---

### 17.3 External Systems Agent

**Responsibility**: Characterize all external systems that BizTalk exchanges messages with, including their ownership, availability SLAs, and sensitivity to protocol changes.

**Input artifacts**:
- Output of Dependency Agent
- Architecture documentation (if available)
- Binding file transport configuration

**Output schema**:
```json
{
  "externalSystems": [{
    "name": "string",
    "owner": "string",
    "messageFormat": "string",
    "protocolLocked": "boolean",
    "changeApprovalRequired": "boolean",
    "notes": "string"
  }]
}
```

**Prompt rules**:
- Any system owned by a government or regulatory body → `protocolLocked: true` and `changeApprovalRequired: true` by default — these systems cannot be changed unilaterally
- Any system with more than 10 known clients consuming a contract → `protocolLocked: true` — coordinated upgrade of all clients is out of scope for a BizTalk migration
- Any system where `owner` is external to the migration team → `changeApprovalRequired: true` by default
- If a system is identified as `protocolLocked: true`, add a note describing what the locked contract covers (operations, message format, transport)

---

### 17.4 Adapter and Pipeline Agent

**Responsibility**: Classify all BizTalk adapters and pipeline components for migration complexity and replacement path.

**Input artifacts**:
- Binding file (adapter type per port)
- Pipeline definitions (`.btp` files)
- Output of Inventory Agent (custom assembly list)

**Output schema**:
```json
{
  "adapters": [{
    "portName": "string",
    "adapterType": "WCF-Custom|WCF-BasicHttp|SFTP|FILE|SQL|Other",
    "isTwoWay": "boolean",
    "customBehaviorExtension": "string|null",
    "migrationPath": "string",
    "migrationRisk": "LOW|MEDIUM|HIGH|BLOCKER"
  }],
  "pipelineComponents": [{
    "name": "string",
    "type": "Encoder|Decoder|Validator|Disassembler|Assembler|Custom",
    "replaceable": "boolean",
    "replacementNote": "string"
  }]
}
```

**Prompt rules**:
- WCF-Custom adapter with `behaviorConfiguration` referencing a non-standard extension → `migrationRisk: "BLOCKER"` — the extension must be located in source, analyzed, and replaced in the EMT pipeline
- Any GAC-deployed WCF behavior extension performing credential bridging (Windows impersonation, token transformation) has no direct Azure Service Bus equivalent → `migrationRisk: "BLOCKER"` with note: "credential bridge — must be redesigned for managed identity or service principal in Azure"
- FILE adapter → `migrationRisk: "LOW"`; replace with Azure Blob Storage trigger
- SFTP adapter → `migrationRisk: "MEDIUM"`; verify key management approach
- SQL adapter → `migrationRisk: "MEDIUM"`; verify whether direct DB writes are safe to expose via Service Bus consumer
- For each adapter, set `isTwoWay` from binding XML `IsTwoWay` attribute — never infer from port name

---

### 17.5 Messaging Patterns Agent

**Responsibility**: Identify all BizTalk messaging patterns that have non-trivial Azure equivalents, focusing on patterns that require explicit design decisions in the migrated system.

**Input artifacts**:
- Output of Inventory Agent (orchestrations + shapes)
- Binding file (port filters, subscriptions)
- ODX shape detail from `ListODXShapes` skill

**Output schema**:
```json
{
  "patterns": [{
    "name": "string",
    "classification": "RequestReply|PublishSubscribe|Convoy|ContentBasedRouting|Aggregator|Scatter-Gather|Other",
    "evidence": "string (source artifact + location)",
    "azureEquivalent": "string",
    "migrationRisk": "LOW|MEDIUM|HIGH|BLOCKER",
    "notes": "string"
  }]
}
```

**Prompt rules (pattern detection)**:
- **Parallel Activating Convoy**: shape sequence `Parallel Receive + Receive shape with Activate=True + CorrelationSet on same set` → `classification: "Convoy"`, `migrationRisk: "HIGH"`. Azure Service Bus sessions can model this but require careful sequence guarantee design.
- **Sequential Convoy**: multiple Receive shapes on the same correlation set in sequence → `classification: "Convoy"`, `migrationRisk: "HIGH"`.
- **Multi-step sequential convoy**: any correlation set followed by 3 or more distinct Receive shapes in sequence → `classification: "Convoy"`, `migrationRisk: "HIGH"`, note: "Azure Service Bus sessions are the natural replacement; session ID = correlation set identifier".
- **Implicit intermediary orchestration** (routing-only): any orchestration with no business-logic shapes (Decision, Transform, Call) that only receives a message and republishes it to MessageBox → `classification: "ContentBasedRouting"` with note: "intermediary routing orchestration — must be migrated as a unit with its downstream target or routing breaks silently".
- **Content-Based Routing via promoted property**: if binding filter uses a promoted property value, flag as `classification: "ContentBasedRouting"` with the property name as evidence.

---

### 17.6 Long-Running Process Agent

**Responsibility**: Identify all BizTalk orchestrations or patterns that require durable, long-running execution semantics, and assess the migratability of each.

**Input artifacts**:
- ODX shape detail (scope shapes, transaction types, compensation blocks)
- Binding file (retry counts, timeout values)
- Output of Messaging Patterns Agent

**Output schema**:
```json
{
  "longRunningConstructs": [{
    "name": "string",
    "type": "AtomicScope|LongRunningScope|Correlation|Loop|Compensation|RetryPort",
    "durationEstimate": "string",
    "retryCount": "integer|null",
    "retryIntervalMinutes": "integer|null",
    "compensation": "boolean",
    "migrationPath": "string",
    "migrationRisk": "LOW|MEDIUM|HIGH|BLOCKER"
  }]
}
```

**Prompt rules**:
- Any `Atomic` transaction scope → check for SQL/Oracle enlistment. If found → `migrationRisk: "BLOCKER"` (DTC is not available in Azure PaaS)
- Any `LongRunning` transaction scope → `migrationRisk: "HIGH"` — must be modeled as a saga or Azure Durable Function with explicit compensation
- For LongRunning scopes, estimate the maximum possible duration from the correlation window (what is the longest gap between the first and last correlated message?). If duration can exceed 1 hour → use Azure Durable Functions eternal orchestration pattern. If duration is reliably under 1 hour → Azure Service Bus sessions are sufficient.
- Any send port with `RetryCount > 100` → flag with note: "high retry count implies an implicit audit trail — if messages are silently discarded after retries exhaust in Azure, this audit trail disappears". Document the business purpose of the retry window (e.g., 1000 retries × 5 min interval = ~83-hour delivery window). The Azure replacement must explicitly reproduce this window.
- `Compensation` blocks in orchestration → list all compensation shapes and their rollback logic as evidence; compensation must be explicitly reimplemented in the migrated system.

---

### 17.7 Security Architecture Agent

**Responsibility**: Identify all authentication, authorization, and transport security mechanisms in the BizTalk application and classify migration risk.

**Input artifacts**:
- Binding file (securityMode, credentialType, identity elements)
- IIS configuration (if available)
- App config (certificates, credential stores)
- Output of Dependency Agent (endpoint auth mechanisms)

**Output schema**:
```json
{
  "securityFindings": [{
    "component": "string",
    "mechanism": "Kerberos|NTLM|Certificate|BasicAuth|Anonymous|Custom",
    "direction": "Inbound|Outbound|Both",
    "hardwareOrInfra": "boolean",
    "migrationRisk": "LOW|MEDIUM|HIGH|BLOCKER",
    "replacementPath": "string",
    "notes": "string"
  }]
}
```

**Prompt rules**:
- **Kerberos on any WCF-Custom Receive Port** (inbound): `migrationRisk: "BLOCKER"` — Azure Service Bus does not natively accept Kerberos tokens. The replacement must implement a credential translation layer (e.g., Azure API Management with Kerberos Constrained Delegation, or certificate/mutual-TLS boundary).
- **Any GAC-deployed WCF behavior extension performing Windows impersonation or delegation** (outbound): `migrationRisk: "BLOCKER"` — this pattern has no direct Azure PaaS equivalent. Must be replaced with Azure Managed Identity with explicit RBAC assignment on the target backend service.
- **Transport TLS only**: `migrationRisk: "LOW"` — Azure Service Bus enforces TLS 1.2+ natively.
- **Certificate pinning** (if found in app config): `migrationRisk: "HIGH"` — certificate thumbprints are environment-specific and must be moved to Azure Key Vault.
- **NTLM** (on any port): `migrationRisk: "HIGH"` — NTLM is not supported in Azure PaaS environments without specific IIS hosting.
- Every security finding must include the exact binding XML path or config key where the mechanism was found, as evidence.

---

### 17.8 Anti-Patterns Agent

**Responsibility**: Identify BizTalk-specific anti-patterns that indicate design debt, operational risk, or architectural coupling that will propagate into the migrated system if not addressed.

**Input artifacts**:
- All outputs from Agents 17.1–17.7
- ODX shape detail
- Binding file

**Output schema**:
```json
{
  "antiPatterns": [{
    "name": "string",
    "location": "string (artifact + element)",
    "severity": "INFO|WARNING|CRITICAL",
    "description": "string",
    "migrationImplication": "string",
    "recommendedAction": "string"
  }]
}
```

**Prompt rules (anti-pattern detection)**:
- **Hardcoded endpoint address in binding**: any `Address` element containing an IP or hostname that is not parameterized → `severity: "WARNING"`. Endpoint addresses are environment-specific and must be moved to Azure App Configuration or Key Vault.
- **Non-atomic backend writes**: if any backend system is classified `NON_ATOMIC` (see §15.4) and is called from a BizTalk send port without compensation → `severity: "CRITICAL"`, `migrationImplication: "partial-success risk — a failed secondary write after a successful primary write leaves the system in an inconsistent state"`.
- **Overly broad subscription filter**: a send port with no filter or `BTS.MessageType = *` → `severity: "WARNING"` — could accidentally pick up messages intended for other applications after migration.
- **Magic-number retry configuration**: `RetryCount` set to a value > 100 with no documented rationale → `severity: "WARNING"`. The business purpose of the retry window must be documented so the Azure replacement does not silently reduce it.
- **Implicit routing-only orchestration**: any orchestration whose sole purpose is to route messages, with its filter as the only routing mechanism → `severity: "CRITICAL"`. If this orchestration is missed during migration, all downstream routing breaks silently.
- **GAC-deployed assembly with no source**: if any GAC assembly name cannot be matched to a known source file → `severity: "CRITICAL"`, `recommendedAction: "locate source or decompile with ILSpy before proceeding with migration"`.

---

### 17.9 Complexity and Risk Agent

**Responsibility**: Produce a quantified risk profile for the BizTalk application under assessment, using inputs from all previous agents.

**Input artifacts**:
- All outputs from Agents 17.1–17.8

**Output schema**:
```json
{
  "overallRisk": "LOW|MEDIUM|HIGH|BLOCKER",
  "riskFactors": [{
    "factor": "string",
    "weight": "LOW|MEDIUM|HIGH|BLOCKER",
    "evidence": "string",
    "mitigable": "boolean",
    "mitigationSummary": "string"
  }],
  "blockers": ["string"],
  "estimatedMigrationComplexity": "1-Story|Sprint|Quarter|Multi-Quarter",
  "pilotSuitability": "SUITABLE|MARGINAL|UNSUITABLE",
  "notes": "string"
}
```

**Prompt rules**:
- If any previous agent reports `migrationRisk: "BLOCKER"` → `overallRisk: "BLOCKER"` and the blocker must appear in `blockers` array
- For each BLOCKER found, determine if it has a known solution pattern in Azure:
  - Kerberos inbound auth → BLOCKER with known pattern (Azure API Management + Kerberos Constrained Delegation, or certificate boundary)
  - GAC-deployed WCF credential bridge extension → BLOCKER with known pattern (Azure Managed Identity + RBAC on backend)
  - DTC / atomic transaction scope → BLOCKER; solution depends on backend capabilities (Saga or Durable Function compensation)
  - Custom GAC assembly with no source → BLOCKER; solution is source recovery or decompilation
- `pilotSuitability: "SUITABLE"` when all BLOCKERs have established solution patterns and the application has bounded complexity. `pilotSuitability: "UNSUITABLE"` only when a BLOCKER has no known resolution path.
- `estimatedMigrationComplexity` must account for blockers: if all blockers have pre-approved solutions, set complexity for each layer independently (EMT core, security redesign, backend adaptation).

---

### 17.10 Consolidation Agent

**Responsibility**: Synthesize all agent outputs into a single, reviewable migration assessment report that a technical architect or project sponsor can act on.

**Input artifacts**:
- All outputs from Agents 17.1–17.9

**Output schema**:
```json
{
  "executiveSummary": "string (3-5 sentences)",
  "applicationProfile": {
    "name": "<application name from binding file>",
    "biztalkVersion": "string",
    "artifactCount": "integer",
    "orchestrationCount": "integer",
    "externalSystemCount": "integer"
  },
  "migrationReadiness": "READY|READY_WITH_CONDITIONS|NOT_READY",
  "blockers": [{ "id": "string", "description": "string", "owner": "string", "resolutionPath": "string" }],
  "highRiskItems": [{ "id": "string", "description": "string", "mitigationSummary": "string" }],
  "recommendedMigrationPattern": "string",
  "hardConstraints": ["string"],
  "decisionPoints": [{ "question": "string", "recommendation": "string", "approvalRequired": "boolean" }],
  "confidenceScore": "float (0-1)",
  "evidenceSources": ["string"]
}
```

**Prompt rules**:
- `migrationReadiness: "NOT_READY"` only if a blocker has no known resolution path. If BLOCKERs have known resolution paths → `migrationReadiness: "READY_WITH_CONDITIONS"`
- `hardConstraints` must list every contract identified by the External Systems Agent as `protocolLocked: true`, plus every `NON_ATOMIC` backend write, plus any custom WCF behavior extension that performs credential bridging
- `decisionPoints` must include one entry per `NON_ATOMIC` backend write asking: "Accept the risk of partial-success as-is OR redesign backend for atomicity?" — this requires human approval.
- `confidenceScore` < 0.7 if Inventory Agent confidence was < 0.8 or if any `.odx` file failed to parse
- `evidenceSources` must list every artifact consulted by name (binding file name, ODX file names, assembly names)

---

## 18. Skill Definitions (Technical Extraction Logic)

Skills are deterministic, side-effect-free extractors. They never reason — they only extract.

### 18.1 `ParseBizTalkBinding`

**Input**: BizTalk binding XML file  
**Output**: Structured list of all receive ports and send ports with their adapter configuration

**Extraction targets**:
```xml
<!-- Receive Port -->
/BindingInfo/ReceivePortCollection/ReceivePort
  → Name, IsTwoWay
  → /ReceiveLocations/ReceiveLocation/ReceiveLocationTransportTypeData
    → Adapter type (TransportType/@Name)
    → Address
    → SecurityMode, ClientCredentialType
    → RetryCount, RetryInterval (from transport type data)

<!-- Send Port -->
/BindingInfo/SendPortCollection/SendPort
  → Name, IsTwoWay, PrimaryTransport/Address
  → Filter expression
  → RetryCount, RetryInterval
  → OutboundTransportCLSID (to identify adapter type)
```

**Error handling**: If any XPath query fails due to missing element, return `null` for that field with a `MISSING` flag — never substitute defaults.

---

### 18.2 `ListODXShapes`

**Input**: BizTalk ODX file (XML format)  
**Output**: Ordered list of orchestration shapes with type, correlation set refs, and transaction context

**Extraction targets**:
```xml
/om:Module/om:PartDef/om:Body  (BizTalk ODX namespace: http://schemas.microsoft.com/BizTalk/2003/design-time)
  → All Shape elements: type (ReceiveShape, SendShape, ScopeShape, ParallelActionsShape, LoopShape, DecideShape, etc.)
  → For ReceiveShape: Activate attribute, CorrelationSets (initialize / follow)
  → For ScopeShape: Transaction attribute (Atomic / LongRunning / None)
  → For DecideShape: Branch conditions
```

**Output format**:
```json
[{
  "shapeType": "string",
  "order": "integer",
  "activate": "boolean|null",
  "transactionType": "Atomic|LongRunning|None|null",
  "correlationSets": { "initializes": ["string"], "follows": ["string"] },
  "children": []
}]
```

---

### 18.3 `ExtractAuthConfig`

**Input**: BizTalk binding XML file  
**Output**: All authentication and security configuration elements per port

**Extraction targets**:
```xml
<!-- WCF-Custom adapter transport type data (serialized XML within CDATA) -->
<security mode="...">
  <transport clientCredentialType="..." />
  <message clientCredentialType="..." />
</security>
<identity>
  <userPrincipalName value="..." />
  <servicePrincipalName value="..." />
</identity>
<endpointBehaviors> → behaviorConfiguration name
```

**Special case — WCF credential bridge**: Any `behaviorConfiguration` name that does not match a standard WCF built-in behavior must be flagged. Extract its name and mark the containing port with `credential_bridge: true`. The actual implementation of the extension will be analysed by the Adapter Agent using `ListGACAssemblies` output.

---

### 18.4 `ListGACAssemblies`

**Input**: BizTalk binding XML + deployment manifest (if available)  
**Output**: List of all assemblies referenced by the application that are marked for GAC deployment

**Extraction targets**:
- `DeploymentConfiguration` elements in binding XML
- `BizTalkAssemblyResourceManager` entries
- Any strong-named assembly reference with `deployToGAC="true"` or equivalent

**Output format**:
```json
[{
  "assemblyName": "string",
  "version": "string",
  "publicKeyToken": "string",
  "gacDeployed": "boolean",
  "sourceFound": "boolean"
}]
```

---

## 19. BizTalk Feature Migration Risk Table

The following table enumerates 17 BizTalk features that may be present in any BizTalk application, their default migration risk rating, and the required Azure replacement pattern. Agents must cross-reference their findings against it.

| # | BizTalk Feature                            | How to Detect                                              | Migration Risk | Azure Replacement Pattern                                      |
|---|--------------------------------------------|------------------------------------------------------------|--------------  |----------------------------------------------------------------|
| 1 | Receive Ports (WCF-Custom)                 | `TransportType/@Name="WCF-Custom"` on ReceiveLocation      | BLOCKER        | Azure API Management + Service Bus ingress                     |
| 2 | Send Ports (WCF-Custom)                    | `TransportType/@Name="WCF-Custom"` on SendPort             | BLOCKER        | EMT Producer with managed identity                             |
| 3 | Orchestration (LongRunning scope)          | ODX: `ScopeShape` with `TransactionType="LongRunning"`     | HIGH           | Azure Durable Functions (Fan-out/Fan-in or Sequential)         |
| 4 | Orchestration (Correlation set)            | ODX: `CorrelationSets` with Receive shapes referencing them | HIGH           | Azure Service Bus Sessions (sessionId = correlation key)       |
| 5 | MessageBox publish/subscribe               | Multiple send ports with filter expressions                | HIGH           | Azure Service Bus Topics + Subscriptions                       |
| 6 | Implicit routing orchestration             | ODX with only Receive/Send shapes, no Transform/Decision   | HIGH           | Explicit routing rule in EMT consumer + topic filter           |
| 7 | Custom behavior extension (GAC)            | `behaviorConfiguration` referencing non-standard name      | BLOCKER        | Redesign: Managed Identity + Azure Key Vault                   |
| 8 | Pipeline (PassThrough)                     | Pipeline component = PassThrough in binding XML            | LOW            | No replacement needed (EMT handles serialization)              |
| 9 | Schema validation pipeline component       | Custom pipeline component DLL in binding                   | MEDIUM         | Azure API Management policy or custom middleware               |
| 10| Maps (XSLT)                                | `.btm` files referenced in send/receive pipelines          | MEDIUM         | Azure Integration Account maps or inline XSLT                  |
| 11| High RetryCount (audit trail semantics)    | `RetryCount > 100` in SendPort binding XML                 | MEDIUM         | Azure Service Bus dead-letter + dead-letter forwarder          |
| 12| WCF-Custom with Kerberos (inbound)         | `SecurityMode=Transport` + `clientCredentialType=Windows`  | BLOCKER        | Azure APIM + Kerberos Constrained Delegation or cert auth      |
| 13| WCF-Custom with Windows auth (outbound)    | `clientCredentialType=Windows` on SendPort transport       | BLOCKER        | Azure Managed Identity with RBAC on backend                    |
| 14| Atomic transaction scope                   | ODX: `ScopeShape` with `TransactionType="Atomic"`          | BLOCKER if YES | Azure Durable Functions activity with saga compensation        |
| 15| BizTalk Server Rule Engine (BRE)           | `CallRulesShape` in ODX or BRE policy deployed             | HIGH if YES    | Azure Logic Apps rule connector or inline rules                |
| 16| EDI/AS2 processing                         | EDI pipeline components or AS2 agreement in binding        | HIGH if YES    | Azure Logic Apps B2B integration account                       |
| 17| Multi-server BizTalk Group                 | Multiple `Server` attributes in binding XML hosts          | MEDIUM if YES  | Azure Service Bus geo-redundancy                               |

> ⚠️ **Assessment rule**: Any feature marked "NOT CONFIRMED" must be explicitly verified against the binding file and ODX files. An agent must not assume absence — it must report `NOT_FOUND_IN_ARTIFACTS` with the artifacts searched.

---

## 20. Assessment Workflow Execution Order

The following is the canonical execution sequence for any BizTalk application assessment. The Orchestrator enforces this order; no agent may skip a predecessor.

```
Phase 1 — Fact Collection (Skills only, no reasoning)
  ├── ParseBizTalkBinding(binding_file)
  ├── ListODXShapes(<all .odx files found by ParseBizTalkBinding or in deployment package>)
  ├── ExtractAuthConfig(binding_file)
  └── ListGACAssemblies(binding_file)

Phase 2 — Structural Agents (can run in parallel after Phase 1)
  ├── Inventory Agent          [input: all Phase 1 outputs]
  ├── Dependency Agent         [input: all Phase 1 outputs]
  └── External Systems Agent   [input: Dependency Agent output]

Phase 3 — Pattern Agents (sequential, each depends on Phase 2)
  ├── Adapter and Pipeline Agent     [input: Inventory + Dependency outputs]
  ├── Messaging Patterns Agent       [input: Inventory + ODX shapes]
  ├── Long-Running Process Agent     [input: Inventory + ODX shapes + Binding retry config]
  └── Security Architecture Agent    [input: Dependency + Auth config]

Phase 4 — Synthesis Agents (sequential, depend on Phase 3)
  ├── Anti-Patterns Agent            [input: all Phase 2-3 outputs]
  └── Complexity and Risk Agent      [input: all Phase 2-3 outputs + Anti-Patterns output]

Phase 5 — Consolidation (single agent, all inputs)
  └── Consolidation Agent            [input: all Phase 1-4 outputs]
```

**Failure handling per phase**:
- Phase 1 skill failure → STOP. Assessment cannot proceed without raw facts.
- Phase 2 agent failure → STOP if Inventory Agent fails; continue with partial inputs if Dependency or External Systems Agent fails.
- Phase 3 agent failure → Skip failed agent, mark output as `INCOMPLETE`, continue. Consolidation Agent must note the gap.
- Phase 4 agent failure → Anti-Patterns: skip and note; Risk Agent: if fails, Consolidation Agent uses `overallRisk: "UNKNOWN"`.
- Phase 5 failure → Escalate to human operator with all Phase 1-4 outputs as raw evidence.

---

## 21. Evidence and Confidence Standards

All agent outputs must meet the following evidence standards. Outputs that do not cite evidence are treated as LOW-CONFIDENCE and flagged for human review.

### 21.1 Evidence Citation Format

Every finding must include an `evidence` field in the following format:

```
evidence: "<artifact_name> → <xpath_or_location> → <exact_value>"
```

**Examples** (substitute actual artifact names for the application under assessment):
```
evidence: "<AppName>_binding.xml → /BindingInfo/SendPortCollection/SendPort[@Name='SP_Backend']/PrimaryTransport/RetryCount → 1000"

evidence: "<OrchestrName>.odx → ScopeShape[name='MainProcess'] → TransactionType=LongRunning"

evidence: "<ExtensionAssembly>.dll → GAC deployment confirmed via binding DeploymentConfiguration/@deployToGAC=true"
```

### 21.2 Confidence Scoring Rules

| Condition                                                  | Confidence Impact |
|------------------------------------------------------------|-------------------|
| All artifacts found and parseable                          | Base: 0.9         |
| Any artifact NOT_FOUND                                     | −0.15 per missing |
| Any ODX parse failure                                      | −0.2              |
| Any GAC assembly with no source                            | −0.1              |
| Finding derived from naming convention, not explicit value | −0.1              |
| Human-confirmed finding (cross-reference with architecture-cible.md) | +0.05  |

> **Pour les développeurs juniors**: The confidence score is not a quality metric for the agent — it tells the human reviewer how much of the assessment is based on hard evidence vs. inference. A score of 0.7 means "we found solid evidence for 70% of our claims; 30% may need human verification." This is expected for a first automated pass against a legacy codebase.

### 21.3 Conflict Resolution

If two agents produce conflicting findings about the same artifact:
1. The more specific finding wins (exact XPath evidence beats generic naming convention inference)
2. The conflict must be reported in the Consolidation Agent output under `conflicts`
3. Conflicts are always escalated to human review — agents must never silently resolve conflicts by majority vote

---

## 22. BizTalk Migration Decision Tree

The following decision tree is an agent-consumable reference for the Complexity and Risk Agent. It encodes the canonical decision logic for any BizTalk-to-Azure-Service-Bus migration.

```
START: BizTalk assessment complete?
│
├─ Kerberos inbound auth confirmed on any Receive Port?
│   ├─ YES → Is Azure APIM + KCD solution pre-approved?
│   │         ├─ YES → migrationRisk = HIGH (manageable)
│   │         └─ NO  → migrationRisk = BLOCKER
│   └─ NO  → Continue
│
├─ GAC-deployed WCF behavior extension performing credential bridging found?
│   ├─ YES → Is Azure Managed Identity + backend RBAC available?
│   │         ├─ YES → migrationRisk = HIGH (credential redesign needed)
│   │         └─ NO  → migrationRisk = BLOCKER
│   └─ NO  → Continue
│
├─ Atomic scope with DTC / database enlistment found?
│   ├─ YES → migrationRisk = BLOCKER (DTC not available in Azure PaaS)
│   └─ NO  → Continue
│
├─ LongRunning scope found in any orchestration?
│   ├─ YES → Can max correlation window exceed 1 hour?
│   │         ├─ YES → Use Azure Durable Functions eternal orchestration (HIGH)
│   │         └─ NO  → Use Azure Service Bus sessions (MEDIUM)
│   └─ NO  → Continue
│
├─ NON_ATOMIC backend write pattern found?
│   ├─ YES → Business accepts partial-success risk?
│   │         ├─ YES → Document acceptance, migrationRisk = MEDIUM
│   │         └─ NO  → Redesign backend for atomicity (HIGH + human decision needed)
│   └─ NO  → Continue
│
├─ Routing-only intermediary orchestration found?
│   ├─ YES → migrationRisk = HIGH; both orchestrations must be migrated as a unit
│   └─ NO  → Continue
│
└─ No blockers found → overallRisk = MEDIUM (standard migration complexity)
```

---

## 23. BizTalk Migration Glossary

Agents must use these terms consistently in their outputs to ensure cross-agent traceability.

| Term                                  | Definition                                                                                                                    |
|---------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| **Binding XML**                       | The BizTalk application export file containing all port configurations, adapter settings, and orchestration-to-port bindings  |
| **Receive Port / Receive Location**   | BizTalk entry point for inbound messages. A Receive Port can have multiple Receive Locations with different adapters           |
| **Send Port**                         | BizTalk egress point. Can be one-way (fire-and-forget) or two-way (request/response). Determined by `IsTwoWay` in binding XML |
| **MessageBox**                        | SQL Server database that acts as the central publish/subscribe message store in BizTalk                                        |
| **Orchestration (ODX)**               | BizTalk process definition compiled from `.odx` source. Contains shapes (Receive, Send, Scope, Decision, Transform, etc.)    |
| **Correlation Set**                   | BizTalk mechanism for correlating multiple messages to the same orchestration instance. Migrates to Azure Service Bus Sessions |
| **LongRunning Scope**                 | Orchestration transaction scope that can span hours or days; uses compensation instead of rollback                            |
| **Atomic Scope**                      | Orchestration transaction scope backed by DTC (Distributed Transaction Coordinator) — not available in Azure PaaS             |
| **Custom Behavior Extension (GAC)**   | A WCF behavior extension compiled to a DLL and deployed to the Global Assembly Cache. Used for credential bridging, logging, etc. |
| **Credential Bridge**                 | A WCF behavior extension that transforms inbound auth tokens (e.g., Kerberos) into outbound credentials (e.g., Windows service account) |
| **PassThrough Pipeline**              | A BizTalk pipeline that performs no transformation. Low migration risk — EMT handles serialization natively                   |
| **Dead-Letter Queue (DLQ)**           | Azure Service Bus concept for undeliverable messages. Equivalent to BizTalk Suspended Message Queue for send port retries     |
| **Implicit Routing Orchestration**    | A BizTalk orchestration whose only purpose is to subscribe to the MessageBox and republish messages to a more specific subscription |
| **EMT / Enterprise Message Transit**  | The Azure Service Bus–based messaging library that is the migration target for all BizTalk applications in scope              |
| **BLOCKER**                           | A migration risk so severe that it must be resolved before migration can proceed. Requires explicit human approval            |
| **NON_ATOMIC**                        | A backend write pattern where multiple operations can succeed or fail independently, creating partial-success (split-brain) risk |
| **Protocol Locked**                   | A contract (`protocolLocked: true`) that cannot be changed because it is owned by an external party or serves more than 10 clients |

---

# Part III — HOA5/TDF Validation Target

> **Purpose**: This part documents the HOA5/TDF BizTalk application as the **validation case** for `AgenticAppMigration`. When the framework (built from Parts I and II) is run against the HOA5 artifacts, it must produce an assessment that matches the quality and accuracy of `architecture-cible.md` — the benchmark produced with extensive human involvement. Matching that benchmark proves the agentic framework is production-ready.

---

## 24. HOA5/TDF Application Profile

### 24.1 What HOA5/TDF Is

HOA5 is the Quebec healthcare data transmission subsystem (**Transmissions préliminaires**). It is part of the broader **TDF (Transmission de données de facturation)** BizTalk application family. Approximately **140 hospitals (CH — Centres Hospitaliers)** use it to send patient record batches to **RAMQ (Régie de l'assurance maladie du Québec)** for billing reconciliation.

The BizTalk application named **HOA5 Transmissions préliminaires** runs on Azure IaaS virtual machines and implements the 3-step WCF protocol between the CH hospitals and the RAMQ Oracle backend.

HOA5 was chosen as the pilot validation case because:
- It has bounded, well-understood scope (two orchestrations, two ports per direction, one custom extension)
- A complete, high-quality human assessment (`architecture-cible.md`) exists as the accuracy benchmark
- It contains all major BizTalk complexity categories: LongRunning scope, correlation sets, Kerberos auth, credential bridge, non-atomic writes, implicit routing

### 24.2 HOA5 Artifact Inventory

| Artifact                             | Role                                                    | Migration Risk |
|--------------------------------------|---------------------------------------------------------|----------------|
| `orcTrnsmFichEntrant` (ODX)          | Main orchestration: 3-step correlation, LongRunning scope | HIGH         |
| `IndExecOrcSpec` (ODX)               | Implicit routing orchestration: TDF-to-HOA5 2-hop routing | HIGH         |
| `TranfBasic2IntegBehaviorExtn` (DLL) | GAC-deployed WCF credential bridge extension            | BLOCKER        |
| `ServiceBusTDFHOA5.xml` (binding)    | Receive/Send port config for HOA5 transmissions         | HIGH           |
| `CorNoEchg` (correlation set field)  | Correlation identifier across the 3-step WCF protocol   | MEDIUM         |
| Port retry config (binding XML)      | `RetryCount=1000, RetryInterval=5 min` — ~83-hour window | MEDIUM        |

### 24.3 The 3-Step WCF Protocol (Locked Contract)

The HOA5 backend exposes a WCF service contract that has been in production for years. **This contract must not be modified by the migration**.

| Step | Operation          | SOAP Action                         | Direction                     |
|------|--------------------|-------------------------------------|-------------------------------|
| 1    | InitierEnvoi       | `http://…/InitierEnvoi`             | CH → BizTalk → HOA5 Backend   |
| 2    | EnvoyerLotFichier  | `http://…/EnvoyerLotFichier`        | CH → BizTalk → HOA5 Backend   |
| 3    | CorrellerEnvoyer   | `http://…/CorrellerEnvoyer`         | CH → BizTalk → HOA5 Backend   |

The three steps are correlated via the `CorNoEchg` field in the message payload, implemented as a BizTalk correlation set on the `orcTrnsmFichEntrant` orchestration.

### 24.4 Physical Architecture (6 Layers, HOA5-Specific)

```
Layer 1 – CH (Hospital)
  └─ ~140 CH clients send SOAP/WCF over TLS using Kerberos tokens

Layer 2 – BizTalk WCF-Custom Receive Port (IIS/WCF host, Kerberos auth)
  └─ Authenticates the CH, forwards messages to BizTalk MessageBox

Layer 3 – BizTalk MessageBox (SQL Server)
  └─ Routes to IndExecOrcSpec (TDF routing orchestration)

Layer 4a – IndExecOrcSpec (implicit routing orchestration)
  └─ Subscribes from MessageBox, republishes to HOA5-specific subscription

Layer 4b – orcTrnsmFichEntrant (main HOA5 orchestration)
  └─ Manages 3-step correlation, LongRunning scope, retry logic, backend call

Layer 5 – BizTalk WCF-Custom Send Port with TranfBasic2IntegBehaviorExtn
  └─ Credential bridge: Kerberos → Windows service account impersonation
  └─ Delivers to HOA5 Backend (WCF over net.tcp or HTTP)

Layer 6 – HOA5 Backend + RAMQ Oracle Database
  └─ DepoApli: writes batch file to local filesystem
  └─ Oracle insert: persists batch record to RAMQ database (NON_ATOMIC pair)
```

### 24.5 Known Risk Profile (HOA5)

| Risk                                   | Severity | Notes                                                               |
|----------------------------------------|----------|---------------------------------------------------------------------|
| Kerberos inbound auth (Layer 2)        | BLOCKER  | Azure SB cannot accept Kerberos tokens natively; APIM + KCD needed |
| TranfBasic2IntegBehaviorExtn (Layer 5) | BLOCKER  | No direct Azure PaaS equivalent; must redesign with Managed Identity |
| Non-atomic DepoApli + Oracle (Layer 6) | HIGH     | Orphan file risk; not a BLOCKER if business accepts the risk explicitly |
| 3-step WCF correlation (Layer 4b)      | HIGH     | Azure Durable Functions or Service Bus sessions required            |
| 1000-retry audit trail                 | MEDIUM   | Reproducible with Azure retry policies + DLQ monitoring             |
| Implicit 2-hop routing (Layer 4a)      | HIGH     | Both orchestrations must be migrated as a unit                      |

---

## 25. Expected Assessment Findings

When `AgenticAppMigration` is run against the HOA5 artifacts, the following findings must appear in its output. If any are absent, the agentic framework has a coverage gap that must be addressed before the tool is considered validated.

### 25.1 Mandatory Findings (from architecture-cible.md)

**Inventory Agent must report**:
- 1 Receive Port (WCF-Custom, IsTwoWay=true)
- 2 Send Ports (WCF-Custom)
- 2 Orchestrations: `orcTrnsmFichEntrant`, `IndExecOrcSpec`
- 1 GAC assembly: `TranfBasic2IntegBehaviorExtn`
- 1 Correlation set: `CorNoEchg`

**Dependency Agent must report**:
- Auth mechanism `Kerberos` on the inbound receive port
- Endpoint classified as `protocolLocked: true` for the WCF 3-step contract
- Oracle database backend identified as external dependency with `protocolLocked: true`

**External Systems Agent must report**:
- `CH hospitals (~140 clients)` as `protocolLocked: true, changeApprovalRequired: true`
- `RAMQ Oracle backend` as `protocolLocked: true, changeApprovalRequired: true` (government-owned)

**Adapter Agent must report**:
- `TranfBasic2IntegBehaviorExtn` as `migrationRisk: "BLOCKER"`, `credential_bridge: true`
- Kerberos inbound as `migrationRisk: "BLOCKER"`

**Messaging Patterns Agent must report**:
- `CorNoEchg` correlation set as `classification: "Convoy"`, `migrationRisk: "HIGH"`
- `IndExecOrcSpec` as an implicit routing orchestration with note about 2-hop migration dependency

**Long-Running Process Agent must report**:
- `orcTrnsmFichEntrant` with `TransactionType=LongRunning`, estimated duration > 1 hour (hospital submission delays)
- Send port `RetryCount=1000, RetryInterval=5min` — audit trail semantics

**Security Agent must report**:
- Kerberos inbound: `BLOCKER`
- `TranfBasic2IntegBehaviorExtn` credential bridge: `BLOCKER`

**Anti-Patterns Agent must report**:
- `DepoApli + Oracle` as `NON_ATOMIC`, `severity: "CRITICAL"`, orphan file risk explicitly named
- `IndExecOrcSpec` as implicit routing dependency: `severity: "CRITICAL"`
- `RetryCount=1000` magic number: `severity: "WARNING"`, audit trail purpose documented

**Risk Agent must report**:
- `overallRisk: "BLOCKER"` (two confirmed BLOCKERs)
- `pilotSuitability: "SUITABLE"` (BLOCKERs have known resolution paths: APIM + Managed Identity)
- `estimatedMigrationComplexity: "Quarter"` (security redesign layer)

**Consolidation Agent must report**:
- `migrationReadiness: "READY_WITH_CONDITIONS"`
- `hardConstraints` must include: WCF 3-step contract, RAMQ Oracle protocol, CH client transparency, CorNoEchg session key preservation
- `blockers` must include: Kerberos auth solution required, credential bridge redesign required
- `confidenceScore >= 0.85` (all primary artifacts parseable)

### 25.2 Evidence Citation Quality Standard

Every finding above must include an `evidence` field citing the exact artifact, XPath, and value. **Examples of what acceptable evidence looks like**:

```
evidence: "ServiceBusTDFHOA5.xml → /BindingInfo/ReceivePortCollection/ReceivePort[@Name='RP_HOA5_CH']/ReceiveLocations/ReceiveLocation/ReceiveLocationTransportType/@Name → WCF-Custom"

evidence: "ServiceBusTDFHOA5.xml → /BindingInfo/SendPortCollection/SendPort[@Name='SP_HOA5_Backend']/PrimaryTransport/RetryCount → 1000"

evidence: "orcTrnsmFichEntrant.odx → ScopeShape[name='Traitement3Etapes'] → TransactionType=LongRunning"

evidence: "ServiceBusTDFHOA5.xml → OutboundTransportCLSID matches TranfBasic2IntegBehaviorExtn → GAC assembly deployment confirmed"
```

---

## 26. Validation Criteria Against architecture-cible.md

### 26.1 Validation Method

After running `AgenticAppMigration` against the HOA5/TDF artifacts, compare the tool output against `architecture-cible.md` using the following evaluation matrix. A human reviewer must perform this comparison for the first validation run.

### 26.2 Validation Matrix

| Validation Dimension                             | Passing Criteria                                               | Source in architecture-cible.md |
|--------------------------------------------------|----------------------------------------------------------------|----------------------------------|
| 3-step WCF protocol identified                   | All 3 operations named, locked contract flagged                | §3.1 Protocol Details            |
| orcTrnsmFichEntrant role identified              | LongRunning scope, 3-step correlation, retry logic all named   | §4.2 Orchestration Analysis      |
| IndExecOrcSpec 2-hop routing identified          | Implicit routing orchestration flagged, migration unit noted   | §4.3 Routing Analysis            |
| TranfBasic2IntegBehaviorExtn BLOCKER raised      | Credential bridge identified, BLOCKER severity, Azure path noted | §5.1 Security Analysis          |
| Kerberos inbound BLOCKER raised                  | Inbound auth mechanism identified as Kerberos, BLOCKER severity | §5.2 Auth Configuration         |
| Non-atomic DepoApli + Oracle risk identified     | Both write operations named, NON_ATOMIC classification, orphan risk described | §6.1 Backend Integration |
| RetryCount=1000 audit trail identified           | RetryCount=1000, RetryInterval=5min extracted from binding, business purpose documented | §7.1 Retry Config |
| CorNoEchg correlation mapped to SB sessions      | Correlation set → Azure Service Bus session equivalence noted  | §4.4 Correlation Design          |
| Locked contracts identified for CH + RAMQ        | Both parties flagged as protocolLocked, change approval required | §2.1 External Systems           |
| Overall risk = BLOCKER, pilot suitable           | overallRisk=BLOCKER, pilotSuitability=SUITABLE, two blockers named | §8.1 Risk Assessment            |
| Migration readiness = READY_WITH_CONDITIONS      | Condition: security redesign (APIM + Managed Identity) required | §9.1 Migration Readiness         |

### 26.3 Pass/Fail Threshold

- **Full Validation** (tool declared production-ready): All 11 dimensions pass on the first automated run without human correction.
- **Conditional Validation** (tool is useful, minor gaps): ≥ 9 of 11 dimensions pass. Remaining gaps are documented as known limitations.
- **Validation Failed** (tool must be improved): < 9 dimensions pass. Root causes must be identified, framework updated, and the full validation run repeated.

### 26.4 Quality Comparison — Agentic Output vs. Human Output

The human-produced `architecture-cible.md` (2,108 lines) sets the quality benchmark. The agentic assessment output must match it on **accuracy**, not necessarily on **length**. Specifically:

| Quality Dimension              | Human Benchmark (architecture-cible.md) | Agentic Target                              |
|--------------------------------|-----------------------------------------|---------------------------------------------|
| Factual accuracy of findings   | 100% (ground truth)                     | ≥ 95% (0 missed BLOCKERs, ≤ 1 missed HIGH) |
| Evidence citation              | Descriptive prose                       | Machine-readable XPath + artifact reference  |
| Structured output format       | Markdown narrative                      | JSON schema per agent + Markdown summary     |
| Coverage of risk categories    | 17 BizTalk features assessed            | 17 features assessed (§19 table complete)    |
| Decision traceability          | Human reasoning narrative               | Prompt rule reference per finding            |

---

# Part IV — Assessment Output Document Template (Customer Quality Gate)

> **Purpose**: This part defines the **mandatory output document** that `AgenticAppMigration` must produce for every assessed BizTalk application. This document is the deliverable that the customer reviews and approves. It is modeled on the quality standard set by `architecture-cible.md` (2,108 lines, human-produced) and must reach the same level of factual precision and structural completeness. The document undergoes a formal quality gate before handoff.

> **Benchmark**: `architecture-cible.md` is the reference for depth, evidence citation rigor, component table completeness, and step-by-step flow diagram precision. Every section in this template has a corresponding section in that benchmark document.

---

## 27. Output Document Structure and Quality Gate Rules

### 27.1 Document Identity

Every generated output document must carry the following header block. Fields in `<angle brackets>` are filled by the Consolidation Agent.

```markdown
# <ApplicationName> — BizTalk Migration Assessment Report

> **Objective**: Document the current-state integration architecture of <ApplicationName>,
> providing a reliable, evidence-based foundation for migration planning to Azure.
> **Target audience**: Enterprise Architecture, Integration Team, Operations, Security,
> and AI-assisted tooling.
> **Status**: Draft v1.0 — pending customer quality gate approval
> **Prepared by**: AgenticAppMigration v<ToolVersion> — <AssessmentDate>
> **Reviewer**: <HumanReviewerName> (required before status changes to "Approved")
> **Confidence score**: <0.0–1.0> (see §10 for scoring rules)
```

### 27.2 Mandatory Sections

The following 14 sections must appear in every output document. A document missing any section **fails the quality gate** and cannot be delivered to the customer.

| § | Section Title                          | Owner Agent(s)                    | Quality Gate Criterion |
|---|----------------------------------------|-----------------------------------|------------------------|
| 1 | Project Context                        | Inventory + External Systems      | Data sensitivity named; sending entity count present; destination system named |
| 2 | Component Inventory Table              | Inventory Agent                   | Every component has Team, Technology, Role, Mutable? columns filled |
| 3 | Constraint Analysis (Mutable vs. Locked) | External Systems + Dependency   | Every component with `protocolLocked: true` is listed with justification |
| 4 | End-to-End Integration Flow            | Messaging Patterns + Inventory    | At least one ASCII diagram per distinct operation; transport mechanisms named |
| 5 | Physical Architecture (Layer View)     | Inventory + Adapter Agent         | 6-layer diagram present; every layer names the hosting model |
| 6 | Messaging Patterns                     | Messaging Patterns Agent          | Orchestration shapes described; convoy type named; correlation set mapped to Azure equivalent |
| 7 | Security Architecture                  | Security Agent                    | Inbound auth chain complete; outbound credential chain complete; all BLOCKERs listed |
| 8 | Non-Atomic Write Analysis              | Anti-Patterns Agent               | Every backend write pair analyzed; orphan risk quantified; business acceptance status noted |
| 9 | Retry and Audit Configuration          | Long-Running Process Agent        | All RetryCount + RetryInterval values extracted from binding XML with evidence |
| 10 | Migration Risk Table                  | Risk Agent                        | Every component has severity + Azure mitigation path |
| 11 | Verified Facts                         | All agents (evidence citations)   | ≥ 10 facts numbered and traced to source artifact + XPath |
| 12 | Hard Constraints                       | External Systems + Dependency     | Every constraint names who owns it and why it cannot change |
| 13 | Migration Decision and Readiness       | Consolidation Agent               | `migrationReadiness` enum value present; `pilotSuitability` present; blocker list present |
| 14 | Open Questions                         | Consolidation Agent               | ≥ 1 entry; each has owner and decision deadline |

### 27.3 Quality Gate Enforcement Rules

Before the document is delivered to the customer:

1. **Completeness check**: All 14 sections present — automated by the Consolidation Agent's self-review.
2. **Evidence density check**: ≥ 85% of findings contain an `evidence:` field with artifact + XPath + value — automated.
3. **BLOCKER completeness check**: Every finding with `severity: "BLOCKER"` must appear in §7 (Security), §10 (Risk Table), and §13 (Migration Decision). Cross-reference is mandatory — automated.
4. **Human review**: A named human reviewer must read and sign off on §12 (Hard Constraints) and §13 (Migration Decision) before status changes from "Draft" to "Approved".
5. **Conflict resolution**: Any `conflicts` entries in the Consolidation Agent's JSON output must be resolved or explicitly deferred before approval.

> **Why a quality gate?** The migration decision based on this document may require months of engineering work and architectural redesign. A wrong BLOCKER classification (false positive) wastes budget; a missed BLOCKER (false negative) causes a failed migration. The quality gate exists to catch both before commitment.

---

## 28. Complete Output Document Template

The following is the exact Markdown template that the Consolidation Agent must use to render the output document. Replace every `<placeholder>` with actual assessed values. Inline comments (`<!-- instruction -->`) are guidance for the generating agent and must NOT appear in the final document.

---

```markdown
# <ApplicationName> — BizTalk Migration Assessment Report

> **Objective**: Document the current-state integration architecture of <ApplicationName>,
> providing a reliable, evidence-based foundation for migration planning to Azure.
> **Target audience**: Enterprise Architecture, Integration Team, Operations, Security, and AI-assisted tooling.
> **Status**: Draft v1.0 — pending customer quality gate approval
> **Prepared by**: AgenticAppMigration v<ToolVersion> — <AssessmentDate>
> **Reviewer**: <HumanReviewerName>
> **Confidence score**: <0.0–1.0>

---

## 1. Project Context

### 1.1 Nature of Data

<ApplicationName> processes **<data sensitivity classification>** data related to
<business domain description>. These data are used to:

- <Business purpose 1>
- <Business purpose 2>

> **Sensitivity**: <classification label>. Any processing outside production must
> be validated with Security and Compliance teams.

<!-- QUALITY GATE: data sensitivity classification must be named (e.g., "Confidential", "PII", "PHI"). -->
<!-- SOURCE: Inventory Agent output → applicationProfile.dataSensitivity -->

### 1.2 Transmission / Message Types

<!-- Fill this table with one row per distinct message type handled by the application. -->

| Type | Code | Avg. Size | Max Size (uncompressed) | Frequency | Condition |
|------|------|-----------|------------------------|-----------|-----------|
| <MessageType1> | <Code1> | <AvgSize> | <MaxSize> | <Frequency> | <Condition> |

**Number of sending entities**: <count> <description (e.g., "hospitals across Quebec")>

**Final destination**: <destination system and storage type>

<!-- SOURCE: Inventory Agent → messageTypes[] + External Systems Agent → sendingEntities -->

### 1.3 End-to-End Architecture Overview (Component Summary)

<!-- One-paragraph narrative describing the flow from sending entities to final destination. -->
<!-- Reference the 6-layer diagram in §5. -->

<ApplicationName> receives <message type> from <sending entities> via <protocol>.
The integration layer routes messages through <BizTalk orchestration(s)> before
delivering to <backend system>. <Key processing steps in 1-2 sentences>.

> **Team ownership — important for migration coordination**:
> - **<Component1>** is owned and maintained by the **<Team1>** team (repo: `<repo1>`).
>   Changes require coordination with <Team1>.
> - **<Component2>** is owned by the **<Team2>** team (repo: `<repo2>`).

> **Migration constraint — WCF mutability boundaries (project assumption)**:
> - **<FrontendComponent>** (external-facing): its service contract is **locked** —
>   project assumption: <external client type> does not change. The interface must
>   be preserved as-is during migration.
> - **<BizTalkComponent(s)>** (inside BizTalk): **must be re-platformed to Azure** —
>   BizTalk retirement is the primary migration objective.
> - **<BackendComponent>**: may be modified when technically justified, evidence-based,
>   and governed (Architecture Board approval required).

---

## 2. Component Inventory Table

<!-- Every component that participates in message processing must have a row. -->
<!-- "Mutable?" = can this component be changed during migration? -->

| Component | Team | Technology | Role | Mutable? | Evidence |
|-----------|------|------------|------|----------|---------|
| **<Component1>** | <Team> | <Tech (e.g., WCF Service on IIS/IaaS)> | <Role description> | <Locked / Mutable with approval / Must migrate> | `<artifact>` → `<xpath>` |
| **<Component2>** | <Team> | <Tech> | <Role description> | <...> | `<artifact>` → `<xpath>` |
| **<BizTalkOrchestration1>** | <Team> | BizTalk Server on Azure IaaS | <Orchestration role> | Must migrate | `<odx_file>` → `TransactionType=<value>` |
| **<ExternalSystem>** | <ExternalOwner> | <Tech> | <Backend role> | Protocol locked | External party ownership — no artifact evidence available |

<!-- QUALITY GATE: Every row must have a non-empty Evidence field. -->
<!-- QUALITY GATE: "Mutable?" must use one of: Locked | Mutable (with approval) | Must migrate | Protocol locked -->

---

## 3. Constraint Analysis — Mutable vs. Locked Components

### 3.1 Locked Components (Cannot Change)

<!-- List every component whose interface/contract cannot be modified. -->
<!-- Justification must name the reason: external clients, government ownership, protocol age, etc. -->

| Component | What Is Locked | Why Locked | Who Owns the Lock | Impact on Migration |
|-----------|---------------|------------|-------------------|---------------------|
| <Component> | <Interface/contract/endpoint URL> | <Reason: e.g., "~N external clients, contract in place since YYYY"> | <Owner> | <Migration must preserve this contract exactly> |

### 3.2 Mutable Components (Can Change with Governance)

| Component | Change Requires | Governance Path | Evidence of Mutability |
|-----------|----------------|-----------------|------------------------|
| <Component> | <What approval is needed> | <Architecture Board / Team Lead / etc.> | <Source evidence> |

---

## 4. End-to-End Integration Flow (Step by Step)

<!-- Model this section after architecture-cible.md §1.3.2. -->
<!-- One subsection per distinct operation. Include ASCII diagrams showing ALL components. -->
<!-- Every arrow must name the transport mechanism (HTTP POST, SOAP proxy, in-process .NET call, etc.) -->

### 4.1 Overview of Operations

<ApplicationName> implements a <N>-step protocol with the following operations:

| Step | Operation | Direction | Transport | BizTalk Entry Point |
|------|-----------|-----------|-----------|---------------------|
| 1 | <OperationName1> | <From> → <To> | <Transport> | <AdapterType> → <PortName> |
| 2 | <OperationName2> | <From> → <To> | <Transport> | <AdapterType> → <PortName> |

<!-- SOURCE: Binding XML → ReceivePort/ReceiveLocation/ReceiveLocationTransportType -->
<!-- SOURCE: ODX files → ReceiveShape, SendShape names and port bindings -->

### 4.2 Step-by-Step Interaction Diagrams

<!-- For each operation, produce an ASCII interaction diagram. -->
<!-- Format: component columns separated by │, arrows show direction and label the method/transport. -->
<!-- Include ALL components — no black boxes. -->

```
════════════════════════════════════════════════════════════
 STEP 1 — <OperationName1>
════════════════════════════════════════════════════════════

  <ClientComponent>        <FrontendComponent>     <BizTalkComponent>     <BackendComponent>
  ═════════════════        ═══════════════════     ══════════════════     ═════════════════
    │                            │                       │                      │
    │  <Protocol> + <Auth>       │                       │                      │
    │  <OperationName1>(...)     │                       │                      │
    ├───────────────────────────►│                       │                      │
    │                            │  <call mechanism>     │                      │
    │                            ├──────────────────────►│                      │
    │                            │                       │  <processing step>   │
    │                            │                       ├─────────────────────►│
    │                            │◄──────────────────────┤                      │
    │◄───────────────────────────┤  Returns: <response>  │                      │
```

<!-- QUALITY GATE: Every component column must appear. No component may be silently collapsed. -->
<!-- QUALITY GATE: Every arrow must carry a transport label. -->
<!-- SOURCE: TDF Frontend source (if available) or ODX receive/send shape names + adapter types from binding XML -->

### 4.3 Clarifications

<!-- Use this sub-section for non-obvious architectural decisions. -->
<!-- Model after architecture-cible.md "Clarification importante" boxes. -->

> **Clarification — <topic>**: <Explanation of why the architecture works this way,
> what could be misunderstood, and what junior developers need to know.>
> **Source**: `<artifact name>` — `<class/method/xpath that proves this>`

---

## 5. Physical Architecture — Layer View

<!-- Model after architecture-cible.md §1.3.3. -->
<!-- ASCII box diagram, one box per layer. Each box shows: component name, tech, key config, inbound/outbound. -->

```
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 1 — <ClientLayer> (<N> sending entities)                 ║
║                                                                  ║
║  Sends: <message type> via <protocol> + <auth mechanism>        ║
║  Target: <FrontendComponent> endpoint                           ║
╚═════════════════════════════════╤════════════════════════════════╝
                                  │ <transport + auth>
                                  ▼
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 2 — <FrontendComponent> (<technology>)                   ║
║  Repo: <repo name>                                              ║
║                                                                  ║
║  <Key implementation detail — e.g., inheritance, protocol impl> ║
║  <Auth: what it validates, how>                                  ║
║  <What it sends to Layer 3>                                     ║
╚═════════════════════════════════╤════════════════════════════════╝
                                  │ <N messages via <transport types>>
                                  ▼
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 3 — <BizTalkIntegrationLayer> (BizTalk Server, <team>)  ║
║  BizTalk application: <AppName> | Orchestration: <OrcName>     ║
║  Repo: <repo name>                                              ║
║                                                                  ║
║  ADAPTERS SUMMARY:                                               ║
║  ┌─────────────────────────────────────────────────────────┐    ║
║  │ INBOUND:  <adapter type> ← <what it receives>          │    ║
║  │ OUTBOUND: <adapter type> → <where it sends>            │    ║
║  └─────────────────────────────────────────────────────────┘    ║
║                                                                  ║
║  ORCHESTRATION: <pattern name (e.g., Parallel Activating Convoy)>║
║  <Key orchestration shapes and their purpose>                   ║
╚═════════════════════════════════╤════════════════════════════════╝
                                  │ <routing mechanism>
                                  ▼
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 4 — <SecondaryBizTalkLayer or DirectBackendLayer>        ║
║  <description>                                                  ║
╚═════════════════════════════════╤════════════════════════════════╝
                                  │ <transport + auth>
                                  ▼
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 5 — <BackendEndpoint> (<technology>)                     ║
║  <Processing steps in order — verified in source code>          ║
║  Step 1: <operation> via <method>                               ║
║  Step 2: <operation>                                            ║
╚═════════════════════════════════╤════════════════════════════════╝
                                  │
                                  ▼
╔══════════════════════════════════════════════════════════════════╗
║  LAYER 6 — <DataStores>                                         ║
║  <DataStore1>: <role, storage type, coupling note>              ║
║  <DataStore2>: <role, storage type, coupling note>              ║
╚══════════════════════════════════════════════════════════════════╝
```

<!-- QUALITY GATE: All 6 layers must be present. Hosting model (IaaS VM / PaaS / on-prem) named per layer. -->

---

## 6. Messaging Patterns

### 6.1 Orchestration Details

<!-- For every orchestration, describe its shape structure, transaction type, and timeout config. -->

#### 6.1.1 <OrchestrationName1>

| Property | Value | Source |
|----------|-------|--------|
| Transaction type | <Atomic / LongRunning / None> | `<odx_file>` → `ScopeShape[name='<...>']/@TransactionType` |
| Pattern | <Convoy / Sequential / Request-Response / Routing> | Inferred from shape structure |
| Timeout | <value> | `<odx_file>` → `ScopeShape[name='<...>']/@Timeout` |
| Correlation sets | <CorrelationSetName> on field <FieldName> | `<odx_file>` → `CorrelationDeclaration` |
| Error handling | <exception handler description> | `<odx_file>` → `ExceptionHandler` shapes |

<!-- QUALITY GATE: Every LongRunning scope must have its estimated real-world duration noted. -->

### 6.2 Correlation Set Analysis

| Correlation Set | Field(s) | Orchestration | Azure Equivalent | Migration Notes |
|----------------|----------|---------------|------------------|-----------------|
| <CorSetName> | <Field(s)> | <OrcName> | Azure Service Bus Session ID | <Any caveats — e.g., session key must be preserved exactly> |

### 6.3 Convoy Classification

<!-- Classify the convoy pattern using the BizTalk Migration Decision Tree (§22). -->

| Pattern | Classification | Evidence | Migration Complexity |
|---------|---------------|----------|---------------------|
| <PatternName> | <Sequential Convoy / Parallel Activating Convoy / Scatter-Gather / etc.> | `<odx>` → <shape evidence> | <LOW / MEDIUM / HIGH> |

---

## 7. Security Architecture

### 7.1 Authentication Chain

<!-- Map the full credential chain from the sending entity to the backend. -->
<!-- Every auth mechanism must be named with its BizTalk artifact evidence. -->

```
<ClientType> ──── <AuthMechanism1> ────► <FrontendComponent>
                                               │
                                         <ValidationStep>
                                               │
<FrontendComponent> ──── <AuthMechanism2> ───► <BizTalkReceivePort>
                                               │
                               (credential transformation: <mechanism>)
                                               │
<BizTalkSendPort> ──── <AuthMechanism3> ──────► <BackendComponent>
```

### 7.2 Security Findings Table

| Finding | Mechanism | Artifact Evidence | Migration Risk | Azure Path |
|---------|-----------|-----------------|----------------|------------|
| <SecurityFinding1> | <e.g., Kerberos token on WCF-Custom port> | `<binding>` → `<xpath>` | <BLOCKER / HIGH / MEDIUM> | <Azure solution> |
| <SecurityFinding2> | <e.g., GAC behavior extension, credential bridge> | `<binding>` → `<xpath>` | <BLOCKER> | <Azure Managed Identity + RBAC> |
| <AccessControl> | <e.g., application-level permission check via AGS> | `<source_file>` → `<method>` | <MEDIUM> | <Azure AD app role> |

<!-- QUALITY GATE: Every BLOCKER finding must have an Azure Path entry. -->
<!-- QUALITY GATE: Every BLOCKER must also appear in §10 and §13. -->

### 7.3 Credential Bridge Analysis

<!-- Only present if a GAC WCF behavior extension performing credential transformation is found. -->

| Extension Name | Assembly Location | Transformation | BLOCKER Reason | Recommended Azure Replacement |
|---------------|------------------|----------------|----------------|-------------------------------|
| <ExtensionName> | GAC — `<dll_name>` | <e.g., Kerberos token → Windows service account> | No Azure PaaS equivalent for GAC WCF behavior injection | Azure Managed Identity + Entra ID workload identity |

---

## 8. Non-Atomic Write Analysis

<!-- Only present if a NON_ATOMIC backend write pattern is detected. -->
<!-- Model after architecture-cible.md §1.3.1 DepoApli / Oracle explanation. -->

### 8.1 Write Operation Pairs

| Operation | Component | Technology | Within Transaction? | Evidence |
|-----------|-----------|------------|---------------------|---------|
| <WriteOp1> | <Component> | <e.g., FileStream.Create> | No — outside Oracle transaction | `<source_file>` → `<method>` |
| <WriteOp2> | <Component> | <e.g., Oracle ODP + explicit transaction> | Yes — Oracle-scoped only | `<source_file>` → `<method>` |

### 8.2 Orphan Risk Analysis

> **What "non-transactionally coupled" means for this application**: `<WriteOp1>` and
> `<WriteOp2>` are **two independent I/O operations executed sequentially** — there is
> **no enclosing lock** covering both. If `<WriteOp2>` fails after `<WriteOp1>` succeeds,
> `<WriteOp1>` result remains permanently — that is the **orphan scenario**.
>
> **Technical detail**: No MSDTC, no XA protocol. `<WriteOp1>` completes (cannot be
> rolled back), then `<WriteOp2>` opens its transaction. Independent failure domains.

| Risk | Probability | Business Impact | Current Mitigation | Migration Recommendation |
|------|------------|-----------------|-------------------|--------------------------|
| Orphan <WriteOp1> result | <LOW / MEDIUM / HIGH> | <e.g., file exists with no DB record> | <e.g., Control-M cleanup job> | <Redesign with compensating transaction or Saga pattern> |

<!-- QUALITY GATE: Severity must be CRITICAL if no automated compensation exists. -->

---

## 9. Retry and Audit Configuration

<!-- Extract ALL retry settings from binding XML. -->
<!-- Note the business purpose of each retry configuration. -->

| Port Name | RetryCount | RetryInterval | Effective Window | Business Purpose | Evidence |
|-----------|-----------|---------------|-----------------|-----------------|---------|
| <PortName> | <count> | <interval (min)> | <count × interval formatted as duration> | <e.g., "audit trail for file delivery", "SLA recovery window"> | `<binding>` → `<xpath>` |

> **Important — distinguish retry semantics**: Not all high retry counts are errors.
> `RetryCount=<value>` on `<PortName>` implements a **<N>-<unit> delivery window** to
> accommodate <business reason>. This is an intentional SLA contract, not a bug.
> The migration must preserve this semantic via Azure Service Bus retry policies +
> DLQ monitoring.

---

## 10. Migration Risk Table

<!-- One row per component or risk category. -->
<!-- Severity: BLOCKER | HIGH | MEDIUM | LOW -->

| Component / Risk | Severity | Reason | Azure Mitigation Path | Evidence |
|-----------------|----------|--------|----------------------|---------|
| <Component1> — <RiskCategory> | BLOCKER | <Why it blocks migration> | <Azure solution> | `<artifact>` → `<value>` |
| <Component2> — <RiskCategory> | HIGH | <Reason> | <Azure approach> | `<artifact>` → `<value>` |
| <Component3> — <RiskCategory> | MEDIUM | <Reason> | <Standard pattern> | `<artifact>` → `<value>` |

**Summary**:
- **BLOCKER**: <count> (<list names>)
- **HIGH**: <count>
- **MEDIUM**: <count>
- **LOW**: <count>

---

## 11. Verified Facts

<!-- Numbered list of facts confirmed by direct artifact inspection (binding XML, ODX shapes, source code). -->
<!-- Format: Fact #N — [CONFIRMED IN SOURCE] <statement>. Source: <artifact> → <exact location>. -->
<!-- Minimum 10 facts. Facts that contradict common assumptions are especially valuable. -->

1. **[CONFIRMED IN SOURCE]** <FactStatement1>  
   *Source*: `<artifact>` → `<class/method/xpath>`

2. **[CONFIRMED IN SOURCE]** <FactStatement2>  
   *Source*: `<artifact>` → `<class/method/xpath>`

3. **[CONFIRMED IN SOURCE]** <FactStatement3> — <clarification of what this means operationally>  
   *Source*: `<artifact>` → `<class/method/xpath>`

<!-- Continue to at least 10 facts. -->
<!-- QUALITY GATE: Every fact must have a source citation. No facts without artifact evidence. -->
<!-- QUALITY GATE: At least one fact must be a counter-intuitive finding (something a developer -->
<!--   might assume incorrectly without reading the source). -->

---

## 12. Hard Constraints

<!-- These are non-negotiable constraints that the migration design MUST respect. -->
<!-- Every constraint must name who owns it, why it cannot change, and what the migration implication is. -->

| Constraint | Owner | Reason Cannot Change | Migration Implication |
|-----------|-------|---------------------|----------------------|
| <ContractOrProtocol1> must be preserved | <External party / Government / N clients> | <e.g., "~N external clients; contract renegotiation not in scope"> | <e.g., "Azure migration must expose the same WSDL/endpoint interface"> |
| <CredentialMechanism> must be supported | <Backend system owner> | <e.g., "Government-owned backend; auth protocol not under our control"> | <e.g., "APIM + KCD or Managed Identity bridge required before migration"> |
| <CorrelationKey> must be preserved | <Protocol design> | <e.g., "Session key embedded in all 3 messages; cannot be changed without client-side changes"> | <e.g., "Azure Service Bus Session ID must use exact same key value"> |
| <RetrySemantics> must be preserved | <Business / SLA> | <e.g., "~N-hour delivery window is a contractual SLA, not a configuration choice"> | <e.g., "Azure retry policy must replicate the same delivery window duration"> |

<!-- QUALITY GATE: This section must be reviewed and signed off by the human reviewer. -->
<!-- QUALITY GATE: Every constraint must have an owner. Ownerless constraints are not actionable. -->

---

## 13. Migration Decision and Readiness

### 13.1 Overall Assessment

| Field | Value |
|-------|-------|
| `migrationReadiness` | <READY / READY_WITH_CONDITIONS / NOT_READY> |
| `overallRisk` | <BLOCKER / HIGH / MEDIUM / LOW> |
| `pilotSuitability` | <SUITABLE / UNSUITABLE / CONDITIONAL> |
| `estimatedMigrationComplexity` | <Sprint / Quarter / HalfYear / Year> |
| `confidenceScore` | <0.0–1.0> |

### 13.2 Blockers (must be resolved before migration starts)

<!-- List every BLOCKER finding with its resolution path. -->
<!-- Each blocker must reference its §7 and §10 evidence. -->

| Blocker | Severity | Resolution Path | Owner | Decision Deadline |
|---------|----------|----------------|-------|-------------------|
| <BlockerName1> | BLOCKER | <Specific Azure solution> | <Team or person> | <Before architecture sign-off / Before sprint N> |

### 13.3 Hard Constraints Summary

```
hardConstraints:
  - "<constraint 1 — one-line summary>"
  - "<constraint 2>"
  - "<constraint 3>"
```

### 13.4 Recommended Migration Path

<!-- Phased plan: what must be done first, what can be done in parallel, what is last. -->

| Phase | What | Dependency | Team |
|-------|------|-----------|------|
| Phase 1 — Blockers | <Resolve security blockers (Kerberos, credential bridge)> | None (prerequisite) | SecurityEngineer + SolutionArchitect |
| Phase 2 — Core migration | <Migrate BizTalk orchestrations to Azure Service Bus + Durable Functions> | Phase 1 complete | BackendEngineer + DataEngineer |
| Phase 3 — Non-atomic risk | <Address orphan write risk if business requires it> | Phase 2 validation | BackendEngineer |
| Phase 4 — Cutover | <Traffic cut from BizTalk to Azure; dual-run validation> | Phase 2 + 3 | PlatformEngineer + QualityEngineer |

---

## 14. Open Questions

<!-- Every item that requires human decision or external verification before migration can proceed. -->

| # | Question | Owner | Why Blocking | Decision Deadline |
|---|---------|-------|-------------|-------------------|
| 1 | <Question requiring human judgment — e.g., "Does the business accept the orphan file risk, or is atomicity redesign required?"> | <Owner> | <Why this question blocks a migration decision> | <Before Phase 2 start> |
| 2 | <Question about external system — e.g., "Has the backend team confirmed Managed Identity support?"> | <Owner> | <Why critical> | <Before Phase 1 start> |

<!-- QUALITY GATE: Must have at least one open question per BLOCKER finding. -->
<!-- QUALITY GATE: Every open question must have an owner and a deadline. -->

---

*This document was generated by AgenticAppMigration v<ToolVersion> on <AssessmentDate>.*
*Evidence-based findings are marked [CONFIRMED IN SOURCE]. Inferred findings are marked [INFERRED — verify manually].*
*Confidence score: <0.0–1.0>. Findings with confidence < 0.7 require human review before use in migration planning.*
```

---

## 29. Quality Gate Checklist (Pre-Delivery Verification)

The Consolidation Agent must execute this checklist before marking the output document as ready for customer delivery. This checklist is a direct application of the CADO `self-review-gate` skill.

```
QUALITY GATE RESULT: PASS | PARTIAL | FAIL

SECTION COMPLETENESS:
  - §1 Project Context:              PASS | FAIL — [note]
  - §2 Component Inventory:          PASS | FAIL — [note]
  - §3 Constraint Analysis:          PASS | FAIL — [note]
  - §4 End-to-End Flow:              PASS | FAIL — [note]
  - §5 Physical Architecture:        PASS | FAIL — [note]
  - §6 Messaging Patterns:           PASS | FAIL — [note]
  - §7 Security Architecture:        PASS | FAIL — [note]
  - §8 Non-Atomic Write Analysis:    PASS | FAIL | N/A — [note]
  - §9 Retry Configuration:          PASS | FAIL — [note]
  - §10 Risk Table:                  PASS | FAIL — [note]
  - §11 Verified Facts:              PASS | FAIL — [count: N facts, N with source]
  - §12 Hard Constraints:            PASS | FAIL — [note]
  - §13 Migration Decision:          PASS | FAIL — [note]
  - §14 Open Questions:              PASS | FAIL — [note]

EVIDENCE DENSITY:
  - Findings with evidence field:    N / M (target: ≥ 85%)
  - Result:                          PASS | FAIL

BLOCKER CROSS-REFERENCE:
  - Every BLOCKER in §7:             PASS | FAIL
  - Every BLOCKER in §10:            PASS | FAIL
  - Every BLOCKER in §13:            PASS | FAIL

CONFIDENCE SCORE:                    <0.0–1.0> (PASS if ≥ 0.7)

CONFLICTS:
  - [none | itemized list of conflicts requiring human resolution]

HUMAN REVIEW REQUIRED FOR:
  - §12 Hard Constraints:            [reviewer name or UNASSIGNED]
  - §13 Migration Decision:          [reviewer name or UNASSIGNED]
  - Any BLOCKER finding:             [reviewer name or UNASSIGNED]

OVERALL: PASS → ready for customer delivery
         PARTIAL → deliverable with listed caveats noted in document
         FAIL → must correct before delivery
```

**Delivery is BLOCKED** if any of the following are true:
- Any §1–§7 or §9–§14 section is absent (FAIL, not PARTIAL)
- Evidence density < 70%
- Any BLOCKER finding has no Azure Path in §10
- §13 has no `migrationReadiness` value
- Confidence score < 0.5

---

# Part V — CADO Framework Integration

> **Purpose**: `AgenticAppMigration` is not built from scratch — it is built **on top of the CADO Framework** already present in this repository. This part documents how the 10-agent assessment pipeline maps to CADO's stages, which CADO skills each agent uses, and how we improve on CADO's baseline to deliver production-quality migration assessments.

---

## 30. CADO Stage Mapping

The CADO framework defines a staged delivery pipeline: **Intake → Plan → Gate → Build → Prove → Ship**. The `AgenticAppMigration` 5-phase execution maps to these stages as follows:

| CADO Stage | AgenticAppMigration Equivalent | Responsible Party | Gate Criteria |
|-----------|---------------------------------------|-------------------|---------------|
| **Intake** | User provides BizTalk artifact folder path + application name | Human operator | Artifacts reachable and at least one binding XML present |
| **Plan** | Phase 0 — Artifact discovery; Orchestrator generates execution plan for 10 agents | Orchestrator (Part I §3–§8) | `applicationProfile.name` set; all artifact paths resolved |
| **Gate** | Phase 1 skills complete — `ParseBizTalkBinding`, `ListODXShapes`, etc. | Orchestrator self-check | All Phase 1 skill outputs have `status: "COMPLETE"` |
| **Build** | Phases 2–4 — 10 agents execute in dependency order and produce structured JSON outputs | All 10 specialist agents | Each agent output passes its own self-review gate |
| **Prove** | Quality Gate Checklist from §29 — Consolidation Agent self-review | Consolidation Agent | §29 checklist score = PASS or PARTIAL |
| **Ship** | Output document rendered from §28 template; delivered for customer approval | Consolidation Agent + Human | Human reviewer signs off on §12 and §13 |

### 30.1 Maximus as the Orchestrator

The **Maximus** agent (`.github/agents/Maximus.md`) defines the CADO orchestration model. `AgenticAppMigration`'s Orchestrator (§3) is built with Maximus as its behavioral blueprint:

- **Maximus rule**: *"Nothing moves from one stage to the next without an explicit progression decision."*  
  → Orchestrator rule: Phase N agents do not start until Phase N-1 skill outputs are verified complete.

- **Maximus rule**: *"You assign risk tiers using the policy in `.cado/workflow/risk-policy.md`."*  
  → Orchestrator rule: The Risk Agent (§17.9) assigns BLOCKER / HIGH / MEDIUM / LOW using the BizTalk Migration Decision Tree (§22).

- **Maximus rule**: *"You enforce evidence requirements before closing any stage."*  
  → Orchestrator rule: §29 quality gate checklist enforces evidence density ≥ 85% before the Ship stage.

- **Maximus rule**: *"Open parallel-safe specialist lanes whenever dependency constraints allow."*  
  → Orchestrator rule: Phase 2 agents (Inventory, Dependency, External Systems) run in parallel since they all consume Phase 1 skill outputs independently.

---

## 31. CADO Skill Assignments per Agent

Each of the 10 specialist agents integrates specific CADO skills from `.github/skills/`. The table below maps every agent to the skills it must use before completing its work.

| Agent | CADO Skills Used | When Applied |
|-------|-----------------|--------------|
| **17.1 Inventory Agent** | `specification-contract-authoring` | To format the component inventory as a machine-readable contract |
| **17.2 Dependency Agent** | `api-change-impact` | To classify each dependency's change-impact risk |
| **17.3 External Systems Agent** | `specification-contract-authoring`, `api-contracts` | To produce locked vs. mutable contract classification |
| **17.4 Adapter and Pipeline Agent** | `security-review-checklist` | To flag credential bridge patterns against OWASP A07 (auth failures) |
| **17.5 Messaging Patterns Agent** | `solution-design-doc` §5 (Data flow and contracts) | To document correlation set → Azure SB session contract |
| **17.6 Long-Running Process Agent** | `observability-design` | To define metrics, traces, and DLQ alert rules for retry-heavy ports |
| **17.7 Security Architecture Agent** | `threat-model`, `security-planning`, `security-review-checklist` | Full STRIDE pass on auth chain; Kerberos + credential bridge threat surface |
| **17.8 Anti-Patterns Agent** | `qa-adversarial-verification` | To stress-test orphan-write scenarios and implicit routing failure modes |
| **17.9 Risk Agent** | `change-risk-classifier`, `approval-packet` | To classify overall risk tier and prepare a BLOCKER escalation packet |
| **17.10 Consolidation Agent** | `self-review-gate`, `delivery-validation`, `implementation-ready-checklist` | To run §29 checklist, validate traceability, and gate the output document |

### 31.1 Approval Packet for BLOCKER Escalation

When the Risk Agent (§17.9) finds `overallRisk: "BLOCKER"`, it must produce a CADO-format approval packet before the Ship stage proceeds:

```
APPROVAL PACKET

Decision:
- Proceed with migration planning despite N confirmed BLOCKER(s)?

Why now:
- Migration timeline requires architecture commitment within [deadline].
  BLOCKERs have known resolution paths but require human approval to prioritize.

Options:
- Option A: Proceed with conditions
  Impact: Migration starts; BLOCKER resolution is a Phase 1 prerequisite gate.
  Risk: If BLOCKER resolution fails, migration scope must be reduced.
- Option B: Pause migration
  Impact: Timeline delayed until BLOCKER resolution is confirmed feasible.
  Risk: BizTalk decommission date is at risk.

Recommendation:
- Option A — provided the named Azure resolution path is pre-approved.

Reasoning:
- All identified BLOCKERs have established Azure PaaS resolution patterns.
- Both require security architecture work (APIM + KCD; Managed Identity) that
  can be designed in parallel with BizTalk migration analysis.
- Delaying the entire assessment over solvable security design items is not
  warranted given the available precedents.

Rollback / exit path:
- If BLOCKER resolution proves infeasible in Phase 1, revert to Option B.
  Assessment output document remains valid as a discovery artifact.

Decision deadline:
- Before Phase 2 (core migration) starts
```

---

## 32. Evidence Contract (CADO-Aligned)

This section formalizes the evidence contract that all 10 agents must satisfy, modeled after CADO's `delivery-validation` and `specification-contract-authoring` skills.

### 32.1 Per-Agent Evidence Contract

Every agent output submitted to the Consolidation Agent must conform to this structure:

```json
{
  "agentId": "<17.N — AgentName>",
  "status": "COMPLETE | INCOMPLETE | BLOCKED",
  "confidenceContribution": 0.0,
  "findings": [
    {
      "findingId": "<agentId>-<N>",
      "category": "<category from §19 risk table>",
      "severity": "BLOCKER | HIGH | MEDIUM | LOW | INFO",
      "description": "<precise, factual statement — no vague language>",
      "evidence": "<artifact> → <xpath_or_location> → <exact_value>",
      "evidenceType": "CONFIRMED_IN_SOURCE | CONFIRMED_IN_BINDING | INFERRED | NOT_FOUND",
      "azurePath": "<Azure equivalent or mitigation — required if severity=BLOCKER>",
      "selfReviewPassed": true
    }
  ],
  "gaps": ["<list of items this agent could not assess — with reason>"],
  "conflicts": ["<list of findings that contradict another agent's output>"]
}
```

**Evidence type rules**:
- `CONFIRMED_IN_SOURCE`: Finding was read from actual source code (`.vb`, `.cs`, `.odx`).
- `CONFIRMED_IN_BINDING`: Finding was read from binding XML or BTP pipeline file.
- `INFERRED`: Finding was inferred from naming convention, file structure, or partial evidence. Must be flagged for human review.
- `NOT_FOUND`: The agent looked for this artifact/pattern and could not find it. Absence is a valid finding.

### 32.2 Delivery Validation at Ship Stage

The Consolidation Agent runs the CADO `delivery-validation` check before rendering the output document:

```
DELIVERY VALIDATION: PASS | PARTIAL | FAIL

TRACEABILITY:
  - artifacts → agent findings: PASS | FAIL — [N findings, N with evidence]
  - agent findings → risk table: PASS | FAIL — [N risks mapped]
  - risk table → migration decision: PASS | FAIL — [all BLOCKERs reflected in §13]
  - migration decision → open questions: PASS | FAIL — [N BLOCKERs have open questions]

DRIFT:
  - [none | any finding in §13 not traceable to a Phase 2-4 agent output]

ACTION REQUIRED:
  - [none | exact correction needed before document delivery]
```

---

## 33. How AgenticAppMigration Improves on CADO

CADO is a general-purpose agentic delivery framework. `AgenticAppMigration` is a **domain-specialized instantiation** of CADO's patterns. This section documents where we diverge from CADO defaults and why the divergence produces better outcomes for BizTalk migration assessments.

| CADO Default | AgenticAppMigration Enhancement | Rationale |
|-------------|----------------------------------------|-----------|
| Generic specialist agents (BackendEngineer, SecurityEngineer, etc.) | 10 BizTalk-specific agents with domain-encoded prompt rules (§17) | Generic agents cannot parse ODX shapes, binding XML XPaths, or classify BizTalk convoy patterns without expert prompting |
| Skills used on demand | Skills pre-assigned per agent (§31 table) | Eliminates the routing decision overhead; every agent knows exactly which skill to apply |
| Evidence contract defined per run | Canonical evidence contract with `evidenceType` enum (§32.1) | Enforces `CONFIRMED_IN_SOURCE` vs. `INFERRED` discipline that generic delivery validation does not require |
| Approval packet triggered manually | Risk Agent automatically produces approval packet when `overallRisk: "BLOCKER"` | BizTalk BLOCKER patterns (Kerberos, DTC, GAC credential bridge) are well-known — automation is safe |
| Output format determined per project | Mandatory 14-section output document template (§28) | Customer quality gate requires a consistent format comparable to a human architecture review |
| Self-review gate applied at specialist level | §29 quality gate applied at Consolidation Agent level with section-by-section checklist | A missed section (e.g., §8 Non-Atomic Write Analysis) is a quality failure, not just a coverage gap |
| Delivery validation checks spec→code traceability | Delivery validation checks artifact→finding→risk→decision traceability | The relevant traceability chain for an assessment tool is from raw evidence to customer decision, not from spec to code |
| CADO stages: Intake → Plan → Gate → Build → Prove → Ship | Same stages but Gate = Phase 1 skill completion (artifact parsing), not plan approval | The gate in an assessment tool is "do we have parseable artifacts?" not "is the plan approved?" |

### 33.1 What CADO Contributes That We Inherit Unchanged

- **Maximus orchestration model**: Stage sequencing, parallel lane opening, risk tier assignment, gate enforcement (§30)
- **Approval packet format**: Exact CADO format for BLOCKER escalation (§31.1)
- **Self-review gate mechanics**: Run verification → collect evidence → one correction attempt → stop or report (§29)
- **Delivery validation output format**: Traceability check with PASS / PARTIAL / FAIL verdict (§32.2)
- **Specialist agent persona structure**: Each of the 10 agents follows the CADO specialist agent pattern (responsibility, inputs, outputs, skills, prompt rules) from Maximus's routing model

---

# Part VI — System Entry Points, Input Modes, and Human-in-the-Loop

> **Purpose**: This part defines two foundational system-level behaviors that the previous parts assume but do not fully specify:
>
> 1. **How the system is started** — `AgenticAppMigration` can be launched with a rich assessment specification, or with nothing more than a BizTalk artifact folder. Both modes produce the same output document. The difference is depth, precision, and how much the user controls the scope.
>
> 2. **How the system handles ambiguity** — Every assessment will encounter moments where the evidence from artifacts alone is insufficient to make a reliable finding. When that happens, the agentic pipeline must not guess. It must pause, ask a precise question, and resume only after a human provides the missing clarity. This is not a fallback mechanism — it is an architectural guarantee that every BLOCKER-level finding rests on confirmed, not inferred, evidence.

---

## 34. Assessment Input Specification and Input Modes

### 34.1 The Two Input Modes

`AgenticAppMigration` accepts two input modes. Both are first-class; neither is a workaround.

| Mode | What the User Provides | When to Use |
|------|----------------------|-------------|
| **Mode A — Guided** | BizTalk artifact repo path **+** `AssessmentSpec` | You know what concerns to prioritize, have external docs, or are running a pilot assessment |
| **Mode B — Discovery** | BizTalk artifact repo path only | You want a complete, unguided assessment of a BizTalk application with no prior context |

In **Mode B**, the Orchestrator applies all default behaviors, runs all 10 agents, and produces the same 14-section output document as Mode A. The user does not need to provide anything other than the repo path. The BizTalk-specialized skills do all the discovery.

> **Key principle**: The assessment quality floor in Mode B is guaranteed by the built-in BizTalk domain skills (§18) and the 17-feature migration risk table (§19). These are not optional — they run in every assessment regardless of what the user provides.

---

### 34.2 AssessmentSpec Schema (Mode A)

When the user provides a specification, it must conform to the following JSON schema. All fields except `artifactRepo` are optional — unset fields adopt their default values.

```json
{
  "assessmentSpec": {
    "artifactRepo": {
      "type": "string",
      "description": "Absolute or relative path to the folder containing BizTalk artifacts (binding XML, ODX, BTP, BTM, DLL, config files). Required in all modes.",
      "required": true
    },
    "applicationName": {
      "type": "string | null",
      "description": "Friendly name for the application under assessment. If null, the Orchestrator derives it from the BizTalk binding file ApplicationName element.",
      "default": null
    },
    "scope": {
      "type": "object",
      "description": "Controls which agents run. Default: all 10 agents.",
      "properties": {
        "mode": {
          "type": "enum",
          "values": ["full", "focused", "security-only", "risk-only"],
          "default": "full"
        },
        "agentOverrides": {
          "type": "array of agentIds",
          "description": "When mode=focused, only agents in this list run. Others produce SKIPPED_BY_SPEC outputs.",
          "default": []
        }
      }
    },
    "knownConcerns": {
      "type": "array of string",
      "description": "Known risk areas the user wants prioritized. Agents receive these as additional context hints. Examples: ['Kerberos auth', 'non-atomic writes', 'long-running orchestrations', 'GAC credential bridge']. Does not restrict what other agents find.",
      "default": []
    },
    "externalDocumentation": {
      "type": "array of path",
      "description": "Paths to architecture documents, WSDLs, or previous assessment reports. External Systems Agent uses these to upgrade INFERRED findings to CONFIRMED. Optional but highly recommended for pilot assessments.",
      "default": []
    },
    "knownConstraints": {
      "type": "array of object",
      "description": "Locked contracts, out-of-scope components, or known external system owners that the user pre-declares. Pre-populates the Dependency Agent and External Systems Agent locked contract lists.",
      "items": {
        "componentName": "string",
        "constraintType": "PROTOCOL_LOCKED | OUT_OF_SCOPE | EXTERNAL_OWNER",
        "owner": "string",
        "reason": "string"
      },
      "default": []
    },
    "pilotCandidate": {
      "type": "boolean",
      "description": "Set to true if this application is being assessed for selection as the pilot migration candidate. Activates the §26 pilot suitability validation criteria.",
      "default": false
    },
    "qualityGateLevel": {
      "type": "enum",
      "values": ["standard", "pilot", "full"],
      "description": "Controls the strictness of the §29 quality gate. 'standard': 85% evidence density required. 'pilot': 90% evidence density, all 11 HOA5 validation dimensions must pass. 'full': same as pilot + human sign-off on all BLOCKER findings, not just §12 and §13.",
      "default": "standard"
    },
    "humanReviewer": {
      "type": "string | null",
      "description": "Name or alias of the designated human reviewer for §12 and §13 sign-off. If null, HITL requests are unrouted and block delivery.",
      "default": null
    }
  }
}
```

---

### 34.3 Orchestrator Behavior in Mode B (Repo-Only)

When no `AssessmentSpec` is provided, the Orchestrator behaves as if the following spec was submitted:

```json
{
  "assessmentSpec": {
    "artifactRepo": "<user-provided path>",
    "applicationName": null,
    "scope": { "mode": "full", "agentOverrides": [] },
    "knownConcerns": [],
    "externalDocumentation": [],
    "knownConstraints": [],
    "pilotCandidate": false,
    "qualityGateLevel": "standard",
    "humanReviewer": null
  }
}
```

In this mode:
- `applicationName` is derived from `//BindingInfo/@Name` in the binding XML.
- All 10 agents run.
- No concern areas are pre-prioritized; agents discover everything from artifacts.
- The quality gate uses the standard 85% evidence density threshold.
- HITL requests (§35) are still generated for critical gaps — they are simply unrouted until a reviewer is assigned.

> **Important**: Mode B is not a lightweight mode. It runs the full 10-agent pipeline. The only difference from Mode A is that the user provides zero upfront context. This is intentional: the system must be able to assess a BizTalk application that no one in the room has ever seen.

---

### 34.4 Always-On BizTalk Specialized Skills

Regardless of input mode, `scope.mode`, or any user override, the following skills and behaviors are permanently active and cannot be disabled:

| Always-On Capability | Why It Cannot Be Disabled |
|---------------------|--------------------------|
| `ParseBizTalkBinding` (§18.1) | Without binding XML parsing, no ports, adapters, or filters are known — the entire assessment is blind |
| `ListODXShapes` (§18.2) | Without ODX parsing, no orchestration patterns (convoy, correlation, long-running scope) can be detected |
| `ExtractAuthConfig` (§18.3) | Without auth extraction, Kerberos and credential bridge BLOCKERs are silently missed |
| `ListGACAssemblies` (§18.4) | Without GAC assembly detection, custom behavior extensions are invisible |
| All 17 BizTalk feature detectors (§19) | Each detector targets a distinct BLOCKER or HIGH risk that has no substitute detection mechanism |
| Evidence citation requirement (§21.1) | Disabling evidence citation makes findings unverifiable — the entire quality contract collapses |
| BLOCKER cross-reference to §10 and §13 | Disabling this allows BLOCKERs to exist in agent outputs without reaching the migration decision |

> **Design principle**: These capabilities constitute the "BizTalk DNA" of the tool. A BizTalk migration assessment tool that does not scan for Kerberos auth, GAC credential bridges, DTC-coupled atomic scopes, and implicit routing orchestrations is not a BizTalk migration assessment tool — it is a generic document analyzer. The always-on list defines the minimum that makes this tool domain-specific.

---

### 34.5 Input Validation (Phase 0 — Before Any Agent Runs)

Before the Orchestrator dispatches a single agent, it performs the following Phase 0 checks. If any **BLOCKING** check fails, the assessment does not start.

| Check | Blocking? | Action on Failure |
|-------|-----------|-------------------|
| `artifactRepo` path exists and is readable | YES | STOP — emit `ASSESSMENT_CANNOT_START: artifact repo not found` |
| At least one `*.xml` binding file found in repo | YES | STOP — emit `ASSESSMENT_CANNOT_START: no binding XML found` |
| At least one binding XML is parseable (not corrupt XML) | YES | STOP — emit `ASSESSMENT_CANNOT_START: binding XML parse error at <path>:<line>` |
| `applicationName` derivable from binding XML or from spec | NO | WARN — use repo folder name as fallback; note in output document |
| At least one `.odx` file found (orchestration check) | NO | WARN — emit `NO_ORCHESTRATIONS_FOUND`; agents proceed with `status: "PARTIAL"` |
| `externalDocumentation` paths are all readable (Mode A only) | NO | WARN — unreadable docs skipped; findings from those docs remain `INFERRED` |
| `humanReviewer` assigned (for `qualityGateLevel: "pilot"` or `"full"`) | YES (pilot/full only) | STOP — emit `ASSESSMENT_CANNOT_START: humanReviewer required for this qualityGateLevel` |

---

## 35. Human-in-the-Loop (HITL) Protocol

### 35.1 Core Principle

> **The agentic pipeline must never invent evidence.** When the artifacts are insufficient to make a reliable finding, the correct behavior is to stop, ask a precise question to a human, and resume with the answer. A delayed but correct finding is always better than an immediate but inferred one.

This is especially critical for BLOCKER-level findings. An inferred BLOCKER is not acceptable — it either wastes migration budget (false positive) or permits a failed migration (false negative). Every BLOCKER finding must rest on `CONFIRMED_IN_SOURCE` or `CONFIRMED_IN_BINDING` evidence, or on a `HUMAN_CONFIRMED` answer to a specific HITL request.

---

### 35.2 HITL Trigger Conditions

The Orchestrator monitors every agent output for the following trigger conditions. When any is detected, a HITL request is generated before the pipeline advances to the next phase.

| Trigger | Level | When It Fires | Pipeline Behavior |
|---------|-------|--------------|-------------------|
| **T1 — BLOCKER with INFERRED evidence** | CRITICAL | Any agent reports `migrationRisk: "BLOCKER"` with `evidenceType: "INFERRED"` | Pipeline halts at current phase boundary. Cannot advance until human confirms or refutes the finding. |
| **T2 — Confidence below threshold on critical finding** | CRITICAL | Any BLOCKER or HIGH finding has `confidence < 0.6` | Pipeline halts. HITL request generated with the specific artifact path and what could not be read. |
| **T3 — Agent conflict on same artifact** | HIGH | Two agents produce contradictory findings about the same artifact element | Pipeline halts at Phase 4 (Synthesis) boundary. Human must choose which finding is authoritative. |
| **T4 — GAC assembly with no source** | HIGH | `ListGACAssemblies` returns `sourceFound: false` for any assembly | Pipeline continues for other agents but emits a HITL request. The affected agent's findings are marked `[BLOCKED_PENDING_SOURCE]`. |
| **T5 — Business decision required** | MEDIUM | Any `NON_ATOMIC` backend write is found (§15.4) | Pipeline continues. HITL request generated for the business acceptance decision. Human answer is required before §13 Migration Decision can be finalized. |
| **T6 — Protocol locked, owner unknown** | MEDIUM | `protocolLocked: true` but no owner can be determined from artifacts | Pipeline continues. HITL request generated to identify the owner. §12 Hard Constraints cannot be signed off without this. |
| **T7 — Artifact unreadable** | MEDIUM | Any `.odx`, `.btp`, or `.btm` file exists in the repo but fails to parse | Pipeline continues. HITL request generated asking the user to provide a corrected artifact or confirm the file is not applicable. |
| **T8 — BLOCKER with no known Azure resolution path** | CRITICAL | Risk Agent finds a BLOCKER whose pattern is not in §19 or §22 | Pipeline halts. Escalation packet generated (§31.1). Requires architect sign-off before any phase can proceed. |

> **Multiple concurrent triggers**: If multiple HITL triggers fire at the same time, they are batched into a single HITL request with a numbered question list. The pipeline waits for answers to **all critical-level questions** (T1, T2, T8) before resuming. Medium-level questions (T5, T6, T7) can be answered asynchronously — the pipeline continues but marks affected sections as `[PENDING_HUMAN_CLARITY]`.

---

### 35.3 HITL Pause State

When the pipeline halts for HITL, the Orchestrator serializes the full assessment state to a **HITL Snapshot**:

```json
{
  "hitlSnapshotId": "<uuid>",
  "assessmentId": "<uuid>",
  "pausedAtPhase": "Phase1 | Phase2 | Phase3 | Phase4 | Phase5",
  "pausedAtAgent": "<17.N — AgentName | Orchestrator>",
  "pipelineStatus": "WAITING_FOR_HUMAN",
  "completedAgentOutputs": ["<list of agentId outputs already produced>"],
  "partialAgentOutputs": ["<list of agentId outputs that were in progress>"],
  "hitlRequests": ["<list of ClarityRequest objects (§35.4)>"],
  "resumeCondition": "ALL_CRITICAL_ANSWERED | ANY_ANSWERED",
  "snapshotTimestamp": "<ISO datetime>",
  "expiresAt": "<ISO datetime or null>"
}
```

No agents run while the pipeline is in `WAITING_FOR_HUMAN` status. The state is fully reproducible — if the human's answers are injected tomorrow or next week, the pipeline resumes from the exact point it paused.

---

### 35.4 Clarity Request Format

Every HITL trigger generates one or more **Clarity Requests** — structured, precise questions sent to the human reviewer. Clarity Requests must be answerable in under 5 minutes by someone familiar with the application.

```json
{
  "clarityRequestId": "<uuid>",
  "assessmentId": "<uuid>",
  "triggerType": "T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8",
  "level": "CRITICAL | HIGH | MEDIUM",
  "generatedBy": "<17.N — AgentName>",
  "question": "<One precise, answerable question. Must name the specific artifact, component, or decision. No general questions.>",
  "context": {
    "whatWasFound": "<What the agent found in the artifacts>",
    "whatIsMissing": "<What specific evidence is absent>",
    "artifactsSearched": ["<artifact name and path>"],
    "xpathOrLocationAttempted": "<exact XPath or line location that returned null or ambiguous>"
  },
  "impactIfUnanswered": "<What finding remains INFERRED or BLOCKED if this is not answered. Name the specific §section affected.>",
  "suggestedAnswers": ["<optional — candidate answers if the agent can narrow the possibilities>"],
  "humanResponseRequired": {
    "format": "FREE_TEXT | CONFIRM_YES_NO | SELECT_ONE | PROVIDE_PATH",
    "validOptions": ["<only for SELECT_ONE>"]
  }
}
```

**Examples of correctly-formed Clarity Requests:**

```
T1 — BLOCKER with INFERRED evidence:
  question: "The binding file references a WCF behavior extension named
             'TranfBasic2IntegBehaviorExtn' on the WCF-Custom Send Port.
             This is classified as a credential bridge (BLOCKER) based on the
             name pattern. Can you confirm: (a) this assembly is in the GAC,
             (b) it performs Kerberos token transformation, or (c) provide the
             source code location so we can confirm from code?"
  suggestedAnswers: ["Yes, confirmed — it transforms Kerberos tokens (BLOCKER confirmed)",
                     "No — it only does logging (reclassify as LOW)"]

T4 — GAC assembly with no source:
  question: "Assembly 'TranfBasic2IntegBehaviorExtn.dll' is referenced by the
             binding file as a GAC-deployed WCF behavior extension, but no
             source code was found in the artifact repo. Is the source code
             available in another location? If yes, provide the path or repo URL.
             If no, confirm we should proceed with decompilation using ILSpy."
  humanResponseRequired: { "format": "PROVIDE_PATH" }

T5 — Business decision on non-atomic write:
  question: "The HOA5 backend performs two independent write operations:
             (1) creates a file on disk, then (2) inserts a record into Oracle
             in a separate transaction. If step 2 fails after step 1 succeeds,
             the file is orphaned with no DB record. Does the business accept
             this orphan-file risk as-is, or is atomicity redesign required?"
  humanResponseRequired: { "format": "SELECT_ONE",
    "validOptions": ["Accept risk (current Control-M cleanup is sufficient)",
                     "Redesign for atomicity required (Phase 3 scope)"] }
```

---

### 35.5 Human Response Injection

When the human provides answers, they are injected as **Human Confirmation** artifacts back into the pipeline.

```json
{
  "humanConfirmationId": "<uuid>",
  "clarityRequestId": "<uuid>",
  "answeredBy": "<human reviewer name>",
  "answeredAt": "<ISO datetime>",
  "answer": "<freetext or selected option>",
  "confidence": "HIGH | MEDIUM | LOW",
  "sourceReference": "<optional: document, section, URL, or artifact the human cites>",
  "overridesAgentFinding": "<agentId>-<findingId> | null"
}
```

**How human answers change agent findings:**

| Answer Type | Evidence Type After Injection | Confidence Impact |
|------------|------------------------------|-------------------|
| Human confirms BLOCKER (T1) | `HUMAN_CONFIRMED` — treated as `CONFIRMED_IN_SOURCE` | +0.10 per §21.2 |
| Human refutes BLOCKER (T1) | `HUMAN_CONFIRMED` — finding severity downgraded to level specified by human | No confidence penalty |
| Human provides source path (T4) | Agent re-runs on provided path; `INFERRED` upgraded to `CONFIRMED_IN_SOURCE` if file found | Removes -0.10 penalty |
| Human accepts business risk (T5) | `HUMAN_CONFIRMED` — finding documented as accepted risk, not open question | No confidence change |
| Human requests redesign (T5) | `HUMAN_CONFIRMED` — finding moves to Phase 3 scope; §14 open question closed | No confidence change |
| Human provides owner name (T6) | `HUMAN_CONFIRMED` — §12 constraint entry owner field populated | No confidence change |
| Human provides corrected artifact (T7) | Phase 1 re-runs on corrected file; normal confidence scoring resumes | Previous penalty removed |

---

### 35.6 Pipeline Resume Protocol

After all required HITL answers are received, the Orchestrator resumes from the exact snapshot point (§35.3):

```
Resume sequence:
  1. Validate all CRITICAL-level Clarity Requests have responses
  2. Inject Human Confirmation artifacts into the relevant agent contexts
  3. Re-run any agents whose outputs were marked [BLOCKED_PENDING_SOURCE] or [PENDING_HUMAN_CLARITY]
  4. Re-run the Consolidation Agent (§17.10) with updated inputs
  5. Re-run the §29 Quality Gate Checklist
  6. If Quality Gate = PASS or PARTIAL → proceed to Ship stage
  7. If Quality Gate = FAIL → generate new HITL request for remaining gaps (up to 2 retry passes)
  8. After 2 failed retry passes → escalate to HITL Level 3 (§35.7) and halt
```

> **MEDIUM-level answers (T5, T6, T7)** are processed on the next Consolidation Agent run even if the pipeline did not halt for them. They do not trigger a full resume — they are incorporated at Phase 5 (Consolidation).

---

### 35.7 HITL Escalation Levels

Three escalation levels govern the interaction between the agentic pipeline and humans.

```
LEVEL 1 — Automatic pause, single agent re-run
  Triggers: T4 (missing source), T7 (unreadable artifact), T6 (unknown owner)
  Pipeline state: CONTINUES for other agents; affected agent marked BLOCKED
  Human interaction: Asynchronous; answers accepted at next Consolidation pass
  Maximum wait: Unlimited (does not block delivery if §29 gate passes without it)

LEVEL 2 — Full pipeline halt, phase boundary pause
  Triggers: T1 (BLOCKER with INFERRED evidence), T2 (low confidence on BLOCKER),
            T3 (agent conflict on same artifact)
  Pipeline state: WAITING_FOR_HUMAN — no agents run
  Human interaction: Synchronous; delivery is BLOCKED until all CRITICAL answers provided
  Maximum wait: Configurable per AssessmentSpec; default = no deadline (block forever)
  Escalation: If no answer in 5 business days → auto-escalate to Level 3

LEVEL 3 — Full stop, architect sign-off required
  Triggers: T8 (BLOCKER with no known Azure resolution), Level 2 with no response in 5 days
  Pipeline state: STOPPED — assessment output is partial; partial output document rendered
                  with all findings to date + explicit ASSESSMENT_INCOMPLETE header
  Human interaction: Formal architecture review required; Approval Packet generated (§31.1)
  Maximum wait: Until architect provides either a resolution path or a GO/NO-GO decision
  Output: Partial assessment document delivered with ASSESSMENT_INCOMPLETE status and
          full evidence of what was found, what was blocked, and what requires human input
```

---

### 35.8 Output Document — HITL Transparency

Every finding in the final output document that was shaped by a human response must be explicitly marked. The customer must be able to distinguish agentic findings from human-contributed findings.

**Evidence type labels in the output document:**

| Label | Meaning |
|-------|---------|
| `[CONFIRMED IN SOURCE]` | Agent read this directly from a BizTalk artifact. No human input. |
| `[CONFIRMED IN BINDING]` | Agent read this from binding XML or pipeline config. No human input. |
| `[HUMAN CONFIRMED — <reviewerName>]` | Agent had INFERRED evidence; human confirmed or provided the fact. Date and reviewer cited. |
| `[INFERRED — verify manually]` | Agent could not confirm from artifacts; no human response received. Treat as hypothesis. |
| `[BLOCKED — source not found]` | Agent could not assess; source artifact absent. Human response pending. |
| `[ACCEPTED RISK — <reviewerName>]` | Business risk finding explicitly accepted by named human reviewer. Not open for migration redesign. |

**§11 Verified Facts** (the numbered evidence list) must include the evidence label for every fact. A fact labeled `[INFERRED — verify manually]` cannot count toward the minimum 10 confirmed facts.

**§29 Quality Gate Checklist** gains an additional check when HITL triggers fired:

```
HITL SUMMARY:
  - CRITICAL requests generated:    N
  - CRITICAL requests answered:     N (required: N = N for PASS)
  - MEDIUM requests generated:      N
  - MEDIUM requests answered:       N (delivery not blocked by open MEDIUM requests)
  - Findings upgraded by HITL:      N (INFERRED → HUMAN_CONFIRMED)
  - Findings still BLOCKED:         N (appear in §14 Open Questions)

HITL RESULT: CLEAN | PENDING_MEDIUM | BLOCKED_CRITICAL
```

---

### 35.9 HITL Integration with CADO Approval Packet

Level 3 HITL escalation (§35.7, trigger T8) generates a CADO-format Approval Packet (§31.1) with the following additions specific to the HITL context:

```
APPROVAL PACKET — ASSESSMENT BLOCKED

Decision:
- Authorize the architecture team to define a custom Azure resolution path
  for the following unresolved BLOCKER: <BlockerDescription>

Why now:
- Assessment pipeline halted at Level 3 HITL. The identified BLOCKER pattern
  ("<BlockerName>") has no entry in the §19 BizTalk Feature Migration Risk Table
  or the §22 Migration Decision Tree. The tool cannot produce a reliable
  migration recommendation without a human-defined resolution path.

Evidence collected before halt:
  <list all Phase 1-4 agent findings collected before the BLOCKER was reached>

Options:
- Option A: Define a new Azure resolution path → tool is updated; assessment resumes
- Option B: Declare the application as NOT_READY for migration → assessment closes

Recommendation:
- Option A if the BLOCKER is a known Azure architecture challenge with precedents.
  Option B only if the BLOCKER involves an Azure capability that does not yet exist.

Decision deadline:
- Before the assessment is rescheduled. Current partial findings expire after 30 days
  if not acted upon (artifacts may change; evidence becomes stale).
```

---

### 35.10 HITL — Summary Decision Table

The following table is the Orchestrator's decision reference for every HITL trigger. It must be evaluated in order after each agent output is received.

| Finding State | Agent Level | Pipeline Action | Output Document Label |
|--------------|-------------|----------------|----------------------|
| `CONFIRMED_IN_SOURCE` or `CONFIRMED_IN_BINDING` | Any | Continue | `[CONFIRMED IN SOURCE/BINDING]` |
| `INFERRED` + finding severity LOW or MEDIUM | Any | Continue, flag for human review | `[INFERRED — verify manually]` |
| `INFERRED` + finding severity HIGH | Any | Continue, generate Level 1 HITL request | `[INFERRED — verify manually]` |
| `INFERRED` + finding severity BLOCKER | Any | HALT (Level 2), generate T1 request | `[BLOCKED_PENDING_HUMAN]` until resolved |
| `NOT_FOUND` + expected artifact type | Any | Continue with `MISSING` flag | `[NOT_FOUND_IN_ARTIFACTS]` |
| `sourceFound: false` on GAC assembly | Inventory | Level 1 HITL, agent continues partially | `[BLOCKED — source not found]` |
| Confidence < 0.6 on BLOCKER | Any | HALT (Level 2), generate T2 request | `[BLOCKED_PENDING_HUMAN]` |
| Two agents conflict on same artifact | Consolidation | HALT (Level 2), generate T3 request | `[CONFLICT — pending resolution]` |
| Business decision needed (NON_ATOMIC) | Anti-Patterns | Level 1 HITL, continue | `[PENDING_BUSINESS_DECISION]` |
| BLOCKER with no §19 / §22 match | Risk Agent | HALT (Level 3), generate T8 + Approval Packet | `ASSESSMENT_INCOMPLETE` |
| HUMAN_CONFIRMED | Orchestrator (resume) | Continue; finding upgraded | `[HUMAN CONFIRMED — <name>]` |
