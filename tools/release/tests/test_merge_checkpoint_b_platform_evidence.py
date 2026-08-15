from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path
from typing import Any, Callable

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import merge_checkpoint_b_platform_evidence as merger  # noqa: E402
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
    "consumer-cleanup-policy-foundation",
    "consumer-cleanup-configuration-and-identity",
    "consumer-cleanup-source-set-authoring",
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
            "policy_shape": _POLICY_SHAPE if shard_id == "consumer-cleanup-policy-foundation" else None,
            "scenarios": shard_scenarios,
        }))
    return manifest_path, shards


def _corrupt(shards: Path, shard_id: str, mutate: Callable[[dict[str, Any]], None]) -> Path:
    path = shards / f"checkpoint-b-platform-shard-{shard_id}.json"
    record = json.loads(path.read_text())
    mutate(record)
    path.write_text(json.dumps(record))
    return path


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

    with pytest.raises(ValueError, match="Expected 6 Checkpoint B shard records"):
        merge_platform_shards(shards, manifest)


def test_rejects_malformed_shard_json(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    (shards / "checkpoint-b-platform-shard-adopter-runtime.json").write_text("{not valid json")

    with pytest.raises(ValueError, match="Cannot read Checkpoint B shard"):
        merge_platform_shards(shards, manifest)


def test_rejects_non_object_shard_json(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    (shards / "checkpoint-b-platform-shard-adopter-runtime.json").write_text(json.dumps([1, 2, 3]))

    with pytest.raises(ValueError, match="must be a JSON object"):
        merge_platform_shards(shards, manifest)


def test_rejects_duplicate_shard_ids(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"shard_id": "consumer-cleanup-policy-foundation"}))

    with pytest.raises(ValueError, match="shard inventory mismatch"):
        merge_platform_shards(shards, manifest)


def test_rejects_unsupported_shard_schema(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"schema": "wrong/v0"}))

    with pytest.raises(ValueError, match="does not use the supported shard evidence schema"):
        merge_platform_shards(shards, manifest)


def test_rejects_invalid_shard_result(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"result": "unknown"}))

    with pytest.raises(ValueError, match="does not report a Checkpoint B shard result"):
        merge_platform_shards(shards, manifest)


def test_rejects_non_synthetic_identities(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"synthetic_identities_only": False}))

    with pytest.raises(ValueError, match="does not affirm synthetic identities only"):
        merge_platform_shards(shards, manifest)


def test_rejects_candidate_mismatch(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "consumer-cleanup-policy-foundation", lambda record: record.update({"candidate_version": "9.9.9"}))

    with pytest.raises(ValueError, match="candidate version differs"):
        merge_platform_shards(shards, manifest)


def test_rejects_source_commit_mismatch(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"source_commit": "c" * 40}))

    with pytest.raises(ValueError, match="source commit differs from the manifest"):
        merge_platform_shards(shards, manifest)


def test_rejects_manifest_digest_mismatch(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"candidate_manifest_sha256": "0" * 64}))

    with pytest.raises(ValueError, match="not bound to the candidate manifest digest"):
        merge_platform_shards(shards, manifest)


def test_rejects_package_inventory_mismatch(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"packages": []}))

    with pytest.raises(ValueError, match="package inventory differs from the candidate manifest"):
        merge_platform_shards(shards, manifest)


def test_rejects_common_field_disagreement(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"platform_id": "linux-x64"}))

    with pytest.raises(ValueError, match="shards disagree on 'platform_id'"):
        merge_platform_shards(shards, manifest)


def test_rejects_shard_without_scenarios(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"scenarios": []}))

    with pytest.raises(ValueError, match="has no scenario results"):
        merge_platform_shards(shards, manifest)


def test_rejects_malformed_scenario_record(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record["scenarios"].__setitem__(0, "not-a-dict"))

    with pytest.raises(ValueError, match="contains a malformed scenario record"):
        merge_platform_shards(shards, manifest)


def test_rejects_malformed_scenario_result(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record["scenarios"][0].update({"result": "maybe"}))

    with pytest.raises(ValueError, match="contains a malformed scenario result"):
        merge_platform_shards(shards, manifest)


