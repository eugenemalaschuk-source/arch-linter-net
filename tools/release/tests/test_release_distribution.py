from __future__ import annotations

import argparse
import gzip
import hashlib
import io
import json
import subprocess
import sys
import tarfile
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import package_manifest  # noqa: E402
import release_distribution as distribution  # noqa: E402


ROOT = Path(__file__).resolve().parents[3]
INVENTORY = ROOT / ".github" / "badge-promotion" / "release-inventory.json"


@pytest.fixture(autouse=True)
def _release_workspace(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)


def _source_commit() -> str:
    return subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", "HEAD"], text=True).strip()


def _candidate(tmp_path: Path, version: str = "0.8.7-preview.1") -> tuple[Path, str]:
    tmp_path.mkdir(parents=True, exist_ok=True)
    packages = tmp_path / "candidate"
    packages.mkdir()
    source_commit = _source_commit()
    for package_id in package_manifest._PACKAGE_IDS:
        for kind in package_manifest._SUBJECT_KINDS:
            path = packages / package_manifest._expected_filename(package_id, version, kind)
            path.write_bytes(f"{package_id}/{kind}".encode())
    package_manifest._create(
        argparse.Namespace(
            packages_dir=packages,
            version=version,
            source_commit=source_commit,
            output=packages / "package-manifest.json",
        )
    )
    return packages / "package-manifest.json", source_commit


def _arguments(tmp_path: Path, version: str = "0.8.7-preview.1") -> tuple[argparse.Namespace, Path]:
    candidate_manifest, source_commit = _candidate(tmp_path, version)
    transport = tmp_path / "transport"
    arguments = argparse.Namespace(
        source_root=ROOT,
        inventory=INVENTORY,
        candidate_manifest=candidate_manifest,
        version=version,
        source_commit=source_commit,
        output_dir=transport,
    )
    distribution._create(arguments)
    return arguments, transport


def _verify_arguments(arguments: argparse.Namespace, transport: Path) -> argparse.Namespace:
    return argparse.Namespace(
        source_root=arguments.source_root,
        inventory=arguments.inventory,
        candidate_manifest=arguments.candidate_manifest,
        version=arguments.version,
        source_commit=arguments.source_commit,
        transport_dir=transport,
        manifest=transport / distribution._MANIFEST_FILE,
        checksums=transport / distribution._CHECKSUMS_FILE,
    )


def test_create_emits_the_six_frozen_transport_outputs_and_deterministic_bytes(tmp_path: Path) -> None:
    arguments, first = _arguments(tmp_path / "first")
    second = tmp_path / "second"
    second.mkdir()
    arguments.output_dir = second
    distribution._create(arguments)

    expected = {
        distribution._ARCHIVE_FILE_TEMPLATE.format(version=arguments.version),
        distribution._WORKFLOW_FILE,
        distribution._ACTION_FILE,
        distribution._COMPATIBILITY_FILE_TEMPLATE.format(version=arguments.version),
        distribution._MANIFEST_FILE,
        distribution._CHECKSUMS_FILE,
    }
    assert {path.name for path in first.iterdir()} == expected
    assert {path.name for path in second.iterdir()} == expected
    assert {path.name: path.read_bytes() for path in first.iterdir()} == {
        path.name: path.read_bytes() for path in second.iterdir()
    }


