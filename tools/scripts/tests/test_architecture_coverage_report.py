from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from architecture_coverage_report import (  # noqa: E402
    ChangedUnit,
    build_coverage_index,
    classify_changed_file,
    configured_scopes,
    load_coverage,
    render_new_code_section,
    render_report,
    render_summary_markdown,
    total_counts,
)

ALL_SCOPES = {"namespace", "project", "assembly"}


def make_report(passed: bool, coverage_summary: list[dict], **additional_fields: object) -> dict:
    report = {"passed": passed, "coverage_summary": coverage_summary, "coverage_findings": []}
    report.update(additional_fields)
    return report


def write_file(tmp_path: Path, rel_path: str, content: str) -> None:
    file_path = tmp_path / rel_path
    file_path.parent.mkdir(parents=True, exist_ok=True)
    file_path.write_text(content, encoding="utf-8")


def test_load_coverage_parses_json(tmp_path: Path) -> None:
    path = tmp_path / "architecture-strict.json"
    path.write_text(json.dumps({"passed": True, "coverage_summary": []}), encoding="utf-8")

    report = load_coverage(path)

    assert report["passed"] is True
    assert report["coverage_summary"] == []


def test_render_summary_markdown_zero_findings() -> None:
    report = make_report(True, [])

    markdown = render_summary_markdown(report)

    assert "## Architecture coverage" in markdown
    assert "✅ pass" in markdown
    assert "| Covered | 0 |" in markdown
    assert "| Uncovered | 0 |" in markdown
    assert "| Stale | 0 |" in markdown
    assert "| Unknown | 0 |" in markdown
    assert "| Failed rules | 0 |" in markdown
    assert "| Failed diagnostics | 0 |" in markdown
    assert "### Failed rules" not in markdown


def test_render_summary_markdown_notes_when_coverage_unconfigured() -> None:
    report = make_report(True, [])

    markdown = render_summary_markdown(report)

    assert "coverage is unconfigured" in markdown


def test_render_summary_markdown_omits_note_when_coverage_contracts_exist_and_clean() -> None:
    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 3, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
            }
        ],
    )

    markdown = render_summary_markdown(report)

    assert "| Covered | 3 |" in markdown
    assert "coverage is unconfigured" not in markdown


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

    assert "❌ fail" in markdown
    assert "| Uncovered | 2 |" in markdown
    assert "### Failed rules (1)" in markdown
    assert "`namespace-coverage`" in markdown
    assert "state `uncovered`" in markdown
    assert "### Failed rules" in markdown.split("| Metric | Count |", 1)[0]


def test_render_summary_markdown_groups_and_bounds_contract_diagnostics() -> None:
    violations = [
        {
            "contract_id": "models-no-first-party-dependencies",
            "contract": "Models must be dependency-free",
            "message_code": "forbidden-dependency",
            "source": f"Example.Model{i}",
            "forbidden_references": [f"Example.Dependency{i}"],
            "policy_origin": {"path": "architecture/policy/audit-conventions.arch.yml", "line": 42},
        }
        for i in (4, 2, 3, 1)
    ]
    report = make_report(False, [], violations=violations)

    markdown = render_summary_markdown(report, max_failure_diagnostics=3)

    assert "### Failed rules (1)" in markdown
    assert "**`models-no-first-party-dependencies` — Models must be dependency-free** — 4 failed diagnostics" in markdown
    assert "source `Example.Model1`" in markdown
    assert "forbidden references `Example.Dependency1`" in markdown
    assert "policy `architecture/policy/audit-conventions.arch.yml:42`" in markdown
    assert "_1 additional diagnostic omitted._" in markdown
    assert "| Failed rules | 1 |" in markdown
    assert "| Failed diagnostics | 4 |" in markdown

    detailed_markdown = render_summary_markdown(report)

    assert "source `Example.Model4`" in detailed_markdown
    assert "additional diagnostic omitted" not in detailed_markdown


def test_render_summary_markdown_sorts_rules_by_contract_id() -> None:
    report = make_report(
        False,
        [],
        violations=[
            {"contract_id": "z-models", "contract": "Models", "source": "Example.Model"},
            {"contract_id": "a-abstractions", "contract": "Abstractions", "source": "Example.IService"},
        ],
    )

    markdown = render_summary_markdown(report)

    assert markdown.index("`a-abstractions`") < markdown.index("`z-models`")


