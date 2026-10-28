"""Resolve trusted producer jobs across GitHub Actions run attempts."""

from __future__ import annotations

from datetime import datetime, timezone
import os
import re
from typing import Any
import urllib.parse

from .errors import ProviderFailure


_MAX_RUN_ATTEMPTS = 51
_JOB_HISTORY_PAGE_SIZE = 100
_MAX_JOB_HISTORY_RECORDS = 20_000
_MAX_JOB_HISTORY_PAGES = _MAX_JOB_HISTORY_RECORDS // _JOB_HISTORY_PAGE_SIZE
_RUN_JOB_PATH = re.compile(r"^(.+)/actions/runs/(\d+)/job/(\d+)$")


def _is_positive_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def _producer_job_history(api: Any, repository_path: str, run_id: int) -> list[dict[str, Any]]:
    base_path = f"/repos/{repository_path}/actions/runs/{run_id}/jobs"
    jobs: list[dict[str, Any]] = []
    seen_ids: set[int] = set()
    expected_count: int | None = None

    for page in range(1, _MAX_JOB_HISTORY_PAGES + 1):
        document = api.request(
            f"{base_path}?filter=all&per_page={_JOB_HISTORY_PAGE_SIZE}&page={page}"
        )
        if not isinstance(document, dict):
            raise ProviderFailure("producer_job_unresolved")

        total_count = document.get("total_count")
        page_jobs = document.get("jobs")
        if (
            isinstance(total_count, bool)
            or not isinstance(total_count, int)
            or total_count < 0
            or total_count > _MAX_JOB_HISTORY_RECORDS
            or not isinstance(page_jobs, list)
            or len(page_jobs) > _JOB_HISTORY_PAGE_SIZE
        ):
            raise ProviderFailure("producer_job_unresolved")
        if expected_count is None:
            expected_count = total_count
        elif total_count != expected_count:
            raise ProviderFailure("producer_job_unresolved")
        if len(jobs) + len(page_jobs) > total_count:
            raise ProviderFailure("producer_job_unresolved")

        for job in page_jobs:
            if not isinstance(job, dict) or not _is_positive_int(job.get("id")):
                raise ProviderFailure("producer_job_unresolved")
            job_id = job["id"]
            if job_id in seen_ids:
                raise ProviderFailure("producer_job_unresolved")
            seen_ids.add(job_id)
            jobs.append(job)

        if len(jobs) == expected_count:
            return jobs
        if not page_jobs or page == _MAX_JOB_HISTORY_PAGES:
            raise ProviderFailure("producer_job_unresolved")

    raise ProviderFailure("producer_job_unresolved")


def _validate_job(
    job: dict[str, Any],
    *,
    job_id: int,
    run_id: int,
    head_sha: str,
    job_name: str,
    run_attempt: int,
    require_success: bool,
) -> None:
    conclusion = job.get("conclusion")
    if (
        not _is_positive_int(job.get("id"))
        or job.get("id") != job_id
        or not _is_positive_int(job.get("run_id"))
        or job.get("run_id") != run_id
        or job.get("head_sha") != head_sha
        or job.get("name") != job_name
        or job.get("status") != "completed"
        or job.get("run_attempt") != run_attempt
        or not isinstance(conclusion, str)
        or not conclusion
        or (require_success and conclusion != "success")
    ):
        raise ProviderFailure("producer_job_unresolved")


def _job_execution_times(job: dict[str, Any]) -> tuple[datetime, datetime]:
    values = (job.get("started_at"), job.get("completed_at"))
    parsed: list[datetime] = []
    for value in values:
        if not isinstance(value, str):
            raise ProviderFailure("producer_job_unresolved")
        try:
            timestamp = datetime.fromisoformat(value.replace("Z", "+00:00"))
        except ValueError as error:
            raise ProviderFailure("producer_job_unresolved") from error
        if timestamp.tzinfo is None or timestamp.utcoffset() is None:
            raise ProviderFailure("producer_job_unresolved")
        parsed.append(timestamp.astimezone(timezone.utc))

    start, end = parsed
    if start >= end:
        raise ProviderFailure("producer_job_unresolved")
    return start, end


