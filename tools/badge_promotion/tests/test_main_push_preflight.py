from __future__ import annotations

import json
import sys
from pathlib import Path
from types import SimpleNamespace
from typing import cast

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.cli import ProviderFailure  # noqa: E402
from badge_promotion.config import parse_config  # noqa: E402
from badge_promotion.decision import DecisionDisposition, PromotionDecision  # noqa: E402
from badge_promotion.model import PromotionStatus, ReasonCode  # noqa: E402


class FakeApi:
    def __init__(self, responses: dict[str, object]) -> None:
        self.responses = responses
        self.paths: list[str] = []

    def request(self, path: str, **_: object) -> object:
        self.paths.append(path)
        return self.responses[path]


def test_merged_pull_request_falls_back_to_exact_closed_pull_when_commit_association_is_empty() -> None:
    repository = "owner/repo"
    main_sha = "a" * 40
    base_sha = "b" * 40
    head_sha = "c" * 40
    tree_sha = "d" * 40
    commit_path = f"/repos/{repository}/commits/{main_sha}"
    associated_path = f"/repos/{repository}/commits/{main_sha}/pulls"
    closed_pulls_path = f"/repos/{repository}/pulls?state=closed&base=main&sort=created&direction=desc&per_page=100&page=1"
    pull_list_item = {
        "number": 42,
        "merge_commit_sha": main_sha,
        "merged_at": "2026-10-26T15:40:45Z",
        "base": {"repo": {"full_name": repository}, "ref": "main"},
    }
    pull_detail = {
        **pull_list_item,
        "merged": True,
        "head": {"sha": head_sha},
    }
    api = FakeApi(
        {
            commit_path: {"commit": {"tree": {"sha": tree_sha}}, "parents": [{"sha": base_sha}]},
            associated_path: [],
            closed_pulls_path: [pull_list_item],
            f"/repos/{repository}/pulls/42": pull_detail,
            f"/repos/{repository}/commits/{head_sha}": {"commit": {"tree": {"sha": tree_sha}}},
        }
    )

    result = cli._merged_pull_request(cast(cli.GitHubApi, api), repository, main_sha, "main")

    assert result == (tree_sha, base_sha, head_sha, tree_sha, 42)
    assert closed_pulls_path in api.paths


def test_merged_pull_request_fallback_fails_closed_when_merge_commit_matches_multiple_prs() -> None:
    repository = "owner/repo"
    main_sha = "a" * 40
    base_sha = "b" * 40
    tree_sha = "d" * 40
    commit_path = f"/repos/{repository}/commits/{main_sha}"
    associated_path = f"/repos/{repository}/commits/{main_sha}/pulls"
    closed_pulls_path = f"/repos/{repository}/pulls?state=closed&base=main&sort=created&direction=desc&per_page=100&page=1"
    matching_pull = {
        "merge_commit_sha": main_sha,
        "merged_at": "2026-10-26T15:40:45Z",
        "base": {"repo": {"full_name": repository}, "ref": "main"},
    }
    api = FakeApi(
        {
            commit_path: {"commit": {"tree": {"sha": tree_sha}}, "parents": [{"sha": base_sha}]},
            associated_path: [],
            closed_pulls_path: [
                {**matching_pull, "number": 42},
                {**matching_pull, "number": 43},
            ],
        }
    )

    api_client = cast(cli.GitHubApi, api)
    with pytest.raises(ProviderFailure, match="merged_pull_request_ambiguous"):
        cli._merged_pull_request(api_client, repository, main_sha, "main")


