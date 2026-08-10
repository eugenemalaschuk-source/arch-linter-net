from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import aggregate_checkpoint_b_evidence as aggregator  # noqa: E402
from aggregate_checkpoint_b_evidence import (  # noqa: E402
    _CONSUMER_CLEANUP_SCENARIOS,
    _REQUIRED_PLATFORMS,
    _REQUIRED_SCENARIOS,
    _read_gates,
    _read_manifest,
    _read_records,
    _read_release_scope,
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


def _release_scope(digest: str, open_items: set[int] | None = None) -> dict:
    open_items = open_items or set()
    return {
        "schema": "checkpoint-b-release-scope/v1",
        "release_target": "0.6.1",
        "story": 434,
        "repository": "owner/repo",
        "source_commit": _COMMIT,
        "candidate_manifest_sha256": digest,
        "required_items": [
            {"issue": number, "finding": f"F{index + 1}", "summary": f"Item {number}",
             "state": "open" if number in open_items else "closed"}
            for index, number in enumerate((435, 436, 466))
        ],
        "excluded_items": [{"issue": 450, "reason": "Post-release refactoring story."}],
    }


def _write_corpus(
    tmp_path: Path,
    failed: dict[str, str] | None = None,
    policy_shape: dict | None = None,
    open_scope_items: set[int] | None = None,
) -> tuple[Path, Path, Path, Path]:
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
    scope_path = tmp_path / "release-scope.json"
    scope_path.write_text(json.dumps(_release_scope(digest, open_scope_items)))
    return manifest_path, platforms, gates_path, scope_path


def _aggregate(tmp_path: Path, **kwargs) -> dict:
    manifest_path, platforms, gates_path, scope_path = _write_corpus(tmp_path, **kwargs)
    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    records = _read_records(platforms, manifest, digest)
    gates = _read_gates(gates_path, manifest, digest)
    scope = _read_release_scope(scope_path, manifest, digest)
    return _summary(records, manifest, gates, scope, digest)


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
    manifest_path, platforms, _, _ = _write_corpus(tmp_path)
    record_path = platforms / "linux-x64.json"
    record = json.loads(record_path.read_text())
    del record["policy_shape"]
    record_path.write_text(json.dumps(record))

    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match="policy-shape counters"):
        _read_records(platforms, manifest, digest)


def test_platform_result_contradicting_its_scenarios_is_rejected(tmp_path: Path) -> None:
    manifest_path, platforms, _, _ = _write_corpus(tmp_path, failed={"source-set-enrolment": "Broken."})
    record_path = platforms / "linux-x64.json"
    record = json.loads(record_path.read_text())
    record["result"] = "passed"
    record_path.write_text(json.dumps(record))

    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match="contradicts its own scenario results"):
        _read_records(platforms, manifest, digest)


def _run_main(tmp_path: Path, **kwargs) -> tuple[int, dict, str]:
    manifest_path, platforms, gates_path, scope_path = _write_corpus(tmp_path, **kwargs)
    output = tmp_path / "release-evidence"
    argv = [
        "aggregate_checkpoint_b_evidence.py",
        "--input-dir", str(platforms),
        "--candidate-manifest", str(manifest_path),
        "--repository-gates", str(gates_path),
        "--release-scope", str(scope_path),
        "--output-dir", str(output),
    ]
    original = sys.argv
    sys.argv = argv
    try:
        exit_code = aggregator.main()
    finally:
        sys.argv = original
    summary = json.loads((output / "checkpoint-b-release-evidence.json").read_text())
    markdown = (output / "checkpoint-b-release-evidence.md").read_text()
    return exit_code, summary, markdown


def test_main_writes_pass_evidence_and_succeeds(tmp_path: Path) -> None:
    exit_code, summary, markdown = _run_main(tmp_path)

    assert exit_code == 0
    assert summary["result"] == "passed"
    assert "PASS: the manifested 0.6.1 candidate is authorized for publication." in markdown
    assert "Composed policy documents: 5 (4 imported fragments)" in markdown
    assert "Copied project inventories: 0" in markdown
    assert markdown.count("| passed |") == len(_REQUIRED_PLATFORMS)
    assert "Failed required scenarios" not in markdown


