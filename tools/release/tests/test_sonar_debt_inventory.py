from __future__ import annotations

import json
import sys
import urllib.error
from pathlib import Path
from typing import Any

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

    with pytest.raises(inventory.SonarInventoryError, match="is not the latest completed analysis"):
        inventory.build_inventory(client, "project", "main", "c" * 40)


def test_stale_revision_that_did_analyze_once_still_fails_closed() -> None:
    """A SHA that merely appears in analysis history is not enough: if main has since moved
    on to a newer analysis, every branch-scoped read below would observe that newer state
    while the document still claimed the stale SHA, silently mixing two revisions."""
    fetch = FakeFetch(_base_responses({1: {"paging": {"total": 0}, "issues": []}}))
    client = _client(fetch)

    with pytest.raises(inventory.SonarInventoryError, match="is not the latest completed analysis") as error:
        inventory.build_inventory(client, "project", "main", _OTHER_SHA)

    assert _SHA in str(error.value)


def test_no_completed_analyses_fails_closed() -> None:
    fetch = FakeFetch({("/api/project_analyses/search", 1): {"paging": {"total": 0}, "analyses": []}})
    client = _client(fetch)

    with pytest.raises(inventory.SonarInventoryError, match="has no completed analyses"):
        inventory._latest_analysis(client, "project", "main")


def test_quality_gate_is_pinned_to_the_resolved_analysis_id() -> None:
    responses = _base_responses({1: {"paging": {"total": 0}, "issues": []}}, {1: {"paging": {"total": 0}, "hotspots": []}})
    fetch = FakeFetch(responses)
    client = _client(fetch)

    inventory.build_inventory(client, "project", "main", _SHA)

    gate_calls = [params for endpoint, params in fetch.calls if endpoint == "/api/qualitygates/project_status"]
    assert gate_calls == [{"analysisId": "analysis-1", "organization": "org"}]


def test_new_analysis_landing_during_capture_fails_closed() -> None:
    """If a newer analysis lands on the branch between resolving the requested revision and
    finishing the capture, the document must not be presented as bound to the stale SHA."""
    call_count = {"project_analyses/search": 0}
    base = _base_responses(
        {1: {"paging": {"total": 0}, "issues": []}},
        {1: {"paging": {"total": 0}, "hotspots": []}},
    )
    race_analyses = {
        "paging": {"total": 3},
        "analyses": [
            {"key": "analysis-2", "revision": "c" * 40, "date": "2026-09-14T00:00:00+0000"},
            {"key": "analysis-1", "revision": _SHA, "date": "2026-09-13T17:14:50+0000"},
            {"key": "analysis-0", "revision": _OTHER_SHA, "date": "2026-09-12T00:00:00+0000"},
        ],
    }

    def fetch(endpoint: str, params: dict[str, str]) -> dict:
        if endpoint == "/api/project_analyses/search":
            call_count["project_analyses/search"] += 1
            return base[(endpoint, 1)] if call_count["project_analyses/search"] == 1 else race_analyses
        page = int(params.get("p", "1"))
        return base[(endpoint, page)]

    client = _client(fetch)

    with pytest.raises(inventory.SonarInventoryError, match="A newer analysis landed") as error:
        inventory.build_inventory(client, "project", "main", _SHA)

    assert "analysis-2" in str(error.value)


