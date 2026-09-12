"""Pure evidence decision and monotonic publication rules."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from enum import StrEnum

from .artifact import ArtifactValidationError, ValidatedArtifact, validate_artifact
from .model import EvidenceContext, PromotionConfig, PromotionStatus, ReasonCode, is_sha1, positive_int


class DecisionDisposition(StrEnum):
    COMMIT = "commit"
    NOOP = "noop"


@dataclass(frozen=True, slots=True)
class PromotionState:
    generation: int
    revocation_epoch: int
    status: PromotionStatus
    tree_sha: str | None
    payload_sha256: str | None
    valid_until: datetime | None
    idempotency_key: str | None


@dataclass(frozen=True, slots=True)
class PromotionRequest:
    evidence: EvidenceContext
    archive: bytes
    generation: int
    revocation_epoch: int
    idempotency_key: str
    deadline: datetime
    now: datetime
    cancelled: bool = False


@dataclass(frozen=True, slots=True)
class PromotionDecision:
    status: PromotionStatus
    reason: ReasonCode
    disposition: DecisionDisposition
    generation: int
    revocation_epoch: int
    payload: bytes | None = None
    payload_sha256: str | None = None
    valid_until: datetime | None = None

    @property
    def should_commit(self) -> bool:
        return self.disposition is DecisionDisposition.COMMIT


def _unavailable(request: PromotionRequest, reason: ReasonCode, disposition: DecisionDisposition = DecisionDisposition.COMMIT) -> PromotionDecision:
    return PromotionDecision(PromotionStatus.UNAVAILABLE, reason, disposition, request.generation, request.revocation_epoch)


def _context_reason(config: PromotionConfig, evidence: EvidenceContext) -> ReasonCode | None:
    if evidence.repository != config.repository:
        return ReasonCode.REPOSITORY_MISMATCH
    if evidence.base_ref != config.base_ref:
        return ReasonCode.BASE_REF_MISMATCH
    if evidence.event != "pull_request":
        return ReasonCode.DIRECT_PUSH
    if evidence.event != config.producer.event or not evidence.merged or not is_sha1(evidence.base_sha) or not is_sha1(evidence.head_sha) or not is_sha1(evidence.main_tree_sha) or not is_sha1(evidence.head_tree_sha) or evidence.main_tree_sha != evidence.head_tree_sha:
        return ReasonCode.MERGE_PROVENANCE_INVALID
    if not evidence.required_gate_present:
        return ReasonCode.REQUIRED_GATE_MISSING
    if evidence.check_name != config.producer.check_name or evidence.check_app != config.producer.check_app:
        return ReasonCode.CHECK_MISMATCH
    if evidence.check_status != "completed" or evidence.check_conclusion != "success":
        return ReasonCode.CHECK_NOT_SUCCESSFUL
    if evidence.workflow_path != config.producer.workflow_path or evidence.workflow_sha != config.producer.workflow_sha:
        return ReasonCode.WORKFLOW_MISMATCH
    if evidence.run_id <= 0:
        return ReasonCode.RUN_MISMATCH
    if evidence.run_attempt <= 0:
        return ReasonCode.ATTEMPT_MISMATCH
    if evidence.job_name != config.producer.job_name or evidence.job_id <= 0:
        return ReasonCode.JOB_MISMATCH
    if evidence.artifact_name != config.producer.artifact_name or evidence.artifact_id <= 0 or evidence.artifact_size <= 0:
        return ReasonCode.ARTIFACT_MISMATCH
    if evidence.artifact_expired:
        return ReasonCode.ARTIFACT_EXPIRED
    return None


def _validity_reason(request: PromotionRequest) -> ReasonCode | None:
    if request.now.tzinfo is None or request.deadline.tzinfo is None or request.evidence.verified_at.tzinfo is None or request.evidence.semantic_horizon.tzinfo is None:
        return ReasonCode.SEMANTIC_HORIZON_EXPIRED
    now = request.now.astimezone(timezone.utc)
    if now >= request.deadline.astimezone(timezone.utc):
        return ReasonCode.CHALLENGE_DEADLINE_EXPIRED
    if request.evidence.verified_at.astimezone(timezone.utc) > now or now >= request.evidence.semantic_horizon.astimezone(timezone.utc) or request.evidence.verified_at >= request.evidence.semantic_horizon:
        return ReasonCode.SEMANTIC_HORIZON_EXPIRED
    return None


def _monotonic_reason(request: PromotionRequest, previous: PromotionState | None, digest: str | None) -> tuple[ReasonCode | None, DecisionDisposition]:
    if not positive_int(request.generation) or request.revocation_epoch < 0 or not request.idempotency_key or len(request.idempotency_key) > 128:
        return ReasonCode.CONFIG_INVALID, DecisionDisposition.COMMIT
    if previous is None:
        return None, DecisionDisposition.COMMIT
    if request.revocation_epoch < previous.revocation_epoch:
        return ReasonCode.STALE_REVOCATION_EPOCH, DecisionDisposition.NOOP
    if request.generation < previous.generation:
        return ReasonCode.STALE_GENERATION, DecisionDisposition.NOOP
    if request.generation == previous.generation:
        if request.revocation_epoch == previous.revocation_epoch and request.idempotency_key == previous.idempotency_key and digest is None:
            return None, DecisionDisposition.COMMIT
        if request.revocation_epoch == previous.revocation_epoch and request.idempotency_key == previous.idempotency_key and digest == previous.payload_sha256:
            return ReasonCode.IDEMPOTENT_REPLAY, DecisionDisposition.NOOP
        return ReasonCode.IDEMPOTENCY_CONFLICT, DecisionDisposition.NOOP
    if request.revocation_epoch != previous.revocation_epoch:
        return ReasonCode.STALE_REVOCATION_EPOCH, DecisionDisposition.NOOP
    if request.generation != previous.generation + 1:
        return ReasonCode.GENERATION_GAP, DecisionDisposition.NOOP
    return None, DecisionDisposition.COMMIT


def decide_promotion(config: PromotionConfig, request: PromotionRequest, previous: PromotionState | None = None) -> PromotionDecision:
    """Return a ready/unavailable result without performing any transport write."""

    monotonic, disposition = _monotonic_reason(request, previous, None)
    if monotonic is not None:
        return _unavailable(request, monotonic, disposition)
    if request.cancelled:
        return _unavailable(request, ReasonCode.CANCELLED)
    context_reason = _context_reason(config, request.evidence)
    if context_reason is not None:
        return _unavailable(request, context_reason)
    if request.evidence.artifact_size != len(request.archive):
        return _unavailable(request, ReasonCode.ARTIFACT_MISMATCH)
    validity_reason = _validity_reason(request)
    if validity_reason is not None:
        return _unavailable(request, validity_reason)
    try:
        artifact: ValidatedArtifact = validate_artifact(request.archive, config, request.evidence)
    except ArtifactValidationError as error:
        reason = error.reason or (ReasonCode.PAYLOAD_INVALID if "payload" in str(error) else ReasonCode.ARTIFACT_INVALID)
        return _unavailable(request, reason)
    monotonic, disposition = _monotonic_reason(request, previous, artifact.payload_sha256)
    if monotonic is not None:
        if monotonic is ReasonCode.IDEMPOTENT_REPLAY and previous is not None:
            return PromotionDecision(previous.status, monotonic, disposition, previous.generation, previous.revocation_epoch, valid_until=previous.valid_until, payload_sha256=previous.payload_sha256)
        return _unavailable(request, monotonic, disposition)
    valid_until = min(
        request.evidence.verified_at.astimezone(timezone.utc) + timedelta(seconds=config.limits.max_lease_seconds),
        request.evidence.semantic_horizon.astimezone(timezone.utc),
    )
    if request.now.astimezone(timezone.utc) >= valid_until:
        return _unavailable(request, ReasonCode.SEMANTIC_HORIZON_EXPIRED)
    return PromotionDecision(PromotionStatus.READY, ReasonCode.READY, DecisionDisposition.COMMIT, request.generation, request.revocation_epoch, artifact.payload, artifact.payload_sha256, valid_until)
