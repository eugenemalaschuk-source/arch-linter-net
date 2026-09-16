#!/usr/bin/env python3
"""Read-only source composition check; never authorizes a candidate or release.

Compare the existing setup bundle manifest with the release owner's transport
allowlist and approved pins. This does not download, hash, execute or deploy
artifacts, and cannot substitute for packed/platform/live acceptance.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path, PurePosixPath
from typing import Any


INVENTORY_PATH = ".github/badge-promotion/release-inventory.json"
BUNDLE_PATH = "relay/bundle-manifest.json"


def _read_object(root: Path, relative: str) -> dict[str, Any]:
    value = json.loads((root / relative).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{relative}: expected an object")
    return value


def _paths(value: Any, key: str, description: str) -> set[str]:
    if not isinstance(value, list) or not value:
        raise ValueError(f"{description}: expected a non-empty list")
    paths: set[str] = set()
    for member in value:
        path = member.get(key) if isinstance(member, dict) else None
        if not isinstance(path, str) or not path:
            raise ValueError(f"{description}: missing path")
        parsed = PurePosixPath(path)
        if (
            not parsed.parts
            or parsed.is_absolute()
            or str(parsed) != path
            or any(part in (".", "..") for part in parsed.parts)
            or any(character in path for character in ("\\", ":", "\x00"))
        ):
            raise ValueError(f"{description}: unsafe path")
        if path in paths:
            raise ValueError(f"{description}: duplicate path {path}")
        paths.add(path)
    return paths


def _pin_violations(inventory: dict[str, Any], bundle: dict[str, Any]) -> list[str]:
    compatibility = inventory.get("compatibility")
    pins = bundle.get("publisher_pins")
    if not isinstance(compatibility, dict) or not isinstance(pins, dict):
        return ["Missing publisher compatibility/pins"]
    commit = compatibility.get("publisher_commit")
    if (
        not isinstance(commit, str)
        or len(commit) != 40
        or any(character not in "0123456789abcdef" for character in commit)
    ):
        return ["Release publisher commit must be an immutable 40-hex SHA"]
    errors = []
    for key in ("commit", "workflow_sha", "action_sha"):
        if pins.get(key) != commit:
            errors.append(f"Bundle publisher pin {key} differs from release inventory")
    workflow = pins.get("workflow_ref")
    action = pins.get("action_ref")
    if not isinstance(workflow, str) or compatibility.get("workflow_ref") != f"{workflow}@{commit}":
        errors.append("Bundle workflow reference differs from release inventory")
    if not isinstance(action, str) or not action.endswith(f"@{commit}") or action != compatibility.get("action_ref"):
        errors.append("Bundle action reference differs from release inventory")
    if bundle.get("bundle") != compatibility.get("bundle"):
        errors.append("Bundle identity differs from release inventory")
    if bundle.get("compatibility_plan") != compatibility.get("plan"):
        errors.append("Bundle compatibility plan differs from release inventory")
    return errors


def find_violations(root: Path) -> list[str]:
    """Return deterministic source-inventory defects, not a delivery verdict."""
    try:
        inventory = _read_object(root, INVENTORY_PATH)
        bundle = _read_object(root, BUNDLE_PATH)
        if inventory.get("schema") != "architecture-health-badge-release-inventory/v2":
            raise ValueError("Unsupported release inventory schema")
        if bundle.get("schema_id") != "badge-relay-bundle-manifest/v1":
            raise ValueError("Unsupported setup bundle manifest schema")
        delivered = _paths(inventory.get("bundle_members"), "archive_path", "release members")
        required = _paths(bundle.get("files"), "path", "setup bundle files")
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        return [f"Cannot inspect source inventory: {error}"]

    # The setup manifest is relative to its Relay directory, except the schema
    # stored under the distribution's root schema/ directory.
    required_archive_paths = {
        path if path.startswith("schema/") else f"relay/{path}"
        for path in required
    }
    errors = [
        f"Release inventory omits setup bundle file: {path}"
        for path in sorted(required_archive_paths - delivered)
    ]
    errors.extend(_pin_violations(inventory, bundle))
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    violations = find_violations(args.root)
    if violations:
        print("BLOCKED: source inventory composition")
        for violation in violations:
            print(f"- {violation}")
        return 1
    print("Source inventory is consistent; packed/platform/live acceptance is still required.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
