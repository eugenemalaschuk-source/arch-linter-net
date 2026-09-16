"""GitHub Actions adapter for the pure trusted-promotion contract.

The resolver is intentionally the only provider-facing code here. It converts
GitHub API facts into ``EvidenceContext``; the pure package then validates the
artifact and decides whether a transport may commit it.
"""

from __future__ import annotations

import argparse
import base64
from datetime import datetime, timedelta, timezone
import fnmatch
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from typing import Any

from .config import ConfigValidationError, parse_config
from .decision import PromotionRequest, decide_promotion
from .adapters import AdapterError, HttpRelayClient, NoneAdapter, issue_github_oidc_token
from .model import EvidenceContext, PromotionStatus, ReasonCode


class ProviderFailure(RuntimeError):
    def __init__(self, reason: str, *, status_code: int | None = None) -> None:
        self.reason = reason
        self.status_code = status_code
        super().__init__(reason)


_MAX_RAW_PUBLICATION_ATTEMPTS = 5
_DEFAULT_BRANCH = "~DEFAULT_BRANCH"


class _ArtifactRedirectHandler(urllib.request.HTTPRedirectHandler):
    """Follow GitHub's signed artifact redirect without forwarding the bearer token."""

    def redirect_request(
        self,
        request: urllib.request.Request,
        response: Any,
        code: int,
        message: str,
        headers: Any,
        newurl: str,
    ) -> urllib.request.Request | None:
        target = urllib.parse.urlparse(newurl)
        if target.scheme != "https":
            return None
        redirected = super().redirect_request(request, response, code, message, headers, newurl)
        if redirected is None:
            return None
        source_host = urllib.parse.urlparse(request.full_url).netloc
        if target.netloc != source_host:
            for header_map in (redirected.headers, redirected.unredirected_hdrs):
                for header_name in header_map.copy():
                    if header_name.lower() == "authorization":
                        del header_map[header_name]
        return redirected


