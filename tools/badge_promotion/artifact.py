"""Bounded, side-effect-free validation of the untrusted badge artifact."""

from __future__ import annotations

import hashlib
import io
import json
from dataclasses import dataclass
from datetime import timezone
from pathlib import PurePosixPath
import re
import stat
import zipfile
from typing import Any, Mapping

from .model import EvidenceContext, PromotionConfig, ReasonCode, SHA256_PATTERN


class ArtifactValidationError(ValueError):
    """Raised for any untrusted archive, manifest, or disclosure violation."""

    def __init__(self, message: str, reason: ReasonCode | None = None) -> None:
        super().__init__(message)
        self.reason = reason


@dataclass(frozen=True, slots=True)
class ValidatedArtifact:
    payload: bytes
    payload_sha256: str
    manifest: Mapping[str, Any]


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ArtifactValidationError("duplicate JSON key")
        result[key] = value
    return result


def _parse_json(data: bytes, label: str) -> dict[str, Any]:
    try:
        text = data.decode("utf-8", errors="strict")
        value = json.loads(
            text,
            object_pairs_hook=_reject_duplicate_keys,
            parse_constant=lambda _: (_ for _ in ()).throw(ArtifactValidationError("non-finite JSON value")),
        )
    except ArtifactValidationError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ArtifactValidationError(f"{label} is not valid UTF-8 JSON") from error
    if not isinstance(value, dict):
        raise ArtifactValidationError(f"{label} must be a JSON object")
    return value


def _safe_member(info: zipfile.ZipInfo, max_member_bytes: int) -> None:
    path = PurePosixPath(info.filename)
    if (
        not info.filename
        or "\\" in info.filename
        or "\x00" in info.filename
        or path.is_absolute()
        or any(part in {"", ".", ".."} for part in path.parts)
        or info.is_dir()
        or info.flag_bits & 0x1
    ):
        raise ArtifactValidationError("unsafe archive member")
    mode = (info.external_attr >> 16) & 0xFFFF
    file_type = stat.S_IFMT(mode)
    if file_type not in {0, stat.S_IFREG}:
        raise ArtifactValidationError("archive member is not a regular file")
    if info.file_size > max_member_bytes or info.compress_size > max_member_bytes:
        raise ArtifactValidationError("archive member exceeds its bound")


def _manifest_context(manifest: Mapping[str, Any]) -> Mapping[str, Any]:
    context = manifest.get("context")
    if not isinstance(context, dict):
        raise ArtifactValidationError("manifest context is invalid")
    required = {"repository", "base_ref", "base_sha", "head_sha", "head_tree_sha", "pr_number", "run_id", "run_attempt"}
    optional = {"main_tree_sha", "workflow_path", "workflow_sha", "check_name", "check_app", "job_id", "job_name", "artifact_id", "artifact_name", "semantic_horizon"}
    if not required.issubset(context) or set(context) - required - optional:
        raise ArtifactValidationError("manifest context shape is invalid")
    return context


