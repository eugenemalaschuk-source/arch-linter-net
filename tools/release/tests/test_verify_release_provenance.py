from __future__ import annotations

import argparse
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
        gh_command="gh",
    )


def test_verified_subjects_include_every_package_and_outer_evidence(tmp_path: Path) -> None:
    arguments = _arguments(tmp_path)

    subjects = provenance._verified_subjects(arguments)

    assert [subject.name for subject in subjects[:-2]] == [
        manifest._expected_filename(package_id, _VERSION, kind)
        for package_id in manifest._PACKAGE_IDS
        for kind in manifest._SUBJECT_KINDS
    ]
    assert [subject.name for subject in subjects[-2:]] == ["package-manifest.json", "package-checksums.txt"]


def test_main_verifies_every_subject_with_repository_workflow_and_source_constraints(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)
    calls: list[list[str]] = []

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        calls.append(command)
        return subprocess.CompletedProcess(command, 0, "", "")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    with pytest.raises(ValueError, match="Tampered release subject unexpectedly verified"):
        provenance.main()

    assert calls[0][0:4] == ["gh", "attestation", "verify", str(arguments.packages_dir / "ArchLinterNet.CEL.0.7.0-preview.1.nupkg")]
    assert calls[0][-6:] == [
        "--repo",
        "owner/repository",
        "--signer-workflow",
        "owner/repository/.github/workflows/release-nuget.yml",
        "--source-digest",
        _COMMIT,
    ]


def test_main_fails_when_an_expected_subject_has_no_attestation(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(command, 1, "", "attestation not found")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    with pytest.raises(ValueError, match="Attestation verification failed"):
        provenance.main()


def test_main_requires_tampered_package_manifest_and_checksum_to_fail(tmp_path: Path, monkeypatch) -> None:
    arguments = _arguments(tmp_path)
    tampered_names: list[str] = []

    def fake_run(command: list[str], **_: object) -> subprocess.CompletedProcess[str]:
        subject = Path(command[3])
        if subject.name.startswith("tampered-"):
            tampered_names.append(subject.name.removeprefix("tampered-"))
            return subprocess.CompletedProcess(command, 1, "", "attestation not found")
        return subprocess.CompletedProcess(command, 0, "", "")

    monkeypatch.setattr(provenance, "_parse_args", lambda: arguments)
    monkeypatch.setattr(provenance.subprocess, "run", fake_run)

    assert provenance.main() == 0
    assert tampered_names == [
        "ArchLinterNet.CEL.0.7.0-preview.1.nupkg",
        "package-manifest.json",
        "package-checksums.txt",
    ]
