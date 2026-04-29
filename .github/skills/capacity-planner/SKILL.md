---
name: capacity-planner
description: Pre-ship resource estimation for new services, models, and pipelines — covering CPU, GPU, memory, token budget, vector index size, and load-test acceptance criteria.
---

# Capacity Planner Skill

Purpose: surface resource constraints and scaling limits before a new service or model ships,
not after the first production incident.

Primary owners:
- `PlatformEngineer` — infra sizing, container limits, K8s resource requests
- `AIEngineer` — model token budget, embedding throughput, vector index sizing

---

## When to Use

- New model being added to LiteLLM or vLLM
- New embedding model or dimension change (Qdrant index size impact)
- New RAG pipeline with materially different retrieval load
- New high-throughput API endpoint or background worker
- `ai-eval-planning` returns a capacity gap flagged as unresolved
- Scaling a service to a new tier (single node → multi-node, CPU → GPU)

---

## Capacity Planning Pass

### 1. Resource Profile

For the component being added or changed, estimate:

| Resource | Current | After change | Delta |
|---|---|---|---|
| CPU (cores) | — | — | — |
| Memory (GB) | — | — | — |
| GPU (VRAM GB) | — | — | — |
| Storage (GB) | — | — | — |
| Tokens/request | — | — | — |
| Requests/minute (peak) | — | — | — |

Use actual bench numbers if available; flag as estimated if not.

### 2. Qdrant Index Impact

For embedding or KB changes:

- embedding dimension: old vs new
- expected document count at 30/90/365 days
- estimated index size (dimension × docs × 4 bytes, rounded up)
- estimated query latency at target index size

### 3. Token Budget

For model or prompt changes:

- prompt tokens per request (system + context + user)
- completion tokens per request (expected average)
- total tokens per request
- monthly token cost at current request volume
- monthly token cost at 2× and 5× current volume

### 4. Scale Thresholds

Define the load at which each resource becomes the bottleneck:

- CPU saturation point (requests/minute)
- Memory saturation point
- GPU VRAM saturation point (concurrent model requests)
- Qdrant query latency degradation point (index size)

### 5. Load-Test Acceptance Criteria

Before production release:

- target RPS for load test
- p95 latency must stay below: [value]
- error rate must stay below: [value]%
- no resource saturation during the test window

---

## Capacity Plan Output

```text
CAPACITY PLAN

Component:
- [name and version]

Resource profile:
- CPU:     [current → after] — delta [value]
- Memory:  [current → after] — delta [value]
- GPU:     [current → after] — delta [value]
- Tokens:  [per request] — monthly cost [current → after]

Qdrant impact:
- [none | dimension/size delta]

Scale thresholds:
- CPU saturation:    [RPS]
- Memory saturation: [RPS]
- GPU saturation:    [concurrent requests]

Load-test gate:
- Target RPS: [value]
- p95 latency: < [value] ms
- Error rate:  < [value]%

Recommendation:
- READY — within current resource envelope
- CONDITIONAL — headroom tight, monitor closely
- BLOCKED — exceeds current resource envelope, requires infra provisioning first
```

---

## Guardrails

- If GPU VRAM is insufficient for the new model at target concurrency, return `BLOCKED` before any deployment.
- Token cost delta > 20% requires explicit approval via `approval-packet`.
- Load-test gate is mandatory for any new endpoint expected to handle > 100 RPM.
