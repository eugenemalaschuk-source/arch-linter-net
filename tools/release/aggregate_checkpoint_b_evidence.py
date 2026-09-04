#!/usr/bin/env python3
"""Strictly aggregate packed-artifact release evidence for one immutable candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

from _release_workspace import _safe_path
from package_manifest import _load_manifest as _load_candidate_manifest
from create_release_scope_evidence import _declarations_directory, _select_declaration

_EVIDENCE_SCHEMA = "checkpoint-b-platform-evidence/v1"
_GATES_SCHEMA = "checkpoint-b-repository-gates/v1"
_MANIFEST_SCHEMA = "checkpoint-b-candidate-manifest/v2"
_RELEASE_SCOPE_SCHEMA = "checkpoint-b-release-scope/v2"
_RELEASE_SCOPE_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_RELEASE_SCOPE_DECLARATION_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
_REQUIRED_PLATFORMS = {
    "linux-x64": ("x64", "bash"),
    "macos-arm64": ("arm64", "zsh"),
    "macos-x64": ("x64", "zsh"),
    "windows-x64": ("x64", "pwsh"),
}
# Platform, packaging, and execution-mode scenarios established by the 0.6.0 Checkpoint B gate.
_PLATFORM_SCENARIOS = {
    "cache-corruption-recompute",
    "cache-miss-population-hit",
    "clean-checkout",
    "documented-entrypoints",
    "external-testing-consumer",
    "generic-ci-neutral",
    "in-flight-cancellation",
    "installed-testing-output-ensure-built",
    "non-tty",
    "offline-schema-registry",
    "packed-package-provenance",
    "posix-entrypoint",
    "powershell-entrypoint",
    "profile-generation",
    "sequential-default-parity",
}
# The 0.6.1 consumer-cleanup matrix: F1-F11 plus the #465 source-set authoring model, proven
# against the installed candidate rather than a source-tree ProjectReference.
_CONSUMER_CLEANUP_SCENARIOS = {
    "actionable-schema-diagnostics",
    "composed-policy-assembly-free-check",
    "consumer-policy-shape",
    "dependency-contract-id-parity",
    "discovered-project-set-authoring",
    "json-configuration-error-format",
    "layer-overlap-allowance",
    "missing-shared-framework-diagnostic",
    "namespace-allowance-pattern",
    "non-destructive-ensure-built",
    "packaged-testing-ensure-built",
    "public-api-snapshot-workflow",
    "release-identity-consistency",
    "source-set-assembly-authoring",
    "source-set-enrolment",
    "stale-source-selector-fail-closed",
    "strict-cycles-baseline-scope",
}
# The 0.6.4 public-API surface-selector consumer-exit matrix (#525/#526/#529): proves a modular
# consumer can replace a whole-assembly reviewed API snapshot with a materially smaller intentional
# one, selected by bounded evidence, without touching CLR visibility or existing semantic roles.
_PUBLIC_API_SURFACE_SELECTOR_SCENARIOS = {
    "public-api-surface-selector-snapshot-reduction",
    "public-api-surface-selector-role-preservation",
    "public-api-surface-selector-exact-delta-lifecycle",
    "public-api-surface-selector-membership-review-visibility",
    "public-api-surface-selector-escape-fails-closed",
    "public-api-surface-selector-strict-run-is-green",
    "public-api-surface-selector-testing-parity",
}
# The v0.8 full-cycle matrix (#524): proves the entire documented single-tool workflow -- policy
# check, strict validate with real contract-surface-exposure and metric-budget violations,
# recursive exposure evidence, declared topology capture/diff/verify, measure, policy
# weakening/gate, required external SARIF evidence binding, change snapshot/report, the 5-state
# Architecture Health matrix, PR report, badge, and cross-projection parity -- against a
# synthetic fixture genuinely external to ArchLinterNet itself.
_V08_FULL_CYCLE_SCENARIOS = {
    "v08-policy-check",
    "v08-validate-strict-audit",
    "v08-recursive-exposure-evidence",
    "v08-topology-capture-diff-verify",
    "v08-topology-unmapped",
    "v08-measure-budget",
    "v08-policy-weakening-gate",
    "v08-external-evidence-binding",
    "v08-change-snapshot-report",
    "v08-health-matrix",
    "v08-health-degrading-advisory",
    "v08-report-pr",
    "v08-badge",
    "v08-projection-parity",
    "v08-unity-topology-review",
    "v08-unity-editor-exposure-rejection",
    "v08-unity-health-report-routing",
}
_REQUIRED_SCENARIOS = (
    _PLATFORM_SCENARIOS
    | _CONSUMER_CLEANUP_SCENARIOS
    | _PUBLIC_API_SURFACE_SELECTOR_SCENARIOS
    | _V08_FULL_CYCLE_SCENARIOS
)
_SCENARIO_RESULTS = {"passed", "not_applicable", "failed"}
_REQUIRED_GATES = {"acceptance", "openspec_strict"}
# Counters the consumer policy must report so the gate can reject a candidate whose canonical
# consumer path still needs a workaround shape this release exists to remove.
_POLICY_SHAPE_FIELDS = {
    "policy_documents",
    "imported_fragments",
    "governed_module_assemblies",
    "authored_directional_assembly_contracts",
    "expanded_directional_assembly_instances",
    "governed_projects",
    "authored_project_metadata_contracts",
    "declared_project_inventories",
    "inline_public_api_signatures",
}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    # Every caller passes a path already confined by _safe_path (the candidate manifest)
    # or discovered via rglob() strictly under a _safe_path-confined directory, so this open cannot
    # escape the release workspace. Sonar's Python taint tracker does not recognize a cross-module
    # call as a sanitizer; see the identical rationale in merge_checkpoint_b_platform_evidence.py.
    with path.open("rb") as source:  # NOSONAR(S2083,S8707)
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json(path: Path, description: str) -> dict[str, Any]:
    try:
        # Every caller passes a path already confined by _safe_path or discovered via
        # rglob() strictly under a _safe_path-confined directory; see the identical rationale in
        # merge_checkpoint_b_platform_evidence.py.
        value = json.loads(path.read_text(encoding="utf-8"))  # NOSONAR(S2083,S8707)
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read {description} '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{description} '{path}' must be a JSON object.")
    return value


def _read_manifest(path: Path) -> dict[str, Any]:
    raw_manifest = _load_json(path, "candidate manifest")
    if raw_manifest.get("schema") != _MANIFEST_SCHEMA:
        raise ValueError("Candidate manifest schema is invalid.")
    return _load_candidate_manifest(path)


def _read_policy_shape(path: Path, record: dict[str, Any]) -> dict[str, Any]:
    shape = record.get("policy_shape")
    if not isinstance(shape, dict) or set(shape) != _POLICY_SHAPE_FIELDS:
        raise ValueError(f"{path} does not report the required consumer policy-shape counters.")
    if any(not isinstance(value, int) for value in shape.values()):
        raise ValueError(f"{path} reports a non-numeric policy-shape counter.")
    return shape


def _read_records(input_directory: Path, manifest: dict[str, Any], manifest_digest: str) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for path in sorted(input_directory.rglob("*.json")):
        record = _load_json(path, "platform evidence")
        if record.get("schema") != _EVIDENCE_SCHEMA:
            raise ValueError(f"{path} does not use the supported evidence schema.")
        if record.get("checkpoint") != "B" or record.get("result") not in {"passed", "failed"}:
            raise ValueError(f"{path} does not report a packed-artifact gate result.")
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
        scenarios = record.get("scenarios")
        if not isinstance(scenarios, list) or len(scenarios) != len(_REQUIRED_SCENARIOS):
            raise ValueError(f"{path} has an incomplete scenario inventory.")
        scenario_ids = [scenario.get("id") for scenario in scenarios if isinstance(scenario, dict)]
        if len(scenario_ids) != len(scenarios) or set(scenario_ids) != _REQUIRED_SCENARIOS:
            raise ValueError(f"{path} has missing, unexpected, or duplicate scenario IDs.")
        for scenario in scenarios:
            if not isinstance(scenario, dict) or scenario.get("result") not in _SCENARIO_RESULTS:
                raise ValueError(f"{path} contains a malformed scenario result.")
            if scenario["result"] != "passed" and not isinstance(scenario.get("reason"), str):
                raise ValueError(f"{path} does not explain a non-passing scenario.")
        declared_failed = any(scenario["result"] == "failed" for scenario in scenarios)
        if declared_failed != (record["result"] == "failed"):
            raise ValueError(f"{path} platform result contradicts its own scenario results.")
        _read_policy_shape(path, record)
        records.append(record)
    if not records:
        raise ValueError("No packed-artifact gate evidence records were found.")
    return records


def _validate_platforms(records: list[dict[str, Any]]) -> None:
    by_platform: dict[str, list[dict[str, Any]]] = {}
    for record in records:
        platform = record.get("platform_id")
        if not isinstance(platform, str):
            raise ValueError("Platform evidence record has no platform_id.")
        by_platform.setdefault(platform, []).append(record)

    if set(by_platform) != set(_REQUIRED_PLATFORMS):
        raise ValueError(f"Release-gate platform matrix mismatch: {sorted(by_platform)}.")
    for platform, (architecture, shell) in _REQUIRED_PLATFORMS.items():
        records_for_platform = by_platform[platform]
        if len(records_for_platform) != 1:
            raise ValueError(f"Expected exactly one evidence record for {platform}.")
        record = records_for_platform[0]
        if str(record.get("architecture", "")).lower() != architecture:
            raise ValueError(f"{platform} evidence reports a wrong architecture.")
        if record.get("shell") != shell:
            raise ValueError(f"{platform} evidence reports a wrong shell adapter.")


def _failed_scenarios(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Every scenario that failed anywhere, plus every scenario no platform ever passed."""
    failures: dict[str, dict[str, Any]] = {}
    for record in records:
        for scenario in record["scenarios"]:
            if scenario["result"] == "failed":
                failures.setdefault(scenario["id"], {
                    "id": scenario["id"],
                    "platform_id": record["platform_id"],
                    "reason": scenario["reason"],
                })
    for scenario_id in sorted(_REQUIRED_SCENARIOS):
        if scenario_id in failures:
            continue
        if not any(
            scenario["id"] == scenario_id and scenario["result"] == "passed"
            for record in records
            for scenario in record["scenarios"]
        ):
            failures[scenario_id] = {
                "id": scenario_id,
                "platform_id": None,
                "reason": "No platform executed this required scenario to a passing result.",
            }
    return [failures[key] for key in sorted(failures)]


