#!/usr/bin/env bash
set -euo pipefail

# Interactive bootstrap for CADO Framework.
# Prompts for framework/tool/persona/profile, runs cado init,
# writes selections to .cado/config.yml, and verifies with cado doctor.

TARGET_REPO=""

usage() {
  cat <<'EOF'
Usage:
  ./scripts/bootstrap.sh --target /path/to/repo

Options:
  --target <path>   Path to target repository (required)
  -h, --help        Show this help
EOF
}

log_info() {
  printf '[INFO] %s\n' "$1"
}

log_warn() {
  printf '[WARN] %s\n' "$1"
}

log_error() {
  printf '[ERROR] %s\n' "$1" >&2
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    log_error "Required command not found on PATH: $cmd"
    exit 1
  fi
}

resolve_target_repo() {
  local path_value="$1"
  if [[ ! -d "$path_value" ]]; then
    log_error "Target repository does not exist: $path_value"
    exit 1
  fi

  (
    cd "$path_value"
    pwd
  )
}

print_header() {
  echo
  echo '========================================='
  echo ' CADO Framework Interactive Bootstrap'
  echo '========================================='
  echo
}

prompt_menu() {
  local title="$1"
  shift
  local options=("$@")

  while true; do
    echo "$title"
    local i=1
    for option in "${options[@]}"; do
      printf '  %d) %s\n' "$i" "$option"
      i=$((i + 1))
    done

    read -r -p 'Enter selection number: ' selected
    if [[ "$selected" =~ ^[0-9]+$ ]] && (( selected >= 1 && selected <= ${#options[@]} )); then
      echo "$selected"
      return 0
    fi

    log_warn 'Invalid selection. Please choose a valid number.'
    echo
  done
}

map_framework_key() {
  case "$1" in
    1) echo 'spec-kit' ;;
    2) echo 'openspec' ;;
    3) echo 'custom' ;;
    *) echo 'spec-kit' ;;
  esac
}

map_tool_key() {
  case "$1" in
    1) echo 'copilot' ;;
    2) echo 'claude' ;;
    3) echo 'cursor' ;;
    4) echo 'other' ;;
    *) echo 'copilot' ;;
  esac
}

map_integration_id() {
  case "$1" in
    copilot) echo 'copilot' ;;
    claude) echo 'claude' ;;
    cursor) echo 'cursor' ;;
    other) echo 'gemini' ;;
    *) echo 'copilot' ;;
  esac
}

map_persona_key() {
  case "$1" in
    1) echo 'architect' ;;
    2) echo 'executor' ;;
    3) echo 'facilitator' ;;
    *) echo 'executor' ;;
  esac
}

map_persona_profile() {
  case "$1" in
    architect) echo 'conductor' ;;
    executor) echo 'maximus' ;;
    facilitator) echo 'conductor' ;;
    *) echo 'maximus' ;;
  esac
}

map_display_name() {
  case "$1" in
    architect) echo 'Architect' ;;
    executor) echo 'Executor' ;;
    facilitator) echo 'Facilitator' ;;
    *) echo 'Maximus' ;;
  esac
}

map_execution_key() {
  case "$1" in
    1) echo 'express' ;;
    2) echo 'standard' ;;
    3) echo 'full' ;;
    *) echo 'standard' ;;
  esac
}

map_runtime_profile() {
  case "$1" in
    express) echo 'precise' ;;
    standard) echo 'balanced' ;;
    full) echo 'creative' ;;
    *) echo 'balanced' ;;
  esac
}

