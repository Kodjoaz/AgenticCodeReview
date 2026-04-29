---
name: governance-policy-review
description: Review agent/tool workflows for governance gaps, trust boundaries, and enforceable policy controls.
---

# Skill: Governance Policy Review

## Purpose
Add fail-closed governance checks to agentic workflows so high-impact actions are policy-controlled and auditable.

## Owner
- `SecurityEngineer`
- `SolutionArchitect`

## Used In
- gate reviews for high-risk changes
- tool and agent permission changes
- multi-agent delegation design

## Inputs
- Agent and tool definitions
- Allowed/blocked operation policy inputs
- Delegation paths and trust boundaries
- Audit log requirements
- Rate-limit and escalation constraints

## Procedure
- Verify allowlist-first policy posture for high-impact operations.
- Identify missing enforcement points before tool invocation.
- Check trust boundaries across delegated agent flows.
- Validate audit trail completeness for policy decisions.
- Confirm sensitive operations fail closed on ambiguity.
- Ensure escalation path exists for human-in-the-loop approvals.
- Return minimum set of controls to close identified risks.

## Output Contract
Expected output headings:
- `GOVERNANCE POLICY REVIEW`
- `Policy Coverage`
- `Trust Boundaries`
- `Auditability`
- `Control Gaps`
- `Required Controls`
- `Decision`

## Validation
- Findings tie to explicit policy or trust assumptions.
- Recommendations are enforceable in code/config, not aspirational.
- High-impact operations have deny-by-default behavior.
- Audit trail requirements are append-only and complete.
