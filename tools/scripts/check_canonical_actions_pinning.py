#!/usr/bin/env python3
"""Reject mutable third-party GitHub Actions refs in canonical docs examples.

Production `.github/workflows/*.yml` already pin third-party actions to full commit
SHAs. This guard applies the same discipline to canonical, copy/paste-intended
workflow examples under `docs/`, so adopting a documented snippet does not
silently weaken the repository's own CI security model.

Only `uses:` lines inside fenced ```yaml/```yml code blocks are in scope — prose
mentioning an action tag descriptively (for example, in historical or explanatory
text) is not a canonical snippet and is not flagged. First-party local actions
(`./...`) and reusable-workflow references owned by this repository are exempt,
since they are not third-party supply-chain surface.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

FENCE_START = re.compile(r"^```(?P<lang>[A-Za-z0-9_+-]*)\s*$")
FENCE_END = re.compile(r"^```\s*$")
USES_LINE = re.compile(r"^\s*(?:-\s*)?uses:\s*(?P<ref>\S+)")
FULL_COMMIT_SHA = re.compile(r"^[0-9a-fA-F]{40}$")
YAML_LANGUAGES = {"yaml", "yml"}
OWN_REPO_PREFIX = "eugenemalaschuk-source/arch-linter-net/"
IGNORED_DOCS_DIRS = {"internal"}


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def canonical_markdown_files(root: Path) -> list[Path]:
    docs_root = root / "docs"
    if not docs_root.exists():
        return []
    return sorted(
        path
        for path in docs_root.rglob("*.md")
        if not set(path.relative_to(docs_root).parts[:-1]) & IGNORED_DOCS_DIRS
    )


def yaml_fence_lines(text: str) -> list[tuple[int, str]]:
    """Return (line_number, content) pairs for lines inside ```yaml/```yml fences."""
    lines: list[tuple[int, str]] = []
    in_yaml_fence = False
    for line_number, line in enumerate(text.splitlines(), start=1):
        start_match = FENCE_START.match(line)
        if start_match is not None:
            in_yaml_fence = start_match.group("lang").lower() in YAML_LANGUAGES
            continue
        if in_yaml_fence and FENCE_END.match(line):
            in_yaml_fence = False
            continue
        if in_yaml_fence:
            lines.append((line_number, line))
    return lines


def is_exempt_ref(action_ref: str) -> bool:
    return action_ref.startswith("./") or action_ref.startswith(OWN_REPO_PREFIX)


def ref_violations(relative_path: str, text: str) -> list[str]:
    violations: list[str] = []
    for line_number, line in yaml_fence_lines(text):
        match = USES_LINE.match(line)
        if match is None:
            continue
        action_ref = match.group("ref")
        if is_exempt_ref(action_ref):
            continue
        if "@" not in action_ref:
            violations.append(
                f"{relative_path}:{line_number}: unpinned third-party action reference '{action_ref}'"
            )
            continue
        _, _, pin = action_ref.rpartition("@")
        if not FULL_COMMIT_SHA.match(pin):
            violations.append(
                f"{relative_path}:{line_number}: mutable third-party action reference '{action_ref}' "
                "must pin a full commit SHA"
            )
    return violations


def find_violations(root: Path) -> list[str]:
    violations: list[str] = []
    for path in canonical_markdown_files(root):
        relative_path = path.relative_to(root).as_posix()
        violations.extend(ref_violations(relative_path, path.read_text(encoding="utf-8")))
    return violations


def main() -> int:
    root = repository_root()
    violations = find_violations(root)
    if not violations:
        print("Canonical Actions pinning guard: OK")
        return 0

    print("Canonical Actions pinning guard failed:", file=sys.stderr)
    for violation in violations:
        print(f"- {violation}", file=sys.stderr)
    print(
        "Pin canonical third-party GitHub Actions examples to a full commit SHA, reusing the "
        "same reviewed pin already used for that action/version in .github/workflows/*.yml.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
