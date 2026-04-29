# CADO Framework

CADO Framework is a stage-based delivery framework for AI-assisted engineering work. It exists to keep orchestration predictable: one Maximus orchestrator owns the flow, specialists execute bounded tasks, gates manage risk, and evidence decides when work is actually done.

## Core Concepts

### Maximus

Maximus is the orchestrator. It owns intake, turns requests into a plan, routes work to the right specialists, enforces approval gates, and refuses completion when evidence is missing.

You can keep `conductor` as the canonical role id and still use a repo-specific persona label (for example, Maximus) through `.cado/config.yml`.

Model behavior can also be tuned from `.cado/config.yml` through `runtime_profiles`
(temperature/top_p/max output presets) and bound per role with
`role_runtime_overrides`.

### Specialists

Specialists are domain-focused contributors such as frontend, backend, platform, security, QA, and docs. They work in parallel only when the tasks are independent and the handoff contract is clear.

### Stages

CADO Framework uses six stages: Intake, Plan, Gate, Build, Prove, and Ship. Each stage has entry criteria, exit criteria, required artifacts, and blocking rules.

### Gates

Gates are approval checks driven by risk. Low-risk changes can move quickly. Medium- and high-risk work may require explicit approval before build or ship. The default GitHub approval signal is the `cado-approve` PR label.

### Evidence

Evidence is the definition of done. Tests, lint results, build output, security checks, migration notes, and docs updates are recorded in a consistent format. No evidence means not done.

## Small PR Quickstart

1. Start with `.cado/templates/change-request.md` and describe the goal, scope, and acceptance criteria.
2. Move through the stage model in `.cado/workflow/stages.md`.
3. Use `.cado/workflow/risk-policy.md` to assign a risk tier.
4. If the change needs approval, apply the `cado-approve` PR label before Build starts.
5. Route work using `.cado/workflow/routing.md`.
6. Capture proof with `.cado/workflow/evidence-contract.md` and `.cado/templates/pr-checklist.md`.
7. Close with a ship summary and handoff note using the examples as reference.

## Tool-Agnostic By Design

CADO Framework is intentionally tool-agnostic. It can be used with GitHub Copilot, Cursor, Claude Code, or any other orchestrated agent stack. If a repository includes agent prompts or YAML definitions, treat those as an adapter layer, not as the framework itself.

## Framework Layout

- `workflow/`: stage model, routing rules, risk policy, evidence contract, and policy extension guidance
- `templates/`: reusable intake, PR artifacts, policy templates, and custom spec contract templates
- `schemas/`: minimal JSON contracts for future automation
- `examples/`: reference run records and specialist handoffs
