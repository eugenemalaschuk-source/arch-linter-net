from __future__ import annotations

import re
from pathlib import Path


def _workflow() -> str:
    return (Path(__file__).resolve().parents[3] / ".github" / "workflows" / "release-nuget.yml").read_text(
        encoding="utf-8"
    )


def _ci_workflow() -> str:
    return (Path(__file__).resolve().parents[3] / ".github" / "workflows" / "ci.yml").read_text(
        encoding="utf-8"
    )


def _job(workflow: str, job_name: str) -> str:
    job_header = f"  {job_name}:\n"
    start = workflow.index(job_header) + len(job_header)
    remainder = workflow[start:]
    next_job = re.search(r"\n  [A-Za-z0-9_-]+:\n", remainder)
    return remainder if next_job is None else remainder[: next_job.start()]


def test_release_workflow_creates_and_attaches_derived_checksum_evidence() -> None:
    workflow = _workflow()

    assert "render-checksums --manifest artifacts/packages/package-manifest.json" in workflow
    assert "artifacts/packages/package-checksums.txt" in workflow
    assert "artifacts/packages/package-manifest.json" in workflow


def test_release_workflow_freezes_transport_before_candidate_upload() -> None:
    workflow = _workflow()
    prepare_job = _job(workflow, "prepare-candidate")

    assert "python3 tools/release/verify_relay_dependencies.py --source-root ." in prepare_job
    assert "python3 tools/release/release_distribution.py create" in prepare_job
    assert "--source-root ." in prepare_job
    assert "--inventory .github/badge-promotion/release-inventory.json" in prepare_job
    assert "--candidate-manifest artifacts/packages/package-manifest.json" in prepare_job
    assert '--version "$PACKAGE_VERSION"' in prepare_job
    assert '--source-commit "$GITHUB_SHA"' in prepare_job
    assert "--output-dir artifacts/packages/transport" in prepare_job
    assert "artifacts/packages/package-manifest.json/transport" not in prepare_job
    assert prepare_job.index("Create immutable candidate manifest") < prepare_job.index(
        "Create frozen Relay distribution"
    ) < prepare_job.index("Verify frozen Relay distribution") < prepare_job.index("Upload package artifacts")

    upload_job = prepare_job.split("      - name: Upload package artifacts\n", maxsplit=1)[1].split(
        "      - name: Upload repository-gate evidence\n", maxsplit=1
    )[0]
    assert "${{ env.PACKAGE_OUTPUT }}/" in upload_job
    assert "artifacts/packages/transport" not in upload_job


def test_checkpoint_b_and_publication_handoffs_verify_frozen_transport() -> None:
    workflow = _workflow()

    for job_name in (
        "checkpoint-b-shards",
        "checkpoint-b-platform-evidence",
        "checkpoint-b-evidence",
        "attest-prepublication-provenance",
        "release",
        "create-release",
    ):
        job = _job(workflow, job_name)
        assert "python3 tools/release/release_distribution.py verify" in job
        assert "--inventory .github/badge-promotion/release-inventory.json" in job
        assert "--candidate-manifest" in job
        assert "--transport-dir" in job
        assert "--manifest " in job
        assert "--checksums " in job
        assert "architecture-health-badge-release-distribution.json" in job
        assert "architecture-health-badge-release-checksums.txt" in job


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


def test_release_scope_resolution_uses_the_verified_immutable_candidate() -> None:
    workflow = _workflow()

    assert "- name: Resolve authoritative release scope" in workflow
    assert "create_release_scope_evidence.py --source-commit \"$GITHUB_SHA\" --repository \"$GITHUB_REPOSITORY\"" in workflow
    assert "--scope-dir" not in workflow
    assert workflow.index("- name: Verify immutable candidate manifest") < workflow.index(
        "- name: Resolve authoritative release scope"
    ) < workflow.index("- name: Generate deterministic release evidence")


def test_github_release_attachment_uses_manifest_selected_subjects_without_globs() -> None:
    workflow = _workflow()

    assert "--kind all" in workflow
    assert (
        "release_distribution.py paths --transport-dir artifacts/packages/transport "
        "--manifest artifacts/packages/transport/architecture-health-badge-release-distribution.json --kind subjects"
        in workflow
    )
    assert (
        "attachment_paths=(artifacts/packages/package-manifest.json artifacts/packages/package-checksums.txt "
        "artifacts/packages/transport/architecture-health-badge-release-distribution.json "
        "artifacts/packages/transport/architecture-health-badge-release-checksums.txt)"
        in workflow
    )
    assert 'attachment_paths+=("artifacts/packages/$asset")' in workflow
    assert 'attachment_paths+=("artifacts/packages/transport/$asset")' in workflow
    assert "artifacts/packages/*.snupkg" not in workflow
    assert "artifacts/packages/transport/*" not in workflow


def test_github_release_notes_link_to_the_evergreen_provenance_guide() -> None:
    workflow = _workflow()

    assert "## Verify release provenance" in workflow
    assert "https://eugenemalaschuk-source.github.io/arch-linter-net/guides/release-provenance-verification/" in workflow


def test_provenance_job_attests_exact_frozen_subject_inventories_with_least_privilege() -> None:
    workflow = _workflow()
    attestation_job = workflow.split("  attest-prepublication-provenance:\n", maxsplit=1)[1].split(
        "  verify-prepublication-provenance:\n", maxsplit=1
    )[0]

    assert "needs: [prepare-candidate, checkpoint-b-evidence]" in attestation_job
    assert "contents: read\n      id-token: write\n      attestations: write" in attestation_job
    assert workflow.count("attestations: write") == 1
    assert "verify-release-evidence" in attestation_job
    assert attestation_job.count("package_manifest.py render-attestation-subject-checksums") == 2
    assert attestation_job.count("release_distribution.py render-attestation-subject-checksums") == 2
    assert "--subject-class package" in attestation_job
    assert "--subject-class evidence" in attestation_job
    assert "--subject-class transport" in attestation_job
    assert attestation_job.count("actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6") == 4
    assert "subject-checksums: artifacts/provenance/package-subjects.sha256" in attestation_job
    assert "subject-checksums: artifacts/provenance/evidence-subjects.sha256" in attestation_job
    assert "subject-checksums: artifacts/provenance/transport-subjects.sha256" in attestation_job
    assert "subject-checksums: artifacts/provenance/transport-evidence-subjects.sha256" in attestation_job
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
    assert "--distribution-dir artifacts/packages/transport" in verification_job
    assert "--distribution-manifest artifacts/packages/transport/architecture-health-badge-release-distribution.json" in (
        verification_job
    )
    assert "--distribution-checksums artifacts/packages/transport/architecture-health-badge-release-checksums.txt" in (
        verification_job
    )
    assert all(
        dependency in release_job
        for dependency in ("prepare-candidate", "checkpoint-b-evidence", "verify-prepublication-provenance")
    )
    assert workflow.index("  attest-prepublication-provenance:\n") < workflow.index(
        "  verify-prepublication-provenance:\n"
    ) < workflow.index("  release:\n")
    assert "verify-release-evidence" in release_job
