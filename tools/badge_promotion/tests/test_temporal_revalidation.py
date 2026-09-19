from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli  # noqa: E402
from badge_promotion.cli import _run_temporal_revalidation, _validate_temporal_receipt  # noqa: E402


def test_temporal_receipt_validator_accepts_cross_midnight_bound_identity() -> None:
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
    captured: dict[str, object] = {}

    def fake_run(command, **kwargs):
        captured["command"] = command
        captured["input"] = Path(command[command.index("--input") + 1]).read_bytes()
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
    assert command[:4] == ["dotnet", "run", "--project", str(tmp_path / "ArchLinterNet.Cli.csproj")]
    assert command[4:8] == ["--configuration", "Release", "--no-build", "--no-restore"]
    assert command[-2:] == ["--producer-identity-sha256", "d" * 64]
    assert captured["input"] == b"exact-health-bytes"
    assert parsed == receipt
    assert horizon == datetime(2026, 9, 20, tzinfo=timezone.utc)
