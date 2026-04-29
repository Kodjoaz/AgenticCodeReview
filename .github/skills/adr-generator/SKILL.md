---
name: adr-generator
description: Produce architecture decision records for significant technical decisions and tradeoffs.
---

# Skill: ADR Generator

Use this skill for significant technical choices that affect architecture, interfaces, hosting, security posture, or long-term maintainability.

## When ADRs Are Required

Create an ADR when the design includes any of the following:

- a new service or subsystem boundary
- a new database or storage pattern
- a messaging or async processing decision
- an auth, RBAC, or identity approach
- a hosting, deployment, or topology change
- a meaningful build-vs-buy tradeoff
- any decision likely to be revisited later

## Required ADR Structure

```text
ADR-NNNN: [Title]
Status: proposed | approved | superseded
Context: [problem statement, constraints, and drivers]
Options considered:
  - [option 1]
  - [option 2]
Decision: [chosen option]
Consequences: [benefits, costs, coupling, operational impact]
Risks: [top risks + mitigation]
Follow-up actions:
  - [implementation or review action]
```

## Required Markdown Template

Use this structure unless the user explicitly requests another format:

```md
# ADR-NNNN: [Title]

## Status
proposed | approved | superseded

## Context
- Problem:
- Constraints:
- Drivers:

## Options Considered
### Option A
- Description:
- Benefits:
- Costs / risks:

### Option B
- Description:
- Benefits:
- Costs / risks:

## Decision
- Chosen option:
- Why this option won:

## Consequences
- Positive:
- Negative:
- Operational impact:

## Risks and Mitigations
- Risk:
  Mitigation:

## Follow-up Actions
- ...
```

## Example Skeleton

```md
# ADR-0007: Use Async Ingestion Workers For Document Processing

## Status
proposed

## Context
- Problem: request-path ingestion makes uploads slow and brittle.
- Constraints: tenant isolation, retry safety, bounded worker capacity.
- Drivers: latency, resilience, operational visibility.

## Options Considered
### Option A
- Description: synchronous ingestion in API request path.
- Benefits: simpler architecture.
- Costs / risks: high latency, timeout and retry failure modes.

### Option B
- Description: enqueue uploads for worker-based async ingestion.
- Benefits: better resilience and scale control.
- Costs / risks: more operational moving parts.

## Decision
- Chosen option: Option B.
- Why this option won: it isolates ingestion latency from user-facing APIs and improves retry behavior.
```

## Quality Bar

- Include at least two realistic options for non-trivial decisions.
- State why the chosen option won.
- Make downstream implementation impact explicit.
- Do not hide tradeoffs; architecture quality depends on honest consequences.

## Completion Rule

An ADR is incomplete if:

- it names only one option for a non-trivial decision,
- it states a decision without consequences,
- it lists risks without mitigations,
- it cannot be tied to downstream implementation work,
- it hides the actual reason the choice was made.
