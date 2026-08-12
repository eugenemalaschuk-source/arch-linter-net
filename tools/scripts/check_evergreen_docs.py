#!/usr/bin/env python3
"""Reject ArchLinterNet product-release SemVer as an evergreen docs identity.

This intentionally does not ban version numbers in general. Machine/document
contract versions (finding/v1, schema IDs), standards (SARIF 2.1.0), TFMs, and
release-process examples are legitimate. The guard targets the repository
practice where a product package release becomes a permanent page, route,
navigation label, or copy-paste install identity.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

SEMVER = r"v?\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?"
PATH_SEMVER = re.compile(r"(?i)(?:^|[-_/])v?\d+(?:[.-])\d+(?:[.-])\d+(?:[-_./]|$)")
VERSIONED_DOC_IDENTITY = re.compile(
    r"(?i)(?:migration-to|upgrade-to|release-notes?|adopt(?:ion)?-to)[-_]v?\d+[-.]\d+[-.]\d+"
)
HARDCODED_TOOL_INSTALL = re.compile(
    rf"(?i)ArchLinterNet\.Cli\s+--version\s+{SEMVER}"
)
PRODUCT_RELEASE_PROSE = re.compile(
    rf"(?i)(?:ArchLinterNet|package)\s+(?:version|release|package\s+line|public\s+package\s+line|public\s+adoption\s+package\s+line)"
    rf"[^\n]{{0,80}}\b{SEMVER}\b"
)
CURRENT_LINE_PROSE = re.compile(
    rf"(?i)\b(?:current|public)\b[^\n]{{0,60}}\b(?:package|release)\b[^\n]{{0,60}}\b{SEMVER}\b"
)
VERSIONED_HEADING = re.compile(
    rf"(?im)^#{{1,6}}\s+[^\n]*(?:ArchLinterNet|Adopt|Upgrade|Migration|Release\s+Notes?)[^\n]*\b{SEMVER}\b"
)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def public_markdown_files(root: Path) -> list[Path]:
    docs_root = root / "docs"
    files = [root / "README.md"]
    files.extend(
        path
        for path in docs_root.rglob("*.md")
        if "internal" not in path.relative_to(docs_root).parts
    )
    return sorted(files)


def path_violations(root: Path, paths: list[Path]) -> list[str]:
    violations: list[str] = []
    for path in paths:
        relative = path.relative_to(root).as_posix()
        if relative == "README.md":
            continue
        if VERSIONED_DOC_IDENTITY.search(relative) or PATH_SEMVER.search(relative):
            violations.append(
                f"{relative}: product release SemVer must not be a public docs path identity"
            )
    return violations


def content_violations(root: Path, paths: list[Path]) -> list[str]:
    violations: list[str] = []
    for path in paths:
        relative = path.relative_to(root).as_posix()
        text = path.read_text(encoding="utf-8")

        for match in VERSIONED_DOC_IDENTITY.finditer(text):
            violations.append(
                f"{relative}: version-named evergreen docs reference '{match.group(0)}'"
            )

        for match in HARDCODED_TOOL_INSTALL.finditer(text):
            violations.append(
                f"{relative}: pin package versions in the tool manifest, not evergreen docs: '{match.group(0)}'"
            )

        # Release-process and schema-reference pages may discuss SemVer or exact
        # immutable machine identifiers because versioning is the subject there.
        if relative not in {
            "docs/reference/release-process.md",
            "docs/reference/yaml-schema.md",
        }:
            for pattern in (PRODUCT_RELEASE_PROSE, CURRENT_LINE_PROSE, VERSIONED_HEADING):
                for match in pattern.finditer(text):
                    snippet = " ".join(match.group(0).split())
                    violations.append(
                        f"{relative}: product package SemVer is coupled to evergreen prose: '{snippet}'"
                    )

    return violations


def navigation_violations(root: Path) -> list[str]:
    path = root / "mkdocs.yml"
    text = path.read_text(encoding="utf-8")
    in_nav = False
    violations: list[str] = []

    for line_number, line in enumerate(text.splitlines(), start=1):
        if line == "nav:":
            in_nav = True
            continue
        if in_nav and line and not line.startswith(" "):
            in_nav = False
        if not in_nav:
            continue
        if VERSIONED_DOC_IDENTITY.search(line) or re.search(SEMVER, line):
            violations.append(
                f"mkdocs.yml:{line_number}: public navigation must use a version-neutral identity: {line.strip()}"
            )

    return violations


def find_violations(root: Path) -> list[str]:
    paths = public_markdown_files(root)
    return [
        *path_violations(root, paths),
        *content_violations(root, paths),
        *navigation_violations(root),
    ]


def main() -> int:
    root = repository_root()
    violations = find_violations(root)
    if not violations:
        print("Evergreen docs guard: OK")
        return 0

    print("Evergreen docs guard failed:", file=sys.stderr)
    for violation in violations:
        print(f"- {violation}", file=sys.stderr)
    print(
        "Use durable page/navigation concepts. Keep product release history in GitHub releases/tags/issues; "
        "retain real machine/standard versions only where they are the contract.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
