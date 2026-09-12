from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
WORKFLOWS = ROOT / ".github" / "workflows"


def read_workflow(name: str) -> str:
    return (WORKFLOWS / name).read_text(encoding="utf-8")


def test_reference_publisher_delegates_to_one_reusable_workflow() -> None:
    workflow = read_workflow("publish-architecture-health-badge.yml")
    assert "uses: ./.github/workflows/architecture-health-badge-promotion.yml" in workflow
    assert "configuration-id: reference-public-raw" in workflow
    assert "adapter: github-raw" in workflow
    assert "actions/github-script" not in workflow
    assert "actions/checkout" not in workflow


def test_reusable_workflow_exposes_only_approved_inputs_and_minimal_trust_boundary() -> None:
    workflow = read_workflow("architecture-health-badge-promotion.yml")
    assert "workflow_call:" in workflow
    assert "configuration-id:" in workflow
    assert "adapter:" in workflow
    assert "operation:" in workflow
    assert "id-token: write" in workflow
    assert "actions: read" in workflow
    assert "checks: read" in workflow
    assert "pull-requests: read" in workflow
    assert "secrets: inherit" not in workflow
    assert "actions/checkout" not in workflow
    assert "run-url" not in workflow
    assert "artifact-url" not in workflow


def test_action_runs_repository_owned_code_and_has_redacted_outputs() -> None:
    action = (ROOT / ".github" / "actions" / "architecture-health-badge-promotion" / "action.yml").read_text(encoding="utf-8")
    assert "using: composite" in action
    assert "tools.badge_promotion.cli" in action
    assert "configuration-id" in action
    assert "head-sha" in action
    assert "reason" in action
    assert "GITHUB_TOKEN: ${{ github.token }}" in action
    assert "secrets: inherit" not in action


def test_registry_is_closed_and_public_raw_is_not_available_for_private_repositories() -> None:
    registry = json.loads((ROOT / ".github" / "badge-promotion" / "registry.json").read_text(encoding="utf-8"))
    assert registry["schema"] == "architecture-health-badge-promotion/registry/v1"
    assert set(registry["configurations"]) >= {"reference-public-raw", "reference-none"}
    for config in registry["configurations"].values():
        assert config["destination"]["adapter"] in {"github-raw", "relay", "none"}
        if config["destination"]["adapter"] == "github-raw":
            assert config["repository_visibility"] == "public"
        assert "url" not in config["destination"]
        assert "callback" not in config["destination"]


def test_release_inventory_is_candidate_only_and_excludes_unrelated_work() -> None:
    inventory = json.loads((ROOT / ".github" / "badge-promotion" / "release-inventory.json").read_text(encoding="utf-8"))
    assert inventory["release_authority"] == "#806"
    assert inventory["publication"] == "not-authorized"
    assert inventory["lifecycle"] == "milestone-6/v0.8.x-completeness"
    assert "#650" in inventory["excluded"]
    assert "#787" in inventory["excluded"]
    for component in inventory["components"]:
        assert len(component["approved_source_sha"]) == 40


def test_ci_keeps_the_exact_two_file_badge_artifact_contract() -> None:
    workflow = read_workflow("ci.yml")
    assert 'name: architecture-health-badge-v1' in workflow
    assert "architecture-health-badge.json" in workflow
    assert "architecture-health-badge.manifest.json" in workflow
    assert "head_tree_sha" in workflow
    assert "pull-requests: write" not in workflow
