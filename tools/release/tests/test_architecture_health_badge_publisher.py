from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
WORKFLOWS = ROOT / ".github" / "workflows"
STAGE_B_ACTION_REF = (
    "eugenemalaschuk-source/arch-linter-net/.github/actions/architecture-health-badge-promotion"
    "@68c69f51ea4ea6a5ae2cca2a09479be99ae3501d"
)


def read_workflow(name: str) -> str:
    return (WORKFLOWS / name).read_text(encoding="utf-8")


def test_reference_publisher_delegates_to_one_reusable_workflow() -> None:
    workflow = read_workflow("publish-architecture-health-badge.yml")
    assert "uses: $/.github/workflows/architecture-health-badge-promotion.yml" in workflow
    assert (
        "permissions:\n"
        "      actions: read\n"
        "      checks: read\n"
        "      contents: write\n"
        "      id-token: write\n"
        "      packages: read\n"
        "      pull-requests: read"
    ) in workflow
    assert "configuration-id: reference-public-raw" in workflow
    assert "adapter: github-raw" in workflow
    assert "actions/github-script" not in workflow
    assert "actions/checkout" not in workflow


def test_source_lines_badge_uses_canonical_evidence_in_the_trusted_pipeline() -> None:
    workflow = read_workflow("architecture-health-badge-promotion.yml")
    caller = read_workflow("publish-architecture-health-badge.yml")
    ci = read_workflow("ci.yml")

    assert "Publish Source lines payload from canonical evidence" in workflow
    assert 'name == "architecture-health"' in workflow
    assert 'repository-metrics-badge.json' in workflow
    assert 'actions/runs/$PRODUCER_RUN_ID/artifacts' in workflow
    assert 'architecture-health-badge/architecture-health.json' not in workflow
    assert "repository_metrics:" not in caller
    assert not (WORKFLOWS / "publish-repository-metrics-badge.yml").exists()
    assert "badge repository-metrics" in ci
    assert "architecture-pr-report/repository-metrics-badge.json" in ci


def test_reusable_workflow_resolves_its_action_from_the_workflow_repository() -> None:
    workflow = read_workflow("architecture-health-badge-promotion.yml")
    # Stage B of #982 intentionally updates the live reusable workflow before
    # the post-merge inventory rotation can bind the final #981 squash SHA.
    assert f"uses: {STAGE_B_ACTION_REF}" in workflow
    assert "uses: ./.github/actions/architecture-health-badge-promotion" not in workflow


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
    assert "packages: read" in workflow
    assert "cf_api_token:" in workflow
    assert "bootstrap_writer" not in workflow
    assert "CF_API_TOKEN: ${{ secrets['cf_api_token'] }}" in workflow
    workflow_call = workflow.split("workflow_call:", 1)[1]
    assert workflow_call.index("inputs:") < workflow_call.index("secrets:")
    assert "secrets: inherit" not in workflow
    assert "actions/checkout" not in workflow
    assert "run-url" not in workflow
    assert "artifact-url" not in workflow


def test_bootstrap_emits_a_private_no_app_handoff_without_consumer_remote_writes() -> None:
    workflow = read_workflow("architecture-health-badge-promotion.yml")
    bootstrap = workflow.split("  promote:", 1)[0]
    assert "bootstrap:\n    if: inputs.operation == 'bootstrap'" in bootstrap
    assert "contents: read" in bootstrap
    assert "contents: write" not in bootstrap
    assert "actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a" in workflow
    assert "name: architecture-health-bootstrap-handoff" in workflow
    assert '"schema": "architecture-health-badge-bootstrap-handoff/v1"' in workflow
    assert "OWNER-APPLY-HANDOFF.md" in workflow
    assert '"tree_sha": os.environ["BOOTSTRAP_BASE_TREE_SHA"]' in workflow
    assert 'git -C "$root" rev-parse "HEAD^{tree}"' in workflow
    assert "Trusted bootstrap produced output outside its managed allowlist" in workflow
    for forbidden in (
        "actions/create-github-app-token",
        "bootstrap_writer",
        "BOOTSTRAP_WRITER_TOKEN",
        "permission-workflows: write",
        "git config user",
        "git add -A",
        "git commit",
        "git push",
        "GITHUB_API_URL/repos/$GITHUB_REPOSITORY/pulls",
    ):
        assert forbidden not in workflow
    assert "secrets: inherit" not in workflow


def test_reusable_workflow_uses_bracket_notation_for_hyphenated_inputs() -> None:
    workflow = read_workflow("architecture-health-badge-promotion.yml")
    assert not re.search(r"inputs\.\w+-[\w-]+", workflow)
    for input_name in (
        "configuration-id",
        "base-ref",
        "cli-version",
        "nuget-source",
        "provider-plan",
        "repository-id",
        "repository-owner-id",
        "disclosure-profile",
        "producer-workflow",
        "producer-job-name",
        "check-name",
        "check-app",
        "artifact-name",
        "evidence-artifact-name",
    ):
        assert f"inputs['{input_name}']" in workflow

    action = (ROOT / ".github" / "actions" / "architecture-health-badge-promotion" / "action.yml").read_text(
        encoding="utf-8"
    )
    assert not re.search(r"inputs\.\w+-[\w-]+", action)
    assert "inputs.configuration_id" in action
    assert "configuration-id:" not in action.split("runs:", 1)[0]


def test_action_runs_repository_owned_code_and_has_redacted_outputs() -> None:
    action = (ROOT / ".github" / "actions" / "architecture-health-badge-promotion" / "action.yml").read_text(encoding="utf-8")
    assert "using: composite" in action
    assert "tools.badge_promotion.cli" in action
    assert "configuration_id:" in action
    assert "--configuration-id" in action
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


def test_release_inventory_is_candidate_only_and_release_line_neutral() -> None:
    inventory = json.loads((ROOT / ".github" / "badge-promotion" / "release-inventory.json").read_text(encoding="utf-8"))
    assert inventory["schema"] == "architecture-health-badge-release-inventory/v3"
    assert inventory["support_status"] == "experimental-opt-in"
    assert inventory["publication_authority"] == "external-checkpoint-b-release-scope"
    assert inventory["review_origin"] == {
        "story": "#825",
        "distribution_task": "#835",
        "first_release_authority": "#806",
        "lifecycle": "milestone-6/v0.8.x-completeness",
    }
    assert "release_authority" not in inventory
    assert "included" not in inventory
    assert "excluded" not in inventory
    for component in inventory["components"]:
        assert len(component["approved_source_sha"]) == 40


def test_ci_keeps_the_exact_two_file_badge_artifact_contract() -> None:
    workflow = read_workflow("ci.yml")
    assert 'name: architecture-health-badge-v1' in workflow
    assert "architecture-health-badge.json" in workflow
    assert "architecture-health-badge.manifest.json" in workflow
    assert "head_tree_sha" in workflow
    assert "pull-requests: write" not in workflow
