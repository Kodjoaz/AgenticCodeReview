# CADO Framework Evidence Contract

CADO Framework treats completion as evidence-based. A change is not done because code was written. It is done only when the required proof for that change has been captured and reviewed.

## Core Rule

No evidence means not done.

If a category is relevant to the change, the run record or PR must include evidence for it. Missing evidence blocks Ship.

## Required Evidence Categories

Use the relevant categories below.

- Tests run or CI pass: unit, integration, smoke, or workflow validation
- Lint or format: static checks, style validation, or policy checks
- Build or package: compilation, bundle creation, image build, or packaging output
- Security checks: secret scan, dependency audit, permissions review, or threat-sensitive validation
- Migration or rollback notes: forward path, rollback path, and operator notes for risky changes
- Docs updates: user docs, runbooks, changelog, or usage notes when behavior changes

## Reusable Evidence Section Format

Use this section in a PR, issue, or run record.

```md
## Evidence

- Category: Tests
  Tool: <tool or CI system>
  Command: <command or job name>
  Result: pass | fail | partial
  Link: <optional link to CI run, log, or report>
  Notes: <short outcome>

- Category: Lint
  Tool: <tool>
  Command: <command>
  Result: pass | fail | partial
  Link: <optional>
  Notes: <short outcome>
```

Add as many entries as needed. If a category is not relevant, say why it is not applicable.

## Evidence Quality Rules

- Evidence must be specific enough that another reviewer can verify what happened.
- Evidence should be captured at Prove, not reconstructed later from memory.
- Partial results must explain what remains open.
- A failed check is blocking unless the Conductor records an explicit exception and reason.

## Baseline Passing Thresholds

When no project-specific thresholds are defined in `.cado/config.yml`, apply
these baselines:

- Tests: all tests pass; no new failures introduced relative to the base branch.
- Lint / format: no new violations; pre-existing violations may be recorded as
  exceptions with owner and issue reference.
- Build / package: clean build with no errors.
- Security checks: no HIGH or CRITICAL severity findings left unaddressed.
- Migration / rollback: both forward and rollback paths verified by test or
  manual procedure; procedure documented.
- Docs: updated when any user-visible behavior changes.

## Definition Of Done

A run can move to Ship only when:

- Required checks have been completed.
- Evidence is recorded in a consistent format.
- Risk-related proof is present when relevant.
- Missing or failing evidence has been resolved or formally blocked.
