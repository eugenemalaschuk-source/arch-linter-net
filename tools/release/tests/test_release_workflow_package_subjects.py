from __future__ import annotations

from pathlib import Path


def _workflow() -> str:
    return (Path(__file__).resolve().parents[3] / ".github" / "workflows" / "release-nuget.yml").read_text(
        encoding="utf-8"
    )


def _ci_workflow() -> str:
    return (Path(__file__).resolve().parents[3] / ".github" / "workflows" / "ci.yml").read_text(
        encoding="utf-8"
    )


def test_release_workflow_creates_and_attaches_derived_checksum_evidence() -> None:
    workflow = _workflow()

    assert "render-checksums --manifest artifacts/packages/package-manifest.json" in workflow
    assert "artifacts/packages/package-checksums.txt" in workflow
    assert "artifacts/packages/package-manifest.json" in workflow


def test_nuget_push_uses_manifest_selected_primary_subjects_and_checks_symbols() -> None:
    workflow = _workflow()

    assert "--kind package" in workflow
    assert "--kind symbols" in workflow
    assert 'test -f "artifacts/packages/${symbols[$index]}"' in workflow
    assert 'dotnet nuget push "artifacts/packages/${packages[$index]}"' in workflow
    assert "--skip-duplicate" not in workflow
    assert "artifacts/packages/*.nupkg" not in workflow


def test_manifest_verification_uses_bash_on_windows_and_release_matrices() -> None:
    workflow = _workflow()
    ci_workflow = _ci_workflow()

    assert "- name: Verify immutable candidate packages\n        shell: bash" in workflow
    assert ci_workflow.count("- name: Verify immutable candidate\n        shell: bash") == 2


def test_github_release_attachment_uses_manifest_selected_subjects_without_globs() -> None:
    workflow = _workflow()

    assert "--kind all" in workflow
    assert 'attachment_paths+=("artifacts/packages/$asset")' in workflow
    assert "artifacts/packages/*.snupkg" not in workflow
