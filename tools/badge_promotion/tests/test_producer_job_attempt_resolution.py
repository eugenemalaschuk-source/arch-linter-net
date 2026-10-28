from __future__ import annotations

from pathlib import Path
import sys
from types import SimpleNamespace
from typing import cast

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from badge_promotion import cli  # noqa: E402
from badge_promotion.cli import ProviderFailure  # noqa: E402


_REPOSITORY = "owner/repo"
_HEAD_SHA = "a" * 40
_RUN_ID = 7001
_START = "2026-10-28T12:01:59Z"
_END = "2026-10-28T12:04:17Z"
_LATER_START = "2026-10-28T12:20:00Z"
_LATER_END = "2026-10-28T12:23:00Z"


class FakeApi:
    def __init__(self, responses: dict[str, object]) -> None:
        self.responses = responses
        self.paths: list[str] = []

    def request(self, path: str, **_: object) -> object:
        self.paths.append(path)
        if path not in self.responses:
            raise AssertionError(f"unexpected request: {path}")
        return self.responses[path]


def _config() -> SimpleNamespace:
    return SimpleNamespace(
        producer=SimpleNamespace(
            workflow_path=".github/workflows/ci.yml",
            event="pull_request",
            job_name="Architecture Coverage",
        )
    )


def _job(
    job_id: int,
    attempt: int,
    *,
    started_at: str | None = _START,
    completed_at: str | None = _END,
    **changes: object,
) -> dict[str, object]:
    job: dict[str, object] = {
        "id": job_id,
        "run_id": _RUN_ID,
        "head_sha": _HEAD_SHA,
        "name": "Architecture Coverage",
        "status": "completed",
        "conclusion": "success",
        "run_attempt": attempt,
        "started_at": started_at,
        "completed_at": completed_at,
    }
    job.update(changes)
    return job


def _paths(run_id: int = _RUN_ID) -> tuple[str, str, str]:
    runs_path = (
        f"/repos/{_REPOSITORY}/actions/runs?head_sha={_HEAD_SHA}"
        "&event=pull_request&per_page=100"
    )
    selected_job_path = f"/repos/{_REPOSITORY}/actions/jobs/"
    history_path = (
        f"/repos/{_REPOSITORY}/actions/runs/{run_id}/jobs"
        "?filter=all&per_page=100&page=1"
    )
    return runs_path, selected_job_path, history_path


def _api(
    *,
    selected_job: dict[str, object],
    history: list[dict[str, object]],
    run_attempt: int | bool = 1,
    history_responses: dict[int, object] | None = None,
) -> FakeApi:
    runs_path, selected_job_prefix, history_path = _paths()
    responses: dict[str, object] = {
        runs_path: {
            "workflow_runs": [
                {
                    "id": _RUN_ID,
                    "path": ".github/workflows/ci.yml",
                    "event": "pull_request",
                    "head_sha": _HEAD_SHA,
                    "conclusion": "success",
                    "run_attempt": run_attempt,
                }
            ]
        },
        selected_job_prefix + str(selected_job["id"]): selected_job,
        history_path: {"total_count": len(history), "jobs": history},
    }
    for page, response in (history_responses or {}).items():
        responses[history_path.rsplit("page=", maxsplit=1)[0] + f"page={page}"] = response
    return FakeApi(responses)


def _resolve(api: FakeApi, selected_job_id: int) -> tuple[dict[str, object], int, dict[str, object]]:
    check = {
        "details_url": (
            f"https://github.com/{_REPOSITORY}/actions/runs/{_RUN_ID}/job/{selected_job_id}"
        )
    }
    result = cli._producer_run(
        cast(cli.GitHubApi, api), _REPOSITORY, _HEAD_SHA, _config(), check
    )
    return result


def test_first_execution_keeps_exact_selected_job_identity() -> None:
    selected = _job(8001, 1, started_at=None, completed_at=None)
    api = _api(selected_job=selected, history=[selected])

    run, run_id, resolved_job = _resolve(api, 8001)

    assert run["run_attempt"] == 1
    assert run_id == _RUN_ID
    assert resolved_job["id"] == 8001
    assert resolved_job["run_attempt"] == 1


