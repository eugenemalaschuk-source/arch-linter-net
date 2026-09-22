from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import classify_pr_traffic_mix as extractor  # noqa: E402


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        ("Directory.Build.props", "central-build-props"),
        ("Directory.Build.targets", "central-build-props"),
        ("Directory.Packages.props", "central-build-props"),
        ("NuGet.config", "central-build-props"),
        ("global.json", "central-build-props"),
        (".editorconfig", "central-build-props"),
        ("architecture/dependencies.arch.yml", "policy"),
        ("architecture/policy/public-api-and-coverage.arch.yml", "policy"),
        ("architecture/api/ArchLinterNet.Core.public-api.txt", "api-snapshot"),
        ("src/ArchLinterNet.Core/Foo.csproj", "csproj"),
        ("src/ArchLinterNet.Core/Foo.cs", "project-source"),
        ("tests/ArchLinterNet.Core.Tests/Foo.cs", "project-source"),
        ("docs/internal/notes.md", "other-non-architecture-relevant"),
        ("openspec/changes/x/proposal.md", "other-non-architecture-relevant"),
        ("README.md", "other-non-architecture-relevant"),
    ],
)
def test_classify_matches_expected_category(path: str, expected: str) -> None:
    assert extractor.classify(path) == expected


def test_classify_is_case_insensitive_and_normalizes_backslashes() -> None:
    assert extractor.classify("SRC\\ArchLinterNet.Core\\Foo.CS".replace("CS", "cs")) == "project-source"
    assert extractor.classify("DIRECTORY.BUILD.PROPS") == "central-build-props"


@pytest.mark.parametrize(
    "value",
    [
        "bb533f0f2fc0d2e30addc7a4110adda26e825e68",
        "main",
        "refs/heads/main",
        "issue-503-changed-project-advisory-analysis-evidence",
        "HEAD",
        "v1.2.3",
    ],
)
def test_validate_git_ref_accepts_safe_values(value: str) -> None:
    assert extractor.validate_git_ref(value) == value


@pytest.mark.parametrize(
    "value",
    [
        "--upload-pack=evil",
        "-oProxyCommand=evil",
        "; rm -rf /",
        "$(whoami)",
        "ref with spaces",
        "",
        "ref\nwith\nnewlines",
    ],
)
def test_validate_git_ref_rejects_option_like_or_unsafe_values(value: str) -> None:
    with pytest.raises(argparse.ArgumentTypeError):
        extractor.validate_git_ref(value)


def test_load_commits_parses_git_log_output_into_a_commit_map(monkeypatch: pytest.MonkeyPatch) -> None:
    fake_output = (
        "__COMMIT__aaa111\n"
        "src/Foo.cs\n"
        "src/Foo.csproj\n"
        "__COMMIT__bbb222\n"
        "docs/readme.md\n"
        "__COMMIT__ccc333\n"
    )

    def fake_run(cmd, capture_output, text, cwd, check):  # noqa: ARG001 - must match subprocess.run signature
        assert cmd[:3] == ["git", "log", "deadbeef"]
        assert "-n" in cmd
        return subprocess.CompletedProcess(cmd, returncode=0, stdout=fake_output, stderr="")

    monkeypatch.setattr(extractor.subprocess, "run", fake_run)

    commits = extractor.load_commits("deadbeef", 3, Path("."))

    assert list(commits.keys()) == ["aaa111", "bbb222", "ccc333"]
    assert commits["aaa111"] == ["src/Foo.cs", "src/Foo.csproj"]
    assert commits["bbb222"] == ["docs/readme.md"]
    assert commits["ccc333"] == []


def test_commit_range_returns_newest_then_oldest_by_insertion_order() -> None:
    commits = {"newest": ["a"], "middle": ["b"], "oldest": ["c"]}

    assert extractor.commit_range(commits) == ("newest", "oldest")


def test_summarize_counts_global_shaped_and_project_source_only_commits() -> None:
    commits = {
        "global-touch": ["Directory.Build.props", "src/Foo.cs"],
        "policy-touch": ["architecture/dependencies.arch.yml"],
        "source-only": ["src/Foo.cs", "tests/Foo.cs"],
        "csproj-only": ["src/Foo.csproj"],
        "docs-only": ["docs/readme.md"],
        "empty": [],
    }

    result = extractor.summarize(commits)

    assert result["total_commits_with_files"] == 5
    assert result["commits_touching_central_props_or_policy_paths"] == 2
    assert result["commits_touching_central_props_or_policy_paths_ratio"] == 0.4
    assert result["commits_touching_only_project_source_or_csproj_paths"] == 2
    assert result["commits_touching_only_project_source_or_csproj_paths_ratio"] == 0.4


def test_summarize_handles_zero_commits_without_division_by_zero() -> None:
    result = extractor.summarize({})

    assert result["total_commits_with_files"] == 0
    assert result["commits_touching_central_props_or_policy_paths_ratio"] == 0.0
    assert result["commits_touching_only_project_source_or_csproj_paths_ratio"] == 0.0


def test_main_prints_json_summary_and_returns_zero(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    fake_output = "__COMMIT__aaa111\nsrc/Foo.cs\n__COMMIT__bbb222\nDirectory.Build.props\n"

    def fake_run(cmd, capture_output, text, cwd, check):  # noqa: ARG001
        return subprocess.CompletedProcess(cmd, returncode=0, stdout=fake_output, stderr="")

    monkeypatch.setattr(extractor.subprocess, "run", fake_run)
    monkeypatch.setattr(sys, "argv", ["classify_pr_traffic_mix.py", "--end-ref", "aaa111", "--commit-count", "2"])

    exit_code = extractor.main()

    assert exit_code == 0
    captured = capsys.readouterr()
    assert '"newest_commit": "aaa111"' in captured.out
    assert '"oldest_commit": "bbb222"' in captured.out


def test_main_rejects_unsafe_end_ref_before_invoking_git(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    def fake_run(*_args, **_kwargs):
        raise AssertionError("git must not be invoked for a rejected --end-ref")

    monkeypatch.setattr(extractor.subprocess, "run", fake_run)
    # Use the "--end-ref=value" form: argparse treats a bare "--end-ref -oProxyCommand=evil" as a
    # missing argument (it looks like a second option), so "=" is required to reach the validator
    # with a value that itself starts with "-" — exactly the attacker-controlled shape being guarded
    # against.
    monkeypatch.setattr(sys, "argv", ["classify_pr_traffic_mix.py", "--end-ref=--upload-pack=evil"])

    with pytest.raises(SystemExit) as exc_info:
        extractor.main()

    assert exc_info.value.code == 2
    assert "not a safe git revision" in capsys.readouterr().err


def test_main_reports_no_commits_found(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    def fake_run(cmd, capture_output, text, cwd, check):  # noqa: ARG001
        return subprocess.CompletedProcess(cmd, returncode=0, stdout="", stderr="")

    monkeypatch.setattr(extractor.subprocess, "run", fake_run)
    monkeypatch.setattr(sys, "argv", ["classify_pr_traffic_mix.py", "--end-ref", "aaa111"])

    assert extractor.main() == 1
    assert "No commits found" in capsys.readouterr().err


def test_repository_root_points_at_the_actual_repository_root() -> None:
    root = extractor.repository_root()

    assert (root / "ArchLinterNet.slnx").is_file()
    assert (root / "tools" / "scripts" / "classify_pr_traffic_mix.py").is_file()
