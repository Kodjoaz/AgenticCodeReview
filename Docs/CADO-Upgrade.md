# CADO Framework Upgrade Guide

This document describes how to upgrade the CADO Framework installation in this repository.

---

## Quick Upgrade (recommended)

### Step 1 — Install or update cado-cli from GitHub

```powershell
uv tool install cado-cli --from git+https://github.com/Keayoub/cado-framework.git@v0.3.4 --force
```

Replace `v0.3.4` with the target version tag. This installs the CLI directly from the release without needing a local clone.

### Step 2 — Upgrade the repo

```powershell
cd C:\Works\Agentic\AgenticCodeReview
cado upgrade
```

`cado upgrade` detects the installed version, applies all framework files to the repo, and reports what changed.

---

## What gets updated

| Source (cado-framework) | Destination (this repo) |
|-------------------------|-------------------------|
| `src/agents/*` | `.github/agents/` |
| `src/skills/*` | `.github/skills/` |
| `src/prompts/cado.*.prompt.md` | `.github/prompts/` |
| `src/workflow/*` | `.cado/workflow/` |
| `src/workflows/*` | `.cado/workflows/` |
| `src/templates/*` | `.cado/templates/` |
| `src/schemas/*` | `.cado/schemas/` |
| `src/examples/*` | `.cado/examples/` |
| `commands/*` | `.cado/commands/` |
| `config.yml` | `.cado/config.yml` |
| `extensions.yml` | `.cado/extensions.yml` |
| _(generated)_ | `.cado/manifest.json` |
| _(generated)_ | `.cado/integration.json` |
| _(generated)_ | `.cado/policies/*` (via `cado policy init`) |

---

## References

- [CADO Framework installation guide](https://github.com/Keayoub/cado-framework/blob/main/docs/installation.md)
- [CHANGELOG](https://github.com/Keayoub/cado-framework/blob/main/CHANGELOG.md)
