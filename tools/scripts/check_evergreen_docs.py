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
import subprocess
import sys
from pathlib import Path

SEMVER = r"v?\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?"
PATH_VERSION = (
    r"v?\d+[.-]\d+[.-]\d+"
    r"(?:[-_][0-9A-Za-z][0-9A-Za-z._-]*?)?"
    r"(?:\+[0-9A-Za-z][0-9A-Za-z._-]*)?"
)
CLI_VERSION_ARG = rf"(?:{SEMVER}|[\"']{SEMVER}[\"'])"
PRODUCT_DOC_CONCEPT = (
    r"(?:migration(?:-to)?|upgrade(?:-to)?|upgrading|release(?:-notes?)?|"
    r"adopt(?:ion)?(?:-to)?|installation|install|quickstart|getting-started|"
    r"troubleshooting|reference-entrypoints|cli|capabilities)"
)
PRODUCT_GUIDE_LABEL = r"(?:Adopt(?:ion)?(?:\s+or\s+Upgrade)?|Upgrade|Migration)"
ARCHLINTERNET_GUIDE_IDENTITY = (
    r"(?:upgrade(?:\s+guide)?|migration(?:\s+guide)?|adoption(?:\s+guide)?|"
    r"release\s+notes?|reference\s+entrypoints?)"
)
PRODUCT_RELEASE_PATH_QUALIFIER = rf"(?:version|{PRODUCT_DOC_CONCEPT})"
PRODUCT_PATH_SEPARATOR = r"[-_./]"
VERSIONED_DOC_IDENTITY = re.compile(
    rf"(?i)(?:^|[/(`'\"]){PRODUCT_DOC_CONCEPT}{PRODUCT_PATH_SEPARATOR}{PATH_VERSION}\b"
)
EXPLICIT_PRODUCT_VERSION_PATH = re.compile(
    rf"(?i)(?:^|/)(?:"
    rf"archlinternet(?:[-_.]{PRODUCT_RELEASE_PATH_QUALIFIER})?{PRODUCT_PATH_SEPARATOR}{PATH_VERSION}(?:[-_.][^/]*)?"
    rf"|{PATH_VERSION}{PRODUCT_PATH_SEPARATOR}archlinternet(?:[-_.]{PRODUCT_RELEASE_PATH_QUALIFIER})?"
    rf")(?:/|\.md$|$)"
)
VERSION_FIRST_PRODUCT_DOC_PATH = re.compile(
    rf"(?i)(?:^|/){PATH_VERSION}{PRODUCT_PATH_SEPARATOR}"
    rf"{PRODUCT_DOC_CONCEPT}(?:[-_.][^/]*)?(?:/|\.md$|$)"
)
ROOT_README_VERSION = re.compile(
    rf"(?i)^README[^/]*[-_.]{PATH_VERSION}(?:[-_.]|$)"
)
HARDCODED_TOOL_PACKAGE_PIN = re.compile(
    rf"(?i)dotnet\s+tool\s+(?:install|update)\b"
    rf"(?=[^\n]*\bArchLinterNet(?:\.[A-Za-z0-9]+)+\b)"
    rf"(?=[^\n]*--version(?:\s+|=){CLI_VERSION_ARG})[^\n]*"
)
HARDCODED_LIBRARY_PACKAGE_PIN = re.compile(
    rf"(?i)dotnet\s+(?:add\s+package|package\s+add)\b"
    rf"(?=[^\n]*\bArchLinterNet(?:\.[A-Za-z0-9]+)+\b)"
    rf"(?=[^\n]*(?:--version|-v)(?:\s+|=){CLI_VERSION_ARG})[^\n]*"
)
HARDCODED_MSBUILD_PACKAGE_PIN = re.compile(
    rf"(?is)<(?:PackageReference|PackageVersion)\b"
    rf"(?=[^>]*\b(?:Include|Update)\s*=\s*[\"']ArchLinterNet(?:\.[^\"']+)+[\"'])"
    rf"(?=[^>]*\b(?:Version|VersionOverride)\s*=\s*[\"']{SEMVER}[\"'])[^>]*>"
)
HARDCODED_NESTED_MSBUILD_PACKAGE_PIN = re.compile(
    rf"(?is)<(?P<package_item>PackageReference|PackageVersion)\b"
    rf"(?=[^>]*\b(?:Include|Update)\s*=\s*[\"']ArchLinterNet(?:\.[^\"']+)+[\"'])[^>]*>"
    rf"(?:(?!</(?P=package_item)>).){{0,240}}?"
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
    rf"\bArchLinterNet\s+{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\b[^\n]{{0,40}}\b(?:package|release|version)\b"
    rf"|"
    rf"\b(?:current|public)\b[^\n]{{0,40}}\bArchLinterNet(?:'s)?\s+(?:package|release|version)\b[^\n]{{0,60}}\b{SEMVER}\b"
    rf"|"
    rf"\b(?:current|public)\b[^\n]{{0,40}}\b(?:package(?:\s+release)?|release|version)\s+(?:for|of)\s+ArchLinterNet\b[^\n]{{0,60}}\b{SEMVER}\b"
    rf"|"
    rf"\b{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\b[^\n]{{0,40}}\b(?:package(?:\s+release)?|release|version)\s+(?:for|of)\s+ArchLinterNet\b"
    rf"|"
    rf"\b{SEMVER}\b[^\n]{{0,80}}\b(?:current|public)\b[^\n]{{0,40}}\bArchLinterNet(?:'s)?\s+(?:package|release|version)\b"
    rf")"
)
VERSIONED_HEADING = re.compile(
    rf"(?im)^#{{1,6}}\s+(?:"
    rf"{PRODUCT_GUIDE_LABEL}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|Release\s+Notes?\s+{SEMVER}\b"
    rf"|Reference\s+Entrypoints?\s+{SEMVER}\b"
    rf"|(?:{PRODUCT_GUIDE_LABEL}|Release\s+Notes?)[^\n]{{0,30}}\bArchLinterNet\b[^\n]{{0,30}}\b{SEMVER}\b"
    rf"|ArchLinterNet(?:'s)?\s+{ARCHLINTERNET_GUIDE_IDENTITY}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|ArchLinterNet\s+{SEMVER}\b"
    rf"|\b{SEMVER}\b\s+(?:ArchLinterNet|{PRODUCT_GUIDE_LABEL}|Release\s+Notes?|Reference\s+Entrypoints?)\b"
    rf")"
)
VERSIONED_NAV_CONCEPT = re.compile(
    rf"(?i)^\s*-\s+[\"']?(?:"
    rf"{PRODUCT_GUIDE_LABEL}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|Release\s+Notes?\s+{SEMVER}\b"
    rf"|Reference\s+Entrypoints?\s+{SEMVER}\b"
    rf"|(?:{PRODUCT_GUIDE_LABEL}|Release\s+Notes?)[^:\n]{{0,30}}\bArchLinterNet\b[^:\n]{{0,30}}\b{SEMVER}\b"
    rf"|ArchLinterNet(?:'s)?\s+{ARCHLINTERNET_GUIDE_IDENTITY}(?:\s+(?:to|for))?\s+{SEMVER}\b"
    rf"|ArchLinterNet\s+{SEMVER}\b"
    rf"|\b{SEMVER}\b\s+(?:ArchLinterNet|{PRODUCT_GUIDE_LABEL}|Release\s+Notes?|Reference\s+Entrypoints?)\b"
    rf")"
)
ARCHLINTERNET_VERSIONED_NAV = re.compile(
    rf"(?i)^\s*-\s+[\"']?(?:(?:current|public)\s+)?ArchLinterNet(?:'s)?"
    rf"(?:\s+(?:release|version|package(?:\s+(?:release|version|line))?))?\s+{SEMVER}\b"
)
MARKDOWN_STRUCTURAL_LINE = re.compile(
    r"^[ \t]*(?:#{1,6}(?:[ \t]|$)|(?:`{3,}|~{3,})|\|)"
)
LIST_ITEM_LINE = re.compile(r"^[ \t]*(?:[-*+]|\d+[.)])[ \t](.*)$")
BLOCKQUOTE_LINE = re.compile(r"^[ \t]*>[ \t]?(.*)$")
README_EXCLUDED_PREFIXES = (
    ("docs", "internal"),
    ("openspec", "changes", "archive"),
)
FALLBACK_IGNORED_PARTS = {
    ".git",
    ".venv",
    ".pytest_cache",
    "TestResults",
    "artifacts",
    "bin",
    "node_modules",
    "obj",
}


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def normalize_soft_wrapped_prose(text: str) -> str:
    """Join Markdown soft wraps inside paragraphs/list items without crossing structure."""
    output: list[str] = []
    pending: list[str] = []
    pending_kind: str | None = None

    def flush_pending() -> None:
        nonlocal pending, pending_kind
        if pending:
            output.append(" ".join(pending))
            pending = []
            pending_kind = None

    for line in text.splitlines():
        quote_match = BLOCKQUOTE_LINE.match(line)
        if quote_match:
            content = quote_match.group(1)
            kind = "quote"
        else:
            content = line
            kind = "plain"

        list_match = LIST_ITEM_LINE.match(content)
        if list_match:
            flush_pending()
            item = list_match.group(1).strip()
            if item:
                pending = [item]
                pending_kind = f"{kind}:list"
            continue

        if not content.strip() or MARKDOWN_STRUCTURAL_LINE.match(content):
            flush_pending()
            output.append(content)
            continue

        if pending and pending_kind not in {kind, f"{kind}:list"}:
            flush_pending()
        if not pending:
            pending_kind = kind
        pending.append(content.strip())

    flush_pending()
    return "\n".join(output)


