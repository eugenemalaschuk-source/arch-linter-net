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
    coverage_contracts_configured = bool(report.get("coverage_summary"))
    status = overall_status(report)
    status_badge = "✅ pass" if status == "pass" else "❌ fail"

    lines = [
        "## Architecture coverage",
        "",
        f"**Status:** {status_badge}",
        "",
        "| Metric | Count |",
        "| --- | --- |",
        f"| Covered | {totals['covered']} |",
        f"| Excluded | {totals['excluded']} |",
        f"| Uncovered | {totals['uncovered']} |",
        f"| Stale | {totals['stale']} |",
        f"| Unknown | {totals['unknown']} |",
    ]

    if not coverage_contracts_configured:
        lines.append("")
        lines.append(
            "> **Note:** the policy defines no coverage contracts (`strict_coverage`/`audit_coverage`). "
            "These zeros mean coverage is unconfigured, not that everything is covered."
        )

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


def find_enclosing_csproj(file_path: Path, repo_root: Path) -> Path | None:
    root_resolved = repo_root.resolve()
    absolute = file_path if file_path.is_absolute() else (root_resolved / file_path)
    current = absolute.parent

    while True:
        csproj_matches = list(current.glob("*.csproj"))
        if csproj_matches:
            return csproj_matches[0]
        if current == root_resolved or current == current.parent:
            return None
        current = current.parent


def detect_project_path(csproj_path: Path, repo_root: Path) -> str:
    return csproj_path.resolve().relative_to(repo_root.resolve()).as_posix()


def detect_assembly_name(csproj_path: Path) -> str:
    try:
        text = csproj_path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return csproj_path.stem

    match = re.search(r"<AssemblyName>\s*([^<\s]+)\s*</AssemblyName>", text)
    return match.group(1) if match else csproj_path.stem


def build_coverage_index(report: dict) -> dict[tuple[str, str], dict]:
    index: dict[tuple[str, str], dict] = {}
    for entry in report.get("coverage_summary", []) or []:
        scope = entry.get("scope")
        if scope not in KNOWN_SCOPES:
            continue
        for bucket, state in (
            ("covered_items", "covered"),
            ("excluded_items", "excluded"),
            ("uncovered_items", "uncovered"),
            ("stale_items", "stale"),
            ("unknown_items", "unknown"),
        ):
            for item in entry.get(bucket, []) or []:
                key = (scope, item.get("item"))
                index[key] = {"state": state, "evidence": item.get("evidence") or item.get("reason")}
    return index


def configured_scopes(report: dict) -> set[str]:
    """Coverage scopes (namespace/project/assembly) that have at least one configured
    coverage contract in this run. A scope with no configured contract carries no
    evidence either way, so the new-code classifier must not synthesize a finding for
    it — that would report an "unknown"/"requires policy update" item purely because
    the repository's policy doesn't define that scope, not because of anything in the
    pull request."""
    return {
        entry.get("scope")
        for entry in report.get("coverage_summary", []) or []
        if entry.get("scope") in KNOWN_SCOPES
    }


def _classify_unit(scope: str, unit: str | None, coverage_index: dict[tuple[str, str], dict]) -> ChangedUnit | None:
    if unit is None:
        return None

    match = coverage_index.get((scope, unit))
    if match is not None:
        return ChangedUnit(scope=scope, unit=unit, state=match["state"], evidence=match.get("evidence"))

    # The unit was detected, but it doesn't appear in any contract's
    # covered_items/excluded_items/uncovered_items/stale_items/unknown_items —
    # meaning no configured coverage contract's roots/scope actually covers this
    # unit. Absence of evidence is not proof of coverage, so report unknown.
    return ChangedUnit(scope=scope, unit=unit, state="unknown")


def classify_changed_file(
    file_path: str,
    repo_root: Path,
    coverage_index: dict[tuple[str, str], dict],
    scopes: set[str],
) -> list[ChangedUnit]:
    """Classify a changed file against every coverage scope (namespace, project,
    assembly) that the policy actually configures, independently. A file can be covered
    in one scope and uncovered/unknown in another (e.g. a covered namespace inside an
    uncovered project) — reporting only the first match would hide that gap, so every
    applicable unit is returned. A scope the policy doesn't configure at all is skipped
    entirely rather than synthesized as "unknown", since that would just restate "this
    repository has no project-coverage contract" on every changed file."""
    path_obj = Path(file_path)
    namespace = detect_namespace(repo_root / path_obj) if "namespace" in scopes else None
    csproj_path = find_enclosing_csproj(path_obj, repo_root) if ("project" in scopes or "assembly" in scopes) else None

    project_path = detect_project_path(csproj_path, repo_root) if csproj_path is not None and "project" in scopes else None
    assembly_name = detect_assembly_name(csproj_path) if csproj_path is not None and "assembly" in scopes else None

    units = [
        _classify_unit("namespace", namespace, coverage_index),
        _classify_unit("project", project_path, coverage_index),
        _classify_unit("assembly", assembly_name, coverage_index),
    ]
    applicable = [unit for unit in units if unit is not None]

    if not applicable:
        return [ChangedUnit(scope="unknown", unit=None, state="unknown")]

    return applicable


