"""Trusted Qodana differential gate and GitHub PR report publisher.

This module runs only from the default-branch workflow_run publisher. It never
checks out or executes pull-request code; scanner artifacts are treated as
untrusted data and independently validated before they can affect a status.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
from dataclasses import dataclass
import fnmatch
import hashlib
import html
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

MAX_SARIF_BYTES = 64 * 1024 * 1024
MAX_BASELINE_BYTES = 16 * 1024 * 1024
MAX_ARTIFACT_FILES = 64
EXPECTED_WORKFLOW = ".github/workflows/qodana.yml"
EXPECTED_IMAGE = (
    "jetbrains/qodana-cdnet:2026.2@sha256:"
    "138560f97271952d6a9511866db99ac63b45673416e7938442a807202e3ce4f2"
)
EXPECTED_PROFILE = "qodana.recommended"
EXPECTED_SOLUTION = "ArchLinterNet.slnx"
REPORT_MARKER = "<!-- archlinternet-qodana-report -->"
EDITORCONFIG = ".editorconfig"
EVIDENCE_FILE = "evidence.json"
SARIF_FILE = "cold.sarif.json"
BASE_SARIF_FILE = "base.sarif.json"
PROTECTED_SCAN_PATHS = (
    ".github/workflows/qodana.yml",
    "qodana.yaml",
    EDITORCONFIG,
    "global.json",
    "ArchLinterNet.slnx",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "NuGet.config",
    "tools/scripts/qodana_ci.py",
    "tools/scripts/qodana_gate.py",
    ".github/workflows/qodana-publisher.yml",
)
BASELINE_PATH = "tools/qodana/baseline.json"
SUPPRESSION_LINE = re.compile(
    r"(?:\[\s*SuppressMessage(?:Attribute)?\s*\(\s*['\"](?:ReSharper|Qodana)\b|"
    r"//\s*(?:ReSharper\s+disable|noinspection|Qodana(?:\s|:))|"
    r"#pragma\s+warning\s+disable)",
    re.IGNORECASE,
)
SAFE_RULE = re.compile(r"^[A-Za-z0-9_.-]{1,160}$")
SAFE_SHA = re.compile(r"^[0-9a-f]{40}$")


class GateError(ValueError):
    """A missing, malformed, untrusted or unsuccessful analysis result."""


@dataclass(frozen=True, slots=True)
class Finding:
    key: str
    rule: str
    severity: str
    path: str
    line: int | None
    message: str
    fingerprint: str | None = None

    def as_json(self) -> dict[str, Any]:
        return {
            "key": self.key,
            "rule": self.rule,
            "severity": self.severity,
            "path": self.path,
            "line": self.line,
            "message": self.message,
            "fingerprint": self.fingerprint,
        }


def _read_json_bytes(raw: bytes, *, limit: int, label: str) -> Any:
    if not raw or len(raw) > limit:
        raise GateError(f"{label} is missing or exceeds its size limit")
    try:
        return json.loads(raw.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise GateError(f"{label} is not valid UTF-8 JSON") from error


def _message_text(result: dict[str, Any]) -> str:
    message = result.get("message")
    text = message.get("text", message.get("markdown")) if isinstance(message, dict) else None
    if not isinstance(text, str) or not text:
        raise GateError("SARIF result has no diagnostic message")
    return text[:2000]


def _rule_id(result: dict[str, Any], driver: dict[str, Any]) -> str:
    rule = result.get("ruleId")
    if not rule:
        index = result.get("ruleIndex")
        rules = driver.get("rules")
        if type(index) is not int or index < 0 or not isinstance(rules, list) or index >= len(rules):
            raise GateError("SARIF result has no valid inspection identity")
        rule = rules[index].get("id") if isinstance(rules[index], dict) else None
    if not isinstance(rule, str) or not SAFE_RULE.fullmatch(rule):
        raise GateError("SARIF contains an invalid inspection identity")
    return rule


def _artifact_uri(artifact: Any, run: dict[str, Any]) -> str:
    if not isinstance(artifact, dict):
        return ""
    if "uri" in artifact:
        uri = artifact["uri"]
    else:
        index = artifact.get("index")
        artifacts = run.get("artifacts", [])
        if type(index) is not int or index < 0 or not isinstance(artifacts, list) or index >= len(artifacts):
            raise GateError("SARIF contains an invalid artifact reference")
        entry = artifacts[index]
        uri = entry.get("location", {}).get("uri") if isinstance(entry, dict) else None
    if uri is None:
        return ""
    if not isinstance(uri, str) or len(uri) > 4096:
        raise GateError("SARIF contains an invalid source path")
    normalized = urllib.parse.unquote(uri).replace("\\", "/")
    path = PurePosixPath(normalized)
    if path.is_absolute() or ".." in path.parts:
        raise GateError("SARIF source path escapes the repository")
    return normalized.removeprefix("./")


def _location(result: dict[str, Any], run: dict[str, Any]) -> tuple[str, int | None, int | None]:
    locations = result.get("locations", [])
    if not isinstance(locations, list):
        raise GateError("SARIF locations are malformed")
    if not locations:
        return "", None, None
    location = locations[0]
    physical = location.get("physicalLocation", {}) if isinstance(location, dict) else {}
    artifact = physical.get("artifactLocation", {}) if isinstance(physical, dict) else {}
    region = physical.get("region", {}) if isinstance(physical, dict) else {}
    if not isinstance(region, dict):
        raise GateError("SARIF source region is malformed")
    line = region.get("startLine")
    column = region.get("startColumn")
    if line is not None and (type(line) is not int or line < 1):
        raise GateError("SARIF source line is invalid")
    if column is not None and (type(column) is not int or column < 1):
        raise GateError("SARIF source column is invalid")
    return _artifact_uri(artifact, run), line, column


def _qodana_severity(result: dict[str, Any]) -> str:
    properties = result.get("properties", {})
    severity = properties.get("qodanaSeverity") if isinstance(properties, dict) else None
    if severity not in {"Critical", "High", "Moderate", "Low", "Info"}:
        raise GateError("SARIF result has no recognized Qodana severity")
    return severity


def _stable_fingerprint(result: dict[str, Any]) -> str | None:
    fingerprints = result.get("partialFingerprints", {})
    stable = fingerprints.get("equalIndicator/v1") if isinstance(fingerprints, dict) else None
    return stable.lower() if isinstance(stable, str) and re.fullmatch(r"[A-Fa-f0-9]{64}", stable) else None


def _finding_key(result: dict[str, Any], rule: str, path: str,
                 line: int | None, column: int | None, message: str) -> str:
    stable = _stable_fingerprint(result)
    identity: Any = [rule, stable, path, line, column, message] if stable else [rule, path, line, column, message]
    canonical = json.dumps(identity, ensure_ascii=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def _canonical_sarif_run(document: Any) -> tuple[dict[str, Any], dict[str, Any]]:
    if not isinstance(document, dict) or document.get("version") != "2.1.0":
        raise GateError("Expected SARIF 2.1.0")
    runs = document.get("runs")
    if not isinstance(runs, list) or len(runs) != 1 or not isinstance(runs[0], dict):
        raise GateError("Expected one canonical Qodana SARIF run")
    run = runs[0]
    tool = run.get("tool")
    driver = tool.get("driver", {}) if isinstance(tool, dict) else {}
    if not isinstance(driver, dict) or driver.get("name") != "QDNETC":
        raise GateError("SARIF scanner identity does not match Qodana Community for .NET")
    invocations = run.get("invocations")
    if not isinstance(invocations, list) or len(invocations) != 1:
        raise GateError("SARIF does not contain one canonical invocation")
    invocation = invocations[0]
    if not isinstance(invocation, dict) or invocation.get("executionSuccessful") is not True:
        raise GateError("Qodana SARIF records an unsuccessful scan")
    for key in ("toolExecutionNotifications", "toolConfigurationNotifications"):
        notifications = invocation.get(key, [])
        if not isinstance(notifications, list) or any(
            isinstance(item, dict) and item.get("level") == "error" for item in notifications
        ):
            raise GateError("Qodana SARIF records a scanner error")
    return run, driver


def _parse_finding(result: dict[str, Any], driver: dict[str, Any], run: dict[str, Any]) -> Finding:
    rule = _rule_id(result, driver)
    path, line, column = _location(result, run)
    message = _message_text(result)
    return Finding(
        key=_finding_key(result, rule, path, line, column, message),
        rule=rule,
        severity=_qodana_severity(result),
        path=path,
        line=line,
        message=message,
        fingerprint=_stable_fingerprint(result),
    )


def parse_sarif(raw: bytes) -> list[Finding]:
    document = _read_json_bytes(raw, limit=MAX_SARIF_BYTES, label="SARIF")
    run, driver = _canonical_sarif_run(document)
    results = run.get("results")
    if not isinstance(results, list):
        raise GateError("SARIF has no results inventory")
    findings: list[Finding] = []
    for result in results:
        if not isinstance(result, dict):
            raise GateError("SARIF contains a malformed result")
        if result.get("baselineState") == "absent":
            continue
        findings.append(_parse_finding(result, driver, run))
    return findings


def build_baseline(sarif: bytes, *, analyzed_sha: str, image: str,
                   profile: str = EXPECTED_PROFILE,
                   solution: str = EXPECTED_SOLUTION) -> dict[str, Any]:
    if not SAFE_SHA.fullmatch(analyzed_sha):
        raise GateError("Baseline source revision must be a full Git SHA")
    if image != EXPECTED_IMAGE or profile != EXPECTED_PROFILE or solution != EXPECTED_SOLUTION:
        raise GateError("Baseline scanner identity does not match the reviewed configuration")
    findings = parse_sarif(sarif)
    blocking = [item for item in findings if item.severity in {"Critical", "High"}]
    if blocking:
        raise GateError(f"Refusing to baseline {len(blocking)} High/Critical findings")
    return {
        "schema_version": 3,
        "scanner": "Qodana Community for .NET",
        "image": image,
        "profile": profile,
        "solution": solution,
        "analyzed_sha": analyzed_sha,
        "block_new_moderate": True,
        "findings": [item.as_json() for item in sorted(findings, key=lambda value: value.key)],
    }


def validate_candidate_baseline(raw: bytes, sarif: bytes, *, analyzed_sha: str) -> None:
    """Accept a baseline update only when it is the canonical inventory of this scan."""
    parse_baseline(raw)
    expected = build_baseline(sarif, analyzed_sha=analyzed_sha, image=EXPECTED_IMAGE)
    expected_bytes = (json.dumps(expected, indent=2, sort_keys=True) + "\n").encode("utf-8")
    if raw != expected_bytes:
        raise GateError("Updated Qodana baseline does not exactly match the successful source scan")


def _baseline_finding(row: Any) -> Finding:
    if not isinstance(row, dict):
        raise GateError("Qodana baseline contains a malformed finding")
    key, rule, severity = row.get("key"), row.get("rule"), row.get("severity")
    path, line, message = row.get("path"), row.get("line"), row.get("message")
    fingerprint = row.get("fingerprint")
    if (not isinstance(key, str) or not re.fullmatch(r"[a-f0-9]{64}", key)
            or not isinstance(rule, str) or not SAFE_RULE.fullmatch(rule)
            or severity not in {"Critical", "High", "Moderate", "Low", "Info"}
            or not isinstance(path, str) or not isinstance(message, str)
            or (fingerprint is not None and (not isinstance(fingerprint, str)
                or not re.fullmatch(r"[a-f0-9]{64}", fingerprint)))
            or (line is not None and (type(line) is not int or line < 1))):
        raise GateError("Qodana baseline contains an invalid finding")
    if severity in {"Critical", "High"}:
        raise GateError("Qodana baseline cannot accept High/Critical findings")
    return Finding(key, rule, severity, path, line, message, fingerprint)


def parse_baseline(raw: bytes) -> tuple[str, list[Finding]]:
    document = _read_json_bytes(raw, limit=MAX_BASELINE_BYTES, label="Qodana baseline")
    if not isinstance(document, dict) or document.get("schema_version") != 3:
        raise GateError("Qodana baseline schema is unsupported")
    if (document.get("scanner") != "Qodana Community for .NET"
            or document.get("image") != EXPECTED_IMAGE
            or document.get("profile") != EXPECTED_PROFILE
            or document.get("solution") != EXPECTED_SOLUTION):
        raise GateError("Qodana baseline does not match the trusted scanner configuration")
    sha = document.get("analyzed_sha")
    if not isinstance(sha, str) or not SAFE_SHA.fullmatch(sha):
        raise GateError("Qodana baseline source revision is invalid")
    rows = document.get("findings")
    if not isinstance(rows, list):
        raise GateError("Qodana baseline findings are missing")
    if document.get("block_new_moderate") is not True:
        raise GateError("Qodana baseline must block every new Moderate finding")
    findings = [_baseline_finding(row) for row in rows]
    if len({item.key for item in findings}) != len(findings):
        raise GateError("Qodana baseline contains duplicate identities")
    return sha, findings


@dataclass(frozen=True, slots=True)
class Verdict:
    passed: bool
    findings: list[Finding]
    baseline_count: int
    new: list[Finding]
    remediated: list[Finding]
    blocking: list[Finding]
    counts: dict[str, int]


def evaluate(sarif: bytes, baseline_bytes: bytes) -> Verdict:
    _, baseline = parse_baseline(baseline_bytes)
    findings = parse_sarif(sarif)
    remaining = {(item.key, item.severity): item for item in baseline}
    candidates: list[Finding] = []
    for finding in findings:
        if remaining.pop((finding.key, finding.severity), None) is None:
            candidates.append(finding)

    # First consume exact matches; then match moved findings by Qodana's stable
    # fingerprint, rule, file, message and severity. Multisets preserve duplicate
    # counts, so an additional copy of the same finding is still new.
    by_fingerprint: dict[tuple[str, str, str, str, str], list[Finding]] = defaultdict(list)
    for item in remaining.values():
        if item.fingerprint:
            by_fingerprint[(item.rule, item.path, item.message, item.severity, item.fingerprint)].append(item)
    new: list[Finding] = []
    for finding in candidates:
        identity = (finding.rule, finding.path, finding.message, finding.severity, finding.fingerprint)
        matches = by_fingerprint.get(identity) if finding.fingerprint else None
        if matches:
            matched = matches.pop()
            remaining.pop((matched.key, matched.severity))
        else:
            new.append(finding)
    blocking = [item for item in findings if item.severity in {"Critical", "High"}]
    blocking.extend(item for item in new if item.severity == "Moderate")
    remediated = list(remaining.values())
    counts = {severity: sum(item.severity == severity for item in findings)
              for severity in ("Critical", "High", "Moderate", "Low", "Info")}
    return Verdict(not blocking, findings, len(baseline), new, remediated, blocking, counts)


def _escape_markdown(value: str) -> str:
    single_line = re.sub(r"[\x00-\x1f\x7f\u0085\u2028\u2029]+", " ", value)
    escaped = html.escape(single_line, quote=False).replace("@", "&#64;")
    return escaped.replace("|", "\\|").replace("`", "\\`").replace("[", "\\[").replace("]", "\\]")


def _finding_section(title: str, items: list[Finding], blocking: set[Finding]) -> list[str]:
    lines = ["", f"### {title}"]
    if not items:
        return lines + ["None."]
    for item in items[:25]:
        location = _escape_markdown(item.path or "(no source location)")
        if item.line is not None:
            location += f":{item.line}"
        message = _escape_markdown(item.message[:240])
        marker = " **blocking**" if item in blocking else ""
        lines.append(f"- `{_escape_markdown(item.severity)}` `{_escape_markdown(item.rule)}`{marker} — `{location}` — {message}")
    if len(items) > 25:
        lines.append(f"- … {len(items) - 25} additional findings are in the scan artifact.")
    return lines


def render_report(verdict: Verdict, *, run_url: str, run_id: str,
                  attempt: str, head_sha: str, base_sha: str,
                  baseline_updated: bool = False) -> str:
    state = "PASS" if verdict.passed else "FAIL"
    lines = [
        REPORT_MARKER,
        f"## Qodana Community .NET — {state}",
        "",
        f"Analysis: [`{head_sha[:12]}`]({run_url}) · base `{base_sha[:12]}` · run {run_id} attempt {attempt}.",
        f"Profile: `{EXPECTED_PROFILE}` · image: `{EXPECTED_IMAGE.split('@')[0]}`.",
        f"Findings: {len(verdict.findings)} total; {len(verdict.new)} new; {len(verdict.remediated)} remediated.",
        "",
    ]
    if baseline_updated:
        lines.extend([
            f"Baseline: refreshed from this scan of unchanged base source `{base_sha[:12]}`; it applies only after merge.",
            "",
        ])
    lines.extend([
        "| Severity | Current findings |",
        "| --- | ---: |",
    ])
    lines.extend(f"| {severity} | {verdict.counts[severity]} |"
                 for severity in ("Critical", "High", "Moderate", "Low", "Info"))
    blocking = set(verdict.blocking)
    for title, items in (("New findings", verdict.new), ("Remediated findings", verdict.remediated)):
        lines.extend(_finding_section(title, items, blocking))
    lines += ["", f"[Open Qodana scan and evidence artifact]({run_url})."]
    rendered = "\n".join(lines)
    if len(rendered.encode("utf-8")) > 60000:
        raise GateError("Qodana PR report exceeds the 60 KB comment limit")
    return rendered


def _is_protected_scan_path(path: str) -> bool:
    if path in PROTECTED_SCAN_PATHS:
        return True
    name = PurePosixPath(path).name.lower()
    return (fnmatch.fnmatch(name, "*.csproj")
            or fnmatch.fnmatch(name, "*.props")
            or fnmatch.fnmatch(name, "*.targets")
            or fnmatch.fnmatch(name, "*.globalconfig")
            or fnmatch.fnmatch(name, "*.ruleset")
            or name.endswith(EDITORCONFIG)
            or name in {
                "global.json", "nuget.config", "packages.lock.json", "qodana.yml", "qodana.yaml",
            })


def _inspect_pr_file(item: dict[str, Any]) -> str | None:
    path = item.get("filename") if isinstance(item, dict) else None
    if not isinstance(path, str):
        raise GateError("Pull request file path is malformed")
    if _is_protected_scan_path(path):
        return path
    if path == BASELINE_PATH:
        # The candidate baseline is validated against the exact SARIF inventory later.
        return None
    patch = item.get("patch")
    if not isinstance(patch, str):
        if PurePosixPath(path).suffix.lower() in {
            ".cs", ".csproj", ".props", ".targets", EDITORCONFIG, ".yml", ".yaml",
            ".py", ".json", ".slnx", ".xml", ".config", ".sh",
        }:
            raise GateError("Pull request patch is unavailable for inspection-suppression review")
        return None
    if any(line.startswith("+") and not line.startswith("+++") and SUPPRESSION_LINE.search(line[1:])
           for line in patch.splitlines()):
        return f"{path}: added inspection suppression"
    return None


def _added_suppressions(files: list[dict[str, Any]]) -> list[str]:
    return [change for item in files if (change := _inspect_pr_file(item)) is not None]


class GitHubApi:
    def __init__(self, repository: str, token: str):
        self.repository = repository
        self.token = token
        self.base = f"https://api.github.com/repos/{repository}"

    def request(self, path: str, *, method: str = "GET", body: Any = None) -> Any:
        url = path if path.startswith("https://api.github.com/") else self.base + path
        data = None if body is None else json.dumps(body).encode("utf-8")
        request = urllib.request.Request(url, data=data, method=method, headers={
            "Accept": "application/vnd.github+json",
            "Authorization": f"Bearer {self.token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "Content-Type": "application/json",
            "User-Agent": "ArchLinterNet-Qodana-Gate",
        })
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                raw = response.read(4 * 1024 * 1024 + 1)
                if len(raw) > 4 * 1024 * 1024:
                    raise GateError("GitHub API response exceeded the size limit")
                return json.loads(raw) if raw else None
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as error:
            raise GateError(f"GitHub API request failed: {method} {url.rsplit('/', 1)[-1]}") from error

    def request_raw(self, path: str, *, limit: int) -> bytes:
        url = path if path.startswith("https://api.github.com/") else self.base + path
        request = urllib.request.Request(url, headers={
            "Accept": "application/vnd.github.raw+json",
            "Authorization": f"Bearer {self.token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "ArchLinterNet-Qodana-Gate",
        })
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                raw = response.read(limit + 1)
                if not raw or len(raw) > limit:
                    raise GateError("GitHub raw file is empty or exceeds its size limit")
                return raw
        except (urllib.error.URLError, TimeoutError) as error:
            raise GateError(f"GitHub raw-file request failed: {url.rsplit('/', 1)[-1]}") from error


def _paginate(api: GitHubApi, path: str, *, max_pages: int = 3) -> list[Any]:
    rows: list[Any] = []
    for page in range(1, max_pages + 1):
        separator = "&" if "?" in path else "?"
        response = api.request(f"{path}{separator}per_page=100&page={page}")
        if not isinstance(response, list):
            raise GateError("GitHub API returned an invalid paginated collection")
        rows.extend(response)
        if len(response) < 100:
            return rows
    raise GateError("GitHub API collection exceeded its validation limit")


def _resolve_pull_request(api: GitHubApi, workflow_run: dict[str, Any]) -> dict[str, Any] | None:
    candidates = workflow_run.get("pull_requests")
    if not isinstance(candidates, list) or not candidates:
        sha = workflow_run.get("head_sha")
        if not isinstance(sha, str) or not SAFE_SHA.fullmatch(sha):
            raise GateError("Workflow run has no valid commit identity")
        candidates = api.request(f"/commits/{sha}/pulls?per_page=100")
    if not isinstance(candidates, list):
        raise GateError("Workflow run PR association is malformed")
    numbers = {item.get("number") for item in candidates
               if isinstance(item, dict) and type(item.get("number")) is int}
    if not numbers:
        return None
    if len(numbers) != 1:
        raise GateError("Workflow run is associated with multiple pull requests")
    pull = api.request(f"/pulls/{next(iter(numbers))}")
    return pull if isinstance(pull, dict) else None


def _source_sha(pull: dict[str, Any], field: str) -> str | None:
    ref = pull.get(field)
    sha = ref.get("sha") if isinstance(ref, dict) else None
    if sha is not None and (not isinstance(sha, str) or not SAFE_SHA.fullmatch(sha)):
        raise GateError(f"Source run contains an invalid pull request {field} SHA")
    return sha


def _source_pull_request_identity(workflow_run: dict[str, Any], number: int,
                                  run_head_sha: str) -> tuple[str | None, str | None]:
    pull_requests = workflow_run.get("pull_requests")
    if pull_requests is None or pull_requests == []:
        # GitHub can omit PR associations from workflow_run payloads. In that
        # case the run API's head is authoritative; the evidence manifest must
        # still match the live PR base before any successful verdict is posted.
        return (run_head_sha, None) if SAFE_SHA.fullmatch(run_head_sha) else (None, None)
    if not isinstance(pull_requests, list):
        return None, None
    matches = [item for item in pull_requests if isinstance(item, dict) and item.get("number") == number]
    if len(matches) != 1:
        return None, None
    pull = matches[0]
    head_sha, base_sha = _source_sha(pull, "head"), _source_sha(pull, "base")
    if head_sha is None and run_head_sha and SAFE_SHA.fullmatch(run_head_sha):
        head_sha = run_head_sha
    return head_sha, base_sha


def _fetch_baseline(api: GitHubApi, revision_sha: str) -> bytes:
    path = f"/contents/{BASELINE_PATH}?ref=" + urllib.parse.quote(revision_sha, safe="")
    return api.request_raw(path, limit=MAX_BASELINE_BYTES)


def _downloaded_reports(directory: Path) -> tuple[bytes, bytes, bytes]:
    if directory.is_symlink() or not directory.is_dir():
        raise GateError("Exact-run Qodana artifact is missing")
    entries = list(directory.rglob("*"))
    if any(path.is_symlink() for path in entries):
        raise GateError("Qodana artifact contains a symlink")
    files = sorted(path for path in entries if path.is_file())
    if len(files) < 3 or len(files) > MAX_ARTIFACT_FILES:
        raise GateError("Qodana artifact has an invalid file inventory")
    named = {path.name: path for path in files}
    if not {EVIDENCE_FILE, SARIF_FILE, BASE_SARIF_FILE}.issubset(named):
        raise GateError("Qodana artifact is missing evidence or a head/base SARIF report")
    if any(directory not in path.parents for path in files):
        raise GateError("Qodana artifact contains a file outside its extraction root")
    allowed = {EVIDENCE_FILE, SARIF_FILE, BASE_SARIF_FILE, "summary.md", "image.log", "cold.log", "base.log"}
    total_size = 0
    for path in files:
        relative = path.relative_to(directory).as_posix()
        scanner_log = relative.startswith(("cold-log/", "base-log/")) and relative.endswith(".log")
        scanner_config = relative in {"cold-log/qodana.yaml", "base-log/qodana.yaml"}
        if relative not in allowed and not scanner_log and not scanner_config:
            raise GateError("Qodana artifact contains an unexpected file")
        size = path.stat().st_size
        total_size += size
        if size > MAX_SARIF_BYTES or total_size > 2 * MAX_SARIF_BYTES + 24 * 1024 * 1024:
            raise GateError("Qodana artifact exceeds its size limit")
    return named[EVIDENCE_FILE].read_bytes(), named[SARIF_FILE].read_bytes(), named[BASE_SARIF_FILE].read_bytes()


def _validate_evidence(raw: bytes, *, repository: str, pr_number: int,
                       head_sha: str, base_sha: str, run_id: int,
                       attempt: int) -> dict[str, Any]:
    document = _read_json_bytes(raw, limit=2 * 1024 * 1024, label="Qodana evidence")
    expected = {
        "repository": repository,
        "pull_request_number": pr_number,
        "head_sha": head_sha,
        "base_sha": base_sha,
        "base_analyzed_sha": base_sha,
        "analyzed_sha": head_sha,
        "run_id": str(run_id),
        "run_attempt": str(attempt),
        "event": "pull_request",
        "configured_image": EXPECTED_IMAGE,
        "configured_profile": EXPECTED_PROFILE,
        "configured_solution": EXPECTED_SOLUTION,
        "status": "completed",
    }
    if not isinstance(document, dict) or document.get("schema_version") != 1:
        raise GateError("Qodana evidence schema is unsupported")
    mismatches = [key for key, value in expected.items() if document.get(key) != value]
    if mismatches:
        raise GateError("Qodana evidence identity mismatch: " + ", ".join(mismatches))
    scans = document.get("scans")
    if not isinstance(scans, list) or len(scans) != 2:
        raise GateError("Expected one Qodana scan each for the PR base and head")
    for scan, label in zip(scans, ("base", "cold")):
        if (not isinstance(scan, dict) or scan.get("label") != label
                or scan.get("status") != "completed" or scan.get("exit_code") != 0):
            raise GateError(f"Qodana {label} scan did not complete successfully")
    if not isinstance(document.get("image_id"), str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", document["image_id"]):
        raise GateError("Qodana evidence has no immutable runtime image identity")
    return document


def _set_status(api: GitHubApi, head_sha: str, state: str, description: str, url: str) -> None:
    api.request(f"/statuses/{head_sha}", method="POST", body={
        "state": state,
        "context": "qodana",
        "description": description[:140],
        "target_url": url,
    })


def _upsert_comment(api: GitHubApi, number: int, body: str) -> None:
    comments = _paginate(api, f"/issues/{number}/comments")
    matches = [item for item in comments if isinstance(item, dict)
               and isinstance(item.get("body"), str) and REPORT_MARKER in item["body"]
               and isinstance(item.get("user"), dict)
               and item["user"].get("login") == "github-actions[bot]"]
    if len(matches) > 1:
        keep = max(matches, key=lambda item: int(item.get("id", 0)))
        for duplicate in matches:
            if duplicate is not keep:
                api.request(f"/issues/comments/{duplicate['id']}", method="DELETE")
        matches = [keep]
    if matches:
        api.request(f"/issues/comments/{matches[0]['id']}", method="PATCH", body={"body": body})
    else:
        api.request(f"/issues/{number}/comments", method="POST", body={"body": body})


def _write_summary(text: str) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with Path(summary_path).open("a", encoding="utf-8") as output:
            output.write(text + "\n")


def _pr_is_current(api: GitHubApi, number: int, head_sha: str, base_sha: str) -> bool:
    current = api.request(f"/pulls/{number}")
    if not isinstance(current, dict) or current.get("state") != "open":
        return False
    head, base = current.get("head"), current.get("base")
    return (isinstance(head, dict) and head.get("sha") == head_sha
            and isinstance(base, dict) and base.get("sha") == base_sha)


def _verified_source_run(api: GitHubApi, run: dict[str, Any], repository: str) -> dict[str, Any]:
    full_run = api.request(f"/actions/runs/{run.get('id')}")
    if not isinstance(full_run, dict):
        raise GateError("Source workflow run cannot be verified")
    if (full_run.get("path") != EXPECTED_WORKFLOW
            or full_run.get("event") != "pull_request"
            or full_run.get("repository", {}).get("full_name") != repository
            or full_run.get("id") != run.get("id")
            or full_run.get("run_attempt") != run.get("run_attempt")
            or full_run.get("head_sha") != run.get("head_sha")):
        raise GateError("Source workflow identity does not match the trusted Qodana workflow")
    return full_run


def _pull_identity(pr: dict[str, Any]) -> tuple[int, str, str]:
    number, head, base = pr.get("number"), pr.get("head"), pr.get("base")
    head_sha = head.get("sha") if isinstance(head, dict) else None
    base_sha = base.get("sha") if isinstance(base, dict) else None
    if (type(number) is not int or not isinstance(head_sha, str) or not SAFE_SHA.fullmatch(head_sha)
            or not isinstance(base_sha, str) or not SAFE_SHA.fullmatch(base_sha)):
        raise GateError("Pull request identity is incomplete")
    return number, head_sha, base_sha


def _governance_changes(files: list[dict[str, Any]]) -> tuple[list[str], bool, bool]:
    changes = _added_suppressions(files)
    baseline_updated = any(
        isinstance(item, dict) and item.get("filename") == BASELINE_PATH for item in files
    )
    baseline_mixed = baseline_updated and any(
        not isinstance(item, dict) or item.get("filename") != BASELINE_PATH for item in files
    )
    if baseline_mixed:
        changes.extend(
            item["filename"] for item in files
            if isinstance(item, dict) and isinstance(item.get("filename"), str)
            and item["filename"] != BASELINE_PATH
        )
    return changes, baseline_updated, baseline_mixed


def _report_governance_changes(api: GitHubApi, number: int, head_sha: str, base_sha: str,
                               run_url: str, changes: list[str], baseline_mixed: bool) -> int:
    if not _pr_is_current(api, number, head_sha, base_sha):
        print("Qodana scan became stale; leaving the current PR report untouched.")
        return 1
    names = ", ".join(
        f"`{_escape_markdown(name)}`" for name in dict.fromkeys(changes[:20])
    )
    reason = (
        "A baseline refresh must be isolated to a baseline-only PR because any other changed source would make the candidate inventory ambiguous."
        if baseline_mixed else
        "This PR changes trusted scanner/build configuration or adds an inspection suppression."
    )
    guidance = (
        "Submit the baseline refresh separately."
        if baseline_mixed else
        "An authorized ruleset bypass is required after separate trusted review."
    )
    report_text = (
        f"{REPORT_MARKER}\n## Qodana Community .NET — UNASSESSABLE\n\n"
        f"{reason} The trusted gate cannot assess those changes from this PR. "
        f"{guidance}\n\nChanged paths: " + names
    )
    _upsert_comment(api, number, report_text)
    _write_summary(report_text)
    _set_status(api, head_sha, "failure", "Qodana scanner policy changed in this PR", run_url)
    return 1


def _assess_scan(api: GitHubApi, artifact_dir: Path, run: dict[str, Any], repository: str,
                 number: int, head_sha: str, base_sha: str, baseline_updated: bool) -> Verdict:
    run_id, attempt = run["id"], run["run_attempt"]
    expected_artifact = f"qodana-evidence-{run_id}-{attempt}"
    artifacts = api.request(f"/actions/runs/{run_id}/artifacts?per_page=100")
    artifact_rows = artifacts.get("artifacts") if isinstance(artifacts, dict) else None
    exact_artifacts = [item for item in artifact_rows if isinstance(item, dict)
                       and item.get("name") == expected_artifact
                       and item.get("expired") is False] if isinstance(artifact_rows, list) else []
    if len(exact_artifacts) != 1:
        raise GateError("The exact-run Qodana evidence artifact is missing or ambiguous.")
    if os.environ.get("QODANA_ARTIFACT_DOWNLOAD_OUTCOME") != "success":
        raise GateError("The exact-run Qodana evidence artifact could not be downloaded.")

    evidence_bytes, sarif, base_sarif = _downloaded_reports(artifact_dir)
    evidence = _validate_evidence(
        evidence_bytes, repository=repository, pr_number=number,
        head_sha=head_sha, base_sha=base_sha, run_id=run_id, attempt=attempt,
    )
    for scan_record, report in zip(evidence["scans"], (base_sarif, sarif)):
        if (scan_record.get("sarif_sha256") != hashlib.sha256(report).hexdigest()
                or scan_record.get("sarif_bytes") != len(report)):
            raise GateError("SARIF digest does not match the scanner evidence manifest")
    if baseline_updated:
        candidate_baseline = _fetch_baseline(api, head_sha)
        # A baseline-only PR analyzes unchanged base source. Stamping the base
        # avoids a self-referential commit hash in the baseline file.
        validate_candidate_baseline(candidate_baseline, base_sarif, analyzed_sha=base_sha)
    # The committed baseline supplies reviewed policy and an audit record. A
    # fresh exact-base scan supplies the actual comparison, so fixed findings
    # cannot silently return while an older baseline remains in the repository.
    parse_baseline(_fetch_baseline(api, base_sha))
    if run.get("conclusion") != "success":
        raise GateError("Qodana workflow reported failure")
    fresh_base = build_baseline(base_sarif, analyzed_sha=base_sha, image=EXPECTED_IMAGE)
    return evaluate(sarif, json.dumps(fresh_base).encode("utf-8"))


def _publish_assessment(api: GitHubApi, number: int, head_sha: str, base_sha: str,
                        run_url: str, run_id: int, attempt: int, baseline_updated: bool,
                        verdict: Verdict | None, failure: str | None) -> int:
    if not _pr_is_current(api, number, head_sha, base_sha):
        print("Qodana scan became stale; leaving the current PR report untouched.")
        return 1
    if verdict is None:
        report_text = (
            f"{REPORT_MARKER}\n## Qodana Community .NET — UNASSESSABLE\n\n"
            "The trusted gate could not verify a complete, current Qodana scan. "
            f"Reason: {_escape_markdown(failure or 'unknown validation error')}\n\n"
            f"[Open source scan run]({run_url})."
        )
        _upsert_comment(api, number, report_text)
        _write_summary(report_text)
        _set_status(api, head_sha, "failure", "Qodana analysis is unassessable", run_url)
        print("Qodana gate: UNASSESSABLE")
        return 1

    report_text = render_report(
        verdict, run_url=run_url, run_id=str(run_id), attempt=str(attempt),
        head_sha=head_sha, base_sha=base_sha, baseline_updated=baseline_updated,
    )
    state = "success" if verdict.passed else "failure"
    description = (
        f"{len(verdict.new)} new findings; {len(verdict.blocking)} blocking"
        if verdict.passed or verdict.new else "High or Critical findings remain"
    )
    _upsert_comment(api, number, report_text)
    _write_summary(report_text)
    _set_status(api, head_sha, state, description, run_url)
    print(f"Qodana gate: {'PASS' if verdict.passed else 'FAIL'}; "
          f"{len(verdict.new)} new, {len(verdict.remediated)} remediated, "
          f"{len(verdict.blocking)} blocking.")
    return 0 if verdict.passed else 1


def publish() -> int:
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    token = os.environ.get("GITHUB_TOKEN", "")
    event_path = os.environ.get("GITHUB_EVENT_PATH", "")
    artifact_dir = Path(os.environ.get("QODANA_ARTIFACT_DIR", ""))
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository) or not token or not event_path:
        raise GateError("Publisher is missing its GitHub identity")
    try:
        event = json.loads(Path(event_path).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise GateError("Publisher event payload is unavailable") from error
    run = event.get("workflow_run") if isinstance(event, dict) else None
    if not isinstance(run, dict):
        raise GateError("Publisher was not triggered by a workflow_run event")
    if run.get("event") != "pull_request":
        print("Qodana publisher ignored a non-pull-request scan.")
        return 0
    api = GitHubApi(repository, token)
    full_run = _verified_source_run(api, run, repository)
    pr = _resolve_pull_request(api, run)
    if pr is None:
        raise GateError("Source scan has no associated pull request")
    number, head_sha, base_sha = _pull_identity(pr)
    if pr.get("state") != "open":
        print(f"Qodana publisher ignored closed PR #{number}.")
        return 0
    run_id, attempt = run.get("id"), run.get("run_attempt")
    run_url = run.get("html_url", "")
    if type(run_id) is not int or type(attempt) is not int or not isinstance(run_url, str):
        raise GateError("Source run identity is malformed")

    source_head_sha, source_base_sha = _source_pull_request_identity(
        run, number, str(full_run.get("head_sha", ""))
    )
    if source_head_sha is None or not SAFE_SHA.fullmatch(source_head_sha):
        print("Qodana source run has no verified PR head; leaving the current PR report untouched.")
        return 1

    # Bind even the provisional failure to the commit that the source run names.
    # An old workflow_run may finish after the PR advances; it must never write a
    # status onto the newer, unscanned head.
    _set_status(api, source_head_sha, "error", "Qodana trusted report is being verified", run_url)

    if (source_head_sha != head_sha
            or (source_base_sha is not None and source_base_sha != base_sha)):
        print("Qodana scan is stale; leaving the current PR report untouched.")
        _set_status(api, source_head_sha, "error", "Qodana scan does not match the current PR head/base", run_url)
        return 1

    try:
        files = _paginate(api, f"/pulls/{number}/files")
        governance_changes, baseline_updated, baseline_mixed = _governance_changes(files)
    except GateError as error:
        return _publish_assessment(api, number, head_sha, base_sha, run_url,
                                   run_id, attempt, False, None, str(error))
    if governance_changes:
        return _report_governance_changes(
            api, number, head_sha, base_sha, run_url, governance_changes, baseline_mixed
        )

    failure: str | None = None
    try:
        verdict = _assess_scan(api, artifact_dir, run, repository, number,
                               head_sha, base_sha, baseline_updated)
    except (GateError, OSError) as error:
        failure = str(error)
        verdict = None

    return _publish_assessment(api, number, head_sha, base_sha, run_url,
                               run_id, attempt, baseline_updated, verdict, failure)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-baseline", type=Path,
                        help="Build a reviewed baseline from a completed raw SARIF report")
    parser.add_argument("--analyzed-sha", help="Full revision analyzed by the baseline scan")
    parser.add_argument("--output", type=Path, help="Output path for --build-baseline")
    args = parser.parse_args(argv)
    if args.build_baseline is None:
        if args.analyzed_sha or args.output:
            parser.error("--analyzed-sha and --output require --build-baseline")
        return publish()
    if args.analyzed_sha is None or args.output is None:
        parser.error("--build-baseline requires --analyzed-sha and --output")
    if args.output != Path(BASELINE_PATH):
        parser.error(f"--output must be {BASELINE_PATH}")
    raw = args.build_baseline.read_bytes()
    baseline = build_baseline(raw, analyzed_sha=args.analyzed_sha, image=EXPECTED_IMAGE)
    Path(BASELINE_PATH).write_text(json.dumps(baseline, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Built Qodana baseline with {len(baseline['findings'])} findings from {args.analyzed_sha}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (GateError, OSError) as error:
        print(f"Qodana gate failed closed: {error}", file=sys.stderr)
        raise SystemExit(1)
