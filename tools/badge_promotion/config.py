"""Strict parser and validator for approved promotion configuration."""

from __future__ import annotations

import json
from urllib.parse import urlparse
from typing import Any, Mapping

from .model import (
    AdapterKind,
    ALIAS_PATTERN,
    DestinationConfig,
    PromotionConfig,
    PromotionLimits,
    ProducerConfig,
    REPOSITORY_PATTERN,
    SAFE_NAME_PATTERN,
    WORKFLOW_PATH_PATTERN,
)


class ConfigValidationError(ValueError):
    """Raised when configuration is not an approved, bounded shape."""


_SCHEMA_ID = "architecture-health-badge-promotion/v1"
_DISCLOSURE_PROFILE = "headline-only/v1"
_RAW_BRANCH = "architecture-health-badge"
_RAW_ENDPOINT = "architecture-health.json"
_MAX_ARCHIVE_BYTES = 1_048_576
_MAX_MEMBER_BYTES = 65_536
_MAX_PAYLOAD_BYTES = 16_384
_MAX_MEMBERS = 2
_MAX_LEASE_SECONDS = 3_600


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ConfigValidationError("duplicate configuration key")
        result[key] = value
    return result


def _object(value: Any, field: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ConfigValidationError(f"{field} must be an object")
    return value


def _keys(value: Mapping[str, Any], expected: set[str], field: str) -> None:
    if set(value) != expected:
        raise ConfigValidationError(f"{field} has unsupported keys")


def _string(value: Any, field: str, *, pattern: str | None = None) -> str:
    if not isinstance(value, str) or not value:
        raise ConfigValidationError(f"{field} must be a non-empty string")
    if pattern is not None:
        import re

        if re.fullmatch(pattern, value) is None:
            raise ConfigValidationError(f"{field} has an unsupported value")
    return value


def _bounded_int(value: Any, field: str, minimum: int, maximum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
        raise ConfigValidationError(f"{field} is outside its safe bound")
    return value


def parse_config(source: str | bytes | Mapping[str, Any]) -> PromotionConfig:
    """Parse trusted setup JSON without accepting consumer-selected producers."""

    if isinstance(source, Mapping):
        raw: Any = dict(source)
    else:
        try:
            raw = json.loads(source, object_pairs_hook=_reject_duplicate_keys, parse_constant=lambda _: (_ for _ in ()).throw(ConfigValidationError("non-finite JSON value")))
        except ConfigValidationError:
            raise
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise ConfigValidationError("configuration is not valid JSON") from error
    raw = _object(raw, "configuration")
    _keys(
        raw,
        {"schema_id", "repository", "repository_visibility", "base_ref", "producer", "destination", "disclosure_profile", "validity", "limits"},
        "configuration",
    )
    schema_id = _string(raw["schema_id"], "schema_id")
    if schema_id != _SCHEMA_ID:
        raise ConfigValidationError("unsupported configuration schema")
    repository = _string(raw["repository"], "repository", pattern=REPOSITORY_PATTERN.pattern)
    visibility = _string(raw["repository_visibility"], "repository_visibility")
    if visibility not in {"public", "private"}:
        raise ConfigValidationError("repository_visibility is unsupported")
    base_ref = _string(raw["base_ref"], "base_ref")
    if base_ref != "main":
        raise ConfigValidationError("only the approved main base is supported")

    producer_raw = _object(raw["producer"], "producer")
    _keys(producer_raw, {"workflow_path", "workflow_sha", "job_name", "check_name", "check_app", "event", "artifact_name", "evidence_artifact_name", "payload_path"}, "producer")
    workflow_path = _string(producer_raw["workflow_path"], "producer.workflow_path", pattern=WORKFLOW_PATH_PATTERN.pattern)
    workflow_sha = _string(producer_raw["workflow_sha"], "producer.workflow_sha", pattern=r"[0-9a-f]{40}")
    job_name = _string(producer_raw["job_name"], "producer.job_name", pattern=SAFE_NAME_PATTERN.pattern)
    check_name = _string(producer_raw["check_name"], "producer.check_name", pattern=SAFE_NAME_PATTERN.pattern)
    check_app = _string(producer_raw["check_app"], "producer.check_app", pattern=r"[a-z0-9][a-z0-9-]{1,63}")
    event = _string(producer_raw["event"], "producer.event")
    if event != "pull_request":
        raise ConfigValidationError("producer.event must be pull_request")
    artifact_name = _string(producer_raw["artifact_name"], "producer.artifact_name", pattern=SAFE_NAME_PATTERN.pattern)
    evidence_artifact_name = _string(producer_raw["evidence_artifact_name"], "producer.evidence_artifact_name", pattern=SAFE_NAME_PATTERN.pattern)
    payload_path = _string(producer_raw["payload_path"], "producer.payload_path")
    if payload_path != "architecture-health-badge.json":
        raise ConfigValidationError("only the approved payload path is supported")
    producer = ProducerConfig(workflow_path, workflow_sha, job_name, check_name, check_app, event, artifact_name, evidence_artifact_name, payload_path)

    destination_raw = _object(raw["destination"], "destination")
    adapter_value = _string(destination_raw.get("adapter"), "destination.adapter")
    try:
        adapter = AdapterKind(adapter_value)
    except ValueError as error:
        raise ConfigValidationError("destination adapter is unsupported") from error
    if adapter is AdapterKind.NONE:
        _keys(destination_raw, {"adapter"}, "destination")
        destination = DestinationConfig(adapter)
    elif adapter is AdapterKind.GITHUB_RAW:
        _keys(destination_raw, {"adapter", "branch", "endpoint_path"}, "destination")
        if visibility != "public":
            raise ConfigValidationError("private repositories cannot use github-raw")
        if destination_raw["branch"] != _RAW_BRANCH or destination_raw["endpoint_path"] != _RAW_ENDPOINT:
            raise ConfigValidationError("github-raw destination is not approved")
        destination = DestinationConfig(adapter, _RAW_BRANCH, _RAW_ENDPOINT)
    else:
        _keys(destination_raw, {"adapter", "alias", "endpoint", "audience"}, "destination")
        alias = _string(destination_raw["alias"], "destination.alias", pattern=ALIAS_PATTERN.pattern)
        endpoint = _string(destination_raw["endpoint"], "destination.endpoint")
        parsed_endpoint = urlparse(endpoint)
        if parsed_endpoint.scheme != "https" or not parsed_endpoint.netloc or parsed_endpoint.query or parsed_endpoint.fragment:
            raise ConfigValidationError("relay endpoint is not an approved HTTPS origin")
        audience = _string(destination_raw["audience"], "destination.audience")
        destination = DestinationConfig(adapter, alias=alias, endpoint=endpoint.rstrip("/"), audience=audience)

    profile = _string(raw["disclosure_profile"], "disclosure_profile")
    if profile != _DISCLOSURE_PROFILE:
        raise ConfigValidationError("disclosure profile is unsupported")
    validity = _object(raw["validity"], "validity")
    _keys(validity, {"max_lease_seconds"}, "validity")
    max_lease = _bounded_int(validity["max_lease_seconds"], "validity.max_lease_seconds", 1, _MAX_LEASE_SECONDS)
    limits_raw = _object(raw["limits"], "limits")
    _keys(limits_raw, {"max_archive_bytes", "max_member_bytes", "max_payload_bytes", "max_members"}, "limits")
    limits = PromotionLimits(
        _bounded_int(limits_raw["max_archive_bytes"], "limits.max_archive_bytes", 1, _MAX_ARCHIVE_BYTES),
        _bounded_int(limits_raw["max_member_bytes"], "limits.max_member_bytes", 1, _MAX_MEMBER_BYTES),
        _bounded_int(limits_raw["max_payload_bytes"], "limits.max_payload_bytes", 1, _MAX_PAYLOAD_BYTES),
        _bounded_int(limits_raw["max_members"], "limits.max_members", 2, _MAX_MEMBERS),
        max_lease,
    )
    return PromotionConfig(repository, visibility, base_ref, producer, destination, profile, limits, schema_id)
