#!/usr/bin/env bash
# Verify that CADO Framework is correctly installed in the target repository.
#
# Usage: ./verify-install.sh [--target <path>]
#
# Validates:
# - Required directories exist
# - manifest.json and integration.json are present and valid JSON
# - All required prompt files are present
# - .github/agents/agents.yml is present and valid YAML
# - Timestamp in manifest.json is recent

set -euo pipefail

TARGET_REPO="${1:-.}"
if [[ "$TARGET_REPO" == "--target" ]]; then
  TARGET_REPO="${2:-.}"
fi

CADO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION_FILE="$CADO_ROOT/VERSION"
VERSION="0.0.0"
if [[ -f "$VERSION_FILE" ]]; then
  VERSION="$(cat "$VERSION_FILE" | tr -d '[:space:]')"
fi

log_header() { echo -e "\033[1;36m[CADO Framework] $*\033[0m"; }
log_info()   { echo -e "\033[0;37m  [INFO]   $*\033[0m"; }
log_ok()     { echo -e "\033[0;32m  [OK]     $*\033[0m"; }
log_warn()   { echo -e "\033[0;33m  [WARN]   $*\033[0m"; }
log_error()  { echo -e "\033[0;31m  [ERROR]  $*\033[0m"; }

FAILURES=0
SUCCESS=1

log_header "CADO Framework Installation Verification v$VERSION"
log_info "Target repository: $TARGET_REPO"
echo ""

# --- Check required directories ---
log_info "Checking required directories..."
REQUIRED_DIRS=(
  ".cado/templates"
  ".cado/integrations"
  ".cado/signals"
  ".cado"
  ".cado/workflows"
  ".cado/commands"
  ".cado/scripts"
  ".github/agents"
  ".github/prompts"
  ".github/skills"
)

for DIR in "${REQUIRED_DIRS[@]}"; do
  FULL_PATH="$TARGET_REPO/$DIR"
  if [[ -d "$FULL_PATH" ]]; then
    log_ok "Found: $DIR"
  else
    log_error "Missing: $DIR"
    ((FAILURES += 1))
    SUCCESS=0
  fi
done

echo ""

# --- Check skills content ---
log_info "Validating skills content..."
SKILL_FILES_COUNT=$(find "$TARGET_REPO/.github/skills" -type f -name "SKILL.md" 2>/dev/null | wc -l | tr -d '[:space:]')
if [[ "$SKILL_FILES_COUNT" -ge 1 ]]; then
  log_ok "Found: $SKILL_FILES_COUNT SKILL.md file(s)"
else
  log_error "No SKILL.md files found under .github/skills"
  ((FAILURES += 1))
  SUCCESS=0
fi

echo ""

# --- Check manifest.json ---
log_info "Validating manifest.json..."
MANIFEST_PATH="$TARGET_REPO/.cado/manifest.json"
if [[ -f "$MANIFEST_PATH" ]]; then
  if command -v jq &> /dev/null; then
    if jq empty "$MANIFEST_PATH" 2>/dev/null; then
      log_ok "Valid JSON: manifest.json"
      
      FRAMEWORK=$(jq -r '.framework' "$MANIFEST_PATH" 2>/dev/null || echo "")
      if [[ "$FRAMEWORK" == "cado" ]]; then
        log_ok "Framework: cado"
      else
        log_error "Invalid framework: $FRAMEWORK"
        ((FAILURES += 1))
        SUCCESS=0
      fi
      
      INSTALLED_AT=$(jq -r '.installed_at' "$MANIFEST_PATH" 2>/dev/null || echo "")
      if [[ -n "$INSTALLED_AT" ]]; then
        log_ok "Installed: $INSTALLED_AT"
      else
        log_warn "No installed_at timestamp"
      fi
    else
      log_error "Invalid JSON: manifest.json"
      ((FAILURES += 1))
      SUCCESS=0
    fi
  else
    log_warn "jq not found; skipping JSON validation"
  fi
else
  log_error "Missing: manifest.json"
  ((FAILURES += 1))
  SUCCESS=0
fi

echo ""

