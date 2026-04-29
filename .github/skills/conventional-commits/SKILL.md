---
name: conventional-commits
description: Use conventional commit messages and changelog update conventions.
---

# Skill: Conventional Commits & Changelog

## Commit format
Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `perf:`

Examples:
- `feat(api): add GET /models/{id}/metrics endpoint`
- `fix(auth): correct token expiry check in middleware`
- `docs: update RAG pipeline configuration guide`

## Changelog
- Update `CHANGELOG.md` for all user-facing changes (feat, fix, perf)
- Format: `## [version] - YYYY-MM-DD` followed by categorized entries
- Reference GitHub issue numbers where applicable: `(#123)`

## Rules
- Keep commits small and atomic — one logical change per commit
- Reference the issue in the commit body when implementing a tracked feature
- Do not amend or force-push published commits
