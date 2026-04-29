---
name: github-ops
description: Handle GitHub issue triage, PR workflows, labels, and status checks consistently.
---

# GitHub Operations Skill

**For agents managing GitHub repositories, issues, and pull requests**: Standardized workflows for issue triage, PR review, label management, and GitHub automation.

## When to Use

- Triaging incoming issues (classification, label assignment, milestone)
- Creating or updating pull requests with context
- Searching for related issues or PRs to avoid duplicates
- Automating GitHub workflows (branch protection, status checks)
- Managing issue lifecycle (open -> in-progress -> done)
- Collecting feedback and notifications

## Capabilities

### Issue Management
- **Fetch issue details**: Get full issue context (title, description, assignee, labels, milestone)
- **Search issues**: Find related issues using GitHub search syntax (label, assignee, state, created date)
- **Label management**: List available labels and assign/remove from issues
- **Milestone tracking**: Organize issues into release milestones
- **Triage workflows**: Classify bugs vs features vs docs; assign priority; route to owners

### Pull Request Operations
- **Fetch PR details**: Get PR title, description, files changed, CI status
- **PR search**: Find related PRs (open/merged/draft)
- **Review threads**: Resolve review comments; post suggestions
- **Create PR**: Open PR with description, linked issue, draft status
- **Status checks**: Monitor CI/CD checks; enforce branch protection

### Notifications & Feedback
- **Fetch notifications**: Get current GitHub notifications (mentions, reviews, assignments)
- **Comment on issues**: Post status updates, replies, decision summaries
- **Resolve threads**: Mark discussions as resolved when addressed

## Workflow Patterns

### Triage Pattern
1. Fetch incoming issue with `issue_fetch`
2. Search for duplicates with `doSearch`
3. Assign labels based on type/priority with `labels_fetch` reference
4. Assign to appropriate owner (use ProductManager or agent routing)
5. Set milestone if planned
6. Close duplicate / link related issues

### PR Review Pattern
1. Check PR status with `pullRequestStatusChecks`
2. Fetch PR diff and description
3. Identify review comments with `activePullRequest`
4. Resolve threads as PR author fixes issues with `resolveReviewThread`
5. Approve or request changes

### Issue Lifecycle Pattern
```
Open (triage) -> In-Progress (assign) -> Review (PR link) -> Done (close)
```

## Available Labels (Repository Convention)

Common labels in use:
- **type/bug**: Broken feature
- **type/feature**: New capability
- **type/docs**: Documentation only
- **type/chore**: Internal cleanup
- **priority/p0**: Blocker (fix immediately)
- **priority/p1**: High (this sprint)
- **priority/p2**: Medium (next sprint)
- **domain/backend**: Backend changes
- **domain/frontend**: UI changes
- **domain/database**: Schema/migration
- **domain/infra**: Deployment/DevOps
- **status/blocked**: Waiting for external input
- **status/needs-review**: Ready for review
- **status/in-progress**: Actively being worked

## Integration with conventional-commits

When creating PRs:
- **Bug fixes**: PR title `fix: <description>`; label `type/bug`
- **Features**: PR title `feat: <description>`; label `type/feature`
- **Docs**: PR title `docs: <description>`; label `type/docs`
- **Internal**: PR title `refactor: <description>` or `chore: <description>`

See `conventional-commits` skill for full convention.

## Best Practices

- **Search before creating**: Always check for duplicates via `doSearch`
- **Link related issues**: Use "Fixes #123" syntax in PR description for auto-close
- **Update before closing**: Post decision summary (accepted/rejected/deferred) before closing
- **Use labels consistently**: Enables filtering and automation
- **Include context in PR description**: "Why is this change needed?" + links to related issues

## Common GitHub Search Queries

```
# Find all open bugs assigned to BackendEngineer
is:open label:type/bug assignee:@BackendEngineer

# Find all PRs from this week
is:pr created:>2026-04-10

# Find all blocking issues
is:open label:status/blocked

# Find related issues for a feature
is:issue label:domain/frontend sort:updated-desc
```
