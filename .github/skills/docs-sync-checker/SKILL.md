---
name: docs-sync-checker
description: Verify that documentation and release notes stay synchronized with implemented behavior changes.
---

# Skill: Docs Sync Checker

## Purpose
Prevent drift between implemented behavior and written guidance by enforcing a lightweight documentation consistency pass.

## Owner
- `ProductManager`
- `QualityEngineer`

## Used In
- `prove` stage before completion report
- release note preparation
- post-merge documentation audits

## Inputs
- Changed feature scope and acceptance criteria
- Updated code paths and config surfaces
- Relevant docs pages, specs, and runbooks
- Changelog entries tied to the change
- Known limitations or deferred behavior

## Procedure
- List user-visible behavior changes and operational changes in scope.
- Locate corresponding docs sections expected to reflect those changes.
- Compare implementation terms with docs terminology for mismatch.
- Check changelog and guide entries for missing or stale statements.
- Mark each doc item as aligned, needs update, or out of scope.
- Produce minimal edit recommendations with file-level ownership.

## Output Contract
Expected output headings:
- `DOCS SYNC CHECK`
- `Implementation Delta`
- `Docs Coverage Matrix`
- `Mismatches`
- `Required Updates`
- `Deferred Documentation`
- `Readiness Verdict`

## Validation
- Every user-visible change is mapped to docs status.
- Required updates reference concrete files and owners.
- Deferred items include rationale and target phase.
- Verdict is `ready` only when no blocking mismatches remain.
