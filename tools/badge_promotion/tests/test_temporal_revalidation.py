from __future__ import annotations

import json
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli  # noqa: E402
from badge_promotion import decision  # noqa: E402
from badge_promotion.config import parse_config  # noqa: E402
from badge_promotion.cli import _run_temporal_revalidation, _validate_temporal_receipt  # noqa: E402
from badge_promotion.decision import PromotionRequest, decide_promotion  # noqa: E402


def _valid_receipt() -> dict[str, object]:
    return {
        "schema_id": "architecture-health-temporal-publication-receipt/v1",
        "state": "ready",
        "evaluation_date": "2026-09-19",
        "semantic_horizon": "2026-09-20T00:00:00Z",
        "source_health_sha256": "a" * 64,
        "badge_payload_sha256": "b" * 64,
        "merged_tree_sha": "c" * 40,
        "producer_identity_sha256": "d" * 64,
        "reasons": [],
    }


def test_temporal_receipt_validator_accepts_cross_midnight_bound_identity() -> None:
    receipt = _valid_receipt()

    parsed, horizon = _validate_temporal_receipt(
        json.dumps(receipt),
        return_code=0,
        evaluation_date="2026-09-19",
        source_health_sha256="a" * 64,
        badge_payload_sha256="b" * 64,
        merged_tree_sha="c" * 40,
        producer_identity_sha256="d" * 64,
    )

    assert parsed == receipt
    assert horizon == datetime(2026, 9, 20, tzinfo=timezone.utc)


def test_temporal_receipt_validator_rejects_duplicate_json_keys() -> None:
    duplicate = '{"schema_id":"architecture-health-temporal-publication-receipt/v1","schema_id":"duplicate"}'

    with pytest.raises(cli.ProviderFailure, match="semantic_revalidation_unavailable"):
        _validate_temporal_receipt(
            duplicate,
            return_code=0,
            evaluation_date="2026-09-19",
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )


@pytest.mark.parametrize(
    ("change", "reason"),
    [
        (lambda receipt: receipt.pop("reasons"), "semantic_revalidation_unavailable"),
        (lambda receipt: receipt.update(schema_id="wrong"), "semantic_revalidation_unavailable"),
        (lambda receipt: receipt.update(source_health_sha256="e" * 64), "semantic_revalidation_mismatch"),
        (lambda receipt: receipt.update(semantic_horizon="not-a-time"), "semantic_evidence_unavailable"),
        (lambda receipt: receipt.update(state="unassessable"), "semantic_evidence_unavailable"),
        (lambda receipt: receipt.update(reasons=[{"code": "failure"}]), "semantic_evidence_unavailable"),
        (lambda receipt: receipt.update(reasons=["failure"]), "semantic_evidence_unavailable"),
    ],
)
def test_temporal_receipt_validator_rejects_contract_and_state_failures(change, reason: str) -> None:
    receipt = _valid_receipt()
    change(receipt)
    return_code = 1 if reason == "semantic_evidence_unavailable" and receipt["state"] == "ready" else 0

    with pytest.raises(cli.ProviderFailure, match=reason):
        _validate_temporal_receipt(
            json.dumps(receipt),
            return_code=return_code,
            evaluation_date="2026-09-19",
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )


