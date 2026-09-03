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
_WORKFLOW = _REPOSITORY_ROOT / ".github" / "workflows" / "publish-architecture-health-badge.yml"
_REPOSITORY = "eugenemalaschuk-source/arch-linter-net"
_MAIN_SHA = "b" * 40
_BASE_SHA = "c" * 40
_HEAD_SHA = "a" * 40
_TREE_SHA = "d" * 40
_RUN_ID = 123456
_RUN_ATTEMPT = 2
_UNAVAILABLE_PAYLOAD_PATH = "architecture/architecture-health-badge-unavailable.json"


_NODE_HARNESS = r"""
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const script = Buffer.from(process.env.WORKFLOW_SCRIPT_B64, 'base64').toString('utf8');
const fixture = JSON.parse(Buffer.from(process.env.WORKFLOW_FIXTURE_B64, 'base64').toString('utf8'));
const outputs = {};
const calls = [];
const missing = () => Object.assign(new Error('not found'), { status: 404 });
const failWhen = (name) => {
  if (fixture.failAt === name) throw new Error(`simulated failure at ${name}`);
};
const core = {
  setOutput(name, value) { outputs[name] = String(value); },
  warning() {},
  notice() {},
};
const github = {
  request: async (route, parameters) => {
    calls.push({ type: 'request', route, parameters });
    if (route === 'GET /repos/{owner}/{repo}/rules/branches/{branch}') {
      return { data: fixture.effectiveRules ?? [] };
    }
    throw new Error(`unexpected request: ${route}`);
  },
  paginate: async (method, parameters) => {
    calls.push({ type: 'paginate', method: method.name, parameters });
    if (method === github.rest.repos.listPullRequestsAssociatedWithCommit) return fixture.associated ?? [];
    if (method === github.rest.actions.listWorkflowRunsForRepo) return fixture.runs ?? [];
    if (method === github.rest.actions.listJobsForWorkflowRun) return fixture.jobs ?? [];
    if (method === github.rest.actions.listWorkflowRunArtifacts) return fixture.artifacts ?? [];
    throw new Error(`unexpected paginated method: ${method.name}`);
  },
  rest: {
    repos: {
      listPullRequestsAssociatedWithCommit: async function listPullRequestsAssociatedWithCommit() {},
      getCommit: async (parameters) => {
        calls.push({ type: 'repos.getCommit', parameters });
        if (parameters.ref === process.env.MAIN_SHA) {
          return { data: fixture.mainCommit ?? { commit: { tree: { sha: 'd'.repeat(40) } } } };
        }
        if (parameters.ref === fixture.pullRequest?.head?.sha) return { data: fixture.headCommit };
        if (parameters.ref === fixture.publicationRefSha) return { data: fixture.publicationCommit };
        throw missing();
      },
      getContent: async (parameters) => {
        calls.push({ type: 'repos.getContent', parameters });
        if (fixture.contents?.[parameters.path]) return { data: fixture.contents[parameters.path] };
        throw missing();
      },
    },
    pulls: {
      get: async (parameters) => {
        calls.push({ type: 'pulls.get', parameters });
        return { data: fixture.pullRequest };
      },
    },
    checks: {
      listForRef: async (parameters) => {
        calls.push({ type: 'checks.listForRef', parameters });
        return { data: { check_runs: fixture.checkRuns ?? [] } };
      },
    },
    actions: {
      listWorkflowRunsForRepo: async function listWorkflowRunsForRepo() {},
      listJobsForWorkflowRun: async function listJobsForWorkflowRun() {},
      listWorkflowRunArtifacts: async function listWorkflowRunArtifacts() {},
    },
      git: {
        getRef: async (parameters) => {
          calls.push({ type: 'git.getRef', parameters });
          if (parameters.ref === 'heads/main') {
            return { data: { object: { sha: fixture.currentMainSha ?? process.env.MAIN_SHA } } };
          }
          if (parameters.ref === 'heads/architecture-health-badge' && fixture.branchExists) {
            return { data: { object: { sha: fixture.publicationRefSha ?? process.env.MAIN_SHA } } };
          }
          throw missing();
        },
        createRef: async (parameters) => {
          calls.push({ type: 'git.createRef', parameters });
          failWhen('git.createRef');
          return { data: {} };
        },
        updateRef: async (parameters) => {
          calls.push({ type: 'git.updateRef', parameters });
          failWhen('git.updateRef');
          return { data: {} };
        },
        createBlob: async (parameters) => {
          calls.push({ type: 'git.createBlob', parameters });
          failWhen('git.createBlob');
          return { data: { sha: `${calls.length}`.padStart(40, '0') } };
        },
        createTree: async (parameters) => {
          calls.push({ type: 'git.createTree', parameters });
          failWhen('git.createTree');
          return { data: { sha: '1'.repeat(40) } };
        },
        createCommit: async (parameters) => {
          calls.push({ type: 'git.createCommit', parameters });
          failWhen('git.createCommit');
          return { data: { sha: '2'.repeat(40) } };
        },
      },
  },
};
const context = { repo: { owner: 'eugenemalaschuk-source', repo: 'arch-linter-net' } };

try {
  const execute = new Function('require', 'github', 'core', 'context', `return (async () => {\n${script}\n})()`);
  await execute(require, github, core, context);
  process.stdout.write(JSON.stringify({ outputs, calls }));
} catch (error) {
  if (fixture.expectError) {
    process.stdout.write(JSON.stringify({ outputs, calls, error: error.message }));
  } else {
    process.stderr.write(error.stack ?? String(error));
    process.exitCode = 1;
  }
}
"""