update_cado_config() {
  local config_path="$1"
  local spec_framework="$2"
  local integration="$3"
  local persona_profile="$4"
  local runtime_profile="$5"
  local display_name="$6"
  local persona_choice="$7"
  local execution_choice="$8"
  local ai_tool_choice="$9"

  if [[ ! -f "$config_path" ]]; then
    log_error "Config file not found: $config_path"
    exit 1
  fi

  local tmp_file
  tmp_file="$(mktemp)"

  awk \
    -v spec_framework="$spec_framework" \
    -v integration="$integration" \
    -v persona_profile="$persona_profile" \
    -v runtime_profile="$runtime_profile" \
    -v display_name="$display_name" \
    '
      BEGIN {
        section = ""
        in_conductor = 0
      }
      {
        if ($0 ~ /^[A-Za-z_][A-Za-z0-9_-]*:[[:space:]]*$/) {
          section = $0
          sub(/:[[:space:]]*$/, "", section)
          in_conductor = 0
        }

        if (section == "agent_identity" && $0 ~ /^  conductor:[[:space:]]*$/) {
          in_conductor = 1
          print $0
          next
        }

        if (section == "agent_identity" && in_conductor == 1 && $0 ~ /^  [A-Za-z_][A-Za-z0-9_-]*:[[:space:]]*$/ && $0 !~ /^  conductor:[[:space:]]*$/) {
          in_conductor = 0
        }

        if (section == "spec_frameworks" && $0 ~ /^  active:[[:space:]]*/) {
          print "  active: " spec_framework
          next
        }

        if (section == "defaults" && $0 ~ /^  integration:[[:space:]]*/) {
          print "  integration: " integration
          next
        }

        if (section == "persona_profiles" && $0 ~ /^  active_persona:[[:space:]]*/) {
          print "  active_persona: " persona_profile
          next
        }

        if (section == "runtime_profiles" && $0 ~ /^  active_profile:[[:space:]]*/) {
          print "  active_profile: " runtime_profile
          next
        }

        if (section == "agent_identity" && in_conductor == 1 && $0 ~ /^    display_name:[[:space:]]*/) {
          print "    display_name: " display_name
          next
        }

        print $0
      }
    ' "$config_path" > "$tmp_file"

  # Drop existing bootstrap_selection block if present.
  local tmp_file2
  tmp_file2="$(mktemp)"
  awk '
    BEGIN { skipping = 0 }
    /^[A-Za-z_][A-Za-z0-9_-]*:[[:space:]]*$/ {
      if ($0 ~ /^bootstrap_selection:[[:space:]]*$/) {
        skipping = 1
        next
      }
      if (skipping == 1) {
        skipping = 0
      }
    }
    {
      if (skipping == 0) {
        print $0
      }
    }
  ' "$tmp_file" > "$tmp_file2"

  mv "$tmp_file2" "$config_path"
  rm -f "$tmp_file"

  local now
  now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

  {
    echo
    echo 'bootstrap_selection:'
    echo "  spec_framework: $spec_framework"
    echo "  ai_tool: $ai_tool_choice"
    echo "  maximus_persona: $persona_choice"
    echo "  execution_profile: $execution_choice"
    echo "  updated_at: $now"
  } >> "$config_path"
}

run_cado_init() {
  local target="$1"
  local integration_id="$2"
  log_info "Running cado init for target: $target"
  cado init --target "$target" --integration "$integration_id"
}

run_cado_doctor() {
  local target="$1"
  log_info "Running cado doctor for target: $target"
  cado doctor --target "$target"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      TARGET_REPO="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      log_error "Unknown argument: $1"
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$TARGET_REPO" ]]; then
  log_error 'Missing required --target argument.'
  usage
  exit 2
fi

require_cmd cado

RESOLVED_TARGET="$(resolve_target_repo "$TARGET_REPO")"

print_header

spec_selection="$(prompt_menu 'Choose specification framework:' \
  'Spec Kit (artifact-driven: spec.md, plan.md, tasks.md)' \
  'OpenSpec (lightweight requirements)' \
  'Custom (use your own spec files and contract rules)')"
spec_framework="$(map_framework_key "$spec_selection")"

tool_selection="$(prompt_menu 'Choose AI tool integration:' \
  'GitHub Copilot' \
  'Claude' \
  'Cursor' \
  'Other (mapped to Gemini profile)')"
ai_tool_choice="$(map_tool_key "$tool_selection")"
integration_id="$(map_integration_id "$ai_tool_choice")"

persona_selection="$(prompt_menu 'Choose Maximus persona:' \
  'Architect' \
  'Executor' \
  'Facilitator')"
persona_choice="$(map_persona_key "$persona_selection")"
persona_profile="$(map_persona_profile "$persona_choice")"
display_name="$(map_display_name "$persona_choice")"

execution_selection="$(prompt_menu 'Choose execution profile:' \
  'Express' \
  'Standard' \
  'Full')"
execution_choice="$(map_execution_key "$execution_selection")"
runtime_profile="$(map_runtime_profile "$execution_choice")"

echo
log_info "Selected framework: $spec_framework"
log_info "Selected tool: $ai_tool_choice -> integration '$integration_id'"
log_info "Selected persona: $persona_choice"
log_info "Selected execution profile: $execution_choice -> runtime '$runtime_profile'"

run_cado_init "$RESOLVED_TARGET" "$integration_id"

config_path="$RESOLVED_TARGET/.cado/config.yml"
log_info "Writing selections to: $config_path"
update_cado_config \
  "$config_path" \
  "$spec_framework" \
  "$integration_id" \
  "$persona_profile" \
  "$runtime_profile" \
  "$display_name" \
  "$persona_choice" \
  "$execution_choice" \
  "$ai_tool_choice"

run_cado_doctor "$RESOLVED_TARGET"

echo
echo 'Bootstrap completed successfully.'
echo "Target: $RESOLVED_TARGET"
echo "Config:  $config_path"
