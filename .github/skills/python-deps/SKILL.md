---
name: python-deps
description: Manage Python dependencies with pip-tools workflow and lockfile regeneration.
---

# Skill: Python Dependency Management

Python dependencies for `src/control-center/core` use a **pip-tools-style workflow**:

| File | Purpose |
|------|---------|
| `requirements.in` | Human-editable (add/remove/update packages here) |
| `requirements.txt` | Auto-generated lockfile (never edit manually) |

## When adding or changing a Python package:
1. Edit `src/control-center/core/requirements.in` (use `>=X.Y.Z` constraints)
2. Regenerate the lockfile: `bash src/control-center/core/compile-deps.sh`
3. Commit both files: `git add requirements.in requirements.txt`

The compile script runs: `uv pip compile requirements.in -o requirements.txt --annotation-style=line`
