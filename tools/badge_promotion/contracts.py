"""Transport/provider seams for the pure promotion core."""

from __future__ import annotations

from typing import Protocol

from .decision import PromotionDecision, PromotionRequest
from .model import EvidenceContext


class EvidenceResolver(Protocol):
    def resolve(self, request: PromotionRequest) -> EvidenceContext:
        """Return provider facts; the resolver owns provider/API capability handling."""


class PromotionTransport(Protocol):
    def commit(self, decision: PromotionDecision) -> None:
        """Conditionally persist an already-decided result."""
