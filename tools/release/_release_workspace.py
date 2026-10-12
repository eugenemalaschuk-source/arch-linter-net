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
    """Allow paths only under the repository that ships this script.

    The process CWD is caller-controlled. In particular, neither the CWD nor a ``.git`` marker
    discovered above it is a trust anchor; an attacker can choose both. Derive the boundary from
    this module's location so an invocation from ``/`` or a directory with a fake ``.git`` cannot
    broaden the paths accepted by release tooling.
    """
    return (_repository_root(),)


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


def _github_command_file_path(value: Path, description: str, env_var: str) -> Path:
    """Validate a GitHub Actions runner command-file transport path, e.g. $GITHUB_OUTPUT or
    $GITHUB_ENV. These files are created by the runner under its own temp directory, outside the
    repository checkout, so `_safe_path`'s repository-workspace confinement does not (and must
    not) apply to them: a real workflow run would always be rejected. Trust is instead anchored
    to the workflow-provided environment variable itself, not to the CLI argument's label -
    the path is accepted only when it is exactly the transport path the runner supplied via
    `env_var`, never an arbitrary filesystem path merely passed as a `--github-*` argument."""
    resolved = os.path.realpath(str(value))
    trusted = os.environ.get(env_var)
    if not trusted:
        raise ValueError(
            f"The {description} '{value}' cannot be trusted: the {env_var} "
            "environment variable is not set by the runner."
        )
    if resolved != os.path.realpath(trusted):
        raise ValueError(
            f"The {description} '{value}' does not match the runner-provided {env_var} transport path."
        )
    return Path(resolved)


def _github_runner_temp_path(value: Path, description: str, env_var: str) -> Path:
    """Validate a path supplied from the GitHub Actions runner's temp directory.

    Unlike command files, scanner logs may be any file below the runner temp root. Both
    paths are resolved before checking containment so traversal and symlink escapes cannot
    bypass the boundary.
    """
    resolved = os.path.realpath(str(value))
    trusted = os.environ.get(env_var)
    if not trusted:
        raise ValueError(
            f"The {description} '{value}' cannot be trusted: the {env_var} "
            "environment variable is not set by the runner."
        )
    trusted_root = os.path.realpath(trusted)
    try:
        contained = os.path.commonpath([resolved, trusted_root]) == trusted_root
    except ValueError:
        contained = False
    if not contained:
        raise ValueError(
            f"The {description} '{value}' resolves outside the runner-provided {env_var} directory."
        )
    return Path(resolved)
