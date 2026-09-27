#!/usr/bin/env python3
"""Select and pin the release-history range for one immutable release candidate."""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

from calculate_version import parse_package_version

_OBJECT_ID = re.compile(r"[0-9a-f]+\Z")


_PrereleaseIdentifierKey = tuple[int, int, str]
_VersionPrecedenceKey = tuple[int, int, int, tuple[int, tuple[_PrereleaseIdentifierKey, ...]]]


class ReleaseHistoryRangeError(ValueError):
    """Raised when candidate or predecessor identity cannot be established."""


@dataclass(frozen=True)
class ReleaseVersion:
    major: int
    minor: int
    patch: int
    prerelease: str | None = None
    build_metadata: str | None = None

    @property
    def base(self) -> tuple[int, int, int]:
        return (self.major, self.minor, self.patch)

    @property
    def is_stable(self) -> bool:
        return self.prerelease is None

    @property
    def line(self) -> str:
        return f"{self.major}.{self.minor}.{self.patch}"

    def __str__(self) -> str:
        prerelease = "" if self.prerelease is None else f"-{self.prerelease}"
        metadata = "" if self.build_metadata is None else f"+{self.build_metadata}"
        return f"{self.line}{prerelease}{metadata}"

    @property
    def precedence_key(self) -> _VersionPrecedenceKey:
        if self.prerelease is None:
            qualifier = (1, ())
        else:
            qualifier = (0, tuple(_prerelease_identifier_key(item) for item in self.prerelease.split(".")))
        return (self.major, self.minor, self.patch, qualifier)

    @staticmethod
    def parse(value: str | bytes) -> ReleaseVersion | None:
        try:
            text = value.decode("ascii") if isinstance(value, bytes) else value.encode("ascii").decode("ascii")
        except (UnicodeDecodeError, UnicodeEncodeError):
            return None
        package_version = parse_package_version(text.removeprefix("v"))
        if package_version is None:
            return None
        major, minor, patch, prerelease, build_metadata = package_version
        return ReleaseVersion(major, minor, patch, prerelease, build_metadata)


def _prerelease_identifier_key(identifier: str) -> _PrereleaseIdentifierKey:
    if identifier.isascii() and identifier.isdigit():
        numeric = identifier.lstrip("0") or "0"
        return (0, len(numeric), numeric)
    return (1, 0, identifier)


@dataclass(frozen=True)
class ReleaseHistoryRange:
    status: Literal["applicable", "not_applicable"]
    reason: str | None
    candidate_version: str
    target_tag: str
    candidate_sha: str
    candidate_tree: str
    series_kind: Literal["stable", "preview"]
    series_identity: str
    base_tag: str | None
    base_sha: str | None

    def as_dict(self) -> dict[str, object]:
        return {
            "status": self.status,
            "reason": self.reason,
            "candidate_version": self.candidate_version,
            "target_tag": self.target_tag,
            "candidate_sha": self.candidate_sha,
            "candidate_tree": self.candidate_tree,
            "series_kind": self.series_kind,
            "series_identity": self.series_identity,
            "base_tag": self.base_tag,
            "base_sha": self.base_sha,
            "range_semantics": "exclusive_base_inclusive_candidate",
        }


@dataclass(frozen=True)
class _Tag:
    name: str
    version: ReleaseVersion


