#!/usr/bin/env bash
# CADO Framework Installer — bash edition
# Usage: ./install.sh [--target <path>] [--integration <name>] [--force]
#
# Options:
#   --target <path>       Target repository path (default: current directory)
#   --integration <name>  AI integration to activate: copilot | claude | cursor | gemini
#                         (default: copilot)
#   --force               Overwrite existing files without prompting

set -euo pipefail

# --------------------------------------------------------------------------
# Defaults
# --------------------------------------------------------------------------
TARGET_REPO="$(pwd)"
INTEGRATION="copilot"
FORCE=0
SKIP_PREREQUISITES=0

# --------------------------------------------------------------------------
# Argument parsing
# --------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)              TARGET_REPO="$2"; shift 2 ;;
    --integration)         INTEGRATION="$2"; shift 2 ;;
    --force)               FORCE=1; shift ;;
    --skip-prerequisites)  SKIP_PREREQUISITES=1; shift ;;
    -h|--help)
      echo "Usage: $0 [--target <path>] [--integration copilot|claude|cursor|gemini] [--force] [--skip-prerequisites]"
      exit 0 ;;
    *)
      echo "[ERROR] Unknown argument: $1" >&2; exit 1 ;;
  esac
done

# --------------------------------------------------------------------------
# Utilities (updated path calculation: scripts/bash -> scripts -> repo root)
# --------------------------------------------------------------------------
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

copy_dir() {
  local src="$1"
  local dst="$2"
  if [[ ! -d "$src" ]]; then
    log_warn "Source directory not found, skipping: $src"
    return
  fi
  mkdir -p "$dst"
  if [[ $FORCE -eq 1 ]]; then
    cp -r "$src/." "$dst/"
  else
    cp -rn "$src/." "$dst/"
  fi
  log_ok "Copied: ${dst#$TARGET_REPO/}"
}

copy_file() {
  local src="$1"
  local dst="$2"
  mkdir -p "$(dirname "$dst")"
  if [[ $FORCE -eq 1 ]]; then
    cp "$src" "$dst"
  else
    cp -n "$src" "$dst" 2>/dev/null || true
  fi
  log_ok "Copied: ${dst#$TARGET_REPO/}"
}

# --------------------------------------------------------------------------
# Validate
# --------------------------------------------------------------------------
log_header "CADO Framework Installer v$VERSION"
log_info   "Target repository : $TARGET_REPO"
log_info   "Integration       : $INTEGRATION"
echo ""

# Prerequisites check (optional)
if [[ $SKIP_PREREQUISITES -eq 0 ]]; then
  log_info "Running prerequisite check..."
  PREREQ_SCRIPT="$(dirname "${BASH_SOURCE[0]}")/check-prerequisites.sh"
  if [[ -f "$PREREQ_SCRIPT" ]]; then
    if ! bash "$PREREQ_SCRIPT" --target "$TARGET_REPO"; then
      log_error "Prerequisites check failed. Aborting installation."
      exit 1
    fi
  fi
  echo ""
fi

if [[ ! -d "$TARGET_REPO" ]]; then
  log_error "Target path does not exist: $TARGET_REPO"
  exit 1
fi

if [[ ! -d "$TARGET_REPO/.git" ]]; then
  log_warn "Target does not appear to be a git repository (.git not found)."
fi

valid_integrations=("copilot" "claude" "cursor" "gemini")
found=0
for v in "${valid_integrations[@]}"; do
  [[ "$INTEGRATION" == "$v" ]] && found=1
done
if [[ $found -eq 0 ]]; then
  log_error "Invalid integration '$INTEGRATION'. Must be one of: ${valid_integrations[*]}"
  exit 1
fi

# --------------------------------------------------------------------------
# Copy framework directories from the CADO Framework source tree
# --------------------------------------------------------------------------
log_info "Installing framework files..."

