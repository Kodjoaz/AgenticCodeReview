# CADO Framework Upgrade Guide

uv tool install cado-cli --from git+https://github.com/Keayoub/cado-framework.git@v0.3.4 --force
puis dans ton repo:
cado upgrade
This document describes how to upgrade the CADO Framework installation in this repository.

---

## Prerequisites

| Requirement | Minimum version | Notes |
|-------------|----------------|-------|
| Git | 2.30+ | Required to pull the cado-framework source |
| uv | any | Used to install and run `cado-cli` |
| cado-framework local clone | — | See step 1 |

---

## Steps

### 1. Pull the latest cado-framework

Navigate to your local clone of the CADO Framework repository and pull the latest changes:

```powershell
cd C:\Works\Agentic\cado-framework
git pull origin main
```

Confirm the version in `VERSION`:

```powershell
Get-Content VERSION
```

---

### 2. Install (or update) cado-cli

Install the CLI from the local checkout using `uv`:

```powershell
uv tool install --from . cado-cli
```

If already installed, `uv` will upgrade it automatically. Verify the installed version:

```powershell
cado --version
```

---

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
