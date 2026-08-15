#!/usr/bin/env python3
"""Merge isolated Checkpoint B scenario shards into one canonical platform record."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from _release_workspace import _safe_path
from aggregate_checkpoint_b_evidence import (
    _POLICY_SHAPE_FIELDS,
    _REQUIRED_SCENARIOS,
    _SCENARIO_RESULTS,
    _read_manifest,
    _sha256,
)

_SHARD_SCHEMA = "checkpoint-b-platform-shard-evidence/v1"
_PLATFORM_SCHEMA = "checkpoint-b-platform-evidence/v1"
_REQUIRED_SHARDS = {
    "package-and-entrypoints",
    "adopter-runtime-core",
    "adopter-runtime-extended",
    "consumer-cleanup-policy-execution",
    "consumer-cleanup-policy-contracts-and-shape",
    "consumer-cleanup-configuration-and-identity",
    "consumer-cleanup-source-set-authoring",
    "public-api-surface-selector-snapshot-and-role",
    "public-api-surface-selector-delta-and-membership",
    "public-api-surface-selector-enforcement",
}
_COMMON_FIELDS = (
    "checkpoint",
    "candidate_version",
    "source_commit",
    "platform_id",
    "platform",
    "runtime",
    "architecture",
    "shell",
    "synthetic_identities_only",
    "candidate_manifest_sha256",
    "packages",
)


def _load(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read Checkpoint B shard '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"Checkpoint B shard '{path}' must be a JSON object.")
    return value


def merge_platform_shards(input_directory: Path, candidate_manifest: Path) -> dict[str, Any]:
    manifest = _read_manifest(candidate_manifest)
    manifest_digest = _sha256(candidate_manifest)
    paths = sorted(input_directory.rglob("checkpoint-b-platform-shard-*.json"))
    if len(paths) != len(_REQUIRED_SHARDS):
        raise ValueError(
            f"Expected {len(_REQUIRED_SHARDS)} Checkpoint B shard records, found {len(paths)}."
        )

    records = [(path, _load(path)) for path in paths]
    shard_ids = [record.get("shard_id") for _, record in records]
    if len(set(shard_ids)) != len(shard_ids) or set(shard_ids) != _REQUIRED_SHARDS:
        raise ValueError(f"Checkpoint B shard inventory mismatch: {sorted(str(value) for value in shard_ids)}.")

    first_path, first = records[0]
    for path, record in records:
        if record.get("schema") != _SHARD_SCHEMA:
            raise ValueError(f"{path} does not use the supported shard evidence schema.")
        if record.get("checkpoint") != "B" or record.get("result") not in {"passed", "failed"}:
            raise ValueError(f"{path} does not report a Checkpoint B shard result.")
        if record.get("synthetic_identities_only") is not True:
            raise ValueError(f"{path} does not affirm synthetic identities only.")
        if record.get("candidate_version") != manifest["version"]:
            raise ValueError(f"{path} candidate version differs from the manifest.")
        if record.get("source_commit") != manifest["source_commit"]:
            raise ValueError(f"{path} source commit differs from the manifest.")
        if record.get("candidate_manifest_sha256") != manifest_digest:
            raise ValueError(f"{path} is not bound to the candidate manifest digest.")
        if record.get("packages") != manifest["packages"]:
            raise ValueError(f"{path} package inventory differs from the candidate manifest.")
        for field in _COMMON_FIELDS:
            if record.get(field) != first.get(field):
                raise ValueError(
                    f"Checkpoint B shards disagree on '{field}': '{first_path}' versus '{path}'."
                )

        scenarios = record.get("scenarios")
        if not isinstance(scenarios, list) or not scenarios:
            raise ValueError(f"{path} has no scenario results.")
        ids: list[str] = []
        for scenario in scenarios:
            if not isinstance(scenario, dict) or not isinstance(scenario.get("id"), str):
                raise ValueError(f"{path} contains a malformed scenario record.")
            if scenario.get("result") not in _SCENARIO_RESULTS:
                raise ValueError(f"{path} contains a malformed scenario result.")
            if scenario["result"] != "passed" and not isinstance(scenario.get("reason"), str):
                raise ValueError(f"{path} does not explain a non-passing scenario.")
            ids.append(scenario["id"])
        if len(ids) != len(set(ids)):
            raise ValueError(f"{path} contains duplicate scenario IDs.")
        declared_failed = any(scenario["result"] == "failed" for scenario in scenarios)
        if declared_failed != (record["result"] == "failed"):
            raise ValueError(f"{path} shard result contradicts its scenario results.")

    all_scenarios = [scenario for _, record in records for scenario in record["scenarios"]]
    all_ids = [scenario["id"] for scenario in all_scenarios]
    if len(all_ids) != len(set(all_ids)):
        raise ValueError("Checkpoint B scenario IDs overlap between shards.")
    if set(all_ids) != _REQUIRED_SCENARIOS:
        missing = sorted(_REQUIRED_SCENARIOS - set(all_ids))
        unexpected = sorted(set(all_ids) - _REQUIRED_SCENARIOS)
        raise ValueError(
            f"Checkpoint B shard union is incomplete: missing={missing}, unexpected={unexpected}."
        )

    shapes = [
        (record["shard_id"], record.get("policy_shape"))
        for _, record in records
        if record.get("policy_shape") is not None
    ]
    if len(shapes) != 1 or shapes[0][0] != "consumer-cleanup-policy-contracts-and-shape":
        raise ValueError("Exactly the consumer-cleanup policy contracts-and-shape shard must report policy_shape.")
    policy_shape = shapes[0][1]
    if not isinstance(policy_shape, dict) or set(policy_shape) != _POLICY_SHAPE_FIELDS:
        raise ValueError("Checkpoint B consumer-cleanup shard reports an invalid policy_shape.")
    if any(not isinstance(value, int) for value in policy_shape.values()):
        raise ValueError("Checkpoint B policy_shape contains a non-numeric counter.")

    return {
        "schema": _PLATFORM_SCHEMA,
        "checkpoint": "B",
        "result": "failed" if any(scenario["result"] == "failed" for scenario in all_scenarios) else "passed",
        "candidate_version": first["candidate_version"],
        "source_commit": first["source_commit"],
        "platform_id": first["platform_id"],
        "platform": first["platform"],
        "runtime": first["runtime"],
        "architecture": first["architecture"],
        "shell": first["shell"],
        "synthetic_identities_only": True,
        "candidate_manifest_sha256": first["candidate_manifest_sha256"],
        "packages": first["packages"],
        "policy_shape": policy_shape,
        "scenarios": sorted(all_scenarios, key=lambda scenario: scenario["id"]),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    input_dir = _safe_path(arguments.input_dir, "input directory")
    candidate_manifest = _safe_path(arguments.candidate_manifest, "candidate manifest")
    output = _safe_path(arguments.output, "output path")

    merged = merge_platform_shards(input_dir, candidate_manifest)
    output.parent.mkdir(parents=True, exist_ok=True)
    # NOSONAR: 'output' is already confined to the working tree or repo root by _safe_path above,
    # and 'merged' is assembled only from shard files discovered by rglob() strictly under the
    # confined input_directory, so this write cannot escape the release workspace. Sonar's Python
    # taint tracker does not recognize a cross-module call as a sanitizer, so it still reports
    # pythonsecurity:S2083/S8707 here; the same _safe_path-guarded shape carries the identical
    # rationale in aggregate_checkpoint_b_evidence.py and create_repository_gate_evidence.py.
    output.write_text(json.dumps(merged, indent=2, sort_keys=True) + "\n", encoding="utf-8")  # NOSONAR
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
