from __future__ import annotations

from pathlib import Path

_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_WORKFLOWS = _REPOSITORY_ROOT / ".github" / "workflows"


def _read(name: str) -> str:
    return (_WORKFLOWS / name).read_text(encoding="utf-8")


def test_main_sonar_distinguishes_refresh_failure_from_processed_gate_failure() -> None:
    workflow = _read("main-quality.yml")

    assert "/d:sonar.qualitygate.wait=true" in workflow
    assert "/d:sonar.qualitygate.timeout=300" in workflow
    assert "End SonarCloud analysis and record quality gate" in workflow
    assert "QUALITY GATE STATUS: FAILED" in workflow
    assert "QUALITY GATE STATUS: (PASSED|OK)" in workflow
    assert "::warning title=SonarCloud quality gate::" in workflow
    assert 'exit "$scanner_status"' in workflow
    assert "completed without a recognizable quality-gate status" in workflow
    assert "SonarCloud quality gate (telemetry only)" in workflow
    assert "continue-on-error: true" not in workflow
    assert "Require complete main telemetry" in workflow


def test_pr_sonar_gate_failure_fails_the_required_coverage_check() -> None:
    workflow = _read("ci.yml")
    sonar_end_start = workflow.index("      - name: End SonarCloud analysis")
    sonar_summary_start = workflow.index("      - name: Publish SonarCloud PR summary link", sonar_end_start)
    sonar_end = workflow[sonar_end_start:sonar_summary_start]

    assert "/d:sonar.qualitygate.wait=true" in workflow
    assert "verify_sonar_quality_gate.py" in sonar_end
    assert "continue-on-error: true" not in sonar_end
    assert 'scanner_status="${PIPESTATUS[0]}"' in sonar_end
    assert "steps.sonar_end.outputs.quality_gate" in workflow
    assert "required Coverage + Sonar check fails" in workflow


def test_main_package_workflow_restores_exact_build_before_safe_cleanup() -> None:
    workflow = _read("main-packages.yml")
    smoke_start = workflow.index("Verify exact published package set is consumable")
    retention_start = workflow.index("Build complete-set retention and stale-orphan cleanup plan")

    assert "\nconcurrency:\n  group: arch-linter-main-packages" not in workflow
    assert smoke_start < retention_start
    assert "dotnet tool install ArchLinterNet.Cli" in workflow
    assert "dotnet tool list --tool-path" in workflow
    assert 'arch-linter-net" --help' in workflow
    assert "dotnet add package ArchLinterNet.Testing" in workflow
    assert "verify_restored_main_packages.py" in workflow
    assert "--prune-stale-partials" in workflow
    assert "--stale-partial-before" in workflow
    assert "1 hour ago" in workflow
    assert "(.delete + .delete_orphans)[]" in workflow
    assert ".stale_partial_versions" in workflow
    assert ".protected_partial_versions" in workflow
