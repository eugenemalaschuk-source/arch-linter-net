"""Transport-independent trusted Architecture Health badge promotion."""

from .artifact import ArtifactValidationError, ValidatedArtifact, validate_artifact
from .adapters import AdapterError, NoneAdapter, RelayAdapter, issue_github_oidc_token, require_supported_adapter
from .config import ConfigValidationError, PromotionConfig, parse_config
from .decision import (
    DecisionDisposition,
    PromotionDecision,
    PromotionRequest,
    PromotionState,
    decide_promotion,
)
from .model import (
    AdapterKind,
    EvidenceContext,
    PromotionStatus,
    ReasonCode,
)

__all__ = [
    "AdapterKind",
    "AdapterError",
    "ArtifactValidationError",
    "ConfigValidationError",
    "DecisionDisposition",
    "EvidenceContext",
    "PromotionConfig",
    "PromotionDecision",
    "PromotionRequest",
    "PromotionState",
    "PromotionStatus",
    "NoneAdapter",
    "RelayAdapter",
    "issue_github_oidc_token",
    "ReasonCode",
    "ValidatedArtifact",
    "decide_promotion",
    "parse_config",
    "require_supported_adapter",
    "validate_artifact",
]
