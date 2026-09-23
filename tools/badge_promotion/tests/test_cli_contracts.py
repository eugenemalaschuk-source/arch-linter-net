from __future__ import annotations

import io
import inspect
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from typing import cast
import zipfile

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.adapters import AdapterError  # noqa: E402
from badge_promotion.config import parse_config  # noqa: E402
from badge_promotion.cli import (  # noqa: E402
    ProviderFailure,
    _publish_raw,
    _read_bounded_zip_member,
    _required_gate,
    _validate_invocation,
    _workflow_blob_sha,
)
from badge_promotion.decision import DecisionDisposition, PromotionDecision  # noqa: E402
from badge_promotion.model import EvidenceContext, PromotionStatus, ReasonCode  # noqa: E402


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
    assert not _required_gate(cast(cli.GitHubApi, api), "owner/repo", "Architecture Coverage", 15368, "main")
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
    assert _required_gate(cast(cli.GitHubApi, api), "owner/repo", "Architecture Coverage", 15368, "main")


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
    assert not _required_gate(cast(cli.GitHubApi, api), "owner/repo", "Architecture Coverage", 15368, "main")


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
    assert _required_gate(cast(cli.GitHubApi, api), "owner/repo", "Architecture Coverage", 15368, "develop")
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


def test_renewal_accepts_scheduled_invocation_for_configured_base_ref(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["base_ref"] = "develop"
    config = parse_config(raw)
    monkeypatch.setenv("GITHUB_EVENT_NAME", "schedule")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/develop")
    _validate_invocation(config, "renew")
    with pytest.raises(ProviderFailure, match="event_or_ref_mismatch"):
        _validate_invocation(config, "publish")


def test_adapter_error_becomes_fixed_cli_output(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["destination"] = {
        "adapter": "relay",
        "alias": "alias",
        "endpoint": "https://relay.example",
        "audience": "audience",
    }
    config = parse_config(raw)
    evidence = SimpleNamespace(
        head_sha="b" * 40,
        head_tree_sha="c" * 40,
        run_id=1,
        run_attempt=1,
        semantic_horizon=datetime(2026, 9, 12, 11, tzinfo=timezone.utc),
    )
    decision = PromotionDecision(
        PromotionStatus.READY,
        ReasonCode.READY,
        DecisionDisposition.COMMIT,
        1,
        0,
        b"{}",
        "a" * 64,
        None,
    )
    output = tmp_path / "github-output"
    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: object())
    monkeypatch.setattr(cli, "resolve_evidence", lambda *_: (evidence, b"archive"))
    monkeypatch.setattr(cli, "decide_promotion", lambda *_: decision)
    monkeypatch.setattr(cli, "issue_github_oidc_token", lambda *_: "token")
    def fail_prepare(*_: object, **__: object) -> object:
        raise AdapterError("relay_cas_conflict")

    monkeypatch.setattr(cli.HttpRelayClient, "prepare", fail_prepare)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "relay"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))
    assert cli.main() == 1
    assert "status=unavailable" in output.read_text()
    assert "reason=relay_cas_conflict" in output.read_text()
    assert "Traceback" not in capsys.readouterr().err


