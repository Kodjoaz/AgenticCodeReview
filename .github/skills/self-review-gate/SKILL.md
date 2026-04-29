---
name: self-review-gate
description: Mandatory pre-completion checklist mechanics for all specialist agents — run verification, collect evidence, apply one correction pass, then report.
---

# Skill: Self-Review Gate

Use this skill before claiming any task complete. No exceptions.

## 1. Run Verification Commands

Execute the commands listed in your agent's **Verification Commands** section.
Record actual output — pass or fail — for every check.

## 2. Collect Evidence

For each check:
- Actual command run
- Exit code or result summary
- Any warnings worth noting

List every file changed with its line delta.

## 3. Self-Correction Loop

- If one check fails: attempt one targeted fix, re-run that check only.
- If it fails again after one fix: **STOP**. Do not proceed. Report `blocked` to Maximus with the exact error.
- Never claim completion with a failing check.

## 4. Error-Stop Criteria

Stop immediately — do not attempt auto-fix — when:
- A security scanner (bandit, trivy, npm audit) returns HIGH or CRITICAL severity.
- Type checks fail in production code paths (not test-only).
- Tests fail and root cause is unclear after one inspection.
- A schema migration would cause irreversible data loss.
- An API contract change breaks a known consumer without an approved migration plan.

## 5. Completion Report Format

Return this exact structure to Maximus:

```
STATUS: completed | blocked | escalated

EVIDENCE:
  - [check name]: PASS | FAIL — [one-line summary]

CHANGED:
  - [relative file path] (+N added / -N removed)

RISKS:
  [assumptions, known gaps, warnings — or "none"]

BLOCKERS:
  [if any: description + responsible agent — or "none"]
```
