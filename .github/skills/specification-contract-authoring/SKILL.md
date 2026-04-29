---
name: specification-contract-authoring
description: Author machine-readable specifications with explicit requirements, contracts, acceptance criteria, and validation rules.
---

# Skill: Specification Contract Authoring

## Purpose
Create unambiguous, implementation-ready specifications that can be validated and used reliably by humans and AI agents.

## Owner
- `ProductManager`
- `SolutionArchitect`

## Used In
- intake and planning for new features
- architecture and interface changes
- medium/high-risk scope definition

## Inputs
- Problem statement and scope constraints
- Business and technical requirements
- Interface/data contract details
- Risk and compliance obligations
- Acceptance and validation expectations

## Procedure
- Normalize scope, non-goals, and constraints into explicit statements.
- Write numbered requirements with testable acceptance criteria.
- Define interface and data contracts with clear ownership.
- Capture assumptions, dependencies, and edge-case behavior.
- Separate mandatory requirements from guidance/recommendations.
- Add validation criteria linked to prove-stage evidence.
- Ensure spec remains self-contained and context-complete.

## Output Contract
Expected output headings:
- `SPECIFICATION CONTRACT`
- `Purpose and Scope`
- `Requirements`
- `Interfaces and Data Contracts`
- `Acceptance Criteria`
- `Validation Criteria`
- `Open Questions`

## Validation
- Every requirement has at least one acceptance criterion.
- Contract sections are explicit enough for implementation delegation.
- Validation criteria map directly to prove-stage evidence.
- Open questions are clearly separated from requirements.
