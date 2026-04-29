---
name: observability-design
description: Pre-ship observability planning for features and services — defining metrics, logs, traces, alerts, and dashboards before implementation is declared done.
---

# Observability Design Skill

Primary owners:
- `PlatformEngineer`
- `AIEngineer` for model/RAG pipeline observability
- `BackendEngineer` for service-level instrumentation

---

## When to Use

- New service, endpoint, or pipeline being shipped for the first time
- Significant behavior change where existing dashboards no longer reflect the system
- Model or RAG pipeline change with new quality or latency characteristics
- Any feature where release readiness depends on knowing it is working correctly in production
- Before marking a feature DONE in `tasks.md`

---

## Observability Design Pass

### 1. Golden Signals

For the feature or service, define values for all four:

- **Latency**: what is the expected p50, p95, p99? What threshold should trigger an alert?
- **Traffic**: what is the expected request rate at steady state and peak?
- **Errors**: what is the acceptable error rate? What constitutes a hard failure?
- **Saturation**: what resource (CPU, memory, GPU, token budget, queue depth) is the constraint?

At least one alert rule must cover each signal that has a defined threshold.

### 2. Required Log Lines

Define the minimum log events that must be emitted:

- request received with tenant/session context
- key decision points (model selected, retrieval result count, cache hit/miss)
- errors with full context (not just status code)
- slow path or fallback taken

Logs must be structured (JSON) and include `tenant_id` for any multi-tenant path.

### 3. Trace Coverage

Define which spans need distributed tracing:

- entry point → downstream service hops
- database query span for slow-path queries
- external LLM/API call span with latency and token count
- RAG retrieval span with result count and latency

Use existing Jaeger/Tempo integration. Do not add new tracing libraries.

### 4. Dashboard Coverage

For each new or changed service:
- confirm an existing Grafana dashboard covers the golden signals
- if not, define the panels required (metric name, visualization type, threshold lines)
- name the dashboard and the responsible owner

### 5. Alert Rules

Define at minimum:
- one latency alert (p95 > threshold for N minutes)
- one error rate alert (rate > threshold for N minutes)
- one saturation alert if the resource constraint is bounded

Alert destination: existing Alertmanager → on-call channel.

---

## Observability Design Output

```text
OBSERVABILITY DESIGN

Feature / service:
- [name]

Golden signals:
- Latency:    p95 target [value] — alert at [threshold] for [N] min
- Traffic:    expected [value] RPS — peak [value]
- Error rate: acceptable [value]% — alert at [threshold]%
- Saturation: resource [name] — alert at [threshold]%

Required log lines:
- [event] — [fields]

Trace spans:
- [span name] — [entry → exit]

Dashboard:
- [existing dashboard name | new panels required]

Alert rules:
- [rule name] — [condition] — [destination]

Gaps:
- [none | signal or coverage missing]

Recommendation:
- READY — observability coverage sufficient for release
- CONDITIONAL — gaps acceptable with follow-up action item
- BLOCKED — no alerting or no logs on a critical path
```

---

## Guardrails

- Do not ship a new service or pipeline without at least one latency alert and one error rate alert.
- Multi-tenant paths must include `tenant_id` in every log line — no exceptions.
- Do not add new observability libraries or agents without PlatformEngineer review.
- If no Grafana dashboard exists for the service, create one before marking DONE.