def test_main_reports_fail_and_terminates_unsuccessfully(tmp_path: Path) -> None:
    exit_code, summary, markdown = _run_main(
        tmp_path, failed={"actionable-schema-diagnostics": "Blocked by #471."})

    assert exit_code == 1
    assert summary["result"] == "failed"
    assert "NOT authorized for publication" in markdown
    assert "## Failed required scenarios" in markdown
    assert "| `actionable-schema-diagnostics` | linux-x64 | Blocked by #471. |" in markdown


def test_main_reports_policy_shape_defects(tmp_path: Path) -> None:
    exit_code, summary, markdown = _run_main(
        tmp_path, policy_shape=dict(_POLICY_SHAPE, imported_fragments=0))

    assert exit_code == 1
    assert summary["policy_shape_defects"]
    assert "## Consumer policy-shape defects" in markdown
    assert "forced monolith" in markdown


def test_scenario_missing_everywhere_blocks_publication(tmp_path: Path) -> None:
    manifest_path, platforms, gates_path, scope_path = _write_corpus(tmp_path)
    for record_path in platforms.iterdir():
        record = json.loads(record_path.read_text())
        for scenario in record["scenarios"]:
            if scenario["id"] == "source-set-enrolment":
                scenario["result"] = "not_applicable"
                scenario["reason"] = "Skipped everywhere."
        record_path.write_text(json.dumps(record))
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    manifest = _read_manifest(manifest_path)

    summary = _summary(
        _read_records(platforms, manifest, digest),
        manifest,
        _read_gates(gates_path, manifest, digest),
        _read_release_scope(scope_path, manifest, digest),
        digest)

    assert summary["result"] == "failed"
    assert [failure["id"] for failure in summary["failed_scenarios"]] == ["source-set-enrolment"]
    assert summary["failed_scenarios"][0]["platform_id"] is None


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda record: record.update(schema="other/v1"), "supported evidence schema"),
        (lambda record: record.update(checkpoint="A"), "packed-artifact gate result"),
        (lambda record: record.update(synthetic_identities_only=False), "synthetic identities"),
        (lambda record: record.update(candidate_version="9.9.9"), "candidate version differs"),
        (lambda record: record.update(source_commit="c" * 40), "source commit differs"),
        (lambda record: record.update(candidate_manifest_sha256="d" * 64), "candidate manifest digest"),
        (lambda record: record.update(packages=[]), "package inventory differs"),
        (lambda record: record["scenarios"].pop(), "incomplete scenario inventory"),
        (lambda record: record["scenarios"].__setitem__(0, dict(record["scenarios"][1])),
         "missing, unexpected, or duplicate scenario IDs"),
        (lambda record: record["scenarios"][0].update(result="unknown"), "malformed scenario result"),
        (lambda record: record["scenarios"][0].update(result="failed", reason=None), "explain a non-passing"),
        (lambda record: record["policy_shape"].update(governed_projects="many"), "non-numeric policy-shape"),
    ],
)
def test_malformed_platform_record_is_rejected(tmp_path: Path, mutate, message: str) -> None:
    manifest_path, platforms, _, _ = _write_corpus(tmp_path)
    record_path = platforms / "linux-x64.json"
    record = json.loads(record_path.read_text())
    mutate(record)
    record_path.write_text(json.dumps(record))

    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match=message):
        _read_records(platforms, manifest, digest)


def test_platform_matrix_must_be_complete(tmp_path: Path) -> None:
    manifest_path, platforms, gates_path, scope_path = _write_corpus(tmp_path)
    (platforms / "macos-x64.json").unlink()
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    manifest = _read_manifest(manifest_path)

    records = _read_records(platforms, manifest, digest)
    gates = _read_gates(gates_path, manifest, digest)
    scope = _read_release_scope(scope_path, manifest, digest)

    with pytest.raises(ValueError, match="platform matrix mismatch"):
        _summary(records, manifest, gates, scope, digest)


