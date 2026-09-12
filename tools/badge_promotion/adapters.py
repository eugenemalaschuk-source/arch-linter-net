"""Thin transport seams for the shared promotion decision.

These adapters deliberately accept a typed decision and never calculate or
repair Architecture Health facts. Relay lifecycle/CAS authority remains in the
reference Worker; the workflow supplies the approved deployment binding.
"""

from __future__ import annotations

from dataclasses import dataclass
import os
import json
from typing import Protocol
import urllib.parse
import urllib.request

from .decision import PromotionDecision
from .model import AdapterKind


class AdapterError(RuntimeError):
    pass


class RelayClient(Protocol):
    def prepare(self, payload: bytes, digest: str, *, idempotency_key: str, generation: int, revocation_epoch: int, semantic_horizon: str, oidc_token: str) -> dict[str, object]: ...
    def publish(self, payload: bytes, digest: str, *, challenge_id: str, idempotency_key: str, generation: int, revocation_epoch: int, oidc_token: str, semantic_horizon: str, tree_sha: str | None = None) -> dict[str, object]: ...
    def renew(self, payload: bytes, digest: str, *, challenge_id: str, idempotency_key: str, generation: int, revocation_epoch: int, oidc_token: str, semantic_horizon: str, tree_sha: str | None = None) -> dict[str, object]: ...


def issue_github_oidc_token(audience: str) -> str:
    """Request a fresh, audience-bound GitHub OIDC token for one Relay call."""
    url = os.environ.get("ACTIONS_ID_TOKEN_REQUEST_URL")
    request_token = os.environ.get("ACTIONS_ID_TOKEN_REQUEST_TOKEN")
    if not url or not request_token or not audience or "\n" in audience or "\r" in audience:
        raise AdapterError("oidc_unavailable")
    separator = "&" if "?" in url else "?"
    request = urllib.request.Request(
        f"{url}{separator}{urllib.parse.urlencode({'audience': audience})}",
        headers={"authorization": f"bearer {request_token}", "accept": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            body = response.read(16 * 1024 + 1)
    except OSError as error:
        raise AdapterError("oidc_unavailable") from error
    if len(body) > 16 * 1024:
        raise AdapterError("oidc_token_oversized")
    try:
        token = __import__("json").loads(body.decode("utf-8"))["value"]
    except (UnicodeError, ValueError, KeyError, TypeError) as error:
        raise AdapterError("oidc_response_invalid") from error
    if not isinstance(token, str) or not token:
        raise AdapterError("oidc_response_invalid")
    return token


@dataclass(frozen=True, slots=True)
class HttpRelayClient:
    """Relay protocol client bound to one approved HTTPS origin and alias."""

    endpoint: str
    alias: str
    profile: str

    def _post(self, operation: str, body: dict[str, object], token: str) -> dict[str, object]:
        request = urllib.request.Request(
            f"{self.endpoint}/badge-relay/v1/{self.alias}/{operation}",
            data=json.dumps(body, separators=(",", ":")).encode("utf-8"),
            method="POST",
            headers={"authorization": f"Bearer {token}", "content-type": "application/json", "accept": "application/json"},
        )
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                result = json.loads(response.read().decode("utf-8"))
        except (OSError, UnicodeError, ValueError) as error:
            raise AdapterError("relay_transport_unavailable") from error
        if not isinstance(result, dict) or response.status >= 400:
            raise AdapterError("relay_publication_rejected")
        return result

    def prepare(self, payload: bytes, digest: str, *, idempotency_key: str, generation: int, revocation_epoch: int, semantic_horizon: str, oidc_token: str) -> dict[str, object]:
        return self._post("prepare", {"operation": "prepare", "canonical_bytes": payload.decode("utf-8"), "canonical_digest": digest, "profile": self.profile, "idempotency_key": idempotency_key, "expected_generation": generation, "expected_revocation_epoch": revocation_epoch, "semantic_horizon": semantic_horizon}, oidc_token)

    def publish(self, payload: bytes, digest: str, *, challenge_id: str, idempotency_key: str, generation: int, revocation_epoch: int, oidc_token: str, semantic_horizon: str, tree_sha: str | None = None) -> dict[str, object]:
        return self._post("publish", {"operation": "publish", "challenge_id": challenge_id, "idempotency_key": idempotency_key, "canonical_bytes": payload.decode("utf-8"), "canonical_digest": digest, "profile": self.profile, "expected_generation": generation, "expected_revocation_epoch": revocation_epoch, "semantic_horizon": semantic_horizon, "trusted_context": {"valid": True, "kind": "github-pr-authoritative/v1", "digest": digest, "tree_sha": tree_sha, "semantic_horizon": semantic_horizon}}, oidc_token)

    def renew(self, payload: bytes, digest: str, *, challenge_id: str, idempotency_key: str, generation: int, revocation_epoch: int, oidc_token: str, semantic_horizon: str, tree_sha: str | None = None) -> dict[str, object]:
        body = {"operation": "renew", "challenge_id": challenge_id, "idempotency_key": idempotency_key, "canonical_bytes": payload.decode("utf-8"), "canonical_digest": digest, "profile": self.profile, "expected_generation": generation, "expected_revocation_epoch": revocation_epoch, "semantic_horizon": semantic_horizon, "trusted_context": {"valid": True, "kind": "github-pr-authoritative/v1", "digest": digest, "tree_sha": tree_sha, "semantic_horizon": semantic_horizon}}
        return self._post("renew", body, oidc_token)


@dataclass(frozen=True, slots=True)
class NoneAdapter:
    kind: AdapterKind = AdapterKind.NONE

    def commit(self, decision: PromotionDecision) -> None:
        if decision.status.value == "ready":
            raise AdapterError("none_adapter_cannot_publish")


@dataclass(frozen=True, slots=True)
class RelayAdapter:
    client: RelayClient
    kind: AdapterKind = AdapterKind.RELAY

    def commit(self, decision: PromotionDecision, *, digest: str, idempotency_key: str, challenge_id: str, semantic_horizon: str, tree_sha: str | None = None, renewal: bool = False) -> dict[str, object]:
        if decision.payload is None or decision.generation is None or decision.revocation_epoch is None:
            raise AdapterError("relay_decision_has_no_ready_payload")
        operation = self.client.renew if renewal else self.client.publish
        token = issue_github_oidc_token(os.environ.get("BADGE_RELAY_AUDIENCE", ""))
        return operation(
            decision.payload,
            digest,
            challenge_id=challenge_id,
            idempotency_key=idempotency_key,
            generation=decision.generation,
            revocation_epoch=decision.revocation_epoch,
            oidc_token=token,
            semantic_horizon=semantic_horizon,
            tree_sha=tree_sha,
        )


def require_supported_adapter(value: str) -> AdapterKind:
    try:
        return AdapterKind(value)
    except ValueError as error:
        raise AdapterError("adapter_unsupported") from error
