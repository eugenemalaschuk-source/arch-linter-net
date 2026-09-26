#!/usr/bin/env python3
"""Create or resume the existing GitHub release and verify its exact asset bytes."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from urllib.parse import quote


_OBJECT_ID = re.compile(r"(?:[0-9a-f]{40}|[0-9a-f]{64})\Z")
_REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\Z")


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


def _remote_tag_commit(repository: str, tag: str) -> str | None:
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


def _release_view(repository: str, tag: str) -> dict[str, object] | None:
    result = subprocess.run(
        ["gh", "release", "view", tag, "--repo", repository, "--json", "tagName,assets"],
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        if "release not found" in detail.lower() or "http 404: not found" in detail.lower():
            return None
        raise ReleaseAssetError(f"Cannot inspect GitHub release {tag!r}: {detail[-3000:]}")
    try:
        release = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise ReleaseAssetError("GitHub CLI returned invalid release metadata.") from error
    if not isinstance(release, dict) or not isinstance(release.get("assets"), list):
        raise ReleaseAssetError("GitHub CLI returned an incomplete release record.")
    if release.get("tagName") != tag:
        raise ReleaseAssetError(f"The existing release does not use the requested tag {tag!r}.")
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


def publish(arguments: argparse.Namespace) -> None:
    if _REPOSITORY.fullmatch(arguments.repository) is None:
        raise ReleaseAssetError("Repository identity must be owner/name.")
    if _OBJECT_ID.fullmatch(arguments.candidate_sha) is None:
        raise ReleaseAssetError("Candidate commit must be a full lowercase Git object ID.")

    assets: dict[str, Path] = {}
    for raw_path in arguments.asset:
        path = Path(raw_path).resolve(strict=True)
        if not path.is_file():
            raise ReleaseAssetError(f"Release asset is not a regular file: {path}")
        if path.name in assets:
            raise ReleaseAssetError(f"Release assets have duplicate names: {path.name!r}.")
        assets[path.name] = path
    if not assets:
        raise ReleaseAssetError("At least one release asset is required.")

    remote_tag_sha = _remote_tag_commit(arguments.repository, arguments.tag)
    if remote_tag_sha is not None and remote_tag_sha != arguments.candidate_sha:
        raise ReleaseAssetError("The existing GitHub tag does not point to the exact candidate commit.")

    release = _release_view(arguments.repository, arguments.tag)
    if release is None:
        if not arguments.create_if_missing:
            raise ReleaseAssetError("The requested GitHub release does not exist.")
        _gh(
            [
                "release",
                "create",
                arguments.tag,
                "--repo",
                arguments.repository,
                "--target",
                arguments.candidate_sha,
                "--title",
                arguments.title,
                "--notes-file",
                str(Path(arguments.notes_file).resolve(strict=True)),
            ]
        )
        _wait_for_tag_commit(arguments.repository, arguments.tag, arguments.candidate_sha)
        release = _wait_for_release(arguments.repository, arguments.tag)
    elif remote_tag_sha != arguments.candidate_sha:
        raise ReleaseAssetError("The existing GitHub release has no verified candidate tag.")

    existing_names = _asset_names(release)
    with tempfile.TemporaryDirectory(prefix="release-asset-readback-") as temporary:
        readback_root = Path(temporary)
        for name, path in assets.items():
            if name in existing_names:
                asset_directory = readback_root / name
                asset_directory.mkdir()
                existing_path = _read_back_asset(arguments.repository, arguments.tag, name, asset_directory)
                if _sha256_file(existing_path) != _sha256_file(path):
                    raise ReleaseAssetError(
                        f"Existing GitHub release asset {name!r} differs from the candidate bytes; refusing to replace it."
                    )
        for name, path in assets.items():
            if name not in existing_names:
                # No clobber flag is used. A name collision after this inventory check fails closed.
                _gh(["release", "upload", arguments.tag, str(path), "--repo", arguments.repository])
                existing_names.add(name)

        release = _release_view(arguments.repository, arguments.tag)
        if release is None:
            raise ReleaseAssetError("GitHub release disappeared during asset publication.")
        published_names = _asset_names(release)
        for name, path in assets.items():
            if name not in published_names:
                raise ReleaseAssetError(f"GitHub release is missing expected asset {name!r} after upload.")
            asset_directory = readback_root / f"verified-{name}"
            asset_directory.mkdir()
            downloaded = _read_back_asset(arguments.repository, arguments.tag, name, asset_directory)
            if _sha256_file(downloaded) != _sha256_file(path):
                raise ReleaseAssetError(f"GitHub release asset read-back digest mismatch: {name}.")

    print(f"Verified {len(assets)} release assets for {arguments.repository} {arguments.tag}.")


def _parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--candidate-sha", required=True)
    parser.add_argument("--title", required=True)
    parser.add_argument("--notes-file", required=True)
    parser.add_argument("--create-if-missing", action="store_true")
    parser.add_argument("--asset", action="append", required=True)
    return parser.parse_args()


def main() -> int:
    try:
        publish(_parse_arguments())
        return 0
    except (OSError, ReleaseAssetError, ValueError) as error:
        print(f"release asset publication failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
