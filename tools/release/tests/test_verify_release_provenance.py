from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import package_manifest as manifest  # noqa: E402
import verify_release_provenance as provenance  # noqa: E402


_COMMIT = "c" * 40
_VERSION = "0.7.0-preview.1"


@pytest.fixture(autouse=True)
def _release_workspace(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)


def _arguments(tmp_path: Path) -> argparse.Namespace:
    packages = tmp_path / "packages"
    packages.mkdir()
    for package_id in manifest._PACKAGE_IDS:
        for kind in manifest._SUBJECT_KINDS:
            (packages / manifest._expected_filename(package_id, _VERSION, kind)).write_bytes(
                f"{package_id}/{kind}".encode()
            )
    candidate_manifest = packages / "package-manifest.json"
    manifest._create(argparse.Namespace(
        packages_dir=packages,
        version=_VERSION,
        source_commit=_COMMIT,
        output=candidate_manifest,
    ))
    checksums = packages / "package-checksums.txt"
    manifest._render_checksums(argparse.Namespace(manifest=candidate_manifest, output=checksums))
    return argparse.Namespace(
        packages_dir=packages,
        manifest=candidate_manifest,
        checksums=checksums,
        version=_VERSION,
        source_commit=_COMMIT,
        repository="owner/repository",
        signer_workflow="owner/repository/.github/workflows/release-nuget.yml",
    )


def _verification_output(subject: Path) -> str:
    return json.dumps([{
        "verificationResult": {
            "statement": {
                "subject": [{"name": subject.name, "digest": {"sha256": manifest._sha256(subject)}}],
            },
        },
    }])


def test_verified_subjects_include_every_package_and_outer_evidence(tmp_path: Path) -> None:
    arguments = _arguments(tmp_path)

    subjects = provenance._verified_subjects(arguments)

    assert [subject.name for subject in subjects] == [
        manifest._expected_filename(package_id, _VERSION, kind)
        for package_id in manifest._PACKAGE_IDS
        for kind in manifest._SUBJECT_KINDS
    ]


def test_main_authenticates_outer_evidence_before_deriving_package_inventory(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)
    calls: list[list[str]] = []

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        calls.append(command)
        return subprocess.CompletedProcess(command, 0, _verification_output(Path(command[3])), "")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    assert provenance.main() == 0

    assert [Path(command[3]).name for command in calls[:3]] == [
        "package-manifest.json",
        "package-checksums.txt",
        "ArchLinterNet.CEL.0.7.0-preview.1.nupkg",
    ]
    assert calls[0][4:10] == [
        "--repo",
        "owner/repository",
        "--signer-workflow",
        "owner/repository/.github/workflows/release-nuget.yml",
        "--source-digest",
        _COMMIT,
    ]
    assert all(command[-2:] == ["--format", "json"] for command in calls)


def test_command_uses_only_the_github_cli_executable(tmp_path: Path) -> None:
    arguments = _arguments(tmp_path)
    arguments.gh_command = "untrusted-command"

    command = provenance._command(arguments, arguments.manifest)

    assert command[0] == "gh"


@pytest.mark.parametrize(
    ("attribute", "value"),
    [
        ("repository", "--repo=attacker/repository"),
        ("signer_workflow", "owner/repository/--config"),
        ("source_commit", "not-a-commit"),
    ],
)
def test_command_rejects_invalid_attestation_selectors(tmp_path: Path, attribute: str, value: str) -> None:
    arguments = _arguments(tmp_path)
    setattr(arguments, attribute, value)

    with pytest.raises(ValueError, match="is invalid"):
        provenance._command(arguments, arguments.manifest)


def test_main_fails_when_an_expected_subject_has_no_attestation(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(command, 1, "", "attestation not found")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    with pytest.raises(ValueError, match="Attestation verification failed"):
        provenance.main()


def test_main_fails_closed_when_attestation_verification_has_an_infrastructure_error(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(command, 1, "", "GitHub API connection refused")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    with pytest.raises(ValueError, match="GitHub API connection refused"):
        provenance.main()


def test_main_compares_tampered_subjects_with_already_verified_attestation_digests(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)
    verified_names: list[str] = []

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        subject = Path(command[3])
        verified_names.append(subject.name)
        return subprocess.CompletedProcess(command, 0, _verification_output(subject), "")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    assert provenance.main() == 0
    assert verified_names == [
        "package-manifest.json",
        "package-checksums.txt",
        "ArchLinterNet.CEL.0.7.0-preview.1.nupkg",
        "ArchLinterNet.CEL.0.7.0-preview.1.snupkg",
        "ArchLinterNet.Cli.0.7.0-preview.1.nupkg",
        "ArchLinterNet.Cli.0.7.0-preview.1.snupkg",
        "ArchLinterNet.Core.0.7.0-preview.1.nupkg",
        "ArchLinterNet.Core.0.7.0-preview.1.snupkg",
        "ArchLinterNet.Testing.0.7.0-preview.1.nupkg",
        "ArchLinterNet.Testing.0.7.0-preview.1.snupkg",
    ]


def test_verified_attestation_output_must_contain_the_verified_subject_digest(tmp_path: Path) -> None:
    arguments = _arguments(tmp_path)
    subject = arguments.manifest
    attestations = [{"verificationResult": {"statement": {"subject": [{"digest": {"sha256": "0" * 64}}]}}}]

    with pytest.raises(ValueError, match="does not contain"):
        provenance._verified_attestation_digests(subject, attestations)
