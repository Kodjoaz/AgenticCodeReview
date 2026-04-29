---
name: github-actions-hardening
description: Harden GitHub Actions workflows with least privilege, immutable action pinning, and secure runner practices.
---

# Skill: GitHub Actions Hardening

## Purpose
Improve CI/CD security and reliability by enforcing hardened workflow patterns and supply-chain safeguards.

## Owner
- `PlatformEngineer`
- `SecurityEngineer`

## Used In
- workflow authoring and updates
- release pipeline reviews
- CI/CD incident prevention

## Inputs
- Workflow YAML files
- Required permissions and secrets usage
- Target environments and deployment steps
- Caching/concurrency expectations
- Compliance controls and branch protections

## Procedure
- Set default workflow permissions to least privilege.
- Enforce immutable action pinning for third-party actions.
- Validate OIDC preference over long-lived cloud credentials.
- Add concurrency controls to prevent unsafe parallel executions.
- Verify dependency/security scanning coverage in critical flows.
- Review secret handling for log exposure and scope minimization.
- Define validation checks (lint, dry run, policy checks) before merge.

## Output Contract
Expected output headings:
- `GITHUB ACTIONS HARDENING`
- `Permission Model`
- `Supply Chain Controls`
- `Secrets and Identity`
- `Execution Safety`
- `Hardening Actions`
- `Decision`

## Validation
- Permission grants are justified per job capability.
- Actions pinning guidance is explicit and actionable.
- Identity and secret recommendations reduce credential risk.
- Hardening actions are testable before rollout.