def _workflow() -> str:
    return _WORKFLOW.read_text(encoding="utf-8")


def _script(step_name: str) -> str:
    workflow = _workflow()
    step_start = workflow.index(f"      - name: {step_name}\n")
    script_start = workflow.index("          script: |\n", step_start) + len("          script: |\n")
    next_step = workflow.find("      - name: ", script_start)
    return "".join(
        line[12:] if line.startswith("            ") else line
        for line in workflow[script_start : None if next_step == -1 else next_step].splitlines(keepends=True)
    )


def _run_script(
    step_name: str,
    fixture: Mapping[str, object],
    *,
    environment: Mapping[str, str] | None = None,
    files: Mapping[str, bytes] | None = None,
) -> dict[str, object]:
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        (root / "runner.mjs").write_text(_NODE_HARNESS, encoding="utf-8")
        for relative_path, contents in (files or {}).items():
            path = root / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)
        process_environment = os.environ | {
            "WORKFLOW_SCRIPT_B64": base64.b64encode(_script(step_name).encode()).decode(),
            "WORKFLOW_FIXTURE_B64": base64.b64encode(json.dumps(fixture).encode()).decode(),
            "MAIN_SHA": _MAIN_SHA,
        }
        process_environment.update(environment or {})
        completed = subprocess.run(
            ["node", "runner.mjs"],
            cwd=root,
            env=process_environment,
            capture_output=True,
            check=False,
            encoding="utf-8",
        )
        assert completed.returncode == 0, completed.stderr
        return json.loads(completed.stdout)


def _fixture(*, head_tree: str = _TREE_SHA, artifacts: list[dict[str, object]] | None = None) -> dict[str, object]:
    return {
        "mainCommit": {"commit": {"tree": {"sha": _TREE_SHA}}, "parents": [{"sha": _BASE_SHA}]},
        "headCommit": {"commit": {"tree": {"sha": head_tree}}},
        "associated": [{"number": 759}],
        "pullRequest": {
            "number": 759,
            "base": {"ref": "main", "repo": {"full_name": _REPOSITORY}},
            "head": {"sha": _HEAD_SHA},
            "merged": True,
            "merge_commit_sha": _MAIN_SHA,
        },
        "rulesets": [{"id": 1, "enforcement": "active"}],
        "effectiveRules": [
            {
                "type": "required_status_checks",
                "parameters": {
                    "strict_required_status_checks_policy": True,
                    "required_status_checks": [{"context": "Architecture Coverage"}],
                },
            }
        ],
        "checkRuns": [
            {
                "name": "Architecture Coverage",
                "status": "completed",
                "conclusion": "success",
                "app": {"slug": "github-actions"},
                "details_url": f"https://github.com/{_REPOSITORY}/actions/runs/{_RUN_ID}/job/1",
            }
        ],
        "runs": [
            {
                "id": _RUN_ID,
                "run_attempt": _RUN_ATTEMPT,
                "path": ".github/workflows/ci.yml",
                "event": "pull_request",
                "head_sha": _HEAD_SHA,
                "conclusion": "success",
            }
        ],
        "jobs": [{"name": "Architecture Coverage", "conclusion": "success"}],
        "artifacts": artifacts
        if artifacts is not None
        else [{"id": 42, "name": "architecture-health-badge-v1", "expired": False, "size_in_bytes": 1024}],
    }


