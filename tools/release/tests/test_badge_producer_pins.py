"""Catch reviewed first-party producer pin drift before post-merge publication."""

from __future__ import annotations

import json
from pathlib import Path
import subprocess

import pytest


ROOT = Path(__file__).resolve().parents[3]
REGISTRY_PATH = ".github/badge-promotion/registry.json"
WORKFLOW_PATH = ".github/workflows/ci.yml"


@pytest.mark.parametrize("configuration_id", ("reference-public-raw", "reference-none"))
def test_first_party_producer_pin_matches_workflow_blob(configuration_id: str) -> None:
    registry = json.loads((ROOT / REGISTRY_PATH).read_text(encoding="utf-8"))
    configuration = registry["configurations"][configuration_id]
    assert configuration["repository"] == "eugenemalaschuk-source/arch-linter-net"
    producer = configuration["producer"]
    assert producer["workflow_path"] == WORKFLOW_PATH
    assert (ROOT / WORKFLOW_PATH).is_file()

    # Hash working-tree content with Git's path-specific clean/EOL rules, not a
    # commit SHA or a raw file checksum. This also works with Windows CRLF
    # checkouts and detects uncommitted workflow edits. No -w: read-only check.
    actual_sha = subprocess.run(
        ["git", "-C", str(ROOT), "hash-object", f"--path={WORKFLOW_PATH}", "--", WORKFLOW_PATH],
        check=True,
        capture_output=True,
        text=True,
        timeout=10,
    ).stdout.strip()
    assert producer["workflow_sha"] == actual_sha, (
        f"{configuration_id}: approved producer pin {producer['workflow_sha']} "
        f"does not match {WORKFLOW_PATH} Git blob {actual_sha}. "
        f"Review the workflow change, then update {REGISTRY_PATH} in the same PR. "
        "Do not auto-refresh pins at runtime or disable the workflow identity check."
    )
