#!/usr/bin/env python3
"""Generate and verify the candidate-bound release history-forensics bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import package_manifest
from release_forensics_analysis import ReleaseForensicsError, analyze
from release_forensics_transport import (
    BUNDLE_SCHEMA,
    verify_bundle as _verify_bundle,
    write_summary as _write_summary,
)
from resolve_release_history_range import (
    ReleaseVersion,
    ReleaseHistoryRange,
    resolve_release_history_range,
)


_NOT_APPLICABLE_SCHEMA = "release-forensics-not-applicable/v1"
_OBSERVATIONS_SCHEMA = "release-forensics-operational-observations/v1"
_CLI_PACKAGE_ID = "ArchLinterNet.Cli"
_REPORT_JSON = "release-forensics.json"
_REPORT_MARKDOWN = "release-forensics.md"
_REPORT_MANIFEST = "release-forensics-manifest.json"
_REPORT_OBSERVATIONS = "release-forensics-observations.json"
_REPORT_CHECKSUMS = "release-forensics-checksums.txt"
_BUNDLE_FILES = frozenset(
    {_REPORT_JSON, _REPORT_MARKDOWN, _REPORT_MANIFEST, _REPORT_OBSERVATIONS, _REPORT_CHECKSUMS}
)


@dataclass(frozen=True)
class ReleaseForensicsPaths:
    """Workspace-relative paths used by the runner and injectable by unit tests."""

    repository: Path
    package_directory: Path
    tool_directory: Path
    bundle_directory: Path
    policy_path: Path


def _workspace_paths(workspace: Path) -> ReleaseForensicsPaths:
    root = workspace.resolve(strict=True)
    return ReleaseForensicsPaths(
        repository=root,
        package_directory=root / "artifacts" / "candidate",
        tool_directory=root / "artifacts" / "forensics-tool",
        bundle_directory=root / "artifacts" / "release-forensics",
        policy_path=root / "architecture" / "dependencies.arch.yml",
    )


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def _sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as content:
        for block in iter(lambda: content.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _canonical_json(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _bundle_file(bundle_directory: Path, name: str) -> Path:
    if name not in _BUNDLE_FILES:
        raise ReleaseForensicsError("Release-forensics output filename is not part of the fixed bundle inventory.")
    root = bundle_directory.resolve(strict=True)
    destination = (root / name).resolve(strict=False)
    if destination.parent != root or destination.name != name:
        raise ReleaseForensicsError("Release-forensics output path escaped the bundle directory.")
    return destination


def _write_json(bundle_directory: Path, name: str, value: Any) -> None:
    destination = _bundle_file(bundle_directory, name)
    payload = _canonical_json(value)
    with destination.open("wb") as output:
        output.write(payload)


def _verify_candidate_package(
    package_directory: Path, candidate_version: str, candidate_sha: str
) -> tuple[dict[str, Any], str]:
    manifest_path = package_directory / "package-manifest.json"
    manifest = package_manifest._load_manifest(manifest_path)  # noqa: SLF001
    if manifest["version"] != candidate_version or manifest["source_commit"] != candidate_sha:
        raise ReleaseForensicsError("Candidate package manifest identity does not match the prepared candidate.")
    package_manifest._verify_inventory(package_directory, manifest)  # noqa: SLF001
    cli_records = [record for record in manifest["packages"] if record.get("id") == _CLI_PACKAGE_ID]
    if len(cli_records) != 1:
        raise ReleaseForensicsError(f"Candidate manifest must contain exactly one {_CLI_PACKAGE_ID} package.")
    cli = cli_records[0]
    if cli["version"] != candidate_version:
        raise ReleaseForensicsError("Candidate CLI package version does not match the prepared candidate.")
    package_file = cli["package"]["file"]
    package_path = package_directory / package_file
    if not package_path.is_file() or _sha256_file(package_path) != cli["package"]["sha256"]:
        raise ReleaseForensicsError("Candidate CLI package is missing or differs from its manifested digest.")
    return manifest, cli["package"]["sha256"]


def _run_checked(arguments: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(arguments, cwd=cwd, text=True, capture_output=True, check=False)
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise ReleaseForensicsError(
            f"Command failed with exit code {result.returncode}: {arguments[0]}\n{detail[-4000:]}"
        )
    return result


def _install_candidate_tool(
    package_directory: Path,
    tool_directory: Path,
    package_version: str,
    repository: Path,
) -> Path:
    parsed_version = ReleaseVersion.parse(package_version)
    if parsed_version is None or str(parsed_version) != package_version:
        raise ReleaseForensicsError("Candidate package version is not a supported canonical release version.")
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        raise ReleaseForensicsError("The .NET SDK is not available to install the verified candidate tool package.")
    tool_directory.mkdir(parents=True, exist_ok=True)
    _run_checked(
        [
            dotnet,
            "tool",
            "install",
            _CLI_PACKAGE_ID,
            "--tool-path",
            str(tool_directory),
            "--source",
            str(package_directory),
            "--version",
            str(parsed_version),
        ],
        repository,
    )
    command = tool_directory / "arch-linter-net"
    if not command.is_file():
        raise ReleaseForensicsError("The exact candidate tool package did not install the expected command.")
    return command


def _not_applicable_report(history_range: ReleaseHistoryRange) -> dict[str, Any]:
    return {
        "schema": _NOT_APPLICABLE_SCHEMA,
        "status": "not-applicable",
        "reason": history_range.reason,
        "candidate": {
            "version": history_range.candidate_version,
            "tag": history_range.target_tag,
            "sha": history_range.candidate_sha,
            "tree": history_range.candidate_tree,
        },
        "series": {
            "kind": history_range.series_kind,
            "identity": history_range.series_identity,
        },
        "analysis": None,
    }


def _not_applicable_markdown(history_range: ReleaseHistoryRange) -> str:
    return "\n".join(
        [
            "# Release architecture forensics",
            "",
            "**Status:** Not applicable — no previous release",
            "",
            "No history analysis ran because no eligible predecessor release exists on candidate ancestry.",
            "",
            f"- Candidate version: `{history_range.candidate_version}`",
            f"- Candidate tag: `{history_range.target_tag}`",
            f"- Candidate commit: `{history_range.candidate_sha}`",
            f"- Candidate tree: `{history_range.candidate_tree}`",
            f"- Series: `{history_range.series_identity}`",
            "",
        ]
    )


def _report_metadata(report: dict[str, Any]) -> dict[str, Any]:
    if report.get("kind") != "release-architecture-forensics":
        return {
            "schema_version": None,
            "kind": report.get("schema"),
            "history_semantics_version": None,
            "tool_version": None,
            "effective_policy_sha256": None,
        }
    config = report["analysis"]["historyAnalysisConfiguration"]
    return {
        "schema_version": report["schemaVersion"],
        "kind": report["kind"],
        "history_semantics_version": report.get("historySemanticsVersion"),
        "tool_version": report.get("toolVersion"),
        "effective_policy_sha256": _sha256_bytes(_canonical_json(config)),
    }


def _file_record(path: Path) -> dict[str, Any]:
    return {"file": path.name, "size": path.stat().st_size, "sha256": _sha256_file(path)}


def generate_bundle(arguments: argparse.Namespace, paths: ReleaseForensicsPaths) -> dict[str, Any]:
    repository = paths.repository.resolve(strict=True)
    package_directory = paths.package_directory.resolve(strict=True)
    tool_directory = paths.tool_directory.resolve()
    bundle_directory = paths.bundle_directory.resolve()
    policy_path = paths.policy_path.resolve(strict=False)
    if not policy_path.is_file():
        raise ReleaseForensicsError(f"The effective history policy input is missing: {policy_path}")
    bundle_directory.mkdir(parents=True, exist_ok=True)
    started_at = _utc_now()
    script_started = time.perf_counter()
    orchestration: dict[str, float] = {}

    tick = time.perf_counter()
    _, cli_package_sha256 = _verify_candidate_package(
        package_directory, arguments.candidate_version, arguments.candidate_sha
    )
    orchestration["candidate_package_verification_ms"] = round((time.perf_counter() - tick) * 1000, 3)

    tick = time.perf_counter()
    history_range = resolve_release_history_range(
        repository,
        arguments.candidate_version,
        arguments.target_tag,
        arguments.candidate_sha,
        arguments.candidate_tree,
    )
    orchestration["range_resolution_ms"] = round((time.perf_counter() - tick) * 1000, 3)

    tool_command: Path | None = None
    if history_range.status == "applicable":
        tick = time.perf_counter()
        tool_command = _install_candidate_tool(
            package_directory, tool_directory, arguments.candidate_version, repository
        )
        orchestration["candidate_tool_install_ms"] = round((time.perf_counter() - tick) * 1000, 3)
        report, analyzer_measurements = analyze(
            tool_command, repository, policy_path, history_range, bundle_directory
        )
        bundle_render_started = time.perf_counter()
    else:
        bundle_render_started = time.perf_counter()
        report = _not_applicable_report(history_range)
        _write_json(bundle_directory, _REPORT_JSON, report)
        _bundle_file(bundle_directory, _REPORT_MARKDOWN).write_text(
            _not_applicable_markdown(history_range), encoding="utf-8", newline="\n"
        )
        analyzer_measurements = {
            "phase_durations_ms": None,
            "process_wall_ms": None,
            "peak_working_set_bytes": None,
        }

    report_files = {
        "json": _file_record(_bundle_file(bundle_directory, _REPORT_JSON)),
        "markdown": _file_record(_bundle_file(bundle_directory, _REPORT_MARKDOWN)),
    }
    report_metadata = _report_metadata(report)
    manifest = {
        "schema": BUNDLE_SCHEMA,
        "status": history_range.status,
        "reason": history_range.reason,
        "repository": arguments.repository_name,
        "candidate": {
            "sha": history_range.candidate_sha,
            "tree": history_range.candidate_tree,
            "version": history_range.candidate_version,
            "target_tag": history_range.target_tag,
        },
        "base": {"tag": history_range.base_tag, "sha": history_range.base_sha},
        "range": {
            "kind": history_range.series_kind,
            "series_identity": history_range.series_identity,
            "semantics": "exclusive_base_inclusive_candidate",
        },
        "tool": {
            "package_id": _CLI_PACKAGE_ID,
            "package_version": arguments.candidate_version,
            "package_file": f"{_CLI_PACKAGE_ID}.{arguments.candidate_version}.nupkg",
            "package_sha256": cli_package_sha256,
            "command": "arch-linter-net",
        },
        "history": {
            "schema_version": report_metadata["schema_version"],
            "kind": report_metadata["kind"],
            "semantics_version": report_metadata["history_semantics_version"],
            "tool_version": report_metadata["tool_version"],
        },
        "policy": {
            "input_path": str(policy_path.relative_to(repository)),
            "input_sha256": _sha256_file(policy_path) if policy_path.is_file() else None,
            "effective_history_configuration_sha256": report_metadata["effective_policy_sha256"],
        },
        "content": report_files,
    }
    _write_json(bundle_directory, _REPORT_MANIFEST, manifest)
    orchestration["bundle_render_ms"] = round((time.perf_counter() - bundle_render_started) * 1000, 3)

    observations = {
        "schema": _OBSERVATIONS_SCHEMA,
        "measurement_scope": "release_forensics_runner_not_pr_performance_kpi",
        "run": {
            "id": arguments.run_id,
            "attempt": arguments.run_attempt,
            "started_at_utc": started_at,
            "recorded_at_utc": _utc_now(),
        },
        "candidate": {
            "version": history_range.candidate_version,
            "sha": history_range.candidate_sha,
            "tree": history_range.candidate_tree,
        },
        "orchestration_ms": {
            **orchestration,
            "script_elapsed_before_observation_finalization_ms": round(
                (time.perf_counter() - script_started) * 1000, 3
            ),
        },
        "analyzer": {
            "phase_durations_ms": analyzer_measurements["phase_durations_ms"],
            "process_wall_ms": analyzer_measurements["process_wall_ms"],
            "peak_working_set_bytes": analyzer_measurements["peak_working_set_bytes"],
        },
    }
    _write_json(bundle_directory, _REPORT_OBSERVATIONS, observations)

    names = (
        _REPORT_JSON,
        _REPORT_MARKDOWN,
        _REPORT_MANIFEST,
        _REPORT_OBSERVATIONS,
    )
    checksum_text = "".join(f"{_sha256_file(_bundle_file(bundle_directory, name))}  {name}\n" for name in names)
    _bundle_file(bundle_directory, _REPORT_CHECKSUMS).write_text(
        checksum_text, encoding="utf-8", newline="\n"
    )
    _verify_bundle(
        bundle_directory,
        arguments.repository_name,
        arguments.candidate_version,
        arguments.target_tag,
        arguments.candidate_sha,
        arguments.candidate_tree,
    )
    _write_summary(
        None if not os.environ.get("GITHUB_STEP_SUMMARY") else Path(os.environ["GITHUB_STEP_SUMMARY"]),
        arguments.repository_name,
        arguments.run_id,
        history_range,
        report,
    )
    return manifest


def _parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("run", help="verify the candidate and generate its history bundle")
    run.add_argument("--repository-name", required=True)
    run.add_argument("--candidate-version", required=True)
    run.add_argument("--target-tag", required=True)
    run.add_argument("--candidate-sha", required=True)
    run.add_argument("--candidate-tree", required=True)
    run.add_argument("--run-id", default=os.environ.get("GITHUB_RUN_ID", "0"))
    run.add_argument("--run-attempt", default=os.environ.get("GITHUB_RUN_ATTEMPT", "0"))

    verify = commands.add_parser("verify", help="verify bundle identity and all declared checksums")
    verify.add_argument("--repository-name")
    verify.add_argument("--candidate-version")
    verify.add_argument("--target-tag")
    verify.add_argument("--candidate-sha")
    verify.add_argument("--candidate-tree")
    return parser.parse_args()


def main() -> int:
    arguments = _parse_arguments()
    try:
        paths = _workspace_paths(Path.cwd())
        if arguments.command == "run":
            generate_bundle(arguments, paths)
            print("Release history-forensics bundle is complete.")
        else:
            _verify_bundle(
                paths.bundle_directory,
                arguments.repository_name,
                arguments.candidate_version,
                arguments.target_tag,
                arguments.candidate_sha,
                arguments.candidate_tree,
            )
            print("Release history-forensics bundle identity and checksums verified.")
        return 0
    except (OSError, ValueError) as error:
        print(f"release history-forensics failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
