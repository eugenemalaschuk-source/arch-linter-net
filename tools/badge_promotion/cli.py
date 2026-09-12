"""GitHub Actions adapter for the pure trusted-promotion contract.

The resolver is intentionally the only provider-facing code here. It converts
GitHub API facts into ``EvidenceContext``; the pure package then validates the
artifact and decides whether a transport may commit it.
"""

from __future__ import annotations

import argparse
import base64
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from typing import Any

from .config import ConfigValidationError, parse_config
from .decision import PromotionRequest, decide_promotion
from .adapters import HttpRelayClient, NoneAdapter, issue_github_oidc_token
from .model import EvidenceContext, PromotionStatus, ReasonCode


class ProviderFailure(RuntimeError):
    def __init__(self, reason: str) -> None:
        self.reason = reason
        super().__init__(reason)


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
                raise ProviderFailure("required_capability_unavailable") from error
            raise ProviderFailure("github_api_unavailable") from error
        except (OSError, json.JSONDecodeError) as error:
            raise ProviderFailure("github_api_unavailable") from error

    def download(self, url: str) -> bytes:
        parsed = urllib.parse.urlparse(url)
        api_host = urllib.parse.urlparse(self.base).netloc
        if parsed.scheme != "https" or parsed.netloc not in {api_host, "api.github.com"}:
            raise ProviderFailure("artifact_url_unapproved")
        request = urllib.request.Request(url, headers={"accept": "application/octet-stream", "authorization": f"Bearer {self.token}"})
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                data = response.read(65_537)
        except (OSError, urllib.error.HTTPError) as error:
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


def _required_gate(api: GitHubApi, repository: str, check_name: str) -> bool:
    def has_required_check(document: Any) -> bool:
        if not isinstance(document, dict):
            return False
        parameters = document.get("parameters", {})
        checks = parameters.get("required_status_checks", []) if isinstance(parameters, dict) else []
        if isinstance(checks, list) and any(isinstance(check, dict) and check.get("context") == check_name for check in checks):
            return True
        rules = document.get("rules", [])
        return isinstance(rules, list) and any(has_required_check(rule) for rule in rules)

    repository_path = _repository_path(repository)
    try:
        rules = api.request(f"/repos/{repository_path}/rules/branches/main")
    except ProviderFailure as error:
        if error.reason != "required_capability_unavailable":
            raise
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
            try:
                detail = api.request(f"/repos/{repository_path}/rulesets/{ruleset_id}")
            except ProviderFailure as detail_error:
                if detail_error.reason == "required_capability_unavailable":
                    continue
                raise
            if has_required_check(detail):
                return True
        return False
    if has_required_check({"rules": rules}):
        return True
    return False


def _workflow_blob_sha(api: GitHubApi, repository: str, workflow_path: str, ref: str) -> str:
    path = urllib.parse.quote(workflow_path, safe="/")
    reference = urllib.parse.quote(ref, safe="")
    workflow = api.request(f"/repos/{_repository_path(repository)}/contents/{path}?ref={reference}")
    if not isinstance(workflow, dict) or workflow.get("type") != "file":
        raise ProviderFailure("workflow_mismatch")
    return _sha(workflow.get("sha"))


