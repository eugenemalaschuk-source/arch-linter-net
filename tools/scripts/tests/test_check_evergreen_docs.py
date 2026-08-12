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


def test_yaml_schema_page_rejects_product_release_status(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "reference" / "yaml-schema.md").write_text(
        "# YAML Schema Reference\n\n"
        "Immutable machine contract: "
        "https://archlinternet.dev/schema/0.6.1/dependencies.arch.schema.json\n\n"
        "ArchLinterNet release 9.8.7 is current.\n",
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("product package SemVer is coupled to evergreen prose" in violation for violation in violations)


def test_find_violations_accepts_external_release_and_package_versions(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Interoperability\n\n"
            "SARIF 2.1.0 is the current release of the standard.\n"
            "The current SARIF release is 2.1.0.\n"
            "The current Newtonsoft.Json package version is 13.0.4.\n"
            "ArchLinterNet supports SARIF 2.1.0, the current release of the standard.\n"
            "ArchLinterNet supports SARIF release 2.1.0.\n"
            "ArchLinterNet uses Newtonsoft.Json package version 13.0.4.\n"
            "The current SARIF release in ArchLinterNet is 2.1.0.\n"
            "Use SARIF migration-to-2.1.0 terminology when discussing the standard.\n"
        ),
    )

    assert evergreen.find_violations(tmp_path) == []


