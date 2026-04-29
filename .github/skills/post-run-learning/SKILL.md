---
name: post-run-learning
description: Capture durable lessons from completed or blocked work and decide whether they should become a routing rule, checklist, skill update, or documentation change.
---

# Post-Run Learning Skill

Purpose: convert real execution outcomes into small framework improvements without creating unnecessary process or agent sprawl.

Primary owners:
- `Maximus`
- `ProductManager`
- `QualityEngineer`

---

## When to Use

- A task exposed a repeated failure mode
- A blocker revealed a missing checklist, routing rule, or dependency contract
- A review found a pattern that is likely to recur
- A successful approach should become repeatable framework guidance

---

## Learning Record

```text
POST-RUN LEARNING

Observed pattern:
- [what happened]

Why it matters:
- [why this is likely to recur or waste time]

Keep / change:
- Keep: [what worked]
- Change: [what should change]

Best framework home:
- [routing rule | skill update | checklist | agent instruction | documentation only]

Proposed minimal update:
- [smallest useful change]

Evidence:
- [task result, blocker, review finding, or validation outcome]
```

---

## Decision Rules

- If the pattern affects routing or escalation, update a routing or orchestration skill.
- If the pattern affects repeated execution quality, update a checklist or specialist skill.
- If the pattern is informative but not operationally reusable, document it only.
- Prefer one small durable improvement over a broad framework expansion.

---

## Anti-Bloat Rules

- Do not create a new skill if an existing one can absorb the learning cleanly.
- Do not add rules for one-off incidents unless recurrence is plausible.
- Do not turn team judgment into rigid process unless the failure mode is expensive or dangerous.
