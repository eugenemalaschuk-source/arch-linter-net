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
DOTTED_OR_DASHED_VERSION = r"v?\d+[.-]\d+[.-]\d+"
PRODUCT_STYLE_DOC_IDENTITY = r"(?:migration-to|upgrade-to|release-notes?|adopt(?:ion)?-to)"
PRODUCT_GUIDE_LABEL = r"(?:Adopt(?:ion)?(?:\s+or\s+Upgrade)?|Upgrade|Migration)"
VERSIONED_DOC_IDENTITY = re.compile(
    rf"(?i)(?:^|[/(`'\"\s]){PRODUCT_STYLE_DOC_IDENTITY}[-_]{DOTTED_OR_DASHED_VERSION}\b"
)
EXPLICIT_PRODUCT_VERSION_PATH = re.compile(
    rf"(?i)(?:^|/)(?:"
    rf"archlinternet[^/]*?[-_]{DOTTED_OR_DASHED_VERSION}"
    rf"|{DOTTED_OR_DASHED_VERSION}[^/]*?[-_]archlinternet"
    rf")[^/]*(?:/|\.md$)"
)
VERSION_FIRST_PRODUCT_DOC_PATH = re.compile(
    rf"(?i)(?:^|/){DOTTED_OR_DASHED_VERSION}[-_]"
    rf"(?:migration|upgrade|release(?:-notes?)?|adopt(?:ion)?)"
    rf"(?:[-_.][^/]*)?(?:/|\.md$)"
)
ROOT_README_VERSION = re.compile(
    rf"(?i)^README[^/]*[-_.]{DOTTED_OR_DASHED_VERSION}(?:[-_.]|$)"
)
HARDCODED_TOOL_PACKAGE_PIN = re.compile(
    rf"(?i)dotnet\s+tool\s+(?:install|update)\b"
    rf"(?=[^\n]*\bArchLinterNet(?:\.[A-Za-z0-9]+)+\b)"
    rf"(?=[^\n]*--version\s+{SEMVER}\b)[^\n]*"
)
HARDCODED_LIBRARY_PACKAGE_PIN = re.compile(
    rf"(?i)dotnet\s+(?:add\s+package|package\s+add)\b"
    rf"(?=[^\n]*\bArchLinterNet(?:\.[A-Za-z0-9]+)+\b)"
    rf"(?=[^\n]*(?:--version|-v)\s+{SEMVER}\b)[^\n]*"
)
HARDCODED_MSBUILD_PACKAGE_PIN = re.compile(
    rf"(?i)<(?:PackageReference|PackageVersion)\b"
    rf"(?=[^>\n]*\b(?:Include|Update)=[\"']ArchLinterNet(?:\.[^\"']+)+[\"'])"
    rf"(?=[^>\n]*\b(?:Version|VersionOverride)=[\"']{SEMVER}[\"'])[^>\n]*>"
)
HARDCODED_NESTED_MSBUILD_PACKAGE_PIN = re.compile(
    rf"(?is)<PackageReference\b"
    rf"(?=[^>]*\b(?:Include|Update)=[\"']ArchLinterNet(?:\.[^\"']+)+[\"'])[^>]*>"
    rf"(?:(?!</PackageReference>).){{0,240}}?"
    rf"<(?:Version|VersionOverride)>\s*{SEMVER}\s*</(?:Version|VersionOverride)>"
)
HARDCODED_TOOL_MANIFEST_PIN = re.compile(
    rf"(?is)[\"']ArchLinterNet(?:\.[A-Za-z0-9]+)+[\"']\s*:\s*\{{"
    rf"(?:(?!\}}).){{0,240}}?[\"']version[\"']\s*:\s*[\"']{SEMVER}[\"']"
)
ARCHLINTERNET_RELEASE_PROSE = re.compile(
    rf"(?i)\bArchLinterNet(?:'s)?\s+"
    rf"(?:package\s+version|package\s+release|version|release|package\s+line|public\s+package\s+line|public\s+adoption\s+package\s+line)\b"
    rf"[^\n]{{0,60}}\b{SEMVER}\b"
)
PACKAGE_LINE_PROSE = re.compile(
    rf"(?i)(?:"
    rf"\b{SEMVER}\b[^\n]{{0,80}}\bpublic\s+adoption\s+package\s+line\b"
    rf"|"
    rf"\b{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\s+package\s+line\b[^\n]{{0,60}}\b(?:for\s+)?ArchLinterNet\b"
    rf"|"
    rf"\b(?:current|public)\s+package\s+line\b[^\n]{{0,60}}\b(?:for\s+)?ArchLinterNet\b[^\n]{{0,60}}\b{SEMVER}\b"
    rf")"
)
ARCHLINTERNET_STATUS_PROSE = re.compile(
    rf"(?i)(?:"
    rf"\bArchLinterNet\s+{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\b[^\n]{{0,40}}\b(?:package|release)\b"
    rf"|"
    rf"\b(?:current|public)\b[^\n]{{0,40}}\bArchLinterNet(?:'s)?\s+(?:package|release)\b[^\n]{{0,60}}\b{SEMVER}\b"
    rf"|"
    rf"\b(?:current|public)\b[^\n]{{0,40}}\b(?:package(?:\s+release)?|release)\s+(?:for|of)\s+ArchLinterNet\b[^\n]{{0,60}}\b{SEMVER}\b"
    rf"|"
    rf"\b{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\b[^\n]{{0,40}}\b(?:package(?:\s+release)?|release)\s+(?:for|of)\s+ArchLinterNet\b"
    rf")"
)
VERSIONED_HEADING = re.compile(
    rf"(?im)^#{{1,6}}\s+(?:"
    rf"{PRODUCT_GUIDE_LABEL}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|Release\s+Notes?\s+{SEMVER}\b"
    rf"|Reference\s+Entrypoints?\s+{SEMVER}\b"
    rf"|(?:{PRODUCT_GUIDE_LABEL}|Release\s+Notes?)[^\n]{{0,30}}\bArchLinterNet\b[^\n]{{0,30}}\b{SEMVER}\b"
    rf"|ArchLinterNet\s+{SEMVER}\b"
    rf"|\b{SEMVER}\b\s+(?:ArchLinterNet|{PRODUCT_GUIDE_LABEL}|Release\s+Notes?|Reference\s+Entrypoints?)\b"
    rf")"
)
VERSIONED_NAV_CONCEPT = re.compile(
    rf"(?i)^\s*-\s+(?:"
    rf"{PRODUCT_GUIDE_LABEL}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|Release\s+Notes?\s+{SEMVER}\b"
    rf"|Reference\s+Entrypoints?\s+{SEMVER}\b"
    rf"|(?:{PRODUCT_GUIDE_LABEL}|Release\s+Notes?)[^:\n]{{0,30}}\bArchLinterNet\b[^:\n]{{0,30}}\b{SEMVER}\b"
    rf"|ArchLinterNet\s+{SEMVER}\b"
    rf"|\b{SEMVER}\b\s+(?:ArchLinterNet|{PRODUCT_GUIDE_LABEL}|Release\s+Notes?|Reference\s+Entrypoints?)\b"
    rf")"
)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def public_markdown_files(root: Path) -> list[Path]:
    docs_root = root / "docs"
    samples_root = root / "samples"
    files = [path for path in root.glob("README*") if path.is_file()]
    if docs_root.exists():
        files.extend(
            path
            for path in docs_root.rglob("*.md")
            if "internal" not in path.relative_to(docs_root).parts
        )
    if samples_root.exists():
        files.extend(path for path in samples_root.rglob("*.md") if path.is_file())
    return sorted(set(files))


