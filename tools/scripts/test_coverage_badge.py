#!/usr/bin/env python3
from __future__ import annotations

import argparse
import glob
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def merge_line_coverage(cobertura_paths: list[Path]) -> tuple[int, int]:
    """Merge per-line hit counts across multiple cobertura reports (one per test
    project) by taking the maximum hit count seen for each (file, line) pair —
    a naive sum of each report's own covered/valid totals double-counts lines in
    source shared across test projects (e.g. Core code exercised by both
    Core.Tests and Cli.Tests)."""
    line_hits: dict[tuple[str, int], int] = {}

    for path in cobertura_paths:
        tree = ET.parse(path)
        for cls in tree.getroot().iter("class"):
            filename = cls.get("filename")
            lines_element = cls.find("lines")
            if lines_element is None:
                continue
            for line in lines_element:
                line_number = int(line.get("number"))
                hits = int(line.get("hits"))
                key = (filename, line_number)
                line_hits[key] = max(line_hits.get(key, 0), hits)

    total = len(line_hits)
    covered = sum(1 for hits in line_hits.values() if hits > 0)
    return covered, total


def badge_color(percentage: float) -> str:
    if percentage >= 80:
        return "brightgreen"
    if percentage >= 60:
        return "yellow"
    return "red"


def render_badge_markdown(percentage: float, link: str) -> str:
    # shields.io static badge path segments: a literal "%" must be escaped as "%25".
    message = f"{percentage:.0f}%25"
    color = badge_color(percentage)
    return f"[![Test coverage](https://img.shields.io/badge/test%20coverage-{message}-{color})]({link})"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Merge cobertura coverage reports from multiple test projects and report overall line coverage."
    )
    parser.add_argument(
        "--reports-glob",
        default="test-results/**/coverage.cobertura.xml",
        help="Glob pattern (relative to cwd) matching cobertura XML files to merge.",
    )
    parser.add_argument(
        "--badge-link",
        default="https://github.com/eugenemalaschuk-source/arch-linter-net/actions/workflows/ci.yml",
        help="Link target for the generated badge Markdown.",
    )
    args = parser.parse_args()

    cobertura_paths = [Path(path) for path in glob.glob(args.reports_glob, recursive=True)]
    if not cobertura_paths:
        print("No cobertura reports found. Run `make test-coverage` first.", file=sys.stderr)
        return 1

    covered, total = merge_line_coverage(cobertura_paths)
    percentage = (covered / total * 100) if total else 0.0

    print(f"Lines covered: {covered}/{total} ({percentage:.1f}%)")
    print(render_badge_markdown(percentage, args.badge_link))

    return 0


if __name__ == "__main__":
    sys.exit(main())
