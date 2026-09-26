#!/usr/bin/env python3
"""Verify the transported report bundle and write its Actions summary."""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from release_forensics_analysis import ReleaseForensicsError
from resolve_release_history_range import ReleaseHistoryRange


BUNDLE_SCHEMA = "release-forensics-transport-manifest/v1"
_SHA256 = re.compile(r"[0-9a-f]{64}\Z")
_GIT_OBJECT_ID = re.compile(r"(?:[0-9a-f]{40}|[0-9a-f]{64})\Z")
_REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\Z")


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as content:
        for block in iter(lambda: content.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _canonical_json_sha256(value: object) -> str:
    canonical = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def verify_bundle(
    bundle_directory: Path,
    expected_repository: str | None = None,
    expected_version: str | None = None,
    expected_tag: str | None = None,
    expected_sha: str | None = None,
    expected_tree: str | None = None,
) -> dict[str, object]:
    manifest_path = bundle_directory / "release-forensics-manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReleaseForensicsError(f"Cannot read release-forensics manifest: {error}") from error
    if not isinstance(manifest, dict) or manifest.get("schema") != BUNDLE_SCHEMA:
        raise ReleaseForensicsError("Unsupported release-forensics bundle manifest schema.")
    candidate = manifest.get("candidate")
    if not isinstance(candidate, dict):
        raise ReleaseForensicsError("Release-forensics candidate identity is malformed.")
    for field in ("version", "target_tag", "sha", "tree"):
        if not isinstance(candidate.get(field), str) or not candidate[field]:
            raise ReleaseForensicsError(f"Release-forensics candidate {field} is malformed.")
    if candidate["target_tag"] != f"v{candidate['version']}":
        raise ReleaseForensicsError("Release-forensics candidate tag does not match its package version.")
    if _GIT_OBJECT_ID.fullmatch(candidate["sha"]) is None:
        raise ReleaseForensicsError("Release-forensics candidate commit is not a full lowercase Git object ID.")
    if _GIT_OBJECT_ID.fullmatch(candidate["tree"]) is None:
        raise ReleaseForensicsError("Release-forensics candidate tree is not a full lowercase Git object ID.")
    checks = (
        (expected_repository, manifest.get("repository"), "repository"),
        (expected_version, candidate.get("version"), "candidate version"),
        (expected_tag, candidate.get("target_tag"), "candidate tag"),
        (expected_sha, candidate.get("sha"), "candidate commit"),
        (expected_tree, candidate.get("tree"), "candidate tree"),
    )
    for expected, actual, description in checks:
        if expected is not None and expected != actual:
            raise ReleaseForensicsError(f"Release-forensics {description} does not match the expected candidate.")

    expected_files = {
        "release-forensics.json",
        "release-forensics.md",
        "release-forensics-observations.json",
    }
    content = manifest.get("content")
    if not isinstance(content, dict) or set(content) != {"json", "markdown"}:
        raise ReleaseForensicsError("Release-forensics manifest does not identify both report content digests.")
    for format_name, record in content.items():
        if not isinstance(record, dict):
            raise ReleaseForensicsError("Release-forensics report record is malformed.")
        name = record.get("file")
        size = record.get("size")
        digest = record.get("sha256")
        expected_name = "release-forensics.json" if format_name == "json" else "release-forensics.md"
        if not isinstance(name, str) or name != expected_name or name not in expected_files:
            raise ReleaseForensicsError("Release-forensics manifest contains an unexpected report path.")
        path = bundle_directory / name
        if (
            not path.is_file()
            or not isinstance(size, int)
            or isinstance(size, bool)
            or path.stat().st_size != size
            or not isinstance(digest, str)
            or _SHA256.fullmatch(digest) is None
            or _sha256_file(path) != digest
        ):
            raise ReleaseForensicsError(f"Release-forensics report digest mismatch: {name}.")
        expected_files.remove(name)
    if expected_files != {"release-forensics-observations.json"}:
        raise ReleaseForensicsError("Release-forensics report inventory is incomplete or duplicated.")

    try:
        report = json.loads((bundle_directory / "release-forensics.json").read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReleaseForensicsError(f"Cannot read the transported history report: {error}") from error
    if not isinstance(report, dict):
        raise ReleaseForensicsError("The transported history report root must be a JSON object.")
    status = manifest.get("status")
    base = manifest.get("base")
    range_metadata = manifest.get("range")
    policy = manifest.get("policy")
    history = manifest.get("history")
    if not isinstance(base, dict):
        raise ReleaseForensicsError("Release-forensics predecessor identity is malformed.")
    if (
        not isinstance(range_metadata, dict)
        or range_metadata.get("semantics") != "exclusive_base_inclusive_candidate"
        or not isinstance(policy, dict)
        or not isinstance(policy.get("input_path"), str)
        or not policy["input_path"]
        or not isinstance(policy.get("input_sha256"), str)
        or _SHA256.fullmatch(policy["input_sha256"]) is None
        or not isinstance(history, dict)
    ):
        raise ReleaseForensicsError("Release-forensics range, policy, or report identity is malformed.")
    preview_suffix = "-preview."
    is_preview = preview_suffix in candidate["version"]
    expected_series = (
        ("preview", f"preview:{candidate['version'].split(preview_suffix, 1)[0]}")
        if is_preview
        else ("stable", "stable")
    )
    if (range_metadata.get("kind"), range_metadata.get("series_identity")) != expected_series:
        raise ReleaseForensicsError("Release-forensics series identity does not match the candidate version.")
    if status == "applicable":
        base_sha = base.get("sha")
        tool = manifest.get("tool")
        analysis = report.get("analysis")
        report_range = analysis.get("range") if isinstance(analysis, dict) else None
        configuration = analysis.get("historyAnalysisConfiguration") if isinstance(analysis, dict) else None
        if (
            not isinstance(base_sha, str)
            or _GIT_OBJECT_ID.fullmatch(base_sha) is None
            or not isinstance(base.get("tag"), str)
            or not isinstance(range_metadata, dict)
            or not isinstance(tool, dict)
            or tool.get("package_id") != "ArchLinterNet.Cli"
            or tool.get("package_version") != candidate["version"]
            or tool.get("package_file") != f"ArchLinterNet.Cli.{candidate['version']}.nupkg"
            or not isinstance(tool.get("package_sha256"), str)
            or _SHA256.fullmatch(tool["package_sha256"]) is None
            or history.get("schema_version") != 1
            or history.get("kind") != "release-architecture-forensics"
            or history.get("semantics_version") != report.get("historySemanticsVersion")
            or history.get("tool_version") != report.get("toolVersion")
            or report.get("schemaVersion") != 1
            or report.get("kind") != "release-architecture-forensics"
            or not isinstance(report.get("toolVersion"), str)
            or not report["toolVersion"]
            or not isinstance(report.get("historySemanticsVersion"), str)
            or not report["historySemanticsVersion"]
            or not isinstance(report_range, dict)
            or report_range.get("resolvedFrom") != base_sha
            or report_range.get("resolvedTo") != candidate["sha"]
            or not isinstance(configuration, dict)
            or policy.get("effective_history_configuration_sha256") != _canonical_json_sha256(configuration)
        ):
            raise ReleaseForensicsError("Transported history report does not match its candidate-bound manifest.")
    elif status == "not_applicable":
        report_candidate = report.get("candidate")
        if (
            manifest.get("reason") != "no_previous_release"
            or base.get("tag") is not None
            or base.get("sha") is not None
            or history.get("schema_version") is not None
            or history.get("kind") != "release-forensics-not-applicable/v1"
            or history.get("semantics_version") is not None
            or history.get("tool_version") is not None
            or report.get("schema") != "release-forensics-not-applicable/v1"
            or report.get("status") != "not-applicable"
            or report.get("reason") != manifest.get("reason")
            or not isinstance(report_candidate, dict)
            or report_candidate.get("version") != candidate["version"]
            or report_candidate.get("tag") != candidate["target_tag"]
            or report_candidate.get("sha") != candidate["sha"]
            or report_candidate.get("tree") != candidate["tree"]
            or report.get("analysis") is not None
            or policy.get("effective_history_configuration_sha256") is not None
        ):
            raise ReleaseForensicsError("Transported not-applicable report does not match its typed manifest result.")
    else:
        raise ReleaseForensicsError("Release-forensics bundle has an unsupported status.")

    observations = bundle_directory / "release-forensics-observations.json"
    if not observations.is_file() or observations.stat().st_size == 0:
        raise ReleaseForensicsError("Release-forensics operational observations are missing.")
    try:
        observation_data = json.loads(observations.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReleaseForensicsError(f"Cannot read release-forensics operational observations: {error}") from error
    observation_candidate = observation_data.get("candidate") if isinstance(observation_data, dict) else None
    if (
        not isinstance(observation_data, dict)
        or observation_data.get("schema") != "release-forensics-operational-observations/v1"
        or not isinstance(observation_candidate, dict)
        or observation_candidate.get("version") != candidate["version"]
        or observation_candidate.get("sha") != candidate["sha"]
        or observation_candidate.get("tree") != candidate["tree"]
    ):
        raise ReleaseForensicsError("Operational observations do not match the candidate-bound manifest.")
    checksums = bundle_directory / "release-forensics-checksums.txt"
    if not checksums.is_file():
        raise ReleaseForensicsError("Release-forensics checksum inventory is missing.")
    checksum_records: dict[str, str] = {}
    for line in checksums.read_text(encoding="utf-8").splitlines():
        digest, separator, name = line.partition("  ")
        if not separator or _SHA256.fullmatch(digest) is None or not name:
            raise ReleaseForensicsError("Release-forensics checksum inventory is malformed.")
        if name in checksum_records:
            raise ReleaseForensicsError("Release-forensics checksum inventory contains duplicate paths.")
        checksum_records[name] = digest
    checkable = [
        bundle_directory / "release-forensics.json",
        bundle_directory / "release-forensics.md",
        observations,
        manifest_path,
    ]
    if set(checksum_records) != {path.name for path in checkable}:
        raise ReleaseForensicsError("Release-forensics checksum inventory does not match the expected bundle.")
    for path in checkable:
        if checksum_records[path.name] != _sha256_file(path):
            raise ReleaseForensicsError(f"Release-forensics checksum verification failed: {path.name}.")
    return manifest


def write_summary(
    summary_path: Path | None,
    repository: str,
    run_id: str,
    history_range: ReleaseHistoryRange,
    report: dict[str, object],
) -> None:
    if summary_path is None:
        return
    if _REPOSITORY.fullmatch(repository) is None or not run_id.isdigit():
        raise ReleaseForensicsError("Actions run identity is invalid for the release summary link.")
    if history_range.status == "applicable":
        analysis = report.get("analysis")
        analyzed_count = analysis.get("analyzedCommitCount", "unknown") if isinstance(analysis, dict) else "unknown"
        status = f"Analyzed {analyzed_count} commits"
        range_text = f"`{history_range.base_sha}..{history_range.candidate_sha}`"
    else:
        status = "Not applicable: no previous release"
        range_text = "No analysis range"
    link = f"https://github.com/{repository}/actions/runs/{run_id}"
    summary = (
        "## Release history forensics\n\n"
        f"- Candidate: `{history_range.candidate_version}` / `{history_range.candidate_sha}`\n"
        f"- Series: `{history_range.series_identity}`\n"
        f"- Range: {range_text}\n"
        f"- Result: {status}\n"
        "- Findings are evidence only and do not gate release publication.\n"
        f"- Bundle and workflow logs: [open run]({link})\n"
    )
    with summary_path.open("a", encoding="utf-8") as output:
        output.write(summary)