class GitHubApi:
    def __init__(self) -> None:
        self.base = os.environ.get("GITHUB_API_URL", "https://api.github.com").rstrip("/")
        self.token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
        if not self.token:
            raise ProviderFailure("authorization_unavailable")

    def request(self, path: str, *, method: str = "GET", value: dict[str, Any] | None = None) -> Any:
        body = None if value is None else json.dumps(value, separators=(",", ":")).encode("utf-8")
        request = urllib.request.Request(
            self.base + path,
            data=body,
            method=method,
            headers={
                "accept": "application/vnd.github+json",
                "authorization": f"Bearer {self.token}",
                "content-type": "application/json",
                "x-github-api-version": "2022-11-28",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as error:
            if error.code in {403, 404}:
                raise ProviderFailure("required_capability_unavailable", status_code=error.code) from error
            raise ProviderFailure("github_api_unavailable") from error
        except (OSError, json.JSONDecodeError) as error:
            raise ProviderFailure("github_api_unavailable") from error

    def download(self, url: str) -> bytes:
        parsed = urllib.parse.urlparse(url)
        api_host = urllib.parse.urlparse(self.base).netloc
        if parsed.scheme != "https" or parsed.netloc not in {api_host, "api.github.com"}:
            raise ProviderFailure("artifact_url_unapproved")
        request = urllib.request.Request(url, headers={"accept": "application/vnd.github+json", "authorization": f"Bearer {self.token}"})
        try:
            opener = urllib.request.build_opener(_ArtifactRedirectHandler())
            with opener.open(request, timeout=30) as response:
                data = response.read(65_537)
        except OSError as error:
            raise ProviderFailure("artifact_download_failed") from error
        if len(data) > 65_536:
            raise ProviderFailure("artifact_archive_oversized")
        return data


def _repository_path(repository: str) -> str:
    return urllib.parse.quote(repository, safe="/")


def _sha(value: Any) -> str:
    if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{40}", value) is None:
        raise ProviderFailure("provenance_invalid")
    return value


def _parse_time(value: Any) -> datetime:
    if not isinstance(value, str):
        raise ProviderFailure("semantic_evidence_unavailable")
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)
    except ValueError as error:
        raise ProviderFailure("semantic_evidence_unavailable") from error


def _has_required_check(document: Any, check_name: str, check_app_id: int) -> bool:
    if not isinstance(document, dict) or document.get("type") != "required_status_checks":
        return False
    parameters = document.get("parameters")
    if not isinstance(parameters, dict) or parameters.get("strict_required_status_checks_policy") is not True:
        return False
    checks = parameters.get("required_status_checks")
    if not isinstance(checks, list):
        return False
    return any(
        isinstance(check, dict)
        and check.get("context") == check_name
        and not isinstance(check.get("integration_id"), bool)
        and check.get("integration_id") == check_app_id
        for check in checks
    )


def _branch_pattern_matches(pattern: Any, ref: str, base_ref: str, default_branch: Any) -> bool:
    if not isinstance(pattern, str):
        return False
    if pattern == _DEFAULT_BRANCH:
        return default_branch == base_ref
    return fnmatch.fnmatchcase(ref, pattern) or fnmatch.fnmatchcase(base_ref, pattern)


def _applies_to_base_ref(document: Any, api: GitHubApi, repository_path: str, base_ref: str) -> bool:
    if not isinstance(document, dict) or document.get("target") != "branch" or document.get("enforcement") != "active":
        return False
    conditions = document.get("conditions")
    ref_name = conditions.get("ref_name") if isinstance(conditions, dict) else None
    if not isinstance(ref_name, dict) or not isinstance(ref_name.get("include"), list) or not isinstance(ref_name.get("exclude"), list):
        return False
    ref = f"refs/heads/{base_ref}"
    default_branch = None
    if _DEFAULT_BRANCH in ref_name["include"] or _DEFAULT_BRANCH in ref_name["exclude"]:
        try:
            repository_info = api.request(f"/repos/{repository_path}")
        except ProviderFailure:
            return False
        default_branch = repository_info.get("default_branch") if isinstance(repository_info, dict) else None
    included = any(_branch_pattern_matches(pattern, ref, base_ref, default_branch) for pattern in ref_name["include"])
    excluded = any(_branch_pattern_matches(pattern, ref, base_ref, default_branch) for pattern in ref_name["exclude"])
    return included and not excluded


def _ruleset_detail_requires_check(
    api: GitHubApi,
    repository_path: str,
    ruleset_id: int,
    check_name: str,
    check_app_id: int,
    base_ref: str,
) -> bool:
    try:
        detail = api.request(f"/repos/{repository_path}/rulesets/{ruleset_id}")
    except ProviderFailure as detail_error:
        if detail_error.reason == "required_capability_unavailable":
            return False
        raise
    detail_rules = detail.get("rules") if isinstance(detail, dict) else None
    return (
        isinstance(detail, dict)
        and detail.get("id") == ruleset_id
        and _applies_to_base_ref(detail, api, repository_path, base_ref)
        and isinstance(detail_rules, list)
        and any(_has_required_check(rule, check_name, check_app_id) for rule in detail_rules)
    )


def _ruleset_requires_check(
    api: GitHubApi,
    repository_path: str,
    check_name: str,
    check_app_id: int,
    base_ref: str,
) -> bool:
    try:
        rules = api.request(f"/repos/{repository_path}/rulesets?includes_parents=true&includes_inherited=true&per_page=100")
    except ProviderFailure:
        return False
    if not isinstance(rules, list):
        return False
    for summary in rules:
        ruleset_id = summary.get("id") if isinstance(summary, dict) else None
        if not isinstance(ruleset_id, int) or ruleset_id <= 0:
            continue
        if _ruleset_detail_requires_check(api, repository_path, ruleset_id, check_name, check_app_id, base_ref):
            return True
    return False


def _required_gate(
    api: GitHubApi,
    repository: str,
    check_name: str,
    check_app_id: int,
    base_ref: str,
) -> bool:
    repository_path = _repository_path(repository)
    try:
        branch_path = urllib.parse.quote(base_ref, safe="")
        rules = api.request(f"/repos/{repository_path}/rules/branches/{branch_path}")
    except ProviderFailure as error:
        if error.reason != "required_capability_unavailable":
            raise
        return _ruleset_requires_check(api, repository_path, check_name, check_app_id, base_ref)
    if not isinstance(rules, list):
        return False
    return any(_has_required_check(rule, check_name, check_app_id) for rule in rules)


def _workflow_blob_sha(api: GitHubApi, repository: str, workflow_path: str, ref: str) -> str:
    path = urllib.parse.quote(workflow_path, safe="/")
    reference = urllib.parse.quote(ref, safe="")
    workflow = api.request(f"/repos/{_repository_path(repository)}/contents/{path}?ref={reference}")
    if not isinstance(workflow, dict) or workflow.get("type") != "file":
        raise ProviderFailure("workflow_mismatch")
    return _sha(workflow.get("sha"))


def _read_bounded_zip_member(opened: zipfile.ZipFile, name: str, max_bytes: int) -> bytes:
    try:
        info = opened.getinfo(name)
    except KeyError as error:
        raise ProviderFailure("semantic_evidence_unavailable") from error
    if info.file_size > max_bytes or info.compress_size > max_bytes:
        raise ProviderFailure("semantic_evidence_oversized")
    try:
        with opened.open(info) as member:
            data = member.read(max_bytes + 1)
    except (OSError, RuntimeError, ValueError, zipfile.BadZipFile) as error:
        raise ProviderFailure("semantic_evidence_unavailable") from error
    if len(data) > max_bytes:
        raise ProviderFailure("semantic_evidence_oversized")
    return data


def _merged_pull_request(api: GitHubApi, repository: str, main_sha: str, base_ref: str) -> tuple[str, str, str, str, int]:
    repository_path = _repository_path(repository)
    commit = api.request(f"/repos/{repository_path}/commits/{main_sha}")
    main_tree = _sha(commit.get("commit", {}).get("tree", {}).get("sha"))
    parents = commit.get("parents", [])
    if len(parents) != 1:
        raise ProviderFailure("main_commit_shape_invalid")
    base_sha = _sha(parents[0].get("sha"))
    associated = api.request(f"/repos/{repository_path}/commits/{main_sha}/pulls")
    if not isinstance(associated, list) or len(associated) != 1 or not isinstance(associated[0].get("number"), int):
        raise ProviderFailure("merged_pull_request_ambiguous")
    pr_number = associated[0]["number"]
    pull = api.request(f"/repos/{repository_path}/pulls/{pr_number}")
    base = pull.get("base", {})
    if base.get("repo", {}).get("full_name") != repository or base.get("ref") != base_ref or pull.get("merged") is not True or pull.get("merge_commit_sha") != main_sha:
        raise ProviderFailure("merged_pull_request_invalid")
    head_sha = _sha(pull.get("head", {}).get("sha"))
    head_commit = api.request(f"/repos/{repository_path}/commits/{head_sha}")
    head_tree = _sha(head_commit.get("commit", {}).get("tree", {}).get("sha"))
    if head_tree != main_tree:
        raise ProviderFailure("merged_tree_mismatch")
    return main_tree, base_sha, head_sha, head_tree, pr_number


def _successful_check(api: GitHubApi, repository: str, head_sha: str, config) -> tuple[int, dict[str, Any]]:
    check_name = urllib.parse.quote(config.producer.check_name, safe="")
    checks = api.request(f"/repos/{_repository_path(repository)}/commits/{head_sha}/check-runs?check_name={check_name}&filter=latest&per_page=100")
    successful = [item for item in checks.get("check_runs", []) if item.get("name") == config.producer.check_name and item.get("status") == "completed" and item.get("conclusion") == "success" and item.get("app", {}).get("slug") == config.producer.check_app]
    if len(successful) != 1:
        raise ProviderFailure("required_gate_not_successful")
    check_app_id = successful[0].get("app", {}).get("id")
    if isinstance(check_app_id, bool) or not isinstance(check_app_id, int) or check_app_id <= 0:
        raise ProviderFailure("check_app_unresolved")
    return check_app_id, successful[0]


def _producer_run(api: GitHubApi, repository: str, head_sha: str, config, check: dict[str, Any]) -> tuple[dict[str, Any], int, dict[str, Any]]:
    details_url = check.get("details_url", "")
    run_job_match = re.search(r"/actions/runs/(\d+)/job/(\d+)(?:[/?#]|$)", details_url)
    if run_job_match is None:
        raise ProviderFailure("producer_run_unresolved")
    run_id = int(run_job_match.group(1))
    job_id = int(run_job_match.group(2))
    repository_path = _repository_path(repository)
    runs = api.request(f"/repos/{repository_path}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100")
    matching = [run for run in runs.get("workflow_runs", []) if run.get("id") == run_id and run.get("path") == config.producer.workflow_path and run.get("event") == config.producer.event and run.get("head_sha") == head_sha and run.get("conclusion") == "success"]
    if len(matching) != 1:
        raise ProviderFailure("producer_run_unresolved")
    run = matching[0]

    jobs_document = api.request(f"/repos/{repository_path}/actions/runs/{run_id}/jobs?per_page=100")
    jobs = jobs_document.get("jobs", []) if isinstance(jobs_document, dict) else []
    exact_jobs = [job for job in jobs if isinstance(job, dict) and job.get("id") == job_id]
    if len(exact_jobs) == 1:
        job = exact_jobs[0]
    else:
        job = api.request(f"/repos/{repository_path}/actions/jobs/{job_id}")

    run_attempt = job.get("run_attempt") if isinstance(job, dict) else None
    job_run_id = job.get("run_id") if isinstance(job, dict) else None
    job_head_sha = job.get("head_sha") if isinstance(job, dict) else None
    if (
        not isinstance(job, dict)
        or job.get("id") != job_id
        or (job_run_id is not None and job_run_id != run_id)
        or (job_head_sha is not None and job_head_sha != head_sha)
        or job.get("name") != config.producer.job_name
        or job.get("conclusion") != "success"
        or isinstance(run_attempt, bool)
        or not isinstance(run_attempt, int)
        or run_attempt <= 0
    ):
        raise ProviderFailure("producer_job_unresolved")
    return run, run_id, job


def _selected_artifact(api: GitHubApi, repository: str, run_id: int, artifact_name: str) -> tuple[list[Any], dict[str, Any]]:
    artifacts = api.request(f"/repos/{_repository_path(repository)}/actions/runs/{run_id}/artifacts?per_page=100").get("artifacts", [])
    selected = [artifact for artifact in artifacts if artifact.get("name") == artifact_name]
    if len(selected) != 1 or selected[0].get("expired") is True:
        raise ProviderFailure("artifact_missing_or_expired")
    return artifacts, selected[0]


def _read_semantic_horizon(api: GitHubApi, artifacts: list[Any], config) -> datetime:
    evidence_artifacts = [item for item in artifacts if item.get("name") == config.producer.evidence_artifact_name and item.get("expired") is not True]
    if len(evidence_artifacts) != 1:
        raise ProviderFailure("semantic_evidence_unavailable")
    evidence_archive = api.download(str(evidence_artifacts[0].get("archive_download_url", "")))
    try:
        with zipfile.ZipFile(io.BytesIO(evidence_archive)) as opened:
            health = json.loads(_read_bounded_zip_member(opened, "architecture-health.json", config.limits.max_member_bytes).decode("utf-8"))
        return _parse_time(health["report_evidence"]["publication_evidence"]["semantic_horizon"])
    except (KeyError, TypeError, ValueError, zipfile.BadZipFile) as error:
        raise ProviderFailure("semantic_evidence_unavailable") from error


def resolve_evidence(api: GitHubApi, config) -> tuple[EvidenceContext, bytes]:
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    base_ref = config.base_ref
    main_sha = _sha(os.environ.get("GITHUB_SHA"))
    if repository != config.repository:
        raise ProviderFailure("repository_mismatch")
    main_tree, base_sha, head_sha, head_tree, pr_number = _merged_pull_request(api, repository, main_sha, base_ref)
    workflow_sha = _workflow_blob_sha(api, repository, config.producer.workflow_path, head_sha)
    if workflow_sha != config.producer.workflow_sha:
        raise ProviderFailure("workflow_mismatch")
    check_app_id, check = _successful_check(api, repository, head_sha, config)
    run, run_id, job = _producer_run(api, repository, head_sha, config, check)
    # The artifact manifest records the attempt of this producer job. A rerun of
    # an unrelated job advances the workflow run's attempt without recreating
    # this artifact, so binding to run.run_attempt would reject valid evidence.
    producer_run_attempt = int(job.get("run_attempt", 0))
    artifacts, artifact = _selected_artifact(api, repository, run_id, config.producer.artifact_name)
    archive = api.download(str(artifact.get("archive_download_url", "")))
    verified_at = _parse_time(run.get("created_at"))
    semantic_horizon = _read_semantic_horizon(api, artifacts, config)
    evidence = EvidenceContext(
        repository=repository, base_ref=base_ref, base_sha=base_sha, main_tree_sha=main_tree,
        head_sha=head_sha, head_tree_sha=head_tree, pr_number=pr_number, event=config.producer.event,
        merged=True, workflow_path=config.producer.workflow_path, workflow_sha=workflow_sha,
        check_name=config.producer.check_name, check_app=config.producer.check_app, check_status="completed",
        check_conclusion="success", required_gate_present=_required_gate(api, repository, config.producer.check_name, check_app_id, config.base_ref), run_id=run_id,
        run_attempt=producer_run_attempt, job_id=int(job.get("id", 0)), job_name=job.get("name", ""),
        artifact_id=int(artifact.get("id", 0)), artifact_name=artifact.get("name", ""), artifact_size=len(archive),
        artifact_expired=False, verified_at=verified_at, semantic_horizon=semantic_horizon,
    )
    return evidence, archive


def _load_config(configuration_id: str):
    path = Path(__file__).resolve().parents[2] / ".github" / "badge-promotion" / "registry.json"
    try:
        registry = json.loads(path.read_text(encoding="utf-8"))
        raw = registry["configurations"][configuration_id]
        return parse_config(raw)
    except (OSError, KeyError, TypeError, json.JSONDecodeError, ConfigValidationError) as error:
        raise ProviderFailure("approved_configuration_invalid") from error


def _write_outputs(values: dict[str, object]) -> None:
    target = os.environ.get("GITHUB_OUTPUT")
    if not target:
        return
    with open(target, "a", encoding="utf-8") as output:
        for key, value in values.items():
            output.write(f"{key}={value}\n")


def _current_raw_ref(api: GitHubApi, ref_path: str) -> Any:
    try:
        return api.request(ref_path)
    except ProviderFailure as error:
        if error.reason != "required_capability_unavailable":
            raise
        return None


def _publication_receipt(config, payload: bytes, evidence: EvidenceContext | None, status: str, reason: str) -> dict[str, Any]:
    return {
        "schema": "architecture-health-badge-publication/v2", "status": status, "reason": reason,
        "repository": config.repository,
        "base_sha": evidence.base_sha if evidence else None, "head_sha": evidence.head_sha if evidence else None,
        "head_tree_sha": evidence.head_tree_sha if evidence else None, "main_sha": os.environ.get("GITHUB_SHA"),
        "main_tree_sha": evidence.main_tree_sha if evidence else None,
        "pr_number": str(evidence.pr_number) if evidence else None,
        "producer_run_id": str(evidence.run_id) if evidence else None,
        "producer_run_attempt": str(evidence.run_attempt) if evidence else None,
        "publisher_run_id": os.environ.get("GITHUB_RUN_ID"), "publisher_run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
        "payload_sha256": hashlib.sha256(payload).hexdigest(),
        "published_at": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    }


def _publication_commit_sha(api: GitHubApi, repository: str, parent: str, base_tree: str, payload: bytes, receipt: dict[str, Any]) -> str:
    blob_shas = []
    for content in (payload, (json.dumps(receipt, indent=2) + "\n").encode("utf-8")):
        blob = api.request(f"/repos/{repository}/git/blobs", method="POST", value={"content": base64.b64encode(content).decode("ascii"), "encoding": "base64"})
        blob_shas.append(blob["sha"])
    tree = api.request(f"/repos/{repository}/git/trees", method="POST", value={"base_tree": base_tree, "tree": [{"path": "architecture-health.json", "mode": "100644", "type": "blob", "sha": blob_shas[0]}, {"path": "architecture-health-publication.json", "mode": "100644", "type": "blob", "sha": blob_shas[1]}]})
    commit = api.request(f"/repos/{repository}/git/commits", method="POST", value={"message": "chore: publish architecture health badge", "tree": tree["sha"], "parents": [parent]})
    return commit["sha"]


def _update_publication_ref(api: GitHubApi, current_ref: Any, update_ref_path: str, repository: str, commit_sha: str) -> None:
    if current_ref:
        api.request(update_ref_path, method="PATCH", value={"sha": commit_sha, "force": False})
    else:
        api.request(f"/repos/{repository}/git/refs", method="POST", value={"ref": "refs/heads/architecture-health-badge", "sha": commit_sha})


def _prepare_raw_publication(
    api: GitHubApi,
    config,
    payload: bytes,
    evidence: EvidenceContext | None,
    status: str,
    reason: str,
    current_ref: Any,
    repository: str,
) -> str:
    parent = current_ref.get("object", {}).get("sha") if current_ref else os.environ.get("GITHUB_SHA")
    if not isinstance(parent, str):
        raise ProviderFailure("publication_parent_unavailable")
    configured_ref = urllib.parse.quote(config.base_ref, safe="/")
    base_ref = api.request(f"/repos/{repository}/git/ref/heads/{configured_ref}")
    if base_ref.get("object", {}).get("sha") != os.environ.get("GITHUB_SHA"):
        raise ProviderFailure("stale_base_ref")
    parent_commit = api.request(f"/repos/{repository}/git/commits/{parent}")
    base_tree = parent_commit.get("tree", {}).get("sha")
    if not isinstance(base_tree, str):
        raise ProviderFailure("publication_parent_unavailable")
    receipt = _publication_receipt(config, payload, evidence, status, reason)
    return _publication_commit_sha(api, repository, parent, base_tree, payload, receipt)


def _retry_raw_publication(attempt: int, error: ProviderFailure) -> None:
    if attempt + 1 == _MAX_RAW_PUBLICATION_ATTEMPTS:
        raise ProviderFailure("publication_race_lost") from error
    # GitHub may expose the newly created commit/tree slightly after the data API
    # accepts it. Give the ref service a bounded opportunity to observe those objects.
    print(
        f"Architecture Health raw publication retry {attempt + 1}: {error.reason}"
        + (f" (http {error.status_code})" if error.status_code is not None else ""),
        file=sys.stderr,
    )
    time.sleep(2**attempt)


def _publish_raw(api: GitHubApi, config, payload: bytes, *, evidence: EvidenceContext | None, status: str, reason: str) -> None:
    """Atomically update the fixed public raw branch; never force-push it."""
    repository = _repository_path(config.repository)
    ref_path = f"/repos/{repository}/git/ref/heads/architecture-health-badge"
    update_ref_path = f"/repos/{repository}/git/refs/heads/architecture-health-badge"
    for attempt in range(_MAX_RAW_PUBLICATION_ATTEMPTS):
        current_ref = _current_raw_ref(api, ref_path)
        commit_sha = _prepare_raw_publication(api, config, payload, evidence, status, reason, current_ref, repository)
        try:
            _update_publication_ref(api, current_ref, update_ref_path, repository, commit_sha)
            return
        except ProviderFailure as error:
            _retry_raw_publication(attempt, error)


def _validate_invocation(config, operation: str) -> None:
    expected_event = "push" if operation == "publish" else "schedule"
    expected_ref = f"refs/heads/{config.base_ref}"
    if os.environ.get("GITHUB_EVENT_NAME") != expected_event or os.environ.get("GITHUB_REF") != expected_ref:
        raise ProviderFailure("event_or_ref_mismatch")


def _promotion_request(evidence: EvidenceContext, archive: bytes, operation: str) -> PromotionRequest:
    return PromotionRequest(
        evidence=evidence, archive=archive, generation=int(os.environ.get("BADGE_GENERATION", "1")),
        revocation_epoch=int(os.environ.get("BADGE_REVOCATION_EPOCH", "0")),
        idempotency_key=f"{os.environ.get('GITHUB_RUN_ID', 'unknown')}:{os.environ.get('GITHUB_RUN_ATTEMPT', '0')}:{operation}",
        deadline=datetime.now(timezone.utc).replace(microsecond=0) + timedelta(minutes=5),
        now=datetime.now(timezone.utc),
    )


def _unavailable_snapshot_path() -> Path:
    return Path(__file__).resolve().parents[2] / "architecture" / "architecture-health-badge-unavailable.json"


def _report_unavailable(config, api: GitHubApi, evidence: EvidenceContext, decision, output_metadata: dict[str, object]) -> int:
    _write_outputs({**output_metadata, "status": "unavailable"})
    if config.destination.adapter.value == "github-raw":
        _publish_raw(api, config, _unavailable_snapshot_path().read_bytes(), evidence=evidence, status="unassessable", reason=decision.reason.value)
    print(json.dumps({"status": "unavailable", "reason": decision.reason.value}, separators=(",", ":")), file=sys.stderr)
    return 1


def _commit_private(decision, output_metadata: dict[str, object]) -> int:
    private_decision = NoneAdapter().commit(decision)
    _write_outputs({**output_metadata, "status": private_decision.status.value})
    print(json.dumps({"status": private_decision.status.value, "reason": private_decision.reason.value}, separators=(",", ":")))
    return 0


def _handle_failure(error, config, api: GitHubApi | None) -> None:
    if config is not None and config.destination.adapter.value == "github-raw":
        try:
            api = api or GitHubApi()
            _publish_raw(api, config, _unavailable_snapshot_path().read_bytes(), evidence=None, status="unassessable", reason=error.reason)
        except Exception:
            pass
    _write_outputs({"status": "unavailable", "reason": error.reason})
    print(json.dumps({"status": "unavailable", "reason": error.reason}, separators=(",", ":")), file=sys.stderr)


def _prepare_relay_publication(config, decision, evidence: EvidenceContext, request: PromotionRequest) -> tuple[HttpRelayClient, str, str, str, str, int, int]:
    digest = hashlib.sha256(decision.payload or b"").hexdigest()
    horizon = evidence.semantic_horizon.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    client = HttpRelayClient(config.destination.endpoint or "", config.destination.alias or "", config.disclosure_profile)
    token = issue_github_oidc_token(config.destination.audience or "")
    # The first prepare is an observation of Relay-owned state. Local
    # PromotionRequest defaults are never sent as CAS expectations.
    prepared = client.prepare(
        decision.payload or b"", digest, idempotency_key=request.idempotency_key, generation=None,
        revocation_epoch=None, semantic_horizon=horizon, oidc_token=token,
    )
    challenge_id = prepared.get("challenge_id")
    generation = prepared.get("generation")
    revocation_epoch = prepared.get("revocation_epoch")
    if not isinstance(challenge_id, str) or not isinstance(generation, int) or not isinstance(revocation_epoch, int):
        raise ProviderFailure("relay_challenge_invalid")
    return client, digest, horizon, token, challenge_id, generation, revocation_epoch


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--configuration-id", required=True)
    parser.add_argument("--adapter", required=True)
    parser.add_argument("--operation", choices=("publish", "renew"), default="publish")
    args = parser.parse_args()
    config = None
    api = None
    try:
        config = _load_config(args.configuration_id)
        if config.destination.adapter.value != args.adapter:
            raise ProviderFailure("adapter_configuration_mismatch")
        _validate_invocation(config, args.operation)
        api = GitHubApi()
        evidence, archive = resolve_evidence(api, config)
        request = _promotion_request(evidence, archive, args.operation)
        # The Relay challenge is the deadline authority. A raw publication uses
        # the same pure decision and remains a fixed snapshot adapter.
        decision = decide_promotion(config, request)
        output_metadata = {"reason": decision.reason.value, "head_sha": evidence.head_sha, "head_tree_sha": evidence.head_tree_sha, "run_id": evidence.run_id, "run_attempt": evidence.run_attempt}
        if decision.status is PromotionStatus.UNAVAILABLE:
            return _report_unavailable(config, api, evidence, decision, output_metadata)
        if config.destination.adapter.value == "none":
            return _commit_private(decision, output_metadata)
        if config.destination.adapter.value == "relay":
            client, digest, horizon, token, challenge_id, generation, revocation_epoch = _prepare_relay_publication(config, decision, evidence, request)
            if args.operation == "renew":
                client.renew(decision.payload or b"", digest, challenge_id=challenge_id, idempotency_key=request.idempotency_key, generation=generation, revocation_epoch=revocation_epoch, oidc_token=token, semantic_horizon=horizon)
            else:
                client.publish(decision.payload or b"", digest, challenge_id=challenge_id, idempotency_key=request.idempotency_key, generation=generation, revocation_epoch=revocation_epoch, oidc_token=token, semantic_horizon=horizon)
            _write_outputs({**output_metadata, "status": "ready"})
            return 0
        _write_outputs({**output_metadata, "status": "ready"})
        _publish_raw(api, config, decision.payload or b"", evidence=evidence, status="ready", reason=decision.reason.value)
        return 0
    except (ProviderFailure, AdapterError) as error:
        _handle_failure(error, config, api)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())