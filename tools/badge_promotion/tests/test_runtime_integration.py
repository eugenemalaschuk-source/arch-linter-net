from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.config import parse_config  # noqa: E402
from badge_promotion.model import PromotionConfig  # noqa: E402


def _approved_none_config() -> PromotionConfig:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    return parse_config(raw)


def test_runtime_integration_mode_checks_the_real_action_root_without_api_access(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _approved_none_config()
    action_path = Path(__file__).resolve().parents[3] / ".github" / "actions" / "architecture-health-badge-promotion"
    output = tmp_path / "github-output"
    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: pytest.fail("runtime smoke must not make API requests"))
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "reference-none", "--adapter", "none", "--operation", "integration-test"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "pull_request")
    monkeypatch.setenv("GITHUB_REPOSITORY", config.repository)
    monkeypatch.setenv("GITHUB_ACTION_PATH", str(action_path))
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))
    monkeypatch.delenv("GITHUB_TOKEN", raising=False)

    assert cli.main() == 0
    assert "status=ready" in output.read_text()
    assert "reason=runtime_integration_test" in output.read_text()
    assert '"status":"ready"' in capsys.readouterr().out


def test_runtime_integration_mode_rejects_non_pull_request_events(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    config = _approved_none_config()
    output = tmp_path / "github-output"
    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "reference-none", "--adapter", "none", "--operation", "integration-test"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "workflow_dispatch")
    monkeypatch.setenv("GITHUB_REPOSITORY", config.repository)
    monkeypatch.setenv("GITHUB_ACTION_PATH", str(Path(__file__).resolve().parents[3] / ".github" / "actions" / "architecture-health-badge-promotion"))
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 1
    assert "reason=runtime_test_event_invalid" in output.read_text()


def test_runtime_integration_mode_rejects_publish_adapter_without_failure_publication(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    output = tmp_path / "github-output"
    monkeypatch.setattr(cli, "_load_config", lambda _: pytest.fail("invalid smoke inputs must fail before loading config"))
    monkeypatch.setattr(cli, "GitHubApi", lambda: pytest.fail("runtime smoke must not make API requests"))
    monkeypatch.setattr(cli, "_publish_raw", lambda *_args, **_kwargs: pytest.fail("runtime smoke must never publish"))
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "reference-public-raw", "--adapter", "github-raw", "--operation", "integration-test"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "pull_request")
    monkeypatch.setenv("GITHUB_REPOSITORY", "eugenemalaschuk-source/arch-linter-net")
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 1
    assert "reason=runtime_test_configuration_invalid" in output.read_text()