def _payload() -> bytes:
    return b'{"schemaVersion":1,"label":"architecture","message":"DEBT \\u00b7 7 ignores \\u00b7 42 rules","color":"yellow"}'


def _unavailable_content() -> dict[str, str]:
    payload = (_REPOSITORY_ROOT / _UNAVAILABLE_PAYLOAD_PATH).read_bytes()
    return {
        "type": "file",
        "encoding": "base64",
        "content": base64.b64encode(payload).decode(),
    }


def _manifest(badge_payload: bytes, **overrides: object) -> bytes:
    context = {
        "repository": _REPOSITORY,
        "pr_number": "759",
        "base_ref": "main",
        "base_sha": _BASE_SHA,
        "head_sha": _HEAD_SHA,
        "head_tree_sha": _TREE_SHA,
        "run_id": str(_RUN_ID),
        "run_attempt": str(_RUN_ATTEMPT),
    }
    context.update(overrides.pop("context", {}))
    document: dict[str, object] = {
        "schema": "architecture-health-badge-promotion/v1",
        "kind": "architecture-health-badge",
        "context": context,
        "payload": {
            "path": "architecture-health-badge.json",
            "bytes": len(badge_payload),
            "sha256": hashlib.sha256(badge_payload).hexdigest(),
        },
    }
    document.update(overrides)
    return json.dumps(document, separators=(",", ":")).encode()


def _validation_environment() -> dict[str, str]:
    return {
        "EXPECTED_BASE_SHA": _BASE_SHA,
        "EXPECTED_HEAD_SHA": _HEAD_SHA,
        "EXPECTED_HEAD_TREE_SHA": _TREE_SHA,
        "EXPECTED_PR_NUMBER": "759",
        "EXPECTED_REPOSITORY": _REPOSITORY,
        "EXPECTED_RUN_ATTEMPT": str(_RUN_ATTEMPT),
        "EXPECTED_RUN_ID": str(_RUN_ID),
    }


def _artifact_files(badge_payload: bytes, **overrides: object) -> dict[str, bytes]:
    return {
        "incoming-health-badge/architecture-health-badge.json": badge_payload,
        "incoming-health-badge/architecture-health-badge.manifest.json": _manifest(badge_payload, **overrides),
    }


def test_resolve_accepts_required_successful_pr_evidence_with_matching_squash_tree() -> None:
    result = _run_script("Resolve required PR evidence for the merged tree", _fixture())

    assert result["outputs"] == {
        "reason": "ready",
        "artifact_id": "42",
        "base_sha": _BASE_SHA,
        "head_sha": _HEAD_SHA,
        "head_tree_sha": _TREE_SHA,
        "main_tree_sha": _TREE_SHA,
        "pr_number": "759",
        "producer_run_attempt": str(_RUN_ATTEMPT),
        "producer_run_id": str(_RUN_ID),
    }
    effective_rule_request = next(call for call in result["calls"] if call["type"] == "request")
    assert effective_rule_request == {
        "type": "request",
        "route": "GET /repos/{owner}/{repo}/rules/branches/{branch}",
        "parameters": {"owner": "eugenemalaschuk-source", "repo": "arch-linter-net", "branch": "main"},
    }


