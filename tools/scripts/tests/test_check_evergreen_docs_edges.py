from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_evergreen_docs as evergreen  # noqa: E402


def write_repo(root: Path) -> None:
    (root / "docs" / "guides").mkdir(parents=True)
    (root / "docs" / "reference").mkdir(parents=True)
    (root / "README.md").write_text("# ArchLinterNet\n", encoding="utf-8")
    (root / "docs" / "guides" / "upgrading.md").write_text("# Upgrade\n", encoding="utf-8")
    (root / "mkdocs.yml").write_text(
        "site_name: ArchLinterNet\nnav:\n  - Guides:\n      - Upgrade: guides/upgrading.md\n",
        encoding="utf-8",
    )


def test_product_routes_cover_dotted_and_directory_forms_without_blocking_standard(tmp_path: Path) -> None:
    write_repo(tmp_path)
    guides = tmp_path / "docs" / "guides"
    (guides / "archlinternet.9.8.7.md").write_text("# Product release\n", encoding="utf-8")
    directory_route = guides / "archlinternet" / "9.8.7" / "index.md"
    directory_route.parent.mkdir(parents=True)
    directory_route.write_text("# Product release\n", encoding="utf-8")
    standard = tmp_path / "docs" / "reference" / "archlinternet-sarif-2.1.0.md"
    standard.write_text("# ArchLinterNet SARIF 2.1.0\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    assert any("archlinternet.9.8.7.md" in item for item in violations)
    assert any("archlinternet/9.8.7/index.md" in item for item in violations)
    assert not any("archlinternet-sarif-2.1.0.md" in item for item in violations)


def test_product_routes_reject_concept_first_versions_without_to(tmp_path: Path) -> None:
    write_repo(tmp_path)
    guides = tmp_path / "docs" / "guides"
    reference = tmp_path / "docs" / "reference"
    product_paths = (
        guides / "upgrade-9.8.7.md",
        guides / "migration-9.8.7.md",
        reference / "release-9.8.7.md",
        guides / "adoption-9.8.7.md",
        guides / "upgrading-9.8.7.md",
        guides / "installation-9.8.7.md",
        guides / "troubleshooting-9.8.7.md",
    )
    for path in product_paths:
        path.write_text("# Versioned product guide\n", encoding="utf-8")

    concept_directory = guides / "upgrade" / "9.8.7" / "index.md"
    concept_directory.parent.mkdir(parents=True)
    concept_directory.write_text("# Versioned product guide\n", encoding="utf-8")
    version_directory = guides / "9.8.7" / "upgrading.md"
    version_directory.parent.mkdir(parents=True)
    version_directory.write_text("# Versioned product guide\n", encoding="utf-8")

    standard = reference / "sarif-upgrade-2.1.0.md"
    standard.write_text("# Upgrade SARIF to 2.1.0\n", encoding="utf-8")
    standard_directory = reference / "sarif-upgrade" / "2.1.0" / "index.md"
    standard_directory.parent.mkdir(parents=True)
    standard_directory.write_text("# Upgrade SARIF to 2.1.0\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    for path in product_paths:
        assert any(path.name in item for item in violations)
    assert any("upgrade/9.8.7/index.md" in item for item in violations)
    assert any("9.8.7/upgrading.md" in item for item in violations)
    assert not any("sarif-upgrade-2.1.0.md" in item for item in violations)
    assert not any("sarif-upgrade/2.1.0/index.md" in item for item in violations)


def test_product_release_prose_rejects_soft_wraps_without_blocking_external_versions(
    tmp_path: Path,
) -> None:
    write_repo(tmp_path)
    guide = tmp_path / "docs" / "guides" / "upgrading.md"
    guide.write_text(
        "ArchLinterNet release\n"
        "9.8.7 is current.\n\n"
        "The current ArchLinterNet release\n"
        "is 9.8.7.\n\n"
        "> ArchLinterNet release\n"
        "> 9.8.7 is current.\n\n"
        "# ArchLinterNet release output\n"
        "SARIF 2.1.0 is the supported standard format.\n\n"
        "The current SARIF release\n"
        "is 2.1.0.\n\n"
        "ArchLinterNet uses Newtonsoft.Json package\n"
        "version 13.0.4.\n",
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)

    product_violations = [
        item for item in violations if "product package SemVer is coupled" in item
    ]
    assert sum("9.8.7" in item for item in product_violations) >= 3
    assert not any("2.1.0" in item for item in product_violations)
    assert not any("13.0.4" in item for item in product_violations)


def test_product_release_prose_rejects_wrapped_list_items_without_crossing_items(
    tmp_path: Path,
) -> None:
    write_repo(tmp_path)
    guide = tmp_path / "docs" / "guides" / "upgrading.md"
    guide.write_text(
        "- The current ArchLinterNet release\n"
        "  is 9.8.7.\n\n"
        "1. ArchLinterNet release\n"
        "   9.8.7 is current.\n\n"
        "> - The current ArchLinterNet release\n"
        ">   is 9.8.7.\n\n"
        "- ArchLinterNet release\n"
        "- SARIF 2.1.0 is the supported standard format.\n",
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)
    product_violations = [
        item for item in violations if "product package SemVer is coupled" in item
    ]

    assert sum("9.8.7" in item for item in product_violations) >= 3
    assert not any("2.1.0" in item for item in product_violations)


def test_all_contributor_readmes_are_scanned_but_archives_are_excluded(tmp_path: Path) -> None:
    write_repo(tmp_path)
    pin = "dotnet tool install ArchLinterNet.Cli --version 9.8.7\n"
    benchmark_readme = tmp_path / "benchmarks" / "Example.Benchmarks" / "README.md"
    benchmark_readme.parent.mkdir(parents=True)
    benchmark_readme.write_text(pin, encoding="utf-8")
    archived_readme = (
        tmp_path / "openspec" / "changes" / "archive" / "old-change" / "README.md"
    )
    archived_readme.parent.mkdir(parents=True)
    archived_readme.write_text("ArchLinterNet release 9.8.7 historical evidence\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    assert any("benchmarks/Example.Benchmarks/README.md" in item for item in violations)
    assert not any("openspec/changes/archive" in item for item in violations)


def test_msbuild_pin_detection_accepts_valid_attribute_spacing(tmp_path: Path) -> None:
    write_repo(tmp_path)
    guide = tmp_path / "docs" / "guides" / "upgrading.md"
    guide.write_text(
        '<PackageReference Include = "ArchLinterNet.Testing" Version = "9.8.7" />\n'
        '<PackageVersion\n  Include = "ArchLinterNet.Testing"\n  VersionOverride = "9.8.7-preview.1" />\n',
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)

    package_pin_violations = [
        item for item in violations if "pin ArchLinterNet package versions" in item
    ]
    assert len(package_pin_violations) >= 2