def test_raw_publication_stale_cas_uses_configured_base_ref(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["base_ref"] = "develop"
    config = parse_config(raw)
    repository = config.repository
    main_sha = "a" * 40
    parent_sha = "b" * 40
    ref_path = f"/repos/{repository}/git/ref/heads/architecture-health-badge"
    update_ref_path = f"/repos/{repository}/git/refs/heads/architecture-health-badge"
    base_ref_path = f"/repos/{repository}/git/ref/heads/develop"
    api = FakeApi(
        {
            ref_path: {"object": {"sha": parent_sha}},
            update_ref_path: {},
            base_ref_path: {"object": {"sha": main_sha}},
            f"/repos/{repository}/git/commits/{parent_sha}": {"tree": {"sha": "c" * 40}},
            f"/repos/{repository}/git/blobs": {"sha": "d" * 40},
            f"/repos/{repository}/git/trees": {"sha": "e" * 40},
            f"/repos/{repository}/git/commits": {"sha": "f" * 40},
        }
    )
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    _publish_raw(cast(cli.GitHubApi, api), config, b"payload", evidence=None, status="ready", reason="ready")
    assert base_ref_path in api.paths


def test_resolve_evidence_binds_manifest_attempt_to_producer_job(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    config = parse_config(raw)
    repository = config.repository
    main_sha = "a" * 40
    base_sha = "b" * 40
    head_sha = "c" * 40
    tree_sha = "d" * 40
    run_id = 7001
    job_id = 8001
    workflow_path = config.producer.workflow_path
    workflow_ref = f"/repos/{repository}/contents/{workflow_path}?ref={head_sha}"
    check_path = f"/repos/{repository}/commits/{head_sha}/check-runs?check_name=Architecture%20Coverage&filter=latest&per_page=100"
    producer_run_path = f"/repos/{repository}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"
    job_path = f"/repos/{repository}/actions/runs/{run_id}/jobs?per_page=100"
    artifacts_path = f"/repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100"
    evidence_stream = io.BytesIO()
    with zipfile.ZipFile(evidence_stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "architecture-health.json",
            b'{"report_evidence":{"publication_evidence":{"semantic_horizon":"2099-09-13T16:00:00Z"}}}',
        )

    class ResolveApi:
        def request(self, path: str, **_: object) -> object:
            responses: dict[str, object] = {
                f"/repos/{repository}/commits/{main_sha}": {"commit": {"tree": {"sha": tree_sha}}, "parents": [{"sha": base_sha}]},
                f"/repos/{repository}/commits/{main_sha}/pulls": [{"number": 42}],
                f"/repos/{repository}/pulls/42": {"base": {"repo": {"full_name": repository}, "ref": "main"}, "merged": True, "merge_commit_sha": main_sha, "head": {"sha": head_sha}},
                f"/repos/{repository}/commits/{head_sha}": {"commit": {"tree": {"sha": tree_sha}}},
                workflow_ref: {"type": "file", "sha": config.producer.workflow_sha},
                check_path: {"check_runs": [{"name": "Architecture Coverage", "status": "completed", "conclusion": "success", "app": {"slug": "github-actions", "id": 15368}, "details_url": f"https://github.com/{repository}/actions/runs/{run_id}/job/{job_id}"}]},
                producer_run_path: {"workflow_runs": [{"id": run_id, "path": workflow_path, "event": "pull_request", "head_sha": head_sha, "conclusion": "success", "run_attempt": 2, "created_at": "2026-09-13T15:00:00Z"}]},
                job_path: {"jobs": [{"id": job_id, "name": config.producer.job_name, "conclusion": "success", "run_attempt": 1}]},
                artifacts_path: {"artifacts": [{"id": 9001, "name": config.producer.artifact_name, "expired": False, "archive_download_url": "badge"}, {"id": 9002, "name": config.producer.evidence_artifact_name, "expired": False, "archive_download_url": "evidence"}]},
                f"/repos/{repository}/rules/branches/main": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}]}}],
            }
            return responses[path]

        def download(self, url: str) -> bytes:
            return b"badge-archive" if url == "badge" else evidence_stream.getvalue()

    monkeypatch.setenv("GITHUB_REPOSITORY", repository)
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    resolved, _ = cli.resolve_evidence(ResolveApi(), config)
    assert isinstance(resolved, EvidenceContext)
    assert resolved.run_id == run_id
    assert resolved.run_attempt == 1
    assert resolved.job_id == job_id


