#!/usr/bin/env python3
"""Verify the retained self-dogfood report against its documented SHA-256."""

from __future__ import annotations

import hashlib
import re
import sys
from pathlib import Path

ARTIFACT_RELATIVE_PATH = Path("docs/internal/dogfood-v0.7.0-release-forensics.json")
EVIDENCE_RELATIVE_PATH = Path("docs/internal/dogfood-v0.7.0-reference-evidence.md")
DIGEST_PATTERN = re.compile(
    r"^Canonical artifact SHA-256: `(?P<digest>[0-9a-f]{64})`$",
    re.MULTILINE,
)
CHUNK_SIZE = 1024 * 1024


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def stream_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(CHUNK_SIZE), b""):
            digest.update(chunk)
    return digest.hexdigest()


def documented_digest(evidence_path: Path) -> str:
    matches = DIGEST_PATTERN.findall(evidence_path.read_text(encoding="utf-8"))
    if len(matches) != 1:
        raise ValueError(
            f"{EVIDENCE_RELATIVE_PATH.as_posix()} must declare exactly one canonical artifact SHA-256"
        )
    return matches[0]


def find_violations(root: Path) -> list[str]:
    evidence_path = root / EVIDENCE_RELATIVE_PATH
    artifact_path = root / ARTIFACT_RELATIVE_PATH
    violations: list[str] = []
    if not evidence_path.is_file():
        return [f"Missing evidence record: {EVIDENCE_RELATIVE_PATH.as_posix()}"]
    if not artifact_path.is_file():
        return [f"Missing canonical artifact: {ARTIFACT_RELATIVE_PATH.as_posix()}"]

    try:
        expected_digest = documented_digest(evidence_path)
    except ValueError as error:
        return [str(error)]

    actual_digest = stream_sha256(artifact_path)
    if actual_digest != expected_digest:
        violations.append(
            "Canonical artifact SHA-256 mismatch: "
            f"expected {expected_digest}, got {actual_digest}"
        )
    return violations


def main() -> int:
    violations = find_violations(repository_root())
    if not violations:
        print("Dogfood reference evidence: OK")
        return 0

    print("Dogfood reference evidence check failed:", file=sys.stderr)
    for violation in violations:
        print(f"- {violation}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
