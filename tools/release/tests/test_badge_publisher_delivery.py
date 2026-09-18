"""Keep installer, schema, bundle and release authority on one reviewed pin."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools/release"))
import release_distribution as distribution  # noqa: E402

MODELS = ROOT / "src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupModels.cs"
MANIFEST = ROOT / "relay/bundle-manifest.json"
SCHEMA = ROOT / "schema/0.8.0/badge-relay-config.schema.json"


def _constant(name: str) -> str:
    matches = re.findall(rf'\bconst string {name} = "([^"]+)";', MODELS.read_text())
    assert len(matches) == 1, name
    return matches[0]


def test_all_shipped_publisher_references_match_reviewed_inventory() -> None:
    inventory = json.loads((ROOT / ".github/badge-promotion/release-inventory.json").read_text())
    distribution._validate_inventory(inventory)
    compatibility = inventory["compatibility"]
    workflow_pin = compatibility["publisher_commit"]
    action_pin = compatibility["action_commit"]
    workflow = compatibility["publisher_repository"] + "/" + compatibility["workflow_path"]
    action = compatibility["action_ref"]
    bundle_pins = json.loads(MANIFEST.read_text())["publisher_pins"]
    assert bundle_pins == {
        "workflow_ref": workflow, "workflow_sha": workflow_pin,
        "action_ref": action, "action_sha": action_pin, "commit": workflow_pin,
    }
    assert _constant("DefaultPublisherWorkflowRef") == workflow
    assert _constant("DefaultPublisherWorkflowSha") == workflow_pin
    assert _constant("DefaultActionRef") == action
    assert _constant("DefaultPublisherActionSha") == action_pin
    schema_pins = json.loads(SCHEMA.read_text())["properties"]["pins"]["properties"]
    assert schema_pins["workflow_ref"]["enum"] == [None, workflow]
    assert schema_pins["workflow_sha"]["enum"] == [None, workflow_pin]
    assert schema_pins["action_ref"]["enum"] == [None, action]


def test_rotated_schema_and_bundle_have_exact_installer_digests() -> None:
    manifest_bytes = MANIFEST.read_bytes()
    assert hashlib.sha256(manifest_bytes).hexdigest() == _constant("ShippedRelayBundleManifestSha256")
    for record in json.loads(manifest_bytes)["files"]:
        path = record["path"]
        source = ROOT / path if path.startswith("schema/") else ROOT / "relay" / path
        assert hashlib.sha256(source.read_bytes()).hexdigest() == record["sha256"], path
