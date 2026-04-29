---
name: review-pack
description: Structured functional and standards review workflow for code changes, regressions, edge cases, and missing tests.
---

# Review Pack Skill

Primary owners:
- `QualityEngineer`
- `Maximus` when deciding whether implementation evidence is sufficient

---

## When to Use

- Before release or merge recommendation
- When the user asks for a review
- After a specialist claims a complex or risky task is complete
- When a change touches auth, proxying, migrations, contracts, or rollout logic

---

## Review Lenses

Run these lenses in order.

### 1. Functional Correctness

Check whether the implementation actually satisfies the requested behavior.

- Inputs and outputs match the spec
- Edge cases are handled
- Error paths are explicit
- Backward compatibility is preserved unless intentionally changed

### 2. Standards and Convention Fit

Check whether the change fits repo conventions.

- Ownership boundaries respected
- Existing architecture layers preserved
- Naming and file placement consistent
- No unnecessary framework or dependency added

### 3. Regression Risk

Look for likely breakage.

- Public API contract drift
- Config shape drift
- Cross-tenant or cross-role access changes
- Partial rollout or migration failure paths

### 4. Test Sufficiency

Check that testing matches risk.

- Changed behavior has targeted tests
- High-risk paths have integration coverage or clear manual verification
- Existing tests were run, not merely assumed

---

## Findings Format

When findings exist, present them first, ordered by severity.

Use this structure:

```text
FINDING: [severity] [short title]
Why it matters: [one or two lines]
Evidence: [file/behavior/command]
Expected fix: [actionable correction]
```

Severity levels:
- `high` — likely production bug, security exposure, data loss, broken contract
- `medium` — meaningful regression risk, missing validation, weak fallback
- `low` — quality issue, weak test coverage, maintainability concern

If no issues are found, say so explicitly and still note residual test or rollout risk.

---

## Output Contract

```text
REVIEW VERDICT: PASS | CONDITIONAL | FAIL

FINDINGS:
  - [severity]: [summary]

RESIDUAL RISKS:
  [none | short description]

TEST GAPS:
  [none | short description]
```

Rules:
- Do not hide findings behind summary text.
- Do not mark PASS if a high-severity issue exists.
- Do not treat missing evidence as success.
