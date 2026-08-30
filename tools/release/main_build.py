#!/usr/bin/env python3
"""Version and retention helpers for installable ArchLinterNet main builds."""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PACKAGE_IDS = (
    "ArchLinterNet.CEL",
    "ArchLinterNet.Cli",
    "ArchLinterNet.Core",
    "ArchLinterNet.Testing",
)
_DEVELOPMENT_PROPERTY = "ArchLinterDevelopmentVersion"
_BASE_VERSION_RE = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
_MAIN_VERSION_RE = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)-main\.([1-9]\d*)$"
)
_RETENTION_SCHEMA = "arch-linter-main-package-retention/v2"


@dataclass(frozen=True)
class PackageVersionInfo:
    version_id: int
    created_at: datetime | None = None


InventoryValue = int | PackageVersionInfo


def _validate_base_version(version: str) -> str:
    if not _BASE_VERSION_RE.fullmatch(version):
        raise ValueError(
            f"{_DEVELOPMENT_PROPERTY} must be a stable SemVer core (major.minor.patch), got '{version}'."
        )
    return version


def read_development_version(props_path: Path) -> str:
    try:
        root = ET.parse(props_path).getroot()
    except (OSError, ET.ParseError) as error:
        raise ValueError(f"Cannot read development version from '{props_path}': {error}") from error

    values = [
        (node.text or "").strip()
        for node in root.iter(_DEVELOPMENT_PROPERTY)
        if (node.text or "").strip()
    ]
    if len(values) != 1:
        raise ValueError(
            f"Expected exactly one non-empty {_DEVELOPMENT_PROPERTY} in '{props_path}', found {len(values)}."
        )
    return _validate_base_version(values[0])


def format_main_version(base_version: str, build_number: int) -> str:
    _validate_base_version(base_version)
    if isinstance(build_number, bool) or build_number <= 0:
        raise ValueError("Main build number must be a positive integer.")
    return f"{base_version}-main.{build_number}"


def _parse_main_version(version: str) -> tuple[int, int, int, int] | None:
    match = _MAIN_VERSION_RE.fullmatch(version)
    if match is None:
        return None
    return tuple(int(match.group(index)) for index in range(1, 5))  # type: ignore[return-value]


def _flatten_inventory(raw: Any, package_id: str) -> list[dict[str, Any]]:
    if not isinstance(raw, list):
        raise ValueError(f"GitHub Packages inventory for {package_id} must be a JSON array.")
    if raw and all(isinstance(item, list) for item in raw):
        raw = [record for page in raw for record in page]
    if not all(isinstance(item, dict) for item in raw):
        raise ValueError(f"GitHub Packages inventory for {package_id} contains an invalid record.")
    return raw


def _parse_github_timestamp(value: Any, package_id: str, version: str) -> datetime | None:
    if value is None:
        return None
    if not isinstance(value, str):
        raise ValueError(
            f"GitHub Packages inventory for {package_id} version '{version}' has an invalid created_at value."
        )
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(
            f"GitHub Packages inventory for {package_id} version '{version}' has an invalid created_at value."
        ) from error
    if parsed.tzinfo is None:
        raise ValueError(
            f"GitHub Packages inventory for {package_id} version '{version}' has a timezone-less created_at value."
        )
    return parsed.astimezone(timezone.utc)


def _load_package_inventory(path: Path, package_id: str) -> dict[str, PackageVersionInfo]:
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read GitHub Packages inventory '{path}': {error}") from error

    records = _flatten_inventory(raw, package_id)
    versions: dict[str, PackageVersionInfo] = {}
    for record in records:
        version = record.get("name")
        version_id = record.get("id")
        if not isinstance(version, str) or not isinstance(version_id, int) or isinstance(version_id, bool):
            raise ValueError(f"GitHub Packages inventory for {package_id} has an invalid id/name record.")
        if version in versions:
            raise ValueError(f"GitHub Packages inventory for {package_id} contains duplicate version '{version}'.")
        versions[version] = PackageVersionInfo(
            version_id=version_id,
            created_at=_parse_github_timestamp(record.get("created_at"), package_id, version),
        )
    return versions


