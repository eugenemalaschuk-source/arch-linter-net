from __future__ import annotations

import urllib.error

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


def test_relay_http_commit_requests_do_not_carry_forged_trusted_context(monkeypatch: pytest.MonkeyPatch) -> None:
    bodies: list[dict[str, object]] = []

    def record(self: HttpRelayClient, operation: str, body: dict[str, object], token: str) -> dict[str, object]:
        bodies.append(body)
        return {"ok": True}

    monkeypatch.setattr(HttpRelayClient, "_post", record)
    client = HttpRelayClient("https://relay.example", "alias", "headline-only/v1")
    kwargs = {
        "challenge_id": "challenge",
        "idempotency_key": "key",
        "generation": 4,
        "revocation_epoch": 2,
        "oidc_token": "token",
        "semantic_horizon": "2026-09-12T11:00:00Z",
        "tree_sha": "c" * 40,
    }
    client.publish(b"{}", "a" * 64, **kwargs)
    client.renew(b"{}", "a" * 64, **kwargs)
    assert all("trusted_context" not in body for body in bodies)


@pytest.mark.parametrize(
    ("status", "reason"),
    [
        (401, "relay_authorization_rejected"),
        (403, "relay_authorization_rejected"),
        (409, "relay_cas_conflict"),
    ],
)
def test_relay_http_statuses_become_redacted_adapter_diagnostics(
    monkeypatch: pytest.MonkeyPatch, status: int, reason: str
) -> None:
    def reject(*_: object, **__: object) -> None:
        raise urllib.error.HTTPError("https://relay.example", status, "private detail", {}, None)

    monkeypatch.setattr("urllib.request.urlopen", reject)
    client = HttpRelayClient("https://relay.example", "alias", "headline-only/v1")
    with pytest.raises(AdapterError) as error:
        client.publish(
            b"{}",
            "a" * 64,
            challenge_id="challenge",
            idempotency_key="key",
            generation=4,
            revocation_epoch=2,
            oidc_token="token",
            semantic_horizon="2026-09-12T11:00:00Z",
        )
    assert error.value.reason == reason
    assert str(error.value) == reason
