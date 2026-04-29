---
name: routing-matrix
version: "1.0"
description: >
  Pre-built decision table for Maximus. Maps request patterns -> agent -> risk mode ->
  model_category. Enables O(1) routing without re-reasoning every request.
  Forward-compatible: model_category fields activate automatically when
  Copilot exposes multi-model routing.
---

## Purpose

Eliminate routing deliberation. Every request type has a known owner, known risk floor,
and known model category. Maximus reads this table, matches the request, and dispatches.
No re-reasoning. No guessing.

---

## Primary Routing Table

| Request Type | Keywords / Signals | Primary Agent | Fallback | Risk Floor | Model Category |
|---|---|---|---|---|---|
| API / service / endpoint | route, endpoint, service, fastapi, worker, job, celery, auth, RBAC, middleware | BackendEngineer | SolutionArchitect | MEDIUM | deep |
| React UI / component | component, page, hook, state, form, tailwind, router, UX, modal, table | FrontendEngineer | - | LOW | visual |
| Database / migration | migration, alembic, schema, column, table, index, FK, seed, tenant | DataEngineer | BackendEngineer | MEDIUM | deep |
| LLM / RAG / AI model | LiteLLM, embedding, RAG, retrieval, vector, model config, prompt, pipeline, docling | AIEngineer | - | MEDIUM | deep |
| Docker / K8s / CI/CD | docker, compose, kubernetes, k8s, helm, github actions, workflow, secrets, infra | PlatformEngineer | - | MEDIUM | deep |
| Security / auth / secrets | auth, OIDC, SSO, RBAC, secret, CVE, bandit, audit, permission, token, JWT | SecurityEngineer | - | HIGH | ultrabrain |
| Architecture / design | ADR, design, architecture, trade-off, decision, cross-domain, scalability, integration | SolutionArchitect | - | MEDIUM | ultrabrain |
| Quality / testing / release | test, coverage, pytest, CI gate, release, regression, QA, quality | QualityEngineer | - | LOW | deep |
| Product / roadmap / docs | feature, roadmap, issue, story, backlog, docs, changelog, prioritize | ProductManager | - | LOW | quick |
| Context discovery | find, search, where is, what file, discover, locate, which module | ContextScout | - | LOW | quick |
| Cross-domain (3+ domains) | (multiple signals above) | TaskManager -> Maximus | - | HIGH | orchestrator |

---

## Skill Invocation Matrix

Maximus must invoke these skills automatically when the listed signals are present.
Do not wait for the specialist to request them — apply the skill before or during delegation.

| Signal in request or context | Invoke skill | Owner | When |
|---|---|---|---|
| No `specs/<NNN>/spec.md` exists, request is a new feature or capability | `requirements-elicitation` | `ContextScout` → `ProductManager` | Before spec creation |
| Auth, SSO, OIDC, token, tenant boundary, proxy, privileged flow, external exposure | `threat-model` | `SecurityEngineer` | Before design approval |
| Auth, proxy, network, secret handling, or rollout posture change | `security-planning` | `SecurityEngineer` | Before implementation |
| New feature, API/contract change, migration, auth flow, state-heavy UI | `test-strategy` | `QualityEngineer` | During plan stage |
| Production deployment, phased rollout, migration coupling, multi-tenant impact | `rollout-planner` | `PlatformEngineer` | Before deployment |
| Work is complete and claims are ready | `delivery-validation` | `Maximus` | Before closing |
| Architecture, cross-domain design, ADR-worthy decision | `solution-design-doc` / `adr-generator` | `SolutionArchitect` | Before implementation |
| Risky or ambiguous decision needs human approval | `approval-packet` | `Maximus` | Before execution |
| Request risk is not obviously LOW | `change-risk-classifier` | `Maximus` | At intent gate |
| Specialist is blocked | `blocker-escalation` | `Maximus` | At loop gate |
| Complex work needs efficient context hand-off | `research-handoff` | `ContextScout` | Before delegation |
| Alembic migration is destructive, transformative, tenant-scoped, or zero-downtime | `migration-strategy` | `DataEngineer` | Before writing migration |
| Model swap, RAG/pipeline change, embedding upgrade, or KB rebuild | `ai-eval-planning` | `AIEngineer` | During plan stage |
| Production or staging incident, alert firing, service degradation | `incident-response` | `PlatformEngineer` | Immediately on signal |
| New service, endpoint, pipeline, or significant behavior change shipping | `observability-design` | `PlatformEngineer` | Before marking DONE |
| New service shipping for first time or missing runbook action item | `runbook-generator` | `PlatformEngineer` | Before production-ready |
| New model, embedding, or high-throughput service being planned | `capacity-planner` | `PlatformEngineer` / `AIEngineer` | During plan stage |
| Input is large doc, multi-source, or postmortem needing fact extraction | `knowledge-extraction` | `ProductManager` / `ContextScout` | Before spec creation or discovery |
| Completed or blocked run has a durable lesson | `post-run-learning` | `Maximus` | After closure |

