---
name: security-patch-impact
description: Prioritize security patch upgrades by exploitability, runtime exposure, and verification confidence.
---

# Skill: Security Patch Impact

## Purpose
Reduce security risk without unnecessary churn by ranking patch urgency and defining the minimum safe validation path.

## Owner
- `SecurityEngineer`
- `PlatformEngineer`

## Used In
- CVE and advisory triage
- emergency patch windows
- routine dependency security reviews

## Inputs
- Advisory details (CVE/GHSA, severity, affected ranges)
- Current dependency versions in repository
- Runtime exposure context (public edge, internal only, dev tool only)
- Existing mitigations or compensating controls
- Test/build baseline and rollback strategy

## Procedure
- Confirm whether the vulnerable range is actually present in lockfiles or manifests.
- Classify exploitability in your environment (reachable, partially reachable, not reachable).
- Rank urgency using severity, exploitability, and service criticality.
- Map affected components and tenant/user blast radius.
- Propose patch target version and pinning strategy.
- Define minimum validation matrix before merge and deploy.
- Document rollback trigger and exact reversion steps.

## Output Contract
Expected output headings:
- `SECURITY PATCH IMPACT`
- `Advisory Summary`
- `Exposure and Exploitability`
- `Urgency Rating`
- `Patch Plan`
- `Validation and Rollback`
- `Decision`

## Validation
- Recommendation states why urgency is high/medium/low with evidence.
- Exposure claim is tied to actual repository/runtime paths.
- Patch plan includes concrete target versions.
- Rollback path is executable and tested in principle.