copy_dir "$CADO_ROOT/.cado/templates"    "$TARGET_REPO/.cado/templates"
copy_dir "$CADO_ROOT/src/workflows"          "$TARGET_REPO/.cado/workflows"
copy_dir "$CADO_ROOT/commands"               "$TARGET_REPO/.cado/commands"
copy_dir "$CADO_ROOT/.cado/integrations" "$TARGET_REPO/.cado/integrations"
copy_dir "$CADO_ROOT/scripts"                "$TARGET_REPO/.cado/scripts"
copy_dir "$CADO_ROOT/src/workflow"      "$TARGET_REPO/.cado/workflow"
copy_dir "$CADO_ROOT/src/templates"     "$TARGET_REPO/.cado/templates"
copy_dir "$CADO_ROOT/src/schemas"       "$TARGET_REPO/.cado/schemas"
copy_dir "$CADO_ROOT/src/examples"      "$TARGET_REPO/.cado/examples"
copy_dir "$CADO_ROOT/src/agents"        "$TARGET_REPO/.github/agents"
copy_dir "$CADO_ROOT/src/skills"        "$TARGET_REPO/.github/skills"

# --------------------------------------------------------------------------
# Copy prompt files
# --------------------------------------------------------------------------
log_info "Installing prompt files..."
PROMPTS_SRC="$CADO_ROOT/src/prompts"
PROMPTS_DST="$TARGET_REPO/.github/prompts"
mkdir -p "$PROMPTS_DST"

for f in "$PROMPTS_SRC"/cado.*.prompt.md; do
  [[ -f "$f" ]] || continue
  copy_file "$f" "$PROMPTS_DST/$(basename "$f")"
done

# --------------------------------------------------------------------------
# Copy framework README
# --------------------------------------------------------------------------
FRAMEWORK_README_SRC="$CADO_ROOT/src/README.md"
FRAMEWORK_README_DST="$TARGET_REPO/.cado/README.md"
if [[ -f "$FRAMEWORK_README_SRC" ]]; then
  copy_file "$FRAMEWORK_README_SRC" "$FRAMEWORK_README_DST"
fi

# --------------------------------------------------------------------------
# Write manifest.json
# --------------------------------------------------------------------------
log_info "Writing manifest.json..."
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
MANIFEST_PATH="$TARGET_REPO/.cado/manifest.json"
mkdir -p "$(dirname "$MANIFEST_PATH")"

cat > "$MANIFEST_PATH" <<EOF
{
  "framework": "cado",
  "version": "$VERSION",
  "description": "Stage-based multi-agent delivery framework",
  "installed_at": "$TIMESTAMP",
  "homepage": "https://github.com/Keayoub/cado",
  "requires": {
    "min_version": "0.1.0"
  },
  "files": {}
}
EOF
log_ok "Written: .cado/manifest.json"

# --------------------------------------------------------------------------
# Write integration.json
# --------------------------------------------------------------------------
INTEGRATION_PATH="$TARGET_REPO/.cado/integration.json"
cat > "$INTEGRATION_PATH" <<EOF
{
  "integration": "$INTEGRATION",
  "version": "$VERSION"
}
EOF
log_ok "Written: .cado/integration.json (integration=$INTEGRATION)"

# --------------------------------------------------------------------------
# Copy extensions.yml (from root)
# --------------------------------------------------------------------------
EXT_SRC="$CADO_ROOT/extensions.yml"
EXT_DST="$TARGET_REPO/.cado/extensions.yml"
if [[ -f "$EXT_SRC" ]]; then
  copy_file "$EXT_SRC" "$EXT_DST"
  log_ok "Written: .cado/extensions.yml"
fi

# --------------------------------------------------------------------------
# Copy config.yml (from root)
# --------------------------------------------------------------------------
CONFIG_SRC="$CADO_ROOT/config.yml"
CONFIG_DST="$TARGET_REPO/.cado/config.yml"
if [[ -f "$CONFIG_SRC" ]]; then
  copy_file "$CONFIG_SRC" "$CONFIG_DST"
  log_ok "Written: .cado/config.yml"