def test_selected_original_job_stays_bound_when_another_job_reruns() -> None:
    original = _job(8001, 1)
    copied = _job(8002, 2)
    api = _api(selected_job=original, history=[original, copied], run_attempt=2)

    _, _, resolved_job = _resolve(api, 8001)

    assert resolved_job["id"] == 8001
    assert resolved_job["run_attempt"] == 1


def test_copied_selected_job_resolves_to_original_execution_identity() -> None:
    original = _job(8001, 1)
    copied = _job(8002, 2)
    api = _api(selected_job=copied, history=[original, copied], run_attempt=2)

    _, _, resolved_job = _resolve(api, 8002)

    assert resolved_job["id"] == 8001
    assert resolved_job["run_attempt"] == 1


def test_multiple_consecutive_copies_resolve_to_first_execution() -> None:
    original = _job(8001, 1)
    copied_twice = _job(8002, 2)
    copied_thrice = _job(8003, 3)
    api = _api(
        selected_job=copied_thrice,
        history=[original, copied_twice, copied_thrice],
        run_attempt=3,
    )

    _, _, resolved_job = _resolve(api, 8003)

    assert resolved_job["id"] == 8001
    assert resolved_job["run_attempt"] == 1


def test_genuine_producer_rerun_keeps_its_new_attempt_and_job() -> None:
    original = _job(8001, 1)
    rerun = _job(8002, 2, started_at=_LATER_START, completed_at=_LATER_END)
    api = _api(selected_job=rerun, history=[original, rerun], run_attempt=2)

    _, _, resolved_job = _resolve(api, 8002)

    assert resolved_job["id"] == 8002
    assert resolved_job["run_attempt"] == 2


def test_copy_after_genuine_rerun_resolves_to_that_new_execution() -> None:
    original = _job(8001, 1)
    rerun = _job(8002, 2, started_at=_LATER_START, completed_at=_LATER_END)
    copied_rerun = _job(8003, 3, started_at=_LATER_START, completed_at=_LATER_END)
    api = _api(
        selected_job=copied_rerun,
        history=[original, rerun, copied_rerun],
        run_attempt=3,
    )

    _, _, resolved_job = _resolve(api, 8003)

    assert resolved_job["id"] == 8002
    assert resolved_job["run_attempt"] == 2


@pytest.mark.parametrize(
    "change",
    [
        {"run_id": 9999},
        {"head_sha": "b" * 40},
        {"name": "Repository Lint"},
        {"status": "in_progress"},
        {"conclusion": "failure"},
        {"run_attempt": 0},
        {"run_attempt": True},
        {"run_attempt": 3},
    ],
)
def test_selected_job_must_match_its_trusted_run_identity(change: dict[str, object]) -> None:
    selected = _job(8002, 2) | change
    history = [_job(8001, 1), _job(8002, 2)]
    api = _api(selected_job=selected, history=history, run_attempt=2)

    with pytest.raises(ProviderFailure, match="producer_job_unresolved"):
        _resolve(api, 8002)


