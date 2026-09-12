from __future__ import annotations

import sys
from pathlib import Path
import inspect

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.cli import ProviderFailure, _required_gate, _workflow_blob_sha  # noqa: E402


class FakeApi:
    def __init__(self, responses: dict[str, object], failures: set[str] | None = None) -> None:
        self.responses = responses
        self.failures = failures or set()
        self.paths: list[str] = []

    def request(self, path: str, **_: object) -> object:
        self.paths.append(path)
        if path in self.failures:
            raise ProviderFailure("required_capability_unavailable")
        return self.responses[path]


def test_ruleset_fallback_fetches_details_instead_of_trusting_summaries() -> None:
    list_path = "/repos/owner/repo/rulesets?includes_parents=true&includes_inherited=true&per_page=100"
    detail_path = "/repos/owner/repo/rulesets/42"
    api = FakeApi(
        {
            list_path: [{"id": 42, "name": "main", "parameters": {"required_status_checks": [{"context": "Architecture Coverage"}]}}],
            detail_path: {"id": 42, "rules": [{"type": "pull_request", "parameters": {}}]},
        },
        failures={"/repos/owner/repo/rules/branches/main"},
    )
    assert not _required_gate(api, "owner/repo", "Architecture Coverage")
    assert detail_path in api.paths


def test_ruleset_fallback_accepts_a_required_check_from_the_detail_document() -> None:
    list_path = "/repos/owner/repo/rulesets?includes_parents=true&includes_inherited=true&per_page=100"
    detail_path = "/repos/owner/repo/rulesets/42"
    api = FakeApi(
        {
            list_path: [{"id": 42, "name": "main"}],
            detail_path: {"id": 42, "rules": [{"type": "required_status_checks", "parameters": {"required_status_checks": [{"context": "Architecture Coverage"}]}}]},
        },
        failures={"/repos/owner/repo/rules/branches/main"},
    )
    assert _required_gate(api, "owner/repo", "Architecture Coverage")


def test_workflow_sha_is_resolved_from_the_versioned_content_endpoint() -> None:
    path = "/repos/owner/repo/contents/.github/workflows/ci.yml?ref=" + "a" * 40
    api = FakeApi({path: {"type": "file", "sha": "b" * 40}})
    assert _workflow_blob_sha(api, "owner/repo", ".github/workflows/ci.yml", "a" * 40) == "b" * 40


def test_workflow_sha_resolution_rejects_a_directory_response() -> None:
    path = "/repos/owner/repo/contents/.github/workflows?ref=" + "a" * 40
    api = FakeApi({path: [{"type": "file", "sha": "b" * 40}]})
    with pytest.raises(ProviderFailure, match="workflow_mismatch"):
        _workflow_blob_sha(api, "owner/repo", ".github/workflows", "a" * 40)


def test_relay_success_writes_ready_output_before_returning() -> None:
    source = inspect.getsource(cli.main)
    relay_start = source.index('if config.destination.adapter.value == "relay":')
    ready_write = source.index('_write_outputs({**output_metadata, "status": "ready"})', relay_start)
    relay_return = source.index("            return 0", ready_write)
    assert source.index("client.publish", relay_start) < ready_write < relay_return
