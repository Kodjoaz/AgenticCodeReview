# CADO Framework Stages

CADO Framework runs work through six stages. Each stage has clear entry criteria, exit criteria, required artifacts, and blockers that prevent unsafe transitions.

## 1. Intake

Entry criteria:
- A request exists.
- The user goal or change need is stated.

Exit criteria:
- Goal, scope, constraints, and acceptance criteria are captured.
- Unknowns and obvious risks are recorded.
- An owner is identified for the next step.

Required artifacts:
- Change request or equivalent intake note
- Initial scope statement
- Acceptance criteria draft

## 2. Plan

Entry criteria:
- Intake artifacts are complete enough to estimate work.
- The Conductor can identify likely specialists.

Exit criteria:
- The work is decomposed into bounded tasks.
- Dependencies, sequence, and likely validation steps are defined.
- Risk tier is proposed.

Required artifacts:
- Plan summary
- Specialist routing list
- Validation approach
- Proposed risk tier

## 3. Gate

Entry criteria:
- A plan exists.
- Risk has been classified.

Exit criteria:
- Required approvals are present.
- Build may proceed within the approved scope.
- Any gate conditions are documented for later verification.

Required artifacts:
- Risk decision
- Approval record if required
- Scope limits or guardrails

## 4. Build

Entry criteria:
- Gate is satisfied or explicitly not required.
- Specialists have a defined task and handoff target.

Exit criteria:
- The scoped implementation is complete.
- Files, config, docs, or scripts changed are listed.
- Any follow-up items are separated from the current slice.

Required artifacts:
- Specialist handoff records
- Change summary
- Changed file list

## 5. Prove

Entry criteria:
- Build outputs exist.
- The validation plan is available.

Exit criteria:
- Required checks have been run.
- Evidence is recorded in the standard format.
- Failures are either fixed or clearly block Ship.

Required artifacts:
- Test or check results
- Build or package results if relevant
- Security, migration, and docs evidence if relevant
- Evidence section or run record

## 6. Ship

Entry criteria:
- Prove is complete.
- No unresolved blocker remains.

Exit criteria:
- The outcome is summarized.
- Handoff details and next actions are captured.
- Rollback notes exist when relevant.

Required artifacts:
- Ship summary
- Handoff record
- Rollback notes when relevant

## Transition Blocking Rules

- Intake cannot move to Plan if scope or acceptance criteria are missing.
- Plan cannot move to Gate if risk has not been classified.
- Gate cannot move to Build if required approval is missing.
- Build cannot move to Prove if the actual change scope differs from the approved scope without re-planning.
- Prove cannot move to Ship if required evidence is missing or failing.
- Ship cannot close the run if the handoff summary or rollback notes are required but absent.

