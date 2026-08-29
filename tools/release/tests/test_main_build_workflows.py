from __future__ import annotations

from pathlib import Path

_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_WORKFLOWS = _REPOSITORY_ROOT / ".github" / "workflows"


def _read(name: str) -> str:
    return (_WORKFLOWS / name).read_text(encoding="utf-8")


def _trigger_block(workflow: str, end_marker: str) -> str:
    start = workflow.index("on:\n")
    end = workflow.index(end_marker, start)
    return workflow[start:end]


def test_pr_ci_no_longer_triggers_on_main_push() -> None:
    workflow = _read("ci.yml")
    trigger = _trigger_block(workflow, "\nconcurrency:")

    assert "pull_request:" in trigger
    assert "push:" not in trigger
    assert "main_badge_refresh:" not in workflow


def test_codeql_keeps_pr_schedule_and_manual_without_main_push() -> None:
    workflow = _read("codeql.yml")
    trigger = _trigger_block(workflow, "\nconcurrency:")

    assert "pull_request:" in trigger
    assert "schedule:" in trigger
    assert "workflow_dispatch:" in trigger
    assert "push:" not in trigger


def test_main_quality_is_coverage_telemetry_only() -> None:
    workflow = _read("main-quality.yml")
    trigger = _trigger_block(workflow, "\nconcurrency:")

    assert "push:" in trigger and "- main" in trigger
    assert "test-coverage-core-1" in workflow
    assert "test-coverage-core-2" in workflow
    assert "test-coverage-other" in workflow
    assert "dotnet-sonarscanner" in workflow
    assert "codecov/codecov-action@" in workflow
    assert "Architecture Coverage" not in workflow
    assert "windows-latest" not in workflow
    assert "macos-" not in workflow
    assert "test-e2e" not in workflow
    assert "test-packed-artifact" not in workflow


def test_main_packages_uses_github_token_and_never_runs_validation_matrix() -> None:
    workflow = _read("main-packages.yml")
    trigger = _trigger_block(workflow, "\npermissions:")

    assert "push:" in trigger and "- main" in trigger
    assert "packages: write" in workflow
    assert "${{ github.token }}" in workflow
    assert "GITHUB_PACKAGES_PAT" not in workflow
    assert "secrets." not in workflow
    assert "tools/release/main_build.py version" in workflow
    assert "tools/release/package_manifest.py create" in workflow
    assert "tools/release/package_manifest.py verify" in workflow
    assert "--no-symbols" in workflow
    assert "retention-plan" in workflow
    assert "dotnet test" not in workflow
    assert "make test" not in workflow
    assert "Architecture Coverage" not in workflow
    assert "Sonar" not in workflow
    assert "Codecov" not in workflow


def test_main_package_retention_is_complete_set_and_current_build_safe() -> None:
    workflow = _read("main-packages.yml")

    assert "needs: publish" in workflow
    assert "if: needs.publish.result == 'success'" in workflow
    assert "ArchLinterNet.CEL" in workflow
    assert "ArchLinterNet.Cli" in workflow
    assert "ArchLinterNet.Core" in workflow
    assert "ArchLinterNet.Testing" in workflow
    assert "--current-version \"$PACKAGE_VERSION\"" in workflow
    assert "--keep 5" in workflow
