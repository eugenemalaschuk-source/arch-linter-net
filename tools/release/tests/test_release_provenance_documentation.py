from __future__ import annotations

from pathlib import Path


_ROOT = Path(__file__).resolve().parents[3]
_GUIDE = _ROOT / "docs" / "guides" / "release-provenance-verification.md"


def test_guide_verifies_outer_evidence_before_package_subjects() -> None:
    guide = _GUIDE.read_text(encoding="utf-8")

    manifest_verification = guide.index("gh attestation verify package-manifest.json")
    checksum_verification = guide.index("gh attestation verify package-checksums.txt")
    package_hash_verification = guide.index("sha256sum --check expected-package-subjects.sha256")
    package_attestation = guide.index("Verify every package and symbol attestation")

    assert manifest_verification < checksum_verification < package_hash_verification < package_attestation
    assert "while IFS= read -r subject" in guide
    assert ".package.file, .symbols.file" in guide


def test_guide_documents_fail_closed_tamper_and_nuget_repository_boundaries() -> None:
    guide = _GUIDE.read_text(encoding="utf-8")

    assert "Modifying a package or symbol" in guide
    assert "Modifying `package-manifest.json` or `package-checksums.txt`" in guide
    assert "not expected** to\nequal the pre-upload manifest digest" in guide
    assert "dotnet nuget verify" in guide
    assert "Do not strip or rewrite signatures" in guide
    assert "not assumed to mirror the primary package" in guide


def test_guide_records_distinct_author_signing_and_sbom_decisions() -> None:
    guide = _GUIDE.read_text(encoding="utf-8")

    assert "### NuGet author signing: deferred" in guide
    assert "GitHub provenance and NuGet.org repository signing are not author" in guide
    assert "### Package-level SBOM: deferred" in guide
    assert "separate focused issue" in guide
    assert "formal SLSA-level claim" in guide


def test_public_entry_points_link_to_the_canonical_guide() -> None:
    guide_url = "https://eugenemalaschuk-source.github.io/arch-linter-net/guides/release-provenance-verification/"
    readme = (_ROOT / "README.md").read_text(encoding="utf-8")
    release_process = (_ROOT / "docs" / "reference" / "release-process.md").read_text(encoding="utf-8")
    mkdocs = (_ROOT / "mkdocs.yml").read_text(encoding="utf-8")

    assert guide_url in readme
    assert "guides/release-provenance-verification.md" in mkdocs
    assert "[release-provenance verification guide](../guides/release-provenance-verification.md)" in release_process
