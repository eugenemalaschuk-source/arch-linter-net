#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path

NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)", re.MULTILINE)

KNOWN_SCOPES = ("namespace", "project", "assembly")


@dataclass(frozen=True)
class ChangedUnit:
    file: str
    scope: str
    unit: str | None
    state: str
    evidence: str | None = None


def load_coverage(json_path: Path) -> dict:
    with json_path.open("r", encoding="utf-8") as file:
        return json.load(file)


def overall_status(report: dict) -> str:
    return "pass" if report.get("passed", False) else "fail"


def total_counts(report: dict) -> dict[str, int]:
    totals = {"covered": 0, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0}
    for entry in report.get("coverage_summary", []) or []:
        counts = entry.get("counts", {})
        for key in totals:
            totals[key] += int(counts.get(key, 0))
    return totals


def render_summary_markdown(report: dict) -> str:
    totals = total_counts(report)
    lines = [
        "Architecture coverage",
        f"Status: {overall_status(report)}",
        f"Covered: {totals['covered']}",
        f"Excluded: {totals['excluded']}",
        f"Uncovered: {totals['uncovered']}",
        f"Stale: {totals['stale']}",
        f"Unknown: {totals['unknown']}",
    ]
    return "\n".join(lines)


def detect_namespace(file_path: Path) -> str | None:
    if not file_path.exists():
        return None

    try:
        text = file_path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return None

    match = NAMESPACE_RE.search(text)
    return match.group(1) if match else None


def detect_project(file_path: Path, repo_root: Path) -> str | None:
    root_resolved = repo_root.resolve()
    absolute = file_path if file_path.is_absolute() else (root_resolved / file_path)
    current = absolute.parent

    while True:
        csproj_matches = list(current.glob("*.csproj"))
        if csproj_matches:
            return csproj_matches[0].stem
        if current == root_resolved or current == current.parent:
            return None
        current = current.parent


def build_coverage_index(report: dict) -> dict[tuple[str, str], dict]:
    index: dict[tuple[str, str], dict] = {}
    for entry in report.get("coverage_summary", []) or []:
        scope = entry.get("scope")
        if scope not in KNOWN_SCOPES:
            continue
        for bucket, state in (
            ("excluded_items", "excluded"),
            ("uncovered_items", "uncovered"),
            ("stale_items", "stale"),
            ("unknown_items", "unknown"),
        ):
            for item in entry.get(bucket, []) or []:
                key = (scope, item.get("item"))
                index[key] = {"state": state, "evidence": item.get("evidence") or item.get("reason")}
    return index


def classify_changed_file(file_path: str, repo_root: Path, coverage_index: dict[tuple[str, str], dict]) -> ChangedUnit:
    path_obj = Path(file_path)
    namespace = detect_namespace(repo_root / path_obj)
    project = detect_project(path_obj, repo_root)

    for scope, unit in (("namespace", namespace), ("project", project), ("assembly", project)):
        if unit is None:
            continue
        match = coverage_index.get((scope, unit))
        if match is not None:
            return ChangedUnit(file=file_path, scope=scope, unit=unit, state=match["state"], evidence=match.get("evidence"))

    # A namespace/project was detected but coverage_summary has no entry proving it is
    # covered by an active contract. Absence of a problem entry is not proof of coverage,
    # so report unknown rather than assuming covered.
    if namespace is not None:
        return ChangedUnit(file=file_path, scope="namespace", unit=namespace, state="unknown")
    if project is not None:
        return ChangedUnit(file=file_path, scope="project", unit=project, state="unknown")

    return ChangedUnit(file=file_path, scope="unknown", unit=None, state="unknown")


def render_new_code_section(changed_units: list[ChangedUnit]) -> str:
    problems = [unit for unit in changed_units if unit.state != "covered"]

    covered_count = sum(1 for unit in changed_units if unit.state == "covered")
    uncovered_count = sum(1 for unit in changed_units if unit.state in ("uncovered", "stale", "excluded"))
    unknown_count = sum(1 for unit in changed_units if unit.state == "unknown")

    lines = [
        "",
        "New-code coverage",
        f"Changed first-party files: {len(changed_units)}",
        f"Changed namespaces/projects/assemblies covered: {covered_count}",
        f"Changed namespaces/projects/assemblies uncovered: {uncovered_count}",
        f"Changed items requiring policy update: {unknown_count if unknown_count else 'none'}",
    ]

    if problems:
        lines.append("")
        for unit in problems:
            label = unit.unit or unit.file
            lines.append(f"  - {label} ({unit.scope}): {unit.state}")

    return "\n".join(lines)


def render_report(report: dict, changed_files: list[str] | None, repo_root: Path) -> str:
    sections = [render_summary_markdown(report)]

    if changed_files is not None:
        coverage_index = build_coverage_index(report)
        changed_units = [classify_changed_file(file, repo_root, coverage_index) for file in changed_files]
        sections.append(render_new_code_section(changed_units))

    return "\n".join(sections) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a Markdown architecture coverage report from CLI JSON output.")
    parser.add_argument("json_path", type=Path, help="Path to the strict-mode architecture JSON output.")
    parser.add_argument("--changed-files", type=Path, default=None, help="Path to a file listing changed first-party files, one per line.")
    parser.add_argument("--repo-root", type=Path, default=Path("."), help="Repository root used to resolve changed file paths.")
    parser.add_argument("--output", type=Path, default=None, help="Path to write the Markdown report. Defaults to stdout.")

    args = parser.parse_args()

    report = load_coverage(args.json_path)

    changed_files: list[str] | None = None
    if args.changed_files is not None and args.changed_files.exists():
        changed_files = [line.strip() for line in args.changed_files.read_text(encoding="utf-8").splitlines() if line.strip()]

    markdown = render_report(report, changed_files, args.repo_root)

    if args.output is not None:
        args.output.write_text(markdown, encoding="utf-8")
    else:
        print(markdown)

    return 0


if __name__ == "__main__":
    sys.exit(main())
