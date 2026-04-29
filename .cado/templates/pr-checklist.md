# PR Checklist — CADO Framework

> Complete this checklist before requesting review.
> Every item in the applicable sections must be checked or explicitly waived with a reason.

---

## Risk Tier

- [ ] **Low** — No gate approval required. Self-reviewed.
- [ ] **Medium** — Gate approval required from one designated reviewer.
- [ ] **High** — Gate approval required from two reviewers, including a domain lead.

Assigned risk tier: `<!-- low | medium | high -->`

---

## Stage Completion

- [ ] **Intake** — Change request is complete and matches this PR's scope
- [ ] **Plan** — Plan summary exists (run record or inline) covering approach and rollback
- [ ] **Gate** — Required approvals obtained for the assigned risk tier
- [ ] **Build** — Implementation matches plan; no unplanned scope changes
- [ ] **Prove** — Tests run and passing; coverage meets baseline
- [ ] **Ship** — Deployment/merge steps documented; run record updated

---

## Gate Approvals

| Reviewer | Role | Approved At |
|----------|------|-------------|
| <!-- name --> | <!-- role --> | <!-- timestamp --> |
| <!-- name --> | <!-- role --> | <!-- timestamp --> |

---

## Evidence Checklist

- [ ] Run record (`run-record.md` or inline) attached or linked
- [ ] Test output or CI link provided
- [ ] Affected components listed (services, routes, DB tables, config keys)
- [ ] No new secrets or credentials committed in plaintext
- [ ] Breaking changes documented (or confirmed: none)
- [ ] Observability impact noted (new metrics, log lines, or traces — or confirmed: none)

---

## Rollback Notes

<!--
Describe how to revert this change if it must be rolled back after merge/deploy.
If rollback is not applicable, state why.
-->

Rollback procedure:
```
<!-- e.g., git revert <sha>, or alembic downgrade -1, or toggle feature flag -->
```

Rollback owner: <!-- name or team -->

---

*PR Author*: <!-- @handle -->
*CADO Framework Run ID*: <!-- cado-run-YYYYMMDD-NNN or N/A -->

