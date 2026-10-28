"""Read-only runtime integration operation and its trust-root checks."""

from __future__ import annotations

import json
import os
from pathlib import Path
import sys
from typing import Any, Callable

from .adapters import AdapterError
from .errors import ProviderFailure, report_read_only_failure

GITHUB_DIRECTORY = ".github"


def _check_trusted_action_root() -> None:
    action_path_value = os.environ.get("GITHUB_ACTION_PATH", "")
    try:
        action_path = Path(action_path_value).resolve(strict=True)
        action_root = action_path.parents[2]
        module_root = Path(__file__).resolve().parents[2]
    except (OSError, IndexError) as error:
        raise ProviderFailure("runtime_test_action_path_invalid") from error

    expected_action_path = action_root / GITHUB_DIRECTORY / "actions" / "architecture-health-badge-promotion"
    if (
        action_root != module_root
        or action_path != expected_action_path.resolve()
        or not (action_path / "action.yml").is_file()
        or not (module_root / GITHUB_DIRECTORY / "badge-promotion" / "registry.json").is_file()
    ):
        raise ProviderFailure("runtime_test_action_path_invalid")


def run_runtime_integration_test(
    configuration_id: str,
    adapter: str,
    *,
    load_config: Callable[[str], Any],
    write: Callable[[dict[str, object]], None],
) -> int:
    """Check the trusted action installation without API or publication access."""
    try:
        if configuration_id != "reference-none" or adapter != "none":
            raise ProviderFailure("runtime_test_configuration_invalid")
        config = load_config(configuration_id)
        if config.destination.adapter.value != "none":
            raise ProviderFailure("runtime_test_configuration_invalid")
        if os.environ.get("GITHUB_EVENT_NAME") != "pull_request":
            raise ProviderFailure("runtime_test_event_invalid")
        if os.environ.get("GITHUB_REPOSITORY") != config.repository:
            raise ProviderFailure("runtime_test_repository_invalid")
        _check_trusted_action_root()
        output = {"status": "ready", "reason": "runtime_integration_test"}
        write(output)
        print(json.dumps(output, separators=(",", ":")))
        return 0
    except (ProviderFailure, AdapterError) as error:
        return report_read_only_failure(error, write)
