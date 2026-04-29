---
name: requirements-elicitation
description: Structured ambiguity detection and clarification pass for incoming requests before spec creation or implementation dispatch.
---

# Requirements Elicitation Skill

Primary owners:
- `ContextScout` (requirements mode)
- `ProductManager` (spec intake)
- `speckit.clarify`

---

## When to Use

Invoke this skill when ContextScout is operating in **requirements mode**, i.e., when:

- No canonical `specs/<NNN-feature-slug>/spec.md` exists yet for the request
- The incoming request is a new feature, capability, or significant behaviour change
- The request description contains vague outcome language (see Ambiguity Lens below)
- Maximus routes the request to ProductManager for spec creation

Do NOT invoke for:
- Implementation discovery when a spec already exists (use `research-handoff` instead)
- Bug fixes against existing behaviour
- Typo or documentation-only changes

---

## Elicitation Pass — Five Lenses

Run lenses in order. Stop at the first lens that produces a blocker question.
Return all open questions at the end, not one at a time.

### Lens 1 — Outcome Testability

Is the stated outcome testable without interpretation?

Flags:
- Vague verbs: *improve*, *handle*, *support*, *manage*, *make better*
- Comparative goals without a baseline: *faster*, *more reliable*, *easier*
- No observable success condition

Required: restate the outcome as `Given / When / Then` or flag it as untestable.

### Lens 2 — Scope Boundary

Is the boundary of the work explicit?

Flags:
- Non-goals not stated
- Ambiguous "also" or "and" items that could expand scope silently
- Dependency on a feature that is not yet implemented or spec'd

Required: confirm what is explicitly **out of scope**. If absent, generate a candidate
non-goals list for the requester to approve or correct.

### Lens 3 — Stakeholder and Consumer

Who consumes this output and what breaks for them if wrong?

Flags:
- No named consumer (user role, agent, service, tenant)
- Missing failure mode — what a wrong or partial result looks like
- Cross-tenant or multi-role impact not acknowledged

Required: name at least one consumer and one unacceptable failure mode.

### Lens 4 — Acceptance Criteria Gap

Can each stated criterion produce a passing/failing test?

Flags:
- Acceptance criteria are behavioural assertions, not measurable conditions
- Criteria reference internal implementation rather than observable output
- Any criterion is duplicated or contradicts another

Required: each criterion maps to one test scenario. Flag any that cannot.

### Lens 5 — Dependency Drift

Does this request assume anything not yet confirmed as available?

Flags:
- References a spec, service, or config that does not yet exist in `specs/` or `config/`
- Relies on a third-party API or model capability not yet validated in the platform
- Assumes data schema or migration that has not landed

Required: list any unconfirmed dependency and its current status.

---

## Clarification Packet Format

Return findings in this format:

```text
REQUIREMENTS ELICITATION

Request summary:
- [one-line restatement of what was asked]

Lens results:
- Outcome testability:  [PASS | NEEDS CLARIFICATION — reason]
- Scope boundary:       [PASS | NEEDS CLARIFICATION — reason]
- Stakeholder:          [PASS | NEEDS CLARIFICATION — reason]
- Acceptance criteria:  [PASS | NEEDS CLARIFICATION — reason]
- Dependency drift:     [PASS | NEEDS CLARIFICATION — reason]

Open questions (max 5, ranked by blocking impact):
1. [Question — which lens triggered it]
2. ...

Suggested non-goals (for requester to confirm or reject):
- [item]

Unconfirmed dependencies:
- [dependency — current status]

Routing recommendation:
- READY FOR SPEC — route to ProductManager / speckit.specify
- BLOCKED — return questions to requester before spec work begins
```

---

## Routing After Elicitation

| Outcome | Next step |
|---|---|
| All lenses PASS | Route to ProductManager to run `speckit.specify` |
| 1–2 NEEDS CLARIFICATION | Surface questions; continue if requester can answer inline |
| 3+ NEEDS CLARIFICATION | BLOCKED — do not route to spec creation until questions are resolved |
| Dependency drift detected | BLOCKED — flag to Maximus; route dependency gap to the owning specialist first |
