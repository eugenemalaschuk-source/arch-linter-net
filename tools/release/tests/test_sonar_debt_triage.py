from __future__ import annotations

import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[3]
BASELINE_PATH = ROOT / "docs/internal/sonar-debt-baseline-2026-09-13.json"
TRIAGE_PATH = ROOT / "docs/internal/sonar-debt-triage-2026-09-14.json"


def _matches(finding: dict[str, Any], selector: dict[str, Any]) -> bool:
    path = str(finding["path"])
    prefixes = selector.get("pathPrefixes")
    if prefixes and not any(path.startswith(prefix) for prefix in prefixes):
        return False
    keys = selector.get("keys")
    if keys and finding.get("key") not in keys:
        return False
    rules = selector.get("rules")
    if rules and finding.get("rule") not in rules:
        return False
    if finding.get("rule") in selector.get("excludeRules", []):
        return False
    return True


def test_triage_assigns_every_baseline_finding_exactly_once() -> None:
    baseline = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
    triage = json.loads(TRIAGE_PATH.read_text(encoding="utf-8"))
    findings = baseline["findings"]
    assignments: dict[str, list[str]] = {finding["key"]: [] for finding in findings}

    for cluster in triage["clusters"]:
        selectors = cluster.get("selectors")
        if selectors is None:
            selectors = [cluster]
        for finding in findings:
            if any(_matches(finding, selector) for selector in selectors):
                assignments[finding["key"]].append(cluster["id"])

    assert len(findings) == triage["coverage"]["baselineTotal"] == 460
    assert all(len(cluster_ids) == 1 for cluster_ids in assignments.values())
    assert sum(len(cluster_ids) for cluster_ids in assignments.values()) == 460

    counts: dict[str, int] = {}
    for cluster_ids in assignments.values():
        cluster_id = cluster_ids[0]
        counts[cluster_id] = counts.get(cluster_id, 0) + 1
    expected = {cluster["id"]: cluster["baselineCount"] for cluster in triage["clusters"]}
    assert counts == expected
    assert sum(expected.values()) == triage["coverage"]["assignedTotal"]
    assert triage["coverage"]["unassigned"] == 0


def test_reviewed_security_dispositions_name_exact_baseline_keys() -> None:
    baseline = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
    triage = json.loads(TRIAGE_PATH.read_text(encoding="utf-8"))
    baseline_keys = {finding["key"] for finding in baseline["findings"]}
    reviewed = next(
        cluster for cluster in triage["clusters"] if cluster["id"] == "reviewed-false-positive-security"
    )

    assert set(reviewed["keys"]).issubset(baseline_keys)
    assert reviewed["baselineCount"] == len(reviewed["keys"]) == reviewed["persistentCount"]
