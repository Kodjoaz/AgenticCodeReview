# Repository Custom Spec Contract

Use this file when your repository selects `custom` under `.cado/config.yml`
`spec_frameworks.active`.

## Purpose

Describe why this repository uses a custom specification format instead of
Spec Kit or OpenSpec.

## Specs Root

Default CADO root:

- `specs/`

If your repository uses subfolders or naming conventions under `specs/`, define
those conventions here.

## Required Artifacts

List the artifacts required for a change to move through CADO stages.

Example:

- `specs/<feature>/overview.md`
- `specs/<feature>/implementation-notes.md`
- `specs/<feature>/validation-checklist.md`

## Stage Requirements

### Intake

State what must exist before Intake can exit.

### Plan

State what planning artifacts are required.

### Gate

State what approvals, risk notes, or sign-off records are required.

### Build

State what implementation references or task records must be linked.

### Prove

State what validation outputs or evidence artifacts are mandatory.

### Ship

State what final summary, handoff, or rollback notes are required.

## Validation Rules

Define what counts as valid completion for this spec format.

- Required reviewers:
- Required evidence categories:
- Required links or references:
- Blocking conditions:

## Change Control For This Contract

Define how this contract itself may be changed.

- Owner:
- Reviewers:
- Approval threshold:
- Effective date:

## Notes

Record any repository-specific conventions, exceptions, or migration notes.
