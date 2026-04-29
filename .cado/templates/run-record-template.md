# CADO Framework Run Record

> One file per delivery run. Populated by the Conductor as the run progresses.
> Filename convention: `run-record-YYYYMMDD-NNN.md`

---

## Header

| Field | Value |
|-------|-------|
| **Run ID** | `cado-run-{{YYYYMMDD}}-{{NNN}}` |
| **Stage** | `<!-- intake / plan / gate / build / prove / ship -->` |
| **Risk Tier** | `<!-- low / medium / high -->` |
| **Started** | `<!-- ISO timestamp -->` |
| **Completed** | `<!-- ISO timestamp or IN PROGRESS -->` |
| **Conductor** | `<!-- agent or person name -->` |

---

## Intent

<!--
One paragraph describing the purpose of this delivery run.
What problem is being solved? What outcome is expected?
-->

---

## Plan Summary

<!--
Brief description of the approach agreed during the Plan stage.
Include: which specialists are delegated, key technical decisions, and any constraints honored.
-->

### Approach

### Key Decisions

### Constraints Honored

---

## Delegations

| Specialist | Domain | Task | Status |
|-----------|--------|------|--------|
| <!-- BackendEngineer --> | <!-- Backend --> | <!-- description --> | <!-- pending / in-progress / done / blocked --> |
| <!-- FrontendEngineer --> | <!-- Frontend --> | <!-- description --> | <!-- pending / in-progress / done / blocked --> |

---

## Approvals

| Stage | Approver | Approved At | Notes |
|-------|----------|-------------|-------|
| Gate | <!-- name --> | <!-- timestamp --> | |
| Ship | <!-- name --> | <!-- timestamp --> | |

---

## Evidence

| Type | Description | Link / Artifact |
|------|-------------|-----------------|
| Tests | <!-- e.g., "47 tests passed, 0 failed" --> | <!-- CI link or inline --> |
| Coverage | <!-- e.g., "82% line coverage" --> | <!-- link --> |
| Build Output | <!-- e.g., "Docker image built: sha256:abc123" --> | <!-- link --> |
| Rollback Plan | <!-- e.g., "alembic downgrade -1" --> | <!-- inline or link --> |
| Security Scan | <!-- e.g., "bandit: 0 high severity" --> | <!-- link --> |

---

## Risks Identified

<!--
List risks discovered during the run. Note how each was mitigated or accepted.
-->

| Risk | Mitigation | Accepted By |
|------|-----------|-------------|
| | | |

---

## Ship Summary

<!--
Completed at the Ship stage. Describe what was delivered, how, and any post-ship actions required.
-->

### What Was Shipped

### How It Was Deployed

### Post-Ship Actions

- [ ] Monitoring confirmed healthy
- [ ] Stakeholders notified
- [ ] Documentation updated
- [ ] Run record filed in `docs/runs/`

---

*CADO Framework version*: 0.2.0

