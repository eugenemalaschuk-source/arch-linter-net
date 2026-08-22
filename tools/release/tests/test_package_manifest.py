from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import package_manifest as manifest  # noqa: E402


_COMMIT = "b" * 40
_VERSION = "0.7.0-preview.1"


@pytest.fixture(autouse=True)
def _release_workspace(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)


def _candidate(tmp_path: Path) -> tuple[Path, Path]:
    packages = tmp_path / "packages"
    packages.mkdir(parents=True)
    for package_id in manifest._PACKAGE_IDS:
        for kind in manifest._SUBJECT_KINDS:
            path = packages / manifest._expected_filename(package_id, _VERSION, kind)
            path.write_bytes(f"{package_id}/{kind}".encode())
    output = packages / "package-manifest.json"
    manifest._create(argparse.Namespace(
        packages_dir=packages,
        version=_VERSION,
        source_commit=_COMMIT,
        output=output,
    ))
    return packages, output


def _verify(packages: Path, output: Path, allow_v1: bool = False) -> None:
    manifest._verify(argparse.Namespace(
        packages_dir=packages,
        manifest=output,
        version=_VERSION,
        source_commit=_COMMIT,
        allow_v1=allow_v1,
    ))


def _release_evidence(packages: Path, output: Path) -> Path:
    checksums = packages / "package-checksums.txt"
    manifest._render_checksums(argparse.Namespace(manifest=output, output=checksums))
    manifest._verify_release_evidence(packages, output, checksums)
    return checksums


def test_create_records_deterministic_paired_subject_inventory(tmp_path: Path) -> None:
    packages, output = _candidate(tmp_path)

    value = json.loads(output.read_text())

    assert value["schema"] == "checkpoint-b-candidate-manifest/v2"
    assert [record["id"] for record in value["packages"]] == list(manifest._PACKAGE_IDS)
    assert [record["package"]["kind"] for record in value["packages"]] == ["package"] * 4
    assert [record["symbols"]["kind"] for record in value["packages"]] == ["symbols"] * 4
    _verify(packages, output)


def test_verification_rejects_missing_unexpected_or_tampered_subjects(tmp_path: Path) -> None:
    packages, output = _candidate(tmp_path)
    (packages / manifest._expected_filename(manifest._PACKAGE_IDS[0], _VERSION, "symbols")).unlink()

    with pytest.raises(ValueError, match="missing="):
        _verify(packages, output)

    packages, output = _candidate(tmp_path / "unexpected")
    (packages / "unrelated.0.7.0-preview.1.nupkg").write_bytes(b"unexpected")
    with pytest.raises(ValueError, match="unexpected="):
        _verify(packages, output)

    packages, output = _candidate(tmp_path / "tampered")
    (packages / manifest._expected_filename(manifest._PACKAGE_IDS[0], _VERSION, "package")).write_bytes(b"tampered")
    with pytest.raises(ValueError, match="digest mismatch"):
        _verify(packages, output)


def test_manifest_reading_rejects_a_path_outside_the_release_workspace(tmp_path: Path) -> None:
    outside_manifest = tmp_path.parent / "outside-candidate-manifest.json"
    outside_manifest.write_text("{}")

    with pytest.raises(ValueError, match="outside the release workspace"):
        manifest._read_manifest(outside_manifest)


def test_hashing_rejects_a_path_outside_the_release_workspace(tmp_path: Path) -> None:
    outside_subject = tmp_path.parent / "outside-release-subject.nupkg"
    outside_subject.write_bytes(b"outside")

    with pytest.raises(ValueError, match="outside the release workspace"):
        manifest._sha256(outside_subject)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda value: value["packages"][0]["symbols"].update(file="ArchLinterNet.CEL.0.7.0-preview.1.nupkg"),
        lambda value: value["packages"][0].update(version="9.9.9"),
        lambda value: value["packages"][1].update(id="ArchLinterNet.CEL"),
    ],
)
def test_verification_rejects_ambiguous_or_inconsistent_manifest_identity(tmp_path: Path, mutate) -> None:
    packages, output = _candidate(tmp_path)
    value = json.loads(output.read_text())
    mutate(value)
    output.write_text(json.dumps(value))

    with pytest.raises(ValueError):
        _verify(packages, output)