def test_cli_round_trip_supports_all_requested_commands(tmp_path: Path, monkeypatch, capsys) -> None:
    candidate_manifest, source_commit = _candidate(tmp_path)
    transport = tmp_path / "transport"
    common = [
        "release_distribution.py",
        "--source-root",
        str(ROOT),
        "--inventory",
        str(INVENTORY),
        "--candidate-manifest",
        str(candidate_manifest),
        "--version",
        "0.8.7-preview.1",
        "--source-commit",
        source_commit,
    ]
    monkeypatch.setattr(sys, "argv", [common[0], "create", *common[1:], "--output-dir", str(transport)])
    assert distribution.main() == 0
    monkeypatch.setattr(
        sys,
        "argv",
        [
            common[0],
            "verify",
            *common[1:],
            "--transport-dir",
            str(transport),
            "--manifest",
            str(transport / distribution._MANIFEST_FILE),
            "--checksums",
            str(transport / distribution._CHECKSUMS_FILE),
        ],
    )
    assert distribution.main() == 0
    output = tmp_path / "subjects.sha256"
    monkeypatch.setattr(
        sys,
        "argv",
        [
            common[0],
            "render-attestation-subject-checksums",
            "--transport-dir",
            str(transport),
            "--manifest",
            str(transport / distribution._MANIFEST_FILE),
            "--checksums",
            str(transport / distribution._CHECKSUMS_FILE),
            "--subject-class",
            "transport",
            "--output",
            str(output),
        ],
    )
    assert distribution.main() == 0
    assert len(output.read_text(encoding="utf-8").splitlines()) == 4
    monkeypatch.setattr(
        sys,
        "argv",
        [
            common[0],
            "paths",
            "--transport-dir",
            str(transport),
            "--manifest",
            str(transport / distribution._MANIFEST_FILE),
            "--kind",
            "subjects",
        ],
    )
    assert distribution.main() == 0
    assert len(capsys.readouterr().out.splitlines()) == 4


def test_metadata_binds_candidate_packages_and_exact_publisher_bytes(tmp_path: Path) -> None:
    arguments, transport = _arguments(tmp_path)
    metadata_path = transport / distribution._COMPATIBILITY_FILE_TEMPLATE.format(version=arguments.version)
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    candidate = json.loads(arguments.candidate_manifest.read_text(encoding="utf-8"))
    manifest_sha = hashlib.sha256(arguments.candidate_manifest.read_bytes()).hexdigest()

    assert metadata["version"] == candidate["version"] == arguments.version
    assert metadata["source_commit"] == candidate["source_commit"] == arguments.source_commit
    assert metadata["candidate_manifest_sha256"] == manifest_sha
    assert metadata["package_ids"] == list(package_manifest._PACKAGE_IDS)
    assert metadata["compatibility"] == distribution._COMPATIBILITY_IDENTITIES
    assert metadata["approved_publisher_commit"] == distribution._APPROVED_PUBLISHER_COMMIT
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    compatibility = inventory["compatibility"]
    assert metadata["publisher"]["workflow"]["git_blob_sha"] == compatibility["workflow_source_sha"]
    assert metadata["publisher"]["action"]["git_blob_sha"] == compatibility["action_source_sha"]
    assert metadata["publisher"]["workflow"]["sha256"] == next(
        subject["sha256"] for subject in json.loads((transport / distribution._MANIFEST_FILE).read_text(encoding="utf-8"))["subjects"]
        if subject["kind"] == "publisher-workflow"
    )
    assert metadata["publisher"]["action"]["sha256"] == next(
        subject["sha256"] for subject in json.loads((transport / distribution._MANIFEST_FILE).read_text(encoding="utf-8"))["subjects"]
        if subject["kind"] == "publisher-action"
    )


def test_verify_accepts_any_0_8_x_candidate_and_rejects_wrong_binding(tmp_path: Path) -> None:
    arguments, transport = _arguments(tmp_path, "0.8.19")
    distribution._verify(_verify_arguments(arguments, transport))

    with pytest.raises(ValueError, match="source commit"):
        wrong_source = _verify_arguments(arguments, transport)
        wrong_source.source_commit = "b" * 40
        distribution._verify(wrong_source)
    with pytest.raises(ValueError, match="version"):
        wrong_version = _verify_arguments(arguments, transport)
        wrong_version.version = "0.8.20"
        distribution._verify(wrong_version)


@pytest.mark.parametrize("mutation", ["missing", "tampered", "incompatible"])
def test_verify_fails_closed_for_missing_tampered_or_incompatible_subjects(tmp_path: Path, mutation: str) -> None:
    arguments, transport = _arguments(tmp_path)
    if mutation == "missing":
        (transport / distribution._COMPATIBILITY_FILE_TEMPLATE.format(version=arguments.version)).unlink()
    elif mutation == "tampered":
        archive = transport / distribution._ARCHIVE_FILE_TEMPLATE.format(version=arguments.version)
        archive.write_bytes(archive.read_bytes() + b"tamper")
    else:
        metadata_path = transport / distribution._COMPATIBILITY_FILE_TEMPLATE.format(version=arguments.version)
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        metadata["compatibility"]["storage"] = "v2"
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")

    with pytest.raises(ValueError):
        distribution._verify(_verify_arguments(arguments, transport))


