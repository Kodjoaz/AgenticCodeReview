---
name: execution-log
description: Record execution outcomes and provide proof-of-completion artifacts.
---

# Skill: Execution Log & Proof of Completion

Before claiming a task done:

## 1. Save execution log to `.github/logs/YYYY-MM-DD-HH-MM-taskname.json`
- Final outcome: `completed` / `escalated` / `blocked`
- All artifacts created or modified
- Delegations made and specialist outcomes
- Follow-up tasks if any

## 2. Provide completion proof
- [x] Validation results (test output, lint results, build output)
- Documentation: Changed files with line counts:
  ```
  src/example/file.py (+87/-12)
  tests/test_example.py (+45/-0)
  ```
- Rollback command (if HIGH-RISK): `git revert [commit-hash]`
- Follow-up tasks (if any)

## 3. Confirm with user
- For HIGH-RISK changes: wait for explicit user approval before final commit
- For LOW/MEDIUM: summarize what was done and surface any follow-ups
