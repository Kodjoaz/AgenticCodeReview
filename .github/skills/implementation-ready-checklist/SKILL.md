---
name: implementation-ready-checklist
description: Validate that a solution design is specific enough for implementation handoff and specialist execution.
---

# Skill: Implementation-Ready Checklist

Use this skill before handing a design to implementation agents.

## The Design Is Not Ready Unless All Items Are True

- Component boundaries are explicit and there is no hidden coupling.
- Specialist ownership is assigned per workstream.
- API, event, schema, or storage contracts are identified.
- Security and tenant-isolation implications are stated.
- Operational expectations are stated: deployment, observability, failure handling.
- Data migration impact is called out if applicable.
- Rollout and rollback expectations are present for risky changes.
- Acceptance criteria are testable by QualityEngineer.
- Open questions are listed separately from resolved decisions.
- No `TBD` item blocks implementation start.

## Handoff Output

Before completion, return a short checklist result:

```text
IMPLEMENTATION READINESS: ready | not-ready

GAPS:
  - [missing item or "none"]

SPECIALIST HANDOFFS:
  - BackendEngineer: [contract]
  - FrontendEngineer: [contract]
  - DataEngineer: [contract]
  - AIEngineer: [contract]
  - PlatformEngineer: [contract]
  - SecurityEngineer: [contract or review gate]
  - QualityEngineer: [validation expectations]
```

## Review Dependencies

Call out these review dependencies explicitly when relevant:

- `ProductManager review required`
- `SecurityEngineer review required`
- `PlatformEngineer review required`
- `Human approval required`

If the result is `not-ready`, stop and revise the design before implementation begins.
