---
name: research-handoff
description: Minimal-context research and handoff format for codebase discovery, external docs lookup, and ranked context packets.
---

# Research Handoff Skill

Purpose: strengthen ContextScout-style discovery with a repeatable handoff packet instead of ad hoc file dumps.

Primary owners:
- `ContextScout`
- `Maximus`

---

## Goal

Return the smallest context set that lets the next agent act confidently.

Default target:
- 2 to 5 files
- 3 to 7 bullet findings
- 1 short unresolved-risk note if needed

---

## Discovery Order

1. Canonical Spec Kit artifacts first
2. Repo-local agent and skill instructions next
3. Source/config files that are likely to change
4. External official docs only if local context is insufficient

Do not front-load large historical docs if the next agent only needs the active contract.

---

## Handoff Packet Format

Return context in this format:

```text
RESEARCH HANDOFF

Task:
- [one-line objective]

Selected context:
- [path] — [why it matters] — [priority]

Key findings:
- [finding]

Open risks:
- [none | short description]

Skipped on purpose:
- [what was not loaded and why]
```

Priority values:
- `critical`
- `high`
- `reference`

---

## Ranking Rules

- Prefer files the next agent will edit over broad background documents
- Prefer canonical requirements over inferred behavior
- Prefer local contracts over external tutorials
- If ambiguity remains high after 5 files, ask for clarification rather than keep loading context