def test_same_analysis_id_can_still_capture_different_issue_state() -> None:
    """The Quality Gate is pinned to one immutable analysisId, but SonarCloud's issue/hotspot
    triage state is live: a finding can be resolved, reopened or marked false-positive without
    a new analysis running. Two captures at the same analysisId must therefore be allowed to
    disagree on findings — the tool's contract is capturedAt-stamped live triage layered on an
    exact code analysis, not a claim that the triage state itself never changes."""
    issue_v1 = {"key": "i1", "rule": "r1", "component": "p:a.py", "line": 1, "status": "OPEN"}
    issue_v2 = {"key": "i1", "rule": "r1", "component": "p:a.py", "line": 1, "status": "RESOLVED"}
    hotspots_page = {"paging": {"total": 0}, "hotspots": []}

    def make_fetch(issue: dict[str, Any]):
        base = _base_responses({1: {"paging": {"total": 1}, "issues": [issue]}}, {1: hotspots_page})

        def fetch(endpoint: str, params: dict[str, str]) -> dict:
            page = int(params.get("p", "1"))
            return base[(endpoint, page)]

        return fetch

    first = inventory.build_inventory(_client(make_fetch(issue_v1)), "project", "main", _SHA, now=lambda: "t1")
    second = inventory.build_inventory(_client(make_fetch(issue_v2)), "project", "main", _SHA, now=lambda: "t2")

    assert first["metadata"]["analysisKey"] == second["metadata"]["analysisKey"] == "analysis-1"
    assert first["metadata"]["capturedAt"] == "t1"
    assert second["metadata"]["capturedAt"] == "t2"
    assert first["findings"][0]["status"] == "OPEN"
    assert second["findings"][0]["status"] == "RESOLVED"


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
            "i1": {
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
            "key": "i1",
            "rule": "r1",
            "path": "a.py",
            "line": 1,
            "disposition": "false-positive",
            "justification": "guarded by the release-workspace confinement helper",
        }
    ]


