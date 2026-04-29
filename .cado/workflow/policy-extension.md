# CADO Framework Policy Extension Guide

This guide explains how gate control works by default and how a repository can
add stricter local policy on top.

## Default Gate Control Model

CADO gate control is evaluated across four layers:

1. Stage transitions from `.cado/workflow/stages.md`
2. Risk policy from `.cado/workflow/risk-policy.md`
3. Evidence contract from `.cado/workflow/evidence-contract.md`
4. Hook execution from `.cado/extensions.yml`

If any required condition fails, progression is blocked.

## Baseline Decision Sequence

At runtime, the Conductor should evaluate gate readiness in this order:

1. Confirm Plan exists and risk tier is recorded.
2. Apply risk-tier approval rules (Low, Medium, High).
3. Apply approval triggers (auth, secrets, migrations, production infra, and so on).
4. Validate required evidence categories for current stage.
5. Run policy hooks from `.cado/extensions.yml`.
6. Return one decision:
   - pass
   - block
   - pass-with-exception (must include explicit waiver record)

## Extension Principles

Local policy should follow these principles:

- Additive: local policy extends framework defaults.
- Fail-closed: missing required policy data should block by default.
- Auditable: every block or waiver must be written to run evidence.
- Explicit waivers: temporary exceptions must include owner and expiry.

## Recommended Repository Policy Files

Create these files in target repositories:

- `.cado/policies/gate-policy.yml`: machine-readable policy
- `.cado/policies/gate-policy.md`: human policy and rationale

Template sources in framework:

- `src/templates/gate-policy.yml`
- `src/templates/gate-policy.md`

## Suggested Merge Semantics

When both framework and repo policy exist:

- Keep framework safety baseline.
- Apply repo policy as stricter overrides.
- Do not silently downgrade a required baseline rule.

Practical merge behavior:

- approvals.min_reviewers: use the higher value
- approvals.required_labels: union of baseline and local labels
- evidence.required_categories: union of baseline and local categories
- triggers: union; any matched trigger applies stricter path
- waivers: allowed only when local policy explicitly permits

## Example Local Extension Patterns

### 1) Tighten Medium Risk Approval

- Baseline: Medium risk may proceed with standard review.
- Extension: require `cado-approve` label and one domain owner approval.

### 2) Add Regulated Data Rule

- Trigger: change touches PII-related schema or processing paths.
- Extension: require SecurityEngineer review and rollback notes.

### 3) Add Production Infra Guard

- Trigger: infra or deployment pipeline files changed.
- Extension: require PlatformEngineer approval and prove-stage evidence from deployment dry run.

## Waiver Contract (Recommended)

When policy allows temporary exceptions, require all fields:

- waiver_id
- rule_id
- reason
- approved_by
- expires_at
- compensating_controls

Expired waivers should block by default.

## Implementation Notes

- Start with report-only mode while calibrating policy quality.
- Switch to enforce mode after at least one successful release cycle.
- Keep policy rules small and explicit.
- Prefer deterministic rule conditions over broad natural language checks.
