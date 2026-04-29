#!/usr/bin/env python3
"""Bump semantic versions in VERSION and pyproject.toml."""

from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys
from dataclasses import dataclass


SEMVER_RE = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-rc\.(\d+))?$")


@dataclass(frozen=True)
class Version:
    major: int
    minor: int
    patch: int
    rc: int | None = None

    @classmethod
    def parse(cls, value: str) -> "Version":
        match = SEMVER_RE.fullmatch(value.strip())
        if not match:
            raise ValueError(f"Invalid semantic version: {value}")
        major, minor, patch, rc = match.groups()
        return cls(
            major=int(major),
            minor=int(minor),
            patch=int(patch),
            rc=int(rc) if rc is not None else None,
        )

    def base(self) -> str:
        return f"{self.major}.{self.minor}.{self.patch}"

    def __str__(self) -> str:
        if self.rc is None:
            return self.base()
        return f"{self.base()}-rc.{self.rc}"


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8").strip()


def write_text(path: pathlib.Path, value: str) -> None:
    path.write_text(f"{value}\n", encoding="utf-8")


def update_pyproject(pyproject_path: pathlib.Path, new_version: str) -> None:
    content = pyproject_path.read_text(encoding="utf-8")
    pattern = re.compile(
        r"(?ms)(\[project\]\s+.*?\nversion\s*=\s*)\"[^\"]+\""
    )
    if not pattern.search(content):
        raise RuntimeError("Could not locate [project].version in pyproject.toml")
    updated = pattern.sub(rf'\1"{new_version}"', content, count=1)
    pyproject_path.write_text(updated, encoding="utf-8")


def compute_next_version(current: Version, args: argparse.Namespace) -> Version:
    if args.set_version:
        return Version.parse(args.set_version)
    if args.major:
        return Version(current.major + 1, 0, 0)
    if args.minor:
        return Version(current.major, current.minor + 1, 0)
    if args.patch:
        return Version(current.major, current.minor, current.patch + 1)
    if args.prerelease:
        if current.rc is None:
            return Version(current.major, current.minor, current.patch, 1)
        return Version(current.major, current.minor, current.patch, current.rc + 1)
    raise RuntimeError("No bump mode selected")


def run_git_tag(tag: str) -> None:
    subprocess.run(["git", "tag", tag], check=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Bump VERSION and pyproject.toml semantic version"
    )
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument(
        "--set-version",
        help="Set an explicit semantic version (for example 0.2.0)",
    )
    group.add_argument("--major", action="store_true", help="Bump major")
    group.add_argument("--minor", action="store_true", help="Bump minor")
    group.add_argument("--patch", action="store_true", help="Bump patch")
    group.add_argument(
        "--prerelease", action="store_true", help="Bump or create rc prerelease"
    )
    parser.add_argument("--tag", action="store_true", help="Create git tag vX.Y.Z")
    parser.add_argument("--dry-run", action="store_true", help="Show changes only")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = pathlib.Path(__file__).resolve().parents[1]
    version_path = repo_root / "VERSION"
    pyproject_path = repo_root / "pyproject.toml"

    current_raw = read_text(version_path)
    current = Version.parse(current_raw)
    next_version = compute_next_version(current, args)
    next_raw = str(next_version)

    print(f"Current version: {current_raw}")
    print(f"Next version:    {next_raw}")
    print(f"Commit message:  chore: bump version to {next_raw}")

    if args.dry_run:
        return 0

    write_text(version_path, next_raw)
    update_pyproject(pyproject_path, next_raw)

    if args.tag:
        tag = f"v{next_raw}"
        run_git_tag(tag)
        print(f"Created tag:     {tag}")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, RuntimeError, subprocess.CalledProcessError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        raise SystemExit(1)