def test_archive_has_sorted_regular_members_and_fixed_metadata(tmp_path: Path) -> None:
    arguments, transport = _arguments(tmp_path)
    manifest = json.loads((transport / distribution._MANIFEST_FILE).read_text(encoding="utf-8"))
    archive_path = transport / next(subject["file"] for subject in manifest["subjects"] if subject["kind"] == "relay-archive")
    with gzip.open(archive_path, "rb") as compressed, tarfile.open(fileobj=compressed, mode="r") as archive:
        members = archive.getmembers()
        assert [member.name for member in members] == sorted(member.name for member in members)
        assert all(member.isreg() and member.mtime == 0 and member.uid == 0 and member.gid == 0 for member in members)
        assert all("synthetic" not in member.name.lower() for member in members)

    distribution._verify(_verify_arguments(arguments, transport))


def _malicious_archive(name: str, symlink: bool = False) -> bytes:
    output = io.BytesIO()
    with gzip.GzipFile(fileobj=output, mode="wb", filename="", mtime=0) as compressed:
        with tarfile.open(fileobj=compressed, mode="w", format=tarfile.USTAR_FORMAT) as archive:
            info = tarfile.TarInfo(name)
            if symlink:
                info.type = tarfile.SYMTYPE
                info.linkname = "outside"
            else:
                info.size = 1
            archive.addfile(info, None if symlink else io.BytesIO(b"x"))
    return output.getvalue()


@pytest.mark.parametrize("archive_bytes", [_malicious_archive("../escape"), _malicious_archive("relay/src/index.ts", symlink=True)])
def test_archive_safety_rejects_traversal_and_symlink_members(archive_bytes: bytes) -> None:
    expected = [{"file": "relay/src/index.ts", "size": 1, "sha256": hashlib.sha256(b"x").hexdigest()}]
    with pytest.raises(ValueError):
        distribution._verify_archive(archive_bytes, expected)


def test_attestation_and_path_commands_exclude_recursive_outer_evidence(tmp_path: Path, capsys) -> None:
    arguments, transport = _arguments(tmp_path)
    manifest = transport / distribution._MANIFEST_FILE
    checksums = transport / distribution._CHECKSUMS_FILE
    transport_subjects = tmp_path / "transport-subjects.sha256"
    evidence_subjects = tmp_path / "evidence-subjects.sha256"
    distribution._render_attestation_subject_checksums(
        argparse.Namespace(
            transport_dir=transport,
            manifest=manifest,
            checksums=checksums,
            subject_class="transport",
            output=transport_subjects,
        )
    )
    distribution._render_attestation_subject_checksums(
        argparse.Namespace(
            transport_dir=transport,
            manifest=manifest,
            checksums=checksums,
            subject_class="evidence",
            output=evidence_subjects,
        )
    )
    distribution._paths(argparse.Namespace(transport_dir=transport, manifest=manifest, kind="subjects"))
    subject_paths = capsys.readouterr().out.splitlines()
    manifest_names = {record["file"] for record in json.loads(manifest.read_text(encoding="utf-8"))["subjects"]}
    assert set(subject_paths) == manifest_names
    assert distribution._MANIFEST_FILE not in transport_subjects.read_text(encoding="utf-8")
    assert distribution._CHECKSUMS_FILE not in transport_subjects.read_text(encoding="utf-8")
    assert [line.rsplit("  ", 1)[1] for line in evidence_subjects.read_text(encoding="utf-8").splitlines()] == [
        distribution._MANIFEST_FILE,
        distribution._CHECKSUMS_FILE,
    ]
    distribution._paths(argparse.Namespace(transport_dir=transport, manifest=manifest, kind="all"))
    assert capsys.readouterr().out.splitlines()[-2:] == [distribution._MANIFEST_FILE, distribution._CHECKSUMS_FILE]


def test_inventory_is_closed_to_pin_drift_and_unrelated_scope(tmp_path: Path) -> None:
    value = json.loads(INVENTORY.read_text(encoding="utf-8"))
    value["excluded"].remove("#787")
    path = tmp_path / "inventory.json"
    path.write_text(json.dumps(value), encoding="utf-8")
    with pytest.raises(ValueError, match="exclusions"):
        distribution._load_inventory(path)
