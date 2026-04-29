---
name: blocker-escalation
description: Structured blocker reporting, dependency impact, and escalation path for work that cannot safely continue.
---

# Blocker Escalation Skill

Purpose: turn blocked work into a precise escalation packet so the next decision is obvious and parallel work is not needlessly stalled.

Primary owners:
- `Maximus`
- `TaskManager`
- `ProductManager`

---

## When to Use

- A specialist cannot proceed after one reasonable fix attempt
- A dependency is missing or contradictory
- A required artifact, environment, or approval is absent
- A blocker affects sequencing or safe parallel execution

---

## Blocker Packet

```text
BLOCKER

What is blocked:
- [exact task or deliverable]

Why blocked:
- [specific cause]

Owner to unblock:
- [human | agent | team]

Impact:
- [what cannot proceed]

Can continue in parallel:
- [none | list]

Minimum unblock action:
- [smallest action that restores progress]

Evidence:
- [error, missing dependency, conflicting requirement, or failed command]
```

---

## Rules

- Do not say “blocked” without naming an owner to unblock it.
- Separate the blocker itself from surrounding inconvenience.
- Always state whether any safe work can continue in parallel.
- Prefer the minimum unblock action over a broad redesign request.

---

## Escalation Guidance

- Technical missing dependency -> owning specialist or PlatformEngineer
- Missing requirement or conflicting scope -> ProductManager
- Risk or approval boundary -> Maximus -> human using `approval-packet`
- Security-sensitive uncertainty -> SecurityEngineer before further implementation