def test_temporal_revalidation_runs_built_release_cli_on_exact_input(
    monkeypatch,
    tmp_path: Path,
) -> None:
    receipt = {
        "schema_id": "architecture-health-temporal-publication-receipt/v1",
        "state": "ready",
        "evaluation_date": "2026-09-19",
        "semantic_horizon": "2026-09-20T00:00:00Z",
        "source_health_sha256": "a" * 64,
        "badge_payload_sha256": "b" * 64,
        "merged_tree_sha": "c" * 40,
        "producer_identity_sha256": "d" * 64,
        "reasons": [],
    }
    captured: dict[str, object] = {"preparation": []}

    def fake_run(command, **kwargs):
        if command[1] == "run":
            captured["command"] = command
            captured["input"] = Path(command[command.index("--input") + 1]).read_bytes()
        else:
            captured["preparation"].append(command)
        return SimpleNamespace(returncode=0, stdout=json.dumps(receipt))

    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR", raising=False)
    monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR_PROJECT", str(tmp_path / "ArchLinterNet.Cli.csproj"))
    monkeypatch.setattr(cli.subprocess, "run", fake_run)

    parsed, horizon = _run_temporal_revalidation(
        b"exact-health-bytes",
        evaluation_date=datetime(2026, 9, 19, 23, 59, tzinfo=timezone.utc),
        source_health_sha256="a" * 64,
        badge_payload_sha256="b" * 64,
        merged_tree_sha="c" * 40,
        producer_identity_sha256="d" * 64,
    )

    command = captured["command"]
    assert isinstance(command, list)
    preparation = captured["preparation"]
    assert preparation == [
        ["dotnet", "restore", str(tmp_path / "ArchLinterNet.Cli.csproj"), "--nologo"],
        ["dotnet", "build", str(tmp_path / "ArchLinterNet.Cli.csproj"), "--configuration", "Release", "--no-restore", "--nologo"],
    ]
    assert command[:4] == ["dotnet", "run", "--project", str(tmp_path / "ArchLinterNet.Cli.csproj")]
    assert command[4:8] == ["--configuration", "Release", "--no-build", "--no-restore"]
    assert command[-2:] == ["--producer-identity-sha256", "d" * 64]
    assert captured["input"] == b"exact-health-bytes"
    assert parsed == receipt
    assert horizon == datetime(2026, 9, 20, tzinfo=timezone.utc)


def test_temporal_revalidation_uses_explicit_command_without_restore_or_build(monkeypatch, tmp_path: Path) -> None:
    receipt = _valid_receipt()
    captured: dict[str, object] = {}

    def fake_run(command, **kwargs):
        captured["command"] = command
        return SimpleNamespace(returncode=0, stdout=json.dumps(receipt))

    monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR", "python trusted-revalidator.py")
    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR_PROJECT", raising=False)
    monkeypatch.setattr(cli.subprocess, "run", fake_run)

    parsed, _ = _run_temporal_revalidation(
        b"exact-health-bytes",
        evaluation_date=datetime(2026, 9, 19, tzinfo=timezone.utc),
        source_health_sha256="a" * 64,
        badge_payload_sha256="b" * 64,
        merged_tree_sha="c" * 40,
        producer_identity_sha256="d" * 64,
    )

    assert parsed == receipt
    assert captured["command"][:3] == ["python", "trusted-revalidator.py", "--"]


def test_temporal_revalidation_fails_closed_when_restore_fails(monkeypatch, tmp_path: Path) -> None:
    calls: list[list[str]] = []

    def fake_run(command, **kwargs):
        calls.append(command)
        return SimpleNamespace(returncode=1, stdout="")

    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR", raising=False)
    monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR_PROJECT", str(tmp_path / "ArchLinterNet.Cli.csproj"))
    monkeypatch.setattr(cli.subprocess, "run", fake_run)

    with pytest.raises(cli.ProviderFailure, match="semantic_revalidation_unavailable"):
        _run_temporal_revalidation(
            b"health",
            evaluation_date=datetime(2026, 9, 19, tzinfo=timezone.utc),
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )

    assert calls == [["dotnet", "restore", str(tmp_path / "ArchLinterNet.Cli.csproj"), "--nologo"]]


def test_temporal_revalidation_fails_closed_on_subprocess_error(monkeypatch, tmp_path: Path) -> None:
    def fail_run(command, **kwargs):
        raise OSError("dotnet unavailable")

    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR", raising=False)
    monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR_PROJECT", str(tmp_path / "ArchLinterNet.Cli.csproj"))
    monkeypatch.setattr(cli.subprocess, "run", fail_run)

    with pytest.raises(cli.ProviderFailure, match="semantic_revalidation_unavailable"):
        _run_temporal_revalidation(
            b"health",
            evaluation_date=datetime(2026, 9, 19, tzinfo=timezone.utc),
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )


