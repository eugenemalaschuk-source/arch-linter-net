from __future__ import annotations

import hashlib
import io
import json
from datetime import datetime, timedelta, timezone
from dataclasses import replace
from pathlib import Path
import sys
import zipfile

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion.artifact import ArtifactValidationError, validate_artifact  # noqa: E402
from badge_promotion.config import ConfigValidationError, parse_config  # noqa: E402
from badge_promotion.decision import (  # noqa: E402
    DecisionDisposition,
    PromotionRequest,
    PromotionState,
    decide_promotion,
)
from badge_promotion.model import EvidenceContext, PromotionStatus, ReasonCode  # noqa: E402


ROOT = Path(__file__).parent
CONFIG = parse_config((ROOT / "fixtures" / "approved-config.json").read_bytes())
NOW = datetime(2026, 9, 12, 10, 0, tzinfo=timezone.utc)
BASE_SHA = "a" * 40
HEAD_SHA = "b" * 40
TREE_SHA = "c" * 40


def evidence(**changes: object) -> EvidenceContext:
    values: dict[str, object] = {
        "repository": CONFIG.repository,
        "base_ref": "main",
        "base_sha": BASE_SHA,
        "main_tree_sha": TREE_SHA,
        "head_sha": HEAD_SHA,
        "head_tree_sha": TREE_SHA,
        "pr_number": 42,
        "event": "pull_request",
        "merged": True,
        "workflow_path": CONFIG.producer.workflow_path,
        "workflow_sha": CONFIG.producer.workflow_sha,
        "check_name": CONFIG.producer.check_name,
        "check_app": CONFIG.producer.check_app,
        "check_status": "completed",
        "check_conclusion": "success",
        "required_gate_present": True,
        "run_id": 7001,
        "run_attempt": 2,
        "job_id": 8001,
        "job_name": CONFIG.producer.job_name,
        "artifact_id": 9001,
        "artifact_name": CONFIG.producer.artifact_name,
        "artifact_size": 1,
        "artifact_expired": False,
        "verified_at": NOW - timedelta(minutes=5),
        "semantic_horizon": NOW + timedelta(minutes=45),
    }
    values.update(changes)
    return EvidenceContext(**values)  # type: ignore[arg-type]


def payload_bytes() -> bytes:
    return b'{"schemaVersion":1,"label":"architecture","message":"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules","color":"brightgreen"}'


def archive_bytes(*, payload: bytes | None = None, manifest: dict[str, object] | None = None, names: tuple[str, str] | None = None, symlink: bool = False) -> bytes:
    payload = payload or payload_bytes()
    current = evidence()
    manifest = manifest or {
        "schema": CONFIG.schema_id,
        "kind": "architecture-health-badge",
        "context": {
            "repository": current.repository,
            "base_ref": current.base_ref,
            "base_sha": current.base_sha,
            "main_tree_sha": current.main_tree_sha,
            "head_sha": current.head_sha,
            "head_tree_sha": current.head_tree_sha,
            "pr_number": current.pr_number,
            "workflow_path": current.workflow_path,
            "workflow_sha": current.workflow_sha,
            "check_name": current.check_name,
            "check_app": current.check_app,
            "run_id": current.run_id,
            "run_attempt": current.run_attempt,
            "job_id": current.job_id,
            "job_name": current.job_name,
            "artifact_id": current.artifact_id,
            "artifact_name": current.artifact_name,
            "semantic_horizon": current.semantic_horizon.strftime("%Y-%m-%dT%H:%M:%SZ"),
        },
        "payload": {
            "path": CONFIG.producer.payload_path,
            "bytes": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
        },
    }
    first, second = names or (CONFIG.producer.payload_path, "architecture-health-badge.manifest.json")
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as output:
        payload_info = zipfile.ZipInfo(first)
        if symlink:
            payload_info.external_attr = (0o120777 << 16) | 0xA0000000
        output.writestr(payload_info, payload)
        output.writestr(second, json.dumps(manifest, separators=(",", ":")).encode())
    return stream.getvalue()


