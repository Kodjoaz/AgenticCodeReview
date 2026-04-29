---
name: test-gap-detector
description: Detect missing or weak test coverage against changed behavior and acceptance criteria.
---

# Skill: Test Gap Detector

## Purpose
Find meaningful testing blind spots before merge by mapping code and requirements changes to expected coverage.

## Owner
- `QualityEngineer`

## Used In
- `plan` stage for test scope definition
- `prove` stage before completion claims
- regression triage after escaped defects

## Inputs
- Change diff and impacted modules
- Acceptance criteria from spec artifacts
- Existing test inventory and coverage signals
- Recent incident or bug patterns in the same area

## Procedure
- Enumerate behavioral changes from code and config diffs.
- Map each acceptance criterion to at least one test layer.
- Identify high-risk paths with no direct tests.
- Flag weak assertions that only validate happy-path outputs.
- Propose minimal new tests with target file and scope.
- Mark deferred tests with rationale and risk impact.

## Output Contract
Expected output headings:
- `TEST GAP ANALYSIS`
- `Change Surface`
- `Mapped Coverage`
- `Detected Gaps`
- `Recommended Tests`
- `Deferred Coverage`
- `Merge Impact`

## Validation
- Every reported gap maps to a concrete behavior or criterion.
- Recommendations are realistic for current scope and risk.
- High-risk uncovered paths are marked as blockers, not notes.
- Deferred coverage includes explicit follow-up trigger.
