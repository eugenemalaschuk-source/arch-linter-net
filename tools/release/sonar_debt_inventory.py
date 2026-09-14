#!/usr/bin/env python3
"""Produce a revision-bound SonarCloud debt inventory and triage report.

The tool reads the SonarCloud web API for one project/branch and refuses to present a
baseline unless the requested revision is the exact latest completed analysis on that
branch, re-verified after capture; see #795 and #783 for that contract. Code identity is
therefore exact: the Quality Gate is read pinned to that one analysis's own analysisId, not
to a mutable "current branch" query.

Issue, hotspot and measure DATA is not similarly pinned, because SonarCloud's web API has
no concept of "the issue list as of analysis X" — only "the issue list right now". Their
disposition (resolved, reopened, hotspot reviewed, false-positive) can change after the
analysis ran without producing a new analysis, so two captures against the same analysisId
are bound to the same code and are not guaranteed byte-identical. Each captured document
records its own `metadata.capturedAt` wall-clock timestamp for exactly this reason: it is
evidence of a point-in-time triage snapshot layered on an exact code analysis, not proof
that the triage state itself is immutable.

It accepts no filesystem path arguments and writes to stdout, so a faulty caller cannot
use it to read or write outside the workspace.

Examples:
    python3 tools/release/sonar_debt_inventory.py --revision <sha>
    python3 tools/release/sonar_debt_inventory.py --revision <sha> --format markdown
"""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Callable

_DEFAULT_HOST = "https://sonarcloud.io"
_DEFAULT_PROJECT = "eugenemalaschuk-source_arch-linter-net"
_DEFAULT_ORGANIZATION = "eugenemalaschuk-source"
_DEFAULT_BRANCH = "main"
_PAGE_SIZE = 500
_METRIC_KEYS = (
    "bugs",
    "vulnerabilities",
    "code_smells",
    "security_hotspots",
    "reliability_rating",
    "security_rating",
    "sqale_rating",
    "duplicated_lines_density",
    "sqale_index",
    "sqale_debt_ratio",
    "cognitive_complexity",
    "complexity",
    "coverage",
    "new_reliability_rating",
    "new_security_rating",
    "new_maintainability_rating",
    "new_coverage",
    "new_duplicated_lines_density",
    "new_security_hotspots_reviewed",
)

_Fetch = Callable[[str, dict[str, str]], dict[str, Any]]


class SonarInventoryError(RuntimeError):
    """Raised when the requested baseline cannot be captured truthfully."""


@dataclass(frozen=True)
class SonarClient:
    """Minimal read-only SonarCloud web API client."""

    host: str
    organization: str
    fetch: _Fetch

    def get(self, endpoint: str, params: dict[str, str]) -> dict[str, Any]:
        query = dict(params)
        query.setdefault("organization", self.organization)
        return self.fetch(endpoint, query)


def _http_fetch(host: str, token: str | None) -> _Fetch:
    base = host.rstrip("/")

    def fetch(endpoint: str, params: dict[str, str]) -> dict[str, Any]:
        url = f"{base}{endpoint}?{urllib.parse.urlencode(params)}"
        request = urllib.request.Request(url, headers={"Accept": "application/json"})
        if token:
            request.add_header("Authorization", f"Bearer {token}")
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                payload = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as error:
            raise SonarInventoryError(
                f"SonarCloud request failed: {endpoint} -> HTTP {error.code}"
            ) from error
        except (urllib.error.URLError, json.JSONDecodeError) as error:
            raise SonarInventoryError(f"SonarCloud request failed: {endpoint}: {error}") from error
        if not isinstance(payload, dict):
            raise SonarInventoryError(f"SonarCloud response was not an object: {endpoint}")
        return payload

    return fetch


