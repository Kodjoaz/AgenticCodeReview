# [PROJECT_NAME] — Project Constitution

> This constitution governs how [PROJECT_NAME] uses CADO Framework to deliver software.
> All agents and contributors are bound by these principles.
> Ratification requires agreement from the core team.

---

## Core Principles

<!--
Define the non-negotiable values that guide all delivery decisions.
Replace placeholders with project-specific principles.
Typical examples are listed; customise or remove as appropriate.
-->

1. **[PRINCIPLE_1]** — [One sentence describing this principle and why it matters here.]
2. **[PRINCIPLE_2]** — [One sentence.]
3. **Safety over speed** — No change ships without evidence. Gate skips are never acceptable under time pressure.
4. **Least-blast-radius** — Prefer incremental, reversible changes over large-batch deployments.
5. **Specialist ownership** — Each domain (backend, frontend, data, security, platform) is owned by one specialist. Cross-domain changes require both specialists to sign off at gate.

---

## Delivery Standards

### Stage Compliance
- Every delivery run must follow the Intake → Plan → Gate → Build → Prove → Ship sequence.
- No stage may be skipped unless the Conductor documents the waiver with a risk justification.
- The Gate stage is mandatory for all Medium and High risk changes.

### Branch and Commit Standards
- Feature branches follow the pattern: `[TYPE]/[SHORT_DESCRIPTION]` (e.g., `feat/add-auth-endpoint`).
- Commits follow Conventional Commits: `type(scope): message`.
- No force-pushes to `main` or `release/*` branches.

### Rollback Readiness
- Every shipped change must include a documented rollback procedure.
- Rollback procedures are verified at the Prove stage before Ship proceeds.

---

## Quality Gates

### Low Risk
- Self-review by the author.
- All automated tests pass.
- No new lint errors introduced.

### Medium Risk
- Peer review by one designated reviewer.
- Integration tests pass.
- Rollback procedure documented.

### High Risk
- Review by two reviewers including a domain lead.
- Full regression suite passes.
- Observability impact reviewed (metrics, alerts, dashboards updated if needed).
- SecurityEngineer sign-off if auth or secrets are involved.
- Rollback rehearsed or documented step-by-step.

---

## Governance

### Decision Authority
- **Conductor** — Final authority on stage progression and gate outcomes within a run.
- **Domain Lead** — Final authority on approach decisions within their domain.
- **[TEAM_LEAD / TECH_LEAD]** — Final authority on constitution amendments.

### Amendments
- Any amendment requires ratification by [NUMBER] core team members.
- Amendments are recorded in the CHANGELOG with a `constitution:` prefix.
- The constitution version is bumped on each amendment.

### Escalation
- If the Conductor and a specialist disagree on a gate decision, escalate to [TEAM_LEAD].
- Security findings always escalate to SecurityEngineer before any gate can close.

---

**Version**: [VERSION] | **Ratified**: [DATE] | **Last Amended**: [DATE]