def test_checksum_rendering_and_subject_paths_are_deterministic(tmp_path: Path, capsys) -> None:
    packages, output = _candidate(tmp_path)
    checksums = tmp_path / "checksums.txt"
    manifest._render_checksums(argparse.Namespace(manifest=output, output=checksums))
    first = checksums.read_bytes()
    manifest._render_checksums(argparse.Namespace(manifest=output, output=checksums))

    manifest._paths(argparse.Namespace(packages_dir=packages, manifest=output, kind="all"))
    paths = capsys.readouterr().out.splitlines()

    assert checksums.read_bytes() == first
    assert len(paths) == 8
    assert paths[::2] == [manifest._expected_filename(package_id, _VERSION, "package") for package_id in manifest._PACKAGE_IDS]
    assert paths[1::2] == [manifest._expected_filename(package_id, _VERSION, "symbols") for package_id in manifest._PACKAGE_IDS]
    assert all(path in checksums.read_text() for path in paths)


def test_release_evidence_verification_rejects_missing_unexpected_or_tampered_evidence(tmp_path: Path) -> None:
    packages, output = _candidate(tmp_path)
    checksums = _release_evidence(packages, output)
    checksums.write_text("tampered\n")

    with pytest.raises(ValueError, match="differs from the manifest rendering"):
        manifest._verify_release_evidence(packages, output, checksums)

    packages, output = _candidate(tmp_path / "missing")
    checksums = _release_evidence(packages, output)
    checksums.unlink()
    with pytest.raises(ValueError, match="differs from the manifest rendering"):
        manifest._verify_release_evidence(packages, output, checksums)

    packages, output = _candidate(tmp_path / "unexpected")
    checksums = _release_evidence(packages, output)
    (packages / "unrelated-release-evidence.txt").write_text("unexpected\n")
    with pytest.raises(ValueError, match="unexpected="):
        manifest._verify_release_evidence(packages, output, checksums)


def test_attestation_subject_checksums_are_exact_and_deterministic(tmp_path: Path) -> None:
    packages, output = _candidate(tmp_path)
    checksums = _release_evidence(packages, output)
    package_subjects = tmp_path / "package-subjects.txt"
    evidence_subjects = tmp_path / "evidence-subjects.txt"

    for subject_class, target in (("package", package_subjects), ("evidence", evidence_subjects)):
        manifest._render_attestation_subject_checksums(argparse.Namespace(
            packages_dir=packages,
            manifest=output,
            checksums=checksums,
            subject_class=subject_class,
            output=target,
        ))

    package_lines = package_subjects.read_text().splitlines()
    evidence_lines = evidence_subjects.read_text().splitlines()
    assert [line.rsplit("  ", maxsplit=1)[1] for line in package_lines] == [
        manifest._expected_filename(package_id, _VERSION, kind)
        for package_id in manifest._PACKAGE_IDS
        for kind in manifest._SUBJECT_KINDS
    ]
    assert [line.rsplit("  ", maxsplit=1)[1] for line in evidence_lines] == [
        "package-manifest.json",
        "package-checksums.txt",
    ]
    assert all(line.count("  ") == 1 for line in package_lines + evidence_lines)


def test_main_dispatches_the_verified_manifest_path_listing(tmp_path: Path, monkeypatch, capsys) -> None:
    packages, output = _candidate(tmp_path)
    monkeypatch.setattr(sys, "argv", [
        "package_manifest.py",
        "paths",
        "--packages-dir", str(packages),
        "--manifest", str(output),
        "--kind", "package",
    ])

    assert manifest.main() == 0
    assert capsys.readouterr().out.splitlines() == [
        manifest._expected_filename(package_id, _VERSION, "package")
        for package_id in manifest._PACKAGE_IDS
    ]


def test_v1_reading_requires_explicit_compatibility_mode(tmp_path: Path) -> None:
    packages = tmp_path / "packages"
    packages.mkdir()
    records = []
    for package_id in manifest._PACKAGE_IDS:
        path = packages / manifest._expected_filename(package_id, _VERSION, "package")
        path.write_bytes(package_id.encode())
        records.append({
            "id": package_id,
            "version": _VERSION,
            "file": path.name,
            "size": path.stat().st_size,
            "sha256": manifest._sha256(path),
        })
    output = packages / "v1.json"
    output.write_text(json.dumps({
        "schema": "checkpoint-b-candidate-manifest/v1",
        "version": _VERSION,
        "source_commit": _COMMIT,
        "packages": records,
    }))

    with pytest.raises(ValueError, match="Unsupported candidate manifest schema"):
        manifest._load_manifest(output)
    _verify(packages, output, allow_v1=True)
    historical_manifest = manifest._load_manifest(output, allow_v1=True)
    with pytest.raises(ValueError, match="complete package-subject inventory"):
        manifest._subjects(historical_manifest)
