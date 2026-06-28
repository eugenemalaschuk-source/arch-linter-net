from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from architecture_coverage_report import (  # noqa: E402
    build_coverage_index,
    classify_changed_file,
    load_coverage,
    render_new_code_section,
    render_report,
    render_summary_markdown,
    total_counts,
)


def make_report(passed: bool, coverage_summary: list[dict]) -> dict:
    return {"passed": passed, "coverage_summary": coverage_summary, "coverage_findings": []}


def test_load_coverage_parses_json(tmp_path: Path) -> None:
    path = tmp_path / "architecture-strict.json"
    path.write_text(json.dumps({"passed": True, "coverage_summary": []}), encoding="utf-8")

    report = load_coverage(path)

    assert report["passed"] is True
    assert report["coverage_summary"] == []


def test_render_summary_markdown_zero_findings() -> None:
    report = make_report(True, [])

    markdown = render_summary_markdown(report)

    assert "Status: pass" in markdown
    assert "Covered: 0" in markdown
    assert "Uncovered: 0" in markdown
    assert "Stale: 0" in markdown
    assert "Unknown: 0" in markdown


def test_render_summary_markdown_failed_gate() -> None:
    report = make_report(
        False,
        [
            {
                "contract": "namespace-coverage",
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 2, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [{"item": "Foo.Bar", "evidence": "no rule references it"}],
                "stale_items": [],
                "unknown_items": [],
            }
        ],
    )

    markdown = render_summary_markdown(report)

    assert "Status: fail" in markdown
    assert "Uncovered: 2" in markdown


def test_total_counts_sums_across_contracts() -> None:
    report = make_report(
        True,
        [
            {"scope": "namespace", "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0}},
            {"scope": "project", "counts": {"covered": 2, "excluded": 1, "uncovered": 0, "stale": 0, "unknown": 0}},
        ],
    )

    totals = total_counts(report)

    assert totals == {"covered": 3, "excluded": 1, "uncovered": 0, "stale": 0, "unknown": 0}


def test_classify_changed_file_unknown_when_unmappable(tmp_path: Path) -> None:
    report = make_report(True, [])
    coverage_index = build_coverage_index(report)

    unit = classify_changed_file("src/Missing/DoesNotExist.cs", tmp_path, coverage_index)

    assert unit.state == "unknown"
    assert unit.unit is None


def test_classify_changed_file_maps_known_uncovered_namespace(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    file_path = tmp_path / file_rel
    file_path.parent.mkdir(parents=True)
    file_path.write_text("namespace Foo.Bar;\n\nclass C {}\n", encoding="utf-8")

    report = make_report(
        False,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 0, "excluded": 0, "uncovered": 1, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [{"item": "Foo.Bar", "evidence": "uncovered"}],
                "stale_items": [],
                "unknown_items": [],
            }
        ],
    )
    coverage_index = build_coverage_index(report)

    unit = classify_changed_file(file_rel, tmp_path, coverage_index)

    assert unit.scope == "namespace"
    assert unit.unit == "Foo.Bar"
    assert unit.state == "uncovered"


def test_render_new_code_section_reports_unknown_and_uncovered(tmp_path: Path) -> None:
    file_rel = "src/Untracked/Thing.cs"
    file_path = tmp_path / file_rel
    file_path.parent.mkdir(parents=True)
    file_path.write_text("// no namespace here\n", encoding="utf-8")

    report = make_report(True, [])

    markdown = render_report(report, [file_rel], tmp_path)

    assert "New-code coverage" in markdown
    assert "Changed items requiring policy update: 1" in markdown


def test_render_new_code_section_skips_covered_units() -> None:
    from architecture_coverage_report import ChangedUnit

    section = render_new_code_section([ChangedUnit(file="a.cs", scope="namespace", unit="A.B", state="covered")])

    assert "Changed namespaces/projects/assemblies covered: 1" in section
    assert "A.B" not in section.split("\n", 4)[-1]


def test_classify_changed_file_does_not_assume_covered_without_evidence(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    file_path = tmp_path / file_rel
    file_path.parent.mkdir(parents=True)
    file_path.write_text("namespace Foo.Bar;\n\nclass C {}\n", encoding="utf-8")

    report = make_report(True, [])
    coverage_index = build_coverage_index(report)

    unit = classify_changed_file(file_rel, tmp_path, coverage_index)

    assert unit.state == "unknown"
    assert unit.unit == "Foo.Bar"


def test_render_report_includes_new_code_section_when_changed_files_list_is_empty() -> None:
    report = make_report(True, [])

    markdown = render_report(report, [], Path("."))

    assert "New-code coverage" in markdown
    assert "Changed first-party files: 0" in markdown


def test_render_report_omits_new_code_section_when_changed_files_not_requested() -> None:
    report = make_report(True, [])

    markdown = render_report(report, None, Path("."))

    assert "New-code coverage" not in markdown
