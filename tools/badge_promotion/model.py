"""Typed values shared by the badge-promotion contract."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from enum import StrEnum
import re
from typing import Any, Mapping


SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SHA1_PATTERN = re.compile(r"^[0-9a-f]{40}$")
REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
WORKFLOW_PATH_PATTERN = re.compile(r"^\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml$")
SAFE_NAME_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,127}$")
ALIAS_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]{0,31}$")


class AdapterKind(StrEnum):
    GITHUB_RAW = "github-raw"
    RELAY = "relay"
    NONE = "none"


class PromotionStatus(StrEnum):
    READY = "ready"
    PRIVATE = "private"
    UNAVAILABLE = "unavailable"


class ReasonCode(StrEnum):
    READY = "ready"
    IDEMPOTENT_REPLAY = "idempotent_replay"
    CONFIG_INVALID = "config_invalid"
    REPOSITORY_MISMATCH = "repository_mismatch"
    BASE_REF_MISMATCH = "base_ref_mismatch"
    MERGE_PROVENANCE_INVALID = "merge_provenance_invalid"
    DIRECT_PUSH = "direct_push"
    REQUIRED_GATE_MISSING = "required_gate_missing"
    CHECK_MISMATCH = "check_mismatch"
    CHECK_NOT_SUCCESSFUL = "check_not_successful"
    WORKFLOW_MISMATCH = "workflow_mismatch"
    RUN_MISMATCH = "run_mismatch"
    ATTEMPT_MISMATCH = "attempt_mismatch"
    JOB_MISMATCH = "job_mismatch"
    ARTIFACT_MISMATCH = "artifact_mismatch"
    ARTIFACT_EXPIRED = "artifact_expired"
    ARTIFACT_INVALID = "artifact_invalid"
    PAYLOAD_INVALID = "payload_invalid"
    SEMANTIC_HORIZON_EXPIRED = "semantic_horizon_expired"
    CHALLENGE_DEADLINE_EXPIRED = "challenge_deadline_expired"
    CANCELLED = "cancelled"
    STALE_GENERATION = "stale_generation"
    GENERATION_GAP = "generation_gap"
    STALE_REVOCATION_EPOCH = "stale_revocation_epoch"
    IDEMPOTENCY_CONFLICT = "idempotency_conflict"


@dataclass(frozen=True, slots=True)
class ProducerConfig:
    workflow_path: str
    workflow_sha: str
    job_name: str
    check_name: str
    check_app: str
    event: str
    artifact_name: str
    evidence_artifact_name: str
    payload_path: str


@dataclass(frozen=True, slots=True)
class DestinationConfig:
    adapter: AdapterKind
    branch: str | None = None
    endpoint_path: str | None = None
    alias: str | None = None
    endpoint: str | None = None
    audience: str | None = None


@dataclass(frozen=True, slots=True)
class PromotionLimits:
    max_archive_bytes: int
    max_member_bytes: int
    max_payload_bytes: int
    max_members: int
    max_lease_seconds: int


@dataclass(frozen=True, slots=True)
class PromotionConfig:
    repository: str
    repository_visibility: str
    base_ref: str
    producer: ProducerConfig
    destination: DestinationConfig
    disclosure_profile: str
    limits: PromotionLimits
    schema_id: str = "architecture-health-badge-promotion/v1"


@dataclass(frozen=True, slots=True)
class EvidenceContext:
    """Resolved provider facts; callers must obtain these from a trusted resolver."""

    repository: str
    base_ref: str
    base_sha: str
    main_tree_sha: str
    head_sha: str
    head_tree_sha: str
    pr_number: int
    event: str
    merged: bool
    workflow_path: str
    workflow_sha: str
    check_name: str
    check_app: str
    check_status: str
    check_conclusion: str
    required_gate_present: bool
    run_id: int
    run_attempt: int
    job_id: int
    job_name: str
    artifact_id: int
    artifact_name: str
    artifact_size: int
    artifact_expired: bool
    verified_at: datetime
    semantic_horizon: datetime


def is_sha256(value: Any) -> bool:
    return isinstance(value, str) and SHA256_PATTERN.fullmatch(value) is not None


def is_sha1(value: Any) -> bool:
    return isinstance(value, str) and SHA1_PATTERN.fullmatch(value) is not None


def is_repository(value: Any) -> bool:
    return isinstance(value, str) and REPOSITORY_PATTERN.fullmatch(value) is not None


def is_workflow_path(value: Any) -> bool:
    return isinstance(value, str) and WORKFLOW_PATH_PATTERN.fullmatch(value) is not None


def is_safe_name(value: Any) -> bool:
    return isinstance(value, str) and SAFE_NAME_PATTERN.fullmatch(value) is not None


def is_alias(value: Any) -> bool:
    return isinstance(value, str) and ALIAS_PATTERN.fullmatch(value) is not None


def require_utc(value: Any, field: str) -> datetime:
    if not isinstance(value, datetime) or value.tzinfo is None or value.utcoffset() is None:
        raise ValueError(f"{field} must be timezone-aware")
    return value.astimezone(timezone.utc)


def positive_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def non_negative_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def exact_keys(value: Mapping[str, Any], expected: set[str]) -> bool:
    return set(value) == expected
