#!/usr/bin/env python3
"""Bind the authoritative release-scope issue state to the immutable candidate.

Issue #466 requires that every required item under story #434 is closed before the packed-artifact
gate may authorize publication, and that the closed release-scope inventory is part of the emitted
evidence. The required set is declared in `release-scope.json` so it is reviewed in the repository;
this tool resolves only the *current* state of those issues and binds the result to the candidate
manifest digest and source commit, so the aggregator cannot be handed a stale or unrelated
inventory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from typing import Any

_DECLARATION_SCHEMA = "checkpoint-b-release-scope-declaration/v1"
_EVIDENCE_SCHEMA = "checkpoint-b-release-scope/v1"


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
    numbers = [item.get("issue") for item in required]
    if any(not isinstance(number, int) for number in numbers):
        raise ValueError("Every required item must declare an integer issue number.")
    if len(set(numbers)) != len(numbers):
        raise ValueError("The release-scope declaration lists a duplicate required item.")
    return declaration


def _resolve_states(repository: str, numbers: list[int]) -> dict[int, dict[str, Any]]:
    """Read issue state from GitHub. Any failure is fatal: an unverifiable scope must not pass."""
    resolved: dict[int, dict[str, Any]] = {}
    for number in numbers:
        completed = subprocess.run(
            ["gh", "issue", "view", str(number), "--repo", repository,
             "--json", "number,state,title"],
            capture_output=True, text=True, check=False)
        if completed.returncode != 0:
            raise ValueError(f"Cannot resolve issue #{number}: {completed.stderr.strip()}")
        issue = json.loads(completed.stdout)
        resolved[number] = {
            "issue": issue["number"],
            "state": str(issue["state"]).lower(),
            "title": issue["title"],
        }
    return resolved


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--declaration", type=Path,
                        default=Path(__file__).with_name("release-scope.json"))
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    declaration = _read_declaration(arguments.declaration)
    manifest = json.loads(arguments.candidate_manifest.read_text(encoding="utf-8"))
    if manifest.get("source_commit") != arguments.source_commit:
        raise ValueError("Candidate manifest source commit does not match the checked commit.")

    required = declaration["required_items"]
    states = _resolve_states(arguments.repository, [item["issue"] for item in required])
    evidence = {
        "schema": _EVIDENCE_SCHEMA,
        "release_target": declaration["release_target"],
        "story": declaration["story"],
        "repository": arguments.repository,
        "source_commit": arguments.source_commit,
        "candidate_manifest_sha256": _sha256(arguments.candidate_manifest),
        "required_items": [
            {**item, "state": states[item["issue"]]["state"], "title": states[item["issue"]]["title"]}
            for item in required
        ],
        "excluded_items": declaration.get("excluded_items", []),
    }
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
