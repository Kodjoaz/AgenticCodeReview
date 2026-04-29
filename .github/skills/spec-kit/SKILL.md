---
name: spec-kit
description: Use official Specify/Spec Kit workflow and canonical artifacts for feature definition and planning.
---

# Spec Kit Skill

Purpose: Use official Spec Kit as a lightweight planning layer under the TIFINIA agent framework.

Owner: ProductManager
Used In: Plan stage (CADO)

> **CADO path mapping**: When Spec Kit is active, the Spec Kit constitution at
> `.specify/memory/constitution.md` serves as the project foundation document.
> CADO specialist agents that reference "project config (`.cado/config.yml`)"
> should also read `.specify/memory/constitution.md` when Spec Kit is the
> active framework, as it contains the canonical project principles and stack
> definition.

---

## Official Source

- Repository: https://github.com/github/spec-kit
- Core CLI: specify
- Core flow: constitution -> specify -> clarify -> plan -> tasks -> implement

---

## Installation

Install Specify CLI from official repository:

```bash
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git@v0.7.3
specify version
```

Initialize in this repository:

```bash
specify init --here --ai copilot --ai-skills
```

---

## Operating Mode (Speckit-lite)

TIFINIA agents stay primary. Speckit is a support layer for specification and task generation.

Issue-first workflow:
- Start from a GitHub issue.
- Use the issue as the source input for Speckit generation.

Risk-based command policy:
- Low risk (small/single-domain):
	- `/speckit.specify`
	- `/speckit.tasks`
- Medium or high risk (cross-domain, auth/security, schema, infra, production-sensitive):
	- `/speckit.specify`
	- `/speckit.clarify`
	- `/speckit.plan`
	- `/speckit.tasks`

Only use `/speckit.implement` when implementation should be driven directly by Speckit and canonical artifacts are complete.

---

## Canonical Artifact Locations

Spec Kit stores artifacts in these paths:

- `.specify/memory/constitution.md`
- `specs/<NNN-feature-slug>/spec.md`
- `specs/<NNN-feature-slug>/plan.md`
- `specs/<NNN-feature-slug>/tasks.md`
- `specs/<NNN-feature-slug>/research.md`
- `specs/<NNN-feature-slug>/data-model.md`
- `specs/<NNN-feature-slug>/quickstart.md`
- `specs/<NNN-feature-slug>/contracts/`

Do not use `docs/2-PLANNED/specs/` for new Spec Kit artifacts.

---

## ProductManager Gate

Before handing off to Maximus for implementation:

- [ ] Constitution exists in `.specify/memory/constitution.md`
- [ ] `spec.md` exists for current feature folder
- [ ] `plan.md` exists for medium/high-risk features
- [ ] `tasks.md` exists for current feature folder
- [ ] Clarifications completed or explicitly skipped
- [ ] Acceptance criteria in `spec.md` are testable
- [ ] Dependencies and ownership are explicit in spec artifacts

Fallback rule:
- If Speckit commands are unavailable, run the official Specify CLI and continue using the same canonical artifact paths.

If any item is missing, stay in Define and iterate.

---

## Notes

- Keep existing TifinIA agents and orchestration.
- Keep Spec Kit integration lightweight; do not require full flow for every task.
- Replace only the specification engine and artifact structure with official Spec Kit conventions.
