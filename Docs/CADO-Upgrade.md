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

## Alternative: Upgrade from local clone

Use this if you have a local clone of `cado-framework` and want to install from it.

### 1. Pull the latest cado-framework

```powershell
cd C:\Works\Agentic\cado-framework
git pull origin main
```

### 2. Install cado-cli from local clone

```powershell
uv tool install --from . cado-cli --force
```

Verify:

```powershell
cado --version
```

### 3. Run the installer

Use `cado install` targeting this repository with the `copilot` integration. Add `--force` to overwrite existing files:

```powershell
cado install --target "C:\Works\Agentic\AgenticCodeReview" --integration copilot --force
```

Expected output:
```
[cado] Installing CADO Framework <version> into C:\Works\Agentic\AgenticCodeReview
[cado] Integration: copilot
[cado] CADO Framework <version> installed successfully.
```

---

### 4. Initialize / refresh policy files

```powershell
cado policy init --target "C:\Works\Agentic\AgenticCodeReview" --force
```

This creates or refreshes:
- `.cado/policies/spec-contract.md`
- `.cado/policies/gate-policy.yml`
- `.cado/policies/gate-policy.md`

---

### 5. Post-install verification

Run each check and confirm the expected output.

**Manifest version and timestamp:**
```powershell
Get-Content "C:\Works\Agentic\AgenticCodeReview\.cado\manifest.json"
```
Expected: `"version"` matches the version pulled in step 1; `"installed_at"` is a fresh timestamp.

**Prompt files (should be 11):**
```powershell
(Get-ChildItem "C:\Works\Agentic\AgenticCodeReview\.github\prompts" -Filter "cado.*.prompt.md").Count
```

**Maximus agent present:**
```powershell
Get-Content "C:\Works\Agentic\AgenticCodeReview\.github\agents\Maximus.md" | Select-Object -First 5
```

**Workflow registry:**
```powershell
Get-Content "C:\Works\Agentic\AgenticCodeReview\.cado\workflows\workflow-registry.json"
```

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
