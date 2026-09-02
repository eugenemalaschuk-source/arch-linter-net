from __future__ import annotations

import base64
import hashlib
import json
import os
import subprocess
import tempfile
from collections.abc import Mapping
from pathlib import Path

import pytest


_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_WORKFLOWS = _REPOSITORY_ROOT / ".github" / "workflows"
_REPOSITORY = "eugenemalaschuk-source/arch-linter-net"
_PR_NUMBER = 759
_HEAD_SHA = "a" * 40
_RUN_ID = 123456
_RUN_ATTEMPT = 2

_NODE_HARNESS = r"""
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const script = Buffer.from(process.env.WORKFLOW_SCRIPT_B64, 'base64').toString('utf8');
const fixture = JSON.parse(Buffer.from(process.env.WORKFLOW_FIXTURE_B64, 'base64').toString('utf8'));
const outputs = {};
const calls = [];
const pullRequests = fixture.pullRequests
  ?? [fixture.pullRequest ?? { head: { sha: process.env.CURRENT_HEAD_SHA ?? '' } }];
let nextPullRequest = 0;
const core = {
  setOutput(name, value) {
    outputs[name] = String(value);
  },
};
const github = {
  paginate: async (method, parameters) => {
    calls.push({ type: 'paginate', method: method.name, parameters });
    if (method === github.rest.actions.listJobsForWorkflowRun) {
      return fixture.jobs ?? [];
    }
    if (method === github.rest.actions.listWorkflowRunArtifacts) {
      return fixture.artifacts ?? [];
    }
    if (method === github.rest.issues.listComments) {
      return fixture.comments ?? [];
    }
    throw new Error(`unexpected paginated method: ${method.name}`);
  },
  rest: {
    pulls: {
      get: async (parameters) => {
        calls.push({ type: 'pulls.get', parameters });
        const index = Math.min(nextPullRequest, pullRequests.length - 1);
        nextPullRequest += 1;
        return { data: pullRequests[index] };
      },
    },
    actions: {
      listJobsForWorkflowRun: async () => undefined,
      listWorkflowRunArtifacts: async () => undefined,
    },
    issues: {
      listComments: async () => undefined,
      updateComment: async (parameters) => {
        calls.push({ type: 'issues.updateComment', parameters });
        return { data: {} };
      },
      createComment: async (parameters) => {
        calls.push({ type: 'issues.createComment', parameters });
        return { data: { id: fixture.createdCommentId ?? 999 } };
      },
    },
  },
};
const context = fixture.context ?? {
  repo: { owner: 'eugenemalaschuk-source', repo: 'arch-linter-net' },
  payload: { workflow_run: fixture.workflowRun },
};

try {
  const execute = new Function(
    'require',
    'github',
    'core',
    'context',
    `return (async () => {\n${script}\n})()`,
  );
  await execute(require, github, core, context);
  process.stdout.write(JSON.stringify({ outputs, calls }));
} catch (error) {
  process.stderr.write(error.stack ?? String(error));
  process.exitCode = 1;
}
"""


def _read(name: str) -> str:
    return (_WORKFLOWS / name).read_text(encoding="utf-8")


def _job(workflow: str, name: str, next_name: str) -> str:
    return workflow.split(f"  {name}:\n", maxsplit=1)[1].split(f"  {next_name}:\n", maxsplit=1)[0]


def _script(step_name: str) -> str:
    workflow = _read("publish-architecture-pr-report.yml")
    step_start = workflow.index(f"      - name: {step_name}\n")
    script_start = workflow.index("          script: |\n", step_start) + len("          script: |\n")
    next_step = workflow.find("      - name: ", script_start)
    script = workflow[script_start : None if next_step == -1 else next_step]
    return "".join(
        line[12:] if line.startswith("            ") else line
        for line in script.splitlines(keepends=True)
    )


def _run_script(
    step_name: str,
    fixture: Mapping[str, object],
    *,
    environment: Mapping[str, str] | None = None,
    files: Mapping[str, bytes] | None = None,
) -> dict[str, object]:
    with tempfile.TemporaryDirectory() as temporary_directory:
        temporary_path = Path(temporary_directory)
        (temporary_path / "runner.mjs").write_text(_NODE_HARNESS, encoding="utf-8")
        for relative_path, contents in (files or {}).items():
            path = temporary_path / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)
        process_environment = os.environ | {
            "WORKFLOW_SCRIPT_B64": base64.b64encode(_script(step_name).encode()).decode(),
            "WORKFLOW_FIXTURE_B64": base64.b64encode(json.dumps(fixture).encode()).decode(),
        }
        process_environment.update(environment or {})
        completed = subprocess.run(
            ["node", "runner.mjs"],
            cwd=temporary_path,
            env=process_environment,
            capture_output=True,
            check=False,
            encoding="utf-8",
        )
        assert completed.returncode == 0, completed.stderr
        return json.loads(completed.stdout)


