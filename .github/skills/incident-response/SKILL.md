---
name: incident-response
description: Structured incident triage, severity classification, runbook execution, and blameless postmortem planning for platform and service incidents.
---

# Incident Response Skill

Primary owners:
- `PlatformEngineer`
- `SecurityEngineer` for security-class incidents

---

## When to Use

- A service, container, or pipeline is down or degraded in production or staging
- An alert fires in Prometheus, Grafana, or Loki
- An auth, RBAC, or data access anomaly is detected
- A deployment causes unexpected behavior
- A postmortem is needed after an incident closes

---

## Triage Pass

### 1. Severity Classification

Classify immediately on first signal:

| Severity | Criteria | Response SLA |
|---|---|---|
| SEV-1 | Platform-wide outage or data loss risk | Immediate — all hands |
| SEV-2 | Major feature unavailable for all or many users | < 30 min response |
| SEV-3 | Partial degradation, workaround available | < 2 hour response |
| SEV-4 | Minor issue, low user impact | Next business day |

Default to a higher severity until proven otherwise.

### 2. Blast Radius Assessment

Answer immediately:
- which services are affected?
- are multiple tenants impacted?
- is data at risk (loss, corruption, unauthorized exposure)?
- is the issue spreading or stable?

If data exposure or cross-tenant impact is suspected — escalate to SecurityEngineer immediately.

### 3. Immediate Stabilization

Before investigation:
- stop the bleeding (rollback, feature flag, route traffic away, scale up)
- confirm the action actually reduced impact
- document the stabilization step with timestamp

Do not investigate root cause before the system is stable.

### 4. Root Cause Investigation

Work through:
- what changed? (deploys, config pushes, external events, infra changes)
- where are the error signals? (Loki logs, Prometheus metrics, container events)
- what is the exact failure mode? (timeout, OOM, auth failure, migration, config drift)

Use `uv run pytest -k smoke` or service health endpoints to confirm scope.

### 5. Resolution and Verification

- apply fix (rollback, hotfix, config restore, scale action)
- verify service health restored across all affected surfaces
- confirm no residual impact for other tenants or services

---

## Postmortem Template

Run after every SEV-1 or SEV-2 closure, and optionally for SEV-3:

```text
POSTMORTEM

Incident:
- [short title]

Severity: [SEV-1 | SEV-2 | SEV-3 | SEV-4]
Duration: [start time → resolution time]

Timeline:
- [HH:MM] — [event]

Impact:
- Services affected: [list]
- Tenants affected: [all | partial | none]
- Data at risk: [yes — description | no]

Root cause:
- [concise technical explanation]

Stabilization action:
- [action taken and timestamp]

Resolution:
- [permanent fix applied]

What went well:
- [item]

What to improve:
- [item]

Action items:
- [ ] [owner] — [action] — [due date]
```

---

## Runbook Invocation

If a runbook exists for the affected service under `docs/3-GUIDES/operations/runbooks/`,
follow it as the primary response path.

If no runbook exists, create one as part of the resolution action items.

---

## Guardrails

- Never skip severity classification — it gates the response speed and escalation.
- If cross-tenant data access is suspected at any point, escalate to SecurityEngineer before proceeding.
- Document every stabilization action with a timestamp — blameless postmortems require an accurate timeline.
- Postmortem action items must have an owner and a due date, not just a description.