def test_render_summary_markdown_ignores_current_build_state_preflight_entries() -> None:
    report = make_report(
        False,
        [],
        violations=[{"contract_id": "broken-rule", "contract": "Broken rule", "source": "Example.Type"}],
        preflight_diagnostics=[
            {
                "contract": "build-state-preflight",
                "source": "src/Example/Example.csproj",
                "state": "current",
            }
        ],
    )

    markdown = render_summary_markdown(report)

    assert "`broken-rule`" in markdown
    assert "src/Example/Example.csproj" not in markdown
    assert "| Failed rules | 1 |" in markdown
    assert "| Failed diagnostics | 1 |" in markdown


def test_render_summary_markdown_falls_back_to_coverage_summary_when_findings_absent() -> None:
    report = make_report(
        False,
        [
            {
                "contract": "self-policy coverage",
                "contract_id": "self-policy-coverage",
                "scope": "rule_input",
                "counts": {"covered": 0, "excluded": 0, "uncovered": 0, "stale": 1, "unknown": 0},
                "uncovered_items": [],
                "stale_items": [{"item": "obsolete-rule", "evidence": "not declared"}],
                "unknown_items": [],
            }
        ],
    )

    markdown = render_summary_markdown(report)

    assert "**`self-policy-coverage` — self-policy coverage** — 1 failed diagnostic" in markdown
    assert "state `stale`" in markdown
    assert "rule_input item `obsolete-rule`" in markdown
    assert "evidence `not declared`" in markdown


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


def test_configured_scopes_reflects_contracts_present() -> None:
    report = make_report(
        True,
        [
            {"scope": "namespace", "counts": {}},
            {"scope": "assembly", "counts": {}},
        ],
    )

    assert configured_scopes(report) == {"namespace", "assembly"}


def test_configured_scopes_empty_when_no_coverage_contracts() -> None:
    report = make_report(True, [])

    assert configured_scopes(report) == set()


def test_classify_changed_file_unknown_when_unmappable(tmp_path: Path) -> None:
    report = make_report(True, [])
    coverage_index = build_coverage_index(report)

    units = classify_changed_file("src/Missing/DoesNotExist.cs", tmp_path, coverage_index, ALL_SCOPES)

    assert len(units) == 1
    assert units[0].state == "unknown"
    assert units[0].unit is None


def test_classify_changed_file_maps_known_uncovered_namespace(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")

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
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    namespace_units = [unit for unit in units if unit.scope == "namespace"]

    assert len(namespace_units) == 1
    assert namespace_units[0].unit == "Foo.Bar"
    assert namespace_units[0].state == "uncovered"


def test_render_new_code_section_reports_unknown_and_uncovered(tmp_path: Path) -> None:
    file_rel = "src/Untracked/Thing.cs"
    write_file(tmp_path, file_rel, "// no namespace here\n")

    report = make_report(True, [])

    markdown = render_report(report, [file_rel], tmp_path)

    assert "### New-code coverage" in markdown
    assert "| Requiring policy update | 1 |" in markdown


def test_render_new_code_section_skips_covered_units() -> None:
    section = render_new_code_section({"a.cs": [ChangedUnit(scope="namespace", unit="A.B", state="covered")]})

    assert "| Changed namespaces/projects/assemblies covered | 1 |" in section
    assert "A.B" not in section


def test_classify_changed_file_does_not_assume_covered_without_evidence(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 0, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [],
            }
        ],
    )
    coverage_index = build_coverage_index(report)
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    namespace_units = [unit for unit in units if unit.scope == "namespace"]

    assert namespace_units[0].state == "unknown"
    assert namespace_units[0].unit == "Foo.Bar"


def test_classify_changed_file_derives_covered_from_real_coverage_summary_shape(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            }
        ],
    )
    coverage_index = build_coverage_index(report)
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    namespace_units = [unit for unit in units if unit.scope == "namespace"]

    assert namespace_units[0].state == "covered"
    assert namespace_units[0].unit == "Foo.Bar"


def test_classify_changed_file_unknown_when_namespace_outside_configured_scope(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Some.Other.Namespace", "evidence": "Some.Other.Namespace.Type"}],
            }
        ],
    )
    coverage_index = build_coverage_index(report)
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    namespace_units = [unit for unit in units if unit.scope == "namespace"]

    assert namespace_units[0].state == "unknown"
    assert namespace_units[0].unit == "Foo.Bar"


def test_render_new_code_section_does_not_flag_real_covered_unit(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            }
        ],
    )

    markdown = render_report(report, [file_rel], tmp_path)

    assert "| Requiring policy update | none |" in markdown
    assert "Foo.Bar" not in markdown.split("### New-code coverage", 1)[1].split("Items needing attention", 1)[0]


