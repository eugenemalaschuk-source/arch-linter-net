#!/usr/bin/env python3
"""Strictly aggregate Checkpoint B evidence for one immutable candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


_EVIDENCE_SCHEMA = "checkpoint-b-platform-evidence/v1"
_GATES_SCHEMA = "checkpoint-b-repository-gates/v1"
_MANIFEST_SCHEMA = "checkpoint-b-candidate-manifest/v1"
_REQUIRED_PLATFORMS = {
    "linux-x64": ("x64", "bash"),
    "macos-arm64": ("arm64", "zsh"),
    "macos-x64": ("x64", "zsh"),
    "windows-x64": ("x64", "pwsh"),
}
_REQUIRED_SCENARIOS = {
    "cache-corruption-recompute",
    "cache-miss-population-hit",
    "clean-checkout",
    "documented-entrypoints",
    "external-testing-consumer",
    "generic-ci-neutral",
    "in-flight-cancellation",
    "non-tty",
    "offline-schema-registry",
    "packed-package-provenance",
    "posix-entrypoint",
    "powershell-entrypoint",
    "profile-generation",
    "sequential-default-parity",
}
_REQUIRED_GATES = {"acceptance", "openspec_strict"}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json(path: Path, description: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read {description} '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{description} '{path}' must be a JSON object.")
    return value


def _read_manifest(path: Path) -> dict[str, Any]:
    manifest = _load_json(path, "candidate manifest")
    if manifest.get("schema") != _MANIFEST_SCHEMA:
        raise ValueError("Candidate manifest schema is invalid.")
    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != 4:
        raise ValueError("Candidate manifest package inventory is invalid.")
    required_fields = {"id", "version", "file", "size", "sha256"}
    if any(not isinstance(package, dict) or set(package) != required_fields for package in packages):
        raise ValueError("Candidate manifest package record is invalid.")
    return manifest


def _read_records(input_directory: Path, manifest: dict[str, Any], manifest_digest: str) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for path in sorted(input_directory.rglob("*.json")):
        record = _load_json(path, "platform evidence")
        if record.get("schema") != _EVIDENCE_SCHEMA:
            raise ValueError(f"{path} does not use the supported evidence schema.")
        if record.get("checkpoint") != "B" or record.get("result") != "passed":
            raise ValueError(f"{path} does not report a passed Checkpoint B result.")
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
            if not isinstance(scenario, dict) or scenario.get("result") not in {"passed", "not_applicable"}:
                raise ValueError(f"{path} contains a failed or malformed scenario result.")
            if scenario["result"] == "not_applicable" and not isinstance(scenario.get("reason"), str):
                raise ValueError(f"{path} does not explain a non-applicable scenario.")
        records.append(record)
    if not records:
        raise ValueError("No Checkpoint B evidence records were found.")
    return records


def _validate_platforms(records: list[dict[str, Any]]) -> None:
    by_platform: dict[str, list[dict[str, Any]]] = {}
    for record in records:
        platform = record.get("platform_id")
        if not isinstance(platform, str):
            raise ValueError("Platform evidence record has no platform_id.")
        by_platform.setdefault(platform, []).append(record)

    if set(by_platform) != set(_REQUIRED_PLATFORMS):
        raise ValueError(f"Checkpoint B platform matrix mismatch: {sorted(by_platform)}.")
    for platform, (architecture, shell) in _REQUIRED_PLATFORMS.items():
        records_for_platform = by_platform[platform]
        if len(records_for_platform) != 1:
            raise ValueError(f"Expected exactly one evidence record for {platform}.")
        record = records_for_platform[0]
        if str(record.get("architecture", "")).lower() != architecture:
            raise ValueError(f"{platform} evidence reports a wrong architecture.")
        if record.get("shell") != shell:
            raise ValueError(f"{platform} evidence reports a wrong shell adapter.")
    for scenario_id in _REQUIRED_SCENARIOS:
        if not any(
            scenario.get("id") == scenario_id and scenario.get("result") == "passed"
            for record in records
            for scenario in record["scenarios"]
        ):
            raise ValueError(f"No platform passed required scenario '{scenario_id}'.")


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


def _summary(records: list[dict[str, Any]], manifest: dict[str, Any], gates: dict[str, Any], manifest_digest: str) -> dict[str, Any]:
    _validate_platforms(records)
    return {
        "schema": "checkpoint-b-release-evidence/v1",
        "checkpoint": "B",
        "result": "passed",
        "authorization": "The manifested candidate is authorized for publication.",
        "candidate_version": manifest["version"],
        "source_commit": manifest["source_commit"],
        "candidate_manifest_sha256": manifest_digest,
        "synthetic_identities_only": True,
        "packages": manifest["packages"],
        "required_scenarios": sorted(_REQUIRED_SCENARIOS),
        "platforms": sorted(records, key=lambda record: str(record["platform_id"])),
        "repository_gates": gates["gates"],
    }


def _markdown(summary: dict[str, Any]) -> str:
    rows = ["| {platform_id} | {runtime} | {shell} | passed |".format(**record) for record in summary["platforms"]]
    return "\n".join([
        "# Checkpoint B release evidence",
        "",
        f"- Candidate version: `{summary['candidate_version']}`",
        f"- Tested commit: `{summary['source_commit']}`",
        f"- Candidate manifest SHA-256: `{summary['candidate_manifest_sha256']}`",
        "- Checkpoint B: **passed**",
        "- Release authorization: the manifested candidate is authorized for publication.",
        "- Private adopter identity: none; all fixtures and evidence are synthetic.",
        "",
        "| Platform | Runtime | Shell | Result |",
        "| --- | --- | --- | --- |",
        *rows,
        "",
    ])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--candidate-manifest", type=Path, required=True)
    parser.add_argument("--repository-gates", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    arguments = parser.parse_args()

    manifest = _read_manifest(arguments.candidate_manifest)
    manifest_digest = _sha256(arguments.candidate_manifest)
    records = _read_records(arguments.input_dir, manifest, manifest_digest)
    gates = _read_gates(arguments.repository_gates, manifest, manifest_digest)
    summary = _summary(records, manifest, gates, manifest_digest)
    arguments.output_dir.mkdir(parents=True, exist_ok=True)
    (arguments.output_dir / "checkpoint-b-release-evidence.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    (arguments.output_dir / "checkpoint-b-release-evidence.md").write_text(
        _markdown(summary), encoding="utf-8"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
