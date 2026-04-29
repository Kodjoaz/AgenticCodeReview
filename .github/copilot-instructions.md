# Copilot Workspace Instructions

## General Guardrails

### Before Acting
- **Read before editing.** Always read the current file contents before making any changes.
- **Verify before assuming.** When something seems missing or broken, verify the actual state with tools before concluding what the problem is.
- **Disambiguate vague requests.** If a request could mean multiple things, identify the most specific and least destructive interpretation first, then confirm before wider action.
- **Smallest valid fix.** Prefer the narrowest change that resolves the issue. Do not touch unrelated files.

### Destructive or Irreversible Actions
- **Never delete files** without explicit user confirmation.
- **Never run `git reset --hard`, `git clean`, or `rm -rf`** without explicit user instruction.
- **Never overwrite committed stubs or framework-owned files** with generated content.
- **Never `git push --force`** or amend published commits without explicit approval.

### Scope Control
- **Do not refactor, rename, or restructure** code or files unless explicitly asked.
- **Do not add features, comments, or error handling** beyond what was directly requested.
- **Do not modify multiple unrelated files** in a single step without justification.
- **Framework-owned files** (`.cado/`, `.github/prompts/`, `.github/skills/`, `.github/agents/`) are read-only unless the user is performing an explicit framework upgrade or configuration task.

### Ambiguity Resolution Order
When a user's request is ambiguous:
1. Check the workspace state with tools (list dir, read file, git status).
2. Pick the least destructive interpretation.
3. Act on that interpretation and state what assumption was made.
4. Do **not** broaden the fix scope until the narrow fix is confirmed correct.

### After Acting
- Confirm what was changed and what was left untouched.
- If a mistake was made, acknowledge it clearly and revert cleanly using `git checkout -- <path>` or by restoring the original content.

---

## Repository Layout

```
AgenticCodeReview/
├── .cado/               # CADO framework runtime config
├── .github/
│   ├── agents/          # CADO agent definitions
│   ├── prompts/         # CADO stage prompt stubs (intentionally minimal — DO NOT fill with content)
│   ├── skills/          # CADO skill definitions
│   └── workflows/       # CI/CD workflows
├── Docs/                # Root-level documentation (tracked in git)
│   ├── CADO-Upgrade.md
│   └── Specs/           # Root-level spec copies (e.g. EMTReviewSpec.md)
└── EnterpriseMessageTransit/   # .NET messaging library (C#, net8.0 / netstandard2.0)
    └── Docs/
        └── Specs/
            └── EMTReviewSpec.md   # Source of truth for the EMT review spec (committed to git)
```

## Critical Rules

### `.github/prompts/cado.*.prompt.md` — Prompt Stubs
- These files are **intentional stubs** shipped by the CADO framework.
- They contain only a YAML front-matter `agent:` field (30–40 bytes each). This is correct.
- **Never fill these files with generated content.** They are not empty by mistake.
- If you must restore them: `git checkout -- .github/prompts/`

### `Docs/` — Root Documentation
- The `Docs/` folder at the repository root is **tracked in git**.
- `Docs/Specs/` holds root-level copies of spec files for easy access.
- When a user reports "specs/content missing", the issue is likely a file missing from `Docs/Specs/`, **not** empty prompt stubs.
- Source of truth for specs is `EnterpriseMessageTransit/Docs/Specs/` (committed to git).

### Disambiguation Rule
When a user reports content is "missing" or "disappeared":
1. First check `Docs/Specs/` for missing spec files.
2. Check `EnterpriseMessageTransit/Docs/Specs/` as the committed source of truth.
3. Do **not** assume empty `.github/prompts/` stubs are the problem — they are stubs by design.

## CADO Framework
- Local clone: `C:\Works\Agentic\cado-framework`
- Current version in this repo: 0.3.4
- Upgrade instructions: `Docs/CADO-Upgrade.md`
- Stages: Intake → Plan → Gate → Build → Prove → Ship
