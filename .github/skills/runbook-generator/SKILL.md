---
name: runbook-generator
description: Generate structured operational runbooks for services before they ship, covering startup, shutdown, common failure scenarios, health checks, and escalation paths.
---

# Runbook Generator Skill

Purpose: close the gap between a deployed service and the knowledge needed to operate it.
Every new service must have a runbook before it is considered production-ready.

Primary owners:
- `PlatformEngineer`

---

## When to Use

- A new service, container, or background worker is being deployed for the first time
- `incident-response` identifies a missing runbook as a postmortem action item
- An existing service has undocumented failure modes that keep recurring
- A significant config or topology change makes the old runbook stale

---

## Runbook Structure

Every generated runbook must include all of the following sections.

### 1. Service Overview

- what the service does
- which port(s) it listens on
- which other services it depends on
- which services depend on it
- config file(s) and environment variables

### 2. Startup and Verification

Step-by-step:
- how to start the service (compose, k8s, direct)
- how to confirm it is healthy
- expected startup log lines
- health endpoint and expected response

### 3. Graceful Shutdown

- how to stop the service without data loss
- drain / connection close behavior
- expected shutdown log lines

### 4. Common Failure Scenarios

For each known or likely failure:

```text
Scenario: [name]
Symptoms: [what you observe]
Cause: [likely root cause]
Fix: [step-by-step resolution]
Escalate if: [condition that means this is beyond self-service]
```

Minimum scenarios to cover:
- service fails to start
- service becomes unresponsive (health endpoint stops responding)
- high error rate in logs
- dependency unavailable (DB, upstream service, external API)

### 5. Health Check Commands

```bash
# All commands must be runnable by on-call without special knowledge
[command] # [what it checks]
```

### 6. Escalation Path

- who owns this service (primary on-call)
- who to escalate to if primary is unavailable
- where the service's Grafana dashboard is
- where the service's Loki logs are

---

## Output Location

Save generated runbooks to:
`docs/3-GUIDES/operations/runbooks/<service-name>.md`

---

## Guardrails

- Do not mark a service DONE or production-ready without a runbook.
- Every failure scenario must have a concrete fix step, not just "investigate logs".
- Escalation path must name a real owner, not a team or role placeholder.