def _policy_shape_defects(records: list[dict[str, Any]]) -> list[str]:
    defects: list[str] = []
    for record in records:
        shape = record["policy_shape"]
        platform = record["platform_id"]
        if shape["imported_fragments"] < 1:
            defects.append(f"{platform}: the consumer policy is a forced monolith.")
        if shape["authored_directional_assembly_contracts"] >= shape["governed_module_assemblies"]:
            defects.append(
                f"{platform}: directional assembly contracts are still authored per module "
                f"({shape['authored_directional_assembly_contracts']} contracts for "
                f"{shape['governed_module_assemblies']} module assemblies)."
            )
        if shape["expanded_directional_assembly_instances"] < shape["governed_module_assemblies"]:
            defects.append(f"{platform}: source-set expansion does not cover every governed module assembly.")
        if shape["declared_project_inventories"] != 0:
            defects.append(f"{platform}: a project inventory is still copied instead of discovered.")
        if shape["inline_public_api_signatures"] != 0:
            defects.append(f"{platform}: the reviewed public API is still an inline YAML inventory.")
    return sorted(set(defects))


def _read_gates(path: Path, manifest: dict[str, Any], manifest_digest: str) -> dict[str, Any]:
    gates = _load_json(path, "repository-gates result")
    if gates.get("schema") != _GATES_SCHEMA:
        raise ValueError("Repository gates schema is invalid.")
    if gates.get("candidate_manifest_sha256") != manifest_digest:
        raise ValueError("Repository gates are not bound to the candidate manifest.")
    if gates.get("source_commit") != manifest["source_commit"]:
        raise ValueError("Repository gates source commit differs from the candidate manifest.")
    results = gates.get("gates")
    if not isinstance(results, list) or {gate.get("id") for gate in results if isinstance(gate, dict)} != _REQUIRED_GATES:
        raise ValueError("Repository gates inventory is incomplete.")
    if any(not isinstance(gate, dict) or gate.get("result") != "passed" for gate in results):
        raise ValueError("Repository gate failed or is malformed.")
    return gates