@pytest.mark.parametrize(
    "history_change",
    [
        "missing_previous_attempt",
        "ambiguous_previous_attempt",
        "ambiguous_selected_attempt",
        "missing_selected_job",
        "missing_selected_start",
        "invalid_selected_time",
        "reversed_selected_time",
        "missing_previous_start",
        "invalid_previous_time",
        "failed_copied_predecessor",
        "wrong_previous_run",
        "wrong_previous_head",
        "nonchronological_previous",
    ],
)
def test_incomplete_ambiguous_or_contradictory_history_fails_closed(
    history_change: str,
) -> None:
    original = _job(8001, 1)
    copied = _job(8002, 2)
    history: list[dict[str, object]] = [original, copied]
    selected = copied

    if history_change == "missing_previous_attempt":
        history = [copied]
    elif history_change == "ambiguous_previous_attempt":
        history.append(_job(8003, 1))
    elif history_change == "ambiguous_selected_attempt":
        history.append(_job(8003, 2))
    elif history_change == "missing_selected_job":
        history = [original, _job(8003, 2)]
    elif history_change == "missing_selected_start":
        selected = _job(8002, 2, started_at=None)
        history[1] = selected
    elif history_change == "invalid_selected_time":
        selected = _job(8002, 2, started_at="not-a-time")
        history[1] = selected
    elif history_change == "reversed_selected_time":
        selected = _job(
            8002,
            2,
            started_at="2026-10-28T12:10:00Z",
            completed_at="2026-10-28T12:09:00Z",
        )
        history[1] = selected
    elif history_change == "missing_previous_start":
        history[0] = _job(8001, 1, started_at=None)
    elif history_change == "invalid_previous_time":
        history[0] = _job(8001, 1, completed_at="not-a-time")
    elif history_change == "failed_copied_predecessor":
        history[0] = _job(8001, 1, conclusion="failure")
    elif history_change == "wrong_previous_run":
        history[0] = _job(8001, 1, run_id=9999)
    elif history_change == "wrong_previous_head":
        history[0] = _job(8001, 1, head_sha="b" * 40)
    elif history_change == "nonchronological_previous":
        history[0] = _job(
            8001,
            1,
            started_at=_LATER_START,
            completed_at=_LATER_END,
        )

    api = _api(selected_job=selected, history=history, run_attempt=2)
    with pytest.raises(ProviderFailure, match="producer_job_unresolved"):
        _resolve(api, int(selected["id"]))


def test_invalid_or_unapproved_check_job_url_fails_before_provider_lookup() -> None:
    api = FakeApi({})
    config = _config()
    for details_url in (
        "https://github.com/owner/repo/actions/runs/7001",
        "https://evil.invalid/owner/repo/actions/runs/7001/job/8001",
        "https://github.com/other/repo/actions/runs/7001/job/8001",
        f"https://github.com/{_REPOSITORY}/actions/runs/{'9' * 5000}/job/8001",
    ):
        with pytest.raises(ProviderFailure, match="producer_run_unresolved"):
            cli._producer_run(
                cast(cli.GitHubApi, api),
                _REPOSITORY,
                _HEAD_SHA,
                config,
                {"details_url": details_url},
            )
    assert api.paths == []


def test_run_attempt_and_history_size_are_bounded() -> None:
    selected = _job(8001, 1)
    attempt_api = _api(selected_job=selected, history=[selected], run_attempt=52)
    with pytest.raises(ProviderFailure, match="producer_run_unresolved"):
        _resolve(attempt_api, 8001)

    oversized_api = _api(selected_job=selected, history=[selected])
    _, _, history_path = _paths()
    oversized_api.responses[history_path] = {
        "total_count": 20_001,
        "jobs": [selected],
    }
    with pytest.raises(ProviderFailure, match="producer_job_unresolved"):
        _resolve(oversized_api, 8001)


def test_job_history_paginates_before_proving_unique_selected_job() -> None:
    selected = _job(8001, 1)
    filler = [
        _job(9000 + index, 1, name=f"Other Job {index}")
        for index in range(100)
    ]
    history = [selected, *filler]
    _, _, history_path = _paths()
    history_responses = {
        1: {"total_count": len(history), "jobs": history[:100]},
        2: {"total_count": len(history), "jobs": history[100:]},
    }
    api = _api(
        selected_job=selected,
        history=history,
        history_responses=history_responses,
    )

    _, _, resolved_job = _resolve(api, 8001)

    assert resolved_job["id"] == 8001
    assert history_path in api.paths
    assert history_path.replace("&page=1", "&page=2") in api.paths


def test_inconsistent_history_page_counts_fail_closed() -> None:
    selected = _job(8001, 1)
    history = [selected, *[_job(9000 + index, 1, name=f"Other {index}") for index in range(100)]]
    _, _, history_path = _paths()
    api = _api(
        selected_job=selected,
        history=history,
        history_responses={
            1: {"total_count": len(history), "jobs": history[:100]},
            2: {"total_count": len(history) + 1, "jobs": history[100:]},
        },
    )

    with pytest.raises(ProviderFailure, match="producer_job_unresolved"):
        _resolve(api, 8001)
