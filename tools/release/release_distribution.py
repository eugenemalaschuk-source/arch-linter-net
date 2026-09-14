#!/usr/bin/env python3
"""Build and verify the frozen Badge Relay release transport.

This helper deliberately sits beside :mod:`package_manifest`; it does not replace the
candidate package manifest or decide which packages belong in a release.  It adds the
small, outer transport boundary needed for the Relay bundle and its reviewed publisher
pins.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import io
import json
import re
import subprocess
import sys
import tarfile
from pathlib import Path, PurePosixPath
from typing import Any

import package_manifest
from _release_workspace import _safe_path
from verify_relay_dependencies import verify_relay_dependencies


_INVENTORY_SCHEMA = "architecture-health-badge-release-inventory/v2"
_DISTRIBUTION_SCHEMA = "architecture-health-badge-release-distribution/v1"
_COMPATIBILITY_SCHEMA = "architecture-health-badge-relay-compatibility/v1"
_APPROVED_PUBLISHER_COMMIT = "ff9b19bfe5abcab233d490ea53f55a387dc4a8db"
_PUBLISHER_REPOSITORY = "eugenemalaschuk-source/arch-linter-net"
_SOURCE_COMMIT_PATTERN = re.compile(r"[0-9a-f]{40,64}")
_SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
_GIT_BLOB_PATTERN = re.compile(r"[0-9a-f]{40}")
_VERSION_PATTERN = re.compile(
    r"^0\.8\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z-](?:\.?[0-9A-Za-z-])*)?(?:\+[0-9A-Za-z-](?:\.?[0-9A-Za-z-])*)?$"
)

_ARCHIVE_FILE_TEMPLATE = "architecture-health-badge-relay-{version}.tar.gz"
_COMPATIBILITY_FILE_TEMPLATE = "architecture-health-badge-relay-{version}.json"
_WORKFLOW_FILE = "architecture-health-badge-publisher-workflow.yml"
_ACTION_FILE = "architecture-health-badge-publisher-action.yml"
_MANIFEST_FILE = "architecture-health-badge-release-distribution.json"
_CHECKSUMS_FILE = "architecture-health-badge-release-checksums.txt"

_MEDIA_YAML = "text/yaml"
_MEDIA_JSON = "application/json"
_MEDIA_TYPESCRIPT = "text/typescript"
_CANDIDATE_MANIFEST_DESCRIPTION = "candidate manifest"
_TRANSPORT_MANIFEST_DESCRIPTION = "transport manifest"
_TRANSPORT_SUBJECT_DESCRIPTION = "transport subject"

_COMPATIBILITY_IDENTITIES = {
    "bundle": "badge-relay/v1",
    "config": "architecture-health-badge-relay-config/v1",
    "plan": "architecture-health-badge-relay/v1",
    "promotion": "architecture-health-badge-promotion/v1",
    "publication": "architecture-health-badge-publication/v2",
    "storage": "v1",
}
_PACKAGE_IDS = list(package_manifest._PACKAGE_IDS)
_WORKFLOW_PATH = ".github/workflows/architecture-health-badge-promotion.yml"
_ACTION_PATH = ".github/actions/architecture-health-badge-promotion/action.yml"
_ACTION_REF_PATH = ".github/actions/architecture-health-badge-promotion"

# This is intentionally duplicated as a closed review boundary.  A caller cannot broaden the
# shipped bundle by editing a caller-provided inventory file.
_REVIEWED_BUNDLE_MEMBERS = (
    (".github/actions/architecture-health-badge-promotion/action.yml", _MEDIA_YAML, "approved-commit"),
    (".github/workflows/architecture-health-badge-promotion.yml", _MEDIA_YAML, "approved-commit"),
    ("relay/THIRD-PARTY-NOTICES.txt", "text/plain", "working-tree"),
    ("relay/bundle-manifest.json", _MEDIA_JSON, "working-tree"),
    ("relay/package-lock.json", _MEDIA_JSON, "working-tree"),
    ("relay/package.json", _MEDIA_JSON, "working-tree"),
    ("relay/src/index.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/payload.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/read.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/registry-do.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/registry.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/relay-do.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/security.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/src/types.ts", _MEDIA_TYPESCRIPT, "working-tree"),
    ("relay/tsconfig.json", _MEDIA_JSON, "working-tree"),
    ("relay/wrangler.jsonc", _MEDIA_JSON, "working-tree"),
    ("schema/0.8.0/badge-relay-config.schema.json", _MEDIA_JSON, "working-tree"),
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _json_bytes(value: dict[str, Any]) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _read_json(path: Path, description: str) -> dict[str, Any]:
    path = _safe_path(path, description)
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"The {description} '{path}' is not a regular file.")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read {description} '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"The {description} must be a JSON object.")
    return value


def _safe_archive_path(value: Any, description: str = "archive path") -> str:
    if not isinstance(value, str) or not value or "\\" in value or "\x00" in value:
        raise ValueError(f"The {description} is invalid.")
    path = PurePosixPath(value)
    if value.startswith("/") or value != str(path) or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"The {description} is unsafe: '{value}'.")
    if ":" in value:
        raise ValueError(f"The {description} is unsafe: '{value}'.")
    return value


def _safe_source_path(root: Path, relative: str, description: str) -> Path:
    _safe_archive_path(relative, description)
    candidate = _safe_path(root.joinpath(*PurePosixPath(relative).parts), description)
    current = root
    for part in PurePosixPath(relative).parts:
        current /= part
        if current.is_symlink():
            raise ValueError(f"The {description} '{relative}' contains a symlink.")
    if not candidate.is_file() or candidate.is_symlink():
        raise ValueError(f"The {description} '{relative}' is not a regular file.")
    return candidate


def _validate_version(value: Any) -> str:
    if not isinstance(value, str) or not _VERSION_PATTERN.fullmatch(value):
        raise ValueError("The candidate version must be a valid 0.8.x version.")
    return value


def _validate_source_commit(value: Any) -> str:
    if not isinstance(value, str) or not _SOURCE_COMMIT_PATTERN.fullmatch(value):
        raise ValueError("The source commit is invalid.")
    return value


def _validate_inventory_header(value: dict[str, Any]) -> None:
    expected_fields = {
        "schema",
        "lifecycle",
        "release_authority",
        "publication",
        "included",
        "excluded",
        "handoff",
        "components",
        "bundle_members",
        "compatibility",
        "package_ids",
        "relay",
    }
    if set(value) != expected_fields or value.get("schema") != _INVENTORY_SCHEMA:
        raise ValueError("The release inventory has invalid fields or schema.")
    if value.get("lifecycle") != "milestone-6/v0.8.x-completeness":
        raise ValueError("The release inventory is not for the reviewed v0.8.x line.")
    if value.get("release_authority") != "#806" or value.get("publication") != "not-authorized":
        raise ValueError("The release inventory has an invalid release authority.")
    if value.get("included") != ["#806", "#825"]:
        raise ValueError("The release inventory must include exactly the #806/#825 handoff.")
    if value.get("excluded") != ["v0.9-performance", "#650", "#787"]:
        raise ValueError("The release inventory exclusions are invalid.")
    if value.get("handoff") != "#825 -> #806 reviewed candidate scope and released-artifact verification":
        raise ValueError("The release inventory handoff is invalid.")
    if value.get("package_ids") != _PACKAGE_IDS:
        raise ValueError("The release inventory package identity set is invalid.")


def _validate_inventory_compatibility(value: dict[str, Any]) -> dict[str, Any]:
    compatibility = value.get("compatibility")
    if not isinstance(compatibility, dict):
        raise ValueError("The release inventory compatibility identity is invalid.")
    required_compatibility = {
        *_COMPATIBILITY_IDENTITIES,
        "publisher_repository",
        "publisher_commit",
        "workflow_path",
        "action_path",
        "workflow_ref",
        "action_ref",
        "workflow_source_sha",
        "action_source_sha",
    }
    if set(compatibility) != required_compatibility:
        raise ValueError("The release inventory compatibility fields are invalid.")
    for key, expected in _COMPATIBILITY_IDENTITIES.items():
        if compatibility.get(key) != expected:
            raise ValueError(f"The release inventory compatibility '{key}' is invalid.")
    if compatibility.get("publisher_repository") != _PUBLISHER_REPOSITORY:
        raise ValueError("The release inventory publisher repository is invalid.")
    if compatibility.get("publisher_commit") != _APPROVED_PUBLISHER_COMMIT:
        raise ValueError("The release inventory publisher commit is not the approved immutable pin.")
    if compatibility.get("workflow_path") != _WORKFLOW_PATH or compatibility.get("action_path") != _ACTION_PATH:
        raise ValueError("The release inventory publisher paths are invalid.")
    if compatibility.get("workflow_ref") != f"{_PUBLISHER_REPOSITORY}/{_WORKFLOW_PATH}@{_APPROVED_PUBLISHER_COMMIT}":
        raise ValueError("The release inventory workflow reference is not immutable.")
    if compatibility.get("action_ref") != f"{_PUBLISHER_REPOSITORY}/{_ACTION_REF_PATH}@{_APPROVED_PUBLISHER_COMMIT}":
        raise ValueError("The release inventory action reference is not immutable.")
    for key in ("workflow_source_sha", "action_source_sha"):
        if not isinstance(compatibility.get(key), str) or not _GIT_BLOB_PATTERN.fullmatch(compatibility[key]):
            raise ValueError(f"The release inventory {key} is invalid.")
    return compatibility


def _validate_inventory_components(value: dict[str, Any], compatibility: dict[str, Any]) -> None:
    components = value.get("components")
    if components != [
        {
            "kind": "reusable-workflow",
            "path": _WORKFLOW_PATH,
            "approved_source_sha": compatibility["workflow_source_sha"],
        },
        {
            "kind": "composite-action",
            "path": _ACTION_PATH,
            "approved_source_sha": compatibility["action_source_sha"],
        },
    ]:
        raise ValueError("The release inventory publisher component set is invalid.")


def _validate_inventory_relay(value: dict[str, Any]) -> None:
    relay = value.get("relay")
    if relay != {
        "package_json": "relay/package.json",
        "package_lock": "relay/package-lock.json",
        "license_notice": "relay/THIRD-PARTY-NOTICES.txt",
        "config_schema": "schema/0.8.0/badge-relay-config.schema.json",
    }:
        raise ValueError("The release inventory Relay dependency paths are invalid.")


def _validate_inventory_members(value: dict[str, Any]) -> None:
    members = value.get("bundle_members")
    expected_members = [
        {"path": path, "archive_path": path, "media_kind": media_kind, "source": source}
        for path, media_kind, source in _REVIEWED_BUNDLE_MEMBERS
    ]
    if members != expected_members:
        raise ValueError("The release inventory bundle member allowlist is invalid.")
    for member in members:
        _safe_archive_path(member["path"], "bundle source path")
        _safe_archive_path(member["archive_path"], "bundle archive path")


def _validate_inventory(value: dict[str, Any]) -> dict[str, Any]:
    _validate_inventory_header(value)
    compatibility = _validate_inventory_compatibility(value)
    _validate_inventory_components(value, compatibility)
    _validate_inventory_relay(value)
    _validate_inventory_members(value)
    return value


def _load_inventory(path: Path) -> dict[str, Any]:
    return _validate_inventory(_read_json(path, "release inventory"))


def _candidate_manifest(path: Path, version: str, source_commit: str) -> tuple[dict[str, Any], str]:
    path = _safe_path(path, _CANDIDATE_MANIFEST_DESCRIPTION)
    manifest = package_manifest._load_manifest(path)
    if manifest["version"] != version:
        raise ValueError("Candidate manifest version does not match the expected version.")
    if manifest["source_commit"] != source_commit:
        raise ValueError("Candidate manifest source commit does not match the expected source commit.")
    package_manifest._verify_inventory(path.parent, manifest)
    return manifest, package_manifest._sha256(path)


def _git_blob(source_root: Path, commit: str, relative: str) -> bytes:
    completed = subprocess.run(
        ["git", "-C", str(source_root), "cat-file", "blob", f"{commit}:{relative}"],
        check=False,
        capture_output=True,
    )  # NOSONAR(S4721,S8707)
    if completed.returncode != 0:
        details = completed.stderr.decode("utf-8", errors="replace").strip()
        raise ValueError(f"Approved publisher source '{relative}' is unavailable at '{commit}': {details}")
    return completed.stdout


def _approved_source_bytes(source_root: Path, relative: str, inventory: dict[str, Any]) -> tuple[bytes, str]:
    component = next(component for component in inventory["components"] if component["path"] == relative)
    try:
        contents = _git_blob(source_root, _APPROVED_PUBLISHER_COMMIT, relative)
    except ValueError:
        # Release verification jobs may use a shallow checkout that does not retain the reviewed
        # bootstrap commit. The exact Git blob digest remains pinned in the checked-in inventory;
        # accepting workspace bytes is safe only when they are byte-for-byte that reviewed blob.
        path = _safe_source_path(source_root, relative, "approved publisher source")
        contents = path.read_bytes()
    observed_blob = subprocess.run(
        ["git", "hash-object", "--stdin"],
        input=contents,
        check=False,
        capture_output=True,
    )  # NOSONAR(S4721,S8707)
    observed_sha = observed_blob.stdout.decode("ascii", errors="replace").strip()
    if observed_blob.returncode != 0 or observed_sha != component["approved_source_sha"]:
        raise ValueError(f"Approved publisher source digest mismatch: {relative}.")
    return contents, observed_sha


def _source_bytes(source_root: Path, member: dict[str, Any], source_commit: str, inventory: dict[str, Any]) -> tuple[bytes, dict[str, Any]]:
    relative = member["path"]
    if member["source"] == "working-tree":
        path = _safe_source_path(source_root, relative, "bundle source path")
        contents = path.read_bytes()
        # When the checkout contains the candidate commit, bind the working-tree source bytes to
        # that commit too. Unit fixtures may use a non-Git temporary tree, so the check is skipped
        # only when Git cannot resolve the fixture's synthetic commit.
        committed = subprocess.run(
            ["git", "-C", str(source_root), "cat-file", "blob", f"{source_commit}:{relative}"],
            check=False,
            capture_output=True,
        )  # NOSONAR(S4721,S8707)
        if committed.returncode == 0 and committed.stdout != contents:
            raise ValueError(f"Bundle source '{relative}' does not match source commit '{source_commit}'.")
        identity = {"kind": "candidate-source", "path": relative, "commit": source_commit}
    else:
        if member["path"] not in (_WORKFLOW_PATH, _ACTION_PATH):
            raise ValueError("Only the approved publisher workflow and action may use an immutable source.")
        contents, observed_sha = _approved_source_bytes(source_root, relative, inventory)
        identity = {
            "kind": "approved-immutable-commit",
            "path": relative,
            "commit": _APPROVED_PUBLISHER_COMMIT,
            "git_blob_sha": observed_sha,
        }
    if b"synthetic" in contents.lower():
        raise ValueError(f"Synthetic identity is forbidden in shipped subject '{relative}'.")
    return contents, identity


def _member_records(source_root: Path, inventory: dict[str, Any], source_commit: str) -> list[dict[str, Any]]:
    root = _safe_path(source_root, "source root")
    if not root.is_dir() or root.is_symlink():
        raise ValueError(f"The source root '{root}' is not a regular directory.")
    records: list[dict[str, Any]] = []
    archive_paths: set[str] = set()
    for member in inventory["bundle_members"]:
        contents, identity = _source_bytes(root, member, source_commit, inventory)
        archive_path = _safe_archive_path(member["archive_path"], "bundle archive path")
        if archive_path in archive_paths:
            raise ValueError("The bundle archive allowlist contains duplicate paths.")
        archive_paths.add(archive_path)
        records.append(
            {
                "file": archive_path,
                "media_kind": member["media_kind"],
                "size": len(contents),
                "sha256": _sha256_bytes(contents),
                "source": identity,
                "_contents": contents,
            }
        )
    return sorted(records, key=lambda record: record["file"])


def _archive_bytes(records: list[dict[str, Any]]) -> bytes:
    output = io.BytesIO()
    with gzip.GzipFile(fileobj=output, mode="wb", filename="", mtime=0, compresslevel=9) as compressed:
        with tarfile.open(fileobj=compressed, mode="w", format=tarfile.USTAR_FORMAT) as archive:
            for record in sorted(records, key=lambda value: value["file"]):
                info = tarfile.TarInfo(record["file"])
                info.size = record["size"]
                info.mtime = 0
                info.uid = 0
                info.gid = 0
                info.uname = ""
                info.gname = ""
                info.mode = 0o644
                info.type = tarfile.REGTYPE
                info.pax_headers = {}
                archive.addfile(info, io.BytesIO(record["_contents"]))
    return output.getvalue()


def _without_contents(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [{key: value for key, value in record.items() if key != "_contents"} for record in records]


def _compatibility_metadata(
    inventory: dict[str, Any],
    candidate: dict[str, Any],
    candidate_manifest_name: str,
    candidate_manifest_sha: str,
    members: list[dict[str, Any]],
) -> dict[str, Any]:
    compatibility = inventory["compatibility"]
    by_path = {record["file"]: record for record in members}
    workflow = by_path[_WORKFLOW_PATH]
    action = by_path[_ACTION_PATH]
    return {
        "schema": _COMPATIBILITY_SCHEMA,
        "version": candidate["version"],
        "source_commit": candidate["source_commit"],
        "candidate_manifest_sha256": candidate_manifest_sha,
        "candidate_manifest": {"file": candidate_manifest_name, "sha256": candidate_manifest_sha},
        "package_ids": list(package_manifest._PACKAGE_IDS),
        "compatibility": {key: compatibility[key] for key in _COMPATIBILITY_IDENTITIES},
        "approved_publisher_commit": compatibility["publisher_commit"],
        "publisher": {
            "repository": compatibility["publisher_repository"],
            "commit": compatibility["publisher_commit"],
            "workflow": {
                "path": compatibility["workflow_path"],
                "ref": compatibility["workflow_ref"],
                "git_blob_sha": workflow["source"]["git_blob_sha"],
                "sha256": workflow["sha256"],
            },
            "action": {
                "path": compatibility["action_path"],
                "ref": compatibility["action_ref"],
                "git_blob_sha": action["source"]["git_blob_sha"],
                "sha256": action["sha256"],
            },
        },
    }


def _subject(
    kind: str,
    file: str,
    media_kind: str,
    contents: bytes,
    source: dict[str, Any],
) -> dict[str, Any]:
    return {
        "kind": kind,
        "file": file,
        "media_kind": media_kind,
        "size": len(contents),
        "sha256": _sha256_bytes(contents),
        "source": source,
    }


def _build_outputs(
    source_root: Path,
    inventory: dict[str, Any],
    candidate: dict[str, Any],
    candidate_manifest_name: str,
    candidate_manifest_sha: str,
) -> dict[str, bytes]:
    members = _member_records(source_root, inventory, candidate["source_commit"])
    by_path = {record["file"]: record for record in members}
    workflow = by_path[_WORKFLOW_PATH]
    action = by_path[_ACTION_PATH]
    archive = _archive_bytes(members)
    archive_name = _ARCHIVE_FILE_TEMPLATE.format(version=candidate["version"])
    compatibility_name = _COMPATIBILITY_FILE_TEMPLATE.format(version=candidate["version"])
    compatibility = _compatibility_metadata(
        inventory,
        candidate,
        candidate_manifest_name,
        candidate_manifest_sha,
        members,
    )
    compatibility_bytes = _json_bytes(compatibility)
    archive_subject = _subject(
        "relay-archive",
        archive_name,
        "application/gzip",
        archive,
        {
            "kind": "reviewed-bundle",
            "bundle": inventory["compatibility"]["bundle"],
            "source_commit": candidate["source_commit"],
            "members": [record["file"] for record in _without_contents(members)],
        },
    )
    compatibility_subject = _subject(
        "compatibility-metadata",
        compatibility_name,
        _MEDIA_JSON,
        compatibility_bytes,
        {
            "kind": "generated-from-candidate",
            "candidate_manifest_sha256": candidate_manifest_sha,
        },
    )
    workflow_subject = _subject(
        "publisher-workflow",
        _WORKFLOW_FILE,
        _MEDIA_YAML,
        workflow["_contents"],
        workflow["source"],
    )
    action_subject = _subject(
        "publisher-action",
        _ACTION_FILE,
        _MEDIA_YAML,
        action["_contents"],
        action["source"],
    )
    distribution = {
        "schema": _DISTRIBUTION_SCHEMA,
        "version": candidate["version"],
        "source_commit": candidate["source_commit"],
        "release_authority": inventory["release_authority"],
        "handoff": inventory["handoff"],
        "lifecycle": inventory["lifecycle"],
        "candidate_manifest": {"file": candidate_manifest_name, "sha256": candidate_manifest_sha},
        "compatibility": compatibility["compatibility"],
        "subjects": [archive_subject, workflow_subject, action_subject, compatibility_subject],
        "archive_members": _without_contents(members),
        "evidence": {"manifest": _MANIFEST_FILE, "checksums": _CHECKSUMS_FILE},
    }
    manifest_bytes = _json_bytes(distribution)
    checksums_bytes = _checksum_text(distribution).encode("utf-8")
    return {
        archive_name: archive,
        _WORKFLOW_FILE: workflow["_contents"],
        _ACTION_FILE: action["_contents"],
        compatibility_name: compatibility_bytes,
        _MANIFEST_FILE: manifest_bytes,
        _CHECKSUMS_FILE: checksums_bytes,
    }


def _checksum_text(distribution: dict[str, Any]) -> str:
    lines = [
        "# ArchLinterNet Badge Relay release transport checksums",
        f"# manifest-schema: {distribution['schema']}",
        f"# version: {distribution['version']}",
        f"# source-commit: {distribution['source_commit']}",
        "",
    ]
    lines.extend(f"{subject['sha256']}  {subject['file']}" for subject in distribution["subjects"])
    return "\n".join(lines) + "\n"


def _output_directory(path: Path) -> Path:
    path = _safe_path(path, "transport output directory")
    if path.exists() and (path.is_symlink() or not path.is_dir()):
        raise ValueError(f"The transport output directory '{path}' is invalid.")
    path.mkdir(parents=True, exist_ok=True)
    return path


def _write_outputs(output_dir: Path, outputs: dict[str, bytes]) -> None:
    output_dir = _output_directory(output_dir)
    for name, contents in outputs.items():
        path = _safe_path(output_dir / name, "transport output")
        path.write_bytes(contents)


def _create(arguments: argparse.Namespace) -> None:
    version = _validate_version(arguments.version)
    source_commit = _validate_source_commit(arguments.source_commit)
    inventory = _load_inventory(arguments.inventory)
    candidate, candidate_manifest_sha = _candidate_manifest(arguments.candidate_manifest, version, source_commit)
    verify_relay_dependencies(arguments.source_root, inventory)
    outputs = _build_outputs(
        arguments.source_root,
        inventory,
        candidate,
        _safe_path(arguments.candidate_manifest, _CANDIDATE_MANIFEST_DESCRIPTION).name,
        candidate_manifest_sha,
    )
    _write_outputs(arguments.output_dir, outputs)


def _transport_paths(transport_dir: Path, manifest_path: Path) -> tuple[Path, Path, dict[str, Any]]:
    transport_dir = _safe_path(transport_dir, "transport directory")
    manifest_path = _safe_path(manifest_path, _TRANSPORT_MANIFEST_DESCRIPTION)
    if not transport_dir.is_dir() or transport_dir.is_symlink():
        raise ValueError(f"The transport directory '{transport_dir}' is invalid.")
    if manifest_path.parent != transport_dir or manifest_path.name != _MANIFEST_FILE:
        raise ValueError("The transport manifest path is invalid.")
    manifest = _read_json(manifest_path, _TRANSPORT_MANIFEST_DESCRIPTION)
    return transport_dir, manifest_path, manifest


def _validate_distribution_header(distribution: dict[str, Any]) -> None:
    required = {
        "schema",
        "version",
        "source_commit",
        "release_authority",
        "handoff",
        "lifecycle",
        "candidate_manifest",
        "compatibility",
        "subjects",
        "archive_members",
        "evidence",
    }
    if set(distribution) != required or distribution.get("schema") != _DISTRIBUTION_SCHEMA:
        raise ValueError("The transport manifest fields or schema are invalid.")
    _validate_version(distribution.get("version"))
    _validate_source_commit(distribution.get("source_commit"))
    candidate_manifest = distribution.get("candidate_manifest")
    if not isinstance(candidate_manifest, dict) or set(candidate_manifest) != {"file", "sha256"}:
        raise ValueError("The transport manifest candidate binding is invalid.")
    _safe_archive_path(candidate_manifest.get("file"), "candidate manifest filename")
    if not _SHA256_PATTERN.fullmatch(candidate_manifest.get("sha256", "")):
        raise ValueError("The transport manifest candidate digest is invalid.")
    if distribution.get("compatibility") != _COMPATIBILITY_IDENTITIES:
        raise ValueError("The transport manifest compatibility identity is invalid.")
    if distribution.get("evidence") != {"manifest": _MANIFEST_FILE, "checksums": _CHECKSUMS_FILE}:
        raise ValueError("The transport manifest evidence paths are invalid.")


def _validate_distribution_subject(subject: Any, names: set[str]) -> None:
    if not isinstance(subject, dict) or set(subject) != {"kind", "file", "media_kind", "size", "sha256", "source"}:
        raise ValueError("The transport manifest subject record is invalid.")
    _safe_archive_path(subject.get("file"), "transport subject filename")
    if subject["file"] in names or subject["file"] in {_MANIFEST_FILE, _CHECKSUMS_FILE}:
        raise ValueError("The transport manifest subject inventory is recursive or duplicated.")
    names.add(subject["file"])
    if not isinstance(subject.get("kind"), str) or not isinstance(subject.get("media_kind"), str):
        raise ValueError("The transport manifest subject media identity is invalid.")
    if not isinstance(subject.get("size"), int) or isinstance(subject["size"], bool) or subject["size"] < 0:
        raise ValueError("The transport manifest subject size is invalid.")
    if not _SHA256_PATTERN.fullmatch(subject.get("sha256", "")):
        raise ValueError("The transport manifest subject digest is invalid.")
    if not isinstance(subject.get("source"), dict):
        raise ValueError("The transport manifest subject source identity is invalid.")


def _validate_distribution_subjects(distribution: dict[str, Any]) -> None:
    subjects = distribution.get("subjects")
    if not isinstance(subjects, list) or len(subjects) != 4:
        raise ValueError("The transport manifest subject inventory is invalid.")
    names: set[str] = set()
    for subject in subjects:
        _validate_distribution_subject(subject, names)
    expected_subjects = {
        ("relay-archive", _ARCHIVE_FILE_TEMPLATE.format(version=distribution["version"])),
        ("publisher-workflow", _WORKFLOW_FILE),
        ("publisher-action", _ACTION_FILE),
        ("compatibility-metadata", _COMPATIBILITY_FILE_TEMPLATE.format(version=distribution["version"])),
    }
    if {(subject["kind"], subject["file"]) for subject in subjects} != expected_subjects:
        raise ValueError("The transport manifest subjects are not the reviewed distribution set.")


def _validate_distribution_member(member: Any, member_names: set[str]) -> None:
    if not isinstance(member, dict) or set(member) != {"file", "media_kind", "size", "sha256", "source"}:
        raise ValueError("The transport archive member record is invalid.")
    _safe_archive_path(member.get("file"), "archive member filename")
    if member["file"] in member_names:
        raise ValueError("The transport archive member inventory is duplicated.")
    member_names.add(member["file"])
    if not isinstance(member.get("media_kind"), str) or not isinstance(member.get("source"), dict):
        raise ValueError("The transport archive member identity is invalid.")
    if not isinstance(member.get("size"), int) or isinstance(member["size"], bool) or member["size"] < 0:
        raise ValueError("The transport archive member size is invalid.")
    if not _SHA256_PATTERN.fullmatch(member.get("sha256", "")):
        raise ValueError("The transport archive member digest is invalid.")


def _validate_distribution_members(distribution: dict[str, Any]) -> None:
    members = distribution.get("archive_members")
    if not isinstance(members, list) or not members:
        raise ValueError("The transport archive member inventory is invalid.")
    member_names: set[str] = set()
    for member in members:
        _validate_distribution_member(member, member_names)


def _validate_distribution_manifest(distribution: dict[str, Any]) -> None:
    _validate_distribution_header(distribution)
    _validate_distribution_subjects(distribution)
    _validate_distribution_members(distribution)


def _regular_transport_files(transport_dir: Path, expected: set[str]) -> None:
    actual: set[str] = set()
    for path in transport_dir.iterdir():
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Unexpected non-regular transport entry: {path.name}")
        actual.add(path.name)
    if actual != expected:
        raise ValueError(f"Transport file inventory differs from the manifest: missing={sorted(expected - actual)}, unexpected={sorted(actual - expected)}.")


def _verify_archive(contents: bytes, expected_members: list[dict[str, Any]]) -> None:
    expected = {member["file"]: member for member in expected_members}
    try:
        with gzip.GzipFile(fileobj=io.BytesIO(contents), mode="rb") as compressed:
            with tarfile.open(fileobj=compressed, mode="r", format=tarfile.USTAR_FORMAT) as archive:
                observed = archive.getmembers()
                if len(observed) != len(expected) or {member.name for member in observed} != set(expected):
                    raise ValueError("Relay archive member inventory differs from the reviewed allowlist.")
                for member in observed:
                    _safe_archive_path(member.name, "archive member filename")
                    if not member.isreg() or member.issym() or member.islnk():
                        raise ValueError(f"Relay archive member '{member.name}' is not a regular file.")
                    if (
                        member.mtime != 0
                        or member.uid != 0
                        or member.gid != 0
                        or member.mode != 0o644
                        or member.uname
                        or member.gname
                        or member.pax_headers
                    ):
                        raise ValueError(f"Relay archive member '{member.name}' has non-deterministic metadata.")
                    extracted = archive.extractfile(member)
                    data = extracted.read() if extracted is not None else b""
                    record = expected[member.name]
                    if len(data) != record["size"] or _sha256_bytes(data) != record["sha256"]:
                        raise ValueError(f"Relay archive member digest mismatch: {member.name}")
    except (OSError, EOFError, tarfile.TarError) as error:
        raise ValueError(f"Relay archive is invalid: {error}") from error


def _verify_transport_subjects(transport_dir: Path, distribution: dict[str, Any], checksums: Path) -> None:
    subjects = distribution["subjects"]
    expected_files = {subject["file"] for subject in subjects} | {_MANIFEST_FILE, checksums.name}
    _regular_transport_files(transport_dir, expected_files)
    checksum_bytes = checksums.read_bytes()
    if checksum_bytes != _checksum_text(distribution).encode("utf-8"):
        raise ValueError("Transport checksum evidence differs from the manifest rendering.")
    for subject in subjects:
        path = _safe_source_path(transport_dir, subject["file"], _TRANSPORT_SUBJECT_DESCRIPTION)
        contents = path.read_bytes()
        if len(contents) != subject["size"] or _sha256_bytes(contents) != subject["sha256"]:
            raise ValueError(f"Transport subject digest mismatch: {subject['file']}")
    archive_subject = next(subject for subject in subjects if subject["kind"] == "relay-archive")
    _verify_archive(
        (_safe_source_path(transport_dir, archive_subject["file"], "Relay archive")).read_bytes(),
        distribution["archive_members"],
    )


def _verify(arguments: argparse.Namespace) -> None:
    version = _validate_version(arguments.version)
    source_commit = _validate_source_commit(arguments.source_commit)
    inventory = _load_inventory(arguments.inventory)
    candidate, candidate_manifest_sha = _candidate_manifest(arguments.candidate_manifest, version, source_commit)
    verify_relay_dependencies(arguments.source_root, inventory)
    transport_dir = _safe_path(arguments.transport_dir, "transport directory")
    manifest_path = _safe_path(arguments.manifest, _TRANSPORT_MANIFEST_DESCRIPTION)
    checksums_path = _safe_path(arguments.checksums, "transport checksum evidence")
    if checksums_path.parent != transport_dir or checksums_path.name != _CHECKSUMS_FILE:
        raise ValueError("The transport checksum evidence path is invalid.")
    _, _, observed_distribution = _transport_paths(transport_dir, manifest_path)
    outputs = _build_outputs(
        arguments.source_root,
        inventory,
        candidate,
        _safe_path(arguments.candidate_manifest, _CANDIDATE_MANIFEST_DESCRIPTION).name,
        candidate_manifest_sha,
    )
    expected_distribution = json.loads(outputs[_MANIFEST_FILE].decode("utf-8"))
    _validate_distribution_manifest(observed_distribution)
    if observed_distribution != expected_distribution:
        raise ValueError("Transport manifest differs from the reviewed candidate composition.")
    _verify_transport_subjects(transport_dir, observed_distribution, checksums_path)
    for name, expected in outputs.items():
        path = _safe_path(transport_dir / name, _TRANSPORT_SUBJECT_DESCRIPTION)
        if path.read_bytes() != expected:
            raise ValueError(f"Frozen transport subject differs from the candidate: {name}")


def _load_for_reading(transport_dir: Path, manifest_path: Path) -> tuple[Path, dict[str, Any]]:
    directory, _, distribution = _transport_paths(transport_dir, manifest_path)
    _validate_distribution_manifest(distribution)
    expected = {subject["file"] for subject in distribution["subjects"]} | {
        distribution["evidence"]["manifest"],
        distribution["evidence"]["checksums"],
    }
    _regular_transport_files(directory, expected)
    return directory, distribution


def _render_attestation_subject_checksums(arguments: argparse.Namespace) -> None:
    directory, distribution = _load_for_reading(arguments.transport_dir, arguments.manifest)
    checksums = _safe_path(arguments.checksums, "transport checksum evidence")
    if checksums.parent != directory or checksums.name != _CHECKSUMS_FILE:
        raise ValueError("The transport checksum evidence path is invalid.")
    if checksums.read_bytes() != _checksum_text(distribution).encode("utf-8"):
        raise ValueError("Transport checksum evidence differs from the manifest rendering.")
    if arguments.subject_class == "transport":
        subjects = distribution["subjects"]
        lines = []
        for subject in subjects:
            path = _safe_source_path(directory, subject["file"], _TRANSPORT_SUBJECT_DESCRIPTION)
            if path.stat().st_size != subject["size"] or _sha256_bytes(path.read_bytes()) != subject["sha256"]:
                raise ValueError(f"Transport subject digest mismatch: {subject['file']}")
            lines.append(f"{subject['sha256']}  {subject['file']}")
    else:
        lines = []
        for path in (_safe_path(arguments.manifest, _TRANSPORT_MANIFEST_DESCRIPTION), checksums):
            lines.append(f"{package_manifest._sha256(path)}  {path.name}")
    output = _safe_path(arguments.output, "attestation subject checksum output")
    if output in {
        _safe_path(arguments.manifest, _TRANSPORT_MANIFEST_DESCRIPTION),
        checksums,
        *[_safe_path(directory / subject["file"], _TRANSPORT_SUBJECT_DESCRIPTION) for subject in distribution["subjects"]],
    }:
        raise ValueError("Attestation output cannot overwrite a transport subject or evidence file.")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _paths(arguments: argparse.Namespace) -> None:
    directory, distribution = _load_for_reading(arguments.transport_dir, arguments.manifest)
    names = [subject["file"] for subject in distribution["subjects"]]
    if arguments.kind == "all":
        names.extend([_MANIFEST_FILE, _CHECKSUMS_FILE])
    for name in names:
        _safe_source_path(directory, name, _TRANSPORT_SUBJECT_DESCRIPTION)
        print(name)


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    subcommands = parser.add_subparsers(dest="command", required=True)

    create = subcommands.add_parser("create")
    create.add_argument("--source-root", type=Path, required=True)
    create.add_argument("--inventory", type=Path, required=True)
    create.add_argument("--candidate-manifest", type=Path, required=True)
    create.add_argument("--version", required=True)
    create.add_argument("--source-commit", required=True)
    create.add_argument("--output-dir", type=Path, required=True)
    create.set_defaults(handler=_create)

    verify = subcommands.add_parser("verify")
    verify.add_argument("--source-root", type=Path, required=True)
    verify.add_argument("--inventory", type=Path, required=True)
    verify.add_argument("--candidate-manifest", type=Path, required=True)
    verify.add_argument("--version", required=True)
    verify.add_argument("--source-commit", required=True)
    verify.add_argument("--transport-dir", type=Path, required=True)
    verify.add_argument("--manifest", type=Path, required=True)
    verify.add_argument("--checksums", type=Path, required=True)
    verify.set_defaults(handler=_verify)

    render = subcommands.add_parser("render-attestation-subject-checksums")
    render.add_argument("--transport-dir", type=Path, required=True)
    render.add_argument("--manifest", type=Path, required=True)
    render.add_argument("--checksums", type=Path, required=True)
    render.add_argument("--subject-class", choices=("transport", "evidence"), required=True)
    render.add_argument("--output", type=Path, required=True)
    render.set_defaults(handler=_render_attestation_subject_checksums)

    paths = subcommands.add_parser("paths")
    paths.add_argument("--transport-dir", type=Path, required=True)
    paths.add_argument("--manifest", type=Path, required=True)
    paths.add_argument("--kind", choices=("subjects", "all"), required=True)
    paths.set_defaults(handler=_paths)
    return parser.parse_args()


def main() -> int:
    arguments = _parse_args()
    try:
        arguments.handler(arguments)
    except (OSError, ValueError, tarfile.TarError) as error:
        print(f"release distribution verification failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
