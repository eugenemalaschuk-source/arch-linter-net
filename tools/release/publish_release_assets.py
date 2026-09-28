#!/usr/bin/env python3
"""Create or resume the existing GitHub release and verify its exact asset bytes."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from urllib.parse import quote

from calculate_version import parse_package_version


_OBJECT_ID = re.compile(r"(?:[0-9a-f]{40}|[0-9a-f]{64})\Z")
_REPOSITORY = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*\Z")
_ASSET_NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]*\Z")


class ReleaseAssetError(ValueError):
    """Raised when an existing release cannot be safely resumed or verified."""


def _gh(arguments: list[str]) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(["gh", *arguments], text=True, capture_output=True, check=False)
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise ReleaseAssetError(f"GitHub CLI command failed: gh {' '.join(arguments[:4])}\n{detail[-3000:]}")
    return result


def _api_json(repository: str, path: str) -> object:
    result = _gh(["api", f"repos/{repository}/{path}"])
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise ReleaseAssetError(f"GitHub API returned invalid JSON for {path}.") from error


def _matching_tag_object(repository: str, tag: str) -> tuple[str, str] | None:
    encoded_tag = quote(tag, safe="")
    refs = _api_json(repository, f"git/matching-refs/tags/{encoded_tag}")
    if not isinstance(refs, list):
        raise ReleaseAssetError("GitHub returned an invalid tag-ref response.")
    matches = [
        item
        for item in refs
        if isinstance(item, dict) and item.get("ref") == f"refs/tags/{tag}"
    ]
    if not matches:
        return None
    if len(matches) != 1:
        raise ReleaseAssetError(f"GitHub returned duplicate refs for tag {tag!r}.")
    ref_object = matches[0].get("object")
    if not isinstance(ref_object, dict):
        raise ReleaseAssetError(f"GitHub returned an invalid object for tag {tag!r}.")
    object_sha = ref_object.get("sha")
    object_type = ref_object.get("type")
    if not isinstance(object_sha, str) or not isinstance(object_type, str):
        raise ReleaseAssetError(f"GitHub returned an invalid object for tag {tag!r}.")
    return object_sha, object_type


def _peel_tag_commit(repository: str, tag: str, object_sha: str, object_type: str) -> str:
    for _ in range(9):
        if not isinstance(object_sha, str) or _OBJECT_ID.fullmatch(object_sha) is None:
            raise ReleaseAssetError(f"GitHub returned an invalid object ID for tag {tag!r}.")
        if object_type == "commit":
            return object_sha
        if object_type != "tag":
            raise ReleaseAssetError(f"GitHub tag {tag!r} does not resolve to a commit.")
        annotated = _api_json(repository, f"git/tags/{object_sha}")
        if not isinstance(annotated, dict) or not isinstance(annotated.get("object"), dict):
            raise ReleaseAssetError(f"GitHub returned an invalid annotated tag object for {tag!r}.")
        ref_object = annotated["object"]
        object_sha = ref_object.get("sha")
        object_type = ref_object.get("type")
    raise ReleaseAssetError(f"GitHub tag {tag!r} exceeds the supported annotated-tag depth.")


def _remote_tag_commit(repository: str, tag: str) -> str | None:
    tagged_object = _matching_tag_object(repository, tag)
    if tagged_object is None:
        return None
    object_sha, object_type = tagged_object
    return _peel_tag_commit(repository, tag, object_sha, object_type)


def _release_view(repository: str, tag: str) -> dict[str, object] | None:
    encoded_tag = quote(tag, safe="")
    try:
        release = _api_json(repository, f"releases/tags/{encoded_tag}")
    except ReleaseAssetError as error:
        detail = str(error).lower()
        if "release not found" not in detail and not ("http 404" in detail and "not found" in detail):
            raise ReleaseAssetError(f"Cannot inspect GitHub release {tag!r}: {error}") from error
        release = _release_from_list(repository, tag)
        if release is None:
            return None
    return _validate_release_view(release, tag)


def _release_from_list(repository: str, tag: str) -> dict[str, object] | None:
    result = _gh(["api", "--paginate", "--slurp", f"repos/{repository}/releases?per_page=100"])
    try:
        pages = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise ReleaseAssetError("GitHub API returned invalid JSON while listing releases.") from error
    if not isinstance(pages, list) or any(not isinstance(page, list) for page in pages):
        raise ReleaseAssetError("GitHub API returned an invalid release-list response.")
    matches = [
        release
        for page in pages
        for release in page
        if isinstance(release, dict) and release.get("tag_name") == tag
    ]
    if len(matches) > 1:
        raise ReleaseAssetError(f"GitHub returned duplicate releases for tag {tag!r}.")
    return matches[0] if matches else None


def _validate_release_view(release: object, tag: str) -> dict[str, object]:
    if not isinstance(release, dict) or not isinstance(release.get("assets"), list):
        raise ReleaseAssetError("GitHub API returned an incomplete release record.")
    if release.get("tag_name") != tag:
        raise ReleaseAssetError(f"The existing release does not use the requested tag {tag!r}.")
    if (
        not isinstance(release.get("draft"), bool)
        or not isinstance(release.get("immutable"), bool)
        or not isinstance(release.get("target_commitish"), str)
    ):
        raise ReleaseAssetError("GitHub API release metadata is missing draft, immutable, or target commit state.")
    return release


def _wait_for_tag_commit(repository: str, tag: str, expected_sha: str) -> str:
    for attempt in range(10):
        commit = _remote_tag_commit(repository, tag)
        if commit is not None:
            if commit != expected_sha:
                raise ReleaseAssetError("The release tag does not point to the exact candidate commit.")
            return commit
        if attempt < 9:
            time.sleep(1)
    raise ReleaseAssetError("The release tag was not readable after release creation.")


def _wait_for_release(repository: str, tag: str) -> dict[str, object]:
    for attempt in range(10):
        release = _release_view(repository, tag)
        if release is not None:
            return release
        if attempt < 9:
            time.sleep(1)
    raise ReleaseAssetError("The GitHub Release was not readable after release creation.")


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as content:
        for block in iter(lambda: content.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _asset_names(release: dict[str, object]) -> set[str]:
    assets = release["assets"]
    assert isinstance(assets, list)
    names: set[str] = set()
    for asset in assets:
        if not isinstance(asset, dict) or not isinstance(asset.get("name"), str):
            raise ReleaseAssetError("GitHub returned a malformed release asset record.")
        name = asset["name"]
        if name in names:
            raise ReleaseAssetError(f"GitHub release has duplicate asset names: {name!r}.")
        names.add(name)
    return names


def _read_back_asset(repository: str, tag: str, name: str, directory: Path) -> Path:
    destination = directory / name
    destination.unlink(missing_ok=True)
    _gh(
        [
            "release",
            "download",
            tag,
            "--repo",
            repository,
            "--pattern",
            name,
            "--dir",
            str(directory),
        ]
    )
    if not destination.is_file():
        raise ReleaseAssetError(f"GitHub release asset {name!r} was not downloaded for read-back.")
    return destination


def _validate_identity(arguments: argparse.Namespace) -> tuple[str, str, str]:
    if _REPOSITORY.fullmatch(arguments.repository) is None:
        raise ReleaseAssetError("Repository identity must be owner/name.")
    tag_version = arguments.tag.removeprefix("v")
    if not arguments.tag.startswith("v") or parse_package_version(tag_version) is None:
        raise ReleaseAssetError("Release tag must be a canonical NuGet SemVer tag.")
    if _OBJECT_ID.fullmatch(arguments.candidate_sha) is None:
        raise ReleaseAssetError("Candidate commit must be a full lowercase Git object ID.")
    return arguments.repository, arguments.tag, arguments.candidate_sha


def _resolve_assets(raw_paths: list[str]) -> dict[str, Path]:
    assets: dict[str, Path] = {}
    for raw_path in raw_paths:
        path = Path(raw_path).resolve(strict=True)
        if not path.is_file():
            raise ReleaseAssetError(f"Release asset is not a regular file: {path}")
        if _ASSET_NAME.fullmatch(path.name) is None:
            raise ReleaseAssetError(f"Release asset has an unsafe filename: {path.name!r}.")
        if path.name in assets:
            raise ReleaseAssetError(f"Release assets have duplicate names: {path.name!r}.")
        assets[path.name] = path
    if not assets:
        raise ReleaseAssetError("At least one release asset is required.")
    return assets


def _create_release(arguments: argparse.Namespace, repository: str, tag: str, candidate_sha: str) -> None:
    notes_path = Path(arguments.notes_file).resolve(strict=True)
    if not notes_path.is_file():
        raise ReleaseAssetError("Release notes must be a regular file.")
    _gh(
        [
            "release",
            "create",
            tag,
            "--repo",
            repository,
            "--target",
            candidate_sha,
            "--title",
            tag,
            "--notes-file",
            str(notes_path),
            "--draft",
        ]
    )


def _publish_draft(repository: str, tag: str) -> None:
    _gh(["release", "edit", tag, "--repo", repository, "--draft=false"])


def _wait_for_published_release(repository: str, tag: str) -> dict[str, object]:
    for attempt in range(10):
        release = _release_view(repository, tag)
        if release is not None and release.get("draft") is False:
            return release
        if attempt < 9:
            time.sleep(1)
    raise ReleaseAssetError("The GitHub Release did not become published after draft promotion.")


def _load_or_create_release(
    arguments: argparse.Namespace, repository: str, tag: str, candidate_sha: str
) -> dict[str, object]:
    remote_tag_sha = _remote_tag_commit(repository, tag)
    if remote_tag_sha is not None and remote_tag_sha != candidate_sha:
        raise ReleaseAssetError("The existing GitHub tag does not point to the exact candidate commit.")
    release = _release_view(repository, tag)
    if release is None:
        if not arguments.create_if_missing:
            raise ReleaseAssetError("The requested GitHub release does not exist.")
        _create_release(arguments, repository, tag, candidate_sha)
        release = _wait_for_release(repository, tag)
    if remote_tag_sha == candidate_sha:
        return release
    if release.get("draft") is True and release.get("target_commitish") == candidate_sha:
        # GitHub does not create a missing tag until the draft release is published.
        return release
    raise ReleaseAssetError("The existing GitHub release has no verified candidate tag or matching draft target.")


def _verify_existing_assets(
    assets: dict[str, Path], existing_names: set[str], repository: str, tag: str, readback_root: Path
) -> None:
    for name, path in assets.items():
        if name not in existing_names:
            continue
        asset_directory = readback_root / name
        asset_directory.mkdir()
        existing_path = _read_back_asset(repository, tag, name, asset_directory)
        if _sha256_file(existing_path) != _sha256_file(path):
            raise ReleaseAssetError(
                f"Existing GitHub release asset {name!r} differs from the candidate bytes; refusing to replace it."
            )


def _upload_missing_assets(
    assets: dict[str, Path], existing_names: set[str], repository: str, tag: str
) -> None:
    for name, path in assets.items():
        if name in existing_names:
            continue
        # No clobber flag is used. A name collision after this inventory check fails closed.
        _gh(["release", "upload", tag, str(path), "--repo", repository])
        existing_names.add(name)


def _verify_published_assets(
    assets: dict[str, Path], repository: str, tag: str, readback_root: Path
) -> None:
    release = _release_view(repository, tag)
    if release is None:
        raise ReleaseAssetError("GitHub release disappeared during asset publication.")
    published_names = _asset_names(release)
    for name, path in assets.items():
        if name not in published_names:
            raise ReleaseAssetError(f"GitHub release is missing expected asset {name!r} after upload.")
        asset_directory = readback_root / f"verified-{name}"
        asset_directory.mkdir(exist_ok=True)
        downloaded = _read_back_asset(repository, tag, name, asset_directory)
        if _sha256_file(downloaded) != _sha256_file(path):
            raise ReleaseAssetError(f"GitHub release asset read-back digest mismatch: {name}.")


def publish(arguments: argparse.Namespace) -> None:
    repository, tag, candidate_sha = _validate_identity(arguments)
    assets = _resolve_assets(arguments.asset)
    release = _load_or_create_release(arguments, repository, tag, candidate_sha)
    existing_names = _asset_names(release)
    missing_names = set(assets) - existing_names
    is_draft = release["draft"]
    is_immutable = release["immutable"]
    assert isinstance(is_draft, bool) and isinstance(is_immutable, bool)
    if not is_draft and is_immutable and missing_names:
        missing = ", ".join(sorted(missing_names))
        raise ReleaseAssetError(
            f"Published immutable release {tag} is missing expected assets ({missing}); "
            "create and review a new release version because this tag cannot be repaired."
        )
    with tempfile.TemporaryDirectory(prefix="release-asset-readback-") as temporary:
        readback_root = Path(temporary)
        _verify_existing_assets(assets, existing_names, repository, tag, readback_root)
        _upload_missing_assets(assets, existing_names, repository, tag)
        _verify_published_assets(assets, repository, tag, readback_root)
        if is_draft:
            _publish_draft(repository, tag)
            _wait_for_published_release(repository, tag)
            _wait_for_tag_commit(repository, tag, candidate_sha)
            _verify_published_assets(assets, repository, tag, readback_root)
    print(f"Verified {len(assets)} release assets for {repository} {tag}.")


def _parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--notes-file", required=True)
    parser.add_argument("--create-if-missing", action="store_true")
    parser.add_argument("--asset", action="append", required=True)
    return parser.parse_args()


def main() -> int:
    try:
        arguments = _parse_arguments()
        arguments.repository = os.environ.get("GH_REPO", "")
        arguments.tag = os.environ.get("TARGET_TAG", "")
        arguments.candidate_sha = os.environ.get("RELEASE_COMMIT", "")
        publish(arguments)
        return 0
    except (OSError, ValueError) as error:
        print(f"release asset publication failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