def request(archive: bytes | None = None, **changes: object) -> PromotionRequest:
    archive_was_supplied = archive is not None
    archive = archive or archive_bytes()
    current = changes.setdefault("evidence", evidence(artifact_size=len(archive)))
    if not archive_was_supplied and isinstance(current, EvidenceContext):
        changes["evidence"] = replace(current, artifact_size=len(archive))
    return PromotionRequest(
        archive=archive,
        generation=changes.pop("generation", 1),
        revocation_epoch=changes.pop("revocation_epoch", 0),
        idempotency_key=changes.pop("idempotency_key", "publish-1"),
        deadline=changes.pop("deadline", NOW + timedelta(minutes=5)),
        now=changes.pop("now", NOW),
        **changes,
    )


def test_config_parser_accepts_approved_shape_and_rejects_arbitrary_destination() -> None:
    assert CONFIG.destination.adapter.value == "none"
    with pytest.raises(ConfigValidationError):
        parse_config('{"schema_id":"architecture-health-badge-promotion/v1","repository":"a/b","repository_visibility":"public","base_ref":"main","producer":{},"destination":{"adapter":"relay","url":"https://evil.invalid"},"disclosure_profile":"headline-only/v1","validity":{"max_lease_seconds":1},"limits":{"max_archive_bytes":1,"max_member_bytes":1,"max_payload_bytes":1,"max_members":2}}')


def test_config_parser_rejects_duplicate_keys_and_private_raw() -> None:
    duplicate = '{"schema_id":"architecture-health-badge-promotion/v1","schema_id":"other"}'
    with pytest.raises(ConfigValidationError, match="duplicate"):
        parse_config(duplicate)
    raw = json.loads((ROOT / "fixtures" / "approved-config.json").read_text())
    raw["repository_visibility"] = "private"
    raw["destination"] = {"adapter": "github-raw", "branch": "architecture-health-badge", "endpoint_path": "architecture-health.json"}
    with pytest.raises(ConfigValidationError, match="private"):
        parse_config(raw)


def test_config_parser_accepts_an_approved_non_main_base_ref() -> None:
    raw = json.loads((ROOT / "fixtures" / "approved-config.json").read_text())
    raw["base_ref"] = "develop"
    assert parse_config(raw).base_ref == "develop"


def test_valid_artifact_returns_exact_canonical_bytes() -> None:
    artifact = validate_artifact(archive_bytes(), CONFIG, evidence())
    assert artifact.payload == payload_bytes()
    assert artifact.payload_sha256 == hashlib.sha256(payload_bytes()).hexdigest()


@pytest.mark.parametrize(
    "mutator, message",
    [
        (lambda data: archive_bytes(names=("../payload.json", "architecture-health-badge.manifest.json")), "members"),
        (lambda data: archive_bytes(symlink=True), "regular file"),
        (lambda data: archive_bytes(payload=b"{\xff"), "payload"),
        (lambda data: archive_bytes(payload=b'{"schemaVersion":1,"schemaVersion":1,"label":"architecture","message":"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules","color":"brightgreen"}'), "duplicate"),
        (lambda data: archive_bytes(payload=b'{"schemaVersion":1,"label":"architecture","message":"PASS \\u00b7 HEALTHY \\u00b7 0 ignores \\u00b7 42 rules","color":"brightgreen"}'), "canonical"),
    ],
)
def test_hostile_artifact_fails_closed(mutator, message: str) -> None:
    with pytest.raises(ArtifactValidationError, match=message):
        validate_artifact(mutator(None), CONFIG, evidence())


def test_manifest_digest_and_provenance_are_bound_to_exact_evidence() -> None:
    artifact = archive_bytes()
    wrong = evidence(run_attempt=3)
    with pytest.raises(ArtifactValidationError, match="provenance"):
        validate_artifact(artifact, CONFIG, wrong)
    bad_manifest = json.loads(json.dumps({}))
    bad_manifest = {"schema": CONFIG.schema_id, "kind": "architecture-health-badge", "context": {}, "payload": {}}
    with pytest.raises(ArtifactValidationError):
        validate_artifact(archive_bytes(manifest=bad_manifest), CONFIG, evidence())