@pytest.mark.parametrize(
    "failing_attempts, expect_failure, expected_sleeps, expected_retry_messages",
    [(1, False, [1], 1), (5, True, [1, 2, 4, 8], 4)],
)
def test_raw_publication_retries_ref_update_race_and_fails_closed(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    failing_attempts: int,
    expect_failure: bool,
    expected_sleeps: list[int],
    expected_retry_messages: int,
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    config = parse_config(raw)
    repository = config.repository
    main_sha = "a" * 40
    parent_sha = "b" * 40
    ref_path = f"/repos/{repository}/git/ref/heads/architecture-health-badge"
    update_ref_path = f"/repos/{repository}/git/refs/heads/architecture-health-badge"

    class FlakyApi(FakeApi):
        patch_attempts = 0

        def request(self, path: str, **kwargs: object) -> object:
            if path == update_ref_path and kwargs.get("method") == "PATCH":
                self.patch_attempts += 1
                self.paths.append(path)
                if self.patch_attempts <= failing_attempts:
                    raise ProviderFailure("github_api_unavailable")
                return {}
            return super().request(path, **kwargs)

    api = FlakyApi(
        {
            ref_path: {"object": {"sha": parent_sha}},
            f"/repos/{repository}/git/ref/heads/main": {"object": {"sha": main_sha}},
            f"/repos/{repository}/git/commits/{parent_sha}": {"tree": {"sha": "c" * 40}},
            f"/repos/{repository}/git/blobs": {"sha": "d" * 40},
            f"/repos/{repository}/git/trees": {"sha": "e" * 40},
            f"/repos/{repository}/git/commits": {"sha": "f" * 40},
        }
    )
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    sleeps: list[int] = []
    monkeypatch.setattr(cli.time, "sleep", sleeps.append)

    if expect_failure:
        typed_api = cast(cli.GitHubApi, api)
        with pytest.raises(ProviderFailure, match="publication_race_lost"):
            _publish_raw(typed_api, config, b"payload", evidence=None, status="ready", reason="ready")
    else:
        _publish_raw(cast(cli.GitHubApi, api), config, b"payload", evidence=None, status="ready", reason="ready")

    assert api.patch_attempts == len(expected_sleeps) + 1
    assert api.paths.count(ref_path) == api.patch_attempts
    assert api.paths.count(update_ref_path) == api.patch_attempts
    assert sleeps == expected_sleeps
    assert capsys.readouterr().err.count("raw publication retry") == expected_retry_messages


def test_semantic_evidence_member_is_bounded_before_decompression() -> None:
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("architecture-health.json", b"0" * 32_769)
    with zipfile.ZipFile(io.BytesIO(stream.getvalue())) as opened:
        with pytest.raises(ProviderFailure, match="semantic_evidence_oversized"):
            _read_bounded_zip_member(opened, "architecture-health.json", 32_768)


def test_semantic_evidence_member_accepts_repository_metrics_evidence_size() -> None:
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("architecture-health.json", b"0" * 18_000)
    with zipfile.ZipFile(io.BytesIO(stream.getvalue())) as opened:
        assert len(_read_bounded_zip_member(opened, "architecture-health.json", 32_768)) == 18_000


def test_workflow_sha_is_resolved_from_the_versioned_content_endpoint() -> None:
    path = "/repos/owner/repo/contents/.github/workflows/ci.yml?ref=" + "a" * 40
    api = FakeApi({path: {"type": "file", "sha": "b" * 40}})
    assert _workflow_blob_sha(cast(cli.GitHubApi, api), "owner/repo", ".github/workflows/ci.yml", "a" * 40) == "b" * 40


def test_workflow_sha_resolution_rejects_a_directory_response() -> None:
    path = "/repos/owner/repo/contents/.github/workflows?ref=" + "a" * 40
    api = FakeApi({path: [{"type": "file", "sha": "b" * 40}]})
    typed_api = cast(cli.GitHubApi, api)
    with pytest.raises(ProviderFailure, match="workflow_mismatch"):
        _workflow_blob_sha(typed_api, "owner/repo", ".github/workflows", "a" * 40)


def test_github_artifact_download_uses_github_api_media_type(monkeypatch: pytest.MonkeyPatch) -> None:
    captured: dict[str, object] = {}

    class Response:
        def __enter__(self) -> "Response":
            return self

        def __exit__(self, *_: object) -> None:
            return None

        def read(self, _limit: int) -> bytes:
            return b"zip-bytes"

    class Opener:
        def open(self, request: object, *, timeout: int) -> Response:
            captured["request"] = request
            captured["timeout"] = timeout
            return Response()

    def build_opener(*handlers: object) -> Opener:
        captured["handlers"] = handlers
        return Opener()

    def unused_urlopen(*_: object, **__: object) -> None:
        raise AssertionError("artifact downloads must use the safe redirect opener")

    monkeypatch.setattr(cli.urllib.request, "build_opener", build_opener)
    monkeypatch.setattr(cli.urllib.request, "urlopen", unused_urlopen)

    monkeypatch.setenv("GITHUB_TOKEN", "token")

    api = cli.GitHubApi()

    assert api.download("https://api.github.com/repos/owner/repo/actions/artifacts/42/zip") == b"zip-bytes"
    request = captured["request"]
    assert isinstance(request, cli.urllib.request.Request)
    assert request.get_header("Accept") == "application/vnd.github+json"
    assert request.get_header("Authorization") == "Bearer token"
    assert captured["timeout"] == 30
    assert isinstance(captured["handlers"][0], cli._ArtifactRedirectHandler)  # type: ignore[index]


def test_github_artifact_redirect_drops_bearer_on_storage_host() -> None:
    request = cli.urllib.request.Request(
        "https://api.github.com/repos/owner/repo/actions/artifacts/42/zip",
        headers={"authorization": "Bearer token", "accept": "application/vnd.github+json"},
    )
    redirected = cli._ArtifactRedirectHandler().redirect_request(
        request,
        None,
        302,
        "Found",
        {},
        "https://productionresultssa.example/artifact.zip?signature=opaque",
    )

    assert redirected is not None
    assert redirected.get_header("Authorization") is None
    assert redirected.get_header("Accept") == "application/vnd.github+json"


def test_github_artifact_redirect_rejects_plain_http() -> None:
    request = cli.urllib.request.Request("https://api.github.com/repos/owner/repo/actions/artifacts/42/zip")
    redirected = cli._ArtifactRedirectHandler().redirect_request(request, None, 302, "Found", {}, "http://storage.example/artifact.zip")

    assert redirected is None


def test_relay_success_writes_ready_output_before_returning() -> None:
    source = inspect.getsource(cli.main)
    relay_start = source.index('if config.destination.adapter.value == "relay":')
    ready_write = source.index('_write_outputs({**output_metadata, "status": "ready"})', relay_start)
    relay_return = source.index("            return 0", ready_write)
    assert source.index("client.publish", relay_start) < ready_write < relay_return
