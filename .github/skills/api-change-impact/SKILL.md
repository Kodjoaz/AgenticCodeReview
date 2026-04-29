---
name: api-change-impact
description: Detect and classify API contract changes, consumer impact, and migration risk before merge.
---

# Skill: API Change Impact

## Purpose
Prevent accidental breaking integrations by identifying contract deltas and forcing explicit migration guidance.

## Owner
- `SolutionArchitect`
- `BackendEngineer`

## Used In
- API endpoint changes
- schema evolution and versioning
- integration and SDK updates

## Inputs
- Before/after API contract (OpenAPI, protobuf, or route/schema diff)
- Changed handlers and serializers
- Known consumer inventory and integration points
- Deprecation policy and versioning rules
- Existing compatibility tests

## Procedure
- Classify each change as additive, behavioral, deprecated, or breaking.
- Identify impacted consumers by endpoint/schema usage.
- Evaluate backward compatibility and versioning policy compliance.
- Flag changes requiring migration notes or dual-support windows.
- Define focused validation for old and new contract paths.
- Propose rollout plan (feature flag, shadow mode, phased deployment).
- Recommend release note and communication requirements.

## Output Contract
Expected output headings:
- `API CHANGE IMPACT`
- `Contract Delta`
- `Consumer Impact`
- `Compatibility Assessment`
- `Migration Plan`
- `Validation Plan`
- `Decision`

## Validation
- Breaking vs non-breaking classification is explicit per change.
- Consumer impact references concrete callers or dependency groups.
- Migration plan contains actionable steps and timeline.
- Validation plan covers both backward and forward paths.