def path_violations(root: Path, paths: list[Path]) -> list[str]:
    violations: list[str] = []
    for path in paths:
        relative = path.relative_to(root).as_posix()
        if (
            ROOT_README_VERSION.search(relative)
            or VERSIONED_DOC_IDENTITY.search(relative)
            or EXPLICIT_PRODUCT_VERSION_PATH.search(relative)
            or VERSION_FIRST_PRODUCT_DOC_PATH.search(relative)
        ):
            violations.append(
                f"{relative}: ArchLinterNet product release must not be a public docs path identity"
            )
    return violations


def content_violations(root: Path, paths: list[Path]) -> list[str]:
    violations: list[str] = []
    for path in paths:
        relative = path.relative_to(root).as_posix()
        text = path.read_text(encoding="utf-8")

        for match in VERSIONED_DOC_IDENTITY.finditer(text):
            snippet = match.group(0)
            violations.append(
                f"{relative}: version-named evergreen docs reference '{snippet}'"
            )

        if relative != "docs/reference/release-process.md":
            for pattern in (
                HARDCODED_TOOL_PACKAGE_PIN,
                HARDCODED_LIBRARY_PACKAGE_PIN,
                HARDCODED_MSBUILD_PACKAGE_PIN,
                HARDCODED_NESTED_MSBUILD_PACKAGE_PIN,
                HARDCODED_TOOL_MANIFEST_PIN,
            ):
                for match in pattern.finditer(text):
                    snippet = " ".join(match.group(0).split())
                    violations.append(
                        f"{relative}: pin ArchLinterNet package versions in repository package/tool metadata, not evergreen docs: '{snippet}'"
                    )

        # Release-process and schema-reference pages may discuss SemVer or exact
        # immutable machine identifiers because versioning is the subject there.
        if relative not in {
            "docs/reference/release-process.md",
            "docs/reference/yaml-schema.md",
        }:
            for pattern in (
                ARCHLINTERNET_RELEASE_PROSE,
                PACKAGE_LINE_PROSE,
                ARCHLINTERNET_STATUS_PROSE,
                VERSIONED_HEADING,
            ):
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
        if VERSIONED_DOC_IDENTITY.search(line) or VERSIONED_NAV_CONCEPT.search(line):
            violations.append(
                f"mkdocs.yml:{line_number}: public navigation must use a version-neutral ArchLinterNet product identity: {line.strip()}"
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
