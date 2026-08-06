#!/usr/bin/env python3
"""Create and verify the immutable package manifest used by Checkpoint B."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


_PACKAGE_IDS = (
    "ArchLinterNet.CEL",
    "ArchLinterNet.Cli",
    "ArchLinterNet.Core",
    "ArchLinterNet.Testing",
)
_SCHEMA = "checkpoint-b-candidate-manifest/v1"


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _expected_path(packages_directory: Path, package_id: str, version: str) -> Path:
    return packages_directory / f"{package_id}.{version}.nupkg"


def _create(arguments: argparse.Namespace) -> None:
    records = []
    for package_id in _PACKAGE_IDS:
        path = _expected_path(arguments.packages_dir, package_id, arguments.version)
        if not path.is_file():
            raise ValueError(f"Missing candidate package: {path}")
        records.append(
            {
                "id": package_id,
                "version": arguments.version,
                "file": path.name,
                "size": path.stat().st_size,
                "sha256": _sha256(path),
            }
        )

    unexpected = sorted(
        path.name
        for path in arguments.packages_dir.glob("*.nupkg")
        if path.name not in {record["file"] for record in records}
    )
    if unexpected:
        raise ValueError(f"Unexpected candidate packages: {', '.join(unexpected)}")

    manifest = {
        "schema": _SCHEMA,
        "version": arguments.version,
        "source_commit": arguments.source_commit,
        "packages": records,
    }
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _verify(arguments: argparse.Namespace) -> None:
    manifest = json.loads(arguments.manifest.read_text(encoding="utf-8"))
    if manifest.get("schema") != _SCHEMA:
        raise ValueError("Unsupported candidate manifest schema.")
    records = manifest.get("packages")
    if not isinstance(records, list) or [record.get("id") for record in records] != list(_PACKAGE_IDS):
        raise ValueError("Candidate manifest package inventory is invalid.")

    for record in records:
        path = arguments.packages_dir / str(record["file"])
        if not path.is_file():
            raise ValueError(f"Missing manifested package: {path}")
        if path.stat().st_size != record.get("size") or _sha256(path) != record.get("sha256"):
            raise ValueError(f"Candidate package digest mismatch: {path.name}")


def main() -> int:
    parser = argparse.ArgumentParser()
    subcommands = parser.add_subparsers(dest="command", required=True)

    create = subcommands.add_parser("create")
    create.add_argument("--packages-dir", type=Path, required=True)
    create.add_argument("--version", required=True)
    create.add_argument("--source-commit", required=True)
    create.add_argument("--output", type=Path, required=True)
    create.set_defaults(handler=_create)

    verify = subcommands.add_parser("verify")
    verify.add_argument("--packages-dir", type=Path, required=True)
    verify.add_argument("--manifest", type=Path, required=True)
    verify.set_defaults(handler=_verify)

    arguments = parser.parse_args()
    arguments.handler(arguments)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
