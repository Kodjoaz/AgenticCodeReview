---
name: solution-design-doc
description: Generate implementation-ready solution design documents for cross-domain product and platform work.
---

# Skill: Solution Design Document

Use this skill when the user asks for solution design, architecture planning, system decomposition, or implementation-ready design documentation.

## Goal

Produce a Markdown design document that is specific enough for implementation agents to start work without reopening basic architectural questions.

## Required Sections

Every solution design doc must include:

1. Problem and context
- What problem is being solved.
- Why now.
- Who the users or operators are.

2. Scope and non-goals
- Explicitly define what is included.
- Explicitly define what is not included.

3. Constraints and assumptions
- Technical, business, compliance, platform, and delivery constraints.
- Assumptions that could invalidate the design if proven wrong.

4. Proposed solution
- High-level approach.
- Major components and responsibilities.
- Integration points.

5. Data flow and contracts
- Inputs, outputs, system boundaries.
- API, event, schema, or storage contracts that matter to implementation.

6. Operational design
- Deployment/runtime model.
- Observability requirements.
- Failure handling and recovery expectations.

7. Security and tenant boundaries
- Security surface area.
- Secrets, auth, RBAC, data isolation, and cross-tenant controls when relevant.

8. Tradeoffs and risks
- Alternatives considered.
- Why they were rejected.
- Top risks and mitigations.

9. Delivery plan
- Implementation phases.
- Specialist ownership by workstream.
- Dependencies and sequencing.

10. Open questions
- Items that need explicit human or specialist resolution before implementation.

## Output Standard

- Prefer concise, implementation-ready Markdown.
- Avoid vague statements like "use best practices" without naming the actual mechanism.
- Call out ownership explicitly for BackendEngineer, FrontendEngineer, DataEngineer, AIEngineer, PlatformEngineer, SecurityEngineer, and QualityEngineer where relevant.
- If information is missing, state the gap rather than inventing details.

## Required Markdown Template

Use this structure unless the user explicitly requests another format:

```md
# Solution Design: [Title]

## 1. Summary
- Problem:
- Proposed solution:
- Primary users/operators:
- Why now:

## 2. Scope
### In Scope
- ...

### Out of Scope
- ...

## 3. Constraints and Assumptions
### Constraints
- ...

### Assumptions
- ...

## 4. Proposed Architecture
### Components
| Component | Responsibility | Owner |
|---|---|---|
| ... | ... | BackendEngineer |

### Integration Points
| Interface | Producer | Consumer | Contract |
|---|---|---|---|
| ... | ... | ... | ... |

## 5. Data Flow and Contracts
- Request flow:
- Event flow:
- Storage/schema impact:
- External dependencies:

## 6. Operational Design
- Runtime/deployment model:
- Observability:
- Failure handling:
- Rollback strategy:

## 7. Security and Tenant Boundaries
- Auth/RBAC implications:
- Secret handling:
- Tenant isolation:
- Security review trigger:

## 8. Tradeoffs and Risks
### Alternatives Considered
- Option A:
- Option B:

### Chosen Tradeoffs
- ...

### Top Risks
- Risk:
	Mitigation:

## 9. Delivery Plan
| Phase | Workstream | Owner | Depends On |
|---|---|---|---|
| 1 | ... | BackendEngineer | none |

## 10. Acceptance Criteria
- ...
- ...

## 11. Open Questions
- ...
```

## Example Skeleton

Use this as a quality reference for tone and specificity:

```md
# Solution Design: Tenant-Scoped Document Ingestion

## 1. Summary
- Problem: Users need to upload documents and make them searchable within their own tenant boundary.
- Proposed solution: Add an ingestion API, async processing pipeline, tenant-scoped storage metadata, and indexing workflow.
- Primary users/operators: Tenant admins, platform operators.
- Why now: Current document workflows are manual and not searchable.

## 2. Scope
### In Scope
- Upload API
- Async ingestion pipeline
- Tenant-scoped metadata and indexing

### Out of Scope
- OCR for handwritten scans
- Cross-tenant document sharing

## 4. Proposed Architecture
### Components
| Component | Responsibility | Owner |
|---|---|---|
| Upload API | Accept files, validate tenant/user context | BackendEngineer |
| Ingestion worker | Parse, chunk, and queue indexing | PlatformEngineer |
| Metadata store | Persist document state and tenant ownership | DataEngineer |
| Search/index pipeline | Create searchable chunks | AIEngineer |

## 8. Tradeoffs and Risks
### Alternatives Considered
- Sync ingestion in request path: rejected due to latency and retry risk.
- Async ingestion with worker: chosen for resilience and scale.

### Top Risks
- Risk: large files starve workers.
	Mitigation: enforce file size limits and worker concurrency caps.
```

## Completion Rule

The design doc is incomplete if:

- it lacks explicit ownership per workstream,
- it does not separate in-scope from out-of-scope work,
- it describes integrations without naming the contract,
- it lists risks without mitigations,
- it leaves open questions mixed into resolved sections.
