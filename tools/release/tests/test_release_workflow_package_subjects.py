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


def test_provenance_job_attests_exact_frozen_subject_inventories_with_least_privilege() -> None:
    workflow = _workflow()
    attestation_job = workflow.split("  attest-prepublication-provenance:\n", maxsplit=1)[1].split(
        "  verify-prepublication-provenance:\n", maxsplit=1
    )[0]

    assert "needs: [prepare-candidate, checkpoint-b-evidence]" in attestation_job
    assert "contents: read\n      id-token: write\n      attestations: write" in attestation_job
    assert workflow.count("attestations: write") == 1
    assert "verify-release-evidence" in attestation_job
    assert attestation_job.count("render-attestation-subject-checksums") == 2
    assert "--subject-class package" in attestation_job
    assert "--subject-class evidence" in attestation_job
    assert attestation_job.count("actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6") == 2
    assert "subject-checksums: artifacts/provenance/package-subjects.sha256" in attestation_job
    assert "subject-checksums: artifacts/provenance/evidence-subjects.sha256" in attestation_job
    assert "*.nupkg" not in attestation_job
    assert "*.snupkg" not in attestation_job


def test_independent_provenance_verification_blocks_publication_handoffs() -> None:
    workflow = _workflow()
    verification_job = workflow.split("  verify-prepublication-provenance:\n", maxsplit=1)[1].split(
        "  release:\n", maxsplit=1
    )[0]
    release_job = workflow.split("  release:\n", maxsplit=1)[1].split("  create-release:\n", maxsplit=1)[0]

    assert "needs: [prepare-candidate, attest-prepublication-provenance]" in verification_job
    assert "attestations: read" in verification_job
    assert "verify_release_provenance.py" in verification_job
    assert '--repository "$GITHUB_REPOSITORY"' in verification_job
    assert '--signer-workflow "$GITHUB_REPOSITORY/.github/workflows/release-nuget.yml"' in verification_job
    assert '--source-commit "$GITHUB_SHA"' in verification_job
    assert all(
        dependency in release_job
        for dependency in ("prepare-candidate", "checkpoint-b-evidence", "verify-prepublication-provenance")
    )
    assert workflow.index("  attest-prepublication-provenance:\n") < workflow.index(
        "  verify-prepublication-provenance:\n"
    ) < workflow.index("  release:\n")
    assert "verify-release-evidence" in release_job