def _workflow_run(*, conclusion: str = "failure", head_sha: str = _HEAD_SHA) -> dict[str, object]:
    return {
        "repository": {"full_name": _REPOSITORY},
        "path": ".github/workflows/ci.yml",
        "event": "pull_request",
        "head_sha": head_sha,
        "id": _RUN_ID,
        "run_attempt": _RUN_ATTEMPT,
        "conclusion": conclusion,
        "pull_requests": [{"number": _PR_NUMBER}],
    }


def _resolve_fixture(
    *,
    conclusion: str = "failure",
    producer_conclusion: str = "success",
    head_sha: str = _HEAD_SHA,
    pull_request_head: str = _HEAD_SHA,
    fork: bool = False,
) -> dict[str, object]:
    return {
        "workflowRun": _workflow_run(conclusion=conclusion, head_sha=head_sha),
        "pullRequest": {
            "head": {
                "sha": pull_request_head,
                "repo": {"full_name": "hostile/fork" if fork else _REPOSITORY},
            }
        },
        "jobs": [
            {
                "name": "Architecture PR Report Producer",
                "conclusion": producer_conclusion,
            }
        ],
        "artifacts": [
            {
                "id": 42,
                "name": "architecture-pr-report-v1",
                "expired": False,
                "size_in_bytes": 1024,
            }
        ],
    }


def _manifest(report_bytes: bytes, **overrides: object) -> bytes:
    context = {
        "repository": _REPOSITORY,
        "pr_number": str(_PR_NUMBER),
        "head_sha": _HEAD_SHA,
        "run_id": str(_RUN_ID),
        "run_attempt": str(_RUN_ATTEMPT),
    }
    context.update(overrides.pop("context", {}))
    report_fields = {
        "path": "architecture-pr-report.md",
        "bytes": len(report_bytes),
        "sha256": hashlib.sha256(report_bytes).hexdigest(),
    }
    report_fields.update(overrides.pop("report", {}))
    document: dict[str, object] = {
        "schema": "architecture-pr-report-publication/v1",
        "kind": "architecture-pr-report",
        "marker": "arch-linter-net-pr-report:v1",
        "context": context,
        "report": report_fields,
    }
    document.update(overrides)
    return json.dumps(document, separators=(",", ":")).encode()


def _artifact_files(report_bytes: bytes, **manifest_overrides: object) -> dict[str, bytes]:
    return {
        "incoming-report/architecture-pr-report.md": report_bytes,
        "incoming-report/architecture-pr-report.manifest.json": _manifest(report_bytes, **manifest_overrides),
    }


def _validate_environment() -> dict[str, str]:
    return {
        "EXPECTED_REPOSITORY": _REPOSITORY,
        "EXPECTED_PR_NUMBER": str(_PR_NUMBER),
        "EXPECTED_HEAD_SHA": _HEAD_SHA,
        "EXPECTED_RUN_ID": str(_RUN_ID),
        "EXPECTED_RUN_ATTEMPT": str(_RUN_ATTEMPT),
    }


def _comment_environment(
    *,
    ready: bool,
    report: bytes = b"# Architecture PR report\n",
    reason: str = "ready",
) -> dict[str, str]:
    return {
        "PR_NUMBER": str(_PR_NUMBER),
        "CURRENT_HEAD_SHA": _HEAD_SHA,
        "PRODUCER_RUN_ID": str(_RUN_ID),
        "PRODUCER_RUN_ATTEMPT": str(_RUN_ATTEMPT),
        "RESOLUTION_REASON": reason,
        "VALIDATION_STATUS": "ready" if ready else "",
        "VALIDATION_REASON": "" if ready else reason,
        "VALIDATED_REPORT_BASE64": base64.b64encode(report).decode() if ready else "",
    }


def _comment(comment_id: int, marker: str, body: str = "old report") -> dict[str, object]:
    return {
        "id": comment_id,
        "user": {"login": "github-actions[bot]"},
        "body": f"{marker}\n{body}",
    }