def _read_release_scope(path: Path, manifest: dict[str, Any], manifest_digest: str) -> dict[str, Any]:
    """Read the candidate-selected release-scope inventory and verify every binding."""
    scope = _load_json(path, "release-scope inventory")
    _validate_release_scope_identity(scope, manifest)

    declaration_path, declaration = _select_declaration(_declarations_directory(), manifest["version"])
    _validate_release_scope_against_declaration(scope, manifest, manifest_digest, declaration_path, declaration)

    required = scope.get("required_items")
    inventory_numbers = _validate_required_items(required)
    _validate_optional_inventories(scope, inventory_numbers)
    _validate_scope_matches_declaration(scope, required, declaration)
    return scope


def _validate_release_scope_identity(scope: dict[str, Any], manifest: dict[str, Any]) -> None:
    if scope.get("schema") != _RELEASE_SCOPE_SCHEMA:
        raise ValueError("Release-scope schema is invalid.")
    if scope.get("candidate_version") != manifest["version"]:
        raise ValueError("Release scope candidate version differs from the candidate manifest.")
    if scope.get("release_target") != manifest["version"]:
        raise ValueError("Release scope target differs from the candidate manifest version.")
    if not isinstance(scope.get("declaration_id"), str) or not _RELEASE_SCOPE_DECLARATION_ID_PATTERN.fullmatch(
        scope["declaration_id"]
    ):
        raise ValueError("Release scope declaration identity is invalid.")
    if not isinstance(scope.get("declaration_sha256"), str) or not _RELEASE_SCOPE_SHA256_PATTERN.fullmatch(
        scope["declaration_sha256"]
    ):
        raise ValueError("Release scope declaration hash is invalid.")


