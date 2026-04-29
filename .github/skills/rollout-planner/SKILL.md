---
name: rollout-planner
description: Structured rollout, rollback, and blast-radius control planning for platform, infra, and multi-tenant feature delivery.
---

# Rollout Planner Skill

Primary owners:
- `PlatformEngineer`
- `ProductManager`
- `SecurityEngineer` for sensitive rollout posture changes

---

## When to Use

- Production deployment with non-trivial blast radius
- Auth, proxy, network, tenancy, or migration-related releases
- Feature-flagged or phased delivery
- Infra or config changes that affect multiple services or tenants
- Any change that requires explicit rollback criteria

---

## Rollout Planning Pass

### 1. Rollout Unit

Define the smallest safe unit of release:
- environment
- tenant
- service
- percentage of traffic
- feature flag cohort

### 2. Preconditions

List what must be true before rollout starts:
- deploy artifacts built
- configs validated
- data migrations applied or sequenced
- alerts and dashboards ready
- runbook owner identified

### 3. Stages

Define stages from safest to broadest, for example:
- internal only
- single tenant / canary
- limited percentage
- full rollout

Each stage must include success signals and time-to-observe.

### 4. Rollback Triggers

Define exact rollback conditions:
- error threshold
- latency threshold
- auth failure pattern
- cross-tenant leak signal
- failed migration checkpoint

### 5. Communication and Ownership

State:
- who executes the rollout
- who monitors it
- who approves progression to the next stage
- who owns rollback if triggers fire

---

## Rollout Plan Output

```text
ROLLOUT PLAN

Change:
- [one-line description]

Rollout unit:
- [unit]

Preconditions:
- [condition]

Stages:
1. [stage] — [success signal] — [observation window]
2. [stage] — [success signal] — [observation window]

Rollback triggers:
- [trigger]

Rollback action:
- [exact rollback or revert step]

Owners:
- Executor: [owner]
- Monitor: [owner]
- Approval: [owner]
- Rollback: [owner]

Recommendation:
- READY FOR ROLLOUT
- CONDITIONAL — missing precondition or signal
- BLOCKED — unsafe rollout posture
```

---

## Guardrails

- Do not approve full-rollout-only plans for high-blast-radius changes.
- If rollback is undefined or not reversible enough for the change class, return `BLOCKED`.
- Prefer explicit canary or tenant-phased rollout when auth, tenancy, or migrations are involved.
