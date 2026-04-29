---
name: qa-adversarial-verification
description: Execute adversarial QA verification with boundary, error-path, and reproducibility-first standards.
---

# Skill: QA Adversarial Verification

## Purpose
Increase release confidence by systematically probing edge cases, failure modes, and regression vectors before ship.

## Owner
- `QualityEngineer`

## Used In
- prove stage validation
- high-risk release readiness checks
- defect triage and reproduction

## Inputs
- Requirements and acceptance criteria
- Changed components and risk notes
- Existing test suite and failure reports
- Runtime/config permutations
- Known defect history in affected modules

## Procedure
- Build risk-based test matrix across happy, boundary, negative, and error paths.
- Reproduce reported defects with deterministic steps and fixtures.
- Add or update automated tests for recurring failure vectors.
- Validate concurrency/idempotency where state mutation exists.
- Confirm security-oriented misuse paths are covered for exposed surfaces.
- Separate confirmed defects from speculative improvements.
- Produce severity-ranked findings with clear reproduction evidence.

## Output Contract
Expected output headings:
- `QA ADVERSARIAL VERIFICATION`
- `Test Matrix`
- `Reproduced Defects`
- `New/Updated Tests`
- `Severity Findings`
- `Release Recommendation`

## Validation
- Findings include reproducible steps and expected vs actual behavior.
- Test matrix covers high-risk paths and non-happy scenarios.
- Added tests are deterministic and aligned with project conventions.
- Release recommendation is evidence-backed and severity-aware.
