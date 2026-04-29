---
name: ci-failure-triage
description: Rapidly analyze CI failures, isolate probable root cause, and define the safest next fix.
---

# Skill: CI Failure Triage

## Purpose
Provide a repeatable way to turn noisy CI failures into clear root-cause hypotheses and actionable fixes.

## Owner
- `PlatformEngineer`
- `Maximus`

## Used In
- `prove` stage when checks fail
- `ship` readiness rechecks after hotfixes
- incident-style release blocking triage

## Inputs
- Failing workflow name and run link
- Failed job/step names and logs
- Commit or PR diff in scope
- Recent related config or dependency changes
- Prior flaky test history, if available

## Procedure
- Confirm the exact failing check and capture first failure timestamp.
- Classify failure type: build, test, lint, packaging, infra, or secret/config.
- Isolate smallest failing unit (single test, target, step, or script).
- Compare with latest green run to identify drift in code, config, or environment.
- Form one primary root-cause hypothesis and one fallback hypothesis.
- Propose a minimal fix with rollback path and explicit owner.
- Define re-run strategy: targeted check first, then full required gates.

## Output Contract
Expected output headings:
- `CI FAILURE TRIAGE`
- `Failure Signal`
- `Impact`
- `Root Cause Hypothesis`
- `Proposed Fix`
- `Verification Plan`
- `Risk and Rollback`
- `Owner and ETA`

## Validation
- Root cause points to a concrete failing step, not generic instability.
- Proposed fix changes only the necessary files or configuration.
- Verification plan includes at least one targeted rerun and full gate rerun.
- Rollback instructions are present for medium/high-risk changes.