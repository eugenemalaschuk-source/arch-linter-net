#!/usr/bin/env python3
"""Run one candidate CLI history analysis and capture its isolated process measurements."""

from __future__ import annotations

import json
import math
import re
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

from resolve_release_history_range import ReleaseHistoryRange


_TIME_MARKER = re.compile(r"release_forensics_peak_rss_kb=(\d+)")
_TIMINGS = re.compile(r"^History timings \(ms\): (.+)$", re.MULTILINE)
_REPORT_JSON = "release-forensics.json"
_REPORT_MARKDOWN = "release-forensics.md"


class ReleaseForensicsError(ValueError):
    """Raised when the candidate CLI does not produce valid history evidence."""


def _parse_timing_value(name: str, value: str) -> float | int | str:
    if name == "ingestion_calls":
        try:
            count = int(value)
        except ValueError as error:
            raise ReleaseForensicsError("The candidate CLI emitted an invalid ingestion count.") from error
        if count < 0:
            raise ReleaseForensicsError("The candidate CLI emitted a negative ingestion count.")
        return count
    if value == "n/a":
        return value
    try:
        duration = float(value)
    except ValueError as error:
        raise ReleaseForensicsError("The candidate CLI emitted a non-numeric phase duration.") from error
    if not math.isfinite(duration) or duration < 0:
        raise ReleaseForensicsError("The candidate CLI emitted an invalid phase duration.")
    return duration


def _validate_timings(values: dict[str, float | int | str]) -> None:
    if values.get("ingestion_calls") != 1:
        raise ReleaseForensicsError("The candidate CLI did not perform exactly one history ingestion.")
    for required in ("ingestion", "scoring", "policy", "json_render", "markdown_render", "output"):
        if not isinstance(values.get(required), (int, float)):
            raise ReleaseForensicsError(f"The candidate CLI timing record is missing {required}.")
    if values.get("enrichment") != "n/a":
        raise ReleaseForensicsError("History timing must report enrichment as n/a for this Git-only analysis.")


def _parse_timings(stderr: str) -> dict[str, float | int | str]:
    match = _TIMINGS.search(stderr)
    if match is None:
        raise ReleaseForensicsError("The candidate CLI did not emit its required --timings record.")
    values: dict[str, float | int | str] = {}
    for item in match.group(1).split(";"):
        name, separator, value = item.strip().partition("=")
        if not separator or not name or name in values:
            raise ReleaseForensicsError("The candidate CLI emitted malformed timing data.")
        values[name] = _parse_timing_value(name, value)
    _validate_timings(values)
    return values


def _run_with_peak_memory(arguments: list[str], repository: Path) -> subprocess.CompletedProcess[str]:
    timer = Path("/usr/bin/time")
    if not timer.is_file():
        raise ReleaseForensicsError("GNU time is required to record isolated candidate process memory.")
    return subprocess.run(
        [str(timer), "-f", "release_forensics_peak_rss_kb=%M", "--", *arguments],
        cwd=repository,
        text=True,
        capture_output=True,
        check=False,
    )


def _read_report(json_path: Path, markdown_path: Path) -> dict[str, Any]:
    if not json_path.is_file() or json_path.stat().st_size == 0:
        raise ReleaseForensicsError("History analysis did not create a non-empty JSON report.")
    if not markdown_path.is_file() or markdown_path.stat().st_size == 0:
        raise ReleaseForensicsError("History analysis did not create a non-empty Markdown report.")
    try:
        report = json.loads(json_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReleaseForensicsError(f"History analysis did not create valid UTF-8 JSON: {error}") from error
    if not isinstance(report, dict):
        raise ReleaseForensicsError("History report root must be a JSON object.")
    return report


def _validate_report(report: dict[str, Any], history_range: ReleaseHistoryRange) -> None:
    analysis = report.get("analysis")
    if not isinstance(analysis, dict):
        raise ReleaseForensicsError("History report does not include a valid analysis object.")
    analysis_range = analysis.get("range")
    if not isinstance(analysis_range, dict):
        raise ReleaseForensicsError("History report does not include a valid resolved range.")
    if report.get("schemaVersion") != 1 or report.get("kind") != "release-architecture-forensics":
        raise ReleaseForensicsError("History report schema or kind does not match the candidate CLI contract.")
    if not isinstance(report.get("toolVersion"), str) or not report["toolVersion"]:
        raise ReleaseForensicsError("History report does not identify its assembly version.")
    if not isinstance(report.get("historySemanticsVersion"), str) or not report["historySemanticsVersion"]:
        raise ReleaseForensicsError("History report does not identify its history semantics version.")
    if (
        analysis_range.get("resolvedFrom") != history_range.base_sha
        or analysis_range.get("resolvedTo") != history_range.candidate_sha
    ):
        raise ReleaseForensicsError("History report resolved range does not match the pinned candidate range.")
    _validate_analyzed_commit_count(analysis)
    if not isinstance(analysis.get("historyAnalysisConfiguration"), dict):
        raise ReleaseForensicsError("History report does not include the effective history policy configuration.")


def _validate_analyzed_commit_count(analysis: dict[str, Any]) -> None:
    count = analysis.get("analyzedCommitCount")
    if not isinstance(count, int) or isinstance(count, bool) or count < 0:
        raise ReleaseForensicsError("History report does not contain a valid analyzed commit count.")


def analyze(
    command: Path,
    repository: Path,
    policy_path: Path,
    history_range: ReleaseHistoryRange,
    bundle_directory: Path,
) -> tuple[dict[str, Any], dict[str, Any]]:
    if history_range.status != "applicable" or history_range.base_sha is None:
        raise ReleaseForensicsError("Cannot execute history analysis without an eligible predecessor.")
    if not policy_path.is_file():
        raise ReleaseForensicsError(f"The effective history policy input is missing: {policy_path}")
    json_path = bundle_directory / _REPORT_JSON
    markdown_path = bundle_directory / _REPORT_MARKDOWN
    arguments = [
        str(command),
        "history",
        "analyze",
        "--repository",
        str(repository),
        "--policy",
        str(policy_path),
        "--from",
        history_range.base_sha,
        "--to",
        history_range.candidate_sha,
        "--report",
        f"json={json_path}",
        "--report",
        f"markdown={markdown_path}",
        "--timings",
    ]
    started = time.perf_counter()
    result = _run_with_peak_memory(arguments, repository)
    process_wall_ms = (time.perf_counter() - started) * 1000
    if result.stdout:
        print(result.stdout, end="", file=sys.stderr)
    if result.returncode != 0:
        print(result.stderr, end="", file=sys.stderr)
        raise ReleaseForensicsError(f"Candidate history analysis failed with exit code {result.returncode}.")
    memory = _TIME_MARKER.search(result.stderr)
    if memory is None:
        raise ReleaseForensicsError("GNU time did not report candidate process peak memory.")
    timings = _parse_timings(result.stderr)
    report = _read_report(json_path, markdown_path)
    _validate_report(report, history_range)
    extra = {
        "phase_durations_ms": timings,
        "process_wall_ms": round(process_wall_ms, 3),
        "peak_working_set_bytes": int(memory.group(1)) * 1024,
    }
    return report, extra