def test_reviewed_disposition_does_not_leak_across_findings_sharing_rule_component_and_line() -> None:
    """Two distinct SonarCloud issues can share rule, component and line (routinely true for
    file-level issues, where line is null for every issue in the file). The (rule, path, line)
    triple is not a unique identity; only the issue's own key is, so a disposition keyed by
    that triple must never silently apply to more than the one finding it names."""
    responses = _base_responses(
        {
            1: {
                "paging": {"total": 2},
                "issues": [
                    {"key": "i1", "rule": "r1", "component": "p:a.py", "line": None},
                    {"key": "i2", "rule": "r1", "component": "p:a.py", "line": None},
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
            "i1": {
                "disposition": "false-positive",
                "justification": "reviewed for i1 only",
            }
        },
    )

    first, second = result["findings"]
    assert first["key"] == "i1"
    assert first["disposition"] == "false-positive"
    assert second["key"] == "i2"
    assert second["disposition"] == "untriaged"
    assert [item["key"] for item in result["reviewedDispositions"]] == ["i1"]


class _FakeResponse:
    def __init__(self, body: bytes) -> None:
        self._body = body

    def read(self) -> bytes:
        return self._body

    def __enter__(self) -> "_FakeResponse":
        return self

    def __exit__(self, *exc_info: object) -> bool:
        return False


def test_http_fetch_sends_bearer_token_and_parses_json(monkeypatch: pytest.MonkeyPatch) -> None:
    captured: dict[str, object] = {}

    def fake_urlopen(request: object, timeout: int | None = None) -> _FakeResponse:
        captured["url"] = request.full_url  # type: ignore[attr-defined]
        captured["headers"] = dict(request.header_items())  # type: ignore[attr-defined]
        captured["timeout"] = timeout
        return _FakeResponse(json.dumps({"ok": True}).encode("utf-8"))

    monkeypatch.setattr("urllib.request.urlopen", fake_urlopen)
    fetch = inventory._http_fetch("https://sonarcloud.example/", "secret-token")

    result = fetch("/api/thing", {"a": "1"})

    assert result == {"ok": True}
    assert captured["url"] == "https://sonarcloud.example/api/thing?a=1"
    assert captured["headers"]["Authorization"] == "Bearer secret-token"
    assert captured["timeout"] == 60


def test_http_fetch_without_token_omits_authorization_header(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_urlopen(request: object, timeout: int | None = None) -> _FakeResponse:
        assert "Authorization" not in dict(request.header_items())  # type: ignore[attr-defined]
        return _FakeResponse(json.dumps({"ok": True}).encode("utf-8"))

    monkeypatch.setattr("urllib.request.urlopen", fake_urlopen)
    fetch = inventory._http_fetch("https://sonarcloud.example", None)

    assert fetch("/api/thing", {}) == {"ok": True}


def test_http_fetch_raises_on_http_error(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_urlopen(request: object, timeout: int | None = None) -> _FakeResponse:
        raise urllib.error.HTTPError("url", 500, "boom", {}, None)

    monkeypatch.setattr("urllib.request.urlopen", fake_urlopen)
    fetch = inventory._http_fetch("https://sonarcloud.example", None)

    with pytest.raises(inventory.SonarInventoryError, match="HTTP 500"):
        fetch("/api/thing", {})


def test_http_fetch_raises_on_url_error(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_urlopen(request: object, timeout: int | None = None) -> _FakeResponse:
        raise urllib.error.URLError("unreachable")

    monkeypatch.setattr("urllib.request.urlopen", fake_urlopen)
    fetch = inventory._http_fetch("https://sonarcloud.example", None)

    with pytest.raises(inventory.SonarInventoryError, match="SonarCloud request failed"):
        fetch("/api/thing", {})


def test_http_fetch_rejects_non_object_payload(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_urlopen(request: object, timeout: int | None = None) -> _FakeResponse:
        return _FakeResponse(b"[1, 2, 3]")

    monkeypatch.setattr("urllib.request.urlopen", fake_urlopen)
    fetch = inventory._http_fetch("https://sonarcloud.example", None)

    with pytest.raises(inventory.SonarInventoryError, match="was not an object"):
        fetch("/api/thing", {})


def test_search_all_requires_the_collection_key() -> None:
    client = _client(lambda endpoint, params: {"paging": {"total": 0}})

    with pytest.raises(inventory.SonarInventoryError, match="is missing 'issues'"):
        inventory._search_all(client, "/api/issues/search", {}, "issues")


def test_search_all_requires_a_total() -> None:
    client = _client(lambda endpoint, params: {"issues": []})

    with pytest.raises(inventory.SonarInventoryError, match="is missing a total"):
        inventory._search_all(client, "/api/issues/search", {}, "issues")


def test_search_all_accepts_a_top_level_total() -> None:
    client = _client(lambda endpoint, params: {"issues": [{"key": "i1"}], "total": 1})

    result = inventory._search_all(client, "/api/issues/search", {}, "issues")

    assert result == [{"key": "i1"}]


def test_normalize_issue_sorts_impacts_and_derives_path() -> None:
    issue = {
        "key": "i1",
        "rule": "r1",
        "component": "proj:src/a.py",
        "line": 10,
        "type": "CODE_SMELL",
        "severity": "MAJOR",
        "impacts": [
            {"softwareQuality": "SECURITY", "severity": "HIGH"},
            {"softwareQuality": "MAINTAINABILITY", "severity": "LOW"},
            "not-a-dict",
        ],
    }

    normalized = inventory._normalize_issue(issue)

    assert normalized["path"] == "src/a.py"
    assert normalized["impacts"] == [
        {"softwareQuality": "MAINTAINABILITY", "severity": "LOW"},
        {"softwareQuality": "SECURITY", "severity": "HIGH"},
    ]


def test_fetch_measures_returns_empty_when_component_is_missing() -> None:
    client = _client(lambda endpoint, params: {"component": None})

    assert inventory._fetch_measures(client, "project", "main") == {}


def test_fetch_quality_gate_requires_project_status() -> None:
    client = _client(lambda endpoint, params: {"projectStatus": None})

    with pytest.raises(inventory.SonarInventoryError, match="missing projectStatus"):
        inventory._fetch_quality_gate(client, "analysis-1")


def test_fetch_hotspots_normalizes_and_sorts() -> None:
    responses = {
        ("/api/hotspots/search", 1): {
            "paging": {"total": 2},
            "hotspots": [
                {"key": "h2", "ruleKey": "python:S2", "component": "p:b.py", "line": 5, "status": "TO_REVIEW"},
                {"key": "h1", "ruleKey": "python:S1", "component": "p:a.py", "line": 1, "status": "REVIEWED"},
            ],
        }
    }
    client = _client(FakeFetch(responses))

    result = inventory._fetch_hotspots(client, "project", "main")

    assert [item["key"] for item in result] == ["h1", "h2"]
    assert result[0]["disposition"] == "untriaged"


def test_parser_requires_revision_and_applies_defaults() -> None:
    parser = inventory._parser()
    with pytest.raises(SystemExit):
        parser.parse_args([])

    arguments = parser.parse_args(["--revision", "abc"])

    assert arguments.host == inventory._DEFAULT_HOST
    assert arguments.project == inventory._DEFAULT_PROJECT
    assert arguments.organization == inventory._DEFAULT_ORGANIZATION
    assert arguments.branch == inventory._DEFAULT_BRANCH
    assert arguments.format == "json"
    assert arguments.dispositions == "{}"


def _stub_http_fetch(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(
        inventory,
        "_http_fetch",
        lambda host, token: (lambda endpoint, params: {}),
    )


def test_main_rejects_invalid_dispositions_json(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)

    exit_code = inventory.main(["--revision", _SHA, "--dispositions", "not-json"])

    assert exit_code == 2
    assert "not valid JSON" in capsys.readouterr().err


def test_main_rejects_non_object_dispositions(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)

    exit_code = inventory.main(["--revision", _SHA, "--dispositions", "[]"])

    assert exit_code == 2
    assert "must be a JSON object" in capsys.readouterr().err


def test_main_reports_inventory_errors(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)

    def fail(*_args: object, **_kwargs: object) -> None:
        raise inventory.SonarInventoryError("boom")

    monkeypatch.setattr(inventory, "build_inventory", fail)

    exit_code = inventory.main(["--revision", _SHA])

    assert exit_code == 2
    assert "boom" in capsys.readouterr().err


_FAKE_MAIN_INVENTORY = {
    "metadata": {"project": "p", "branch": "main", "revision": _SHA, "analysisDate": "d", "capturedAt": "c"},
    "qualityGate": {"status": "OK", "conditions": []},
    "totals": {"findings": 0, "hotspots": 0},
    "findings": [],
    "hotspots": [],
    "reviewedDispositions": [],
}


def test_main_writes_json_by_default(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)
    monkeypatch.setattr(inventory, "build_inventory", lambda *_a, **_k: dict(_FAKE_MAIN_INVENTORY))

    exit_code = inventory.main(["--revision", _SHA])

    assert exit_code == 0
    output = json.loads(capsys.readouterr().out)
    assert output["qualityGate"]["status"] == "OK"


def test_main_writes_markdown_when_requested(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)
    monkeypatch.setattr(inventory, "build_inventory", lambda *_a, **_k: dict(_FAKE_MAIN_INVENTORY))

    exit_code = inventory.main(["--revision", _SHA, "--format", "markdown"])

    assert exit_code == 0
    assert "# SonarCloud debt inventory" in capsys.readouterr().out


def test_apply_reviewed_dispositions_is_invoked_from_main(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture) -> None:
    _stub_http_fetch(monkeypatch)
    captured: dict[str, object] = {}

    def fake_apply(inventory_payload: dict[str, object], dispositions: dict[str, object]) -> None:
        captured["dispositions"] = dispositions

    monkeypatch.setattr(inventory, "build_inventory", lambda *_a, **_k: dict(_FAKE_MAIN_INVENTORY))
    monkeypatch.setattr(inventory, "apply_reviewed_dispositions", fake_apply)

    exit_code = inventory.main(
        ["--revision", _SHA, "--dispositions", json.dumps({"r1|a.py|1": {"disposition": "false-positive"}})]
    )

    assert exit_code == 0
    assert captured["dispositions"] == {"r1|a.py|1": {"disposition": "false-positive"}}
