---
name: dependency-upgrade-safety
description: Evaluate dependency update risk, compatibility impact, and rollback readiness before merging upgrades.
---

# Skill: Dependency Upgrade Safety

## Purpose
Reduce upgrade regressions by forcing explicit compatibility checks, impact scope review, and rollback planning.

## Owner
- `PlatformEngineer`
- `SecurityEngineer`

## Used In
- dependency refresh and patch cycles
- security remediation updates
- CI recovery when tooling versions change

## Inputs
- Dependency diff (old version -> new version)
- Upstream release notes or changelog summary
- Impacted package graph and runtime surfaces
- Existing test and build baseline
- Rollback mechanism in repo

## Procedure
- Classify update type: patch, minor, major, or transitive-only drift.
- Identify breaking-change signals from release notes and deprecations.
- Map impacted runtime/build paths and critical integrations.
- Define minimum verification matrix across lint, build, tests, and smoke.
- Check for new security advisories introduced or resolved.
- Require explicit rollback command or version pin strategy.
- Recommend merge policy: auto, guarded, or blocked.

## Output Contract
Expected output headings:
- `DEPENDENCY UPGRADE SAFETY`
- `Upgrade Scope`
- `Compatibility Risk`
- `Security Impact`
- `Verification Matrix`
- `Rollback Plan`
- `Decision`

## Validation
- Risk rating is justified by concrete compatibility evidence.
- Verification matrix covers all affected execution paths.
- Rollback plan can be executed in one command sequence.
- Decision aligns with update type and observed failures.
