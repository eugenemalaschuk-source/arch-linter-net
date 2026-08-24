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


def test_accepts_machine_standard_framework_and_external_versions(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "# Interoperability\n\n"
            "Use `finding/v1`, SARIF 2.1.0, and target framework `net10.0`.\n"
            "SARIF 2.1.0 is the current release of the standard.\n"
            "The current Newtonsoft.Json package version is 13.0.4.\n"
            "ArchLinterNet supports SARIF release 2.1.0.\n"
            "ArchLinterNet uses Newtonsoft.Json package version 13.0.4.\n"
            "Use SARIF migration-to-2.1.0 terminology when discussing the standard.\n"
        ),
    )
    (tmp_path / "docs" / "reference" / "yaml-schema.md").write_text(
        "# YAML Schema Reference\n\n"
        "https://archlinternet.dev/schema/0.6.1/dependencies.arch.schema.json\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_accepts_versioned_standard_paths_headings_and_navigation(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        nav=(
            "site_name: ArchLinterNet\n"
            "nav:\n"
            "  - Reference:\n"
            "      - ArchLinterNet SARIF 2.1.0: reference/archlinternet-sarif-2.1.0.md\n"
            "      - Protocol v2.1.0: reference/v2.1.0.md\n"
            "      - SARIF Migration to 2.1.0: reference/sarif-migration-to-2.1.0.md\n"
            "      - Upgrade SARIF to 2.1.0: reference/sarif-upgrade-to-2.1.0.md\n"
            "exclude_docs: |\n"
            "  internal/\n"
        ),
    )
    reference = tmp_path / "docs" / "reference"
    (reference / "archlinternet-sarif-2.1.0.md").write_text(
        "# ArchLinterNet SARIF 2.1.0\n\nStandard-format reference.\n",
        encoding="utf-8",
    )
    (reference / "v2.1.0.md").write_text("# Protocol v2.1.0\n", encoding="utf-8")
    (reference / "sarif-migration-to-2.1.0.md").write_text(
        "# SARIF Migration to 2.1.0\n",
        encoding="utf-8",
    )
    (reference / "sarif-upgrade-to-2.1.0.md").write_text(
        "# Upgrade SARIF to 2.1.0\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_rejects_product_release_paths_including_prereleases(tmp_path: Path) -> None:
    write_repo(tmp_path)
    guides = tmp_path / "docs" / "guides"
    names = (
        "migration-to-0-5-1.md",
        "v9-8-7-upgrade.md",
        "archlinternet-9.8.7.md",
        "archlinternet-release-9.8.7.md",
        "9.8.7-archlinternet.md",
        "archlinternet-release-9.8.7-preview.1.md",
        "archlinternet-9.8.7-preview.1-guide.md",
        "v9.8.7-preview.1-upgrade.md",
    )
    for name in names:
        (guides / name).write_text("# Versioned product doc\n", encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)
    for name in names:
        assert any(name in violation for violation in violations)


def test_rejects_versioned_root_readme_identity(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "README-v9.8.7-preview.1.md").write_text(
        "# Versioned README\n", encoding="utf-8"
    )

    assert any(
        "README-v9.8.7-preview.1.md" in item for item in evergreen.find_violations(tmp_path)
    )


def test_rejects_product_release_prose_but_not_schema_ids(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "reference" / "yaml-schema.md").write_text(
        "# YAML Schema Reference\n\n"
        "https://archlinternet.dev/schema/0.6.1/dependencies.arch.schema.json\n\n"
        "ArchLinterNet release 9.8.7 is current.\n",
        encoding="utf-8",
    )

    assert any(
        "product package SemVer is coupled" in item for item in evergreen.find_violations(tmp_path)
    )


def test_rejects_product_status_wording_and_version_first_forms(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "0.6.4 is the public adoption package line for ArchLinterNet.\n"
            "9.8.7 is the current ArchLinterNet release.\n"
            "The current ArchLinterNet version is 9.8.7.\n"
            "ArchLinterNet 9.8.7 is the current version.\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)
    assert sum("product package SemVer is coupled" in item for item in violations) >= 4


def test_rejects_versioned_product_heading_and_navigation(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide="# Upgrade to 9.8.7\n",
        nav=(
            "site_name: ArchLinterNet\n"
            "nav:\n"
            "  - Guides:\n"
            "      - Upgrade to 9.8.7: guides/upgrading.md\n"
            "      - ArchLinterNet release 9.8.7: guides/upgrading.md\n"
            "exclude_docs: |\n"
            "  internal/\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)
    assert any("product package SemVer is coupled" in item for item in violations)
    assert any("Upgrade to 9.8.7" in item for item in violations)
    assert any("ArchLinterNet release 9.8.7" in item for item in violations)


def test_rejects_tool_package_pins_in_supported_argument_forms(tmp_path: Path) -> None:
    commands = (
        "dotnet tool install ArchLinterNet.Cli --version 9.8.7",
        "dotnet tool install --global ArchLinterNet.Cli --version 9.8.7",
        "dotnet tool install --tool-path .tools ArchLinterNet.Cli --version 9.8.7",
        "dotnet tool install ArchLinterNet.Cli --version=9.8.7",
        "dotnet tool update --global ArchLinterNet.Cli --version=9.8.7",
        'dotnet tool install ArchLinterNet.Cli --version "9.8.7"',
        "dotnet tool install ArchLinterNet.Cli --version '9.8.7-preview.1'",
        "dotnet tool install ArchLinterNet.Cli --version 9.8.7+build.1",
        "dotnet tool install ArchLinterNet.Cli \\\n  --version 9.8.7",
        "dotnet tool install ArchLinterNet.Cli `\n  --version 9.8.7",
    )
    for index, command in enumerate(commands):
        root = tmp_path / str(index)
        write_repo(root, guide=f"`{command}`\n")
        assert any(
            "pin ArchLinterNet package versions" in item for item in evergreen.find_violations(root)
        )


def test_allows_exact_tool_pin_only_for_the_historical_reference_workflow(tmp_path: Path) -> None:
    write_repo(tmp_path)
    reference = tmp_path / "docs" / "guides" / "real-repository-workflow.md"
    reference.write_text(
        "dotnet tool install --tool-path .tools ArchLinterNet.Cli --version 0.7.0\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_rejects_library_package_pins_in_supported_command_forms(tmp_path: Path) -> None:
    commands = (
        "dotnet add package ArchLinterNet.Testing --version 9.8.7",
        "dotnet package add ArchLinterNet.Testing --version 9.8.7",
        "dotnet package add ArchLinterNet.Testing -v 9.8.7",
        "dotnet package add ArchLinterNet.Testing --version=9.8.7",
        "dotnet package add ArchLinterNet.Testing -v=9.8.7",
        'dotnet package add ArchLinterNet.Testing --version "9.8.7"',
        "dotnet package add ArchLinterNet.Testing -v '9.8.7-preview.1'",
        "dotnet package add ArchLinterNet.Testing \\\n  --version 9.8.7",
    )
    for index, command in enumerate(commands):
        root = tmp_path / str(index)
        write_repo(root, guide=f"`{command}`\n")
        assert any(
            "pin ArchLinterNet package versions" in item for item in evergreen.find_violations(root)
        )


def test_rejects_msbuild_and_tool_manifest_package_pins(tmp_path: Path) -> None:
    snippets = (
        '<PackageReference Include="ArchLinterNet.Testing" Version="9.8.7" />',
        '<PackageVersion Include="ArchLinterNet.Testing" Version="9.8.7" />',
        '<PackageReference Include="ArchLinterNet.Testing" VersionOverride="9.8.7" />',
        '<PackageReference\n  Include="ArchLinterNet.Testing"\n  Version="9.8.7-preview.1" />',
        '<PackageVersion\n  Include="ArchLinterNet.Testing"\n  Version="9.8.7" />',
        '<PackageReference Include="ArchLinterNet.Testing">\n  <Version>9.8.7</Version>\n</PackageReference>',
        '<PackageVersion Include="ArchLinterNet.Testing">\n  <Version>9.8.7</Version>\n</PackageVersion>',
        '"ArchLinterNet.Cli": { "version": "9.8.7", "commands": ["arch-linter-net"] }',
    )
    for index, snippet in enumerate(snippets):
        root = tmp_path / str(index)
        write_repo(root, guide=snippet + "\n")
        assert any(
            "pin ArchLinterNet package versions" in item for item in evergreen.find_violations(root)
        )


def test_nested_package_guard_does_not_cross_into_neighbor(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            '<PackageReference Include="ArchLinterNet.Testing">\n'
            "</PackageReference>\n"
            '<PackageReference Include="Newtonsoft.Json">\n'
            "  <Version>13.0.4</Version>\n"
            "</PackageReference>\n"
        ),
    )

    assert evergreen.find_violations(tmp_path) == []


def test_scans_root_readme_variants_samples_and_packaged_readmes(tmp_path: Path) -> None:
    write_repo(tmp_path)
    pin = "`dotnet tool install ArchLinterNet.Cli --version 9.8.7`\n"
    surfaces = (
        tmp_path / "README.quickstart.md",
        tmp_path / "samples" / "Example" / "README.md",
        tmp_path / "src" / "ArchLinterNet.Example" / "README.md",
    )
    for path in surfaces:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(pin, encoding="utf-8")

    violations = evergreen.find_violations(tmp_path)
    for path in surfaces:
        relative = path.relative_to(tmp_path).as_posix()
        assert any(relative in item for item in violations)


def test_release_process_may_use_explicit_product_version_examples(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "reference" / "release-process.md").write_text(
        "# Release Process\n\n"
        "Example: `dotnet tool install ArchLinterNet.Cli --version 9.8.7`.\n"
        "ArchLinterNet release 9.8.7 is the candidate under review.\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_internal_docs_are_not_part_of_public_guard(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "internal" / "release-9-8-7.md").write_text(
        "ArchLinterNet release 9.8.7 historical evidence\n",
        encoding="utf-8",
    )

    assert evergreen.find_violations(tmp_path) == []


def test_versioned_doc_reference_is_rejected_but_plain_external_term_is_allowed(tmp_path: Path) -> None:
    write_repo(
        tmp_path,
        guide=(
            "Use SARIF migration-to-2.1.0 terminology for the standard.\n"
            "Do not link to `migration-to-9.8.7.md`.\n"
        ),
    )

    violations = evergreen.find_violations(tmp_path)
    assert any("migration-to-9.8.7" in item for item in violations)
    assert not any("SARIF migration-to-2.1.0" in item for item in violations)


def test_main_returns_failure_and_reports_actionable_violation(tmp_path: Path, monkeypatch, capsys) -> None:
    write_repo(tmp_path, guide="ArchLinterNet release 9.8.7 is current.\n")
    monkeypatch.setattr(evergreen, "repository_root", lambda: tmp_path)

    exit_code = evergreen.main()
    captured = capsys.readouterr()

    assert exit_code == 1
    assert "Evergreen docs guard failed" in captured.err
    assert "product package SemVer is coupled" in captured.err


def test_main_returns_success_for_clean_tree(tmp_path: Path, monkeypatch, capsys) -> None:
    write_repo(tmp_path)
    monkeypatch.setattr(evergreen, "repository_root", lambda: tmp_path)

    exit_code = evergreen.main()
    captured = capsys.readouterr()

    assert exit_code == 0
    assert "Evergreen docs guard: OK" in captured.out
