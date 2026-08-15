#!/usr/bin/env python3
"""Bind successful repository gates to the immutable Checkpoint B candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

from _release_workspace import _safe_path


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