def _validate_manifest(manifest: Mapping[str, Any], config: PromotionConfig, evidence: EvidenceContext, payload: bytes) -> None:
    if set(manifest) != {"schema", "kind", "context", "payload"} or manifest.get("schema") != config.schema_id or manifest.get("kind") != "architecture-health-badge":
        raise ArtifactValidationError("manifest shape is invalid")
    context = _manifest_context(manifest)
    expected_context = {
        "repository": evidence.repository, "base_ref": evidence.base_ref, "base_sha": evidence.base_sha,
        "head_sha": evidence.head_sha, "head_tree_sha": evidence.head_tree_sha, "pr_number": evidence.pr_number,
        "run_id": evidence.run_id, "run_attempt": evidence.run_attempt,
    }
    optional_context = {
        "main_tree_sha": evidence.main_tree_sha, "workflow_path": evidence.workflow_path, "workflow_sha": evidence.workflow_sha,
        "check_name": evidence.check_name, "check_app": evidence.check_app, "job_id": evidence.job_id, "job_name": evidence.job_name,
        "artifact_id": evidence.artifact_id, "artifact_name": evidence.artifact_name,
        "semantic_horizon": evidence.semantic_horizon.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    expected_context.update({key: value for key, value in optional_context.items() if key in context})
    if dict(context) != expected_context:
        reason_by_field = {
            "workflow_path": ReasonCode.WORKFLOW_MISMATCH,
            "workflow_sha": ReasonCode.WORKFLOW_MISMATCH,
            "check_name": ReasonCode.CHECK_MISMATCH,
            "check_app": ReasonCode.CHECK_MISMATCH,
            "run_id": ReasonCode.RUN_MISMATCH,
            "run_attempt": ReasonCode.ATTEMPT_MISMATCH,
            "job_id": ReasonCode.JOB_MISMATCH,
            "job_name": ReasonCode.JOB_MISMATCH,
            "artifact_id": ReasonCode.ARTIFACT_MISMATCH,
            "artifact_name": ReasonCode.ARTIFACT_MISMATCH,
        }
        reason = next((reason_by_field[key] for key in expected_context if context.get(key) != expected_context[key] and key in reason_by_field), None)
        raise ArtifactValidationError("manifest provenance binding is invalid", reason)
    payload_ref = manifest.get("payload")
    if not isinstance(payload_ref, dict) or set(payload_ref) != {"path", "bytes", "sha256"}:
        raise ArtifactValidationError("manifest payload binding is invalid")
    if payload_ref["path"] != config.producer.payload_path or payload_ref["bytes"] != len(payload) or not isinstance(payload_ref["bytes"], int):
        raise ArtifactValidationError("manifest payload size binding is invalid")
    if not isinstance(payload_ref["sha256"], str) or SHA256_PATTERN.fullmatch(payload_ref["sha256"]) is None:
        raise ArtifactValidationError("manifest payload digest is invalid")
    if payload_ref["sha256"] != hashlib.sha256(payload).hexdigest():
        raise ArtifactValidationError("manifest payload digest does not match")


_MESSAGE_PATTERN = re.compile(r"^(PASS|FAIL) · (HEALTHY|DEBT|DEGRADING|FAILING) · ([0-9]{1,4}) ignores · ([0-9]{1,4}) rules$")
_HEALTH_COLORS = {"HEALTHY": "brightgreen", "DEBT": "yellow", "DEGRADING": "orange", "FAILING": "red"}


def _validate_canonical_payload(payload: bytes, max_payload_bytes: int) -> None:
    if len(payload) > max_payload_bytes:
        raise ArtifactValidationError("payload exceeds its bound")
    document = _parse_json(payload, "payload")
    if set(document) != {"schemaVersion", "label", "message", "color"} or document["schemaVersion"] != 1 or document["label"] != "architecture":
        raise ArtifactValidationError("payload shape is not canonical")
    if not isinstance(document["message"], str) or not isinstance(document["color"], str):
        raise ArtifactValidationError("payload values are not canonical")
    match = _MESSAGE_PATTERN.fullmatch(document["message"])
    if match is None or _HEALTH_COLORS[match.group(2)] != document["color"]:
        raise ArtifactValidationError("payload disclosure dictionary is invalid")
    # System.Text.Json emits the canonical disclosure's middle-dot escape with an
    # uppercase hexadecimal digit. Preserve that existing producer convention.
    canonical = json.dumps(document, ensure_ascii=True, separators=(",", ":"), sort_keys=False).replace("\\u00b7", "\\u00B7").encode("utf-8")
    if canonical != payload:
        raise ArtifactValidationError("payload bytes are not canonical")


def validate_artifact(archive: bytes, config: PromotionConfig, evidence: EvidenceContext) -> ValidatedArtifact:
    """Verify and return exact payload bytes; never extracts or rewrites them."""

    if not isinstance(archive, bytes) or len(archive) > config.limits.max_archive_bytes:
        raise ArtifactValidationError("archive exceeds its bound")
    try:
        with zipfile.ZipFile(io.BytesIO(archive)) as opened:
            infos = opened.infolist()
            if len(infos) != config.limits.max_members:
                raise ArtifactValidationError("archive shape is invalid")
            names = [info.filename for info in infos]
            expected = [config.producer.payload_path, "architecture-health-badge.manifest.json"]
            if len(set(names)) != len(names) or sorted(names) != sorted(expected):
                raise ArtifactValidationError("archive members are not approved")
            for info in infos:
                _safe_member(info, config.limits.max_member_bytes)
            payload = opened.read(config.producer.payload_path)
            manifest_bytes = opened.read("architecture-health-badge.manifest.json")
            if len(payload) != next(info.file_size for info in infos if info.filename == config.producer.payload_path):
                raise ArtifactValidationError("payload read size is invalid")
    except ArtifactValidationError:
        raise
    except (OSError, ValueError, zipfile.BadZipFile, zipfile.LargeZipFile) as error:
        raise ArtifactValidationError("archive cannot be safely read") from error
    manifest = _parse_json(manifest_bytes, "manifest")
    _validate_manifest(manifest, config, evidence, payload)
    _validate_canonical_payload(payload, config.limits.max_payload_bytes)
    return ValidatedArtifact(payload, hashlib.sha256(payload).hexdigest(), manifest)
