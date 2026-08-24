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
_SCHEMA_V1 = "checkpoint-b-candidate-manifest/v1"
_SCHEMA_V2 = "checkpoint-b-candidate-manifest/v2"
_SUBJECT_KINDS = ("package", "symbols")
_CANONICAL_MANIFEST_FILE = "package-manifest.json"
_CANONICAL_CHECKSUM_FILE = "package-checksums.txt"
_SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
_SOURCE_COMMIT_PATTERN = re.compile(r"[0-9a-f]{40,64}")
_UNSUPPORTED_SCHEMA = "Unsupported candidate manifest schema."


def _sha256(path: Path) -> str:
    path = _safe_path(path, "release subject")
    digest = hashlib.sha256()
    # The subject is confined by _safe_path above before it is read.
    with path.open("rb") as source:  # NOSONAR
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _expected_filename(package_id: str, version: str, kind: str) -> str:
    extension = ".nupkg" if kind == "package" else ".snupkg"
    return f"{package_id}.{version}{extension}"


def _subject(path: Path, kind: str) -> dict[str, Any]:
    return {
        "kind": kind,
        "file": path.name,
        "size": path.stat().st_size,
        "sha256": _sha256(path),
    }


def _read_manifest(path: Path) -> dict[str, Any]:
    path = _safe_path(path, "candidate manifest")
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
    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != len(_PACKAGE_IDS):
        raise ValueError("Candidate manifest package inventory is invalid.")

    subject_files: set[str] = set()
    for package_id, record in zip(_PACKAGE_IDS, packages, strict=True):
        _validate_package_record(record, package_id, version, subject_files)
    return manifest


def _validate_package_record(record: Any, package_id: str, version: str, subject_files: set[str]) -> None:
    if not isinstance(record, dict) or set(record) != {"id", "version", "package", "symbols"}:
        raise ValueError("Candidate manifest package record is invalid.")
    if record.get("id") != package_id or record.get("version") != version:
        raise ValueError("Candidate manifest package identity is invalid.")
    for kind in _SUBJECT_KINDS:
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
        raise ValueError("Candidate manifest package inventory is invalid.")
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
    if allow_v1 and manifest.get("schema") == _SCHEMA_V1:
        return _validate_v1(manifest)
    raise ValueError(_UNSUPPORTED_SCHEMA)


def _subjects(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    if manifest["schema"] != _SCHEMA_V2:
        raise ValueError("Candidate manifest does not contain a complete package-subject inventory.")
    return [
        {"id": record["id"], "version": record["version"], **record[kind]}
        for record in manifest["packages"]
        for kind in _SUBJECT_KINDS
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
    records = []
    for package_id in _PACKAGE_IDS:
        subjects: dict[str, dict[str, Any]] = {}
        for kind in _SUBJECT_KINDS:
            path = arguments.packages_dir / _expected_filename(package_id, arguments.version, kind)
            if not path.is_file():
                raise ValueError(f"Missing candidate {kind} package: {path}")
            subjects[kind] = _subject(path, kind)
        records.append({"id": package_id, "version": arguments.version, **subjects})

    manifest = {
        "schema": _SCHEMA_V2,
        "version": arguments.version,
        "source_commit": arguments.source_commit,
        "packages": records,
    }
    _validate_v2(manifest)
    _verify_inventory(arguments.packages_dir, manifest)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


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
    manifest_path = _safe_path(manifest_path, "candidate manifest")
    checksums_path = _safe_path(checksums_path, "candidate checksum evidence")
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
    checksums_path = _safe_path(checksums_path, "candidate checksum evidence")
    # The checksum evidence path is confined by _safe_path above before it is read.
    if not checksums_path.is_file() or checksums_path.read_text(encoding="utf-8") != _checksum_text(manifest):  # NOSONAR
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
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(_checksum_text(manifest), encoding="utf-8")


def _render_attestation_subject_checksums(arguments: argparse.Namespace) -> None:
    manifest = _verify_release_evidence(arguments.packages_dir, arguments.manifest, arguments.checksums)
    if arguments.subject_class == "package":
        subjects = _subjects(manifest)
        lines = [f"{subject['sha256']}  {subject['file']}" for subject in subjects]
    else:
        evidence_subjects = (arguments.manifest, arguments.checksums)
        lines = [f"{_sha256(path)}  {path.name}" for path in evidence_subjects]
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text("\n".join(lines) + "\n", encoding="utf-8")


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

    arguments = parser.parse_args()
    arguments.handler(arguments)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