def test_ci_producer_uses_per_tree_baseline_and_separate_strict_gate() -> None:
    workflow = _read("ci.yml")
    producer = _job(workflow, "architecture_pr_report_producer", "architecture_pr_report_gate")
    gate = _job(workflow, "architecture_pr_report_gate", "tooling_support_tests")

    assert "name: Architecture PR Report Producer" in producer
    assert producer.count("if [[ -f architecture/baseline.arch.yml ]]; then") == 2
    assert "snapshot \"$output_directory/base-architecture-change-snapshot.json\"" in producer
    assert "snapshot \"$output_directory/current-architecture-change-snapshot.json\"" in producer
    assert "arch-linter-net-empty-baseline-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}.arch.yml" in producer
    assert 'document.get("schema_id") != "architecture-health/v1"' in producer
    assert "strict_coverage_outcome: ${{ steps.architecture_coverage.outcome }}" in producer
    assert "pull-requests: write" not in workflow
    assert "Fail if strict architecture coverage failed" not in producer
    assert "needs: architecture_pr_report_producer" in gate
    assert "outputs.strict_coverage_outcome == 'failure'" in gate


def test_resolve_accepts_a_successful_producer_when_overall_ci_failed() -> None:
    result = _run_script(
        "Resolve current PR and bound report artifact",
        _resolve_fixture(conclusion="failure"),
    )

    assert result["outputs"]["reason"] == "ready"
    assert result["outputs"]["artifact-id"] == "42"


@pytest.mark.parametrize("producer_conclusion", ["failure", "cancelled"])
def test_resolve_rejects_failed_or_cancelled_producer(producer_conclusion: str) -> None:
    result = _run_script(
        "Resolve current PR and bound report artifact",
        _resolve_fixture(conclusion="success", producer_conclusion=producer_conclusion),
    )

    assert result["outputs"]["reason"] == "producer_not_success"
    assert "artifact-id" not in result["outputs"]


def test_resolve_rejects_ambiguous_producer_jobs() -> None:
    fixture = _resolve_fixture(conclusion="success")
    fixture["jobs"].append(
        {"name": "Architecture PR Report Producer", "conclusion": "success"},
    )
    result = _run_script("Resolve current PR and bound report artifact", fixture)

    assert result["outputs"]["reason"] == "producer_ambiguous"
    assert "artifact-id" not in result["outputs"]


def test_resolve_marks_a_partial_rerun_without_a_producer_as_missing() -> None:
    fixture = _resolve_fixture(conclusion="failure")
    fixture["jobs"] = []
    result = _run_script("Resolve current PR and bound report artifact", fixture)

    assert result["outputs"]["reason"] == "producer_missing"
    assert result["outputs"]["producer-run-attempt"] == str(_RUN_ATTEMPT)
    assert "artifact-id" not in result["outputs"]


def test_resolve_rejects_stale_head_before_artifact_lookup() -> None:
    result = _run_script(
        "Resolve current PR and bound report artifact",
        _resolve_fixture(head_sha="b" * 40),
    )

    assert result["outputs"]["reason"] == "stale_head"
    assert all(call["method"] != "listWorkflowRunArtifacts" for call in result["calls"] if call["type"] == "paginate")


@pytest.mark.parametrize(
    ("report", "overrides", "expected_reason"),
    [
        (b"# Architecture PR report\n", {"schema": "wrong"}, "manifest_binding_invalid"),
        (b"# Architecture PR report\n", {"context": {"pr_number": "760"}}, "manifest_binding_invalid"),
        (b"# Architecture PR report\n", {"context": {"head_sha": "b" * 40}}, "manifest_binding_invalid"),
        (b"# Architecture PR report\n", {"context": {"run_id": "123457"}}, "manifest_binding_invalid"),
        (b"# Architecture PR report\n", {"report": {"sha256": "0" * 64}}, "report_integrity_invalid"),
        (b"# Architecture PR report\n\xff", {}, "report_encoding_invalid"),
        (b"# Architecture PR report\n" + b"x" * 60000, {}, "artifact_size_or_type_invalid"),
    ],
    ids=(
        "bad-schema",
        "bad-pr-binding",
        "bad-head-binding",
        "bad-run-binding",
        "bad-hash",
        "malformed-utf8",
        "oversized-report",
    ),
)
def test_validate_rejects_bad_binding_schema_hash_and_oversized_payload(
    report: bytes,
    overrides: dict[str, object],
    expected_reason: str,
) -> None:
    result = _run_script(
        "Validate inert report bytes and manifest",
        {},
        environment=_validate_environment(),
        files=_artifact_files(report, **overrides),
    )

    assert result["outputs"] == {"status": "rejected", "reason": expected_reason}


