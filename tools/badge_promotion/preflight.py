"""Read-only preflight operation execution and failure reporting."""

from __future__ import annotations

import json
import sys
from typing import Any, Callable

from .adapters import AdapterError
from .errors import ProviderFailure, report_read_only_failure
from .model import PromotionStatus


def run_preflight(
    configuration_id: str,
    adapter: str,
    *,
    load_config: Callable[[str], Any],
    validate_invocation: Callable[[Any, str], None],
    resolve_evidence: Callable[..., Any],
    promotion_request: Callable[..., Any],
    decide: Callable[..., Any],
    write: Callable[[dict[str, object]], None],
) -> int:
    """Evaluate fresh evidence without receiving any publication capability."""
    try:
        config = load_config(configuration_id)
        if config.destination.adapter.value != adapter:
            raise ProviderFailure("adapter_configuration_mismatch")
        validate_invocation(config, "preflight")
        evidence, archive = resolve_evidence(config, force_temporal_revalidation=True)
        decision = decide(config, promotion_request(evidence, archive, "preflight"))
        metadata = {
            "reason": decision.reason.value,
            "head_sha": evidence.head_sha,
            "head_tree_sha": evidence.head_tree_sha,
            "run_id": evidence.run_id,
            "run_attempt": evidence.run_attempt,
        }
        temporal_revalidated = evidence.temporal_receipt is not None
        if decision.status is PromotionStatus.UNAVAILABLE:
            write({**metadata, "status": "unavailable", "temporal_revalidated": str(temporal_revalidated).lower()})
            print(json.dumps({"status": "unavailable", "reason": decision.reason.value}, separators=(",", ":")), file=sys.stderr)
            return 1
        if config.destination.adapter.value != "github-raw":
            raise ProviderFailure("preflight_adapter_invalid")
        if not temporal_revalidated:
            raise ProviderFailure("preflight_temporal_revalidation_missing")
        write({**metadata, "status": "ready", "temporal_revalidated": "true"})
        print(json.dumps({"status": "ready", "reason": decision.reason.value, "temporal_revalidated": True}, separators=(",", ":")))
        return 0
    except (ProviderFailure, AdapterError) as error:
        return report_read_only_failure(error, write)
