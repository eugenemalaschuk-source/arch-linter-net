from __future__ import annotations

import argparse
import gzip
import json
import subprocess
import sys
import tarfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "release"))

import package_manifest  # noqa: E402
import release_distribution as distribution  # noqa: E402


INVENTORY = ROOT / ".github" / "badge-promotion" / "release-inventory.json"
BUNDLE_MANIFEST = ROOT / "relay" / "bundle-manifest.json"
LIFECYCLE_SOURCE = "relay/src/lifecycle.ts"


def _release_source_path(bundle_path: str) -> str:
    return bundle_path if bundle_path.startswith("schema/") else f"relay/{bundle_path}"


def _candidate(tmp_path: Path, version: str = "0.8.7-preview.1") -> tuple[Path, str]:
    packages = tmp_path / "candidate"
    packages.mkdir(parents=True)
    source_commit = subprocess.check_output(
        ["git", "-C", str(ROOT), "rev-parse", "HEAD"], text=True
    ).strip()
    for package_id in package_manifest._PACKAGE_IDS:
        for kind in package_manifest._SUBJECT_KINDS:
            path = packages / package_manifest._expected_filename(package_id, version, kind)
            path.write_bytes(f"{package_id}/{kind}".encode())
    manifest = packages / "package-manifest.json"
    package_manifest._create(
        argparse.Namespace(
            packages_dir=packages,
            version=version,
            source_commit=source_commit,
            output=manifest,
        )
    )
    return manifest, source_commit


def test_release_distribution_covers_every_declared_bundle_manifest_file() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    bundle_manifest = json.loads(BUNDLE_MANIFEST.read_text(encoding="utf-8"))

    declared = {_release_source_path(record["path"]) for record in bundle_manifest["files"]}
    inventoried = {record["path"] for record in inventory["bundle_members"]}
    reviewed = {path for path, _, _ in distribution._REVIEWED_BUNDLE_MEMBERS}

    assert LIFECYCLE_SOURCE in declared
    assert declared <= inventoried
    assert declared <= reviewed


def test_generated_relay_archive_contains_exact_lifecycle_source_bytes(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    version = "0.8.7-preview.1"
    candidate_manifest, source_commit = _candidate(tmp_path, version)
    transport = tmp_path / "transport"

    distribution._create(
        argparse.Namespace(
            source_root=ROOT,
            inventory=INVENTORY,
            candidate_manifest=candidate_manifest,
            version=version,
            source_commit=source_commit,
            output_dir=transport,
        )
    )

    archive_path = transport / distribution._ARCHIVE_FILE_TEMPLATE.format(version=version)
    with gzip.open(archive_path, "rb") as compressed, tarfile.open(fileobj=compressed, mode="r") as archive:
        member = archive.extractfile(LIFECYCLE_SOURCE)
        assert member is not None
        assert member.read() == (ROOT / LIFECYCLE_SOURCE).read_bytes()
