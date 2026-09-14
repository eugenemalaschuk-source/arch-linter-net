#!/usr/bin/env python3
"""Perform the offline dependency and license checks for the shipped Relay."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Any

from _release_workspace import _safe_path


_EXACT_VERSION = re.compile(r"^[0-9A-Za-z][0-9A-Za-z.+-]*$")
_NODE_MODULES = "/node_modules/"


def _read_json(path: Path, description: str) -> dict[str, Any]:
    path = _safe_path(path, description)
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"The {description} '{path}' is not a regular file.")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read {description} '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"The {description} must be a JSON object.")
    return value


def _relative_path(root: Path, value: Any, description: str) -> Path:
    if not isinstance(value, str) or not value or "\\" in value or value.startswith("/"):
        raise ValueError(f"The {description} path is invalid.")
    relative = PurePosixPath(value)
    if value != str(relative) or any(part in ("", ".", "..") for part in relative.parts) or ":" in value:
        raise ValueError(f"The {description} path is unsafe.")
    path = _safe_path(root.joinpath(*relative.parts), description)
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"The {description} '{value}' is missing or not a regular file.")
    return path


def _dependency_map(value: Any, description: str) -> dict[str, str]:
    if not isinstance(value, dict) or any(not isinstance(key, str) or not isinstance(item, str) or not item for key, item in value.items()):
        raise ValueError(f"Relay {description} must be a string dependency map.")
    return dict(value)


def _license_present(value: Any) -> bool:
    if isinstance(value, str):
        return bool(value.strip())
    if isinstance(value, dict):
        return bool(value) and all(isinstance(key, str) and isinstance(item, str) and item.strip() for key, item in value.items())
    return False


def _license_label(value: Any) -> str:
    if isinstance(value, str):
        return value.strip()
    if isinstance(value, dict):
        for key in ("type", "spdx", "name"):
            label = value.get(key)
            if isinstance(label, str) and label.strip():
                return label.strip()
        return ", ".join(str(item).strip() for item in value.values() if isinstance(item, str) and item.strip())
    return ""


def _package_name(package_path: str) -> str:
    return package_path.removeprefix("node_modules/").rsplit(_NODE_MODULES, 1)[-1]


def _notice_documents(notice: str, package_name: str, version: str, license_label: str) -> bool:
    package_pattern = re.compile(rf"(?<![A-Za-z0-9]){re.escape(package_name)}(?![A-Za-z0-9])")
    return any(
        package_pattern.search(line) is not None and version in line and license_label in line
        for line in notice.splitlines()
    )


def _package_entry(packages: dict[str, Any], package_name: str, parent: str = "") -> tuple[str, dict[str, Any]] | None:
    path = parent
    while True:
        candidate = f"{path}{_NODE_MODULES}{package_name}" if path else f"node_modules/{package_name}"
        value = packages.get(candidate)
        if isinstance(value, dict):
            return candidate, value
        if not path:
            return None
        path = path.rsplit(_NODE_MODULES, 1)[0] if _NODE_MODULES in path else ""


def _runtime_dependency_closure(packages: dict[str, Any], direct: dict[str, str]) -> set[str]:
    seen: set[str] = set()
    pending: list[tuple[str, str]] = [(name, "") for name in direct]
    while pending:
        name, parent = pending.pop()
        entry = _package_entry(packages, name, parent)
        if entry is None:
            raise ValueError(f"Runtime dependency '{name}' is missing from the lockfile.")
        package_path, package = entry
        if package_path in seen:
            continue
        seen.add(package_path)
        dependencies = _dependency_map(package.get("dependencies", {}), f"lock package '{package_path}' dependencies")
        pending.extend((child, package_path) for child in dependencies)
    return seen


def _load_lock(package_json_path: Path, package_lock_path: Path) -> tuple[dict[str, Any], dict[str, str]]:
    package = _read_json(package_json_path, "Relay package.json")
    lock = _read_json(package_lock_path, "Relay package-lock.json")
    dependencies = _dependency_map(package.get("dependencies"), "runtime dependencies")
    dev_dependencies = _dependency_map(package.get("devDependencies", {}), "development dependencies")
    if lock.get("lockfileVersion") != 3:
        raise ValueError("Relay package-lock.json must use lockfileVersion 3.")
    packages = lock.get("packages")
    if not isinstance(packages, dict) or not isinstance(packages.get(""), dict):
        raise ValueError("Relay package-lock.json has no root package entry.")
    lock_root = packages[""]
    if _dependency_map(lock_root.get("dependencies"), "lockfile runtime dependencies") != dependencies:
        raise ValueError("Relay direct runtime dependencies do not match package-lock.json.")
    if _dependency_map(lock_root.get("devDependencies", {}), "lockfile development dependencies") != dev_dependencies:
        raise ValueError("Relay direct development dependencies do not match package-lock.json.")
    return packages, dependencies


def _validate_direct_versions(packages: dict[str, Any], dependencies: dict[str, str]) -> None:
    for name, requested in dependencies.items():
        entry = _package_entry(packages, name)
        if entry is None:
            raise ValueError(f"Runtime dependency '{name}' is missing from the lockfile.")
        locked_version = entry[1].get("version")
        if _EXACT_VERSION.fullmatch(requested) and locked_version != requested:
            raise ValueError(f"Relay runtime dependency '{name}' is locked to an unexpected version.")


def _validate_lock_metadata(packages: dict[str, Any]) -> None:
    for path, record in packages.items():
        if path == "":
            continue
        if not isinstance(path, str) or not path.startswith("node_modules/") or not isinstance(record, dict):
            raise ValueError(f"Relay lock package '{path}' is invalid.")
        for field in ("version", "resolved", "integrity"):
            if not isinstance(record.get(field), str) or not record[field].strip():
                raise ValueError(f"Relay lock package '{path}' has no {field} metadata.")
        if not _license_present(record.get("license")):
            raise ValueError(f"Relay lock package '{path}' has no license metadata.")


def _validate_notice(notice: str, packages: dict[str, Any], audited: set[str]) -> None:
    for package_path in sorted(audited):
        package = packages[package_path]
        package_name = _package_name(package_path)
        locked_version = package["version"]
        license_label = _license_label(package["license"])
        if not _notice_documents(notice, package_name, locked_version, license_label):
            raise ValueError(
                f"Relay license notice does not document runtime dependency '{package_name}' "
                f"version '{locked_version}' with license '{license_label}'."
            )


def verify_relay_dependencies(source_root: Path, inventory: dict[str, Any]) -> dict[str, Any]:
    """Validate Relay package metadata without network access.

    Every non-root lock package must carry resolution, integrity, and license metadata. The
    notice must document every package in the runtime dependency closure with its locked version
    and license; devDependencies are intentionally not treated as shipped runtime dependencies.
    """

    root = _safe_path(source_root, "source root")
    if not root.is_dir() or root.is_symlink():
        raise ValueError(f"The source root '{root}' is invalid.")
    relay = inventory.get("relay") if isinstance(inventory, dict) else None
    if not isinstance(relay, dict):
        raise ValueError("The release inventory has no Relay dependency declaration.")
    package_json_path = _relative_path(root, relay.get("package_json"), "Relay package.json")
    package_lock_path = _relative_path(root, relay.get("package_lock"), "Relay package-lock.json")
    notice_path = _relative_path(root, relay.get("license_notice"), "Relay license notice")
    notice = notice_path.read_text(encoding="utf-8")
    if not notice.strip():
        raise ValueError("The Relay license notice is empty.")
    packages, dependencies = _load_lock(package_json_path, package_lock_path)
    _validate_direct_versions(packages, dependencies)
    _validate_lock_metadata(packages)
    audited = _runtime_dependency_closure(packages, dependencies)
    _validate_notice(notice, packages, audited)
    return {
        "package": package_json_path.as_posix(),
        "lockfile": package_lock_path.as_posix(),
        "license_notice": notice_path.as_posix(),
        "lock_package_count": len(packages) - 1,
        "audited_runtime_packages": sorted(audited),
    }


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--inventory", type=Path)
    return parser.parse_args()


def main() -> int:
    arguments = _parse_args()
    try:
        inventory_path = arguments.inventory or Path(__file__).resolve().parents[2] / ".github" / "badge-promotion" / "release-inventory.json"
        inventory = _read_json(inventory_path, "release inventory")
        report = verify_relay_dependencies(arguments.source_root, inventory)
    except (OSError, ValueError) as error:
        print(f"Relay dependency verification failed: {error}", file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
