from __future__ import annotations

from pathlib import Path


_ROOT = Path(__file__).resolve().parents[3]
_GUIDE = _ROOT / "docs" / "guides" / "release-provenance-verification.md"


def test_guide_verifies_outer_evidence_before_package_subjects() -> None:
    guide = _GUIDE.read_text(encoding="utf-8")

    release_download = guide.index("gh release download")
    release_source = guide.index('gh api "repos/$REPOSITORY/commits/$RELEASE_TAG"')
    rehearsal_download = guide.index('gh run download "$RUN_ID"')
    rehearsal_source = guide.index('gh run view "$RUN_ID"')
    manifest_verification = guide.index("gh attestation verify package-manifest.json")
    checksum_verification = guide.index("gh attestation verify package-checksums.txt")
    package_set_verification = guide.index("diff -u expected-package-subjects.txt actual-package-subjects.txt")
    package_hash_verification = guide.index("sha256sum --check expected-package-subjects.sha256")
    package_attestation = guide.index("Verify every package and symbol attestation")

    assert release_download < release_source < manifest_verification
    assert rehearsal_download < rehearsal_source < manifest_verification
    assert manifest_verification < checksum_verification < package_set_verification < package_hash_verification < package_attestation
    assert "while IFS= read -r subject" in guide
    assert ".package.file, .symbols.file" in guide
    assert "for asset in ./*.nupkg ./*.snupkg" in guide
    assert "actual-package-subjects.txt" in guide
    assert "Package/symbol release assets do not match the verified manifest." in guide
    assert "--pattern '*.nupkg'" in guide
    assert "--pattern '*.snupkg'" in guide
    assert '--name "nuget-candidate-$CANDIDATE_VERSION"' in guide
    assert '--json headSha' in guide


def test_guide_documents_fail_closed_tamper_and_nuget_repository_boundaries() -> None:
    guide = _GUIDE.read_text(encoding="utf-8")

    assert "Modifying a package or symbol" in guide
    assert "Modifying `package-manifest.json` or `package-checksums.txt`" in guide
    assert "not expected** to\nequal the pre-upload manifest digest" in guide
    assert "dotnet nuget verify" in guide
    assert "dotnet nuget trust source nuget.org" in guide
    assert 'signatureValidationMode" value="require"' in guide
    assert '<clear />\n    <add key="nuget.org" value="https://api.nuget.org/v3/index.json"' in guide
    assert "generated `trustedSigners` repository entry" in guide
    assert "`serviceIndex` is\n`https://api.nuget.org/v3/index.json`" in guide
    assert 'hashAlgorithm="SHA256"' in guide
    assert 'allowUntrustedRoot="false"' in guide
    assert 'dotnet package download "$PACKAGE_ID@$PACKAGE_VERSION"' in guide
    assert '--source "$NUGET_SOURCE"' in guide
    assert ".NET 10.0.2xx SDK or later" in guide
    assert "https://learn.microsoft.com/dotnet/core/tools/dotnet-package-download" in guide
    assert "mapfile -d '' -t downloaded_packages" in guide
    assert "find ./nuget-package -type f -name '*.nupkg' -print0" in guide
    assert 'export PACKAGE_FILE="${downloaded_packages[0]}"' in guide
    assert 'export PACKAGE_FILE="./nuget-package/$PACKAGE_ID.$PACKAGE_VERSION.nupkg"' not in guide
    assert "Expected exactly one .nuspec" in guide
    assert "Package identity mismatch" in guide
    assert 'dotnet nuget verify "$PACKAGE_FILE"' in guide
    assert guide.index('dotnet package download "$PACKAGE_ID@$PACKAGE_VERSION"') < guide.index(
        "find ./nuget-package -type f -name '*.nupkg' -print0"
    ) < guide.index('dotnet nuget verify "$PACKAGE_FILE"')
    assert guide.index("cat > nuget.config") < guide.index("dotnet nuget trust source nuget.org") < guide.index(
        "dotnet nuget verify"
    )
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