def _version_id(value: InventoryValue) -> int:
    return value if isinstance(value, int) else value.version_id


def _created_at(value: InventoryValue) -> datetime | None:
    return None if isinstance(value, int) else value.created_at


def _is_partial_version_stale(
    version: str,
    inventories: dict[str, dict[str, InventoryValue]],
    stale_partial_before: datetime,
) -> bool:
    records = [
        inventories[package_id][version]
        for package_id in PACKAGE_IDS
        if version in inventories[package_id]
    ]
    created_at_values = [_created_at(record) for record in records]
    return bool(created_at_values) and all(
        created_at is not None and created_at <= stale_partial_before
        for created_at in created_at_values
    )


def create_retention_plan(
    inventories: dict[str, dict[str, InventoryValue]],
    current_version: str,
    keep: int,
    *,
    prune_stale_partials: bool = False,
    stale_partial_before: datetime | None = None,
) -> dict[str, Any]:
    if keep <= 0:
        raise ValueError("Retention count must be a positive integer.")
    current_version_key = _parse_main_version(current_version)
    if current_version_key is None:
        raise ValueError(f"Current package version '{current_version}' is not a main build.")
    if prune_stale_partials and stale_partial_before is None:
        raise ValueError("Stale partial cleanup requires an explicit UTC cutoff.")
    if stale_partial_before is not None:
        if stale_partial_before.tzinfo is None:
            raise ValueError("Stale partial cleanup cutoff must include a timezone.")
        stale_partial_before = stale_partial_before.astimezone(timezone.utc)

    missing_packages = sorted(set(PACKAGE_IDS) - set(inventories))
    unexpected_packages = sorted(set(inventories) - set(PACKAGE_IDS))
    if missing_packages or unexpected_packages:
        raise ValueError(
            f"Package inventory set mismatch: missing={missing_packages}, unexpected={unexpected_packages}."
        )

    main_versions_by_package = {
        package_id: {version for version in versions if _parse_main_version(version) is not None}
        for package_id, versions in inventories.items()
    }
    complete_versions = set.intersection(*(main_versions_by_package[package_id] for package_id in PACKAGE_IDS))
    if current_version not in complete_versions:
        raise ValueError(
            f"Current main build '{current_version}' is not complete across all four package IDs."
        )

    all_main_versions = set.union(*(main_versions_by_package[package_id] for package_id in PACKAGE_IDS))
    partial_versions = sorted(
        all_main_versions - complete_versions,
        key=lambda version: _parse_main_version(version) or (-1, -1, -1, -1),
        reverse=True,
    )
    stale_partial_versions = (
        [
            version
            for version in partial_versions
            if (_parse_main_version(version) or current_version_key) < current_version_key
            and stale_partial_before is not None
            and _is_partial_version_stale(version, inventories, stale_partial_before)
        ]
        if prune_stale_partials
        else []
    )
    stale_partial_set = set(stale_partial_versions)
    protected_partial_versions = [
        version for version in partial_versions if version not in stale_partial_set
    ]
    ordered_complete = sorted(
        complete_versions,
        key=lambda version: _parse_main_version(version) or (-1, -1, -1, -1),
        reverse=True,
    )

    target_retained = ordered_complete[:keep]
    retained = list(target_retained)
    current_retention_deferred = current_version not in target_retained
    if current_retention_deferred:
        retained.append(current_version)

    retained_set = set(retained)
    deletions = []
    orphan_deletions = []
    for package_id in PACKAGE_IDS:
        for version in ordered_complete:
            if version in retained_set:
                continue
            deletions.append(
                {
                    "package_id": package_id,
                    "version": version,
                    "version_id": _version_id(inventories[package_id][version]),
                    "reason": "expired_complete",
                }
            )

        for version in stale_partial_versions:
            record = inventories[package_id].get(version)
            if record is None:
                continue
            orphan_deletions.append(
                {
                    "package_id": package_id,
                    "version": version,
                    "version_id": _version_id(record),
                    "reason": "stale_partial",
                }
            )

    return {
        "schema": _RETENTION_SCHEMA,
        "current_version": current_version,
        "keep": keep,
        "complete_versions": ordered_complete,
        "target_retained_versions": target_retained,
        "retained_versions": retained,
        "partial_versions": partial_versions,
        "orphan_cleanup_enabled": prune_stale_partials,
        "stale_partial_before": (
            stale_partial_before.isoformat().replace("+00:00", "Z")
            if stale_partial_before is not None
            else None
        ),
        "stale_partial_versions": stale_partial_versions,
        "protected_partial_versions": protected_partial_versions,
        "current_retention_deferred": current_retention_deferred,
        "delete": deletions,
        "delete_orphans": orphan_deletions,
    }


