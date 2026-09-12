from __future__ import annotations

import io
import inspect
import json
import sys
from pathlib import Path
import zipfile

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.cli import ProviderFailure, _publish_raw, _read_bounded_zip_member, _required_gate, _workflow_blob_sha  # noqa: E402
from badge_promotion.config import parse_config  # noqa: E402


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
    repository_path = "/repos/owner/repo"
    api = FakeApi(
        {
            list_path: [{"id": 42, "name": "main", "parameters": {"required_status_checks": [{"context": "Architecture Coverage"}]}}],
            detail_path: {"id": 42, "target": "branch", "enforcement": "active", "conditions": {"ref_name": {"include": ["~DEFAULT_BRANCH"], "exclude": []}}, "rules": [{"type": "pull_request", "parameters": {}}]},
            repository_path: {"default_branch": "main"},
        },
        failures={"/repos/owner/repo/rules/branches/main"},
    )
    assert not _required_gate(api, "owner/repo", "Architecture Coverage", 15368, "main")
    assert detail_path in api.paths


def test_ruleset_fallback_accepts_a_required_check_from_the_detail_document() -> None:
    list_path = "/repos/owner/repo/rulesets?includes_parents=true&includes_inherited=true&per_page=100"
    detail_path = "/repos/owner/repo/rulesets/42"
    api = FakeApi(
        {
            list_path: [{"id": 42, "name": "main"}],
            detail_path: {"id": 42, "target": "branch", "enforcement": "active", "conditions": {"ref_name": {"include": ["refs/heads/main"], "exclude": []}}, "rules": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}]}}]},
        },
        failures={"/repos/owner/repo/rules/branches/main"},
    )
    assert _required_gate(api, "owner/repo", "Architecture Coverage", 15368, "main")


@pytest.mark.parametrize(
    "change",
    [
        {"enforcement": "evaluate"},
        {"conditions": {"ref_name": {"include": ["refs/heads/release"], "exclude": []}}},
        {"rules": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": False, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}]}}]},
        {"rules": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 99999}]}}]},
    ],
)
def test_ruleset_fallback_requires_active_main_strict_matching_source(change: dict[str, object]) -> None:
    list_path = "/repos/owner/repo/rulesets?includes_parents=true&includes_inherited=true&per_page=100"
    detail_path = "/repos/owner/repo/rulesets/42"
    detail: dict[str, object] = {
        "id": 42,
        "target": "branch",
        "enforcement": "active",
        "conditions": {"ref_name": {"include": ["refs/heads/main"], "exclude": []}},
        "rules": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}]}}],
    }
    detail.update(change)
    api = FakeApi(
        {list_path: [{"id": 42}], detail_path: detail},
        failures={"/repos/owner/repo/rules/branches/main"},
    )
    assert not _required_gate(api, "owner/repo", "Architecture Coverage", 15368, "main")


def test_ruleset_lookup_uses_configured_base_ref() -> None:
    rules_path = "/repos/owner/repo/rules/branches/develop"
    api = FakeApi(
        {
            rules_path: [{
                "type": "required_status_checks",
                "parameters": {
                    "strict_required_status_checks_policy": True,
                    "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}],
                },
            }],
        },
    )
    assert _required_gate(api, "owner/repo", "Architecture Coverage", 15368, "develop")
    assert api.paths == [rules_path]


def test_main_ref_guard_uses_configured_base_ref(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["base_ref"] = "develop"
    config = parse_config(raw)
    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "none"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")
    assert cli.main() == 1


def test_raw_publication_stale_cas_uses_configured_base_ref(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["base_ref"] = "develop"
    config = parse_config(raw)
    repository = config.repository
    main_sha = "a" * 40
    parent_sha = "b" * 40
    ref_path = f"/repos/{repository}/git/ref/heads/architecture-health-badge"
    base_ref_path = f"/repos/{repository}/git/ref/heads/develop"
    api = FakeApi(
        {
            ref_path: {"object": {"sha": parent_sha}},
            base_ref_path: {"object": {"sha": main_sha}},
            f"/repos/{repository}/git/commits/{parent_sha}": {"tree": {"sha": "c" * 40}},
            f"/repos/{repository}/git/blobs": {"sha": "d" * 40},
            f"/repos/{repository}/git/trees": {"sha": "e" * 40},
            f"/repos/{repository}/git/commits": {"sha": "f" * 40},
        }
    )
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    _publish_raw(api, config, b"payload", evidence=None, status="ready", reason="ready")
    assert base_ref_path in api.paths


def test_semantic_evidence_member_is_bounded_before_decompression() -> None:
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("architecture-health.json", b"0" * 32_768)
    with zipfile.ZipFile(io.BytesIO(stream.getvalue())) as opened:
        with pytest.raises(ProviderFailure, match="semantic_evidence_oversized"):
            _read_bounded_zip_member(opened, "architecture-health.json", 16_384)


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
