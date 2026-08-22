#!/usr/bin/env python3
"""Verify every frozen release subject with GitHub-hosted build provenance."""

from __future__ import annotations

import argparse
import subprocess
import tempfile
from pathlib import Path

import package_manifest


def _verified_subjects(arguments: argparse.Namespace) -> list[Path]:
    manifest = package_manifest._verify_release_evidence(
        arguments.packages_dir,
        arguments.manifest,
        arguments.checksums,
    )
    if manifest["version"] != arguments.version:
        raise ValueError("Candidate manifest version does not match the expected version.")
    if manifest["source_commit"] != arguments.source_commit:
        raise ValueError("Candidate manifest source commit does not match the expected source commit.")
    return [
        arguments.packages_dir / subject["file"]
        for subject in package_manifest._subjects(manifest)
    ] + [arguments.manifest, arguments.checksums]


def _command(arguments: argparse.Namespace, subject: Path) -> list[str]:
    return [
        arguments.gh_command,
        "attestation",
        "verify",
        str(subject),
        "--repo",
        arguments.repository,
        "--signer-workflow",
        arguments.signer_workflow,
        "--source-digest",
        arguments.source_commit,
    ]


def _verify(arguments: argparse.Namespace, subject: Path) -> None:
    completed = subprocess.run(_command(arguments, subject), check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        details = completed.stderr.strip() or completed.stdout.strip()
        raise ValueError(f"Attestation verification failed for '{subject.name}': {details}")


def _verify_tamper_is_rejected(arguments: argparse.Namespace, subject: Path, directory: Path) -> None:
    tampered = directory / f"tampered-{subject.name}"
    tampered.write_bytes(subject.read_bytes() + b"\nattestation-tamper-negative\n")
    completed = subprocess.run(_command(arguments, tampered), check=False, capture_output=True, text=True)
    if completed.returncode == 0:
        raise ValueError(f"Tampered release subject unexpectedly verified: '{subject.name}'.")


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages-dir", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--checksums", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--signer-workflow", required=True)
    parser.add_argument("--gh-command", default="gh")
    return parser.parse_args()


def main() -> int:
    arguments = _parse_args()
    subjects = _verified_subjects(arguments)
    for subject in subjects:
        _verify(arguments, subject)

    package_subject = next(subject for subject in subjects if subject.suffix == ".nupkg")
    with tempfile.TemporaryDirectory(prefix="archlinternet-provenance-") as temporary_directory:
        directory = Path(temporary_directory)
        _verify_tamper_is_rejected(arguments, package_subject, directory)
        _verify_tamper_is_rejected(arguments, arguments.manifest, directory)
        _verify_tamper_is_rejected(arguments, arguments.checksums, directory)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