def _search_all(
    client: SonarClient,
    endpoint: str,
    params: dict[str, str],
    collection_key: str,
) -> list[dict[str, Any]]:
    """Page through an endpoint until every result is collected, or fail."""
    items: list[dict[str, Any]] = []
    page = 1
    expected: int | None = None
    while True:
        page_params = dict(params)
        page_params["ps"] = str(_PAGE_SIZE)
        page_params["p"] = str(page)
        payload = client.get(endpoint, page_params)
        chunk = payload.get(collection_key)
        if not isinstance(chunk, list):
            raise SonarInventoryError(f"SonarCloud response is missing '{collection_key}': {endpoint}")
        if expected is None:
            paging = payload.get("paging")
            if isinstance(paging, dict) and isinstance(paging.get("total"), int):
                expected = paging["total"]
            elif isinstance(payload.get("total"), int):
                expected = payload["total"]
            else:
                raise SonarInventoryError(f"SonarCloud response is missing a total: {endpoint}")
        items.extend(item for item in chunk if isinstance(item, dict))
        if len(items) >= expected or not chunk:
            break
        page += 1
    if len(items) != expected:
        raise SonarInventoryError(
            f"Pagination mismatch for {endpoint}: collected {len(items)} of {expected}"
        )
    return items


def _normalize_issue(issue: dict[str, Any]) -> dict[str, Any]:
    impacts = issue.get("impacts")
    normalized_impacts = []
    if isinstance(impacts, list):
        for impact in impacts:
            if isinstance(impact, dict):
                normalized_impacts.append(
                    {
                        "softwareQuality": impact.get("softwareQuality"),
                        "severity": impact.get("severity"),
                    }
                )
    normalized_impacts.sort(
        key=lambda impact: (str(impact["softwareQuality"]), str(impact["severity"]))
    )
    return {
        "key": issue.get("key"),
        "rule": issue.get("rule"),
        "component": issue.get("component"),
        "path": (issue.get("component") or "").split(":", 1)[-1],
        "line": issue.get("line"),
        "type": issue.get("type"),
        "severity": issue.get("severity"),
        "cleanCodeAttribute": issue.get("cleanCodeAttribute"),
        "impacts": normalized_impacts,
        "debt": issue.get("debt"),
        "status": issue.get("status"),
        "message": issue.get("message"),
        "disposition": "untriaged",
    }


def _latest_analysis(client: SonarClient, project: str, branch: str) -> dict[str, Any]:
    analyses = _search_all(
        client,
        "/api/project_analyses/search",
        {"project": project, "branch": branch},
        "analyses",
    )
    if not analyses:
        raise SonarInventoryError(f"Branch '{branch}' has no completed analyses.")
    return max(analyses, key=lambda analysis: str(analysis.get("date") or ""))


def _resolve_analysis(client: SonarClient, project: str, branch: str, revision: str) -> dict[str, Any]:
    """Bind to the requested revision only if it is the latest completed analysis.

    A requested SHA that merely appears somewhere in analysis history is not enough: if a
    newer analysis has since landed on the branch, every branch-scoped read below (quality
    gate, measures, issues, hotspots) would observe that newer state while the returned
    metadata still claimed the stale SHA, silently mixing two revisions into one document.
    """
    latest = _latest_analysis(client, project, branch)
    if latest.get("revision") != revision:
        raise SonarInventoryError(
            f"Requested revision {revision} is not the latest completed analysis on branch "
            f"'{branch}'. Latest completed analysis is {latest.get('revision')} "
            f"(analysis {latest.get('key')} at {latest.get('date')})."
        )
    return latest


def _fetch_quality_gate(client: SonarClient, analysis_id: str) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    gate_payload = client.get(
        "/api/qualitygates/project_status",
        {"analysisId": analysis_id},
    ).get("projectStatus")
    if not isinstance(gate_payload, dict):
        raise SonarInventoryError("SonarCloud response is missing projectStatus")
    conditions = [
        {
            "metricKey": condition.get("metricKey"),
            "comparator": condition.get("comparator"),
            "errorThreshold": condition.get("errorThreshold"),
            "actualValue": condition.get("actualValue"),
            "status": condition.get("status"),
        }
        for condition in gate_payload.get("conditions") or []
        if isinstance(condition, dict)
    ]
    conditions.sort(key=lambda condition: str(condition["metricKey"]))
    return gate_payload, conditions


def _fetch_measures(client: SonarClient, project: str, branch: str) -> dict[str, Any]:
    measures: dict[str, Any] = {}
    component = client.get(
        "/api/measures/component",
        {"component": project, "branch": branch, "metricKeys": ",".join(_METRIC_KEYS)},
    ).get("component")
    if not isinstance(component, dict):
        return measures
    for measure in component.get("measures") or []:
        if isinstance(measure, dict) and measure.get("metric") is not None:
            measures[measure["metric"]] = measure.get("value")
    return measures


