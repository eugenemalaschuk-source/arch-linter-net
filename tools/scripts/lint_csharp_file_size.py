#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from dataclasses import dataclass


DEFAULT_IGNORED_DIRS = {
    ".git",
    ".idea",
    ".vscode",
    "obj",
    "bin",
    "Build",
    "Builds",
    "Logs",
    "Packages",
}

DEFAULT_IGNORED_SUFFIXES = (
    ".g.cs",
    ".generated.cs",
    ".designer.cs",
    ".Designer.cs",
    ".AssemblyInfo.cs",
)

YELLOW = "\033[33m"
RED = "\033[31m"
RESET = "\033[0m"


@dataclass(frozen=True)
class LintRule:
    label: str
    warn_lines: int
    error_lines: int


def should_skip(path: Path, ignored_dirs: set[str]) -> bool:
    if any(part in ignored_dirs for part in path.parts):
        return True

    return any(path.name.endswith(suffix) for suffix in DEFAULT_IGNORED_SUFFIXES)


def count_lines(path: Path) -> int:
    with path.open("r", encoding="utf-8", errors="ignore") as file:
        return sum(1 for _ in file)


def iter_files_with_suffix(roots: list[Path], ignored_dirs: set[str], suffix: str):
    for root in roots:
        if not root.exists():
            continue

        if root.is_file():
            if root.suffix == suffix and not should_skip(root, ignored_dirs):
                yield root
            continue

        if root.is_dir():
            for path in root.rglob(f"*{suffix}"):
                if not should_skip(path, ignored_dirs):
                    yield path


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Checks source/documentation file sizes for AI-safe decomposition. "
            "Warns when files should be split and fails when they are too large."
        )
    )
    parser.add_argument(
        "roots",
        nargs="*",
        default=["src", "tests", "docs"],
        help="Files or directories to scan. Defaults to: src tests docs",
    )
    parser.add_argument(
        "--warn-lines",
        type=int,
        default=500,
        help="Print warning when file has at least this many lines. Default: 500",
    )
    parser.add_argument(
        "--error-lines",
        type=int,
        default=800,
        help="Fail when file has more than this many lines. Default: 800",
    )
    parser.add_argument(
        "--ignore-dir",
        action="append",
        default=[],
        help="Additional directory name to ignore. Can be passed multiple times.",
    )
    parser.add_argument(
        "--md-warn-lines",
        type=int,
        default=600,
        help="Print warning when .md file has at least this many lines. Default: 600",
    )
    parser.add_argument(
        "--md-error-lines",
        type=int,
        default=1000,
        help="Fail when .md file has more than this many lines. Default: 1000",
    )

    args = parser.parse_args()

    ignored_dirs = DEFAULT_IGNORED_DIRS | set(args.ignore_dir)
    roots = [Path(root) for root in args.roots]

    rules = {
        ".cs": LintRule("C#", args.warn_lines, args.error_lines),
        ".md": LintRule("Docs (.md)", args.md_warn_lines, args.md_error_lines),
    }

    all_warnings: list[tuple[str, int, Path]] = []
    all_errors: list[tuple[str, int, Path]] = []

    for suffix, rule in rules.items():
        warnings: list[tuple[int, Path]] = []
        errors: list[tuple[int, Path]] = []

        for path in sorted(iter_files_with_suffix(roots, ignored_dirs, suffix)):
            lines = count_lines(path)

            if lines > rule.error_lines:
                errors.append((lines, path))
            elif lines >= rule.warn_lines:
                warnings.append((lines, path))

        if warnings:
            print("")
            print(f"{YELLOW}{rule.label} decomposition warnings: files with >= {rule.warn_lines} lines{RESET}")
            for lines, path in warnings:
                print(f"  WARN  {lines:4d} lines  {path}")
                all_warnings.append((rule.label, lines, path))

        if errors:
            print("")
            print(f"{RED}{rule.label} decomposition required: files with > {rule.error_lines} lines{RESET}")
            for lines, path in errors:
                print(f"  ERROR {lines:4d} lines  {path}")
                all_errors.append((rule.label, lines, path))

    if all_errors:
        print("")
        print("Large files are not AI-safe for targeted edits.")
        print("Decompose them into smaller files with a single responsibility.")
        print("")
        return 1

    if not all_warnings:
        print("Size lint passed: no files over warning thresholds.")
    else:
        print("")
        print("Size lint passed with warnings.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