def render_new_code_section(file_units: dict[str, list[ChangedUnit]]) -> str:
    all_units = [unit for units in file_units.values() for unit in units]

    # Dedupe by (scope, unit): the same project/assembly/namespace can be the
    # classification target for multiple changed files.
    unique_units: dict[tuple[str, str | None], ChangedUnit] = {}
    for unit in all_units:
        unique_units[(unit.scope, unit.unit)] = unit

    covered_count = sum(1 for unit in unique_units.values() if unit.state == "covered")
    uncovered_count = sum(1 for unit in unique_units.values() if unit.state in ("uncovered", "stale", "excluded"))
    unknown_count = sum(1 for unit in unique_units.values() if unit.state == "unknown")

    lines = [
        "",
        "### New-code coverage",
        "",
        "| Metric | Count |",
        "| --- | --- |",
        f"| Changed first-party files | {len(file_units)} |",
        f"| Changed namespaces/projects/assemblies covered | {covered_count} |",
        f"| Changed namespaces/projects/assemblies uncovered | {uncovered_count} |",
        f"| Requiring policy update | {unknown_count if unknown_count else 'none'} |",
    ]

    files_with_problems = {
        file: [unit for unit in units if unit.state != "covered"]
        for file, units in file_units.items()
        if any(unit.state != "covered" for unit in units)
    }

    if files_with_problems:
        lines.append("")
        lines.append("Items needing attention:")
        lines.append("")
        for file, problem_units in files_with_problems.items():
            for unit in problem_units:
                label = unit.unit or file
                lines.append(f"- `{file}` — `{label}` ({unit.scope}): **{unit.state}**")

    return "\n".join(lines)


def render_diff_unavailable_section() -> str:
    return (
        "\n### New-code coverage\n\n"
        "> **Unavailable:** the changed-files diff could not be computed for this run "
        "(e.g. a `git diff`/fetch failure). This is reported explicitly rather than as "
        "zero changed files, since a diff failure is not the same as an empty diff."
    )


def render_report(report: dict, changed_files: list[str] | None, repo_root: Path, diff_failed: bool = False) -> str:
    sections = [render_summary_markdown(report)]

    if diff_failed:
        sections.append(render_diff_unavailable_section())
    elif changed_files is not None:
        coverage_index = build_coverage_index(report)
        scopes = configured_scopes(report)
        file_units = {file: classify_changed_file(file, repo_root, coverage_index, scopes) for file in changed_files}
        sections.append(render_new_code_section(file_units))

    return "\n".join(sections) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a Markdown architecture coverage report from CLI JSON output.")
    parser.add_argument("json_path", type=Path, help="Path to the strict-mode architecture JSON output.")
    parser.add_argument("--changed-files", type=Path, default=None, help="Path to a file listing changed first-party files, one per line.")
    parser.add_argument("--repo-root", type=Path, default=Path("."), help="Repository root used to resolve changed file paths.")
    parser.add_argument("--output", type=Path, default=None, help="Path to write the Markdown report. Defaults to stdout.")
    parser.add_argument(
        "--diff-status",
        choices=("ok", "failed"),
        default="ok",
        help="Pass 'failed' when the changed-files diff computation itself failed (e.g. git diff/fetch error), "
        "so the report says diff-unavailable instead of silently reporting zero changed files.",
    )

    args = parser.parse_args()

    report = load_coverage(args.json_path)
    diff_failed = args.diff_status == "failed"

    changed_files: list[str] | None = None
    if not diff_failed and args.changed_files is not None and args.changed_files.exists():
        changed_files = [line.strip() for line in args.changed_files.read_text(encoding="utf-8").splitlines() if line.strip()]

    markdown = render_report(report, changed_files, args.repo_root, diff_failed=diff_failed)

    if args.output is not None:
        args.output.write_text(markdown, encoding="utf-8")
    else:
        print(markdown)

    return 0


if __name__ == "__main__":
    sys.exit(main())
