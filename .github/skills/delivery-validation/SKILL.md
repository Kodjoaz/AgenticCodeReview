---
name: delivery-validation
description: Validate traceability and drift across spec, plan, tasks, implementation, and completion evidence.
---

# Delivery Validation Skill

Primary owners:
- `TaskManager`
- `QualityEngineer`
- `Maximus`

---

## What It Validates

### 1. Spec to Plan

- Plan addresses the accepted scope
- Non-goals are not silently pulled into the build
- Dependencies and risks are reflected

### 2. Plan to Tasks

- Each major plan area has executable tasks
- Ownership is explicit
- Dependency order makes sense
- Validation commands are present for risky work

### 3. Tasks to Implementation

- Changed files line up with task intent
- Completed claims match actual code or docs changes
- No major plan item is skipped without explanation

### 4. Implementation to Evidence

- Required checks were actually run
- Evidence is specific, not generic
- Residual risks are surfaced instead of hidden

---

## Drift Signals

Flag validation issues when you see:

- Files changed outside stated scope without explanation
- Claimed completion with no changed artifact or test evidence
- Acceptance criteria not mapped to any implementation or test
- Plan says one thing while tasks or code implement another
- Significant behavior added without specification approval

---

## Validation Output

```text
DELIVERY VALIDATION: PASS | PARTIAL | FAIL

TRACEABILITY:
  - spec -> plan: PASS | FAIL — [note]
  - plan -> tasks: PASS | FAIL — [note]
  - tasks -> implementation: PASS | FAIL — [note]
  - implementation -> evidence: PASS | FAIL — [note]

DRIFT:
  - [none | itemized drift]

ACTION REQUIRED:
  - [none | exact correction needed]
```

Rules:
- `PASS` only if there is no material drift.
- `PARTIAL` if work is directionally correct but evidence or traceability is incomplete.
- `FAIL` if scope, implementation, or verification materially diverges from the approved path.
