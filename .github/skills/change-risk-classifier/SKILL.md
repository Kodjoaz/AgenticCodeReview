---
name: change-risk-classifier
description: Fast risk classification for scope, blast radius, reversibility, contract impact, and approval burden before execution.
---

# Change Risk Classifier Skill

Purpose: classify the real delivery risk of a proposed change before delegation or implementation starts.

Primary owners:
- `Maximus`
- `TaskManager`
- `ProductManager` for intake-quality checks

---

## Dimensions

Score each dimension as `low`, `medium`, or `high`.

### 1. Blast Radius

- How many domains, services, or users are affected?
- Is this local, cross-domain, or platform-wide?

### 2. Reversibility

- Can the change be rolled back quickly?
- Does it create irreversible schema, data, or contract consequences?

### 3. Contract Impact

- Does it change API behavior, schema shape, event shape, or operational expectations?

### 4. Security / Tenant Sensitivity

- Does it touch auth, permissions, secrets, tenancy, or public exposure?

### 5. Evidence Burden

- What level of testing, validation, and human review is required to claim confidence?

---

## Output Format

```text
CHANGE RISK

Overall:
- LOW | MEDIUM | HIGH

Dimensions:
- blast_radius: [low|medium|high] — [note]
- reversibility: [low|medium|high] — [note]
- contract_impact: [low|medium|high] — [note]
- security_tenant_sensitivity: [low|medium|high] — [note]
- evidence_burden: [low|medium|high] — [note]

Required workflow:
- EXPRESS | STANDARD | FULL

Required gates:
- [none | explicit gates]
```

---

## Classification Heuristics

- Overall is `HIGH` if security/tenant sensitivity is high, or if reversibility is high-risk, or if two or more other dimensions are high.
- Overall is `MEDIUM` if there is meaningful cross-file or cross-domain change but rollback remains manageable.
- Overall is `LOW` only if scope is narrow, reversible, and contract-safe.

Workflow guidance:
- `LOW` -> usually `EXPRESS`
- `MEDIUM` -> usually `STANDARD`
- `HIGH` -> always `FULL`

---

## Gate Mapping

- High security/tenant sensitivity -> SecurityEngineer and human approval before ship
- High contract impact -> SolutionArchitect review before implementation or merge
- High reversibility risk -> rollback plan must exist before execution
- High evidence burden -> targeted tests plus validation summary are mandatory
