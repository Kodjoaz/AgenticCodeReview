# Example CADO Framework Run

## Context

A small PR updates a deployment health check script and its operator notes.

## Stage: Intake

Intent: tighten a local health check so failed endpoints are reported with clearer output.

Artifacts:
- Change request completed
- Scope limited to one script and one doc update
- Acceptance criteria defined for success and failure messages

## Stage: Plan

Plan summary:
- Platform updates the script
- Docs updates the operator note
- QA verifies the script output on a local run
- Risk tier is Medium because operator behavior changes, but no production credentials or destructive actions are involved

Delegations:
- Platform: adjust the script output and keep the command contract stable
- Docs: update the operator note for the new output
- QA: run the prove checks and capture evidence

## Stage: Gate

Decision:
- Medium risk
- Team chooses to use the `cado-approve` label for a visible approval gate
- Build starts only after the label is present

## Stage: Build

Outcome:
- Script output updated
- Operator note updated
- No extra scope added

## Stage: Prove

## Evidence

- Category: Tests
  Tool: PowerShell
  Command: ./scripts/check-deployment-health.ps1
  Result: pass
  Link: https://example.org/runs/123/tests
  Notes: Expected success and failure messages were printed.

- Category: Docs
  Tool: Manual review
  Command: operator note update review
  Result: pass
  Link: https://example.org/runs/123/docs
  Notes: Operator instructions match the new script output.

## Stage: Ship

Ship summary:
- Small PR completed through all six stages.
- `cado-approve` was used as the approval gate.
- Evidence was recorded before the run closed.
- Rollback is to restore the prior script message text and operator note.

