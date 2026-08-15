#!/usr/bin/env python3
"""Shared release-workspace path confinement, used by every release-authorizing script in this
directory. Kept in one place so the security-relevant sanitizer has a single definition instead of
being copy-pasted per script."""

from __future__ import annotations

import os
from pathlib import Path


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _allowed_roots() -> tuple[Path, ...]:
    """Paths are accepted only inside the working tree or the repository this script ships in, so a
    faulty caller (including an LLM agent invoking this script with a hallucinated path) cannot read
    or write outside the release workspace. Resolved per call rather than at import time, so the
    answer never depends on when the module was loaded."""
    return (Path.cwd().resolve(), _repository_root())


def _safe_path(value: Path, description: str) -> Path:
    resolved = os.path.realpath(str(value))
    for root in _allowed_roots():
        candidate = os.path.realpath(str(root))
        try:
            contained = os.path.commonpath([resolved, candidate]) == candidate
        except ValueError:
            # Different drives on Windows: no common path, so this root does not contain it.
            continue
        if contained:
            return Path(resolved)
    raise ValueError(f"The {description} '{value}' resolves outside the release workspace.")
