# Change Request

> Use this template to submit a change request through the CADO Framework intake stage.
> Complete all sections before routing to the Conductor.

---

## Goal

<!--
State the primary objective in one or two sentences.
What outcome do you need to achieve? Be specific about the end state.
-->

## Scope

<!--
List the systems, services, APIs, configurations, or components in scope.
- Component A
- Component B
-->

## Non-Goals

<!--
Explicitly state what is OUT of scope to prevent scope creep.
- Not changing X
- Not modifying Y
-->

## Constraints

<!--
List hard technical, business, or time constraints.
- Must be backward-compatible with API v2
- No downtime window available before 2026-06-01
- Must pass SOC2 audit requirements
-->

## Acceptance Criteria

<!--
List concrete, verifiable conditions that define "done".
Each criterion should be testable.
- [ ] All existing integration tests pass
- [ ] New endpoint returns 200 with correct schema
- [ ] Deployment completes with zero downtime
-->

## Risk Notes

<!--
Describe any known risks or unknowns.
Conductor uses this to assign an initial risk tier (low / medium / high).
- Risk: database migration may lock table for N seconds
- Unknown: third-party API rate limit behavior under load
-->

## Rollout / Rollback Needs

<!--
Describe any special rollout considerations or rollback procedures required.
- Requires feature flag: FEATURE_XYZ
- Rollback: revert migration with: alembic downgrade -1
-->

---

*Submitted by*: [NAME]
*Date*: [DATE]
*Priority*: [low | medium | high | critical]