def test_find_violations_accepts_versioned_standard_and_contract_paths_and_navigation(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        nav=(
            "site_name: ArchLinterNet\n"
            "nav:\n"
            "  - Guides:\n"
            "      - Upgrade: guides/upgrading.md\n"
            "  - Reference:\n"
            "      - ArchLinterNet SARIF 2.1.0: reference/sarif-2.1.0.md\n"
            "      - Protocol v2.1.0: reference/v2.1.0.md\n"
            "      - SARIF Migration to 2.1.0: reference/sarif-migration-to-2.1.0.md\n"
            "      - SARIF Release Notes 2.1.0: reference/sarif-release-notes-2.1.0.md\n"
            "      - Upgrade SARIF to 2.1.0: reference/sarif-upgrade-to-2.1.0.md\n"
            "exclude_docs: |\n"
            "  internal/\n"
        ),
    )
    (tmp_path / "docs" / "reference" / "sarif-2.1.0.md").write_text(
        "# ArchLinterNet SARIF 2.1.0\n\nStandard-format reference.\n",
        encoding="utf-8",
    )
    (tmp_path / "docs" / "reference" / "v2.1.0.md").write_text(
        "# Protocol v2.1.0\n\nPersisted contract reference.\n",
        encoding="utf-8",
    )
    (tmp_path / "docs" / "reference" / "sarif-migration-to-2.1.0.md").write_text(
        "# SARIF Migration to 2.1.0\n\nStandard migration reference.\n",
        encoding="utf-8",
    )
    (tmp_path / "docs" / "reference" / "sarif-release-notes-2.1.0.md").write_text(
        "# SARIF Release Notes 2.1.0\n\nStandard release reference.\n",
        encoding="utf-8",
    )
    (tmp_path / "docs" / "reference" / "sarif-upgrade-to-2.1.0.md").write_text(
        "# Upgrade SARIF to 2.1.0\n\nStandard upgrade reference.\n",
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


def test_find_violations_rejects_version_first_product_concept_path(tmp_path: Path) -> None:
    write_repo(tmp_path)
    path = tmp_path / "docs" / "guides" / "v9-8-7-upgrade.md"
    path.write_text("# Upgrade\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    assert any("v9-8-7-upgrade.md" in violation for violation in violations)


def test_find_violations_rejects_versioned_root_readme_identity(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "README-v9.8.7.md").write_text("# Historical-looking README\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)

    assert any("README-v9.8.7.md" in violation for violation in violations)


def test_find_violations_rejects_version_first_public_package_line(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide="0.6.4 is the public adoption package line for ArchLinterNet.\n",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("product package SemVer is coupled to evergreen prose" in violation for violation in violations)


def test_find_violations_rejects_explicit_current_archlinternet_release(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide="The current ArchLinterNet release is 9.8.7.\n",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("product package SemVer is coupled to evergreen prose" in violation for violation in violations)


def test_find_violations_rejects_hardcoded_archlinternet_cli_install_version(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Adopt or Upgrade ArchLinterNet\n\n"
            "`dotnet tool install ArchLinterNet.Cli --version 9.8.7`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("pin ArchLinterNet package versions" in violation for violation in violations)


def test_find_violations_rejects_global_tool_install_pin(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide="`dotnet tool install --global ArchLinterNet.Cli --version 9.8.7`\n",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("--global ArchLinterNet.Cli --version 9.8.7" in violation for violation in violations)


def test_find_violations_rejects_tool_path_install_pin(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide="`dotnet tool install --tool-path .tools ArchLinterNet.Cli --version 9.8.7`\n",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("--tool-path .tools ArchLinterNet.Cli --version 9.8.7" in violation for violation in violations)


def test_find_violations_rejects_hardcoded_archlinternet_library_package_version(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Test integration\n\n"
            "`dotnet add package ArchLinterNet.Testing --version 9.8.7`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("ArchLinterNet.Testing --version 9.8.7" in violation for violation in violations)


def test_find_violations_rejects_noun_first_library_package_version(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Test integration\n\n"
            "`dotnet package add ArchLinterNet.Testing --version 9.8.7`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("dotnet package add ArchLinterNet.Testing --version 9.8.7" in violation for violation in violations)


def test_find_violations_rejects_noun_first_short_version_option(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Test integration\n\n"
            "`dotnet package add ArchLinterNet.Testing -v 9.8.7`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("dotnet package add ArchLinterNet.Testing -v 9.8.7" in violation for violation in violations)


def test_find_violations_rejects_hardcoded_archlinternet_package_reference(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Test integration\n\n"
            "`<PackageReference Include=\"ArchLinterNet.Testing\" Version=\"9.8.7\" />`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("PackageReference" in violation and "9.8.7" in violation for violation in violations)


def test_find_violations_rejects_nested_archlinternet_package_reference(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "<PackageReference Include=\"ArchLinterNet.Testing\">\n"
            "  <Version>9.8.7</Version>\n"
            "</PackageReference>\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("PackageReference" in violation and "9.8.7" in violation for violation in violations)


def test_nested_package_guard_does_not_cross_into_neighbor(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "<PackageReference Include=\"ArchLinterNet.Testing\">\n"
            "</PackageReference>\n"
            "<PackageReference Include=\"Newtonsoft.Json\">\n"
            "  <Version>13.0.4</Version>\n"
            "</PackageReference>\n"
        ),
    )

    assert evergreen.find_violations(tmp_path) == []


def test_find_violations_rejects_central_package_version_pin(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Central package management\n\n"
            "`<PackageVersion Include=\"ArchLinterNet.Testing\" Version=\"9.8.7\" />`\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("PackageVersion" in violation and "9.8.7" in violation for violation in violations)


def test_find_violations_rejects_tool_manifest_json_pin(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "```json\n"
            "{\n"
            "  \"tools\": {\n"
            "    \"ArchLinterNet.Cli\": {\n"
            "      \"version\": \"9.8.7\",\n"
            "      \"commands\": [\"arch-linter-net\"]\n"
            "    }\n"
            "  }\n"
            "}\n"
            "```\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("ArchLinterNet.Cli" in violation and "9.8.7" in violation for violation in violations)


def test_root_readme_variants_are_part_of_public_guard(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "README.quickstart.md").write_text(
        "`dotnet tool install ArchLinterNet.Cli --version 9.8.7`\n",
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("README.quickstart.md" in violation for violation in violations)


def test_sample_markdown_is_part_of_public_guard(tmp_path: Path) -> None:
    write_repo(tmp_path)
    sample = tmp_path / "samples" / "Example" / "README.md"
    sample.parent.mkdir(parents=True)
    sample.write_text(
        "`dotnet tool install ArchLinterNet.Cli --version 9.8.7`\n",
        encoding="utf-8",
    )

    violations = evergreen.find_violations(tmp_path)

    assert any("samples/Example/README.md" in violation for violation in violations)


def test_release_process_may_use_explicit_product_version_examples(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "reference" / "release-process.md").write_text(
        "# Release Process\n\n"
        "Example candidate check: `dotnet tool install ArchLinterNet.Cli --version 9.8.7`.\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


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

    assert any("public navigation must use a version-neutral ArchLinterNet product identity" in violation for violation in violations)


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