@pytest.mark.parametrize("configured", [None, "   "])
def test_temporal_revalidation_requires_a_nonempty_command(monkeypatch, configured: str | None) -> None:
    if configured is None:
        monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR", raising=False)
    else:
        monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR", configured)
    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR_PROJECT", raising=False)

    with pytest.raises(cli.ProviderFailure, match="semantic_revalidation_unavailable"):
        _run_temporal_revalidation(
            b"health",
            evaluation_date=datetime(2026, 9, 19, tzinfo=timezone.utc),
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )


def test_temporal_revalidation_fails_closed_when_revalidator_execution_times_out(monkeypatch, tmp_path: Path) -> None:
    def fake_run(command, **kwargs):
        if command[1] == "run":
            raise cli.subprocess.TimeoutExpired(command, 120)
        return SimpleNamespace(returncode=0, stdout="")

    monkeypatch.delenv("ARCHLINTERNET_REVALIDATOR", raising=False)
    monkeypatch.setenv("ARCHLINTERNET_REVALIDATOR_PROJECT", str(tmp_path / "ArchLinterNet.Cli.csproj"))
    monkeypatch.setattr(cli.subprocess, "run", fake_run)

    with pytest.raises(cli.ProviderFailure, match="semantic_revalidation_unavailable"):
        _run_temporal_revalidation(
            b"health",
            evaluation_date=datetime(2026, 9, 19, tzinfo=timezone.utc),
            source_health_sha256="a" * 64,
            badge_payload_sha256="b" * 64,
            merged_tree_sha="c" * 40,
            producer_identity_sha256="d" * 64,
        )


def test_resolve_evidence_then_decide_uses_post_receipt_lease_anchor(monkeypatch) -> None:
    config = parse_config(json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text()))
    repository = config.repository
    main_sha = "a" * 40
    old_horizon = datetime(2026, 9, 19, tzinfo=timezone.utc)
    refreshed_horizon = datetime.now(timezone.utc) + timedelta(days=1)
    archive = b"exact-badge-archive"
    health = b"exact-health-evidence"

    monkeypatch.setenv("GITHUB_REPOSITORY", repository)
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    monkeypatch.setattr(cli, "_merged_pull_request", lambda *_: ("d" * 40, "b" * 40, "c" * 40, "d" * 40, 42))
    monkeypatch.setattr(cli, "_workflow_blob_sha", lambda *_: config.producer.workflow_sha)
    monkeypatch.setattr(cli, "_successful_check", lambda *_: (15368, {}))
    monkeypatch.setattr(
        cli,
        "_producer_run",
        lambda *_: (
            {"created_at": "2026-09-18T21:38:26Z"},
            7001,
            {"id": 8001, "name": config.producer.job_name, "run_attempt": 1},
        ),
    )
    monkeypatch.setattr(
        cli,
        "_selected_artifact",
        lambda *_: ([], {"id": 9001, "name": config.producer.artifact_name}),
    )
    monkeypatch.setattr(cli, "_read_semantic_evidence", lambda *_: (old_horizon, health, 9002))
    monkeypatch.setattr(cli, "_required_gate", lambda *_: True)
    monkeypatch.setattr(cli, "_run_temporal_revalidation", lambda *_args, **_kwargs: ({"state": "ready"}, refreshed_horizon))
    monkeypatch.setattr(cli, "validate_artifact", lambda *_: SimpleNamespace(payload_sha256="b" * 64))
    monkeypatch.setattr(decision, "validate_artifact", lambda *_: SimpleNamespace(payload=b"payload", payload_sha256="b" * 64))

    class StubApi:
        def download(self, _url: str) -> bytes:
            return archive

    resolved, resolved_archive = cli.resolve_evidence(StubApi(), config)
    assert resolved.verified_at == datetime(2026, 9, 18, 21, 38, 26, tzinfo=timezone.utc)
    assert resolved.temporal_verified_at is not None

    now = resolved.temporal_verified_at + timedelta(seconds=1)
    result = decide_promotion(
        config,
        PromotionRequest(
            evidence=resolved,
            archive=resolved_archive,
            generation=1,
            revocation_epoch=0,
            idempotency_key="issue-978-chain",
            deadline=now + timedelta(minutes=5),
            now=now,
        ),
    )

    assert result.status.value == "ready"
    assert result.valid_until == resolved.temporal_verified_at + timedelta(seconds=config.limits.max_lease_seconds)
