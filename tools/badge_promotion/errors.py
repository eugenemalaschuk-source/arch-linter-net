"""Shared error and output seams for badge operations."""

from __future__ import annotations

import json
import os
import sys
from typing import Callable

from .adapters import AdapterError


class ProviderFailure(RuntimeError):
    def __init__(self, reason: str, *, status_code: int | None = None) -> None:
        self.reason = reason
        self.status_code = status_code
        super().__init__(reason)


def write_outputs(values: dict[str, object]) -> None:
    target = os.environ.get("GITHUB_OUTPUT")
    if not target:
        return
    with open(target, "a", encoding="utf-8") as output:
        for key, value in values.items():
            output.write(f"{key}={value}\n")


def report_read_only_failure(error: ProviderFailure | AdapterError, write: Callable[[dict[str, object]], None]) -> int:
    reason = error.reason
    write({"status": "unavailable", "reason": reason})
    print(json.dumps({"status": "unavailable", "reason": reason}, separators=(",", ":")), file=sys.stderr)
    return 1
