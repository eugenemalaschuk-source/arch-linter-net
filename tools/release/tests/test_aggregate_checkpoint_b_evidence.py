from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from aggregate_checkpoint_b_evidence import (  # noqa: E402
    _CONSUMER_CLEANUP_SCENARIOS,
    _REQUIRED_PLATFORMS,
    _REQUIRED_SCENARIOS,
    _read_gates,
    _read_manifest,
    _read_records,
    _summary,
)

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


def _scenarios(shell: str, platform: str, failed: dict[str, str] | None = None) -> list[dict]:
    failed = failed or {}
    results = []
    for scenario_id in sorted(_REQUIRED_SCENARIOS):
        if scenario_id in failed:
            results.append({"id": scenario_id, "result": "failed", "reason": failed[scenario_id]})
        elif scenario_id == "posix-entrypoint" and shell == "pwsh":
            results.append({"id": scenario_id, "result": "not_applicable", "reason": "PowerShell job."})
        elif scenario_id == "powershell-entrypoint" and shell != "pwsh":
            results.append({"id": scenario_id, "result": "not_applicable", "reason": "POSIX job."})
        elif scenario_id == "in-flight-cancellation" and platform != "linux-x64":
            results.append({"id": scenario_id, "result": "not_applicable", "reason": "Linux oracle."})
        else:
            results.append({"id": scenario_id, "result": "passed", "reason": None})
    return results


def _write_corpus(
    tmp_path: Path,
    failed: dict[str, str] | None = None,
    policy_shape: dict | None = None,
) -> tuple[Path, Path, Path]:
    manifest_path = tmp_path / "package-manifest.json"
    manifest_path.write_text(json.dumps({
        "schema": "checkpoint-b-candidate-manifest/v1",
        "version": "0.6.1",
        "source_commit": _COMMIT,
        "packages": _PACKAGES,
    }))
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    platforms = tmp_path / "platforms"
    platforms.mkdir()
    for platform, (architecture, shell) in _REQUIRED_PLATFORMS.items():
        scenarios = _scenarios(shell, platform, failed if platform == "linux-x64" else None)
        (platforms / f"{platform}.json").write_text(json.dumps({
            "schema": "checkpoint-b-platform-evidence/v1",
            "checkpoint": "B",
            "result": "failed" if any(s["result"] == "failed" for s in scenarios) else "passed",
            "candidate_version": "0.6.1",
            "source_commit": _COMMIT,
            "platform_id": platform,
            "platform": platform,
            "runtime": ".NET 10.0.0",
            "architecture": architecture,
            "shell": shell,
            "synthetic_identities_only": True,
            "candidate_manifest_sha256": digest,
            "packages": _PACKAGES,
            "policy_shape": policy_shape or _POLICY_SHAPE,
            "scenarios": scenarios,
        }))

    gates_path = tmp_path / "repository-gates.json"
    gates_path.write_text(json.dumps({
        "schema": "checkpoint-b-repository-gates/v1",
        "source_commit": _COMMIT,
        "candidate_manifest_sha256": digest,
        "gates": [{"id": "acceptance", "result": "passed"}, {"id": "openspec_strict", "result": "passed"}],
    }))
    return manifest_path, platforms, gates_path


def _aggregate(tmp_path: Path, **kwargs) -> dict:
    manifest_path, platforms, gates_path = _write_corpus(tmp_path, **kwargs)
    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    records = _read_records(platforms, manifest, digest)
    gates = _read_gates(gates_path, manifest, digest)
    return _summary(records, manifest, gates, digest)


def test_consumer_cleanup_scenarios_are_required() -> None:
    assert _CONSUMER_CLEANUP_SCENARIOS <= _REQUIRED_SCENARIOS
    assert "source-set-enrolment" in _REQUIRED_SCENARIOS
    assert "consumer-policy-shape" in _REQUIRED_SCENARIOS


def test_complete_matrix_authorizes_publication(tmp_path: Path) -> None:
    summary = _aggregate(tmp_path)

    assert summary["result"] == "passed"
    assert summary["authorization"].startswith("PASS: the manifested 0.6.1 candidate is authorized")
    assert summary["failed_scenarios"] == []
    assert summary["policy_shape_defects"] == []


def test_failed_consumer_cleanup_scenario_blocks_publication(tmp_path: Path) -> None:
    summary = _aggregate(tmp_path, failed={"actionable-schema-diagnostics": "Blocked by #471."})

    assert summary["result"] == "failed"
    assert summary["authorization"].startswith("FAIL: the manifested 0.6.1 candidate is NOT authorized")
    assert [failure["id"] for failure in summary["failed_scenarios"]] == ["actionable-schema-diagnostics"]
    assert summary["failed_scenarios"][0]["reason"] == "Blocked by #471."


def test_per_module_contract_copies_block_publication(tmp_path: Path) -> None:
    shape = dict(_POLICY_SHAPE, authored_directional_assembly_contracts=20)

    summary = _aggregate(tmp_path, policy_shape=shape)

    assert summary["result"] == "failed"
    assert any("authored per module" in defect for defect in summary["policy_shape_defects"])


def test_copied_project_inventory_blocks_publication(tmp_path: Path) -> None:
    shape = dict(_POLICY_SHAPE, declared_project_inventories=2)

    summary = _aggregate(tmp_path, policy_shape=shape)

    assert summary["result"] == "failed"
    assert any("copied instead of discovered" in defect for defect in summary["policy_shape_defects"])


def test_inline_public_api_inventory_blocks_publication(tmp_path: Path) -> None:
    shape = dict(_POLICY_SHAPE, inline_public_api_signatures=1)

    summary = _aggregate(tmp_path, policy_shape=shape)

    assert summary["result"] == "failed"
    assert any("inline YAML inventory" in defect for defect in summary["policy_shape_defects"])


def test_missing_policy_shape_is_rejected(tmp_path: Path) -> None:
    manifest_path, platforms, _ = _write_corpus(tmp_path)
    record_path = platforms / "linux-x64.json"
    record = json.loads(record_path.read_text())
    del record["policy_shape"]
    record_path.write_text(json.dumps(record))

    with pytest.raises(ValueError, match="policy-shape counters"):
        _read_records(platforms, _read_manifest(manifest_path),
                      hashlib.sha256(manifest_path.read_bytes()).hexdigest())


def test_platform_result_contradicting_its_scenarios_is_rejected(tmp_path: Path) -> None:
    manifest_path, platforms, _ = _write_corpus(tmp_path, failed={"source-set-enrolment": "Broken."})
    record_path = platforms / "linux-x64.json"
    record = json.loads(record_path.read_text())
    record["result"] = "passed"
    record_path.write_text(json.dumps(record))

    with pytest.raises(ValueError, match="contradicts its own scenario results"):
        _read_records(platforms, _read_manifest(manifest_path),
                      hashlib.sha256(manifest_path.read_bytes()).hexdigest())