def test_rejects_non_passing_scenario_without_reason(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    def _mutate(record: dict[str, Any]) -> None:
        record["scenarios"][0]["result"] = "not_applicable"
        record["scenarios"][0]["reason"] = None

    _corrupt(shards, "adopter-runtime", _mutate)

    with pytest.raises(ValueError, match="does not explain a non-passing scenario"):
        merge_platform_shards(shards, manifest)


def test_rejects_duplicate_scenario_ids_within_shard(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    def _mutate(record: dict[str, Any]) -> None:
        record["scenarios"][1]["id"] = record["scenarios"][0]["id"]

    _corrupt(shards, "adopter-runtime", _mutate)

    with pytest.raises(ValueError, match="contains duplicate scenario IDs"):
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


def test_rejects_shard_result_contradicting_scenarios(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    def _mutate(record: dict[str, Any]) -> None:
        record["scenarios"][0]["result"] = "failed"
        record["scenarios"][0]["reason"] = "Broken."

    _corrupt(shards, "adopter-runtime", _mutate)

    with pytest.raises(ValueError, match="shard result contradicts its scenario results"):
        merge_platform_shards(shards, manifest)


def test_rejects_incomplete_scenario_union(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record["scenarios"].pop())

    with pytest.raises(ValueError, match="shard union is incomplete"):
        merge_platform_shards(shards, manifest)


def test_rejects_policy_shape_reported_by_wrong_shard(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)
    _corrupt(shards, "adopter-runtime", lambda record: record.update({"policy_shape": _POLICY_SHAPE}))

    with pytest.raises(ValueError, match="Exactly the consumer-cleanup policy foundation shard must report policy_shape"):
        merge_platform_shards(shards, manifest)


def test_rejects_invalid_policy_shape_fields(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    def _mutate(record: dict[str, Any]) -> None:
        shape = dict(record["policy_shape"])
        del shape["governed_projects"]
        record["policy_shape"] = shape

    _corrupt(shards, "consumer-cleanup-policy-foundation", _mutate)

    with pytest.raises(ValueError, match="reports an invalid policy_shape"):
        merge_platform_shards(shards, manifest)


def test_rejects_non_numeric_policy_shape_counter(tmp_path: Path) -> None:
    manifest, shards = _write_corpus(tmp_path)

    def _mutate(record: dict[str, Any]) -> None:
        shape = dict(record["policy_shape"])
        shape["governed_projects"] = "22"
        record["policy_shape"] = shape

    _corrupt(shards, "consumer-cleanup-policy-foundation", _mutate)

    with pytest.raises(ValueError, match="non-numeric counter"):
        merge_platform_shards(shards, manifest)


def _run_main(tmp_path: Path) -> tuple[int, dict[str, Any]]:
    manifest, shards = _write_corpus(tmp_path)
    output = tmp_path / "merged" / "checkpoint-b-platform-evidence.json"
    argv = [
        "merge_checkpoint_b_platform_evidence.py",
        "--input-dir", str(shards),
        "--candidate-manifest", str(manifest),
        "--output", str(output),
    ]
    # main() confines every path argument to the working directory or the repository (the same
    # release-workspace guard the other release scripts use), so the CLI matches CI's own
    # invocation shape: paths given relative to the runner's checkout-root working directory.
    original_argv = sys.argv
    original_cwd = Path.cwd()
    sys.argv = argv
    os.chdir(tmp_path)
    try:
        exit_code = merger.main()
    finally:
        sys.argv = original_argv
        os.chdir(original_cwd)
    merged = json.loads(output.read_text())
    return exit_code, merged


def test_main_merges_shards_and_writes_output(tmp_path: Path) -> None:
    exit_code, merged = _run_main(tmp_path)

    assert exit_code == 0
    assert merged["schema"] == "checkpoint-b-platform-evidence/v1"
    assert merged["result"] == "passed"
    assert merged["policy_shape"] == _POLICY_SHAPE
    assert {scenario["id"] for scenario in merged["scenarios"]} == _REQUIRED_SCENARIOS