def _git(repository: Path, *arguments: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    result = subprocess.run(
        ["git", "-C", str(repository), *arguments],
        capture_output=True,
        check=False,
    )
    if check and result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise ReleaseHistoryRangeError(f"Git command failed ({' '.join(arguments)}): {detail}")
    return result


def _full_object_id(value: str, expected_length: int, description: str) -> str:
    if len(value) != expected_length or _OBJECT_ID.fullmatch(value) is None:
        raise ReleaseHistoryRangeError(f"The {description} is not a full lowercase Git object ID.")
    return value


def _object_format(repository: Path) -> tuple[int, str]:
    result = _git(repository, "rev-parse", "--show-object-format")
    value = result.stdout.decode("ascii", errors="strict").strip()
    lengths = {"sha1": 40, "sha256": 64}
    if value not in lengths:
        raise ReleaseHistoryRangeError(f"Unsupported Git object format: {value or '<empty>'}.")
    return lengths[value], value


def _resolve_tag_commit(repository: Path, name: str, object_id_length: int) -> str:
    ref = f"refs/tags/{name}^{{commit}}"
    result = _git(repository, "rev-parse", "--verify", "--end-of-options", ref)
    try:
        commit = result.stdout.decode("ascii", errors="strict").strip()
    except UnicodeDecodeError as error:
        raise ReleaseHistoryRangeError(f"Tag {name!r} resolved to an invalid object ID.") from error
    commit = _full_object_id(commit, object_id_length, f"tag {name!r} commit")
    _git(repository, "cat-file", "-e", f"{commit}^{{commit}}")
    return commit


def _is_ancestor(repository: Path, base_sha: str, candidate_sha: str) -> bool:
    result = _git(
        repository,
        "merge-base",
        "--is-ancestor",
        base_sha,
        candidate_sha,
        check=False,
    )
    if result.returncode == 0:
        return True
    if result.returncode == 1:
        return False
    detail = result.stderr.decode("utf-8", errors="replace").strip()
    raise ReleaseHistoryRangeError(f"Cannot establish tag ancestry: {detail}")


def _release_tags(repository: Path) -> list[_Tag]:
    result = _git(repository, "for-each-ref", "--format=%(refname:strip=2)", "refs/tags")
    tags: list[_Tag] = []
    for raw_name in result.stdout.splitlines():
        version = ReleaseVersion.parse(raw_name)
        if version is None:
            continue
        try:
            name = raw_name.decode("ascii", errors="strict")
        except UnicodeDecodeError:
            continue
        tags.append(_Tag(name, version))
    return tags


def _validate_existing_target(
    repository: Path,
    target_tag: str,
    candidate_sha: str,
    tags: list[_Tag],
    object_id_length: int,
) -> None:
    version = ReleaseVersion.parse(target_tag)
    if version is None:
        raise ReleaseHistoryRangeError("The target tag is not a valid NuGet SemVer release tag.")
    equivalent_precedence = [tag for tag in tags if tag.version.precedence_key == version.precedence_key]
    aliases = [tag for tag in tags if tag.version == version]
    if len(aliases) > 1:
        names = ", ".join(sorted(tag.name for tag in aliases))
        raise ReleaseHistoryRangeError(
            f"Ambiguous release tags identify candidate version {version}: {names}."
        )
    metadata_aliases = [tag for tag in equivalent_precedence if tag.version != version]
    if metadata_aliases:
        names = ", ".join(sorted(tag.name for tag in equivalent_precedence))
        raise ReleaseHistoryRangeError(
            f"Candidate version {version} has existing tags with equivalent SemVer precedence: {names}."
        )
    if aliases:
        tag = aliases[0]
        if tag.name != target_tag:
            raise ReleaseHistoryRangeError(
                f"Candidate version {version} already has a different authored tag {tag.name!r}."
            )
        if _resolve_tag_commit(repository, tag.name, object_id_length) != candidate_sha:
            raise ReleaseHistoryRangeError(
                f"Existing target tag {target_tag!r} does not point to the candidate commit."
            )


def _eligible(version: ReleaseVersion, candidate: ReleaseVersion) -> bool:
    if candidate.is_stable:
        return version.is_stable and version.precedence_key < candidate.precedence_key
    if version.is_stable:
        return version.base < candidate.base
    return version.base == candidate.base and version.precedence_key < candidate.precedence_key


def resolve_release_history_range(
    repository: Path,
    candidate_version: str,
    target_tag: str,
    candidate_sha: str,
    candidate_tree: str,
) -> ReleaseHistoryRange:
    """Resolve the prior applicable release and pin both range endpoints by object ID."""
    repository = repository.resolve(strict=True)
    candidate = ReleaseVersion.parse(candidate_version)
    if candidate is None or str(candidate) != candidate_version:
        raise ReleaseHistoryRangeError("Candidate version must be a canonical NuGet SemVer package version.")
    if target_tag != f"v{candidate_version}":
        raise ReleaseHistoryRangeError("Target tag does not match the candidate package version.")

    shallow = _git(repository, "rev-parse", "--is-shallow-repository").stdout.strip()
    if shallow != b"false":
        raise ReleaseHistoryRangeError("Release history analysis requires a complete, non-shallow Git checkout.")

    object_id_length, _ = _object_format(repository)
    candidate_sha = _full_object_id(candidate_sha, object_id_length, "candidate commit")
    candidate_tree = _full_object_id(candidate_tree, object_id_length, "candidate tree")
    _git(repository, "cat-file", "-e", f"{candidate_sha}^{{commit}}")
    actual_tree = _git(repository, "rev-parse", "--verify", "--end-of-options", f"{candidate_sha}^{{tree}}")
    if actual_tree.stdout.decode("ascii", errors="strict").strip() != candidate_tree:
        raise ReleaseHistoryRangeError("Candidate tree does not match the candidate commit.")

    tags = _release_tags(repository)
    _validate_existing_target(repository, target_tag, candidate_sha, tags, object_id_length)
    series_kind: Literal["stable", "preview"] = "stable" if candidate.is_stable else "preview"
    series_identity = "stable" if candidate.is_stable else f"preview:{candidate.line}"

    eligible = [tag for tag in tags if _eligible(tag.version, candidate)]
    reachable: list[tuple[_Tag, str]] = []
    for tag in eligible:
        commit = _resolve_tag_commit(repository, tag.name, object_id_length)
        if _is_ancestor(repository, commit, candidate_sha):
            reachable.append((tag, commit))

    versions: dict[_VersionPrecedenceKey, list[str]] = {}
    for tag, _ in reachable:
        versions.setdefault(tag.version.precedence_key, []).append(tag.name)
    ambiguous = {version: names for version, names in versions.items() if len(names) > 1}
    if ambiguous:
        _, names = max(ambiguous.items(), key=lambda item: item[0])
        joined = ", ".join(sorted(names))
        raise ReleaseHistoryRangeError(f"Ambiguous release tags have equivalent SemVer precedence: {joined}.")

    if not reachable:
        return ReleaseHistoryRange(
            "not_applicable",
            "no_previous_release",
            candidate_version,
            target_tag,
            candidate_sha,
            candidate_tree,
            series_kind,
            series_identity,
            None,
            None,
        )

    base_tag, base_sha = max(reachable, key=lambda item: item[0].version.precedence_key)
    return ReleaseHistoryRange(
        "applicable",
        None,
        candidate_version,
        target_tag,
        candidate_sha,
        candidate_tree,
        series_kind,
        series_identity,
        base_tag.name,
        base_sha,
    )