def _validate_release_scope_against_declaration(
    scope: dict[str, Any],
    manifest: dict[str, Any],
    manifest_digest: str,
    declaration_path: Path,
    declaration: dict[str, Any],
) -> None:
    expected_declaration_sha256 = _sha256(declaration_path)
    if scope["declaration_sha256"] != expected_declaration_sha256:
        raise ValueError("Release scope declaration hash does not match the tracked declaration.")
    if scope["declaration_id"] != declaration["declaration_id"]:
        raise ValueError("Release scope declaration identity does not match the tracked declaration.")
    if scope.get("story") != declaration["story"]:
        raise ValueError("Release scope authority story does not match the tracked declaration.")
    if scope.get("candidate_manifest_sha256") != manifest_digest:
        raise ValueError("Release scope is not bound to the candidate manifest.")
    if scope.get("source_commit") != manifest["source_commit"]:
        raise ValueError("Release scope source commit differs from the candidate manifest.")
    if not isinstance(scope.get("story"), int) or isinstance(scope["story"], bool) or scope["story"] <= 0:
        raise ValueError("Release scope authority story is invalid.")


def _validate_required_items(required: Any) -> set[int]:
    if not isinstance(required, list) or not required:
        raise ValueError("Release scope declares no required items.")
    for item in required:
        if (
            not isinstance(item, dict)
            or not isinstance(item.get("issue"), int)
            or isinstance(item["issue"], bool)
            or item["issue"] <= 0
        ):
            raise ValueError("Release-scope item is malformed.")
        if item.get("state") not in {"open", "closed"}:
            raise ValueError(f"Release-scope item #{item.get('issue')} has no resolved state.")
    inventory_numbers = {item["issue"] for item in required}
    if len(inventory_numbers) != len(required):
        raise ValueError("Release scope repeats a required item.")
    return inventory_numbers


