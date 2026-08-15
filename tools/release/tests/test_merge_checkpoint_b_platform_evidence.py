from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from aggregate_checkpoint_b_evidence import _REQUIRED_SCENARIOS  # noqa: E402
from merge_checkpoint_b_platform_evidence import merge_platform_shards  # noqa: E402

_PACKAGES = [
    {"id": name, "version": "0.6.1", "file": f"{name}.0.6.1.nupkg", "size": 1, "sha256": "a" * 64}
    for name in ("ArchLinterNet.CEL", "ArchLinterNet.Cli", "ArchLinterNet.Core", "ArchLinterNet.Testing")
]
_COMMIT = "b" * 40
_POLICY_SHAPE = {
    "policy_documents": 5,
    "imported_fragments": 4,
    "governed_module_assemblies": 20,
    "authored_directional_assembly_contracts": 3,
    "expanded_directional_assembly_instances": 40,
    "governed_projects": 22,
    "authored_project_metadata_contracts": 2,
    "declared_project_inventories": 0,
    "inline_public_api_signatures": 0,
}
_SHARDS = [
    "package-and-entrypoints",
    "adopter-runtime",
    "consumer-cleanup",
    "public-api-surface-selector",
]


def _write_corpus(tmp_path: Path) -> tuple[Path, Path]:
    manifest_path = tmp_path / "package-manifest.json"
    manifest_path.write_text(json.dumps({
        "schema": "checkpoint-b-candidate-manifest/v1",
        "version": "0.6.1",
        "source_commit": _COMMIT,
        "packages": _PACKAGES,
    }))
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    shards = tmp_path / "shards"
    shards.mkdir()

    scenarios = [
        {"id": scenario_id, "result": "passed", "reason": None}
        for scenario_id in sorted(_REQUIRED_SCENARIOS)
    ]
    buckets = {shard: [] for shard in _SHARDS}
    for index, scenario in enumerate(scenarios):
        buckets[_SHARDS[index % len(_SHARDS)]].append(scenario)

    for shard_id, shard_scenarios in buckets.items():
        (shards / f"checkpoint-b-platform-shard-{shard_id}.json").write_text(json.dumps({
            "schema": "checkpoint-b-platform-shard-evidence/v1",
            "checkpoint": "B",
            "shard_id": shard_id,
            "result": "passed",
            "candidate_version": "0.6.1",
            "source_commit": _COMMIT,
            "platform_id": "windows-x64",
            "platform": "Windows",
            "runtime": ".NET 10.0.0",
            "architecture": "X64",
            "shell": "pwsh",
            "synthetic_identities_only": True,
            "candidate_manifest_sha256": digest,
            "packages": _PACKAGES,
            "policy_shape": _POLICY_SHAPE if shard_id == "consumer-cleanup" else None,
            "scenarios": shard_scenarios,
        }))
    return manifest_path, shards


def test_merges_complete_disjoint_shards(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    merged = merge_platform_shards(shards, manifest)

    assert merged["schema"] == "checkpoint-b-platform-evidence/v1"
    assert merged["platform_id"] == "windows-x64"
    assert merged["policy_shape"] == _POLICY_SHAPE
    assert {scenario["id"] for scenario in merged["scenarios"]} == _REQUIRED_SCENARIOS


def test_rejects_missing_shard(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    (shards / "checkpoint-b-platform-shard-adopter-runtime.json").unlink()

    with pytest.raises(ValueError, match="Expected 4 Checkpoint B shard records"):
        merge_platform_shards(shards, manifest)


def test_rejects_overlapping_scenario_ids(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    package_path = shards / "checkpoint-b-platform-shard-package-and-entrypoints.json"
    adopter_path = shards / "checkpoint-b-platform-shard-adopter-runtime.json"
    package = json.loads(package_path.read_text())
    adopter = json.loads(adopter_path.read_text())
    adopter["scenarios"].append(package["scenarios"][0])
    adopter_path.write_text(json.dumps(adopter))

    with pytest.raises(ValueError, match="overlap between shards"):
        merge_platform_shards(shards, manifest)


def test_rejects_candidate_mismatch(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    path = shards / "checkpoint-b-platform-shard-consumer-cleanup.json"
    record = json.loads(path.read_text())
    record["candidate_version"] = "9.9.9"
    path.write_text(json.dumps(record))

    with pytest.raises(ValueError, match="candidate version differs"):
        merge_platform_shards(shards, manifest)
