from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_public_docs_contract as docs_contract  # noqa: E402


def write_repo(root: Path) -> None:
    (root / "src" / "ArchLinterNet.Core" / "Contracts" / "Validators").mkdir(parents=True)
    (root / "src" / "ArchLinterNet.Cli" / "Commands" / "Foo" / "EntryPoint").mkdir(parents=True)
    (root / "src" / "ArchLinterNet.Cli" / "Commands" / "Foo" / "Application").mkdir(parents=True)
    (root / "docs" / "policy-format").mkdir(parents=True)
    (root / "docs" / "ai").mkdir(parents=True)
    (root / "docs" / "cli").mkdir(parents=True)
    (root / "docs" / "contracts").mkdir(parents=True)
    (root / "docs" / "reference").mkdir(parents=True)
    (root / "schema").mkdir(parents=True)

    (root / "src" / "ArchLinterNet.Core" / "Contracts" / "Validators" / "CoverageValidator.cs").write_text(
        'private static readonly string[] _implementedCoverageScopes = { "namespace", "project" };\n',
        encoding="utf-8",
    )
    (root / "src" / "ArchLinterNet.Cli" / "Commands" / "Foo" / "EntryPoint" / "FooCommandModule.cs").write_text(
        'public string CommandName => "foo";\n',
        encoding="utf-8",
    )
    (root / "src" / "ArchLinterNet.Cli" / "Commands" / "Foo" / "Application" / "FooCommandDefinition.cs").write_text(
        'Command command = new("foo");\nCommand child = new("bar");\n',
        encoding="utf-8",
    )

    coverage_markers = (
        "<!-- coverage-scope: namespace -->\n"
        "<!-- coverage-scope: project -->\n"
    )
    for relative in docs_contract.COVERAGE_DOCS:
        (root / relative).write_text(coverage_markers, encoding="utf-8")

    (root / "docs" / "cli" / "index.md").write_text(
        "<!-- cli-command: validate -->\n"
        "<!-- cli-command: foo -->\n"
        "<!-- cli-command: foo bar -->\n",
        encoding="utf-8",
    )
    (root / "docs" / "contracts" / "index.md").write_text(
        "<!-- contract-family: dependency -->\n"
        "<!-- contract-family: coverage -->\n"
        "[Dependency](dependency.md)\n"
        "[Coverage](coverage.md)\n",
        encoding="utf-8",
    )
    (root / "docs" / "contracts" / "dependency.md").write_text("# Dependency\n", encoding="utf-8")
    (root / "docs" / "contracts" / "coverage.md").write_text("# Coverage\n", encoding="utf-8")
    (root / "docs" / "reference" / "yaml-schema.md").write_text(
        "# YAML\n<!-- layer-selector-only-supported -->\n",
        encoding="utf-8",
    )
    (root / "mkdocs.yml").write_text(
        "nav:\n"
        "  - Contracts:\n"
        "      - Dependency: contracts/dependency.md\n"
        "      - Coverage: contracts/coverage.md\n",
        encoding="utf-8",
    )
    (root / "archlinternet.capabilities.json").write_text(
        json.dumps(
            {
                "contractFamilies": [
                    {"kind": "dependency"},
                    {
                        "kind": "coverage",
                        "validates": "Coverage of namespace and project inventory.",
                    },
                ]
            }
        ),
        encoding="utf-8",
    )
    (root / "schema" / "dependencies.arch.schema.json").write_text(
        json.dumps(
            {
                "$defs": {
                    "layer": {
                        "type": "object",
                        "properties": {
                            "namespace": {"type": "string"},
                            "selector": {"type": "object"},
                        },
                    }
                }
            }
        ),
        encoding="utf-8",
    )


def test_accepts_consistent_public_docs_contract(tmp_path: Path) -> None:
    write_repo(tmp_path)

    assert docs_contract.find_violations(tmp_path) == []


def test_detects_runtime_coverage_scope_drift(tmp_path: Path) -> None:
    write_repo(tmp_path)
    validator = tmp_path / "src" / "ArchLinterNet.Core" / "Contracts" / "Validators" / "CoverageValidator.cs"
    validator.write_text(
        'private static readonly string[] _implementedCoverageScopes = { "namespace", "project", "assembly" };\n',
        encoding="utf-8",
    )

    violations = docs_contract.find_violations(tmp_path)

    assert any("coverage scope markers differ from runtime" in item for item in violations)
    assert any("coverage description omits runtime scopes assembly" in item for item in violations)


def test_detects_executable_cli_command_missing_from_reference(tmp_path: Path) -> None:
    write_repo(tmp_path)
    definition = tmp_path / "src" / "ArchLinterNet.Cli" / "Commands" / "Foo" / "Application" / "FooCommandDefinition.cs"
    definition.write_text(
        'Command command = new("foo");\nCommand child = new("bar");\nCommand added = new("baz");\n',
        encoding="utf-8",
    )

    violations = docs_contract.find_violations(tmp_path)

    assert any("missing=['foo baz']" in item for item in violations)


def test_detects_machine_contract_family_missing_from_index(tmp_path: Path) -> None:
    write_repo(tmp_path)
    capabilities = tmp_path / "archlinternet.capabilities.json"
    payload = json.loads(capabilities.read_text(encoding="utf-8"))
    payload["contractFamilies"].append({"kind": "new-family"})
    capabilities.write_text(json.dumps(payload), encoding="utf-8")

    violations = docs_contract.find_violations(tmp_path)

    assert any("new-family" in item and "contract-family markers" in item for item in violations)


def test_rejects_selector_only_schema_regression(tmp_path: Path) -> None:
    write_repo(tmp_path)
    schema = tmp_path / "schema" / "dependencies.arch.schema.json"
    schema.write_text(
        json.dumps(
            {
                "$defs": {
                    "layer": {
                        "type": "object",
                        "required": ["namespace"],
                        "properties": {
                            "namespace": {"type": "string"},
                            "selector": {"type": "object"},
                        },
                    }
                }
            }
        ),
        encoding="utf-8",
    )

    violations = docs_contract.find_violations(tmp_path)

    assert any("selector-only layers are no longer accepted" in item for item in violations)


def test_detects_indexed_contract_page_missing_from_navigation(tmp_path: Path) -> None:
    write_repo(tmp_path)
    (tmp_path / "docs" / "contracts" / "extra.md").write_text("# Extra\n", encoding="utf-8")
    index = tmp_path / "docs" / "contracts" / "index.md"
    index.write_text(index.read_text(encoding="utf-8") + "[Extra](extra.md)\n", encoding="utf-8")

    violations = docs_contract.find_violations(tmp_path)

    assert any("contracts/extra.md" in item and "missing from navigation" in item for item in violations)