def resolve_evidence(api: GitHubApi, config) -> tuple[EvidenceContext, bytes]:
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    main_sha = _sha(os.environ.get("GITHUB_SHA"))
    if repository != config.repository:
        raise ProviderFailure("repository_mismatch")
    commit = api.request(f"/repos/{_repository_path(repository)}/commits/{main_sha}")
    main_tree = _sha(commit.get("commit", {}).get("tree", {}).get("sha"))
    parents = commit.get("parents", [])
    if len(parents) != 1:
        raise ProviderFailure("main_commit_shape_invalid")
    base_sha = _sha(parents[0].get("sha"))
    associated = api.request(f"/repos/{_repository_path(repository)}/commits/{main_sha}/pulls")
    if not isinstance(associated, list) or len(associated) != 1 or not isinstance(associated[0].get("number"), int):
        raise ProviderFailure("merged_pull_request_ambiguous")
    pr_number = associated[0]["number"]
    pull = api.request(f"/repos/{_repository_path(repository)}/pulls/{pr_number}")
    base = pull.get("base", {})
    if base.get("repo", {}).get("full_name") != repository or base.get("ref") != "main" or pull.get("merged") is not True or pull.get("merge_commit_sha") != main_sha:
        raise ProviderFailure("merged_pull_request_invalid")
    head_sha = _sha(pull.get("head", {}).get("sha"))
    head_commit = api.request(f"/repos/{_repository_path(repository)}/commits/{head_sha}")
    head_tree = _sha(head_commit.get("commit", {}).get("tree", {}).get("sha"))
    if head_tree != main_tree:
        raise ProviderFailure("merged_tree_mismatch")
    workflow_sha = _workflow_blob_sha(api, repository, config.producer.workflow_path, head_sha)
    if workflow_sha != config.producer.workflow_sha:
        raise ProviderFailure("workflow_mismatch")
    check_name = urllib.parse.quote(config.producer.check_name, safe="")
    checks = api.request(f"/repos/{_repository_path(repository)}/commits/{head_sha}/check-runs?check_name={check_name}&filter=latest&per_page=100")
    successful_checks = [item for item in checks.get("check_runs", []) if item.get("name") == config.producer.check_name and item.get("status") == "completed" and item.get("conclusion") == "success" and item.get("app", {}).get("slug") == config.producer.check_app]
    if len(successful_checks) != 1:
        raise ProviderFailure("required_gate_not_successful")
    details = successful_checks[0].get("details_url", "")
    run_match = re.search(r"/runs/(\d+)(?:/|$)", details)
    if run_match is None:
        raise ProviderFailure("producer_run_unresolved")
    run_id = int(run_match.group(1))
    runs = api.request(f"/repos/{_repository_path(repository)}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100")
    matching = [run for run in runs.get("workflow_runs", []) if run.get("id") == run_id and run.get("path") == config.producer.workflow_path and run.get("event") == config.producer.event and run.get("head_sha") == head_sha and run.get("conclusion") == "success"]
    if len(matching) != 1:
        raise ProviderFailure("producer_run_unresolved")
    run = matching[0]
    jobs = api.request(f"/repos/{_repository_path(repository)}/actions/runs/{run_id}/jobs?per_page=100").get("jobs", [])
    producer_jobs = [job for job in jobs if job.get("name") == config.producer.job_name and job.get("conclusion") == "success"]
    if len(producer_jobs) != 1:
        raise ProviderFailure("producer_job_unresolved")
    job = producer_jobs[0]
    artifacts = api.request(f"/repos/{_repository_path(repository)}/actions/runs/{run_id}/artifacts?per_page=100").get("artifacts", [])
    selected = [artifact for artifact in artifacts if artifact.get("name") == config.producer.artifact_name]
    if len(selected) != 1 or selected[0].get("expired") is True:
        raise ProviderFailure("artifact_missing_or_expired")
    artifact = selected[0]
    archive = api.download(str(artifact.get("archive_download_url", "")))
    verified_at = _parse_time(run.get("created_at"))
    evidence_artifacts = [item for item in artifacts if item.get("name") == config.producer.evidence_artifact_name and item.get("expired") is not True]
    if len(evidence_artifacts) != 1:
        raise ProviderFailure("semantic_evidence_unavailable")
    evidence_archive = api.download(str(evidence_artifacts[0].get("archive_download_url", "")))
    try:
        with zipfile.ZipFile(io.BytesIO(evidence_archive)) as opened:
            health = json.loads(opened.read("architecture-health.json").decode("utf-8"))
        semantic_value = health["report_evidence"]["publication_evidence"]["semantic_horizon"]
        semantic_horizon = _parse_time(semantic_value)
    except (KeyError, TypeError, ValueError, UnicodeError, zipfile.BadZipFile) as error:
        raise ProviderFailure("semantic_evidence_unavailable") from error
    evidence = EvidenceContext(
        repository=repository, base_ref="main", base_sha=base_sha, main_tree_sha=main_tree,
        head_sha=head_sha, head_tree_sha=head_tree, pr_number=pr_number, event=config.producer.event,
        merged=True, workflow_path=config.producer.workflow_path, workflow_sha=workflow_sha,
        check_name=config.producer.check_name, check_app=config.producer.check_app, check_status="completed",
        check_conclusion="success", required_gate_present=_required_gate(api, repository, config.producer.check_name), run_id=run_id,
        run_attempt=int(run.get("run_attempt", 0)), job_id=int(job.get("id", 0)), job_name=job.get("name", ""),
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


def _publish_raw(api: GitHubApi, config, payload: bytes, *, evidence: EvidenceContext | None, status: str, reason: str) -> None:
    """Atomically update the fixed public raw branch; never force-push it."""
    repository = _repository_path(config.repository)
    ref_path = f"/repos/{repository}/git/ref/heads/architecture-health-badge"
    try:
        current_ref = api.request(ref_path)
    except ProviderFailure as error:
        if error.reason != "required_capability_unavailable":
            raise
        current_ref = None
    parent = current_ref.get("object", {}).get("sha") if current_ref else os.environ.get("GITHUB_SHA")
    if not isinstance(parent, str):
        raise ProviderFailure("publication_parent_unavailable")
    main_ref = api.request(f"/repos/{repository}/git/ref/heads/main")
    if main_ref.get("object", {}).get("sha") != os.environ.get("GITHUB_SHA"):
        raise ProviderFailure("stale_main")
    parent_commit = api.request(f"/repos/{repository}/git/commits/{parent}")
    base_tree = parent_commit.get("tree", {}).get("sha")
    if not isinstance(base_tree, str):
        raise ProviderFailure("publication_parent_unavailable")
    receipt: dict[str, Any] = {
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
    blob_shas = []
    for content in (payload, (json.dumps(receipt, indent=2) + "\n").encode("utf-8")):
        blob = api.request(f"/repos/{repository}/git/blobs", method="POST", value={"content": base64.b64encode(content).decode("ascii"), "encoding": "base64"})
        blob_shas.append(blob["sha"])
    tree = api.request(f"/repos/{repository}/git/trees", method="POST", value={"base_tree": base_tree, "tree": [{"path": "architecture-health.json", "mode": "100644", "type": "blob", "sha": blob_shas[0]}, {"path": "architecture-health-publication.json", "mode": "100644", "type": "blob", "sha": blob_shas[1]}]})
    commit = api.request(f"/repos/{repository}/git/commits", method="POST", value={"message": "chore: publish architecture health badge", "tree": tree["sha"], "parents": [parent]})
    try:
        if current_ref:
            api.request(ref_path, method="PATCH", value={"sha": commit["sha"], "force": False})
        else:
            api.request(f"/repos/{repository}/git/refs", method="POST", value={"ref": "refs/heads/architecture-health-badge", "sha": commit["sha"]})
    except ProviderFailure as error:
        raise ProviderFailure("publication_race_lost") from error


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--configuration-id", required=True)
    parser.add_argument("--adapter", required=True)
    parser.add_argument("--operation", choices=("publish", "renew"), default="publish")
    args = parser.parse_args()
    try:
        config = _load_config(args.configuration_id)
        if config.destination.adapter.value != args.adapter:
            raise ProviderFailure("adapter_configuration_mismatch")
        if os.environ.get("GITHUB_EVENT_NAME") != "push" or os.environ.get("GITHUB_REF") != "refs/heads/main":
            raise ProviderFailure("event_or_ref_mismatch")
        api = GitHubApi()
        evidence, archive = resolve_evidence(api, config)
        request = PromotionRequest(
            evidence=evidence, archive=archive, generation=int(os.environ.get("BADGE_GENERATION", "1")),
            revocation_epoch=int(os.environ.get("BADGE_REVOCATION_EPOCH", "0")),
            idempotency_key=f"{os.environ.get('GITHUB_RUN_ID', 'unknown')}:{os.environ.get('GITHUB_RUN_ATTEMPT', '0')}:{args.operation}",
            deadline=datetime.now(timezone.utc).replace(microsecond=0) + timedelta(minutes=5),
            now=datetime.now(timezone.utc),
        )
        # The Relay challenge is the deadline authority. A raw publication uses
        # the same pure decision and remains a fixed snapshot adapter.
        decision = decide_promotion(config, request)
        output_metadata = {"reason": decision.reason.value, "head_sha": evidence.head_sha, "head_tree_sha": evidence.head_tree_sha, "run_id": evidence.run_id, "run_attempt": evidence.run_attempt}
        if decision.status is PromotionStatus.UNAVAILABLE:
            _write_outputs({**output_metadata, "status": "unavailable"})
            if config.destination.adapter.value == "github-raw":
                unavailable = Path(__file__).resolve().parents[2] / "architecture" / "architecture-health-badge-unavailable.json"
                _publish_raw(api, config, unavailable.read_bytes(), evidence=evidence, status="unassessable", reason=decision.reason.value)
            print(json.dumps({"status": "unavailable", "reason": decision.reason.value}, separators=(",", ":")), file=sys.stderr)
            return 1
        if config.destination.adapter.value == "none":
            private_decision = NoneAdapter().commit(decision)
            _write_outputs({**output_metadata, "status": private_decision.status.value})
            print(json.dumps({"status": private_decision.status.value, "reason": private_decision.reason.value}, separators=(",", ":")))
            return 0
        if config.destination.adapter.value == "relay":
            digest = hashlib.sha256(decision.payload or b"").hexdigest()
            horizon = evidence.semantic_horizon.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
            client = HttpRelayClient(config.destination.endpoint or "", config.destination.alias or "", config.disclosure_profile)
            token = issue_github_oidc_token(config.destination.audience or "")
            # The first prepare is an observation of Relay-owned state.  Local
            # PromotionRequest defaults are never sent as CAS expectations.
            prepared = client.prepare(decision.payload or b"", digest, idempotency_key=request.idempotency_key, generation=None, revocation_epoch=None, semantic_horizon=horizon, oidc_token=token)
            challenge_id = prepared.get("challenge_id")
            generation = prepared.get("generation")
            revocation_epoch = prepared.get("revocation_epoch")
            if not isinstance(challenge_id, str) or not isinstance(generation, int) or not isinstance(revocation_epoch, int):
                raise ProviderFailure("relay_challenge_invalid")
            if args.operation == "renew":
                client.renew(decision.payload or b"", digest, challenge_id=challenge_id, idempotency_key=request.idempotency_key, generation=generation, revocation_epoch=revocation_epoch, oidc_token=token, semantic_horizon=horizon, tree_sha=evidence.head_tree_sha)
            else:
                client.publish(decision.payload or b"", digest, challenge_id=challenge_id, idempotency_key=request.idempotency_key, generation=generation, revocation_epoch=revocation_epoch, oidc_token=token, semantic_horizon=horizon, tree_sha=evidence.head_tree_sha)
            return 0
        _write_outputs({**output_metadata, "status": "ready"})
        _publish_raw(api, config, decision.payload or b"", evidence=evidence, status="ready", reason=decision.reason.value)
        return 0
    except ProviderFailure as error:
        if "config" in locals() and config.destination.adapter.value == "github-raw":
            try:
                api = locals().get("api") or GitHubApi()
                unavailable = Path(__file__).resolve().parents[2] / "architecture" / "architecture-health-badge-unavailable.json"
                _publish_raw(api, config, unavailable.read_bytes(), evidence=None, status="unassessable", reason=error.reason)
            except Exception:
                pass
        _write_outputs({"status": "unavailable", "reason": error.reason})
        print(json.dumps({"status": "unavailable", "reason": error.reason}, separators=(",", ":")), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