def test_render_report_includes_new_code_section_when_changed_files_list_is_empty() -> None:
    report = make_report(True, [])

    markdown = render_report(report, [], Path("."))

    assert "### New-code coverage" in markdown
    assert "| Changed first-party files | 0 |" in markdown


def test_render_report_omits_new_code_section_when_changed_files_not_requested() -> None:
    report = make_report(True, [])

    markdown = render_report(report, None, Path("."))

    assert "### New-code coverage" not in markdown


def test_classify_changed_file_flags_uncovered_project_even_when_namespace_is_covered(tmp_path: Path) -> None:
    """A changed file's namespace can be covered while its containing project is not.
    The classifier must report both scopes independently instead of stopping at the
    first match (namespace), or the project-level gap would be silently hidden."""
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            },
            {
                "scope": "project",
                "counts": {"covered": 0, "excluded": 0, "uncovered": 1, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [{"item": "src/Foo/Foo.csproj", "evidence": "no layer covers this project"}],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [],
            },
        ],
    )
    coverage_index = build_coverage_index(report)
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    by_scope = {unit.scope: unit for unit in units}

    assert by_scope["namespace"].state == "covered"
    assert by_scope["project"].state == "uncovered"
    assert by_scope["project"].unit == "src/Foo/Foo.csproj"


def test_render_new_code_section_surfaces_project_problem_despite_covered_namespace(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            },
            {
                "scope": "project",
                "counts": {"covered": 0, "excluded": 0, "uncovered": 1, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [{"item": "src/Foo/Foo.csproj", "evidence": "no layer covers this project"}],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [],
            },
        ],
    )

    markdown = render_report(report, [file_rel], tmp_path)

    assert "| Changed namespaces/projects/assemblies covered | 1 |" in markdown
    assert "| Changed namespaces/projects/assemblies uncovered | 1 |" in markdown
    assert "src/Foo/Foo.csproj" in markdown
    assert "**uncovered**" in markdown


def test_classify_changed_file_skips_unconfigured_project_scope(tmp_path: Path) -> None:
    """If the policy has no project-scope coverage contract at all, a changed .cs file
    inside a .csproj must not be reported as project: unknown — that would just restate
    "this policy has no project coverage configured" on every PR, not a real gap."""
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            }
        ],
    )
    coverage_index = build_coverage_index(report)
    scopes = configured_scopes(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, scopes)
    scopes_seen = {unit.scope for unit in units}

    assert scopes_seen == {"namespace"}
    assert "project" not in scopes_seen
    assert "assembly" not in scopes_seen