def test_validate_accepts_inert_fork_report_bytes_without_checkout() -> None:
    report = b"# Architecture PR report\n\n<script>not executed</script> $(not-executed)\n"
    resolution = _run_script(
        "Resolve current PR and bound report artifact",
        _resolve_fixture(fork=True),
    )
    validation = _run_script(
        "Validate inert report bytes and manifest",
        {},
        environment=_validate_environment(),
        files=_artifact_files(report),
    )
    publication = _run_script(
        "Publish or replace one architecture report comment",
        {"comments": []},
        environment=_comment_environment(ready=True, report=report),
        files={"incoming-report/architecture-pr-report.md": report},
    )

    assert resolution["outputs"]["reason"] == "ready"
    assert validation["outputs"]["status"] == "ready"
    assert validation["outputs"]["reason"] == "ready"
    assert base64.b64decode(validation["outputs"]["report-base64"]) == report
    assert publication["outputs"] == {"status": "published", "reason": "ready"}
    writes = [call for call in publication["calls"] if call["type"].startswith("issues.")]
    assert [write["type"] for write in writes] == ["issues.createComment"]
    assert writes[0]["parameters"]["body"].endswith(report.decode())
    assert "actions/checkout@" not in _read("publish-architecture-pr-report.yml")


@pytest.mark.parametrize(
    ("comments", "expected_call"),
    [
        ([], "issues.createComment"),
        ([_comment(1, "<!-- arch-linter-net-pr-report:v1 -->")], "issues.updateComment"),
        ([_comment(2, "<!-- architecture-coverage-report -->")], "issues.updateComment"),
    ],
)
def test_comment_script_creates_updates_and_migrates_one_sticky_comment(
    comments: list[dict[str, object]],
    expected_call: str,
) -> None:
    report = b"# Architecture PR report\n\nCanonical report\n"
    result = _run_script(
        "Publish or replace one architecture report comment",
        {"comments": comments},
        environment=_comment_environment(ready=True, report=report),
        files={"incoming-report/architecture-pr-report.md": report},
    )

    writes = [call for call in result["calls"] if call["type"].startswith("issues.") and call["type"] != "paginate"]
    assert result["outputs"] == {"status": "published", "reason": "ready"}
    assert [call["type"] for call in writes] == [expected_call]
    assert writes[0]["parameters"]["body"].endswith(report.decode())


def test_comment_script_never_overwrites_a_current_report_with_stale_evidence() -> None:
    current = _comment(
        3,
        "<!-- arch-linter-net-pr-report:v1 -->",
        f"<!-- arch-linter-net-pr-report-context:head={_HEAD_SHA};run=1;attempt=1 -->",
    )
    result = _run_script(
        "Publish or replace one architecture report comment",
        {"comments": [current]},
        environment=_comment_environment(ready=False, reason="stale_head"),
    )

    assert result["outputs"] == {"status": "rejected", "reason": "stale_head"}
    assert not [call for call in result["calls"] if call["type"].startswith("issues.") and call["type"] != "paginate"]


def test_comment_script_preserves_verified_same_head_report_when_partial_rerun_has_no_producer() -> None:
    current = _comment(
        4,
        "<!-- arch-linter-net-pr-report:v1 -->",
        f"<!-- arch-linter-net-pr-report-context:head={_HEAD_SHA};run=1;attempt=1 -->",
    )
    result = _run_script(
        "Publish or replace one architecture report comment",
        {"comments": [current]},
        environment=_comment_environment(ready=False, reason="producer_missing"),
    )

    assert result["outputs"] == {"status": "preserved", "reason": "producer_missing"}
    assert not [call for call in result["calls"] if call["type"].startswith("issues.") and call["type"] != "paginate"]


def test_comment_script_rejects_stale_head_immediately_before_comment_mutation() -> None:
    report = b"# Architecture PR report\n\nCanonical report\n"
    result = _run_script(
        "Publish or replace one architecture report comment",
        {"comments": [], "pullRequests": [{"head": {"sha": "b" * 40}}]},
        environment=_comment_environment(ready=True, report=report),
    )

    assert result["outputs"] == {"status": "rejected", "reason": "stale_head"}
    assert not [call for call in result["calls"] if call["type"].startswith("issues.") and call["type"] != "paginate"]


def test_comment_script_replaces_just_written_report_when_head_changes_after_write() -> None:
    newer_head = "b" * 40
    report = b"# Architecture PR report\n\nCanonical report\n"
    result = _run_script(
        "Publish or replace one architecture report comment",
        {
            "comments": [],
            "createdCommentId": 17,
            "pullRequests": [{"head": {"sha": _HEAD_SHA}}, {"head": {"sha": newer_head}}],
        },
        environment=_comment_environment(ready=True, report=report),
    )

    writes = [call for call in result["calls"] if call["type"].startswith("issues.") and call["type"] != "paginate"]
    assert result["outputs"] == {"status": "rejected", "reason": "stale_head"}
    assert [call["type"] for call in writes] == ["issues.createComment", "issues.updateComment"]
    assert writes[1]["parameters"]["comment_id"] == 17
    assert f"head={newer_head}" in writes[1]["parameters"]["body"]
    assert "# Architecture PR report unavailable" in writes[1]["parameters"]["body"]
