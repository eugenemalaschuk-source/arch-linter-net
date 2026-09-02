from __future__ import annotations

from pathlib import Path


_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_WORKFLOWS = _REPOSITORY_ROOT / ".github" / "workflows"


def _read(name: str) -> str:
    return (_WORKFLOWS / name).read_text(encoding="utf-8")


def _job(workflow: str, name: str, next_name: str) -> str:
    return workflow.split(f"  {name}:\n", maxsplit=1)[1].split(f"  {next_name}:\n", maxsplit=1)[0]


def test_read_only_ci_producer_uses_existing_cli_and_bound_report_artifact() -> None:
    workflow = _read("ci.yml")
    producer = _job(workflow, "architecture_pr_report", "tooling_support_tests")

    assert "name: Architecture PR Report" in producer
    assert "contents: read" in producer
    assert "pull-requests: write" not in workflow
    assert "actions/github-script@" not in producer
    assert "ref: ${{ github.event.pull_request.head.sha }}" in producer
    assert "git worktree add --detach" in producer
    assert "-- change snapshot" in producer
    assert "-- change report" in producer
    assert "-- health" in producer
    assert "-- report pr" in producer
    assert "--execution-context \"$EXECUTION_CONTEXT\"" in producer
    assert "architecture-pr-report-v1" in producer
    assert '"schema": "architecture-pr-report-publication/v1"' in producer
    assert '"kind": "architecture-pr-report"' in producer
    assert '"marker": "arch-linter-net-pr-report:v1"' in producer
    assert '"path": "architecture-pr-report.md"' in producer
    for manifest_field in ("repository", "pr_number", "head_sha", "run_id", "run_attempt", "sha256"):
        assert f'"{manifest_field}"' in producer


def test_ci_retires_legacy_comment_writer_but_retains_coverage_artifacts() -> None:
    workflow = _read("ci.yml")
    producer = _job(workflow, "architecture_pr_report", "tooling_support_tests")

    assert "architecture-coverage-report -->" not in workflow
    assert "Comment architecture coverage on pull request" not in workflow
    assert "architecture-strict" in producer
    assert "architecture-audit" in producer
    assert "architecture-coverage-report" in producer
    assert "architecture-health" in producer
    assert "architecture-change" in producer
    assert "Fail if strict architecture coverage failed" in producer
    assert "Fail if architecture PR report inputs are unavailable" in producer


def test_publisher_is_completed_ci_no_checkout_single_writer_workflow() -> None:
    workflow = _read("publish-architecture-pr-report.yml")
    publisher = workflow.split("  publish:\n", maxsplit=1)[1]

    assert "workflow_run:" in workflow
    assert "workflows: [CI]" in workflow
    assert "types: [completed]" in workflow
    assert "workflow_run.event == 'pull_request'" in publisher
    assert "pull_request_target" not in workflow
    assert "actions/checkout@" not in workflow
    assert publisher.count("pull-requests: write") == 1
    assert "actions: read" in publisher
    assert "contents: read" in publisher
    assert "architecture-pr-report-${{ github.event.workflow_run.pull_requests[0].number" in workflow


def test_publisher_rejects_untrusted_transport_before_reading_or_posting_report() -> None:
    workflow = _read("publish-architecture-pr-report.yml")

    assert "workflowRun.path !== '.github/workflows/ci.yml'" in workflow
    assert "associatedPullRequests.length !== 1" in workflow
    assert "pullRequest.head.sha !== workflowRun.head_sha" in workflow
    assert "workflowRun.conclusion !== 'success'" in workflow
    assert "github.rest.actions.listWorkflowRunArtifacts" in workflow
    assert "artifact.name === 'architecture-pr-report-v1'" in workflow
    assert "artifact.expired" in workflow
    assert "artifact.size_in_bytes > 524288" in workflow
    assert "actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c" in workflow
    assert "artifact-ids:" in workflow
    assert "run-id:" in workflow
    assert "fs.readdirSync(root).sort()" in workflow
    assert "architecture-pr-report.manifest.json" in workflow
    assert "manifestStat.isSymbolicLink()" in workflow
    assert "reportStat.isSymbolicLink()" in workflow
    assert "reportStat.size > 60000" in workflow
    assert "JSON.parse" in workflow
    assert "architecture-pr-report-publication/v1" in workflow
    assert "arch-linter-net-pr-report:v1" in workflow
    assert "crypto.createHash('sha256')" in workflow
    assert "report.includes(Buffer.from('<!--'))" in workflow
    assert "source(" not in workflow
    assert "eval(" not in workflow


def test_publisher_updates_one_comment_or_replaces_legacy_without_report_semantics() -> None:
    workflow = _read("publish-architecture-pr-report.yml")

    assert "<!-- arch-linter-net-pr-report:v1 -->" in workflow
    assert "<!-- architecture-coverage-report -->" in workflow
    assert "comment.user?.login === 'github-actions[bot]'" in workflow
    assert "unified.length > 1 || legacy.length > 1" in workflow
    assert "reason === 'stale_head'" in workflow
    assert "github.rest.issues.updateComment" in workflow
    assert "github.rest.issues.createComment" in workflow
    assert "No substitute architecture verdict was created." in workflow
    assert "-- report pr" not in workflow
    for unrelated_status in ("check-runs", "SonarCloud", "Codecov", "codeql"):
        assert unrelated_status not in workflow
