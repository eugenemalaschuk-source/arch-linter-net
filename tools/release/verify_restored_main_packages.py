#!/usr/bin/env python3
"""Verify that a clean NuGet restore resolved the exact ArchLinterNet main package set."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from _release_workspace import _safe_path

LIBRARY_PACKAGE_IDS = (
    "ArchLinterNet.CEL",
    "ArchLinterNet.Core",
    "ArchLinterNet.Testing",
)


def _load_libraries(assets_path: Path) -> dict[str, Any]:
    try:
        document = json.loads(assets_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read NuGet assets file '{assets_path}': {error}") from error

    libraries = document.get("libraries")
    if not isinstance(libraries, dict):
        raise ValueError(
            f"NuGet assets file '{assets_path}' does not contain an object-valued 'libraries' map."
        )
    return libraries


def verify_restored_main_packages(assets_path: Path, version: str) -> None:
    if not version.strip():
        raise ValueError("Package version must be non-empty.")

    resolved_versions = {package_id: set() for package_id in LIBRARY_PACKAGE_IDS}
    package_ids_by_key = {package_id.casefold(): package_id for package_id in LIBRARY_PACKAGE_IDS}
    for library in _load_libraries(assets_path):
        package_id, separator, resolved_version = library.rpartition("/")
        if not separator:
            continue
        expected_package_id = package_ids_by_key.get(package_id.casefold())
        if expected_package_id is not None:
            resolved_versions[expected_package_id].add(resolved_version)

    missing = [
        f"{package_id}/{version}"
        for package_id, versions in resolved_versions.items()
        if version not in versions
    ]
    if missing:
        raise ValueError(
            "Exact published library package set was not restored: " + ", ".join(missing)
        )

    wrong_versions = sorted(
        f"{package_id}/{resolved_version}"
        for package_id, versions in resolved_versions.items()
        for resolved_version in versions
        if resolved_version != version
    )
    if wrong_versions:
        raise ValueError(
            "Restore also resolved unexpected ArchLinterNet library versions: "
            + ", ".join(wrong_versions)
        )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--version", required=True)
    return parser


def main() -> None:
    arguments = _parser().parse_args()
    try:
        assets_path = _safe_path(arguments.assets, "NuGet assets file")
        verify_restored_main_packages(assets_path, arguments.version)
    except ValueError as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(2) from error
    print(f"Verified restored ArchLinterNet library package set {arguments.version}.")


if __name__ == "__main__":
    main()
