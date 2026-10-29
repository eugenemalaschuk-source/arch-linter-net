from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import aggregate_checkpoint_b_evidence as checkpoint_b_evidence  # noqa: E402
import package_manifest as manifest  # noqa: E402


_COMMIT = "b" * 40
_VERSION = "0.7.0-preview.1"
_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


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


def _annotations_candidate(tmp_path: Path, version: str = "0.10.0") -> tuple[Path, Path]:
    packages = tmp_path / "packages"
    packages.mkdir(parents=True)
    for package_id in manifest._package_ids_for_version(version):  # noqa: SLF001
        kinds = ("package",) if package_id == "ArchLinterNet.Annotations" else manifest._SUBJECT_KINDS
        for kind in kinds:
            path = packages / manifest._expected_filename(package_id, version, kind)
            path.write_bytes(f"{package_id}/{kind}".encode())
    output = packages / "package-manifest.json"
    manifest._create(argparse.Namespace(
        packages_dir=packages,
        version=version,
        source_commit=_COMMIT,
        output=output,
    ))
    return packages, output


def _verify(packages: Path, output: Path, allow_v1: bool = False, version: str = _VERSION) -> None:
    manifest._verify(argparse.Namespace(
        packages_dir=packages,
        manifest=output,
        version=version,
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


@pytest.mark.parametrize(
    ("version", "expected"),
    [
        ("0.9.1", list(manifest._PACKAGE_IDS)),
        ("0.10.0-preview.3", list(manifest._PACKAGE_IDS)),
        ("0.10.0", list(manifest._PACKAGE_IDS_WITH_ANNOTATIONS)),
        ("0.10.7+build.4", list(manifest._PACKAGE_IDS_WITH_ANNOTATIONS)),
        ("0.11.0", list(manifest._PACKAGE_IDS)),
    ],
)
def test_package_set_follows_catalog_generation_compatibility_range(version: str, expected: list[str]) -> None:
    assert list(manifest._package_ids_for_version(version)) == expected  # noqa: SLF001


def test_v2_manifest_rejects_stable_0_10_without_annotations(tmp_path: Path) -> None:
    packages = tmp_path / "packages"
    packages.mkdir()
    version = "0.10.0"
    records = []
    for package_id in manifest._PACKAGE_IDS:
        subjects = {}
        for kind in manifest._SUBJECT_KINDS:
            path = packages / manifest._expected_filename(package_id, version, kind)
            path.write_bytes(f"{package_id}/{kind}".encode())
            subjects[kind] = {
                "kind": kind,
                "file": path.name,
                "size": path.stat().st_size,
                "sha256": manifest._sha256(path),
            }
        records.append({"id": package_id, "version": version, **subjects})
    output = packages / "package-manifest.json"
    output.write_text(json.dumps({
        "schema": "checkpoint-b-candidate-manifest/v2",
        "version": version,
        "source_commit": _COMMIT,
        "packages": records,
    }))

    manifest_arguments = argparse.Namespace(
        packages_dir=packages,
        manifest=output,
        version=version,
        source_commit=_COMMIT,
        allow_v1=False,
    )
    with pytest.raises(ValueError, match="schema is incompatible with its package version"):
        manifest._verify(manifest_arguments)
    with pytest.raises(ValueError, match="schema is incompatible with its package version"):
        checkpoint_b_evidence._read_manifest(output)


@pytest.mark.skipif(shutil.which("dotnet") is None, reason=".NET SDK is required for the pack regression")
def test_dotnet_pack_build_metadata_filename_is_accepted_by_manifest_create() -> None:
    version = "0.10.7+build.4"
    with tempfile.TemporaryDirectory(prefix=".package-manifest-pack-", dir=_REPOSITORY_ROOT) as workspace_name:
        workspace = Path(workspace_name)
        packages = workspace / "packages"
        packages.mkdir()
        project = _REPOSITORY_ROOT / "src/ArchLinterNet.Annotations/ArchLinterNet.Annotations.csproj"
        build_artifacts = workspace / "dotnet-artifacts"
        subprocess.run(
            [
                "dotnet",
                "pack",
                str(project),
                "--configuration",
                "Release",
                "--output",
                str(packages),
                "--artifacts-path",
                str(build_artifacts),
                "--nologo",
                "--verbosity",
                "quiet",
                f"-p:PackageVersion={version}",
                f"-p:Version={version}",
            ],
            cwd=_REPOSITORY_ROOT,
            capture_output=True,
            check=True,
            text=True,
            timeout=180,
        )

        normalized_annotation_package = packages / "ArchLinterNet.Annotations.0.10.7.nupkg"
        assert normalized_annotation_package.is_file()
        assert not (packages / f"ArchLinterNet.Annotations.{version}.nupkg").exists()

        for package_id in manifest._PACKAGE_IDS:
            for kind in manifest._SUBJECT_KINDS:
                path = packages / manifest._expected_filename(package_id, version, kind)
                path.write_bytes(f"{package_id}/{kind}".encode())

        output = packages / "package-manifest.json"
        subprocess.run(
            [
                sys.executable,
                str(_REPOSITORY_ROOT / "tools/release/package_manifest.py"),
                "create",
                "--packages-dir",
                str(packages),
                "--version",
                version,
                "--source-commit",
                _COMMIT,
                "--output",
                str(output),
            ],
            cwd=workspace,
            capture_output=True,
            check=True,
            text=True,
            timeout=30,
        )

        created = json.loads(output.read_text(encoding="utf-8"))
        annotation_record = created["packages"][-1]
        assert created["version"] == version
        assert annotation_record["version"] == version
        assert annotation_record["package"]["file"] == normalized_annotation_package.name
        manifest._verify(argparse.Namespace(
            packages_dir=packages,
            manifest=output,
            version=version,
            source_commit=_COMMIT,
            allow_v1=False,
        ))


def test_v3_manifest_records_source_only_annotations_without_symbol_subject(tmp_path: Path, capsys) -> None:
    packages, output = _annotations_candidate(tmp_path)
    value = json.loads(output.read_text())
    annotation_record = value["packages"][-1]

    assert value["schema"] == "checkpoint-b-candidate-manifest/v3"
    assert [record["id"] for record in value["packages"]] == list(manifest._PACKAGE_IDS_WITH_ANNOTATIONS)
    assert annotation_record == {
        "id": "ArchLinterNet.Annotations",
        "version": "0.10.0",
        "package": {
            "kind": "package",
            "file": "ArchLinterNet.Annotations.0.10.0.nupkg",
            "size": len(b"ArchLinterNet.Annotations/package"),
            "sha256": manifest._sha256(packages / "ArchLinterNet.Annotations.0.10.0.nupkg"),  # noqa: SLF001
        },
    }
    _verify(packages, output, version="0.10.0")

    manifest._paths(argparse.Namespace(packages_dir=packages, manifest=output, kind="all"))
    all_paths = capsys.readouterr().out.splitlines()
    assert len(all_paths) == 9
    assert all_paths[-1] == "ArchLinterNet.Annotations.0.10.0.nupkg"

    checksums = _release_evidence(packages, output)
    assert "ArchLinterNet.Annotations.0.10.0.nupkg" in checksums.read_text()
    package_subjects = tmp_path / "package-subjects.txt"
    manifest._render_attestation_subject_checksums(argparse.Namespace(
        packages_dir=packages,
        manifest=output,
        checksums=checksums,
        subject_class="package",
        output=package_subjects,
    ))
    assert package_subjects.read_text().splitlines()[-1].endswith("ArchLinterNet.Annotations.0.10.0.nupkg")


def test_v3_manifest_rejects_symbols_for_the_source_only_annotations_package(tmp_path: Path) -> None:
    _, output = _annotations_candidate(tmp_path)
    value = json.loads(output.read_text())
    value["packages"][-1]["symbols"] = {
        "kind": "symbols",
        "file": "ArchLinterNet.Annotations.0.10.0.snupkg",
        "size": 1,
        "sha256": "c" * 64,
    }
    output.write_text(json.dumps(value))

    with pytest.raises(ValueError, match="package record is invalid"):
        manifest._load_manifest(output)


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda value: value.update(unexpected=True), "fields are invalid"),
        (lambda value: value.update(schema="checkpoint-b-candidate-manifest/v2"), "Unsupported candidate manifest schema"),
        (lambda value: value.update(version="0.10.0-preview.1"), "schema is incompatible with its package version"),
        (lambda value: value["packages"].pop(), "package inventory is invalid"),
    ],
)
def test_v3_manifest_rejects_invalid_schema_identity_or_inventory(
    tmp_path: Path,
    mutate,
    message: str,
) -> None:
    _, output = _annotations_candidate(tmp_path)
    value = json.loads(output.read_text())
    mutate(value)

    with pytest.raises(ValueError, match=message):
        manifest._validate_v3(value)  # noqa: SLF001


