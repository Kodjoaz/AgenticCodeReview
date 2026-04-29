---
name: release-notes-generator
description: Generate concise release notes grouped by features, fixes, risks, and operational actions.
---

# Skill: Release Notes Generator

## Purpose
Improve release communication quality by producing structured, accurate notes tied to shipped changes and risks.

## Owner
- `ProductManager`
- `QualityEngineer`

## Used In
- ship stage summaries
- tagged release workflows
- stakeholder and customer updates

## Inputs
- Merged PR list and titles since previous release
- Linked issues/spec identifiers
- Notable behavior changes and migrations
- Known risks, mitigations, and follow-up work
- Contributor and dependency update summaries

## Procedure
- Collect merged work between version boundaries.
- Group entries into features, fixes, platform/security, and docs.
- Highlight breaking changes and required operator actions first.
- Summarize validation evidence and known residual risks.
- Include upgrade or rollback notes when relevant.
- Add concise acknowledgments and contributor list.
- Produce internal and external-facing variants if requested.

## Output Contract
Expected output headings:
- `RELEASE NOTES`
- `Highlights`
- `Features`
- `Fixes`
- `Breaking Changes`
- `Operational Notes`
- `Known Risks`
- `Contributors`

## Validation
- Notes align with merged PRs and shipped artifacts only.
- Breaking changes include explicit action requirements.
- Operational notes are actionable for on-call/release owners.
- Language is concise and non-ambiguous for external readers.
