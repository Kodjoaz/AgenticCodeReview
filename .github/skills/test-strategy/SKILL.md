---
name: test-strategy
description: Upfront test planning skill for selecting the right mix of unit, integration, contract, smoke, and release checks based on change risk.
---

# Test Strategy Skill

Primary owners:
- `QualityEngineer`
- `BackendEngineer`
- `FrontendEngineer`
- `AIEngineer` for workflow and model-adjacent evaluation paths

---

## When to Use

- New feature or capability work before implementation starts
- High-risk or cross-domain changes
- API, schema, auth, workflow, or migration changes
- Any task where release readiness depends on more than a single happy-path test

---

## Strategy Pass

### 1. Risk and Surface Area

Identify:
- user-visible behavior
- internal contracts
- persistence or schema impact
- configuration or rollout sensitivity

### 2. Test Pyramid Selection

Choose the minimum effective set from:
- unit tests
- component tests
- integration tests
- contract tests
- smoke tests
- end-to-end tests
- non-functional checks such as security or performance validation

Do not default to full E2E coverage when lower-level checks give faster confidence.

### 3. Assertions and Fixtures

Define:
- key behaviors to assert
- mocks or fixtures required
- external dependencies that must be simulated
- failure paths that must be covered

### 4. Release Gates

Specify:
- mandatory commands before merge
- mandatory commands before release
- which failures are hard stops versus report-only findings

### 5. Coverage and Gaps

State:
- what is intentionally not tested now
- why that omission is acceptable
- what follow-up coverage is required later

---

## Test Strategy Output

```text
TEST STRATEGY

Change:
- [one-line description]

Risk summary:
- [low | medium | high] — [reason]

Required test layers:
- [layer] — [why]

Key assertions:
- [assertion]

Fixtures / mocks:
- [item]

Failure paths to cover:
- [path]

Merge gates:
- [command]

Release gates:
- [command]

Intentional gaps:
- [none | gap]

Recommendation:
- READY FOR IMPLEMENTATION
- CONDITIONAL — add missing test plan details
- BLOCKED — risk too high for current test coverage plan
```

---

## Guardrails

- Match test depth to risk; do not over-prescribe slow E2E suites.
- Every acceptance criterion in `spec.md` should map to at least one test layer.
- If a breaking contract, migration, or auth flow has no contract/integration coverage, return `BLOCKED`.
