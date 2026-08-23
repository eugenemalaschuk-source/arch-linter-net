#!/usr/bin/env python3
"""Bind a candidate-selected release scope to immutable Checkpoint B evidence.

Release authorities are reviewed declarations in the fixed ``tools/release/scopes`` directory.
The generator selects exactly one declaration by matching its explicit stable release target to the
candidate manifest version; declaration filenames and caller-provided paths carry no release
semantics. It resolves only required items' live issue-tracker states and binds that inventory,
declaration identity/bytes, manifest digest, candidate version, and source commit into evidence.

This release-authorizing command takes no declaration, manifest, or output path arguments. Those
locations are fixed in the release workspace. ``build_evidence`` retains explicit paths only as a
non-CLI test seam.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any

from _release_workspace import _allowed_roots, _repository_root, _safe_path  # noqa: F401

_DECLARATION_SCHEMA = "checkpoint-b-release-scope-declaration/v2"
_EVIDENCE_SCHEMA = "checkpoint-b-release-scope/v2"
_REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$")
_COMMIT_PATTERN = re.compile(r"^[0-9a-fA-F]{7,64}$")
_DECLARATION_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
_RELEASE_TARGET_PATTERN = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")


# Fixed release-workspace locations. The release workflow already pins these paths for every other
# tool in this directory; keeping them here instead of in argv means a release-authorizing script
# has no way to be pointed at a different manifest or to write its verdict somewhere else.
def _declarations_directory() -> Path:
    return Path(__file__).with_name("scopes")


def _candidate_manifest_path() -> Path:
    return _repository_root() / "artifacts" / "candidate" / "package-manifest.json"


def _output_path() -> Path:
    return _repository_root() / "artifacts" / "checkpoint-b" / "release-scope.json"


def _repository(value: str) -> str:
    if not _REPOSITORY_PATTERN.fullmatch(value):
        raise ValueError(f"'{value}' is not a valid GitHub owner/name repository.")
    return value


def _source_commit(value: str) -> str:
    if not _COMMIT_PATTERN.fullmatch(value):
        raise ValueError(f"'{value}' is not a valid commit SHA.")
    return value


def _issue_number(value: Any) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise ValueError(f"'{value}' is not a valid issue number.")
    return value


def _release_target(value: Any, description: str) -> str:
    if not isinstance(value, str) or not _RELEASE_TARGET_PATTERN.fullmatch(value):
        raise ValueError(f"{description} must be an exact stable release target.")
    return value


def _declaration_id(value: Any) -> str:
    if not isinstance(value, str) or not _DECLARATION_ID_PATTERN.fullmatch(value):
        raise ValueError("The release-scope declaration has an invalid declaration identity.")
    return value


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _items(declaration: dict[str, Any], name: str, *, required: bool, reasons: bool) -> list[dict[str, Any]]:
    items = declaration.get(name)
    if not isinstance(items, list) or (required and not items):
        raise ValueError(f"The release-scope declaration lists no {name.replace('_', ' ')}.")
    numbers: list[int] = []
    for item in items:
        if not isinstance(item, dict):
            raise ValueError(f"The release-scope declaration has a malformed {name.replace('_', ' ')} item.")
        numbers.append(_issue_number(item.get("issue")))
        if reasons and (not isinstance(item.get("reason"), str) or not item["reason"].strip()):
            raise ValueError(f"The release-scope declaration {name.replace('_', ' ')} item has no reason.")
    if len(set(numbers)) != len(numbers):
        raise ValueError(f"The release-scope declaration lists a duplicate {name.replace('_', ' ')} item.")
    return items


def _read_declaration(path: Path) -> dict[str, Any]:
    try:
        declaration = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read release-scope declaration '{path}': {error}") from error
    if not isinstance(declaration, dict):
        raise ValueError(f"'{path}' is not a release-scope declaration.")
    if declaration.get("schema") != _DECLARATION_SCHEMA:
        raise ValueError(f"'{path}' is not a release-scope declaration.")
    _declaration_id(declaration.get("declaration_id"))
    _release_target(declaration.get("release_target"), "The declaration release target")
    _issue_number(declaration.get("story"))
    required = _items(declaration, "required_items", required=True, reasons=False)
    excluded = _items(declaration, "excluded_items", required=False, reasons=True)
    delivered = _items(declaration, "delivered_items", required=False, reasons=True)
    required_numbers = {item["issue"] for item in required}
    excluded_numbers = {item["issue"] for item in excluded}
    delivered_numbers = {item["issue"] for item in delivered}
    if required_numbers & excluded_numbers or required_numbers & delivered_numbers or excluded_numbers & delivered_numbers:
        raise ValueError("The release-scope declaration repeats an item across inventories.")
    return declaration


def _select_declaration(declarations_directory: Path, candidate_version: str) -> tuple[Path, dict[str, Any]]:
    declarations_directory = _safe_path(declarations_directory, "release-scope declarations")
    if not declarations_directory.is_dir():
        raise ValueError(f"Release-scope declarations directory '{declarations_directory}' is missing.")

    declarations: list[tuple[Path, dict[str, Any]]] = []
    for path in sorted(declarations_directory.glob("*.json")):
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Release-scope declaration '{path}' is not a regular file.")
        declarations.append((path, _read_declaration(path)))
    matches = [entry for entry in declarations if entry[1]["release_target"] == candidate_version]
    if not matches:
        raise ValueError(f"No reviewed release-scope declaration matches candidate target {candidate_version}.")
    if len(matches) != 1:
        raise ValueError(f"Multiple release-scope declarations match candidate target {candidate_version}.")
    return matches[0]


def _resolve_states(repository: str, numbers: list[int]) -> dict[int, dict[str, Any]]:
    """Read issue state from GitHub. Any failure is fatal: an unverifiable scope must not pass."""
    resolved: dict[int, dict[str, Any]] = {}
    for number in numbers:
        # Fixed argv, no shell, and both interpolated values re-validated at the call site.
        completed = subprocess.run(
            ["gh", "issue", "view", str(_issue_number(number)), "--repo", _repository(repository),
             "--json", "number,state,title"],
            capture_output=True, text=True, check=False, shell=False)
        if completed.returncode != 0:
            raise ValueError(f"Cannot resolve issue #{number}: {completed.stderr.strip()}")
        try:
            issue = json.loads(completed.stdout)
        except json.JSONDecodeError as error:
            raise ValueError(f"Cannot resolve issue #{number}: invalid GitHub response.") from error
        state = issue.get("state") if isinstance(issue, dict) else None
        title = issue.get("title") if isinstance(issue, dict) else None
        if (
            not isinstance(issue, dict)
            or issue.get("number") != number
            or not isinstance(state, str)
            or not isinstance(title, str)
        ):
            raise ValueError(f"Cannot resolve issue #{number}: invalid GitHub response.")
        state = state.lower()
        if state not in {"open", "closed"}:
            raise ValueError(f"Cannot resolve issue #{number}: invalid GitHub response.")
        resolved[number] = {
            "issue": number,
            "state": state,
            "title": title,
        }
    return resolved


def build_evidence(
    declarations_directory: Path,
    candidate_manifest: Path,
    source_commit: str,
    repository: str,
) -> dict[str, Any]:
    candidate_manifest = _safe_path(candidate_manifest, "candidate manifest")
    source_commit = _source_commit(source_commit)
    repository = _repository(repository)

    try:
        manifest = json.loads(candidate_manifest.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read candidate manifest '{candidate_manifest}': {error}") from error
    if not isinstance(manifest, dict):
        raise ValueError("Candidate manifest must be a JSON object.")
    if manifest.get("source_commit") != source_commit:
        raise ValueError("Candidate manifest source commit does not match the checked commit.")
    candidate_version = _release_target(manifest.get("version"), "Candidate manifest version")
    declaration_path, declaration = _select_declaration(declarations_directory, candidate_version)
    if declaration["release_target"] != candidate_version:
        raise ValueError("Release-scope declaration target differs from the candidate manifest version.")

    required = declaration["required_items"]
    states = _resolve_states(repository, [item["issue"] for item in required])
    return {
        "schema": _EVIDENCE_SCHEMA,
        "declaration_id": declaration["declaration_id"],
        "declaration_sha256": _sha256(declaration_path),
        "candidate_version": candidate_version,
        "release_target": declaration["release_target"],
        "story": declaration["story"],
        "repository": repository,
        "source_commit": source_commit,
        "candidate_manifest_sha256": _sha256(candidate_manifest),
        "required_items": [
            {**item, "state": states[item["issue"]]["state"], "title": states[item["issue"]]["title"]}
            for item in required
        ],
        "excluded_items": declaration["excluded_items"],
        "delivered_items": declaration["delivered_items"],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--repository", required=True)
    arguments = parser.parse_args()

    evidence = build_evidence(
        _declarations_directory(),
        _candidate_manifest_path(),
        arguments.source_commit,
        arguments.repository)
    output = _output_path()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
