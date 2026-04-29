# CADO Framework Routing

The Conductor routes work to specialists based on ownership, coupling, and risk. Routing should reduce ambiguity, not spread responsibility. Each specialist receives a bounded task, a required output, and a handoff target.

## Specialist Routing

Use the smallest specialist set that can safely complete the work.

- Frontend: user interface, client state, accessibility, and browser behavior
- Backend: APIs, services, business logic, integrations, and data contracts
- Platform: infrastructure, deployment, config, runtime operations, CI/CD workflow automation, and observability
- Security: auth, permissions, secret handling, cryptography, abuse controls, and governance policy checks
- QA: test strategy, regression checks, release confidence, and evidence review
- Docs: user docs, runbooks, change notes, and rollout communication

Routing rules:

- Route by ownership, not by who is available.
- Keep each task narrow enough that the output is reviewable.
- Send cross-cutting risk questions to Security before Build when possible.
- Add QA when the change affects release confidence, test strategy, or acceptance verification.
- Add Platform when the change modifies CI/CD execution, automation permissions,
  workflow reliability controls, or GitHub Actions behavior.
- Add Security when delegated agent behavior, policy enforcement, trust
  boundaries, or auditability requirements are in scope.
- Add Docs when behavior, operation, or rollout expectations change.

## Parallel-Safe Lanes

Parallel work is allowed only when tasks are independent and do not write the same artifact.

Safe examples:

- Frontend and Backend can work in parallel after the API contract is fixed.
- Platform and Docs can work in parallel when docs describe an agreed implementation.
- Security can review a settled design while QA prepares the prove checklist.

Do not parallelize:

- Two specialists changing the same file or schema at the same time.
- A migration task in parallel with a dependent model or API change that assumes the new schema already exists.
- Security findings and implementation in the same approval loop when the findings could change scope.
- Proof collection before the implementation target is stable.

## Standard Specialist Handoff Format

Every delegated specialist should return the same concise structure.

```text
SPECIALIST: <name>
STATUS: COMPLETED | BLOCKED
SCOPE: <what was owned>
CHANGED: <files, systems, or none>
EVIDENCE: <checks run and outcomes>
RISKS: <none or concise description>
BLOCKERS: <none or concise description>
NEXT: <recommended next action>
```

## Conductor Expectations

- Keep a visible list of active delegations.
- Merge specialist outputs back into the current stage decision.
- Re-run Gate if Build discovers higher risk than planned.
- Refuse Ship if a required specialist handoff is missing.
