---
name: approval-packet
description: Structured human decision packet for risky changes, tradeoffs, recommended option, and rollback path.
---

# Approval Packet Skill

Purpose: standardize how risky or ambiguous decisions are escalated to the human so approvals are based on a clear packet, not scattered reasoning.

Primary owners:
- `Maximus`
- `SolutionArchitect`
- `SecurityEngineer`
- `PlatformEngineer`

---

## When to Use

- Auth, SSO, OIDC, RBAC, or tenant-boundary changes
- Breaking API or contract changes
- Production rollout and rollback decisions
- Significant architectural tradeoffs
- Any case where the framework requires human approval before proceeding

---

## Required Packet

Use this format exactly when escalating for approval.

```text
APPROVAL PACKET

Decision:
- [one-line decision needed]

Why now:
- [why the team cannot proceed safely without this call]

Options:
- Option A: [summary]
  Impact: [short impact]
  Risk: [short risk]
- Option B: [summary]
  Impact: [short impact]
  Risk: [short risk]

Recommendation:
- [recommended option]

Reasoning:
- [2-4 concise points]

Rollback / exit path:
- [how to undo or contain the decision]

Decision deadline:
- [now | before implementation | before deploy | other]
```

---

## Packet Rules

- Do not escalate without a recommendation unless the evidence is genuinely split.
- Keep options concrete and mutually exclusive.
- Include rollback or containment even for documentation or contract decisions.
- If one option is clearly unsafe, say so explicitly rather than pretending the options are equal.

---

## Not For

- Routine low-risk implementation details
- Stylistic code choices with no user or operational impact
- Questions a specialist can resolve through direct verification