def test_resolve_rejects_matching_metadata_with_a_different_merged_tree() -> None:
    result = _run_script("Resolve required PR evidence for the merged tree", _fixture(head_tree="e" * 40))

    assert result["outputs"] == {"reason": "merged_tree_mismatch"}


@pytest.mark.parametrize(
    ("fixture", "reason"),
    [
        (_fixture(artifacts=[]), "badge_artifact_missing"),
        (
            _fixture(artifacts=[{"id": 42, "name": "architecture-health-badge-v1", "expired": True, "size_in_bytes": 1}]),
            "badge_artifact_invalid",
        ),
    ],
)
def test_resolve_fails_closed_when_promotion_artifact_is_unavailable(
    fixture: dict[str, object],
    reason: str,
) -> None:
    result = _run_script("Resolve required PR evidence for the merged tree", fixture)

    assert result["outputs"] == {"reason": reason}


@pytest.mark.parametrize(
    ("payload", "overrides", "reason"),
    [
        (b"not-json", {}, "badge_payload_parse_failed"),
        (_payload(), {"context": {"head_tree_sha": "e" * 40}}, "badge_manifest_binding_invalid"),
        (_payload(), {"payload": {"sha256": "0" * 64}}, "badge_manifest_binding_invalid"),
    ],
)
def test_validate_rejects_bad_badge_artifact_bindings(
    payload: bytes,
    overrides: dict[str, object],
    reason: str,
) -> None:
    result = _run_script(
        "Validate inert badge payload and manifest",
        {},
        environment=_validation_environment(),
        files=_artifact_files(payload, **overrides),
    )

    assert result["outputs"] == {"status": "rejected", "reason": reason}


def test_validate_accepts_exact_cli_payload_without_interpreting_health_semantics() -> None:
    result = _run_script(
        "Validate inert badge payload and manifest",
        {},
        environment=_validation_environment(),
        files=_artifact_files(_payload()),
    )

    assert result["outputs"]["status"] == "ready"
    assert result["outputs"]["reason"] == "ready"
    assert result["outputs"]["payload_path"].endswith("architecture-health-badge.json")


def test_static_publisher_creates_one_atomic_commit_for_the_fixed_paths() -> None:
    result = _run_script(
        "Publish fixed badge endpoint and metadata",
        {"branchExists": False},
        environment={
            "MAIN_SHA": _MAIN_SHA,
            "MAIN_TREE_SHA": _TREE_SHA,
            "PAYLOAD_PATH": "payload.json",
            "PR_NUMBER": "759",
            "PRODUCER_RUN_ID": str(_RUN_ID),
            "PUBLICATION_REASON": "ready",
            "PUBLICATION_STATUS": "ready",
        },
        files={"payload.json": _payload()},
    )

    tree = next(call["parameters"] for call in result["calls"] if call["type"] == "git.createTree")
    assert [entry["path"] for entry in tree["tree"]] == [
        "architecture-health.json",
        "architecture-health-publication.json",
    ]
    assert len([call for call in result["calls"] if call["type"] == "git.createCommit"]) == 1
    assert len([call for call in result["calls"] if call["type"] == "git.createRef"]) == 1
    assert not [call for call in result["calls"] if call["type"] == "repos.createOrUpdateFileContents"]
    assert result["outputs"] == {"status": "ready"}


def test_static_publisher_falls_back_to_reviewed_receipt_without_a_cli_payload() -> None:
    result = _run_script(
        "Publish fixed badge endpoint and metadata",
        {"branchExists": False, "contents": {_UNAVAILABLE_PAYLOAD_PATH: _unavailable_content()}},
        environment={
            "MAIN_SHA": _MAIN_SHA,
            "MAIN_TREE_SHA": _TREE_SHA,
            "PUBLICATION_REASON": "badge_artifact_missing",
            "PUBLICATION_STATUS": "unassessable",
            "UNAVAILABLE_PAYLOAD_PATH": _UNAVAILABLE_PAYLOAD_PATH,
        },
    )

    blobs = [call["parameters"] for call in result["calls"] if call["type"] == "git.createBlob"]
    reviewed_payload = (_REPOSITORY_ROOT / _UNAVAILABLE_PAYLOAD_PATH).read_bytes()
    assert any(call["content"] == base64.b64encode(reviewed_payload).decode() for call in blobs)
    assert result["outputs"] == {"status": "unassessable"}
    assert any(call["type"] == "git.createRef" for call in result["calls"])


