from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_badge_delivery_inventory as composition  # noqa: E402


PIN = "a" * 40
WORKFLOW = "owner/repo/.github/workflows/publish.yml"
ACTION = f"owner/repo/.github/actions/publish@{PIN}"


def write_manifests(root: Path) -> tuple[Path, Path]:
    inventory_path = root / composition.INVENTORY_PATH
    bundle_path = root / composition.BUNDLE_PATH
    inventory_path.parent.mkdir(parents=True)
    bundle_path.parent.mkdir(parents=True)
    inventory_path.write_text(json.dumps({
        "schema": "architecture-health-badge-release-inventory/v2",
        "bundle_members": [
            {"archive_path": "relay/src/index.ts"},
            {"archive_path": "relay/src/lifecycle.ts"},
            {"archive_path": "schema/0.8.0/badge-relay-config.schema.json"},
        ],
        "compatibility": {
            "publisher_commit": PIN,
            "workflow_ref": f"{WORKFLOW}@{PIN}",
            "action_ref": ACTION,
            "bundle": "badge-relay/v1",
            "plan": "architecture-health-badge-relay/v1",
        },
    }), encoding="utf-8")
    bundle_path.write_text(json.dumps({
        "schema_id": "badge-relay-bundle-manifest/v1",
        "bundle": "badge-relay/v1",
        "compatibility_plan": "architecture-health-badge-relay/v1",
        "files": [
            {"path": "src/index.ts"},
            {"path": "src/lifecycle.ts"},
            {"path": "schema/0.8.0/badge-relay-config.schema.json"},
        ],
        "publisher_pins": {
            "commit": PIN,
            "workflow_sha": PIN,
            "action_sha": PIN,
            "workflow_ref": WORKFLOW,
            "action_ref": ACTION,
        },
    }), encoding="utf-8")
    return inventory_path, bundle_path


def test_complete_inventory_preserves_root_schema_mapping(tmp_path: Path) -> None:
    write_manifests(tmp_path)
    assert composition.find_violations(tmp_path) == []


def test_missing_runtime_module_blocks_composition(tmp_path: Path) -> None:
    inventory_path, _ = write_manifests(tmp_path)
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["bundle_members"].pop(1)
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
    assert composition.find_violations(tmp_path) == [
        "Release inventory omits setup bundle file: relay/src/lifecycle.ts"
    ]


@pytest.mark.parametrize("key", ["commit", "workflow_sha", "action_sha", "workflow_ref", "action_ref"])
def test_mismatched_approved_pin_is_not_compatible(tmp_path: Path, key: str) -> None:
    _, bundle_path = write_manifests(tmp_path)
    bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
    bundle["publisher_pins"][key] = "unapproved"
    bundle_path.write_text(json.dumps(bundle), encoding="utf-8")
    assert composition.find_violations(tmp_path)


@pytest.mark.parametrize("path", ["../secret", "/absolute", "src\\outside.ts", "src//index.ts"])
def test_unsafe_manifest_path_is_rejected(tmp_path: Path, path: str) -> None:
    _, bundle_path = write_manifests(tmp_path)
    bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
    bundle["files"][0]["path"] = path
    bundle_path.write_text(json.dumps(bundle), encoding="utf-8")
    assert "unsafe path" in composition.find_violations(tmp_path)[0]


def test_duplicate_archive_member_is_rejected(tmp_path: Path) -> None:
    inventory_path, _ = write_manifests(tmp_path)
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["bundle_members"].append(inventory["bundle_members"][0])
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
    assert "duplicate path" in composition.find_violations(tmp_path)[0]


def test_missing_manifest_cannot_pass(tmp_path: Path) -> None:
    assert composition.find_violations(tmp_path)


def test_invalid_manifest_cannot_pass(tmp_path: Path) -> None:
    inventory_path, _ = write_manifests(tmp_path)
    inventory_path.write_text("[]", encoding="utf-8")
    assert "expected an object" in composition.find_violations(tmp_path)[0]


def test_source_check_is_read_only_and_does_not_claim_live_acceptance(tmp_path: Path) -> None:
    paths = write_manifests(tmp_path)
    before = {path: path.read_bytes() for path in paths}
    result = subprocess.run(
        [sys.executable, str(Path(composition.__file__)), "--root", str(tmp_path)],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 0
    assert "packed/platform/live acceptance is still required" in result.stdout
    assert {path: path.read_bytes() for path in paths} == before


def test_cli_missing_runtime_module_returns_nonzero(tmp_path: Path) -> None:
    inventory_path, _ = write_manifests(tmp_path)
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["bundle_members"].pop(1)
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
    result = subprocess.run(
        [sys.executable, str(Path(composition.__file__)), "--root", str(tmp_path)],
        capture_output=True, text=True, check=False,
    )
    assert result.returncode == 1
    assert "BLOCKED" in result.stdout
    assert "relay/src/lifecycle.ts" in result.stdout


def test_public_compatibility_table_matches_source_inventory() -> None:
    root = Path(__file__).resolve().parents[3]
    inventory = json.loads((root / composition.INVENTORY_PATH).read_text(encoding="utf-8"))
    document = (root / "docs/reference/badge-distribution.md").read_text(encoding="utf-8")
    labels = {
        "bundle": "Relay bundle",
        "config": "Configuration",
        "plan": "Compatibility plan",
        "promotion": "Promotion",
        "publication": "Publication",
        "storage": "Storage migration",
    }
    for key, label in labels.items():
        assert f"| {label} | `{inventory['compatibility'][key]}` |" in document
    for package_id in inventory["package_ids"]:
        assert f"`{package_id}`" in document


def test_badge_guides_are_navigable_and_handoff_stays_private() -> None:
    import yaml

    root = Path(__file__).resolve().parents[3]
    configuration = yaml.safe_load((root / "mkdocs.yml").read_text(encoding="utf-8"))

    def targets(node: object) -> set[str]:
        if isinstance(node, str):
            return {node}
        values = node.values() if isinstance(node, dict) else node
        return set().union(*(targets(child) for child in values))

    routes = targets(configuration["nav"])
    for route in (
        "guides/badge-adoption.md", "guides/badge-setup.md",
        "guides/badge-lifecycle-operations.md", "reference/badge-distribution.md",
    ):
        assert route in routes
        assert (root / "docs" / route).is_file()
    assert "internal/" in configuration["exclude_docs"].splitlines()
    assert not any(route.startswith("internal/") for route in routes)