def _fetch_findings(client: SonarClient, project: str, branch: str) -> list[dict[str, Any]]:
    issues = _search_all(
        client,
        "/api/issues/search",
        {"componentKeys": project, "branch": branch, "resolved": "false"},
        "issues",
    )
    findings = [_normalize_issue(issue) for issue in issues]
    findings.sort(
        key=lambda finding: (
            str(finding["rule"]),
            str(finding["component"]),
            finding["line"] if isinstance(finding["line"], int) else 0,
            str(finding["key"]),
        )
    )
    return findings


def _fetch_hotspots(client: SonarClient, project: str, branch: str) -> list[dict[str, Any]]:
    hotspots = _search_all(
        client,
        "/api/hotspots/search",
        {"projectKey": project, "branch": branch},
        "hotspots",
    )
    return sorted(
        (
            {
                "key": hotspot.get("key"),
                "rule": hotspot.get("ruleKey") or hotspot.get("rule"),
                "component": hotspot.get("component"),
                "line": hotspot.get("line"),
                "status": hotspot.get("status"),
                "disposition": "untriaged",
            }
            for hotspot in hotspots
        ),
        key=lambda hotspot: (
            str(hotspot["rule"]),
            str(hotspot["component"]),
            hotspot["line"] if isinstance(hotspot["line"], int) else 0,
        ),
    )


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def build_inventory(
    client: SonarClient,
    project: str,
    branch: str,
    revision: str,
    *,
    now: Callable[[], str] = _utc_now_iso,
) -> dict[str, Any]:
    """Capture a debt inventory whose code identity is pinned to one analysis revision.

    The Quality Gate is bound exactly to the resolved analysis. Findings, hotspots and
    measures are SonarCloud's live triage state for that same branch at capture time —
    see the module docstring for why the API gives no stronger guarantee — so the
    `metadata.capturedAt` timestamp this returns is part of the document's identity, not
    incidental logging: two documents at the same analysisId can legitimately differ if
    someone re-triaged an issue or reviewed a hotspot between captures.
    """
    analysis = _resolve_analysis(client, project, branch, revision)
    gate_payload, conditions = _fetch_quality_gate(client, str(analysis["key"]))
    measures = _fetch_measures(client, project, branch)
    findings = _fetch_findings(client, project, branch)
    normalized_hotspots = _fetch_hotspots(client, project, branch)
    captured_at = now()

    latest_after_capture = _latest_analysis(client, project, branch)
    if latest_after_capture.get("key") != analysis.get("key"):
        raise SonarInventoryError(
            f"A newer analysis landed on branch '{branch}' while this inventory was being "
            f"captured (requested analysis {analysis.get('key')} at revision {revision}, "
            f"branch now at analysis {latest_after_capture.get('key')} / revision "
            f"{latest_after_capture.get('revision')}). Re-run against the new revision."
        )

    return {
        "metadata": {
            "project": project,
            "organization": client.organization,
            "branch": branch,
            "revision": revision,
            "analysisDate": analysis.get("date"),
            "analysisKey": analysis.get("key"),
            "capturedAt": captured_at,
        },
        "qualityGate": {"status": gate_payload.get("status"), "conditions": conditions},
        "measures": {key: measures[key] for key in sorted(measures)},
        "totals": {
            "findings": len(findings),
            "hotspots": len(normalized_hotspots),
        },
        "findings": findings,
        "hotspots": normalized_hotspots,
        "reviewedDispositions": [],
    }


def disposition_key(finding: dict[str, Any]) -> str:
    """The finding's SonarCloud issue key — the only identity precise enough to bind a
    disposition to exactly one finding. ``rule``/``path``/``line`` are not unique: distinct
    issues routinely share all three, especially file-level issues where ``line`` is null."""
    return str(finding["key"])


