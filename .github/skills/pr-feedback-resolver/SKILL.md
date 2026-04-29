---
name: pr-feedback-resolver
description: Resolve review feedback by classifying comments, mapping fixes, and tracking addressed versus deferred items.
---

# PR Feedback Resolver Skill

Purpose: standardize how review comments are processed after the first implementation pass so feedback becomes an execution plan, not a loose conversation.

Primary owners:
- `QualityEngineer`
- `BackendEngineer`
- `FrontendEngineer`

---

## When to Use

- Pull request review comments arrive
- Copilot review findings need resolution
- A user asks to address review comments
- A specialist needs to separate must-fix issues from optional suggestions

---

## Resolution Flow

### 1. Classify Each Comment

Use one of:
- `correctness`
- `security`
- `regression-risk`
- `test-gap`
- `maintainability`
- `style`
- `needs-decision`

### 2. Decide Status

Use one of:
- `addressed`
- `planned`
- `deferred`
- `rejected-with-reason`
- `needs-decision`

### 3. Map Exact Fix

For comments that will be addressed, state the exact code or test change required.

---

## Output Format

```text
PR FEEDBACK RESOLUTION

Comment:
- [summary]

Classification:
- [type]

Status:
- [status]

Action:
- [exact code/test/doc action or rationale]

Risk if skipped:
- [none | short description]
```

---

## Rules

- Correctness, security, and regression-risk comments are not optional by default.
- Style-only comments should not displace higher-signal fixes.
- If a comment changes API behavior or scope, escalate with `approval-packet`.
- If a comment is unclear, convert it into a concrete action or mark `needs-decision`.
