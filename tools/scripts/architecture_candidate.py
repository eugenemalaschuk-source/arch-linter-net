#!/usr/bin/env python3
"""Create and verify the immutable candidate used by architecture governance CI."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath
from typing import Any, Callable

SCHEMA = "architecture-candidate/v1"
DEFAULT_POLICY_PATH = "architecture/dependencies.arch.yml"
DEFAULT_TOOL_IDENTITY = "ArchLinterNet.Cli/ArchLinterNet.Testing"
SHA_PATTERN = re.compile(r"^[0-9a-f]{40,64}$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
MANIFEST_FIELDS = {
    "cli_assembly_path",
    "cli_assembly_sha256",
    "policy_path",
    "policy_sha256",
    "schema",
    "source_sha",
    "testing_assembly_path",
    "testing_assembly_sha256",
    "tool_identity",
    "tree_sha",
}
CHUNK_SIZE = 1024 * 1024


class CandidateIdentityError(ValueError):
    """Raised when a candidate cannot be created or verified safely."""


def canonical_json(value: object) -> str:
    """Return the one deterministic JSON representation used for candidate evidence."""

    return json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(",", ":")) + "\n"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(CHUNK_SIZE), b""):
                digest.update(chunk)
    except (OSError, UnicodeError) as error:
        raise CandidateIdentityError(f"Cannot read candidate input '{path}': {error}") from error
    return digest.hexdigest()


def _git_output(repository_root: Path, *arguments: str, allow_empty: bool = False) -> str:
    try:
        completed = subprocess.run(
            ["git", "-C", str(repository_root), *arguments],
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        raise CandidateIdentityError(f"Cannot read Git candidate identity: {error}") from error
    value = completed.stdout.strip()
    if not value and not allow_empty:
        raise CandidateIdentityError("Git returned an empty candidate identity.")
    return value


def _git_identity(repository_root: Path) -> tuple[str, str]:
    source_sha = _git_output(repository_root, "rev-parse", "HEAD")
    tree_sha = _git_output(repository_root, "rev-parse", "HEAD^{tree}")
    _validate_sha(source_sha, "Git source SHA")
    _validate_sha(tree_sha, "Git tree SHA")
    return source_sha, tree_sha


def _ensure_clean(repository_root: Path) -> None:
    status = _git_output(repository_root, "status", "--porcelain", "--untracked-files=all", allow_empty=True)
    if status:
        raise CandidateIdentityError("The candidate repository has uncommitted changes.")


def _validate_sha(value: Any, description: str) -> str:
    if not isinstance(value, str) or SHA_PATTERN.fullmatch(value) is None:
        raise CandidateIdentityError(f"{description} is malformed.")
    return value


def _validate_sha256(value: Any, description: str) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        raise CandidateIdentityError(f"{description} is malformed.")
    return value


def _repository_root(value: Path) -> Path:
    root = value.resolve()
    if not root.is_dir() or root.is_symlink():
        raise CandidateIdentityError(f"Candidate repository root is not a directory: {value}")
    return root


def _relative_input(repository_root: Path, value: Path | str, description: str) -> tuple[str, Path]:
    supplied = Path(value)
    if supplied.is_absolute():
        candidate = supplied.resolve()
    else:
        candidate = (repository_root / supplied).resolve()
    if candidate == repository_root or repository_root not in candidate.parents:
        raise CandidateIdentityError(f"{description} must be inside the repository root.")
    if supplied.is_symlink() or candidate.is_symlink():
        raise CandidateIdentityError(f"{description} must not be a symbolic link.")
    relative = candidate.relative_to(repository_root).as_posix()
    if not relative or relative == ".":
        raise CandidateIdentityError(f"{description} must be a repository-relative file.")
    if not candidate.is_file():
        raise CandidateIdentityError(f"Missing {description}: {relative}")
    return relative, candidate


def _manifest_relative_path(repository_root: Path, value: Any, description: str) -> tuple[str, Path]:
    if not isinstance(value, str) or not value or "\\" in value:
        raise CandidateIdentityError(f"{description} is not a repository-relative path.")
    relative = PurePosixPath(value)
    if value != relative.as_posix() or relative.is_absolute() or ".." in relative.parts:
        raise CandidateIdentityError(f"{description} is not a repository-relative path.")
    return _relative_input(repository_root, Path(value), description)


def _read_manifest(path: Path) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        raise CandidateIdentityError(f"Missing candidate manifest: {path}")

    def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise CandidateIdentityError(f"Candidate manifest contains duplicate field: {key}")
            result[key] = value
        return result

    try:
        value = json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda constant: (_ for _ in ()).throw(
                ValueError(f"unsupported JSON constant {constant}")
            ),
        )
    except (OSError, UnicodeError, ValueError, json.JSONDecodeError) as error:
        raise CandidateIdentityError(f"Candidate manifest is malformed: {error}") from error
    if not isinstance(value, dict):
        raise CandidateIdentityError("Candidate manifest must be a JSON object.")
    return value


def _validate_manifest(manifest: dict[str, Any]) -> None:
    if set(manifest) != MANIFEST_FIELDS:
        raise CandidateIdentityError("Candidate manifest fields are missing or unexpected.")
    if manifest.get("schema") != SCHEMA:
        raise CandidateIdentityError(f"Unsupported candidate manifest schema: {manifest.get('schema')!r}")
    _validate_sha(manifest.get("source_sha"), "Candidate source SHA")
    _validate_sha(manifest.get("tree_sha"), "Candidate tree SHA")
    for field in ("policy_path", "cli_assembly_path", "testing_assembly_path", "tool_identity"):
        if not isinstance(manifest.get(field), str) or not manifest[field]:
            raise CandidateIdentityError(f"Candidate {field} is missing or malformed.")
    for field in ("policy_sha256", "cli_assembly_sha256", "testing_assembly_sha256"):
        _validate_sha256(manifest.get(field), f"Candidate {field}")


def create_manifest(
    repository_root: Path,
    *,
    policy_path: Path | str = DEFAULT_POLICY_PATH,
    cli_assembly_path: Path | str,
    testing_assembly_path: Path | str,
    source_sha: str | None = None,
    tool_identity: str = DEFAULT_TOOL_IDENTITY,
    git_identity: Callable[[Path], tuple[str, str]] | None = None,
) -> dict[str, Any]:
    """Create a candidate manifest after validating the checked-out repository identity."""

    root = _repository_root(repository_root)
    _ensure_clean(root)
    if git_identity is None:
        git_identity = _git_identity
    actual_source_sha, tree_sha = git_identity(root)
    if source_sha is not None:
        _validate_sha(source_sha, "Requested source SHA")
    if source_sha is not None and source_sha != actual_source_sha:
        raise CandidateIdentityError(
            f"Requested source SHA does not match Git HEAD: expected {source_sha}, got {actual_source_sha}"
        )
    source_sha = actual_source_sha
    _validate_sha(source_sha, "Candidate source SHA")
    if not isinstance(tool_identity, str) or not tool_identity.strip():
        raise CandidateIdentityError("Candidate tool identity is missing or malformed.")

    policy_relative, policy_file = _relative_input(root, policy_path, "candidate policy")
    cli_relative, cli_file = _relative_input(root, cli_assembly_path, "candidate CLI assembly")
    testing_relative, testing_file = _relative_input(root, testing_assembly_path, "candidate Testing assembly")
    manifest = {
        "schema": SCHEMA,
        "source_sha": source_sha,
        "tree_sha": tree_sha,
        "policy_path": policy_relative,
        "policy_sha256": sha256_file(policy_file),
        "cli_assembly_path": cli_relative,
        "cli_assembly_sha256": sha256_file(cli_file),
        "testing_assembly_path": testing_relative,
        "testing_assembly_sha256": sha256_file(testing_file),
        "tool_identity": tool_identity,
    }
    _validate_manifest(manifest)
    return manifest


def verify_manifest(
    repository_root: Path,
    manifest_path: Path,
    *,
    expected_source_sha: str | None = None,
    expected_tool_identity: str = DEFAULT_TOOL_IDENTITY,
    git_identity: Callable[[Path], tuple[str, str]] | None = None,
) -> dict[str, Any]:
    """Verify a candidate manifest against the current Git identity and build outputs.

    The producer may create report files between concurrent projection checks, so verification
    intentionally does not require a clean working tree. The build step performs the clean check
    before the manifest is created; later checks bind the same source/tree, policy, and assembly
    hashes instead of treating generated evidence files as candidate input.
    """

    root = _repository_root(repository_root)
    manifest = _read_manifest(manifest_path)
    _validate_manifest(manifest)
    if git_identity is None:
        git_identity = _git_identity
    actual_source_sha, actual_tree_sha = git_identity(root)
    if manifest["source_sha"] != actual_source_sha:
        raise CandidateIdentityError("Candidate source SHA does not match Git HEAD.")
    if manifest["tree_sha"] != actual_tree_sha:
        raise CandidateIdentityError("Candidate tree SHA does not match Git HEAD.")
    if expected_source_sha is not None:
        _validate_sha(expected_source_sha, "Expected source SHA")
        if manifest["source_sha"] != expected_source_sha:
            raise CandidateIdentityError("Candidate source SHA does not match the expected source SHA.")
    if not isinstance(expected_tool_identity, str) or not expected_tool_identity.strip():
        raise CandidateIdentityError("Expected tool identity is missing or malformed.")
    if manifest["tool_identity"] != expected_tool_identity:
        raise CandidateIdentityError("Candidate tool identity does not match the expected tool identity.")

    for path_field, digest_field, description in (
        ("policy_path", "policy_sha256", "candidate policy"),
        ("cli_assembly_path", "cli_assembly_sha256", "candidate CLI assembly"),
        ("testing_assembly_path", "testing_assembly_sha256", "candidate Testing assembly"),
    ):
        _, path = _manifest_relative_path(root, manifest[path_field], description)
        actual_digest = sha256_file(path)
        if actual_digest != manifest[digest_field]:
            raise CandidateIdentityError(f"{description} SHA-256 does not match the candidate manifest.")
    return manifest


def _write_manifest(path: Path, manifest: dict[str, Any]) -> None:
    if path.exists() and path.is_symlink():
        raise CandidateIdentityError(f"Candidate manifest output must not be a symbolic link: {path}")
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(canonical_json(manifest), encoding="utf-8", newline="\n")
    except OSError as error:
        raise CandidateIdentityError(f"Cannot write candidate manifest '{path}': {error}") from error


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    create = subparsers.add_parser("create", help="create a verified candidate manifest")
    create.add_argument("--repository-root", type=Path, default=Path("."))
    create.add_argument("--output", "--manifest", dest="output", type=Path, required=True)
    create.add_argument("--source-sha")
    create.add_argument("--policy", type=Path, default=Path(DEFAULT_POLICY_PATH))
    create.add_argument("--cli-assembly", type=Path, required=True)
    create.add_argument("--testing-assembly", type=Path, required=True)
    create.add_argument("--tool-identity", default=DEFAULT_TOOL_IDENTITY)

    verify = subparsers.add_parser("verify", help="verify a candidate manifest")
    verify.add_argument("--repository-root", type=Path, default=Path("."))
    verify.add_argument("--manifest", type=Path, required=True)
    verify.add_argument("--source-sha")
    verify.add_argument("--tool-identity", default=DEFAULT_TOOL_IDENTITY)
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _parser().parse_args(argv)
    try:
        if arguments.command == "create":
            manifest = create_manifest(
                arguments.repository_root,
                policy_path=arguments.policy,
                cli_assembly_path=arguments.cli_assembly,
                testing_assembly_path=arguments.testing_assembly,
                source_sha=arguments.source_sha,
                tool_identity=arguments.tool_identity,
            )
            _write_manifest(arguments.output, manifest)
        else:
            manifest = verify_manifest(
                arguments.repository_root,
                arguments.manifest,
                expected_source_sha=arguments.source_sha,
                expected_tool_identity=arguments.tool_identity,
            )
    except CandidateIdentityError as error:
        print(canonical_json({"error": str(error), "schema": SCHEMA}), file=sys.stderr, end="")
        return 1
    print(canonical_json(manifest), end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
