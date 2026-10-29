from __future__ import annotations

import hashlib
import io
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import cast
import zipfile

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli  # noqa: E402
from badge_promotion.artifact import ArtifactValidationError, validate_artifact  # noqa: E402
from badge_promotion.config import parse_config  # noqa: E402
from badge_promotion.model import EvidenceContext  # noqa: E402


def test_resolve_evidence_accepts_original_manifest_for_selected_copied_job(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    raw = json.loads((Path(__file__).parent / "fixtures" / "approved-config.json").read_text())
    config = parse_config(raw)
    repository = config.repository
    main_sha = "a" * 40
    base_sha = "b" * 40
    head_sha = "c" * 40
    tree_sha = "d" * 40
    run_id = 7001
    original_job_id = 8001
    copied_job_id = 8002
    producer_created_at = datetime.now(timezone.utc).isoformat()
    workflow_path = config.producer.workflow_path
    workflow_ref = f"/repos/{repository}/contents/{workflow_path}?ref={head_sha}"
    check_path = f"/repos/{repository}/commits/{head_sha}/check-runs?check_name=Architecture%20Coverage&filter=latest&per_page=100"
    producer_run_path = f"/repos/{repository}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"
    selected_job_path = f"/repos/{repository}/actions/jobs/{copied_job_id}"
    job_history_path = (
        f"/repos/{repository}/actions/runs/{run_id}/jobs"
        "?filter=all&per_page=100&page=1"
    )
    artifacts_path = f"/repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100"
    original_job = {
        "id": original_job_id,
        "run_id": run_id,
        "head_sha": head_sha,
        "name": config.producer.job_name,
        "status": "completed",
        "conclusion": "success",
        "run_attempt": 1,
        "started_at": "2026-10-28T12:01:59Z",
        "completed_at": "2026-10-28T12:04:17Z",
    }
    copied_job = original_job | {"id": copied_job_id, "run_attempt": 2}
    payload = (
        b'{"schemaVersion":1,"label":"architecture","message":"PASS '
        b'\\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 1 rules",'
        b'"color":"brightgreen"}'
    )

    def badge_archive(*, run_attempt: int, job_id: int | None = None) -> bytes:
        context = {
            "repository": repository,
            "base_ref": config.base_ref,
            "base_sha": base_sha,
            "head_sha": head_sha,
            "head_tree_sha": tree_sha,
            "pr_number": "42",
            "run_id": str(run_id),
            "run_attempt": str(run_attempt),
        }
        if job_id is not None:
            context["job_id"] = job_id
        manifest = {
            "schema": config.schema_id,
            "kind": "architecture-health-badge",
            "context": context,
            "payload": {
                "path": config.producer.payload_path,
                "bytes": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest(),
            },
        }
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(config.producer.payload_path, payload)
            archive.writestr(
                "architecture-health-badge.manifest.json",
                json.dumps(manifest, separators=(",", ":")).encode(),
            )
        return stream.getvalue()

    valid_archive = badge_archive(run_attempt=1)
    evidence_stream = io.BytesIO()
    with zipfile.ZipFile(evidence_stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "architecture-health.json",
            b'{"report_evidence":{"publication_evidence":{"semantic_horizon":"2099-09-13T16:00:00Z"}}}',
        )

    class ResolveApi:
        def request(self, path: str, **_: object) -> object:
            responses: dict[str, object] = {
                f"/repos/{repository}/commits/{main_sha}": {"commit": {"tree": {"sha": tree_sha}}, "parents": [{"sha": base_sha}]},
                f"/repos/{repository}/commits/{main_sha}/pulls": [{"number": 42}],
                f"/repos/{repository}/pulls/42": {"base": {"repo": {"full_name": repository}, "ref": "main"}, "merged": True, "merge_commit_sha": main_sha, "head": {"sha": head_sha}},
                f"/repos/{repository}/commits/{head_sha}": {"commit": {"tree": {"sha": tree_sha}}},
                workflow_ref: {"type": "file", "sha": config.producer.workflow_sha},
                check_path: {"check_runs": [{"name": "Architecture Coverage", "status": "completed", "conclusion": "success", "app": {"slug": "github-actions", "id": 15368}, "details_url": f"https://github.com/{repository}/actions/runs/{run_id}/job/{copied_job_id}"}]},
                producer_run_path: {"workflow_runs": [{"id": run_id, "path": workflow_path, "event": "pull_request", "head_sha": head_sha, "conclusion": "success", "run_attempt": 2, "created_at": producer_created_at}]},
                selected_job_path: copied_job,
                job_history_path: {"total_count": 2, "jobs": [original_job, copied_job]},
                artifacts_path: {"artifacts": [{"id": 9001, "name": config.producer.artifact_name, "expired": False, "archive_download_url": "badge"}, {"id": 9002, "name": config.producer.evidence_artifact_name, "expired": False, "archive_download_url": "evidence"}]},
                f"/repos/{repository}/rules/branches/main": [{"type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True, "required_status_checks": [{"context": "Architecture Coverage", "integration_id": 15368}]}}],
            }
            return responses[path]

        def download(self, url: str) -> bytes:
            return valid_archive if url == "badge" else evidence_stream.getvalue()

    temporal_identity_inputs: list[str] = []

    def fake_temporal_revalidation(*_args: object, **kwargs: object):
        producer_identity = str(kwargs["producer_identity_sha256"])
        temporal_identity_inputs.append(producer_identity)
        now = datetime.now(timezone.utc)
        return (
            {"producer_identity_sha256": producer_identity},
            datetime(2099, 9, 14, 16, tzinfo=timezone.utc),
            now,
        )

    monkeypatch.setenv("GITHUB_REPOSITORY", repository)
    monkeypatch.setenv("GITHUB_SHA", main_sha)
    monkeypatch.setattr(
        cli,
        "_run_temporal_revalidation_with_midnight_retry",
        fake_temporal_revalidation,
    )
    resolved, downloaded_archive = cli.resolve_evidence(
        ResolveApi(), config, force_temporal_revalidation=True
    )
    assert isinstance(resolved, EvidenceContext)
    assert resolved.run_id == run_id
    assert resolved.run_attempt == 1
    assert resolved.job_id == original_job_id
    assert resolved.producer_identity_sha256 is not None

    def producer_identity(run_attempt: int, job_id: int) -> cli.ProducerIdentity:
        return cli.ProducerIdentity(
            repository=repository,
            base_ref=config.base_ref,
            base_sha=base_sha,
            main_tree_sha=tree_sha,
            head_sha=head_sha,
            head_tree_sha=tree_sha,
            pr_number=42,
            workflow_path=config.producer.workflow_path,
            workflow_sha=config.producer.workflow_sha,
            check_name=config.producer.check_name,
            check_app=config.producer.check_app,
            run_id=run_id,
            run_attempt=run_attempt,
            job_id=job_id,
            job_name=config.producer.job_name,
            artifact_id=9001,
            artifact_name=config.producer.artifact_name,
            evidence_artifact_id=9002,
        )

    expected_identity = cli._producer_identity_sha256(producer_identity(1, original_job_id))
    copied_identity = cli._producer_identity_sha256(producer_identity(2, copied_job_id))
    assert resolved.producer_identity_sha256 == expected_identity
    assert temporal_identity_inputs == [expected_identity]
    assert expected_identity != copied_identity
    assert resolved.temporal_receipt == {
        "producer_identity_sha256": resolved.producer_identity_sha256
    }
    assert validate_artifact(downloaded_archive, config, resolved).payload == payload

    wrong_attempt_archive = badge_archive(run_attempt=2)
    with pytest.raises(ArtifactValidationError):
        validate_artifact(wrong_attempt_archive, config, resolved)
    copied_job_archive = badge_archive(run_attempt=1, job_id=copied_job_id)
    with pytest.raises(ArtifactValidationError):
        validate_artifact(copied_job_archive, config, resolved)
