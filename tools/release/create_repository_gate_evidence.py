#!/usr/bin/env python3
"""Bind successful repository gates to the immutable Checkpoint B candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _allowed_roots() -> tuple[Path, ...]:
    """Paths are accepted only inside the working tree or the repository this script ships in, so a
    faulty caller (including an LLM agent invoking this script with a hallucinated path) cannot read
    or write outside the release workspace. Resolved per call rather than at import time, so the
    answer never depends on when the module was loaded."""
    return (Path.cwd().resolve(), _repository_root())


def _safe_path(value: Path, description: str) -> Path:
    resolved = os.path.realpath(str(value))
    for root in _allowed_roots():
        candidate = os.path.realpath(str(root))
        try:
            contained = os.path.commonpath([resolved, candidate]) == candidate
        except ValueError:
            # Different drives on Windows: no common path, so this root does not contain it.
            continue
        if contained:
            return Path(resolved)
    raise ValueError(f"The {description} '{value}' resolves outside the release workspace.")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    candidate_manifest = _safe_path(arguments.candidate_manifest, "candidate manifest")
    output = _safe_path(arguments.output, "output path")

    manifest = json.loads(candidate_manifest.read_text(encoding="utf-8"))
    if manifest.get("source_commit") != arguments.source_commit:
        raise ValueError("Candidate manifest source commit does not match the checked commit.")

    run_url = "/".join(
        value.strip("/")
        for value in (os.environ.get("GITHUB_SERVER_URL", ""), os.environ.get("GITHUB_REPOSITORY", ""), "actions/runs", os.environ.get("GITHUB_RUN_ID", ""))
        if value
    )
    evidence = {
        "schema": "checkpoint-b-repository-gates/v1",
        "source_commit": arguments.source_commit,
        "candidate_manifest_sha256": _sha256(candidate_manifest),
        "workflow_run_url": run_url or None,
        "gates": [
            {"id": "acceptance", "result": "passed", "command": "make acceptance"},
            {"id": "openspec_strict", "result": "passed", "command": "openspec validate --all --strict"},
        ],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
