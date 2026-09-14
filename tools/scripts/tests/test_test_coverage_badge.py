from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from test_coverage_badge import (  # noqa: E402
    badge_color,
    collect_cobertura_reports,
    merge_line_coverage,
    render_badge_markdown,
)


def write_cobertura(path: Path, filename: str, line_hits: dict[int, int]) -> None:
    lines_xml = "".join(f'<line number="{number}" hits="{hits}"/>' for number, hits in line_hits.items())
    path.write_text(
        f"""<?xml version="1.0" encoding="utf-8"?>
<coverage>
  <packages>
    <package>
      <classes>
        <class filename="{filename}">
          <lines>{lines_xml}</lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
""",
        encoding="utf-8",
    )


def test_merge_line_coverage_sums_distinct_files(tmp_path: Path) -> None:
    report_a = tmp_path / "a.cobertura.xml"
    report_b = tmp_path / "b.cobertura.xml"
    write_cobertura(report_a, "Foo.cs", {1: 1, 2: 0})
    write_cobertura(report_b, "Bar.cs", {1: 1, 2: 1})

    covered, total = merge_line_coverage([report_a, report_b])

    assert total == 4
    assert covered == 3


def test_merge_line_coverage_unions_overlapping_file_by_max_hits(tmp_path: Path) -> None:
    """Two test projects can both exercise the same shared source file. Naively summing
    each report's covered/total would double-count those lines; the merge must take the
    union of lines and the max hit count per line instead."""
    report_a = tmp_path / "a.cobertura.xml"
    report_b = tmp_path / "b.cobertura.xml"
    write_cobertura(report_a, "Shared.cs", {1: 0, 2: 1})
    write_cobertura(report_b, "Shared.cs", {1: 1, 2: 0})

    covered, total = merge_line_coverage([report_a, report_b])

    assert total == 2
    assert covered == 2


def test_badge_color_thresholds() -> None:
    assert badge_color(85) == "brightgreen"
    assert badge_color(80) == "brightgreen"
    assert badge_color(70) == "yellow"
    assert badge_color(60) == "yellow"
    assert badge_color(40) == "red"


def test_render_badge_markdown_escapes_percent_sign() -> None:
    markdown = render_badge_markdown(79.0, "https://example.com")

    assert "79%25" in markdown
    assert "%2525" not in markdown
    assert "yellow" in markdown
    assert "(https://example.com)" in markdown


def test_collect_cobertura_reports_returns_only_workspace_files(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    report = tmp_path / "test-results" / "coverage.cobertura.xml"
    report.parent.mkdir(parents=True)
    write_cobertura(report, "Foo.cs", {1: 1})

    reports = collect_cobertura_reports("test-results/**/coverage.cobertura.xml")

    assert reports == [report.resolve()]


def test_collect_cobertura_reports_rejects_absolute_glob(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)

    import pytest

    with pytest.raises(ValueError):
        collect_cobertura_reports("/etc/*.xml")


def test_collect_cobertura_reports_rejects_traversal_glob(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)

    import pytest

    with pytest.raises(ValueError):
        collect_cobertura_reports("../**/coverage.cobertura.xml")