def test_static_publisher_stale_main_event_makes_no_publication_write() -> None:
    result = _run_script(
        "Publish fixed badge endpoint and metadata",
        {
            "branchExists": False,
            "currentMainSha": "e" * 40,
            "contents": {_UNAVAILABLE_PAYLOAD_PATH: _unavailable_content()},
        },
        environment={
            "MAIN_SHA": _MAIN_SHA,
            "PUBLICATION_STATUS": "unassessable",
            "PUBLICATION_REASON": "badge_artifact_missing",
            "UNAVAILABLE_PAYLOAD_PATH": _UNAVAILABLE_PAYLOAD_PATH,
        },
    )

    writes = {"git.createBlob", "git.createTree", "git.createCommit", "git.createRef", "git.updateRef"}
    assert result["outputs"] == {"status": "stale_main"}
    assert not [call for call in result["calls"] if call["type"] in writes]


def test_static_publisher_never_moves_the_ref_when_atomic_tree_creation_fails() -> None:
    result = _run_script(
        "Publish fixed badge endpoint and metadata",
        {
            "branchExists": False,
            "contents": {_UNAVAILABLE_PAYLOAD_PATH: _unavailable_content()},
            "failAt": "git.createTree",
            "expectError": True,
        },
        environment={
            "MAIN_SHA": _MAIN_SHA,
            "PUBLICATION_STATUS": "unassessable",
            "PUBLICATION_REASON": "badge_artifact_missing",
            "UNAVAILABLE_PAYLOAD_PATH": _UNAVAILABLE_PAYLOAD_PATH,
        },
    )

    assert result["error"] == "simulated failure at git.createTree"
    assert not [call for call in result["calls"] if call["type"] in {"git.createRef", "git.updateRef"}]


def test_resolve_rejects_unrelated_active_ruleset_when_main_has_no_effective_gate() -> None:
    fixture = _fixture()
    fixture["effectiveRules"] = []

    result = _run_script("Resolve required PR evidence for the merged tree", fixture)

    assert result["outputs"] == {"reason": "required_architecture_gate_missing"}


def test_ci_producer_generates_a_bound_cli_payload_without_badge_semantics_in_workflow() -> None:
    workflow = (_REPOSITORY_ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")

    assert "badge architecture-health" in workflow
    assert "architecture-health-badge-promotion/v1" in workflow
    assert "architecture-health-badge-v1" in workflow
    assert "architecture/architecture-health-badge-unavailable.json" in workflow
    assert "cmp --silent" in workflow
    assert '"head_tree_sha"' in workflow
    assert "Architecture Health badge manifest is unavailable" in workflow
    assert "pull-requests: write" not in workflow


def test_badge_workflow_has_a_serialized_static_only_publication_boundary() -> None:
    workflow = _workflow()

    assert "push:\n    branches: [main]" in workflow
    assert "group: architecture-health-badge-publication" in workflow
    assert "cancel-in-progress: false" in workflow
    assert "contents: write" in workflow
    assert "architecture-health-badge" in workflow
    assert "architecture-health.json" in workflow
    assert "architecture-health-publication.json" in workflow
    assert "GET /repos/{owner}/{repo}/rules/branches/{branch}" in workflow
    assert "getRef('heads/main')" in workflow
    assert "createTree" in workflow
    assert "createCommit" in workflow
    assert "updateRef" in workflow
    assert "Checkout trusted main for unavailable payload" not in workflow
    assert "Setup .NET for unavailable payload" not in workflow
    assert "dotnet" not in workflow.lower()
    assert "make acceptance" not in workflow
    assert "deploy-pages" not in workflow
    assert "mkdocs" not in workflow.lower()
