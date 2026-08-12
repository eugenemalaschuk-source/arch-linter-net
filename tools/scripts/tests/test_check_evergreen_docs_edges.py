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