def apply_reviewed_dispositions(
    inventory: dict[str, Any],
    dispositions: dict[str, dict[str, str]],
) -> None:
    """Apply individually reviewed dispositions to matching findings only.

    Any finding without an entry stays ``untriaged``; a disposition never covers other
    findings that merely share a rule, file or directory — dispositions are keyed by each
    finding's own SonarCloud issue key, never by the (rule, path, line) it happens to sit at.
    """
    reviewed: list[dict[str, Any]] = []
    for finding in inventory["findings"]:
        entry = dispositions.get(disposition_key(finding))
        if not isinstance(entry, dict):
            continue
        finding["disposition"] = str(entry.get("disposition", "untriaged"))
        finding["justification"] = str(entry.get("justification", ""))
        reviewed.append(
            {
                "key": finding["key"],
                "rule": finding["rule"],
                "path": finding["path"],
                "line": finding["line"],
                "disposition": finding["disposition"],
                "justification": finding["justification"],
            }
        )
    reviewed.sort(key=lambda item: (item["rule"], item["path"], item["line"] or 0, str(item["key"])))
    inventory["reviewedDispositions"] = reviewed


def _tally(findings: list[dict[str, Any]], key: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for finding in findings:
        value = str(finding.get(key))
        counts[value] = counts.get(value, 0) + 1
    return {name: counts[name] for name in sorted(counts)}


_TWO_COLUMN_SEPARATOR = "| --- | --- |"


def _render_tally_section(title: str, header: str, findings: list[dict[str, Any]], key: str) -> list[str]:
    lines = ["", f"## {title}", "", header, _TWO_COLUMN_SEPARATOR]
    for name, count in _tally(findings, key).items():
        lines.append(f"| {name} | {count} |")
    return lines


def render_markdown(inventory: dict[str, Any]) -> str:
    metadata = inventory["metadata"]
    gate = inventory["qualityGate"]
    findings = inventory["findings"]
    lines = [
        "# SonarCloud debt inventory",
        "",
        f"- Project: `{metadata['project']}`",
        f"- Branch: `{metadata['branch']}`",
        f"- Revision: `{metadata['revision']}`",
        f"- Analysis date: {metadata['analysisDate']}",
        f"- Captured at: {metadata['capturedAt']} (findings/hotspots/measures reflect SonarCloud's "
        "live triage state at this instant, not a snapshot pinned to the analysis above)",
        f"- Quality gate: **{gate['status']}**",
        f"- Findings: {inventory['totals']['findings']}",
        f"- Security hotspots: {inventory['totals']['hotspots']}",
        "",
        "## Quality gate conditions",
        "",
        "| Metric | Comparator | Threshold | Actual | Status |",
        "| --- | --- | --- | --- | --- |",
    ]
    for condition in gate["conditions"]:
        lines.append(
            "| {metricKey} | {comparator} | {errorThreshold} | {actualValue} | {status} |".format(
                **condition
            )
        )
    lines += _render_tally_section("Findings by rule", "| Rule | Count |", findings, "rule")
    lines += _render_tally_section("Findings by component", "| Component | Count |", findings, "component")
    lines += _render_tally_section("Disposition summary", "| Disposition | Count |", findings, "disposition")
    lines.append("")
    return "\n".join(lines)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default=_DEFAULT_HOST)
    parser.add_argument("--project", default=_DEFAULT_PROJECT)
    parser.add_argument("--organization", default=_DEFAULT_ORGANIZATION)
    parser.add_argument("--branch", default=_DEFAULT_BRANCH)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--format", choices=("json", "markdown"), default="json")
    parser.add_argument(
        "--dispositions",
        default="{}",
        help="Inline JSON map of a finding's SonarCloud issue key to {disposition, justification}.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _parser().parse_args(argv)
    client = SonarClient(
        host=arguments.host,
        organization=arguments.organization,
        fetch=_http_fetch(arguments.host, None),
    )
    try:
        dispositions = json.loads(arguments.dispositions)
    except json.JSONDecodeError as error:
        print(f"Error: --dispositions is not valid JSON: {error}", file=sys.stderr)
        return 2
    if not isinstance(dispositions, dict):
        print("Error: --dispositions must be a JSON object.", file=sys.stderr)
        return 2
    try:
        inventory = build_inventory(
            client,
            project=arguments.project,
            branch=arguments.branch,
            revision=arguments.revision,
        )
    except SonarInventoryError as error:
        print(f"Error: {error}", file=sys.stderr)
        return 2
    apply_reviewed_dispositions(inventory, dispositions)
    if arguments.format == "markdown":
        sys.stdout.write(render_markdown(inventory))
    else:
        json.dump(inventory, sys.stdout, indent=2, sort_keys=True)
        sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