def test_verification_rejects_missing_or_tampered_subjects(tmp_path: Path) -> None:
    packages, output = _candidate(tmp_path)
    (packages / manifest._expected_filename(manifest._PACKAGE_IDS[0], _VERSION, "symbols")).unlink()

    with pytest.raises(ValueError, match="missing="):
        _verify(packages, output)

    packages, output = _candidate(tmp_path / "tampered")
    (packages / manifest._expected_filename(manifest._PACKAGE_IDS[0], _VERSION, "package")).write_bytes(b"tampered")
    with pytest.raises(ValueError, match="digest mismatch"):
        _verify(packages, output)


@pytest.mark.parametrize("extension", [".nupkg", ".snupkg"])
def test_verification_rejects_unexpected_package_or_symbol_asset(tmp_path: Path, extension: str) -> None:
    packages, output = _candidate(tmp_path)
    (packages / f"stale-release-asset{extension}").write_bytes(b"unexpected")

    with pytest.raises(ValueError, match="unexpected="):
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


def test_manifest_creation_rejects_output_outside_the_release_workspace(tmp_path: Path) -> None:
    packages, _ = _candidate(tmp_path)
    output = tmp_path.parent / f"{tmp_path.name}-outside-manifest.json"
    arguments = argparse.Namespace(
        packages_dir=packages,
        version=_VERSION,
        source_commit=_COMMIT,
        output=output,
    )

    with pytest.raises(ValueError, match="outside the release workspace"):
        manifest._create(arguments)

    assert not output.exists()


