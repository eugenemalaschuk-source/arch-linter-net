from __future__ import annotations

from pathlib import Path
import shutil
import subprocess
import sys

import pytest
import yaml

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "release"))
import acquire_publisher_sources as acquisition  # noqa: E402
import release_distribution  # noqa: E402


def test_fetch_uses_only_the_approved_pair_and_repository_root(monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[tuple[list[str], dict[str, object]]] = []

    def fake_run(command: list[str], **kwargs: object) -> None:
        calls.append((command, kwargs))

    monkeypatch.setattr(acquisition.subprocess, "run", fake_run)
    acquisition.acquire(ROOT)

    assert calls == [
        (
            [
                "git", "fetch", "--no-tags", "--depth=1", "origin",
                release_distribution._APPROVED_PUBLISHER_COMMIT,
                release_distribution._APPROVED_PUBLISHER_ACTION_COMMIT,
            ],
            {"cwd": ROOT, "check": True},
        )
    ]


def test_changed_reviewed_authority_changes_the_fetched_pair(monkeypatch: pytest.MonkeyPatch) -> None:
    pair = ["a" * 40, "b" * 40]
    calls: list[list[str]] = []
    monkeypatch.setattr(release_distribution, "_APPROVED_PUBLISHER_COMMIT", pair[0])
    monkeypatch.setattr(release_distribution, "_APPROVED_PUBLISHER_ACTION_COMMIT", pair[1])
    monkeypatch.setattr(acquisition.subprocess, "run", lambda command, **_: calls.append(command))

    acquisition.acquire(ROOT)

    assert calls[0][-2:] == pair


def test_git_failure_propagates(monkeypatch: pytest.MonkeyPatch) -> None:
    def fail(*_: object, **__: object) -> None:
        raise subprocess.CalledProcessError(128, "git fetch")

    monkeypatch.setattr(acquisition.subprocess, "run", fail)
    with pytest.raises(subprocess.CalledProcessError):
        acquisition.acquire(ROOT)


def test_main_reports_failed_git_acquisition_and_returns_nonzero(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    def fail() -> None:
        raise subprocess.CalledProcessError(128, ["git", "fetch"])

    monkeypatch.setattr(acquisition, "acquire", fail)
    monkeypatch.setattr(sys, "argv", ["acquire_publisher_sources.py"])

    assert acquisition.main() == 1
    captured = capsys.readouterr()
    assert "publisher source acquisition failed:" in captured.err
    assert "returned non-zero exit status 128" in captured.err


def test_real_shallow_fetch_acquires_exact_objects(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    git = shutil.which("git")
    if git is None:
        pytest.skip("git is required")
    origin = tmp_path / "origin.git"
    source = tmp_path / "source"
    checkout = tmp_path / "checkout"
    source.mkdir()
    checkout.mkdir()
    subprocess.run([git, "init", "--bare", str(origin)], check=True, capture_output=True)
    subprocess.run([git, "init", str(source)], check=True, capture_output=True)
    subprocess.run([git, "-C", str(source), "config", "user.email", "test@example.invalid"], check=True)
    subprocess.run([git, "-C", str(source), "config", "user.name", "Test"], check=True)
    def commit(message: str, filename: str, content: str) -> str:
        (source / filename).write_text(content, encoding="utf-8")
        subprocess.run([git, "-C", str(source), "add", filename], check=True)
        subprocess.run([git, "-C", str(source), "commit", "-m", message], check=True, capture_output=True)
        return subprocess.run(
            [git, "-C", str(source), "rev-parse", "HEAD"], check=True, capture_output=True, text=True
        ).stdout.strip()

    workflow_commit = commit("approved workflow", "workflow.yml", "workflow\n")
    action_commit = commit("approved action", "action.yml", "action\n")
    tip_commit = commit("later tip", "later.txt", "tip\n")
    subprocess.run([git, "-C", str(source), "tag", "publisher-tags", workflow_commit], check=True)
    subprocess.run([git, "-C", str(source), "tag", "action-tags", action_commit], check=True)
    subprocess.run([git, "-C", str(source), "remote", "add", "origin", str(origin)], check=True)
    subprocess.run([git, "-C", str(source), "push", "origin", "HEAD", "--tags"], check=True, capture_output=True)
    subprocess.run(
        [git, "clone", "--no-local", "--depth=1", "--no-tags", str(origin), str(checkout)],
        check=True, capture_output=True,
    )
    shallow_before = (checkout / ".git" / "shallow").read_text(encoding="ascii").splitlines()
    assert shallow_before == [tip_commit]
    assert subprocess.run(
        [git, "-C", str(checkout), "for-each-ref", "--format=%(refname)", "refs/tags"],
        check=True, capture_output=True, text=True,
    ).stdout == ""
    monkeypatch.setattr(release_distribution, "_APPROVED_PUBLISHER_COMMIT", workflow_commit)
    monkeypatch.setattr(release_distribution, "_APPROVED_PUBLISHER_ACTION_COMMIT", action_commit)

    acquisition.acquire(checkout)

    for approved_commit in (workflow_commit, action_commit):
        fetched = subprocess.run(
            [git, "-C", str(checkout), "cat-file", "-e", f"{approved_commit}^{{commit}}"], capture_output=True
        )
        assert fetched.returncode == 0
    shallow = (checkout / ".git" / "shallow").read_text(encoding="ascii").splitlines()
    assert tip_commit in shallow
    assert workflow_commit in shallow
    assert action_commit in shallow
    assert subprocess.run(
        [git, "-C", str(checkout), "for-each-ref", "--format=%(refname)", "refs/tags"],
        check=True, capture_output=True, text=True,
    ).stdout == ""


def test_cli_rejects_caller_selected_values() -> None:
    result = subprocess.run(
        [sys.executable, str(ROOT / "tools" / "release" / "acquire_publisher_sources.py"), "--ref", "HEAD"],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )
    assert result.returncode != 0


def test_all_six_release_jobs_acquire_before_independent_verification() -> None:
    workflow = yaml.safe_load((ROOT / ".github" / "workflows" / "release-nuget.yml").read_text(encoding="utf-8"))
    jobs = workflow["jobs"]
    verify_jobs = (
        "checkpoint-b-shards", "checkpoint-b-platform-evidence", "checkpoint-b-evidence",
        "attest-prepublication-provenance", "release", "create-release",
    )
    for job_name in verify_jobs:
        steps = jobs[job_name]["steps"]
        acquire_indexes = [
            index for index, step in enumerate(steps)
            if step.get("run") == "python3 tools/release/acquire_publisher_sources.py"
        ]
        verify_indexes = [
            index for index, step in enumerate(steps)
            if "release_distribution.py verify" in step.get("run", "")
        ]
        assert len(acquire_indexes) == len(verify_indexes) == 1, job_name
        assert acquire_indexes[0] < verify_indexes[0], job_name
        assert "git fetch --no-tags --depth=1 origin" not in "\n".join(
            step.get("run", "") for step in steps
        ), job_name
