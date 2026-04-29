---
name: pr-review-summary
description: Summarize pull request intent, change scope, reviewer feedback, and decision-ready next actions.
---

# Skill: PR Review Summary

## Purpose
Create a concise, decision-focused summary of PR changes and review outcomes for faster orchestration and approvals.

## Owner
- `Maximus`
- `ProductManager`

## Used In
- `plan` stage to evaluate readiness of in-flight work
- `gate` stage for approval context
- `prove` stage when resolving review threads

## Inputs
- PR title, description, and linked issue/spec
- Changed file list grouped by domain
- Review comments and thread status
- CI/check status and failing signals
- Requested reviewers and required approvals

## Procedure
- Capture PR objective and user-facing value in one short statement.
- Group changed files by backend, frontend, data, platform, docs, and tests.
- Identify unresolved review threads and classify them by severity.
- Summarize check status with explicit blockers vs informational warnings.
- Note acceptance criteria coverage and any missing validation evidence.
- Recommend next action: merge, conditional merge, or hold.

## Output Contract
Expected output headings:
- `PR REVIEW SUMMARY`
- `Objective`
- `Change Scope`
- `Review Feedback`
- `Checks and Evidence`
- `Risks`
- `Recommendation`
- `Required Follow-ups`

## Validation
- Summary reflects current PR state, not stale check results.
- Every unresolved blocking comment is listed.
- Recommendation is consistent with check status and required approvals.
- Follow-ups include clear owner and completion condition.