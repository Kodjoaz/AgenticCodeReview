---
name: knowledge-extraction
description: Extract structured, agent-consumable knowledge from unstructured sources — large docs, conversations, postmortems, specs, PR histories — for spec creation, context discovery, and framework improvement.
---

# Knowledge Extraction Skill

Purpose: turn noisy or large unstructured input into clean, structured facts that coding
agents can act on. This is an agent-framework skill — it improves how agents consume inputs,
not a product feature.

Primary owners:
- `ProductManager` — extracting requirements, constraints, and product intent from messy inputs before spec creation
- `ContextScout` — distilling large or multi-file sources into a minimal ranked fact set for a downstream agent

---

## When to Use

**ProductManager invokes this when:**
- The input is a large document, user interview transcript, or design brief instead of a clean feature request
- Multiple sources (emails, PRs, Slack threads, old specs) need to be synthesized into a coherent set of requirements
- A completed run needs its decisions and lessons extracted back into a spec or framework artifact
- A postmortem has action items and root cause facts that should update a spec or runbook

**ContextScout invokes this when:**
- The source material is too large or too diffuse to produce a clean 2–5 file context packet directly
- Multiple docs overlap and need fact-level consolidation rather than file-level selection
- The downstream agent needs structured facts, not raw file content

---

## Extraction Pass

### 1. Source Classification

Classify the input:

- `requirements-source` — feature request, user story, interview, PRD, brief
- `decision-record` — architecture discussion, PR review, meeting notes, ADR draft
- `incident-record` — postmortem, incident timeline, support ticket
- `codebase-artifact` — large spec, multiple related files, implementation history

Classification determines which extraction lenses to apply.

### 2. Extraction Lenses

Apply only the lenses relevant to the source type.

**For `requirements-source`:**
- What outcome is being requested? (testable statement)
- Who is the consumer or user?
- What are the explicit constraints?
- What is explicitly out of scope?
- What open questions remain?

**For `decision-record`:**
- What was decided?
- What options were considered?
- What was the deciding factor?
- What risks or follow-ups were noted?

**For `incident-record`:**
- What failed and why?
- What was the impact?
- What fixed it?
- What action items remain unresolved?

**For `codebase-artifact`:**
- What are the key contracts or interfaces?
- What assumptions does the code make?
- What is intentionally not covered?
- What would break if changed?

### 3. Deduplication and Ranking

After extraction:
- remove duplicate facts
- rank by relevance to the downstream task
- flag contradictions explicitly rather than silently resolving them
- surface open questions that block a clean handoff

---

## Extraction Output

```text
KNOWLEDGE EXTRACTION

Source:
- [type and brief description]

Extracted facts:
- [fact] — [source reference] — [priority: critical | high | reference]

Open questions:
- [question]

Contradictions detected:
- [none | contradiction]

Routing recommendation:
- READY — facts sufficient for downstream use
- NEEDS CLARIFICATION — open questions must resolve first
- CONFLICTED — contradictions require human decision before use
```

---

## Guardrails

- Do not invent facts not present in the source material.
- Flag contradictions explicitly — never silently pick one side.
- Keep the output minimal: 5–15 facts is better than an exhaustive dump.
- If the source is too ambiguous to extract reliable facts, return `NEEDS CLARIFICATION` with specific questions rather than a low-confidence fact list.
