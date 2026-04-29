#!/usr/bin/env bash
set -euo pipefail

# Release helper for CADO Framework
# Usage:
#   ./scripts/release.sh --new-version 0.2.0
#   ./scripts/release.sh --new-version 0.2.0 --push --build

NEW_VERSION=""
PUSH=0
BUILD=0
FORCE=0
BASE_BRANCH="main"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --new-version) NEW_VERSION="$2"; shift 2 ;;
    --push) PUSH=1; shift ;;
    --build) BUILD=1; shift ;;
    --force) FORCE=1; shift ;;
    --base-branch) BASE_BRANCH="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: $0 --new-version X.Y.Z [--push] [--build] [--force] [--base-branch main]"
      exit 0
      ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$NEW_VERSION" ]]; then
  echo "Missing --new-version" >&2
  exit 2
fi

if [[ ! "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9\.]+)?$ ]]; then
  echo "Invalid version format: $NEW_VERSION" >&2
  exit 2
fi

git --version >/dev/null

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

if [[ $FORCE -ne 1 ]] && [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree is not clean. Commit/stash changes or use --force." >&2
  git status --short
  exit 5
fi

python scripts/bump-version.py --set-version "$NEW_VERSION"

if [[ $BUILD -eq 1 ]]; then
  uv build
  uv run cado --help >/dev/null
fi

git add VERSION pyproject.toml
git commit -m "chore: bump version to $NEW_VERSION"

TAG_NAME="v$NEW_VERSION"
git tag -a "$TAG_NAME" -m "Release $TAG_NAME"

if [[ $PUSH -eq 1 ]]; then
  git push origin "$BASE_BRANCH"
  git push origin "$TAG_NAME"
fi

echo "Release workflow completed: $TAG_NAME"
echo "Next: release workflow runs on tag push; PyPI publish can be enabled later."
