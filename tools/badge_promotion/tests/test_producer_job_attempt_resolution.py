from __future__ import annotations

from pathlib import Path
import sys
from types import SimpleNamespace
from typing import cast

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli  # noqa: E402
from badge_promotion.cli import ProviderFailure  # noqa: E402


class FakeApi:
    def __init__(self, responses: dict[str, object]) -> None:
        self.responses = responses
        self.paths: list[str] = []

    def request(self, path: str, **_: object) -> object:
        self.paths.append(path)
        return self.responses[path]


def _config() -> SimpleNamespace:
    return SimpleNamespace(
        producer=SimpleNamespace(
            workflow_path=".github/workflows/ci.yml",
            event="pull_request",
            job_name="Architecture Coverage",
        )
    )


def test_producer_run_binds_to_exact_check_job_when_run_was_rerun() -> None:
    repository = "owner/repo"
    head_sha = "a" * 40
    run_id = 7001
    selected_job_id = 8001
    latest_job_id = 8002
    runs_path = f"/repos/{repository}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"
    jobs_path = f"/repos/{repository}/actions/runs/{run_id}/jobs?per_page=100"
    selected_job_path = f"/repos/{repository}/actions/jobs/{selected_job_id}"
    api = FakeApi(
        {
            runs_path: {
                "workflow_runs": [
                    {
                        "id": run_id,
                        "path": ".github/workflows/ci.yml",
                        "event": "pull_request",
                        "head_sha": head_sha,
                        "conclusion": "success",
                        "run_attempt": 2,
                    }
                ]
            },
            jobs_path: {
                "jobs": [
                    {
                        "id": latest_job_id,
                        "run_id": run_id,
                        "head_sha": head_sha,
                        "name": "Architecture Coverage",
                        "conclusion": "success",
                        "run_attempt": 2,
                    }
                ]
            },
            selected_job_path: {
                "id": selected_job_id,
                "run_id": run_id,
                "head_sha": head_sha,
                "name": "Architecture Coverage",
                "conclusion": "success",
                "run_attempt": 1,
            },
        }
    )
    check = {
        "details_url": f"https://github.com/{repository}/actions/runs/{run_id}/job/{selected_job_id}"
    }

    run, resolved_run_id, job = cli._producer_run(
        cast(cli.GitHubApi, api), repository, head_sha, _config(), check
    )

    assert resolved_run_id == run_id
    assert run["run_attempt"] == 2
    assert job["id"] == selected_job_id
    assert job["run_attempt"] == 1
    assert selected_job_path in api.paths


@pytest.mark.parametrize(
    "job_change",
    (
        {"run_id": 9999},
        {"head_sha": "b" * 40},
        {"name": "Repository Lint"},
        {"conclusion": "failure"},
        {"run_attempt": 0},
        {"run_attempt": True},
    ),
)
def test_producer_run_rejects_invalid_exact_check_job(job_change: dict[str, object]) -> None:
    repository = "owner/repo"
    head_sha = "a" * 40
    run_id = 7001
    selected_job_id = 8001
    runs_path = f"/repos/{repository}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"
    jobs_path = f"/repos/{repository}/actions/runs/{run_id}/jobs?per_page=100"
    selected_job_path = f"/repos/{repository}/actions/jobs/{selected_job_id}"
    selected_job: dict[str, object] = {
        "id": selected_job_id,
        "run_id": run_id,
        "head_sha": head_sha,
        "name": "Architecture Coverage",
        "conclusion": "success",
        "run_attempt": 1,
    }
    selected_job.update(job_change)
    api = FakeApi(
        {
            runs_path: {
                "workflow_runs": [
                    {
                        "id": run_id,
                        "path": ".github/workflows/ci.yml",
                        "event": "pull_request",
                        "head_sha": head_sha,
                        "conclusion": "success",
                        "run_attempt": 2,
                    }
                ]
            },
            jobs_path: {"jobs": []},
            selected_job_path: selected_job,
        }
    )
    check = {
        "details_url": f"https://github.com/{repository}/actions/runs/{run_id}/job/{selected_job_id}"
    }

    with pytest.raises(ProviderFailure, match="producer_job_unresolved"):
        cli._producer_run(cast(cli.GitHubApi, api), repository, head_sha, _config(), check)


def test_producer_run_requires_job_identity_in_check_details_url() -> None:
    repository = "owner/repo"
    head_sha = "a" * 40
    with pytest.raises(ProviderFailure, match="producer_run_unresolved"):
        cli._producer_run(
            cast(cli.GitHubApi, FakeApi({})),
            repository,
            head_sha,
            _config(),
            {"details_url": "https://github.com/owner/repo/actions/runs/7001"},
        )
