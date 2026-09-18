"""Resolve setup-generated *data* from the exact caller commit, never its code.

The caller's protected-branch registry is reviewed configuration authority. The
ordinary promotion resolver still verifies the squash, required check, producer,
artifact and semantic horizon before any transport receives a payload.
"""
from __future__ import annotations

import base64
from collections.abc import Callable, Mapping
import hashlib
from typing import Any
from urllib.parse import quote

from .config import ConfigValidationError, _parse_base_ref, _raw_config, parse_config
from .model import PromotionConfig, is_repository, is_sha1

REGISTRY_PATH = ".github/badge-promotion/registry.json"
_MAX_REGISTRY_BYTES = 65_536
_REGISTRY_SCHEMA = "architecture-health-badge-promotion/registry/v1"


def _context(environ: Mapping[str, str]) -> tuple[str, str, str]:
    repository = environ.get("GITHUB_REPOSITORY", "")
    sha = environ.get("GITHUB_SHA", "")
    ref = environ.get("GITHUB_REF", "")
    if (
        environ.get("GITHUB_EVENT_NAME") not in {"push", "schedule"}
        or not is_repository(repository)
        or any(part in {".", ".."} for part in repository.split("/"))
        or not is_sha1(sha)
        or not ref.startswith("refs/heads/")
    ):
        raise ConfigValidationError("unsupported setup registry context")
    return repository, sha, _parse_base_ref(ref.removeprefix("refs/heads/"))


def _child_entry_sha(
    request: Callable[[str], Any], repository_path: str, tree_sha: str, part: str, *, leaf: bool
) -> str:
    document = request(f"/repos/{repository_path}/git/trees/{tree_sha}")
    if not isinstance(document, dict) or document.get("sha") != tree_sha or document.get("truncated") is not False:
        raise ConfigValidationError("invalid setup registry tree")
    entries = document.get("tree")
    if not isinstance(entries, list):
        raise ConfigValidationError("invalid setup registry entries")
    matches = [entry for entry in entries if isinstance(entry, dict) and entry.get("path") == part]
    if len(matches) != 1 or not is_sha1(matches[0].get("sha")):
        raise ConfigValidationError("ambiguous setup registry path")
    entry = matches[0]
    if entry.get("type") != ("blob" if leaf else "tree") or entry.get("mode") not in ({"100644", "100755"} if leaf else {"040000"}):
        raise ConfigValidationError("setup registry path is not regular")
    return entry["sha"]


def _registry_blob(request: Callable[[str], Any], repository_path: str, sha: str) -> str:
    commit = request(f"/repos/{repository_path}/git/commits/{sha}")
    tree = commit.get("tree") if isinstance(commit, dict) else None
    tree_sha = tree.get("sha") if isinstance(tree, dict) else None
    if not isinstance(commit, dict) or commit.get("sha") != sha or not is_sha1(tree_sha):
        raise ConfigValidationError("invalid setup registry commit")
    parts = REGISTRY_PATH.split("/")
    for index, part in enumerate(parts):
        tree_sha = _child_entry_sha(request, repository_path, tree_sha, part, leaf=index == len(parts) - 1)
    return tree_sha


def _registry_bytes(document: Any, expected_blob: str) -> bytes:
    if (
        not isinstance(document, dict)
        or document.get("type") != "file"
        or document.get("path") != REGISTRY_PATH
        or document.get("encoding") != "base64"
        or "target" in document
        or document.get("submodule_git_url") is not None
    ):
        raise ConfigValidationError("setup registry is not a regular file")
    size = document.get("size")
    content = document.get("content")
    if (
        isinstance(size, bool)
        or not isinstance(size, int)
        or not 0 < size <= _MAX_REGISTRY_BYTES
        or not isinstance(content, str)
        or len(content) > 2 * _MAX_REGISTRY_BYTES
        or document.get("sha") != expected_blob
    ):
        raise ConfigValidationError("invalid setup registry size or identity")
    try:
        data = base64.b64decode(content.replace("\n", "").replace("\r", ""), validate=True)
    except ValueError as error:
        raise ConfigValidationError("invalid setup registry encoding") from error
    if len(data) != size:
        raise ConfigValidationError("setup registry size mismatch")
    # Git object identity, not a signature or a replacement for GitHub authority.
    blob = hashlib.sha1(f"blob {len(data)}\0".encode("ascii") + data, usedforsecurity=False).hexdigest()
    if blob != document["sha"]:
        raise ConfigValidationError("setup registry blob mismatch")
    return data


def _configuration(data: bytes) -> PromotionConfig:
    try:
        registry = _raw_config(data.decode("utf-8"))
        if not isinstance(registry, dict) or set(registry) != {"schema", "configurations"}:
            raise ConfigValidationError("invalid setup registry envelope")
        configurations = registry["configurations"]
        if registry["schema"] != _REGISTRY_SCHEMA or not isinstance(configurations, dict):
            raise ConfigValidationError("unsupported setup registry schema")
        return parse_config(configurations["setup_generated"])
    except (KeyError, ValueError, RecursionError) as error:
        raise ConfigValidationError("invalid setup registry") from error


def load_setup_registry(request: Callable[[str], Any], environ: Mapping[str, str]) -> PromotionConfig:
    """Read one fixed JSON file at a platform-supplied immutable caller revision.

    No URL, path, producer selector or revision is accepted from workflow inputs.
    There is deliberately no workspace, branch-tip or upstream-registry fallback.
    """
    repository, sha, base_ref = _context(environ)
    repository_path = quote(repository, safe="/")
    blob_sha = _registry_blob(request, repository_path, sha)
    document = request(f"/repos/{repository_path}/contents/{REGISTRY_PATH}?ref={sha}")
    config = _configuration(_registry_bytes(document, blob_sha))
    if config.repository != repository or config.base_ref != base_ref:
        raise ConfigValidationError("setup registry does not match caller context")
    metadata = request(f"/repos/{repository_path}")
    if (
        not isinstance(metadata, dict)
        or metadata.get("full_name") != repository
        or not isinstance(metadata.get("private"), bool)
        or metadata["private"] != (config.repository_visibility == "private")
    ):
        raise ConfigValidationError("setup registry does not match repository visibility")
    return config
