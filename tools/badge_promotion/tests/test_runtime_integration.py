from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli
from badge_promotion.adapters import AdapterError  # noqa: E402
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


@pytest.mark.parametrize(
    ("invalid_field", "reason"),
    [("repository", "runtime_test_repository_invalid"), ("action_path", "runtime_test_action_path_invalid")],
)
def test_runtime_integration_rejects_untrusted_context_without_api_access(
    invalid_field: str,
    reason: str,
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    config = _approved_none_config()
    action_path = Path(__file__).resolve().parents[3] / ".github" / "actions" / "architecture-health-badge-promotion"
    output = tmp_path / "github-output"
    monkeypatch.setattr(cli, "_load_config", lambda _: config)
    monkeypatch.setattr(cli, "GitHubApi", lambda: pytest.fail("runtime integration must not access the API"))
    monkeypatch.setattr(cli, "_publish_raw", lambda *_args, **_kwargs: pytest.fail("runtime integration must not publish"))
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "reference-none", "--adapter", "none", "--operation", "integration-test"])
    monkeypatch.setenv("GITHUB_EVENT_NAME", "pull_request")
    monkeypatch.setenv("GITHUB_REPOSITORY", "another/repository" if invalid_field == "repository" else config.repository)
    monkeypatch.setenv("GITHUB_ACTION_PATH", str(tmp_path if invalid_field == "action_path" else action_path))
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 1
    assert output.read_text() == f"status=unavailable\nreason={reason}\n"


def test_runtime_integration_reports_adapter_error_from_config_without_publishing(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    output = tmp_path / "github-output"

    def fail_publishing(*_: object, **__: object) -> None:
        pytest.fail("runtime integration configuration failures must never publish")

    monkeypatch.setattr(cli, "_load_config", lambda _: (_ for _ in ()).throw(AdapterError("relay_transport_unavailable")))
    monkeypatch.setattr(cli, "_publish_raw", fail_publishing)
    monkeypatch.setattr(cli, "_prepare_relay_publication", fail_publishing)
    monkeypatch.setattr(cli, "_handle_failure", fail_publishing)
    monkeypatch.setattr(sys, "argv", ["cli", "--configuration-id", "reference-none", "--adapter", "none", "--operation", "integration-test"])
    monkeypatch.setenv("GITHUB_OUTPUT", str(output))

    assert cli.main() == 1
    assert output.read_text() == "status=unavailable\nreason=relay_transport_unavailable\n"
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == '{"status":"unavailable","reason":"relay_transport_unavailable"}\n'


def test_python_module_entry_point_runs_from_the_trusted_action_root(tmp_path: Path) -> None:
    repository_root = Path(__file__).resolve().parents[3]
    action_path = repository_root / ".github" / "actions" / "architecture-health-badge-promotion"
    output = tmp_path / "github-output"
    environment = os.environ.copy()
    environment.update(
        {
            "GITHUB_EVENT_NAME": "pull_request",
            "GITHUB_REPOSITORY": "eugenemalaschuk-source/arch-linter-net",
            "GITHUB_ACTION_PATH": str(action_path),
            "GITHUB_OUTPUT": str(output),
        }
    )
    environment.pop("GITHUB_TOKEN", None)
    environment.pop("GH_TOKEN", None)

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "tools.badge_promotion.cli",
            "--configuration-id",
            "reference-none",
            "--adapter",
            "none",
            "--operation",
            "integration-test",
        ],
        cwd=repository_root,
        env=environment,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert json.loads(result.stdout) == {"status": "ready", "reason": "runtime_integration_test"}
    assert "status=ready" in output.read_text()
