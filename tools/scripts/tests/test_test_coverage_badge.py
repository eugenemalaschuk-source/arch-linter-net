from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from test_coverage_badge import badge_color, merge_line_coverage, render_badge_markdown  # noqa: E402


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