fi

# --------------------------------------------------------------------------
# Update workflow-registry timestamps (if jq is available)
# --------------------------------------------------------------------------
REGISTRY_PATH="$TARGET_REPO/.cado/workflows/workflow-registry.json"
if [[ -f "$REGISTRY_PATH" ]] && command -v jq &>/dev/null; then
  TMP="$(mktemp)"
  jq --arg ts "$TIMESTAMP" \
    '.workflows.cadoflow.installed_at = $ts | .workflows.cadoflow.updated_at = $ts' \
    "$REGISTRY_PATH" > "$TMP" && mv "$TMP" "$REGISTRY_PATH"
  log_ok "Updated: .cado/workflows/workflow-registry.json timestamps"
fi

# --------------------------------------------------------------------------
# Initialize signals directory
# --------------------------------------------------------------------------
log_info "Initializing signals directory..."
SIGNALS_PATH="$TARGET_REPO/.cado/signals"
if [[ ! -d "$SIGNALS_PATH" ]]; then
  mkdir -p "$SIGNALS_PATH"
  touch "$SIGNALS_PATH/.gitkeep"
  log_ok "Created: .cado/signals"
else
  log_ok "Signals directory already exists"
fi

echo ""

# --------------------------------------------------------------------------
# Run post-install verification
# --------------------------------------------------------------------------
log_info "Verifying installation..."
VERIFY_SCRIPT="$(dirname "${BASH_SOURCE[0]}")/verify-install.sh"
if [[ -f "$VERIFY_SCRIPT" ]]; then
  if ! bash "$VERIFY_SCRIPT" --target "$TARGET_REPO"; then
    log_error "Installation verification FAILED"
    echo ""
    exit 1
  fi
else
  log_warn "Verification script not found (skipped)"
fi

# --------------------------------------------------------------------------
# Done
# --------------------------------------------------------------------------
echo ""
log_header "CADO Framework $VERSION installed successfully."
echo ""
echo "  INSTALLATION SUMMARY"
echo "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Framework Version     : $VERSION"
echo "  Target Repository     : $TARGET_REPO"
echo "  Active Integration    : $INTEGRATION"
echo "  Installed At          : $TIMESTAMP"
echo ""
echo "  DIRECTORIES INSTALLED"
echo "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  .cado/workflows  → Workflow definitions"
echo "  .cado/commands   → Command registry"
echo "  .cado/scripts    → Installation and utility scripts"
echo ""
echo "  .cado/templates        → Templates for change requests"
echo "  .cado/signals          → Inter-process signals"
echo "  .cado/integrations     → Integration-specific config"
echo ""
echo "  .github/agents             → Agent definitions"
echo "  .github/prompts            → AI prompts for each stage"
echo "  .cado           → Framework documentation"
echo "  .github/skills             → Specialized skills library"
echo ""
echo "  CONFIGURATION FILES"
echo "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  .cado/manifest.json    → Installation metadata"
echo "  .cado/integration.json → Active integration setting"
echo "  .cado/extensions.yml      → Hooks and extensions"
echo "  .cado/config.yml          → Default behavior settings"
echo "  .cado/commands/commands.yml → Command registry"
echo ""
echo "  NEXT STEPS"
echo "  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "   1. Customize constitution:"
echo "      Edit .cado/templates/constitution-template.md"
echo ""
echo "   2. Review configuration:"
echo "      Edit .cado/config.yml (timeouts, evidence requirements)"
echo ""
echo "   3. Run your first delivery:"
echo "      Use the 'cado.intake' command in your AI tool"
echo ""
echo "   4. Read the documentation:"
echo "      See .cado/README.md for detailed guidance"
echo ""
echo "  For help, run: bash ./.cado/scripts/bash/install.sh --help"
echo ""