def is_excluded_readme(root: Path, path: Path) -> bool:
    parts = path.relative_to(root).parts
    return any(parts[: len(prefix)] == prefix for prefix in README_EXCLUDED_PREFIXES)


def repository_readme_files(root: Path) -> list[Path]:
    """Return tracked contributor README files, with a filesystem fallback for tests."""
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "ls-files", "-z"],
            check=True,
            capture_output=True,
            text=True,
        )
    except (FileNotFoundError, subprocess.CalledProcessError):
        candidates = [
            path
            for path in root.rglob("README*")
            if path.is_file()
            and not any(part in FALLBACK_IGNORED_PARTS for part in path.relative_to(root).parts)
        ]
    else:
        candidates = [
            root / relative
            for relative in result.stdout.split("\0")
            if relative
            and Path(relative).name.startswith("README")
            and (root / relative).is_file()
        ]

    return sorted(path for path in candidates if not is_excluded_readme(root, path))


def public_markdown_files(root: Path) -> list[Path]:
    docs_root = root / "docs"
    samples_root = root / "samples"
    files = repository_readme_files(root)
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
            command_text = re.sub(r"[\\`]\r?\n[ \t]*", " ", text)
            for pattern in (
                HARDCODED_TOOL_PACKAGE_PIN,
                HARDCODED_LIBRARY_PACKAGE_PIN,
            ):
                for match in pattern.finditer(command_text):
                    snippet = " ".join(match.group(0).split())
                    violations.append(
                        f"{relative}: pin ArchLinterNet package versions in repository package/tool metadata, not evergreen docs: '{snippet}'"
                    )
            for pattern in (
                HARDCODED_MSBUILD_PACKAGE_PIN,
                HARDCODED_NESTED_MSBUILD_PACKAGE_PIN,
                HARDCODED_TOOL_MANIFEST_PIN,
            ):
                for match in pattern.finditer(text):
                    snippet = " ".join(match.group(0).split())
                    violations.append(
                        f"{relative}: pin ArchLinterNet package versions in repository package/tool metadata, not evergreen docs: '{snippet}'"
                    )

        # Release-process examples may discuss product SemVer because versioning
        # is the subject there. All other evergreen surfaces still distinguish
        # product release prose from legitimate machine/schema/standard versions.
        if relative != "docs/reference/release-process.md":
            prose_text = normalize_soft_wrapped_prose(text)
            for pattern in (
                ARCHLINTERNET_RELEASE_PROSE,
                PACKAGE_LINE_PROSE,
                ARCHLINTERNET_STATUS_PROSE,
            ):
                for match in pattern.finditer(prose_text):
                    snippet = " ".join(match.group(0).split())
                    violations.append(
                        f"{relative}: product package SemVer is coupled to evergreen prose: '{snippet}'"
                    )
            for match in VERSIONED_HEADING.finditer(text):
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
        if (
            VERSIONED_DOC_IDENTITY.search(line)
            or VERSIONED_NAV_CONCEPT.search(line)
            or ARCHLINTERNET_VERSIONED_NAV.search(line)
        ):
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
