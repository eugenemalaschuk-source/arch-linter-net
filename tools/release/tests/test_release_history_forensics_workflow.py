from __future__ import annotations

import re
from pathlib import Path

import yaml


_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_RELEASE_WORKFLOW = _REPOSITORY_ROOT / ".github" / "workflows" / "release-nuget.yml"


def _workflow() -> str:
    return _RELEASE_WORKFLOW.read_text(encoding="utf-8")


def _jobs() -> dict[str, dict]:
    return yaml.safe_load(_workflow())["jobs"]


def _job_text(workflow: str, name: str) -> str:
    start = workflow.index(f"  {name}:\n")
    remainder = workflow[start:]
    next_job = re.search(r"\n {2}[A-Za-z0-9_-]+:\n", remainder[1:])
    return remainder if next_job is None else remainder[: next_job.start() + 1]


def test_history_forensics_runs_from_prepare_candidate_and_in_parallel_with_checkpoint_b() -> None:
    jobs = _jobs()
    history = jobs["history-forensics"]
    release = jobs["release"]

    assert history["needs"] == "prepare-candidate"
    assert jobs["checkpoint-b-shards"]["needs"] == "prepare-candidate"
    assert "history-forensics" in release["needs"]
    assert "checkpoint-b-evidence" in release["needs"]


def test_history_forensics_uses_only_the_verified_candidate_package_and_identity_outputs() -> None:
    workflow = _workflow()
    history = _job_text(workflow, "history-forensics")
    runner = "\n".join(
        path.read_text(encoding="utf-8")
        for path in (
            _REPOSITORY_ROOT / "tools" / "release" / "release_forensics.py",
            _REPOSITORY_ROOT / "tools" / "release" / "release_forensics_analysis.py",
        )
    )

    assert "name: nuget-candidate-${{ needs.prepare-candidate.outputs.package_version }}" in history
    assert "needs.prepare-candidate.outputs.candidate_commit" in history
    assert "needs.prepare-candidate.outputs.candidate_tree" in history
    assert "package_manifest.py verify" in history
    assert "--manifest artifacts/candidate/package-manifest.json" in history
    assert "--source-commit" in history
    assert "release_forensics.py run" in history
    assert '_CLI_PACKAGE_ID = "ArchLinterNet.Cli"' in runner
    assert '"tool",\n            "install",\n            _CLI_PACKAGE_ID' in runner
    assert '"--source",\n            str(package_directory)' in runner
    assert '"--version",\n            str(parsed_version)' in runner
    assert 'package_directory=root / "artifacts" / "candidate"' in runner
    assert 'bundle_directory=root / "artifacts" / "release-forensics"' in runner
    assert "--bundle-directory" not in history
    assert '"history",\n        "analyze"' in runner
    assert "--report\"" in runner
    assert '"json={json_path}"' in runner
    assert '"markdown={markdown_path}"' in runner
    assert '"--timings"' in runner
    assert "--enrich-dotnet" not in runner
    assert "dotnet build" not in history
    assert "dotnet restore" not in history


def test_history_forensics_is_full_history_read_only_and_has_no_release_publisher() -> None:
    workflow = _workflow()
    history = _job_text(workflow, "history-forensics")
    jobs = _jobs()

    assert "contents: read" in history
    assert "contents: write" not in history
    assert "fetch-depth: 0" in history
    assert "persist-credentials: false" in history
    assert "gh release" not in history
    assert "actions/create-release" not in history
    assert "NuGet/login" not in history
    assert "dotnet nuget push" not in history
    assert "contents: write" in _job_text(workflow, "create-release")
    publisher = (_REPOSITORY_ROOT / "tools" / "release" / "publish_release_assets.py").read_text(encoding="utf-8")
    assert '"release",\n            "create"' in publisher
    assert '["release", "upload"' in publisher
    assert '"release",\n            "download"' in publisher
    assert "history-forensics" not in jobs["create-release"].get("needs", [])


def test_history_forensics_uploads_complete_bundle_and_release_attaches_it() -> None:
    workflow = _workflow()
    history = _job_text(workflow, "history-forensics")
    create_release = _job_text(workflow, "create-release")

    for filename in (
        "release-forensics.json",
        "release-forensics.md",
        "release-forensics-manifest.json",
        "release-forensics-checksums.txt",
        "release-forensics-observations.json",
    ):
        assert filename in history
        assert filename in create_release
    assert "release-forensics-${{ needs.prepare-candidate.outputs.package_version }}" in history
    assert "actions/upload-artifact" in history
    assert "actions/download-artifact" in create_release
    assert "publish_release_assets.py" in create_release
    assert "sha256" in (_REPOSITORY_ROOT / "tools" / "release" / "publish_release_assets.py").read_text(encoding="utf-8")
    assert "--clobber" not in create_release
    assert "--clobber" not in workflow


def test_history_forensics_failures_are_not_masked_and_release_waits_for_success() -> None:
    workflow = _workflow()
    history = _job_text(workflow, "history-forensics")
    release = _job_text(workflow, "release")
    create_release = _job_text(workflow, "create-release")

    assert "continue-on-error: true" not in history
    assert "|| true" not in history
    assert "if: always()" not in history
    assert "history-forensics" in release
    assert "if: always()" not in release
    assert "if: always()" not in create_release
