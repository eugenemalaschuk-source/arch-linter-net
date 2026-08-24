from __future__ import annotations

import hashlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_dogfood_reference_evidence as evidence  # noqa: E402


def write_reference(root: Path, artifact: bytes, digest: str | None = None) -> None:
    internal = root / "docs" / "internal"
    internal.mkdir(parents=True)
    (internal / "dogfood-v0.7.0-release-forensics.json").write_bytes(artifact)
    expected = digest or hashlib.sha256(artifact).hexdigest()
    (internal / "dogfood-v0.7.0-reference-evidence.md").write_text(
        f"Canonical artifact SHA-256: `{expected}`\n",
        encoding="utf-8",
    )


def test_accepts_retained_artifact_with_documented_digest(tmp_path: Path) -> None:
    write_reference(tmp_path, b'{"schemaVersion":1}\n')

    assert evidence.find_violations(tmp_path) == []


def test_rejects_artifact_that_does_not_match_documented_digest(tmp_path: Path) -> None:
    write_reference(tmp_path, b'{"schemaVersion":1}\n', "0" * 64)

    violations = evidence.find_violations(tmp_path)

    assert len(violations) == 1
    assert "SHA-256 mismatch" in violations[0]


def test_rejects_missing_or_ambiguous_digest_marker(tmp_path: Path) -> None:
    write_reference(tmp_path, b'{"schemaVersion":1}\n')
    evidence_path = tmp_path / evidence.EVIDENCE_RELATIVE_PATH
    evidence_path.write_text("No digest marker\n", encoding="utf-8")

    assert "must declare exactly one" in evidence.find_violations(tmp_path)[0]
