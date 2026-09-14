from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import sonar_debt_inventory as inventory  # noqa: E402


_SHA = "a" * 40
_OTHER_SHA = "b" * 40


class FakeFetch:
    """Deterministic in-memory SonarCloud API double with call recording."""

    def __init__(self, responses: dict[tuple[str, int], dict]) -> None:
        self._responses = responses
        self.calls: list[tuple[str, dict[str, str]]] = []

    def __call__(self, endpoint: str, params: dict[str, str]) -> dict:
        self.calls.append((endpoint, dict(params)))
        page = int(params.get("p", "1"))
        key = (endpoint, page)
        if key not in self._responses:
            raise AssertionError(f"Unexpected request: {endpoint} page {page}")
        return self._responses[key]


def _client(fetch) -> inventory.SonarClient:
    return inventory.SonarClient(
        host="https://sonarcloud.example",
        organization="org",
        fetch=fetch,
    )


def _base_responses(issues_pages: dict[int, dict], hotspots_pages: dict[int, dict] | None = None) -> dict:
    responses = {
        ("/api/project_analyses/search", 1): {
            "paging": {"total": 2},
            "analyses": [
                {"key": "analysis-1", "revision": _SHA, "date": "2026-09-13T17:14:50+0000"},
                {"key": "analysis-0", "revision": _OTHER_SHA, "date": "2026-09-12T00:00:00+0000"},
            ],
        },
        ("/api/qualitygates/project_status", 1): {
            "projectStatus": {
                "status": "ERROR",
                "conditions": [
                    {
                        "metricKey": "new_security_rating",
                        "comparator": "GT",
                        "errorThreshold": "1",
                        "actualValue": "5",
                        "status": "ERROR",
                    }
                ],
            }
        },
        ("/api/measures/component", 1): {
            "component": {
                "measures": [
                    {"metric": "bugs", "value": "1"},
                    {"metric": "code_smells", "value": "455"},
                ]
            }
        },
    }
    for page, payload in issues_pages.items():
        responses[("/api/issues/search", page)] = payload
    for page, payload in (hotspots_pages or {}).items():
        responses[("/api/hotspots/search", page)] = payload
    return responses


def test_revision_mismatch_fails_closed() -> None:
    fetch = FakeFetch(_base_responses({1: {"paging": {"total": 0}, "issues": []}}))
    client = _client(fetch)

    with pytest.raises(inventory.SonarInventoryError, match="no completed analysis"):
        inventory.build_inventory(client, "project", "main", "c" * 40)


def test_full_pagination_is_honoured() -> None:
    responses = _base_responses(
        {
            1: {
                "paging": {"total": 3},
                "issues": [
                    {"key": "i1", "rule": "r1", "component": "p:a.py", "line": 1, "type": "CODE_SMELL"},
                    {"key": "i2", "rule": "r1", "component": "p:b.py", "line": 2, "type": "CODE_SMELL"},
                ],
            },
            2: {
                "paging": {"total": 3},
                "issues": [
                    {"key": "i3", "rule": "r2", "component": "p:c.py", "line": 3, "type": "BUG"},
                ],
            },
        },
        {1: {"paging": {"total": 0}, "hotspots": []}},
    )
    client = _client(FakeFetch(responses))

    result = inventory.build_inventory(client, "project", "main", _SHA)

    assert result["totals"]["findings"] == 3
    assert [finding["rule"] for finding in result["findings"]] == ["r1", "r1", "r2"]
    assert all(finding["disposition"] == "untriaged" for finding in result["findings"])


def test_pagination_total_mismatch_fails_closed() -> None:
    responses = _base_responses(
        {
            1: {"paging": {"total": 5}, "issues": [{"key": "i1", "rule": "r1"}]},
            2: {"paging": {"total": 5}, "issues": []},
        },
        {1: {"paging": {"total": 0}, "hotspots": []}},
    )
    client = _client(FakeFetch(responses))

    with pytest.raises(inventory.SonarInventoryError, match="Pagination mismatch"):
        inventory.build_inventory(client, "project", "main", _SHA)


def test_inventory_captures_gate_identity_and_disposition_summary() -> None:
    responses = _base_responses(
        {1: {"paging": {"total": 1}, "issues": [{"key": "i1", "rule": "r1", "component": "p:a.py", "line": 1}]}},
        {1: {"paging": {"total": 0}, "hotspots": []}},
    )
    client = _client(FakeFetch(responses))

    result = inventory.build_inventory(client, "project", "main", _SHA)

    assert result["metadata"]["revision"] == _SHA
    assert result["metadata"]["analysisDate"] == "2026-09-13T17:14:50+0000"
    assert result["qualityGate"]["status"] == "ERROR"
    assert result["qualityGate"]["conditions"][0]["metricKey"] == "new_security_rating"
    markdown = inventory.render_markdown(result)
    assert "| untriaged | 1 |" in markdown


def test_reviewed_disposition_applies_only_to_the_named_finding() -> None:
    responses = _base_responses(
        {
            1: {
                "paging": {"total": 2},
                "issues": [
                    {"key": "i1", "rule": "r1", "component": "p:a.py", "line": 1},
                    {"key": "i2", "rule": "r1", "component": "p:b.py", "line": 2},
                ],
            }
        },
        {1: {"paging": {"total": 0}, "hotspots": []}},
    )
    client = _client(FakeFetch(responses))
    result = inventory.build_inventory(client, "project", "main", _SHA)

    inventory.apply_reviewed_dispositions(
        result,
        {
            "r1|a.py|1": {
                "disposition": "false-positive",
                "justification": "guarded by the release-workspace confinement helper",
            }
        },
    )

    first, second = result["findings"]
    assert first["disposition"] == "false-positive"
    assert second["disposition"] == "untriaged"
    assert result["reviewedDispositions"] == [
        {
            "rule": "r1",
            "path": "a.py",
            "line": 1,
            "disposition": "false-positive",
            "justification": "guarded by the release-workspace confinement helper",
        }
    ]
