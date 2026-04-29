# Example Specialist Handoff

```text
SPECIALIST: Platform
STATUS: COMPLETED
SCOPE: Update local deployment health check output without changing command arguments.
CHANGED: scripts/check-deployment-health.ps1, docs/runbooks/local-health-check.md
EVIDENCE: Ran the local health check script and verified expected success and failure output. Manual doc review completed.
RISKS: Medium risk because operator-facing behavior changed. No secrets, destructive operations, or auth changes involved.
BLOCKERS: none
NEXT: QA should record the final evidence section and confirm the `cado-approve` label is still present before Ship.
```

Notes:
- This handoff format is intentionally short and reusable.
- If the specialist is blocked, `STATUS` must be `BLOCKED` and `BLOCKERS` must describe the stop condition.