def _validate_optional_inventories(scope: dict[str, Any], inventory_numbers: set[int]) -> None:
    for inventory_name in ("excluded_items", "delivered_items"):
        inventory = scope.get(inventory_name)
        if not isinstance(inventory, list):
            raise ValueError(f"Release scope {inventory_name.replace('_', ' ')} is malformed.")
        for item in inventory:
            if (
                not isinstance(item, dict)
                or not isinstance(item.get("issue"), int)
                or isinstance(item["issue"], bool)
                or item["issue"] <= 0
                or not isinstance(item.get("reason"), str)
                or not item["reason"].strip()
                or item["issue"] in inventory_numbers
            ):
                raise ValueError(f"Release scope {inventory_name.replace('_', ' ')} is malformed.")
            inventory_numbers.add(item["issue"])


def _validate_scope_matches_declaration(
    scope: dict[str, Any],
    required: list[dict[str, Any]],
    declaration: dict[str, Any],
) -> None:
    required_inventory = [
        {key: value for key, value in item.items() if key not in {"state", "title"}}
        for item in required
    ]
    if required_inventory != declaration["required_items"]:
        raise ValueError("Release scope required inventory does not match the tracked declaration.")
    for inventory_name in ("excluded_items", "delivered_items"):
        if scope[inventory_name] != declaration[inventory_name]:
            raise ValueError(
                f"Release scope {inventory_name.replace('_', ' ')} does not match the tracked declaration."
            )


def _release_scope_defects(scope: dict[str, Any]) -> list[str]:
    """A candidate cannot be authorized while a selected required item is open."""
    return [
        f"#{item['issue']} ({item.get('finding', 'release scope')}) is {item['state']}: "
        f"{item.get('summary') or item.get('title', '')}".strip()
        for item in sorted(scope["required_items"], key=lambda item: item["issue"])
        if item["state"] != "closed"
    ]


def _summary(
    records: list[dict[str, Any]],
    manifest: dict[str, Any],
    gates: dict[str, Any],
    scope: dict[str, Any],
    manifest_digest: str,
) -> dict[str, Any]:
    _validate_platforms(records)
    failures = _failed_scenarios(records)
    defects = _policy_shape_defects(records)
    open_scope = _release_scope_defects(scope)
    version = manifest["version"]
    passed = not failures and not defects and not open_scope
    return {
        "schema": "checkpoint-b-release-evidence/v1",
        "checkpoint": "B",
        "result": "passed" if passed else "failed",
        "authorization": (
            f"PASS: the manifested {version} candidate is authorized for publication."
            if passed
            else f"FAIL: the manifested {version} candidate is NOT authorized for publication."
        ),
        "candidate_version": version,
        "source_commit": manifest["source_commit"],
        "candidate_manifest_sha256": manifest_digest,
        "synthetic_identities_only": True,
        "packages": manifest["packages"],
        "required_scenarios": sorted(_REQUIRED_SCENARIOS),
        "consumer_cleanup_scenarios": sorted(_CONSUMER_CLEANUP_SCENARIOS),
        "failed_scenarios": failures,
        "policy_shape_defects": defects,
        "open_release_scope_items": open_scope,
        "release_scope": scope,
        "platforms": sorted(records, key=lambda record: str(record["platform_id"])),
        "repository_gates": gates["gates"],
    }


