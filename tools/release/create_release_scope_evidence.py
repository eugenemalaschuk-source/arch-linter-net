#!/usr/bin/env python3
"""Bind the authoritative release-scope issue state to the immutable candidate.

Issue #466 requires that every required item under story #434 is closed before the packed-artifact
gate may authorize publication, and that the closed release-scope inventory is part of the emitted
evidence. The required set is declared in `release-scope.json` so it is reviewed in the repository;
this tool resolves only the *current* state of those issues and binds the result to the candidate
manifest digest and source commit, so the aggregator cannot be handed a stale or unrelated
inventory.

This tool authorizes a publication decision, so it takes no path arguments at all: the declaration,
the candidate manifest, and the output all sit at fixed locations in the release workspace, which
the release workflow already pins. The only inputs are the repository and the source commit, both
validated against a strict grammar before they reach an OS command. `build_evidence` keeps explicit
path parameters as the seam the tests drive.
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

_DECLARATION_SCHEMA = "checkpoint-b-release-scope-declaration/v1"
_EVIDENCE_SCHEMA = "checkpoint-b-release-scope/v1"
_REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$")
_COMMIT_PATTERN = re.compile(r"^[0-9a-fA-F]{7,64}$")


# Fixed release-workspace locations. The release workflow already pins these paths for every other
# tool in this directory; keeping them here instead of in argv means a release-authorizing script
# has no way to be pointed at a different manifest or to write its verdict somewhere else.
def _declaration_path() -> Path:
    return Path(__file__).with_name("release-scope.json")


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


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _read_declaration(path: Path) -> dict[str, Any]:
    declaration = json.loads(path.read_text(encoding="utf-8"))
    if declaration.get("schema") != _DECLARATION_SCHEMA:
        raise ValueError(f"'{path}' is not a release-scope declaration.")
    required = declaration.get("required_items")
    if not isinstance(required, list) or not required:
        raise ValueError("The release-scope declaration lists no required items.")
    numbers = [_issue_number(item.get("issue")) for item in required]
    if len(set(numbers)) != len(numbers):
        raise ValueError("The release-scope declaration lists a duplicate required item.")
    return declaration


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
        issue = json.loads(completed.stdout)
        resolved[number] = {
            "issue": issue["number"],
            "state": str(issue["state"]).lower(),
            "title": issue["title"],
        }
    return resolved


def build_evidence(
    declaration_path: Path,
    candidate_manifest: Path,
    source_commit: str,
    repository: str,
) -> dict[str, Any]:
    declaration_path = _safe_path(declaration_path, "release-scope declaration")
    candidate_manifest = _safe_path(candidate_manifest, "candidate manifest")
    source_commit = _source_commit(source_commit)
    repository = _repository(repository)

    declaration = _read_declaration(declaration_path)
    manifest = json.loads(candidate_manifest.read_text(encoding="utf-8"))
    if manifest.get("source_commit") != source_commit:
        raise ValueError("Candidate manifest source commit does not match the checked commit.")

    required = declaration["required_items"]
    states = _resolve_states(repository, [item["issue"] for item in required])
    return {
        "schema": _EVIDENCE_SCHEMA,
        "release_target": declaration["release_target"],
        "story": declaration["story"],
        "repository": repository,
        "source_commit": source_commit,
        "candidate_manifest_sha256": _sha256(candidate_manifest),
        "required_items": [
            {**item, "state": states[item["issue"]]["state"], "title": states[item["issue"]]["title"]}
            for item in required
        ],
        "excluded_items": declaration.get("excluded_items", []),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--repository", required=True)
    arguments = parser.parse_args()

    evidence = build_evidence(
        _declaration_path(),
        _candidate_manifest_path(),
        arguments.source_commit,
        arguments.repository)
    output = _output_path()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