def test_preflight_is_read_only_and_requires_temporal_revalidation(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["destination"] = {
        "adapter": "github-raw",
        "branch": "architecture-health-badge",
        "endpoint_path": "architecture-health.json",
    }
    config = parse_config(raw)
    evidence = SimpleNamespace(
        head_sha="b" * 40,
        head_tree_sha="c" * 40,
        run_id=7001,
        run_attempt=1,
        temporal_receipt={"state": "ready"},
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
    api = object()
    resolve_calls: list[bool] = []

    def resolve(_api, _config, *, force_temporal_revalidation: bool = False):
        resolve_calls.append(force_temporal_revalidation)
        return evidence, b"archive"

    def fail_publish(*_: object, **__: object) -> None:
        pytest.fail("preflight must never publish a raw badge")

    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: api)
    monkeypatch.setattr(cli, "resolve_evidence", resolve)
    monkeypatch.setattr(cli, "_promotion_request", lambda *_: object())
    monkeypatch.setattr(cli, "decide_promotion", lambda *_: decision)
    monkeypatch.setattr(cli, "_publish_raw", fail_publish)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "github-raw", "--operation", "preflight"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 0
    assert resolve_calls == [True]
    assert "status=ready" in output.read_text()
    assert "temporal_revalidated=true" in output.read_text()
    assert json.loads(capsys.readouterr().out)["temporal_revalidated"] is True


def test_preflight_error_does_not_publish_an_unavailable_snapshot(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["destination"] = {
        "adapter": "github-raw",
        "branch": "architecture-health-badge",
        "endpoint_path": "architecture-health.json",
    }
    config = parse_config(raw)

    def fail_publish(*_: object, **__: object) -> None:
        pytest.fail("a failed preflight must leave the public badge branch untouched")

    def fail_publisher_failure_handler(*_: object, **__: object) -> None:
        pytest.fail("read-only preflight failures must not enter publishing failure handling")

    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: object())
    def fail_resolve_evidence(*_: object, **__: object) -> object:
        raise ProviderFailure("evidence_unavailable")

    monkeypatch.setattr(cli, "resolve_evidence", fail_resolve_evidence)
    monkeypatch.setattr(cli, "_publish_raw", fail_publish)
    monkeypatch.setattr(cli, "_handle_failure", fail_publisher_failure_handler)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "github-raw", "--operation", "preflight"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")

    assert cli.main() == 1


def test_preflight_unavailable_decision_preserves_metadata_and_never_publishes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["destination"] = {
        "adapter": "github-raw",
        "branch": "architecture-health-badge",
        "endpoint_path": "architecture-health.json",
    }
    config = parse_config(raw)
    evidence = SimpleNamespace(
        head_sha="b" * 40,
        head_tree_sha="c" * 40,
        run_id=7001,
        run_attempt=2,
        temporal_receipt=None,
    )
    decision = PromotionDecision(
        PromotionStatus.UNAVAILABLE,
        ReasonCode.DIRECT_PUSH,
        DecisionDisposition.NOOP,
        1,
        0,
        None,
        None,
        None,
    )
    output = tmp_path / "github-output"
    resolve_calls: list[bool] = []

    def resolve(_api, _config, *, force_temporal_revalidation: bool = False):
        resolve_calls.append(force_temporal_revalidation)
        return evidence, b"archive"

    def fail_publication(*_: object, **__: object) -> None:
        pytest.fail("unavailable preflight decisions must not use raw or Relay publication")

    def fail_publisher_failure_handler(*_: object, **__: object) -> None:
        pytest.fail("unavailable preflight decisions must stay out of publishing failure handling")

    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: object())
    monkeypatch.setattr(cli, "resolve_evidence", resolve)
    monkeypatch.setattr(cli, "_promotion_request", lambda *_: object())
    monkeypatch.setattr(cli, "decide_promotion", lambda *_: decision)
    monkeypatch.setattr(cli, "_publish_raw", fail_publication)
    monkeypatch.setattr(cli, "_prepare_relay_publication", fail_publication)
    monkeypatch.setattr(cli, "_handle_failure", fail_publisher_failure_handler)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "github-raw", "--operation", "preflight"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 1
    assert resolve_calls == [True]
    assert output.read_text() == (
        "reason=direct_push\n"
        f"head_sha={'b' * 40}\n"
        f"head_tree_sha={'c' * 40}\n"
        "run_id=7001\n"
        "run_attempt=2\n"
        "status=unavailable\n"
        "temporal_revalidated=false\n"
    )
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == '{"status":"unavailable","reason":"direct_push"}\n'


def test_preflight_fails_closed_when_temporal_receipt_is_missing(monkeypatch: pytest.MonkeyPatch) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    raw["destination"] = {
        "adapter": "github-raw",
        "branch": "architecture-health-badge",
        "endpoint_path": "architecture-health.json",
    }
    config = parse_config(raw)
    evidence = SimpleNamespace(
        head_sha="b" * 40,
        head_tree_sha="c" * 40,
        run_id=7001,
        run_attempt=1,
        temporal_receipt=None,
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

    def fail_publish(*_: object, **__: object) -> None:
        pytest.fail("preflight without a temporal receipt must leave the public badge untouched")

    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: object())
    monkeypatch.setattr(cli, "resolve_evidence", lambda *_args, **_kwargs: (evidence, b"archive"))
    monkeypatch.setattr(cli, "_promotion_request", lambda *_: object())
    monkeypatch.setattr(cli, "decide_promotion", lambda *_: decision)
    monkeypatch.setattr(cli, "_publish_raw", fail_publish)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "fixture", "--adapter", "github-raw", "--operation", "preflight"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "push")
    monkeypatch.setenv("GITHUB_REF", "refs/heads/main")

    assert cli.main() == 1