def test_classify_changed_file_skips_test_project_files(tmp_path: Path) -> None:
    """A changed file inside a *.Tests project must not be classified against any
    coverage scope at all — its namespace/project/assembly can never be scanned by the
    architecture engine (test projects aren't in analysis.target_assemblies), so
    reporting "unknown" for it is tooling noise, not a real policy gap."""
    file_rel = "tests/Foo.Tests/BarTests.cs"
    write_file(tmp_path, file_rel, "namespace FooTestFixtures;\n\nclass BarTests {}\n")
    write_file(tmp_path, "tests/Foo.Tests/Foo.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(True, [])
    coverage_index = build_coverage_index(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, ALL_SCOPES)

    assert units == []


def test_classify_changed_file_skips_synthetic_acceptance_fixture_files(tmp_path: Path) -> None:
    """A synthetic adoption-acceptance fixture carries its own .csproj, so the enclosing-
    project check alone would classify it as "unknown". Its assemblies are built into
    throwaway copies by the acceptance and release gates and are never in
    analysis.target_assemblies, so it is skipped exactly like a test project."""
    file_rel = ("tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/"
                "modular-consumer/src/Synthetic.Modules.M01/Module.cs")
    write_file(tmp_path, file_rel, "namespace Synthetic.Modules.M01;\n\nclass Module {}\n")
    write_file(
        tmp_path,
        ("tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/"
         "modular-consumer/src/Synthetic.Modules.M01/Synthetic.Modules.M01.csproj"),
        "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(True, [])
    coverage_index = build_coverage_index(report)

    units = classify_changed_file(file_rel, tmp_path, coverage_index, ALL_SCOPES)

    assert units == []


def test_render_new_code_section_omits_synthetic_acceptance_fixture_noise(tmp_path: Path) -> None:
    file_rel = ("tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/"
                "modular-consumer/src/Synthetic.Modules.M01/Module.cs")
    write_file(tmp_path, file_rel, "namespace Synthetic.Modules.M01;\n\nclass Module {}\n")
    write_file(
        tmp_path,
        ("tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/"
         "modular-consumer/src/Synthetic.Modules.M01/Synthetic.Modules.M01.csproj"),
        "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(True, [])

    markdown = render_report(report, [file_rel], tmp_path)

    assert "Synthetic.Modules.M01" not in markdown
    assert "| Requiring policy update | none |" in markdown


def test_render_new_code_section_omits_test_project_noise(tmp_path: Path) -> None:
    file_rel = "tests/Foo.Tests/BarTests.cs"
    write_file(tmp_path, file_rel, "namespace FooTestFixtures;\n\nclass BarTests {}\n")
    write_file(tmp_path, "tests/Foo.Tests/Foo.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(True, [])

    markdown = render_report(report, [file_rel], tmp_path)

    assert "FooTestFixtures" not in markdown
    assert "| Requiring policy update | none |" in markdown


def test_render_new_code_section_omits_unconfigured_project_scope_noise(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Bar;\n\nclass C {}\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    report = make_report(
        True,
        [
            {
                "scope": "namespace",
                "counts": {"covered": 1, "excluded": 0, "uncovered": 0, "stale": 0, "unknown": 0},
                "excluded_items": [],
                "uncovered_items": [],
                "stale_items": [],
                "unknown_items": [],
                "covered_items": [{"item": "Foo.Bar", "evidence": "Foo.Bar.SomeType"}],
            }
        ],
    )

    markdown = render_report(report, [file_rel], tmp_path)

    assert "Foo.csproj" not in markdown
    assert "| Requiring policy update | none |" in markdown


def test_detect_project_path_matches_repo_relative_csproj_path(tmp_path: Path) -> None:
    file_rel = "src/Foo/Sub/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo.Sub;\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    coverage_index: dict[tuple[str, str], dict] = {("project", "src/Foo/Foo.csproj"): {"state": "covered", "evidence": None}}

    units = classify_changed_file(file_rel, tmp_path, coverage_index, {"project"})
    project_unit = next(unit for unit in units if unit.scope == "project")

    assert project_unit.unit == "src/Foo/Foo.csproj"
    assert project_unit.state == "covered"


def test_detect_assembly_name_uses_csproj_assembly_name_when_present(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo;\n")
    write_file(
        tmp_path,
        "src/Foo/Foo.csproj",
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><AssemblyName>CustomAssembly</AssemblyName></PropertyGroup></Project>\n",
    )

    coverage_index: dict[tuple[str, str], dict] = {("assembly", "CustomAssembly"): {"state": "covered", "evidence": None}}

    units = classify_changed_file(file_rel, tmp_path, coverage_index, {"assembly"})
    assembly_unit = next(unit for unit in units if unit.scope == "assembly")

    assert assembly_unit.unit == "CustomAssembly"
    assert assembly_unit.state == "covered"


def test_detect_assembly_name_falls_back_to_csproj_stem_without_assembly_name(tmp_path: Path) -> None:
    file_rel = "src/Foo/Bar.cs"
    write_file(tmp_path, file_rel, "namespace Foo;\n")
    write_file(tmp_path, "src/Foo/Foo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")

    coverage_index: dict[tuple[str, str], dict] = {}

    units = classify_changed_file(file_rel, tmp_path, coverage_index, {"assembly"})
    assembly_unit = next(unit for unit in units if unit.scope == "assembly")

    assert assembly_unit.unit == "Foo"
    assert assembly_unit.state == "unknown"


def test_render_report_reports_diff_unavailable_instead_of_zero_files() -> None:
    report = make_report(True, [])

    markdown = render_report(report, changed_files=None, repo_root=Path("."), diff_failed=True)

    assert "### New-code coverage" in markdown
    assert "Unavailable" in markdown
    assert "Changed first-party files | 0" not in markdown


def test_render_report_diff_failed_takes_precedence_over_changed_files() -> None:
    """Even if a (possibly stale/empty) changed-files list was produced, a failed diff
    must render as unavailable rather than silently reporting on whatever partial list
    exists — a diff failure is not the same as a successfully-computed empty diff."""
    report = make_report(True, [])

    markdown = render_report(report, changed_files=[], repo_root=Path("."), diff_failed=True)

    assert "Unavailable" in markdown
    assert "Requiring policy update" not in markdown