---

## Fast-Path Patterns (Immediate Dispatch, No Gate Needed)

These are LOW risk, single-domain, single-file scope. Dispatch directly without
waiting for explicit user confirmation.

```
- Add a test for <existing function>  -> QualityEngineer
- Update docstring / comment          -> ProductManager (docs) or BackendEngineer
- Rename a variable / file            -> owning domain agent
- Fix a typo in config value          -> PlatformEngineer or BackendEngineer
- Add a React prop or UI label        -> FrontendEngineer
```

---

## Parallel Execution Map

These agent pairs have no write-domain overlap. Safe to run concurrently.

| Lane A | Lane B | Condition |
|---|---|---|
| BackendEngineer | FrontendEngineer | API contract defined upfront by SolutionArchitect |
| DataEngineer | AIEngineer | Migration doesn't touch vector schema AND AI task doesn't touch DB |
| QualityEngineer | ProductManager | QA writing tests while PM updates docs/roadmap |
| PlatformEngineer | QualityEngineer | Infra changes are config-only (no app code) |
| SecurityEngineer | QualityEngineer | Security review while QA runs coverage checks |

**Never parallelize:**
- Two agents writing to the same file path
- DataEngineer migration + BackendEngineer model layer changes (schema contract must land first)
- SecurityEngineer findings + any implementation agent (findings gate implementation)

---

## Hard Escalation Triggers

If ANY of these signals appear in the request or in any specialist's output, stop and
escalate to the appropriate gate before continuing:

| Signal | Action |
|---|---|
| Hardcoded credential, API key, secret in code | -> Immediate stop -> SecurityEngineer BLOCKED verdict |
| `DROP TABLE`, `DELETE FROM` without WHERE | -> Immediate stop -> DataEngineer + user approval |
| Breaking change to a public API endpoint | -> SolutionArchitect contract review first |
| Auth / SSO / OIDC flow change | -> SecurityEngineer must APPROVE before FrontendEngineer or BackendEngineer ships |
| Production deployment path | -> FULL workflow mode, PlatformEngineer + QualityEngineer gate required |
| Cross-tenant data access pattern | -> DataEngineer tenant isolation checklist + SecurityEngineer review |

---

## Model Category Reference

These map to model capability tiers. When Copilot exposes per-agent model routing,
Maximus uses `model_category` from `agents.yml` to select the right model automatically.

| Category | Intended Use | Example Agents |
|---|---|---|
| `orchestrator` | Intent gate, task decomposition, coordination — needs best reasoning + context | Maximus, TaskManager |
| `ultrabrain` | High-stakes decisions, architecture, security verdicts — needs deep logic | SolutionArchitect, SecurityEngineer |
| `deep` | End-to-end implementation — needs balance of capability and context | BackendEngineer, FrontendEngineer, AIEngineer, DataEngineer, PlatformEngineer, QualityEngineer |
| `visual` | UI/UX implementation — may benefit from multimodal model when available | FrontendEngineer |
| `quick` | Search, planning text, lightweight reads — fastest/cheapest model acceptable | ContextScout, ProductManager |

---

## Usage by Maximus

1. On new request: scan keywords against Primary Routing Table → determine Primary Agent.
2. Check for Hard Escalation Triggers before dispatching.
3. If 3+ domains → delegate to TaskManager for decomposition, then dispatch in parallel lanes.
4. Attach `model_category` in delegation packet so future multi-model router can act on it.
5. Do not re-derive routing once table produces a match.
