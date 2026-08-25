#!/usr/bin/env python3
"""Verify every frozen release subject with GitHub-hosted build provenance."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import tempfile
from pathlib import Path
from typing import Any

import package_manifest
from _release_workspace import _safe_path


_GH_COMMAND = "gh"
_REPOSITORY_PATTERN = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
_SOURCE_COMMIT_PATTERN = re.compile(r"[0-9a-f]{40,64}")
_WORKFLOW_PATTERN = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml")


def _outer_evidence_subjects(arguments: argparse.Namespace) -> tuple[Path, Path, Path]:
    return package_manifest._canonical_evidence_paths(
        arguments.packages_dir,
        arguments.manifest,
        arguments.checksums,
    )


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
        _safe_path(arguments.packages_dir / subject["file"], "candidate package subject")
        for subject in package_manifest._subjects(manifest)
    ]


def _command(arguments: argparse.Namespace, subject: Path) -> list[str]:
    subject = _safe_path(subject, "attestation subject")
    repository = _validated_selector(arguments.repository, _REPOSITORY_PATTERN, "repository")
    workflow = _validated_selector(arguments.signer_workflow, _WORKFLOW_PATTERN, "signer workflow")
    source_commit = _validated_selector(arguments.source_commit, _SOURCE_COMMIT_PATTERN, "source commit")
    return [
        _GH_COMMAND,
        "attestation",
        "verify",
        str(subject),
        "--repo",
        repository,
        "--signer-workflow",
        workflow,
        "--source-digest",
        source_commit,
        "--format",
        "json",
    ]


def _validated_selector(value: Any, pattern: re.Pattern[str], description: str) -> str:
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise ValueError(f"Attestation {description} is invalid.")
    return value


def _verify(arguments: argparse.Namespace, subject: Path) -> list[dict[str, Any]]:
    # _command uses the literal `gh` executable; all selectors and the file subject are
    # validated before they enter this argument vector, and subprocess is never run through a shell.
    completed = subprocess.run(_command(arguments, subject), check=False, capture_output=True, text=True)  # NOSONAR(S4721,S8705)
    if completed.returncode != 0:
        details = completed.stderr.strip() or completed.stdout.strip()
        raise ValueError(f"Attestation verification failed for '{subject.name}': {details}")
    try:
        attestations = json.loads(completed.stdout)
    except json.JSONDecodeError as error:
        raise ValueError(f"Attestation verification returned invalid JSON for '{subject.name}'.") from error
    if not isinstance(attestations, list) or not attestations:
        raise ValueError(f"Attestation verification returned no attestations for '{subject.name}'.")
    return attestations


def _attested_subjects(attestation: dict[str, Any]) -> list[dict[str, Any]]:
    verification_result = attestation.get("verificationResult")
    if not isinstance(verification_result, dict):
        return []
    statement = verification_result.get("statement")
    if not isinstance(statement, dict):
        return []
    subjects = statement.get("subject")
    if not isinstance(subjects, list):
        return []
    return [subject for subject in subjects if isinstance(subject, dict)]


def _subject_digest(subject: dict[str, Any]) -> str | None:
    digest = subject.get("digest")
    if not isinstance(digest, dict):
        return None
    sha256 = digest.get("sha256")
    return sha256 if isinstance(sha256, str) else None


def _verified_attestation_digests(subject: Path, attestations: list[dict[str, Any]]) -> set[str]:
    digests = {
        digest
        for attestation in attestations
        for attested_subject in _attested_subjects(attestation)
        if (digest := _subject_digest(attested_subject)) is not None
    }
    if package_manifest._sha256(subject) not in digests:
        raise ValueError(f"Verified attestation does not contain '{subject.name}' as a SHA-256 subject.")
    return digests


def _verify_tamper_is_rejected(subject: Path, attested_digests: set[str], directory: Path) -> None:
    subject = _safe_path(subject, "attestation subject")
    directory = _safe_path(directory, "tamper evidence directory")
    tampered = _safe_path(directory / f"tampered-{subject.name}", "tampered release subject")
    # subject and tampered are confined by _safe_path above, and tampered is created in a
    # TemporaryDirectory rooted inside the already confined candidate packages directory.
    tampered.write_bytes(subject.read_bytes() + b"\nattestation-tamper-negative\n")  # NOSONAR(S2083,S8707)
    if package_manifest._sha256(tampered) in attested_digests:
        raise ValueError(f"Tampered release subject unexpectedly matches an attestation: '{subject.name}'.")


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages-dir", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--checksums", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--signer-workflow", required=True)
    return parser.parse_args()


def main() -> int:
    arguments = _parse_args()
    verified_attestations: dict[Path, set[str]] = {}
    packages_directory, manifest_subject, checksums_subject = _outer_evidence_subjects(arguments)
    for subject in (manifest_subject, checksums_subject):
        verified_attestations[subject] = _verified_attestation_digests(subject, _verify(arguments, subject))

    subjects = _verified_subjects(arguments)
    for subject in subjects:
        verified_attestations[subject] = _verified_attestation_digests(subject, _verify(arguments, subject))

    package_subject = next(subject for subject in subjects if subject.suffix == ".nupkg")
    with tempfile.TemporaryDirectory(prefix="archlinternet-provenance-", dir=packages_directory) as temporary_directory:
        directory = Path(temporary_directory)
        _verify_tamper_is_rejected(package_subject, verified_attestations[package_subject], directory)
        _verify_tamper_is_rejected(manifest_subject, verified_attestations[manifest_subject], directory)
        _verify_tamper_is_rejected(checksums_subject, verified_attestations[checksums_subject], directory)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