def _markdown(summary: dict[str, Any]) -> str:
    platform_rows = ["| {platform_id} | {runtime} | {shell} | {result} |".format(**record) for record in summary["platforms"]]
    shape = summary["platforms"][0]["policy_shape"]
    failure_rows = [
        f"| `{failure['id']}` | {failure['platform_id'] or 'all'} | {failure['reason']} |"
        for failure in summary["failed_scenarios"]
    ]
    failure_section = [
        "## Failed required scenarios",
        "",
        "| Scenario | Platform | Reason |",
        "| --- | --- | --- |",
        *failure_rows,
        "",
    ] if failure_rows else []
    defect_section = [
        "## Consumer policy-shape defects",
        "",
        *[f"- {defect}" for defect in summary["policy_shape_defects"]],
        "",
    ] if summary["policy_shape_defects"] else []
    scope = summary["release_scope"]
    scope_section = [
        f"## Release scope (story #{scope['story']}, target {scope['release_target']})",
        "",
        f"- Declaration: `{scope['declaration_id']}`",
        f"- Declaration SHA-256: `{scope['declaration_sha256']}`",
        f"- Candidate version: `{scope['candidate_version']}`",
        "",
        "| Item | Finding | State | Summary |",
        "| --- | --- | --- | --- |",
        *[
            "| #{issue} | {finding} | {state} | {summary} |".format(
                issue=item["issue"],
                finding=item.get("finding", ""),
                state=item["state"],
                summary=item.get("summary") or item.get("title", ""))
            for item in sorted(scope["required_items"], key=lambda item: item["issue"])
        ],
        "",
        *([
            "Excluded from the release scope:",
            "",
            *[f"- #{item['issue']} — {item['reason']}" for item in scope.get("excluded_items", [])],
            "",
        ] if scope.get("excluded_items") else []),
        *([
            "Delivered release context:",
            "",
            *[f"- #{item['issue']} — {item['reason']}" for item in scope.get("delivered_items", [])],
            "",
        ] if scope.get("delivered_items") else []),
    ]
    return "\n".join([
        "# Packed-artifact release evidence",
        "",
        f"- Candidate version: `{summary['candidate_version']}`",
        f"- Tested commit: `{summary['source_commit']}`",
        f"- Candidate manifest SHA-256: `{summary['candidate_manifest_sha256']}`",
        f"- Result: **{summary['result']}**",
        f"- Release authorization: {summary['authorization']}",
        "- Private adopter identity: none; all fixtures and evidence are synthetic.",
        "",
        "## Consumer policy shape",
        "",
        f"- Composed policy documents: {shape['policy_documents']} ({shape['imported_fragments']} imported fragments)",
        f"- Module assemblies governed by {shape['authored_directional_assembly_contracts']} authored directional "
        f"assembly contracts: {shape['governed_module_assemblies']} "
        f"({shape['expanded_directional_assembly_instances']} expanded instances)",
        f"- Projects governed by {shape['authored_project_metadata_contracts']} project-metadata contracts through "
        f"solution discovery: {shape['governed_projects']}",
        f"- Copied project inventories: {shape['declared_project_inventories']}",
        f"- Inline public API signatures: {shape['inline_public_api_signatures']}",
        "",
        *failure_section,
        *defect_section,
        *scope_section,
        "| Platform | Runtime | Shell | Result |",
        "| --- | --- | --- | --- |",
        *platform_rows,
        "",
    ])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--repository-gates", type=Path, required=True)
    parser.add_argument("--release-scope", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    arguments = parser.parse_args()

    candidate_manifest = _safe_path(arguments.candidate_manifest, "candidate manifest")
    input_dir = _safe_path(arguments.input_dir, "input directory")
    repository_gates = _safe_path(arguments.repository_gates, "repository-gates result")
    release_scope = _safe_path(arguments.release_scope, "release-scope inventory")
    output_dir = _safe_path(arguments.output_dir, "output directory")

    manifest = _read_manifest(candidate_manifest)
    manifest_digest = _sha256(candidate_manifest)
    records = _read_records(input_dir, manifest, manifest_digest)
    gates = _read_gates(repository_gates, manifest, manifest_digest)
    scope = _read_release_scope(release_scope, manifest, manifest_digest)
    summary = _summary(records, manifest, gates, scope, manifest_digest)
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "checkpoint-b-release-evidence.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    (output_dir / "checkpoint-b-release-evidence.md").write_text(
        _markdown(summary), encoding="utf-8"
    )
    if summary["result"] != "passed":
        print(summary["authorization"])
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
