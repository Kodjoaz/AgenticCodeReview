# CADO Framework Risk Policy

CADO Framework classifies change risk as Low, Medium, or High. Risk determines whether work can proceed directly or must stop for explicit approval.

## Risk Tiers

### Low

Routine, bounded changes with low blast radius.

Typical examples:
- Small docs-only updates
- Narrow test changes
- Low-impact refactors with no behavior change
- Internal tooling adjustments with clear rollback

Approval rule:
- No extra gate is required beyond normal review.

### Medium

Changes with moderate blast radius, operational effect, or meaningful regression risk.

Typical examples:
- User-facing behavior changes
- Non-destructive config or infrastructure updates
- Moderate dependency upgrades
- New automation that affects delivery flow

Approval rule:
- The Conductor should confirm risk and capture explicit approval when the impact is not routine.
- The `cado-approve` label is recommended when the team wants a visible gate in GitHub.

### High

Changes with security, data, money, or production stability implications.

Typical examples:
- Auth or permissions changes
- Secrets, keys, or credential handling
- Cryptography or security control changes
- Payments or billing logic
- Data migrations or destructive operations
- Production config or infrastructure changes
- Major dependency upgrades with upgrade risk

Approval rule:
- Explicit approval is required before Build proceeds.
- The default GitHub mechanism is a PR label named `cado-approve`.

## Approval Triggers

Treat the following as approval triggers unless a stricter local policy already exists:

- Authentication, authorization, or permissions changes
- Secret, key, token, or credential handling
- Cryptography, signing, encryption, or security control changes
- Payments, billing, invoicing, or quota enforcement changes
- Data migrations, schema rewrites, destructive operations, or rollback-sensitive work
- Production environment config, infrastructure, deployment path, or runtime access changes
- Major dependency upgrades or framework jumps

## `cado-approve` Label Policy

Use `cado-approve` as the default visible approval signal in GitHub.

Policy rules:
- Add the label after Plan is complete and the risk tier is known.
- Do not start Build for High risk work until the label is present.
- If scope expands in Build and raises risk, remove confidence, return to Gate, and require a fresh approval decision.
- Removing the label reopens the gate and blocks further progress until approval is restored.

## Practical Notes

- Local repositories may document the policy before automating it.
- Teams can add stricter mechanisms such as CODEOWNERS or protected environments, but those are extensions of this baseline policy.

