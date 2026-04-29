#!/usr/bin/env python3
"""Generate release changelog markdown from git history."""

from __future__ import annotations

import argparse
import datetime as dt
import pathlib
import re
import subprocess
import sys
from collections import defaultdict


LOG_FIELD_SEP = "\x1f"
LOG_ENTRY_SEP = "\x1e"


def run(cmd: list[str]) -> str:
    result = subprocess.run(cmd, check=True, capture_output=True, text=True)
    return result.stdout.strip()


def infer_repo_url() -> str:
    raw = run(["git", "config", "--get", "remote.origin.url"])
    if raw.startswith("git@github.com:"):
        repo = raw.removeprefix("git@github.com:").removesuffix(".git")
        return f"https://github.com/{repo}"
    if raw.startswith("https://github.com/"):
        return raw.removesuffix(".git")
    return ""


def github_handle(name: str, email: str) -> str:
    lower = email.lower()
    if lower.endswith("@users.noreply.github.com"):
        prefix = lower.split("@", 1)[0]
        if "+" in prefix:
            return f"@{prefix.split('+', 1)[1]}"
        return f"@{prefix}"
    if " " not in name and name:
        return f"@{name}"
    return name or email


def parse_commits(start_ref: str, end_ref: str) -> list[dict[str, str]]:
    range_expr = f"{start_ref}..{end_ref}"
    output = run(
        [
            "git",
            "log",
            "--pretty=format:%H%x1f%an%x1f%ae%x1f%s%x1f%b%x1e",
            range_expr,
        ]
    )
    commits: list[dict[str, str]] = []
    if not output:
        return commits
    for raw in output.split(LOG_ENTRY_SEP):
        raw = raw.strip()
        if not raw:
            continue
        parts = raw.split(LOG_FIELD_SEP)
        if len(parts) < 5:
            continue
        commits.append(
            {
                "hash": parts[0].strip(),
                "author": parts[1].strip(),
                "email": parts[2].strip(),
                "subject": parts[3].strip(),
                "body": parts[4].strip(),
            }
        )
    return commits


CC_RE = re.compile(
    r"^(?P<type>[a-zA-Z]+)(?:\([^)]+\))?(?P<breaking>!)?:\s*(?P<desc>.+)$"
)


def categorize(commits: list[dict[str, str]]) -> dict[str, list[dict[str, str]]]:
    buckets: dict[str, list[dict[str, str]]] = defaultdict(list)
    for commit in commits:
        subject = commit["subject"]
        body = commit["body"]
        match = CC_RE.match(subject)
        ctype = match.group("type").lower() if match else "other"
        is_breaking = bool(match and match.group("breaking")) or "BREAKING CHANGE" in body

        if is_breaking:
            buckets["breaking"].append(commit)
        elif ctype == "feat":
            buckets["features"].append(commit)
        elif ctype == "fix":
            buckets["fixes"].append(commit)
        elif ctype == "docs":
            buckets["documentation"].append(commit)
        else:
            buckets["other"].append(commit)
    return buckets


def line_for(commit: dict[str, str], repo_url: str) -> str:
    short_hash = commit["hash"][:7]
    link = f"{repo_url}/commit/{commit['hash']}" if repo_url else ""
    subject = commit["subject"]
    mention = github_handle(commit["author"], commit["email"])
    if link:
        return f"- {subject} ([{short_hash}]({link})) by {mention}"
    return f"- {subject} ({short_hash}) by {mention}"


def section(title: str, items: list[str]) -> str:
    body = "\n".join(items) if items else "- None"
    return f"### {title}\n{body}\n"


def heading_for(end_ref: str) -> str:
    date_str = dt.date.today().isoformat()
    tag_match = re.fullmatch(r"v(\d+\.\d+\.\d+(?:-rc\.\d+)?)", end_ref)
    label = tag_match.group(1) if tag_match else end_ref
    return f"## [{label}] - {date_str}"


def build_entry(start_ref: str, end_ref: str, repo_url: str) -> str:
    commits = parse_commits(start_ref, end_ref)
    groups = categorize(commits)

    contributors = sorted(
        {github_handle(c["author"], c["email"]) for c in commits if c["author"] or c["email"]}
    )

    parts: list[str] = [heading_for(end_ref), ""]
    parts.append(section("Features", [line_for(c, repo_url) for c in groups["features"]]))
    parts.append(section("Fixes", [line_for(c, repo_url) for c in groups["fixes"]]))
    parts.append(
        section("Breaking Changes", [line_for(c, repo_url) for c in groups["breaking"]])
    )

    if groups["documentation"]:
        parts.append(
            section(
                "Documentation",
                [line_for(c, repo_url) for c in groups["documentation"]],
            )
        )

    if groups["other"]:
        parts.append(
            section("Other Changes", [line_for(c, repo_url) for c in groups["other"]])
        )

    parts.append(section("Contributors", [f"- {c}" for c in contributors]))
    return "\n".join(parts).strip() + "\n"


def prepend_changelog(entry: str, changelog_path: pathlib.Path) -> None:
    if not changelog_path.exists():
        header = "# Changelog\n\nAll notable changes to CADO Framework are documented here.\n\n"
        changelog_path.write_text(header + entry, encoding="utf-8")
        return

    content = changelog_path.read_text(encoding="utf-8")
    marker = re.search(r"^## \[", content, flags=re.MULTILINE)
    if marker:
        idx = marker.start()
        updated = content[:idx].rstrip() + "\n\n" + entry + "\n" + content[idx:].lstrip()
    else:
        updated = content.rstrip() + "\n\n" + entry
    changelog_path.write_text(updated, encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate markdown changelog between two refs"
    )
    parser.add_argument("start", help="Start git ref (exclusive)")
    parser.add_argument("end", help="End git ref (inclusive)")
    parser.add_argument("--output", help="Write entry to file")
    parser.add_argument(
        "--no-prepend",
        action="store_true",
        help="Do not prepend entry to CHANGELOG.md",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = pathlib.Path(__file__).resolve().parents[1]
    repo_url = infer_repo_url()
    entry = build_entry(args.start, args.end, repo_url)

    if not args.no_prepend:
        prepend_changelog(entry, repo_root / "CHANGELOG.md")

    if args.output:
        output_path = pathlib.Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(entry, encoding="utf-8")

    print(entry)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as exc:
        print(exc.stderr or str(exc), file=sys.stderr)
        raise SystemExit(1)

