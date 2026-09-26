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


_TIME_MARKER = re.compile(r"release_forensics_peak_rss_kb=([0-9]+)")
_TIMINGS = re.compile(r"^History timings \(ms\): (.+)$", re.MULTILINE)


class ReleaseForensicsError(ValueError):
    """Raised when the candidate CLI does not produce valid history evidence."""


def _parse_timings(stderr: str) -> dict[str, float | int | str]:
    match = _TIMINGS.search(stderr)
    if match is None:
        raise ReleaseForensicsError("The candidate CLI did not emit its required --timings record.")
    values: dict[str, float | int | str] = {}
    for item in match.group(1).split(";"):
        name, separator, value = item.strip().partition("=")
        if not separator or not name or name in values:
            raise ReleaseForensicsError("The candidate CLI emitted malformed timing data.")
        if name == "ingestion_calls":
            try:
                count = int(value)
            except ValueError as error:
                raise ReleaseForensicsError("The candidate CLI emitted an invalid ingestion count.") from error
            if count < 0:
                raise ReleaseForensicsError("The candidate CLI emitted a negative ingestion count.")
            values[name] = count
        elif value == "n/a":
            values[name] = value
        else:
            try:
                duration = float(value)
            except ValueError as error:
                raise ReleaseForensicsError("The candidate CLI emitted a non-numeric phase duration.") from error
            if not math.isfinite(duration) or duration < 0:
                raise ReleaseForensicsError("The candidate CLI emitted an invalid phase duration.")
            values[name] = duration
    if values.get("ingestion_calls") != 1:
        raise ReleaseForensicsError("The candidate CLI did not perform exactly one history ingestion.")
    for required in ("ingestion", "scoring", "policy", "json_render", "markdown_render", "output"):
        if not isinstance(values.get(required), (int, float)):
            raise ReleaseForensicsError(f"The candidate CLI timing record is missing {required}.")
    if values.get("enrichment") != "n/a":
        raise ReleaseForensicsError("History timing must report enrichment as n/a for this Git-only analysis.")
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
    json_path = bundle_directory / "release-forensics.json"
    markdown_path = bundle_directory / "release-forensics.md"
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
    analysis = report.get("analysis")
    if not isinstance(analysis, dict):
        raise ReleaseForensicsError("History report does not include a valid analysis object.")
    analysis_range = analysis.get("range")
    if not isinstance(analysis_range, dict):
        raise ReleaseForensicsError("History report does not include a valid resolved range.")
    analyzed_commit_count = analysis.get("analyzedCommitCount")
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
    if (
        not isinstance(analyzed_commit_count, int)
        or isinstance(analyzed_commit_count, bool)
        or analyzed_commit_count < 0
    ):
        raise ReleaseForensicsError("History report does not contain a valid analyzed commit count.")
    configuration = analysis.get("historyAnalysisConfiguration")
    if not isinstance(configuration, dict):
        raise ReleaseForensicsError("History report does not include the effective history policy configuration.")
    extra = {
        "phase_durations_ms": timings,
        "process_wall_ms": round(process_wall_ms, 3),
        "peak_working_set_bytes": int(memory.group(1)) * 1024,
    }
    return report, extra