@pytest.mark.parametrize("field", ["base_sha", "head_sha", "head_tree_sha", "run_id", "run_attempt", "job_id", "artifact_id"])
def test_manifest_cannot_mix_provenance_from_another_attempt_or_tree(field: str) -> None:
    current = evidence()
    replacement: object = ("d" * 40 if field.endswith("sha") else 9999)
    # Slots-backed records have no mutable dictionary; construct the replacement explicitly.
    values = {name: getattr(current, name) for name in EvidenceContext.__dataclass_fields__}
    values[field] = replacement
    mismatched = EvidenceContext(**values)
    result = decide_promotion(CONFIG, request(evidence=mismatched))
    assert result.status is PromotionStatus.UNAVAILABLE
    assert result.should_commit


def test_artifact_size_is_bound_to_the_provider_metadata() -> None:
    archive = archive_bytes()
    result = decide_promotion(CONFIG, request(archive, evidence=evidence(artifact_size=len(archive) + 1)))
    assert result.reason is ReasonCode.ARTIFACT_MISMATCH


def test_decision_accepts_ready_and_does_not_recalculate_payload() -> None:
    archive = archive_bytes()
    result = decide_promotion(CONFIG, request(archive))
    assert result.status is PromotionStatus.READY
    assert result.reason is ReasonCode.READY
    assert result.payload == payload_bytes()
    assert result.valid_until == NOW + timedelta(minutes=45)


@pytest.mark.parametrize(
    "change, reason",
    [
        ({"evidence": evidence(repository="other/repo", artifact_size=1)}, ReasonCode.REPOSITORY_MISMATCH),
        ({"evidence": evidence(base_ref="develop", artifact_size=1)}, ReasonCode.BASE_REF_MISMATCH),
        ({"evidence": evidence(event="push", artifact_size=1)}, ReasonCode.DIRECT_PUSH),
        ({"evidence": evidence(required_gate_present=False, artifact_size=1)}, ReasonCode.REQUIRED_GATE_MISSING),
        ({"evidence": evidence(run_attempt=3, artifact_size=1)}, ReasonCode.ATTEMPT_MISMATCH),
        ({"cancelled": True}, ReasonCode.CANCELLED),
    ],
)
def test_wrong_provenance_and_cancelled_requests_become_unavailable(change, reason: ReasonCode) -> None:
    result = decide_promotion(CONFIG, request(**change))
    assert result.status is PromotionStatus.UNAVAILABLE
    assert result.reason is reason
    assert result.should_commit


def test_expired_deadline_and_semantic_horizon_fail_closed() -> None:
    expired = decide_promotion(CONFIG, request(deadline=NOW))
    assert expired.reason is ReasonCode.CHALLENGE_DEADLINE_EXPIRED
    horizon = decide_promotion(CONFIG, request(evidence=evidence(semantic_horizon=NOW)))
    assert horizon.reason is ReasonCode.SEMANTIC_HORIZON_EXPIRED


def test_stale_writer_and_generation_gap_cannot_overwrite_newer_state() -> None:
    previous = PromotionState(4, 2, PromotionStatus.READY, TREE_SHA, "d" * 64, NOW + timedelta(minutes=10), "publish-4")
    stale = decide_promotion(CONFIG, request(generation=3, revocation_epoch=2), previous)
    assert stale.reason is ReasonCode.STALE_GENERATION
    assert stale.disposition is DecisionDisposition.NOOP
    gap = decide_promotion(CONFIG, request(generation=6, revocation_epoch=2), previous)
    assert gap.reason is ReasonCode.GENERATION_GAP
    assert not gap.should_commit


def test_same_generation_is_idempotent_only_for_same_key_and_digest() -> None:
    digest = hashlib.sha256(payload_bytes()).hexdigest()
    previous = PromotionState(1, 0, PromotionStatus.READY, TREE_SHA, digest, NOW + timedelta(minutes=10), "publish-1")
    replay = decide_promotion(CONFIG, request(generation=1), previous)
    assert replay.reason is ReasonCode.IDEMPOTENT_REPLAY
    assert replay.disposition is DecisionDisposition.NOOP
    conflict = decide_promotion(CONFIG, request(generation=1, idempotency_key="other"), previous)
    assert conflict.reason is ReasonCode.IDEMPOTENCY_CONFLICT
    assert not conflict.should_commit