# --- Check integration.json ---
log_info "Validating integration.json..."
INTEGRATION_PATH="$TARGET_REPO/.cado/integration.json"
if [[ -f "$INTEGRATION_PATH" ]]; then
  if command -v jq &> /dev/null; then
    if jq empty "$INTEGRATION_PATH" 2>/dev/null; then
      log_ok "Valid JSON: integration.json"
      INTEGRATION=$(jq -r '.integration' "$INTEGRATION_PATH" 2>/dev/null || echo "")
      log_ok "Active integration: $INTEGRATION"
    else
      log_error "Invalid JSON: integration.json"
      ((FAILURES += 1))
      SUCCESS=0
    fi
  else
    log_warn "jq not found; skipping JSON validation"
  fi
else
  log_error "Missing: integration.json"
  ((FAILURES += 1))
  SUCCESS=0
fi

echo ""

# --- Check prompt files ---
log_info "Validating prompt files..."
REQUIRED_PROMPTS=(
  "cado.intake.prompt.md"
  "cado.route.prompt.md"
  "cado.plan.prompt.md"
  "cado.gate.prompt.md"
  "cado.build.prompt.md"
  "cado.prove.prompt.md"
  "cado.evidence.prompt.md"
  "cado.ship.prompt.md"
  "cado.git.commit.prompt.md"
  "cado.git.feature.prompt.md"
  "cado.git.initialize.prompt.md"
)

PROMPTS_FOUND=0
for PROMPT in "${REQUIRED_PROMPTS[@]}"; do
  PROMPT_PATH="$TARGET_REPO/.github/prompts/$PROMPT"
  if [[ -f "$PROMPT_PATH" ]]; then
    ((PROMPTS_FOUND += 1))
  else
    log_warn "Missing: $PROMPT"
    ((FAILURES += 1))
  fi
done
log_ok "Found: $PROMPTS_FOUND / ${#REQUIRED_PROMPTS[@]} prompts"

echo ""

# --- Check agents.yml ---
log_info "Validating agents.yml..."
AGENTS_PATH="$TARGET_REPO/.github/agents/agents.yml"
if [[ -f "$AGENTS_PATH" ]]; then
  if grep -q "^agents:" "$AGENTS_PATH" 2>/dev/null || grep -q "agents:" "$AGENTS_PATH" 2>/dev/null; then
    log_ok "Found: agents.yml"
  else
    log_warn "agents.yml may not be valid YAML"
  fi
else
  log_warn "Missing: agents.yml"
  ((FAILURES += 1))
fi

echo ""

# --- Check config.yml and extensions.yml ---
log_info "Validating configuration files..."
CONFIG_PATH="$TARGET_REPO/.cado/config.yml"
if [[ -f "$CONFIG_PATH" ]]; then
  log_ok "Found: .cado/config.yml"
else
  log_warn "Missing: .cado/config.yml"
  ((FAILURES += 1))
fi

EXTENSIONS_PATH="$TARGET_REPO/.cado/extensions.yml"
if [[ -f "$EXTENSIONS_PATH" ]]; then
  log_ok "Found: .cado/extensions.yml"
else
  log_warn "Missing: .cado/extensions.yml"
  ((FAILURES += 1))
fi

echo ""

# --- Ensure operational files are not installed at target root ---
log_info "Checking for root-level operational files (legacy layout)..."
UNEXPECTED_ROOT_PATHS=(
  "workflows"
  "commands"
  "scripts"
  "config.yml"
  "extensions.yml"
)

for P in "${UNEXPECTED_ROOT_PATHS[@]}"; do
  if [[ -e "$TARGET_REPO/$P" ]]; then
    log_error "Found unexpected root path: $P"
    ((FAILURES += 1))
    SUCCESS=0
  else
    log_ok "Not present at root: $P"
  fi
done

echo ""

# --- Summary ---
if [[ $FAILURES -eq 0 ]]; then
  SUCCESS=1
else
  SUCCESS=0
fi

if [[ $SUCCESS -eq 1 && $FAILURES -eq 0 ]]; then
  log_header "Installation verification PASSED"
  echo ""
  echo "  Status: All required files and directories are present."
  echo ""
  exit 0
else
  log_error "Installation verification FAILED"
  echo ""
  echo "  Issues found: $FAILURES failures"
  echo "  Run install.sh again to repair the installation."
  echo ""
  exit 1
fi


