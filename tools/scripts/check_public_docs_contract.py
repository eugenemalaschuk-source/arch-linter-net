#!/usr/bin/env python3
"""Fail when public documentation drifts from executable ArchLinterNet capabilities."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

COVERAGE_DOCS = (
    "docs/policy-format/index.md",
    "docs/policy-format/supported-capabilities.md",
    "docs/ai/capabilities.md",
)
CLI_DOC = "docs/cli/index.md"
CONTRACT_INDEX = "docs/contracts/index.md"
YAML_REFERENCE = "docs/reference/yaml-schema.md"

MARKER_RE_TEMPLATE = r"<!--\s*{kind}:\s*(.*?)\s*-->"
COMMAND_DECLARATION = re.compile(
    r'(?:new\s+Command|Command\s+\w+\s*=\s*new)\s*\(\s*"([^"]+)"'
)
MODULE_COMMAND_NAME = re.compile(
    r'public\s+string\s+CommandName\s*=>\s*"([^"]+)"'
)


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def read_text(root: Path, relative: str) -> str:
    path = root / relative
    if not path.is_file():
        raise ValueError(f"{relative}: required public documentation/source file is missing")
    return path.read_text(encoding="utf-8")


def markers(text: str, kind: str) -> set[str]:
    pattern = re.compile(MARKER_RE_TEMPLATE.format(kind=re.escape(kind)))
    return {match.group(1).strip() for match in pattern.finditer(text)}


def capability_contract_families(root: Path) -> set[str]:
    payload = json.loads(read_text(root, "archlinternet.capabilities.json"))
    families = payload.get("contractFamilies")
    if not isinstance(families, list):
        raise ValueError("archlinternet.capabilities.json: contractFamilies must be a list")
    result = {item.get("kind") for item in families if isinstance(item, dict)}
    if None in result or not result:
        raise ValueError("archlinternet.capabilities.json: every contract family must have kind")
    return {str(item) for item in result}


def capability_coverage_text(root: Path) -> str:
    payload = json.loads(read_text(root, "archlinternet.capabilities.json"))
    for family in payload.get("contractFamilies", []):
        if isinstance(family, dict) and family.get("kind") == "coverage":
            validates = family.get("validates")
            if isinstance(validates, str):
                return validates
    raise ValueError("archlinternet.capabilities.json: coverage family is missing validates text")


def runtime_coverage_scopes(root: Path) -> set[str]:
    text = read_text(
        root,
        "src/ArchLinterNet.Core/Contracts/Validators/CoverageValidator.cs",
    )
    match = re.search(
        r"_implementedCoverageScopes\s*=\s*\{(?P<body>.*?)\};",
        text,
        flags=re.DOTALL,
    )
    if match is None:
        raise ValueError("CoverageValidator.cs: implemented coverage scope inventory was not found")
    scopes = set(re.findall(r'"([a-z_]+)"', match.group("body")))
    if not scopes:
        raise ValueError("CoverageValidator.cs: implemented coverage scope inventory is empty")
    return scopes


def cli_command_paths(root: Path) -> set[str]:
    commands_root = root / "src" / "ArchLinterNet.Cli" / "Commands"
    if not commands_root.is_dir():
        raise ValueError("CLI Commands directory is missing")

    result = {"validate"}
    for area in sorted(path for path in commands_root.iterdir() if path.is_dir()):
        entrypoint = area / "EntryPoint"
        if not entrypoint.is_dir():
            continue

        top_module = entrypoint / f"{area.name}CommandModule.cs"
        if not top_module.is_file():
            continue

        top_text = top_module.read_text(encoding="utf-8")
        top_match = MODULE_COMMAND_NAME.search(top_text)
        if top_match is None:
            # Validate owns the root command and therefore has no top-level subcommand name.
            if area.name == "Validate":
                continue
            raise ValueError(f"{top_module.relative_to(root)}: CommandName was not found")

        top = top_match.group(1)
        result.add(top)

        for path in area.rglob("*.cs"):
            text = path.read_text(encoding="utf-8")
            for child in MODULE_COMMAND_NAME.findall(text):
                if child != top:
                    result.add(f"{top} {child}")
            for child in COMMAND_DECLARATION.findall(text):
                if child != top and child != "arch-linter-net":
                    result.add(f"{top} {child}")

    return result


def assert_schema_allows_selector_only_layer(root: Path) -> None:
    schema = json.loads(read_text(root, "schema/dependencies.arch.schema.json"))
    candidates: list[dict[str, object]] = []

    def visit(node: object) -> None:
        if isinstance(node, dict):
            properties = node.get("properties")
            if (
                isinstance(properties, dict)
                and "namespace" in properties
                and "selector" in properties
            ):
                candidates.append(node)
            for value in node.values():
                visit(value)
        elif isinstance(node, list):
            for value in node:
                visit(value)

    visit(schema)
    if not candidates:
        raise ValueError("policy schema: no layer definition with namespace + selector was found")

    def permits_selector_only(node: dict[str, object]) -> bool:
        required = node.get("required", [])
        if isinstance(required, list) and "namespace" in required:
            return False
        for keyword in ("allOf", "oneOf", "anyOf"):
            branches = node.get(keyword)
            if isinstance(branches, list):
                for branch in branches:
                    if not isinstance(branch, dict):
                        continue
                    branch_required = branch.get("required", [])
                    if isinstance(branch_required, list) and "selector" in branch_required:
                        return True
        # With neither a direct namespace requirement nor a branch that forces it,
        # the selector property is independently legal.
        return True

    if not any(permits_selector_only(node) for node in candidates):
        raise ValueError("policy schema: selector-only layers are no longer accepted")


def nav_contract_pages(root: Path) -> set[str]:
    text = read_text(root, "mkdocs.yml")
    return set(re.findall(r"\b(contracts/[A-Za-z0-9_-]+\.md)\b", text))


def indexed_contract_pages(root: Path) -> set[str]:
    text = read_text(root, CONTRACT_INDEX)
    pages: set[str] = set()
    for target in re.findall(r"\]\(([^)]+\.md)\)", text):
        if "://" in target or target.startswith("/"):
            continue
        resolved = (root / "docs" / "contracts" / target).resolve()
        contracts_root = (root / "docs" / "contracts").resolve()
        try:
            relative = resolved.relative_to(contracts_root)
        except ValueError as exc:
            raise ValueError(
                f"{CONTRACT_INDEX}: contract reference escapes docs/contracts: {target}"
            ) from exc
        if not resolved.is_file():
            raise ValueError(f"{CONTRACT_INDEX}: linked contract page is missing: {target}")
        pages.add(f"contracts/{relative.as_posix()}")
    return pages


def stale_claims(root: Path) -> list[str]:
    patterns = (
        re.compile(r"only\s+`scope:\s*namespace`\s+and\s+`scope:\s*rule_input`\s+are\s+implemented", re.I),
        re.compile(r"`scope:\s*dependency_edge`[^.\n]{0,100}(?:unsupported|reserved|not implemented|fail)", re.I),
        re.compile(r"`scope:\s*project`[^.\n]{0,100}(?:unsupported|reserved|not implemented|fail)", re.I),
        re.compile(r"`scope:\s*assembly`[^.\n]{0,100}(?:unsupported|reserved|not implemented|fail)", re.I),
        re.compile(r"layers\.<name>\.selector[^.\n]{0,100}(?:reserved|not implemented|unsupported)", re.I),
        re.compile(r"until\s+(?:the\s+)?(?:package|packages)\s+(?:is|are)\s+published", re.I),
    )
    violations: list[str] = []
    docs_root = root / "docs"
    for path in docs_root.rglob("*.md"):
        if "internal" in path.relative_to(docs_root).parts:
            continue
        text = path.read_text(encoding="utf-8")
        for pattern in patterns:
            match = pattern.search(text)
            if match:
                violations.append(
                    f"{path.relative_to(root).as_posix()}: stale capability claim '{match.group(0)}'"
                )
    return violations


def find_violations(root: Path) -> list[str]:
    violations: list[str] = []

    try:
        scopes = runtime_coverage_scopes(root)
        machine_coverage = capability_coverage_text(root).replace("-", "_")
        missing_machine = sorted(scope for scope in scopes if scope not in machine_coverage)
        if missing_machine:
            violations.append(
                "archlinternet.capabilities.json: coverage description omits runtime scopes "
                + ", ".join(missing_machine)
            )

        for relative in COVERAGE_DOCS:
            found = markers(read_text(root, relative), "coverage-scope")
            if found != scopes:
                violations.append(
                    f"{relative}: coverage scope markers differ from runtime; "
                    f"expected={sorted(scopes)} actual={sorted(found)}"
                )

        expected_families = capability_contract_families(root)
        documented_families = markers(read_text(root, CONTRACT_INDEX), "contract-family")
        if documented_families != expected_families:
            violations.append(
                f"{CONTRACT_INDEX}: contract-family markers differ from machine inventory; "
                f"missing={sorted(expected_families - documented_families)} "
                f"extra={sorted(documented_families - expected_families)}"
            )

        expected_cli = cli_command_paths(root)
        documented_cli = markers(read_text(root, CLI_DOC), "cli-command")
        if documented_cli != expected_cli:
            violations.append(
                f"{CLI_DOC}: CLI command markers differ from executable command tree; "
                f"missing={sorted(expected_cli - documented_cli)} "
                f"extra={sorted(documented_cli - expected_cli)}"
            )

        assert_schema_allows_selector_only_layer(root)
        if "<!-- layer-selector-only-supported -->" not in read_text(root, YAML_REFERENCE):
            violations.append(
                f"{YAML_REFERENCE}: selector-only layer support marker is missing"
            )

        contract_pages = indexed_contract_pages(root)
        nav_pages = nav_contract_pages(root)
        missing_nav = sorted(contract_pages - nav_pages)
        if missing_nav:
            violations.append(
                "mkdocs.yml: public contract pages missing from navigation: "
                + ", ".join(missing_nav)
            )
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        violations.append(str(exc))

    violations.extend(stale_claims(root))
    return violations


def main() -> int:
    violations = find_violations(repository_root())
    if not violations:
        print("public documentation semantic contract: OK")
        return 0

    print("Public documentation semantic contract failed:", file=sys.stderr)
    for violation in violations:
        print(f"- {violation}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
