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


def test_main_quality_is_coverage_telemetry_only_and_fail_closed() -> None:
    workflow = _read("main-quality.yml")
    trigger = _trigger_block(workflow, "\nconcurrency:")

    assert "push:" in trigger and "- main" in trigger
    assert "test-coverage-core-1" in workflow
    assert "test-coverage-core-2" in workflow
    assert "test-coverage-other" in workflow
    assert "Canonicalize coverage shard evidence" in workflow
    assert "Main Coverage Evidence" in workflow
    assert "Assemble and verify complete coverage inventory" in workflow
    assert "dotnet-sonarscanner" in workflow
    assert "codecov/codecov-action@" in workflow
    assert "fail_ci_if_error: true" in workflow
    assert "continue-on-error: true" not in workflow
    assert "Architecture Coverage" not in workflow
    assert "windows-latest" not in workflow
    assert "macos-" not in workflow
    assert "test-e2e" not in workflow
    assert "test-packed-artifact" not in workflow


def test_main_quality_uses_commit_bound_canonical_coverage_evidence() -> None:
    workflow = _read("main-quality.yml")

    assert "main_quality_coverage.py canonicalize-shard" in workflow
    assert "main_quality_coverage.py assemble" in workflow
    assert workflow.count('--expected-sha "$GITHUB_SHA"') >= 3
    assert "/d:sonar.scm.revision=\"$GITHUB_SHA\"" in workflow
    assert "/d:sonar.cs.opencover.reportsPaths=\"$OPENCOVER_FILES\"" in workflow
    assert 'sonar.cs.opencover.reportsPaths="test-results/**/coverage.opencover.xml"' not in workflow
    assert "find test-results -name 'coverage.cobertura.xml'" not in workflow
    assert "files: ${{ steps.coverage_evidence.outputs.cobertura_files }}" in workflow
    assert "disable_search: true" in workflow
    assert "override_commit: ${{ github.sha }}" in workflow
    assert "main_quality_coverage.py verify-sonar" in workflow
    assert "/api/project_analyses/search" in workflow
    assert "Required .NET coverage shards: $shards" in workflow
    assert "Canonical OpenCover reports: $opencover" in workflow
    assert "Canonical Cobertura reports: $cobertura" in workflow
    assert "SonarCloud analysis revision:" in workflow
    assert "Codecov commit/upload:" in workflow


def test_main_sonar_and_codecov_refresh_independently_from_same_coverage() -> None:
    workflow = _read("main-quality.yml")

    assert "\n  coverage_inventory:\n" in workflow
    assert "\n  sonar:\n" in workflow
    assert "\n  codecov:\n" in workflow
    assert "\n  summary:\n" in workflow

    inventory_start = workflow.index("\n  coverage_inventory:\n")
    sonar_start = workflow.index("\n  sonar:\n")
    codecov_start = workflow.index("\n  codecov:\n")
    summary_start = workflow.index("\n  summary:\n")
    inventory = workflow[inventory_start:sonar_start]
    sonar = workflow[sonar_start:codecov_start]
    codecov = workflow[codecov_start:summary_start]
    summary = workflow[summary_start:]

    assert "needs: dotnet_coverage" in inventory
    assert "pattern: main-dotnet-coverage-*" in inventory
    assert "main_quality_coverage.py assemble" in inventory
    assert "main-dotnet-coverage-canonical" in inventory

    assert "needs: coverage_inventory" in sonar
    assert "name: main-dotnet-coverage-canonical" in sonar
    assert "main_quality_coverage.py verify-inventory" in sonar
    assert "dotnet-sonarscanner" in sonar
    assert "codecov/codecov-action@" not in sonar

    assert "needs: coverage_inventory" in codecov
    assert "name: main-dotnet-coverage-canonical" in codecov
    assert "main_quality_coverage.py verify-inventory" in codecov
    assert "codecov/codecov-action@" in codecov
    assert "dotnet-sonarscanner" not in codecov

    assert "needs: [dotnet_coverage, coverage_inventory, sonar, codecov]" in summary
    assert "Require complete main telemetry" in summary


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


def test_main_packages_accept_existing_package_visibility() -> None:
    workflow = _read("main-packages.yml")
    publish_start = workflow.index("\n  publish:\n")
    retention_start = workflow.index("\n  retention:\n")
    publish = workflow[publish_start:retention_start]

    assert "Publish main package set to GitHub Packages" in publish
    assert "dotnet nuget push" in publish
    assert "gh api" not in publish
    assert "PACKAGE_API_SCOPE" not in workflow
    assert "existing GitHub Package visibility" not in workflow
    assert "expected 'private'" not in workflow
    assert "Package visibility: unchanged" in workflow


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


def test_main_package_consumer_smoke_uses_isolated_nuget_config() -> None:
    workflow = _read("main-packages.yml")
    retention = workflow[workflow.index("\n  retention:\n") :]

    assert "<clear />" in retention
    assert 'key="nuget.org" value="https://api.nuget.org/v3/index.json"' in retention
    assert 'key="github" value="$feed"' in retention
    assert "NuGetPackageSourceCredentials_github" in retention
    assert "dotnet nuget list source" in retention
    assert "--format Detailed" in retention
    assert retention.count('--configfile "$smoke_config"') >= 3
    assert "--add-source \"$feed\"" not in retention
    assert "dotnet nuget add source" not in retention
    assert "restore_verbosity=normal" in retention
    assert "--verbosity \"$restore_verbosity\"" in retention


def test_main_workflows_never_publish_mkdocs_or_pages() -> None:
    for name in ("main-quality.yml", "main-packages.yml"):
        workflow = _read(name)
        assert "pages: write" not in workflow
        assert "actions/configure-pages" not in workflow
        assert "actions/deploy-pages" not in workflow
        assert "make docs-build" not in workflow

    release = _read("release-nuget.yml")
    assert "deploy-docs:" in release
    assert "if: ${{ inputs.publish == true }}" in release
    assert "pages: write" in release


def test_readme_main_badges_are_driven_by_post_merge_telemetry() -> None:
    readme = (_REPOSITORY_ROOT / "README.md").read_text(encoding="utf-8")

    assert "actions/workflows/main-quality.yml/badge.svg?branch=main" in readme
    assert "codecov.io/github/eugenemalaschuk-source/arch-linter-net/graph/badge.svg?branch=main" in readme
    assert "sonarcloud.io/api/project_badges/measure?project=eugenemalaschuk-source_arch-linter-net" in readme
    assert 'alt="Architecture policy"' not in readme
    assert "GitHub Pages is deployed only by the public release workflow" in readme