def test_wrong_architecture_or_shell_is_rejected(tmp_path: Path) -> None:
    manifest_path, platforms, gates_path, scope_path = _write_corpus(tmp_path)
    record_path = platforms / "macos-arm64.json"
    record = json.loads(record_path.read_text())
    record["architecture"] = "X64"
    record_path.write_text(json.dumps(record))
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    manifest = _read_manifest(manifest_path)

    records = _read_records(platforms, manifest, digest)
    gates = _read_gates(gates_path, manifest, digest)
    scope = _read_release_scope(scope_path, manifest, digest)

    with pytest.raises(ValueError, match="wrong architecture"):
        _summary(records, manifest, gates, scope, digest)


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda gates: gates.update(schema="other/v1"), "Repository gates schema"),
        (lambda gates: gates.update(candidate_manifest_sha256="e" * 64), "not bound to the candidate"),
        (lambda gates: gates.update(source_commit="f" * 40), "source commit differs"),
        (lambda gates: gates.update(gates=[{"id": "acceptance", "result": "passed"}]), "inventory is incomplete"),
        (lambda gates: gates["gates"][0].update(result="failed"), "gate failed or is malformed"),
    ],
)
def test_malformed_repository_gates_are_rejected(tmp_path: Path, mutate, message: str) -> None:
    manifest_path, _, gates_path, _ = _write_corpus(tmp_path)
    gates = json.loads(gates_path.read_text())
    mutate(gates)
    gates_path.write_text(json.dumps(gates))

    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match=message):
        _read_gates(gates_path, manifest, digest)


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda manifest: manifest.update(schema="other/v1"), "manifest schema is invalid"),
        (lambda manifest: manifest.update(packages=[]), "package inventory is invalid"),
        (lambda manifest: manifest["packages"][0].pop("sha256"), "package record is invalid"),
    ],
)
def test_malformed_candidate_manifest_is_rejected(tmp_path: Path, mutate, message: str) -> None:
    manifest_path, _, _, _ = _write_corpus(tmp_path)
    manifest = json.loads(manifest_path.read_text())
    mutate(manifest)
    manifest_path.write_text(json.dumps(manifest))

    with pytest.raises(ValueError, match=message):
        _read_manifest(manifest_path)


def test_unreadable_input_is_reported_with_its_description(tmp_path: Path) -> None:
    broken = tmp_path / "broken.json"
    broken.write_text("{ not json")

    with pytest.raises(ValueError, match="Cannot read candidate manifest"):
        _read_manifest(broken)


def test_empty_evidence_directory_is_rejected(tmp_path: Path) -> None:
    manifest_path, platforms, _, _ = _write_corpus(tmp_path)
    for record_path in platforms.iterdir():
        record_path.unlink()

    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match="No packed-artifact gate evidence records"):
        _read_records(platforms, manifest, digest)


def test_open_release_scope_item_blocks_publication(tmp_path: Path) -> None:
    summary = _aggregate(tmp_path, open_scope_items={436})

    assert summary["result"] == "failed"
    assert summary["open_release_scope_items"] == ["#436 (F2) is open: Item 436"]
    assert summary["authorization"].startswith("FAIL")


def test_release_scope_inventory_is_emitted_in_the_evidence(tmp_path: Path) -> None:
    exit_code, summary, markdown = _run_main(tmp_path)

    assert exit_code == 0
    assert summary["release_scope"]["story"] == 434
    assert [item["issue"] for item in summary["release_scope"]["required_items"]] == [435, 436, 466]
    assert "## Release scope (story #434, target 0.6.1)" in markdown
    assert "| #435 | F1 | closed | Item 435 |" in markdown
    assert "- #450 — Post-release refactoring story." in markdown


def test_open_release_scope_item_is_listed_in_the_markdown(tmp_path: Path) -> None:
    exit_code, _, markdown = _run_main(tmp_path, open_scope_items={466})

    assert exit_code == 1
    assert "| #466 | F3 | open | Item 466 |" in markdown


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda scope: scope.update(schema="other/v1"), "Release-scope schema"),
        (lambda scope: scope.update(candidate_manifest_sha256="e" * 64), "not bound to the candidate"),
        (lambda scope: scope.update(source_commit="f" * 40), "source commit differs"),
        (lambda scope: scope.update(required_items=[]), "declares no required items"),
        (lambda scope: scope["required_items"][0].update(issue="435"), "item is malformed"),
        (lambda scope: scope["required_items"][0].update(state="unknown"), "no resolved state"),
    ],
)
def test_malformed_release_scope_is_rejected(tmp_path: Path, mutate, message: str) -> None:
    manifest_path, _, _, scope_path = _write_corpus(tmp_path)
    scope = json.loads(scope_path.read_text())
    mutate(scope)
    scope_path.write_text(json.dumps(scope))
    manifest = _read_manifest(manifest_path)
    digest = hashlib.sha256(manifest_path.read_bytes()).hexdigest()

    with pytest.raises(ValueError, match=message):
        _read_release_scope(scope_path, manifest, digest)
