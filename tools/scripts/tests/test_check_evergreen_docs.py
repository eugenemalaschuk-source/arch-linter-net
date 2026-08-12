from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_evergreen_docs as evergreen  # noqa: E402


def write_repo(root: Path, *, guide: str = "Evergreen guide\n", nav: str | None = None) -> None:
    (root / "docs" / "guides").mkdir(parents=True)
    (root / "docs" / "reference").mkdir(parents=True)
    (root / "docs" / "internal").mkdir(parents=True)
    (root / "README.md").write_text("# ArchLinterNet\n", encoding="utf-8")
    (root / "docs" / "guides" / "upgrading.md").write_text(guide, encoding="utf-8")
    (root / "mkdocs.yml").write_text(
        nav
        or "site_name: ArchLinterNet\nnav:\n  - Guides:\n      - Upgrade: guides/upgrading.md\nexclude_docs: |\n  internal/\n",
        encoding="utf-8",
    )


def test_find_violations_accepts_machine_standard_and_framework_versions(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Adopt or Upgrade ArchLinterNet\n\n"
            "Use `finding/v1`, SARIF 2.1.0, and target framework `net10.0`.\n"
            "Discover exact packaged schema IDs with `schema list`.\n"
        ),
    )
    (tmp_path / "docs" / "reference" / "yaml-schema.md").write_text(
        "# YAML Schema Reference\n\n"
        "Immutable machine contract: "
        "https://archlinternet.dev/schema/0.6.1/dependencies.arch.schema.json\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_find_violations_rejects_version_named_public_docs_path(tmp_path: Path) -> None:
    write_repo(tmp_path)
    path = tmp_path / "docs" / "guides" / "migration-to-0-5-1.md"
    path.write_text("# Migration\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    assert any("migration-to-0-5-1.md" in violation for violation in violations)
    assert any("public docs path identity" in violation for violation in violations)


def test_find_violations_rejects_hardcoded_archlinternet_install_version(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Adopt or Upgrade ArchLinterNet\n\n"
            "`dotnet tool install ArchLinterNet.Cli --version 9.8.7`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("pin package versions in the tool manifest" in violation for violation in violations)


def test_find_violations_rejects_versioned_mkdocs_navigation(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        nav=(
            "site_name: ArchLinterNet\n"
            "nav:\n"
            "  - Guides:\n"
            "      - Upgrade to 9.8.7: guides/upgrading.md\n"
            "exclude_docs: |\n"
            "  internal/\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("public navigation must use a version-neutral identity" in violation for violation in violations)


def test_internal_docs_are_not_part_of_public_guard(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "internal" / "release-9-8-7.md").write_text(
        "ArchLinterNet release 9.8.7 historical evidence\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_main_returns_failure_and_reports_actionable_violation(
    tmp_path: Path, monkeypatch, capsys
) -> None:
    write_repo(tmp_path, guide="ArchLinterNet package release 9.8.7 is current.\n")
    monkeypatch.setattr(evergreen, "repository_root", lambda: tmp_path)

    exit_code = evergreen.main()
    captured = capsys.readouterr()

    assert exit_code == 1
    assert "Evergreen docs guard failed" in captured.err
    assert "product package SemVer is coupled to evergreen prose" in captured.err


def test_main_returns_success_for_clean_tree(tmp_path: Path, monkeypatch, capsys) -> None:
    write_repo(tmp_path)
    monkeypatch.setattr(evergreen, "repository_root", lambda: tmp_path)

    exit_code = evergreen.main()
    captured = capsys.readouterr()

    assert exit_code == 0
    assert "Evergreen docs guard: OK" in captured.out
