from __future__ import annotations

import pytest

from badge_promotion.adapters import AdapterError, HttpRelayClient, NoneAdapter, require_supported_adapter
from badge_promotion.decision import DecisionDisposition, PromotionDecision
from badge_promotion.model import AdapterKind, PromotionStatus, ReasonCode


def test_only_three_promotion_adapters_are_supported() -> None:
    assert [require_supported_adapter(value) for value in ("github-raw", "relay", "none")] == [AdapterKind.GITHUB_RAW, AdapterKind.RELAY, AdapterKind.NONE]
    with pytest.raises(AdapterError, match="unsupported"):
        require_supported_adapter("https")


def test_none_adapter_records_a_successful_private_outcome() -> None:
    adapter = NoneAdapter()
    decision = PromotionDecision(PromotionStatus.READY, ReasonCode.READY, DecisionDisposition.COMMIT, 1, 0, b"payload", "a" * 64, None)
    private = adapter.commit(decision)
    assert private.status is PromotionStatus.PRIVATE
    assert private.reason is ReasonCode.READY


def test_relay_prepare_does_not_send_local_cas_defaults(monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[tuple[str, dict[str, object], str]] = []

    def record(self: HttpRelayClient, operation: str, body: dict[str, object], token: str) -> dict[str, object]:
        calls.append((operation, body, token))
        return {"challenge_id": "challenge", "generation": 4, "revocation_epoch": 2}

    monkeypatch.setattr(HttpRelayClient, "_post", record)
    client = HttpRelayClient("https://relay.example", "alias", "headline-only/v1")
    client.prepare(b"{}", "a" * 64, idempotency_key="key", generation=None, revocation_epoch=None, semantic_horizon="2026-09-12T11:00:00Z", oidc_token="token")
    assert calls[0][0] == "prepare"
    assert "expected_generation" not in calls[0][1]
    assert "expected_revocation_epoch" not in calls[0][1]
