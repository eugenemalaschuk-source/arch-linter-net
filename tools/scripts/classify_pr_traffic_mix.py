#!/usr/bin/env python3
"""Reproducible extractor for the #503 PR-traffic-mix evidence.

Classifies the changed file paths of the last N commits on a given ref by
path pattern, and reports how many commits touch a central-build-props or
policy path (paths that force ``GlobalExpansion`` under
``ChangedProjectScopePlanner``) versus only project source/``.csproj`` paths.

This is informal, repository-history context for
docs/internal/changed-project-advisory-analysis-evidence.md — a path-pattern
heuristic over commit metadata, not a literal run of
``ChangedProjectScopePlanner`` (which requires a structured project/edge
graph the raw commit history does not provide). Output is deterministic for a
fixed (end_ref, commit_count) pair, so the JSON evidence's aggregate figures
can be regenerated and checked against the recorded commit range.

Usage:
    uv run --project tools/pyproject.toml python tools/scripts/classify_pr_traffic_mix.py \
        --end-ref bb533f0f2fc0d2e30addc7a4110adda26e825e68 --commit-count 300
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

CENTRAL_BUILD_PROPS_NAMES = {
    "nuget.config",
    "global.json",
}
CENTRAL_BUILD_PROPS_PREFIXES = (
    "directory.build.",
    "directory.packages.props",
)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def classify(path: str) -> str:
    normalized = path.replace("\\", "/")
    lower = normalized.lower()
    basename = lower.rsplit("/", 1)[-1]

    if basename in CENTRAL_BUILD_PROPS_NAMES or basename.startswith(CENTRAL_BUILD_PROPS_PREFIXES):
        return "central-build-props"
    if ".editorconfig" in basename:
        return "central-build-props"
    if lower.startswith("architecture/") and lower.endswith((".yml", ".yaml")):
        return "policy"
    if lower.startswith("architecture/api/") and lower.endswith(".public-api.txt"):
        return "api-snapshot"
    if lower.endswith(".csproj"):
        return "csproj"
    if lower.startswith(("src/", "tests/")):
        return "project-source"
    return "other-non-architecture-relevant"


def load_commits(end_ref: str, commit_count: int, cwd: Path) -> dict[str, list[str]]:
    output = subprocess.run(
        ["git", "log", end_ref, "-n", str(commit_count), "--pretty=format:__COMMIT__%H", "--name-only"],
        capture_output=True,
        text=True,
        cwd=cwd,
        check=True,
    ).stdout

    commits: dict[str, list[str]] = {}
    current: str | None = None
    for line in output.splitlines():
        if line.startswith("__COMMIT__"):
            current = line[len("__COMMIT__"):]
            commits[current] = []
        elif line.strip() and current is not None:
            commits[current].append(line.strip())
    return commits


def commit_range(commits: dict[str, list[str]], cwd: Path) -> tuple[str, str]:
    shas = list(commits.keys())
    newest = shas[0]
    oldest = shas[-1]
    return newest, oldest


def summarize(commits: dict[str, list[str]]) -> dict[str, object]:
    total = 0
    global_shaped = 0
    project_source_or_csproj_only = 0
    for files in commits.values():
        if not files:
            continue
        total += 1
        kinds = {classify(path) for path in files}
        if kinds & {"central-build-props", "policy"}:
            global_shaped += 1
        if kinds <= {"project-source", "csproj"}:
            project_source_or_csproj_only += 1

    return {
        "total_commits_with_files": total,
        "commits_touching_central_props_or_policy_paths": global_shaped,
        "commits_touching_central_props_or_policy_paths_ratio": round(global_shaped / total, 4) if total else 0.0,
        "commits_touching_only_project_source_or_csproj_paths": project_source_or_csproj_only,
        "commits_touching_only_project_source_or_csproj_paths_ratio": round(project_source_or_csproj_only / total, 4)
        if total
        else 0.0,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--end-ref", required=True, help="Newest commit (inclusive) to start counting back from.")
    parser.add_argument("--commit-count", type=int, default=300, help="Number of commits to sample (default: 300).")
    args = parser.parse_args()

    root = repository_root()
    commits = load_commits(args.end_ref, args.commit_count, root)
    if not commits:
        print("No commits found for the given range.", file=sys.stderr)
        return 1

    newest, oldest = commit_range(commits, root)
    result = {
        "end_ref": args.end_ref,
        "commit_count_requested": args.commit_count,
        "commit_count_returned": len(commits),
        "newest_commit": newest,
        "oldest_commit": oldest,
        **summarize(commits),
    }
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