def _selected_check_job_ids(details_url: Any, repository: str) -> tuple[int, int]:
    if not isinstance(details_url, str):
        raise ProviderFailure("producer_run_unresolved")
    parsed = urllib.parse.urlparse(details_url)
    server = urllib.parse.urlparse(os.environ.get("GITHUB_SERVER_URL", "https://github.com"))
    match = _RUN_JOB_PATH.fullmatch(parsed.path)
    if (
        parsed.scheme != "https"
        or not server.netloc
        or parsed.netloc.casefold() != server.netloc.casefold()
        or match is None
        or match.group(1) != f"/{repository}"
    ):
        raise ProviderFailure("producer_run_unresolved")

    run_id_text, job_id_text = match.group(2), match.group(3)
    if len(run_id_text) > 20 or len(job_id_text) > 20:
        raise ProviderFailure("producer_run_unresolved")
    run_id, job_id = int(run_id_text), int(job_id_text)
    if run_id <= 0 or job_id <= 0:
        raise ProviderFailure("producer_run_unresolved")
    return run_id, job_id


def resolve_producer_run(
    api: Any,
    repository: str,
    head_sha: str,
    config: Any,
    check: dict[str, Any],
) -> tuple[dict[str, Any], int, dict[str, Any]]:
    """Return the run and job record for the execution selected by a successful check."""
    run_id, job_id = _selected_check_job_ids(check.get("details_url"), repository)
    repository_path = urllib.parse.quote(repository, safe="/")
    runs_document = api.request(
        f"/repos/{repository_path}/actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"
    )
    runs = runs_document.get("workflow_runs") if isinstance(runs_document, dict) else None
    if not isinstance(runs, list):
        raise ProviderFailure("producer_run_unresolved")

    matching_runs = [
        run
        for run in runs
        if isinstance(run, dict)
        and run.get("id") == run_id
        and run.get("path") == config.producer.workflow_path
        and run.get("event") == config.producer.event
        and run.get("head_sha") == head_sha
        and run.get("conclusion") == "success"
    ]
    if len(matching_runs) != 1 or not _is_positive_int(matching_runs[0].get("id")):
        raise ProviderFailure("producer_run_unresolved")
    run = matching_runs[0]
    latest_attempt = run.get("run_attempt")
    if not _is_positive_int(latest_attempt) or latest_attempt > _MAX_RUN_ATTEMPTS:
        raise ProviderFailure("producer_run_unresolved")

    selected_job = api.request(f"/repos/{repository_path}/actions/jobs/{job_id}")
    selected_attempt = selected_job.get("run_attempt") if isinstance(selected_job, dict) else None
    if (
        not isinstance(selected_job, dict)
        or not _is_positive_int(selected_attempt)
        or selected_attempt > latest_attempt
    ):
        raise ProviderFailure("producer_job_unresolved")
    _validate_job(
        selected_job,
        job_id=job_id,
        run_id=run_id,
        head_sha=head_sha,
        job_name=config.producer.job_name,
        run_attempt=selected_attempt,
        require_success=True,
    )

    history = _producer_job_history(api, repository_path, run_id)
    history_by_id = [job for job in history if job.get("id") == job_id]
    if len(history_by_id) != 1:
        raise ProviderFailure("producer_job_unresolved")
    selected_history_job = history_by_id[0]
    for field in ("id", "run_id", "head_sha", "name", "status", "conclusion", "run_attempt", "started_at", "completed_at"):
        if selected_history_job.get(field) != selected_job.get(field):
            raise ProviderFailure("producer_job_unresolved")

    jobs_by_attempt: dict[int, list[dict[str, Any]]] = {}
    for job in history:
        if job.get("name") != config.producer.job_name:
            continue
        attempt = job.get("run_attempt")
        if not _is_positive_int(attempt) or attempt > latest_attempt:
            raise ProviderFailure("producer_job_unresolved")
        _validate_job(
            job,
            job_id=job["id"],
            run_id=run_id,
            head_sha=head_sha,
            job_name=config.producer.job_name,
            run_attempt=attempt,
            require_success=False,
        )
        jobs_by_attempt.setdefault(attempt, []).append(job)

    if any(len(jobs) != 1 for jobs in jobs_by_attempt.values()):
        raise ProviderFailure("producer_job_unresolved")
    effective_job = selected_history_job
    effective_attempt = selected_attempt
    if effective_attempt > 1:
        current_start, current_end = _job_execution_times(effective_job)
        while effective_attempt > 1:
            previous_attempt = effective_attempt - 1
            previous_jobs = jobs_by_attempt.get(previous_attempt, [])
            if len(previous_jobs) != 1:
                raise ProviderFailure("producer_job_unresolved")
            previous_job = previous_jobs[0]
            previous_start, previous_end = _job_execution_times(previous_job)
            if previous_start == current_start and previous_end == current_end:
                if previous_job.get("conclusion") != "success":
                    raise ProviderFailure("producer_job_unresolved")
                effective_job = previous_job
                effective_attempt = previous_attempt
                current_start, current_end = previous_start, previous_end
                continue
            if previous_end > current_start:
                raise ProviderFailure("producer_job_unresolved")
            break

    return run, run_id, effective_job