def test_checksum_rendering_rejects_output_outside_the_release_workspace(tmp_path: Path) -> None:
    _, candidate_manifest = _candidate(tmp_path)
    output = tmp_path.parent / f"{tmp_path.name}-outside-checksums.txt"
    arguments = argparse.Namespace(manifest=candidate_manifest, output=output)

    with pytest.raises(ValueError, match="outside the release workspace"):
        manifest._render_checksums(arguments)

    assert not output.exists()


def test_attestation_rendering_rejects_output_outside_the_release_workspace(tmp_path: Path) -> None:
    packages, candidate_manifest = _candidate(tmp_path)
    checksums = _release_evidence(packages, candidate_manifest)
    output = tmp_path.parent / f"{tmp_path.name}-outside-subjects.txt"
    arguments = argparse.Namespace(
        packages_dir=packages,
        manifest=candidate_manifest,
        checksums=checksums,
        subject_class="package",
        output=output,
    )

    with pytest.raises(ValueError, match="outside the release workspace"):
        manifest._render_attestation_subject_checksums(arguments)

    assert not output.exists()


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


def test_main_dispatches_package_ids_for_stable_annotation_release(monkeypatch, capsys) -> None:
    monkeypatch.setattr(sys, "argv", ["package_manifest.py", "package-ids", "--version", "0.10.0"])

    assert manifest.main() == 0
    assert capsys.readouterr().out.splitlines() == list(manifest._PACKAGE_IDS_WITH_ANNOTATIONS)


def test_main_rejects_invalid_package_ids_version(monkeypatch) -> None:
    monkeypatch.setattr(sys, "argv", ["package_manifest.py", "package-ids", "--version", "not-a-version"])

    with pytest.raises(ValueError, match="Candidate package version is invalid"):
        manifest.main()


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
