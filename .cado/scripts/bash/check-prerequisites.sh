#!/usr/bin/env bash
# Check prerequisites for CADO Framework installation.
#
# Usage: ./check-prerequisites.sh [--target <path>]
#
# Validates:
# - Bash version >= 4.0
# - Git is installed
# - Target directory exists and is writable
# - Target is a git repository (recommended)

set -euo pipefail

TARGET_REPO="${1:-.}"
if [[ "$TARGET_REPO" == "--target" ]]; then
  TARGET_REPO="${2:-.}"
fi

log_header() { echo -e "\033[1;36m[CADO Framework] $*\033[0m"; }
log_info()   { echo -e "\033[0;37m  [INFO]   $*\033[0m"; }
log_ok()     { echo -e "\033[0;32m  [OK]     $*\033[0m"; }
log_warn()   { echo -e "\033[0;33m  [WARN]   $*\033[0m"; }
log_error()  { echo -e "\033[0;31m  [ERROR]  $*\033[0m"; }

FAILURES=0

log_header "CADO Framework Prerequisites Check"
echo ""

# --- Bash version ---
log_info "Checking Bash version..."
BASH_VERSION_MAJOR="${BASH_VERSINFO[0]}"
if [[ $BASH_VERSION_MAJOR -ge 4 ]]; then
  log_ok "Bash: v${BASH_VERSION_MAJOR}.${BASH_VERSINFO[1]}"
else
  log_error "Bash: v${BASH_VERSION_MAJOR}.${BASH_VERSINFO[1]} (requires >= 4.0)"
  ((FAILURES += 1))
fi

echo ""

# --- Git availability ---
log_info "Checking for Git..."
if command -v git &> /dev/null; then
  GIT_VERSION=$(git --version 2>&1 | head -n 1)
  log_ok "$GIT_VERSION"
else
  log_error "Git not found in PATH"
  ((FAILURES += 1))
fi

echo ""

# --- Target directory ---
log_info "Checking target directory..."
if [[ ! -d "$TARGET_REPO" ]]; then
  log_error "Directory does not exist: $TARGET_REPO"
  ((FAILURES += 1))
else
  log_ok "Target: $TARGET_REPO"

  # Check write permissions
  TEST_FILE="$TARGET_REPO/.cadoflow-write-test-$$-$RANDOM"
  if echo "test" > "$TEST_FILE" 2>/dev/null; then
    rm -f "$TEST_FILE"
    log_ok "Directory is writable"
  else
    log_error "Directory is not writable"
    ((FAILURES += 1))
  fi
fi

echo ""

# --- Git repository ---
log_info "Checking for git repository..."
if [[ -d "$TARGET_REPO/.git" ]]; then
  log_ok "Git repository found"
else
  log_warn ".git directory not found (will still work, but recommended)"
fi

echo ""

# --- Summary ---
if [[ $FAILURES -eq 0 ]]; then
  log_header "Prerequisites check PASSED"
  echo ""
  echo "  Status: Environment is ready for CADO Framework installation."
  echo ""
  exit 0
else
  log_error "Prerequisites check FAILED"
  echo ""
  echo "  Issues found: $FAILURES failures"
  echo ""
  exit 1
fi