def create_retention_plan_from_directory(
    inventory_dir: Path,
    current_version: str,
    keep: int,
    *,
    prune_stale_partials: bool = False,
    stale_partial_before: datetime | None = None,
) -> dict[str, Any]:
    inventories = {
        package_id: _load_package_inventory(inventory_dir / f"{package_id}.json", package_id)
        for package_id in PACKAGE_IDS
    }
    return create_retention_plan(
        inventories,
        current_version,
        keep,
        prune_stale_partials=prune_stale_partials,
        stale_partial_before=stale_partial_before,
    )


def _append_key_value(path: Path | None, key: str, value: str) -> None:
    if path is None:
        return
    with path.open("a", encoding="utf-8") as target:
        target.write(f"{key}={value}\n")


def _version_command(arguments: argparse.Namespace) -> None:
    base_version = read_development_version(arguments.props)
    package_version = format_main_version(base_version, arguments.build_number)
    _append_key_value(arguments.github_env, "ARCH_LINTER_DEVELOPMENT_VERSION", base_version)
    _append_key_value(arguments.github_env, "PACKAGE_VERSION", package_version)
    _append_key_value(arguments.github_output, "development_version", base_version)
    _append_key_value(arguments.github_output, "package_version", package_version)
    print(package_version)


def _retention_command(arguments: argparse.Namespace) -> None:
    plan = create_retention_plan_from_directory(
        arguments.inventory_dir,
        arguments.current_version,
        arguments.keep,
        prune_stale_partials=arguments.prune_stale_partials,
        stale_partial_before=arguments.stale_partial_before,
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(plan, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(
        f"Retention plan: keep={plan['retained_versions']}, "
        f"partial={plan['partial_versions']}, stale_partial={plan['stale_partial_versions']}, "
        f"delete={len(plan['delete'])} package versions, "
        f"orphan_delete={len(plan['delete_orphans'])} package versions"
    )


def _utc_timestamp(value: str) -> datetime:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise argparse.ArgumentTypeError(f"Invalid UTC timestamp: {value}") from error
    if parsed.tzinfo is None:
        raise argparse.ArgumentTypeError("UTC timestamp must include a timezone.")
    return parsed.astimezone(timezone.utc)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    version = subparsers.add_parser("version", help="Resolve the explicit development version and format main.N.")
    version.add_argument("--props", type=Path, required=True)
    version.add_argument("--build-number", type=int, required=True)
    version.add_argument("--github-env", type=Path)
    version.add_argument("--github-output", type=Path)
    version.set_defaults(func=_version_command)

    retention = subparsers.add_parser(
        "retention-plan",
        help="Keep the latest complete main build sets and optionally prune stale partial builds.",
    )
    retention.add_argument("--inventory-dir", type=Path, required=True)
    retention.add_argument("--current-version", required=True)
    retention.add_argument("--keep", type=int, default=5)
    retention.add_argument(
        "--prune-stale-partials",
        action="store_true",
        help="Delete old partial main versions behind the current complete build.",
    )
    retention.add_argument(
        "--stale-partial-before",
        type=_utc_timestamp,
        help="UTC cutoff; every existing package record for a partial version must be this old before deletion.",
    )
    retention.add_argument("--output", type=Path, required=True)
    retention.set_defaults(func=_retention_command)

    return parser


def main() -> None:
    arguments = _parser().parse_args()
    try:
        arguments.func(arguments)
    except ValueError as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(2) from error


if __name__ == "__main__":
    main()
