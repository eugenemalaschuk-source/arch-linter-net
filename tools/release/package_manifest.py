#!/usr/bin/env python3
"""Create, verify, and render immutable NuGet candidate package manifests."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

from _release_workspace import _safe_path

_PACKAGE_IDS = (
    "ArchLinterNet.CEL",
    "ArchLinterNet.Cli",
    "ArchLinterNet.Core",
    "ArchLinterNet.Testing",
)
_ANNOTATIONS_PACKAGE_ID = "ArchLinterNet.Annotations"
_PACKAGE_IDS_WITH_ANNOTATIONS = (*_PACKAGE_IDS, _ANNOTATIONS_PACKAGE_ID)
_SCHEMA_V1 = "checkpoint-b-candidate-manifest/v1"
_SCHEMA_V2 = "checkpoint-b-candidate-manifest/v2"
_SCHEMA_V3 = "checkpoint-b-candidate-manifest/v3"
_SUBJECT_KINDS = ("package", "symbols")
_CANONICAL_MANIFEST_FILE = "package-manifest.json"
_CANONICAL_CHECKSUM_FILE = "package-checksums.txt"
_CANDIDATE_MANIFEST_LABEL = "candidate manifest"
_CANDIDATE_CHECKSUM_EVIDENCE_LABEL = "candidate checksum evidence"
_INVALID_PACKAGE_INVENTORY_MESSAGE = "Candidate manifest package inventory is invalid."
_SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
_SOURCE_COMMIT_PATTERN = re.compile(r"[0-9a-f]{40,64}")
_UNSUPPORTED_SCHEMA = "Unsupported candidate manifest schema."


def _sha256(path: Path) -> str:
    path = _safe_path(path, "release subject")
    digest = hashlib.sha256()
    # The subject is confined by _safe_path above before it is read.
    with path.open("rb") as source:  # NOSONAR(S2083,S8707)
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _expected_filename(package_id: str, version: str, kind: str) -> str:
    extension = ".nupkg" if kind == "package" else ".snupkg"
    # NuGet removes SemVer build metadata from the package filename even though the
    # candidate and package metadata retain the complete version identity.
    artifact_version = version.partition("+")[0]
    return f"{package_id}.{artifact_version}{extension}"


def _package_ids_for_version(version: str) -> tuple[str, ...]:
    """Return the reviewed package set for this release line.

    Catalog generation 1 is explicitly compatible with stable 0.10.x package versions. Preview
    candidates and later minor lines keep the existing four-package inventory until the catalog
    contract is revised.
    """
    from calculate_version import parse_package_version

    parsed = parse_package_version(version)
    if parsed is not None and parsed[0:2] == (0, 10) and parsed[3] is None:
        return _PACKAGE_IDS_WITH_ANNOTATIONS
    return _PACKAGE_IDS


def _subject(path: Path, kind: str) -> dict[str, Any]:
    return {
        "kind": kind,
        "file": path.name,
        "size": path.stat().st_size,
        "sha256": _sha256(path),
    }


def _read_manifest(path: Path) -> dict[str, Any]:
    path = _safe_path(path, _CANDIDATE_MANIFEST_LABEL)
    try:
        # _safe_path confines this read to the release workspace or repository root.
        value = json.loads(path.read_text(encoding="utf-8"))  # NOSONAR
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read candidate manifest '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError("Candidate manifest must be a JSON object.")
    return value


def _validate_identity(manifest: dict[str, Any]) -> None:
    version = manifest.get("version")
    source_commit = manifest.get("source_commit")
    if not isinstance(version, str) or not version or Path(version).name != version:
        raise ValueError("Candidate manifest version is invalid.")
    if not isinstance(source_commit, str) or not _SOURCE_COMMIT_PATTERN.fullmatch(source_commit):
        raise ValueError("Candidate manifest source commit is invalid.")


def _validate_subject(subject: Any, package_id: str, version: str, kind: str) -> dict[str, Any]:
    if not isinstance(subject, dict) or set(subject) != {"kind", "file", "size", "sha256"}:
        raise ValueError("Candidate manifest subject record is invalid.")
    if subject.get("kind") != kind:
        raise ValueError("Candidate manifest subject kind is invalid.")
    if subject.get("file") != _expected_filename(package_id, version, kind):
        raise ValueError("Candidate manifest subject filename is invalid.")
    if not isinstance(subject.get("size"), int) or isinstance(subject["size"], bool) or subject["size"] < 0:
        raise ValueError("Candidate manifest subject size is invalid.")
    if not isinstance(subject.get("sha256"), str) or not _SHA256_PATTERN.fullmatch(subject["sha256"]):
        raise ValueError("Candidate manifest subject digest is invalid.")
    return subject


def _validate_v2(manifest: dict[str, Any]) -> dict[str, Any]:
    if set(manifest) != {"schema", "version", "source_commit", "packages"}:
        raise ValueError("Candidate manifest fields are invalid.")
    if manifest.get("schema") != _SCHEMA_V2:
        raise ValueError(_UNSUPPORTED_SCHEMA)
    _validate_identity(manifest)
    version = manifest["version"]
    if _package_ids_for_version(version) != _PACKAGE_IDS:
        raise ValueError("Candidate manifest schema is incompatible with its package version.")
    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != len(_PACKAGE_IDS):
        raise ValueError(_INVALID_PACKAGE_INVENTORY_MESSAGE)

    subject_files: set[str] = set()
    for package_id, record in zip(_PACKAGE_IDS, packages, strict=True):
        _validate_package_record(record, package_id, version, subject_files, require_symbols=True)
    return manifest


def _validate_v3(manifest: dict[str, Any]) -> dict[str, Any]:
    if set(manifest) != {"schema", "version", "source_commit", "packages"}:
        raise ValueError("Candidate manifest fields are invalid.")
    if manifest.get("schema") != _SCHEMA_V3:
        raise ValueError(_UNSUPPORTED_SCHEMA)
    _validate_identity(manifest)
    version = manifest["version"]
    package_ids = _package_ids_for_version(version)
    if package_ids != _PACKAGE_IDS_WITH_ANNOTATIONS:
        raise ValueError("Candidate manifest schema is incompatible with its package version.")
    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != len(package_ids):
        raise ValueError(_INVALID_PACKAGE_INVENTORY_MESSAGE)

    subject_files: set[str] = set()
    for package_id, record in zip(package_ids, packages, strict=True):
        _validate_package_record(
            record,
            package_id,
            version,
            subject_files,
            require_symbols=package_id != _ANNOTATIONS_PACKAGE_ID,
        )
    return manifest


def _validate_package_record(
    record: Any,
    package_id: str,
    version: str,
    subject_files: set[str],
    *,
    require_symbols: bool,
) -> None:
    expected_fields = {"id", "version", "package", "symbols"} if require_symbols else {"id", "version", "package"}
    if not isinstance(record, dict) or set(record) != expected_fields:
        raise ValueError("Candidate manifest package record is invalid.")
    if record.get("id") != package_id or record.get("version") != version:
        raise ValueError("Candidate manifest package identity is invalid.")
    for kind in ("package", "symbols") if require_symbols else ("package",):
        subject = _validate_subject(record.get(kind), package_id, version, kind)
        if subject["file"] in subject_files:
            raise ValueError("Candidate manifest contains duplicated subject files.")
        subject_files.add(subject["file"])


def _validate_v1(manifest: dict[str, Any]) -> dict[str, Any]:
    """Read only historical v1 evidence; it intentionally proves no symbol coverage."""
    if set(manifest) != {"schema", "version", "source_commit", "packages"} or manifest.get("schema") != _SCHEMA_V1:
        raise ValueError(_UNSUPPORTED_SCHEMA)
    _validate_identity(manifest)
    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != len(_PACKAGE_IDS):
        raise ValueError(_INVALID_PACKAGE_INVENTORY_MESSAGE)
    for package_id, record in zip(_PACKAGE_IDS, packages, strict=True):
        if not isinstance(record, dict) or set(record) != {"id", "version", "file", "size", "sha256"}:
            raise ValueError("Candidate manifest package record is invalid.")
        if record.get("id") != package_id or record.get("version") != manifest["version"]:
            raise ValueError("Candidate manifest package identity is invalid.")
        _validate_subject(
            {"kind": "package", **{key: record.get(key) for key in ("file", "size", "sha256")}},
            package_id,
            manifest["version"],
            "package",
        )
    return manifest


def _load_manifest(path: Path, allow_v1: bool = False) -> dict[str, Any]:
    manifest = _read_manifest(path)
    if manifest.get("schema") == _SCHEMA_V2:
        return _validate_v2(manifest)
    if manifest.get("schema") == _SCHEMA_V3:
        return _validate_v3(manifest)
    if allow_v1 and manifest.get("schema") == _SCHEMA_V1:
        return _validate_v1(manifest)
    raise ValueError(_UNSUPPORTED_SCHEMA)


def _subjects(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    if manifest["schema"] not in {_SCHEMA_V2, _SCHEMA_V3}:
        raise ValueError("Candidate manifest does not contain a complete package-subject inventory.")
    return [
        {"id": record["id"], "version": record["version"], **record[kind]}
        for record in manifest["packages"]
        for kind in _SUBJECT_KINDS
        if kind in record
    ]


def _verify_subject(path: Path, subject: dict[str, Any]) -> None:
    if not path.is_file():
        raise ValueError(f"Missing manifested package subject: {path}")
    if path.stat().st_size != subject["size"] or _sha256(path) != subject["sha256"]:
        raise ValueError(f"Candidate package subject digest mismatch: {path.name}")


def _verify_inventory(packages_directory: Path, manifest: dict[str, Any]) -> None:
    if manifest["schema"] == _SCHEMA_V1:
        expected = {record["file"] for record in manifest["packages"]}
        actual = {path.name for path in packages_directory.glob("*.nupkg")}
        if actual != expected:
            raise ValueError("Historical v1 candidate package inventory differs from the manifest.")
        for record in manifest["packages"]:
            _verify_subject(packages_directory / record["file"], record)
        return

    subjects = _subjects(manifest)
    expected = {subject["file"] for subject in subjects}
    actual = {
        path.name
        for extension in ("*.nupkg", "*.snupkg")
        for path in packages_directory.glob(extension)
    }
    if actual != expected:
        missing = sorted(expected - actual)
        unexpected = sorted(actual - expected)
        raise ValueError(
            f"Candidate package subject inventory differs from the manifest: missing={missing}, unexpected={unexpected}."
        )
    for subject in subjects:
        _verify_subject(packages_directory / subject["file"], subject)


def _create(arguments: argparse.Namespace) -> None:
    package_ids = _package_ids_for_version(arguments.version)
    schema = _SCHEMA_V3 if package_ids == _PACKAGE_IDS_WITH_ANNOTATIONS else _SCHEMA_V2
    records = []
    for package_id in package_ids:
        subjects: dict[str, dict[str, Any]] = {}
        subject_kinds = _SUBJECT_KINDS if package_id != _ANNOTATIONS_PACKAGE_ID else ("package",)
        for kind in subject_kinds:
            path = arguments.packages_dir / _expected_filename(package_id, arguments.version, kind)
            if not path.is_file():
                raise ValueError(f"Missing candidate {kind} package: {path}")
            subjects[kind] = _subject(path, kind)
        records.append({"id": package_id, "version": arguments.version, **subjects})

    manifest = {
        "schema": schema,
        "version": arguments.version,
        "source_commit": arguments.source_commit,
        "packages": records,
    }
    if schema == _SCHEMA_V3:
        _validate_v3(manifest)
    else:
        _validate_v2(manifest)
    _verify_inventory(arguments.packages_dir, manifest)
    output = _safe_path(arguments.output, "candidate manifest output")
    protected_subjects = {
        _safe_path(arguments.packages_dir / subject["file"], "package subject")
        for subject in _subjects(manifest)
    }
    if output in protected_subjects:
        raise ValueError("Candidate manifest output cannot overwrite a package subject.")
    output.parent.mkdir(parents=True, exist_ok=True)
    # The output is confined by _safe_path above before it is written.
    output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _verify(arguments: argparse.Namespace) -> None:
    manifest = _load_manifest(arguments.manifest, arguments.allow_v1)
    if arguments.version is not None and manifest["version"] != arguments.version:
        raise ValueError("Candidate manifest version does not match the expected version.")
    if arguments.source_commit is not None and manifest["source_commit"] != arguments.source_commit:
        raise ValueError("Candidate manifest source commit does not match the expected source commit.")
    _verify_inventory(arguments.packages_dir, manifest)


def _checksum_text(manifest: dict[str, Any]) -> str:
    lines = [
        "# ArchLinterNet pre-publication package checksums",
        f"# manifest-schema: {manifest['schema']}",
        f"# version: {manifest['version']}",
        f"# source-commit: {manifest['source_commit']}",
        "",
    ]
    lines.extend(f"{subject['sha256']}  {subject['file']}" for subject in _subjects(manifest))
    return "\n".join(lines) + "\n"


def _canonical_evidence_paths(
    packages_directory: Path,
    manifest_path: Path,
    checksums_path: Path,
) -> tuple[Path, Path, Path]:
    manifest_path = _safe_path(manifest_path, _CANDIDATE_MANIFEST_LABEL)
    checksums_path = _safe_path(checksums_path, _CANDIDATE_CHECKSUM_EVIDENCE_LABEL)
    packages_directory = _safe_path(packages_directory, "candidate packages directory")
    if manifest_path.parent != packages_directory or manifest_path.name != _CANONICAL_MANIFEST_FILE:
        raise ValueError("Canonical candidate manifest path is invalid.")
    if checksums_path.parent != packages_directory or checksums_path.name != _CANONICAL_CHECKSUM_FILE:
        raise ValueError("Canonical candidate checksum evidence path is invalid.")
    return packages_directory, manifest_path, checksums_path


def _verify_release_evidence(
    packages_directory: Path,
    manifest_path: Path,
    checksums_path: Path,
    expected_version: str | None = None,
    expected_source_commit: str | None = None,
) -> dict[str, Any]:
    packages_directory, manifest_path, checksums_path = _canonical_evidence_paths(
        packages_directory,
        manifest_path,
        checksums_path,
    )

    manifest = _load_manifest(manifest_path)
    if expected_version is not None and manifest["version"] != expected_version:
        raise ValueError("Candidate manifest version does not match the expected version.")
    if expected_source_commit is not None and manifest["source_commit"] != expected_source_commit:
        raise ValueError("Candidate manifest source commit does not match the expected source commit.")
    _verify_inventory(packages_directory, manifest)
    checksums_path = _safe_path(checksums_path, _CANDIDATE_CHECKSUM_EVIDENCE_LABEL)
    # The checksum evidence path is confined by _safe_path above before it is read.
    if not checksums_path.is_file() or checksums_path.read_text(encoding="utf-8") != _checksum_text(manifest):  # NOSONAR(S2083,S8707)
        raise ValueError("Canonical candidate checksum evidence differs from the manifest rendering.")

    expected = {subject["file"] for subject in _subjects(manifest)} | {
        _CANONICAL_MANIFEST_FILE,
        _CANONICAL_CHECKSUM_FILE,
    }
    actual = {path.name for path in packages_directory.iterdir() if path.is_file()}
    if actual != expected:
        missing = sorted(expected - actual)
        unexpected = sorted(actual - expected)
        raise ValueError(
            "Candidate release evidence inventory differs from the canonical set: "
            f"missing={missing}, unexpected={unexpected}."
        )
    return manifest


def _render_checksums(arguments: argparse.Namespace) -> None:
    manifest = _load_manifest(arguments.manifest)
    output = _safe_path(arguments.output, "candidate checksum output")
    if output == _safe_path(arguments.manifest, _CANDIDATE_MANIFEST_LABEL):
        raise ValueError("Candidate checksum output cannot overwrite its manifest.")
    output.parent.mkdir(parents=True, exist_ok=True)
    # The output is confined by _safe_path above before it is written.
    output.write_text(_checksum_text(manifest), encoding="utf-8")  # NOSONAR(S2083)


def _render_attestation_subject_checksums(arguments: argparse.Namespace) -> None:
    manifest = _verify_release_evidence(arguments.packages_dir, arguments.manifest, arguments.checksums)
    if arguments.subject_class == "package":
        subjects = _subjects(manifest)
        lines = [f"{subject['sha256']}  {subject['file']}" for subject in subjects]
    else:
        evidence_subjects = (arguments.manifest, arguments.checksums)
        lines = [f"{_sha256(path)}  {path.name}" for path in evidence_subjects]
    output = _safe_path(arguments.output, "attestation subject checksum output")
    protected_paths = {
        _safe_path(arguments.manifest, _CANDIDATE_MANIFEST_LABEL),
        _safe_path(arguments.checksums, _CANDIDATE_CHECKSUM_EVIDENCE_LABEL),
        *(_safe_path(arguments.packages_dir / subject["file"], "package subject") for subject in _subjects(manifest)),
    }
    if output in protected_paths:
        raise ValueError("Attestation output cannot overwrite candidate evidence or a package subject.")
    output.parent.mkdir(parents=True, exist_ok=True)
    # The output is confined by _safe_path above before it is written.
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _verify_release_evidence_command(arguments: argparse.Namespace) -> None:
    _verify_release_evidence(
        arguments.packages_dir,
        arguments.manifest,
        arguments.checksums,
        arguments.version,
        arguments.source_commit,
    )


def _paths(arguments: argparse.Namespace) -> None:
    manifest = _load_manifest(arguments.manifest)
    _verify_inventory(arguments.packages_dir, manifest)
    for subject in _subjects(manifest):
        if arguments.kind == "all" or subject["kind"] == arguments.kind:
            print(subject["file"])


def _package_ids(arguments: argparse.Namespace) -> None:
    from calculate_version import parse_package_version

    if parse_package_version(arguments.version) is None:
        raise ValueError("Candidate package version is invalid.")
    for package_id in _package_ids_for_version(arguments.version):
        print(package_id)


def main() -> int:
    parser = argparse.ArgumentParser()
    subcommands = parser.add_subparsers(dest="command", required=True)

    create = subcommands.add_parser("create")
    create.add_argument("--packages-dir", type=Path, required=True)
    create.add_argument("--version", required=True)
    create.add_argument("--source-commit", required=True)
    create.add_argument("--output", type=Path, required=True)
    create.set_defaults(handler=_create)

    verify = subcommands.add_parser("verify")
    verify.add_argument("--packages-dir", type=Path, required=True)
    verify.add_argument("--manifest", type=Path, required=True)
    verify.add_argument("--version")
    verify.add_argument("--source-commit")
    verify.add_argument("--allow-v1", action="store_true")
    verify.set_defaults(handler=_verify)

    render = subcommands.add_parser("render-checksums")
    render.add_argument("--manifest", type=Path, required=True)
    render.add_argument("--output", type=Path, required=True)
    render.set_defaults(handler=_render_checksums)

    verify_evidence = subcommands.add_parser("verify-release-evidence")
    verify_evidence.add_argument("--packages-dir", type=Path, required=True)
    verify_evidence.add_argument("--manifest", type=Path, required=True)
    verify_evidence.add_argument("--checksums", type=Path, required=True)
    verify_evidence.add_argument("--version")
    verify_evidence.add_argument("--source-commit")
    verify_evidence.set_defaults(handler=_verify_release_evidence_command)

    attest_subjects = subcommands.add_parser("render-attestation-subject-checksums")
    attest_subjects.add_argument("--packages-dir", type=Path, required=True)
    attest_subjects.add_argument("--manifest", type=Path, required=True)
    attest_subjects.add_argument("--checksums", type=Path, required=True)
    attest_subjects.add_argument("--subject-class", choices=("package", "evidence"), required=True)
    attest_subjects.add_argument("--output", type=Path, required=True)
    attest_subjects.set_defaults(handler=_render_attestation_subject_checksums)

    paths = subcommands.add_parser("paths")
    paths.add_argument("--packages-dir", type=Path, required=True)
    paths.add_argument("--manifest", type=Path, required=True)
    paths.add_argument("--kind", choices=(*_SUBJECT_KINDS, "all"), required=True)
    paths.set_defaults(handler=_paths)

    package_ids = subcommands.add_parser("package-ids")
    package_ids.add_argument("--version", required=True)
    package_ids.set_defaults(handler=_package_ids)

    arguments = parser.parse_args()
    arguments.handler(arguments)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
