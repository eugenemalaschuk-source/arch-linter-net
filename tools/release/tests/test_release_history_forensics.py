from __future__ import annotations

import json
import shutil
import subprocess
import sys
from argparse import Namespace
from pathlib import Path

import pytest


_RELEASE_DIRECTORY = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_RELEASE_DIRECTORY))

import package_manifest  # noqa: E402
import release_forensics_analysis  # noqa: E402
import release_forensics  # noqa: E402
import release_forensics_transport  # noqa: E402
from resolve_release_history_range import (  # noqa: E402
    ReleaseHistoryRange,
    ReleaseHistoryRangeError,
    resolve_release_history_range,
)


def _git(repository: Path, *arguments: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", "-C", str(repository), *arguments],
        check=check,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def _repository(tmp_path: Path) -> Path:
    repository = tmp_path / "repo"
    repository.mkdir()
    _git(repository, "init", "--quiet", "--initial-branch=main")
    _git(repository, "config", "user.name", "Release Test")
    _git(repository, "config", "user.email", "release@example.invalid")
    _git(repository, "config", "gc.auto", "0")
    return repository


def _commit(repository: Path, index: int) -> str:
    (repository / "history.txt").write_text(f"commit {index}\n", encoding="utf-8")
    _git(repository, "add", "history.txt")
    _git(repository, "commit", "--quiet", "-m", f"history change #{index}")
    return _git(repository, "rev-parse", "HEAD")


def _tag(repository: Path, name: str, *, annotated: bool = False) -> None:
    arguments = ["tag"]
    if annotated:
        arguments.extend(["-a", "-m", f"Release {name}"])
    arguments.append(name)
    _git(repository, *arguments)


def _candidate(repository: Path) -> tuple[str, str]:
    commit = _git(repository, "rev-parse", "HEAD")
    tree = _git(repository, "rev-parse", f"{commit}^{{tree}}")
    return commit, tree


def _resolve(repository: Path, version: str, target_tag: str) -> object:
    candidate_sha, candidate_tree = _candidate(repository)
    return resolve_release_history_range(
        repository, version, target_tag, candidate_sha, candidate_tree
    )


def test_stable_candidate_uses_highest_lower_stable_ancestor_and_ignores_previews(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.7.0")
    _commit(repository, 2)
    _tag(repository, "v0.8.0", annotated=True)
    _commit(repository, 3)
    _tag(repository, "v0.9.0-preview.1")
    _tag(repository, "0.8.9-main.17")
    _commit(repository, 4)

    selected = _resolve(repository, "0.9.0", "v0.9.0")

    assert selected.status == "applicable"
    assert selected.base_tag == "v0.8.0"
    assert selected.base_sha == _git(repository, "rev-parse", "v0.8.0^{commit}")
    assert selected.series_identity == "stable"


def test_preview_candidate_uses_highest_lower_preview_in_same_series(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.8.0")
    _commit(repository, 2)
    _tag(repository, "v0.9.0-preview.1")
    _commit(repository, 3)
    _tag(repository, "v0.9.0-preview.2")
    _commit(repository, 4)

    selected = _resolve(repository, "0.9.0-preview.3", "v0.9.0-preview.3")

    assert selected.base_tag == "v0.9.0-preview.2"
    assert selected.series_kind == "preview"
    assert selected.series_identity == "preview:0.9.0"


def test_first_preview_uses_highest_lower_stable_ancestor(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.7.0")
    _commit(repository, 2)
    _tag(repository, "v0.8.0")
    _commit(repository, 3)

    selected = _resolve(repository, "0.9.0-preview.1", "v0.9.0-preview.1")

    assert selected.base_tag == "v0.8.0"
    assert selected.series_identity == "preview:0.9.0"


def test_candidate_override_prereleases_and_build_metadata_use_nuget_semver(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.0.9")
    _commit(repository, 2)
    _tag(repository, "v0.1.0-alpha.1")
    _commit(repository, 3)
    _tag(repository, "v0.1.0-beta.2")
    _commit(repository, 4)
    _tag(repository, "v0.1.0-beta.10")
    _commit(repository, 5)

    alpha = _resolve(repository, "0.1.0-alpha.2", "v0.1.0-alpha.2")
    release_candidate = _resolve(repository, "0.1.0-rc.1", "v0.1.0-rc.1")
    stable_with_metadata = _resolve(repository, "0.1.0+build.123", "v0.1.0+build.123")

    assert alpha.base_tag == "v0.1.0-alpha.1"
    assert release_candidate.base_tag == "v0.1.0-beta.10"
    assert release_candidate.series_kind == "preview"
    assert release_candidate.series_identity == "preview:0.1.0"
    assert stable_with_metadata.candidate_version == "0.1.0+build.123"
    assert stable_with_metadata.base_tag == "v0.0.9"


def test_no_predecessor_is_typed_not_applicable_without_a_fake_range(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "0.1.0-main.3")

    selected = _resolve(repository, "0.1.0", "v0.1.0")

    assert selected.status == "not_applicable"
    assert selected.reason == "no_previous_release"
    assert selected.base_tag is None
    assert selected.base_sha is None


def test_duplicate_semver_tags_on_candidate_ancestry_fail_as_ambiguous(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.8.0")
    _tag(repository, "0.8.0")
    _commit(repository, 2)

    with pytest.raises(ReleaseHistoryRangeError, match="Ambiguous release tags"):
        _resolve(repository, "0.9.0", "v0.9.0")


def test_build_metadata_tags_with_equal_precedence_fail_as_ambiguous(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.9.0-rc.1+build.1")
    _commit(repository, 2)
    _tag(repository, "0.9.0-rc.1+build.2")
    candidate_sha = _commit(repository, 3)
    candidate_tree = _git(repository, "rev-parse", "HEAD^{tree}")

    with pytest.raises(ReleaseHistoryRangeError, match="equivalent SemVer precedence"):
        resolve_release_history_range(
            repository, "0.9.0-rc.2", "v0.9.0-rc.2", candidate_sha, candidate_tree
        )


def test_candidate_build_metadata_cannot_alias_an_existing_precedence(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    _tag(repository, "v0.9.0-rc.1+build.1")
    candidate_sha = _commit(repository, 2)
    candidate_tree = _git(repository, "rev-parse", "HEAD^{tree}")

    with pytest.raises(ReleaseHistoryRangeError, match="equivalent SemVer precedence"):
        resolve_release_history_range(
            repository,
            "0.9.0-rc.1+build.2",
            "v0.9.0-rc.1+build.2",
            candidate_sha,
            candidate_tree,
        )


def test_shallow_history_fails_closed(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    candidate_sha, candidate_tree = _candidate(repository)
    (repository / ".git" / "shallow").write_text(f"{candidate_sha}\n", encoding="ascii")

    with pytest.raises(ReleaseHistoryRangeError, match="non-shallow"):
        resolve_release_history_range(
            repository, "0.1.0", "v0.1.0", candidate_sha, candidate_tree
        )


def test_missing_predecessor_object_fails_closed(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    predecessor = _commit(repository, 1)
    _tag(repository, "v0.8.0")
    _commit(repository, 2)
    candidate_sha, candidate_tree = _candidate(repository)
    object_path = repository / ".git" / "objects" / predecessor[:2] / predecessor[2:]
    object_path.unlink()

    with pytest.raises(ReleaseHistoryRangeError, match="Git command failed"):
        resolve_release_history_range(
            repository, "0.9.0", "v0.9.0", candidate_sha, candidate_tree
        )


def test_existing_candidate_tag_must_point_to_exact_candidate(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    old_commit = _commit(repository, 1)
    _tag(repository, "v0.9.0")
    candidate_sha = _commit(repository, 2)
    candidate_tree = _git(repository, "rev-parse", "HEAD^{tree}")
    assert old_commit != candidate_sha

    with pytest.raises(ReleaseHistoryRangeError, match="does not point to the candidate"):
        resolve_release_history_range(
            repository, "0.9.0", "v0.9.0", candidate_sha, candidate_tree
        )


def test_candidate_tree_mismatch_fails_closed(tmp_path: Path) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    candidate_sha, _ = _candidate(repository)
    wrong_tree = "0" * 40

    with pytest.raises(ReleaseHistoryRangeError, match="tree does not match"):
        resolve_release_history_range(
            repository, "0.1.0", "v0.1.0", candidate_sha, wrong_tree
        )


def test_candidate_package_verification_binds_every_manifested_subject(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    package_directory = tmp_path / "packages"
    package_directory.mkdir()
    monkeypatch.chdir(package_directory)
    for package_id in package_manifest._PACKAGE_IDS:  # noqa: SLF001
        for extension in (".nupkg", ".snupkg"):
            (package_directory / f"{package_id}.0.9.0{extension}").write_bytes(
                f"{package_id}{extension}".encode("ascii")
            )
    package_manifest._create(  # noqa: SLF001
        Namespace(
            packages_dir=package_directory,
            version="0.9.0",
            source_commit="a" * 40,
            output=package_directory / "package-manifest.json",
        )
    )

    _, digest = release_forensics._verify_candidate_package(  # noqa: SLF001
        package_directory, "0.9.0", "a" * 40
    )
    assert len(digest) == 64
    (package_directory / "ArchLinterNet.Cli.0.9.0.nupkg").write_bytes(b"tampered")
    with pytest.raises(ValueError, match="digest mismatch"):
        release_forensics._verify_candidate_package(  # noqa: SLF001
            package_directory, "0.9.0", "a" * 40
        )


def test_candidate_tool_install_uses_only_a_canonical_package_version(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repository = tmp_path / "repository"
    repository.mkdir()
    package_directory = tmp_path / "packages"
    package_directory.mkdir()
    tool_directory = tmp_path / "tool"
    monkeypatch.setattr(shutil, "which", lambda _name: "/usr/bin/dotnet")
    recorded: list[str] = []

    def install(arguments: list[str], _cwd: Path) -> subprocess.CompletedProcess[str]:
        recorded.extend(arguments)
        command = tool_directory / "arch-linter-net"
        command.write_text("#!/bin/sh\n", encoding="utf-8")
        command.chmod(0o755)
        return subprocess.CompletedProcess(arguments, 0, "", "")

    monkeypatch.setattr(release_forensics, "_run_checked", install)
    command = release_forensics._install_candidate_tool(  # noqa: SLF001
        package_directory, tool_directory, "0.9.0-preview.2", repository
    )

    assert command == tool_directory / "arch-linter-net"
    assert recorded[recorded.index("--version") + 1] == "0.9.0-preview.2"
    with pytest.raises(release_forensics.ReleaseForensicsError, match="canonical release version"):
        release_forensics._install_candidate_tool(  # noqa: SLF001
            package_directory, tool_directory, "--help", repository
        )


def test_candidate_tool_install_reports_a_missing_dotnet_sdk(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(shutil, "which", lambda _name: None)

    with pytest.raises(release_forensics.ReleaseForensicsError, match=".NET SDK is not available"):
        release_forensics._install_candidate_tool(  # noqa: SLF001
            tmp_path, tmp_path / "tool", "0.9.0", tmp_path
        )


def test_bundle_output_paths_are_restricted_to_the_fixed_inventory(tmp_path: Path) -> None:
    bundle = tmp_path / "bundle"
    bundle.mkdir()

    with pytest.raises(release_forensics.ReleaseForensicsError, match="fixed bundle inventory"):
        release_forensics._bundle_file(bundle, "../outside.json")  # noqa: SLF001

    outside = tmp_path / "outside.json"
    outside.write_text("preserve\n", encoding="utf-8")
    (bundle / "release-forensics.json").symlink_to(outside)
    with pytest.raises(release_forensics.ReleaseForensicsError, match="escaped the bundle directory"):
        release_forensics._bundle_file(bundle, "release-forensics.json")  # noqa: SLF001
    assert outside.read_text(encoding="utf-8") == "preserve\n"


def test_checked_subprocess_reports_its_failure_details(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(
        release_forensics.subprocess,
        "run",
        lambda *_args, **_kwargs: subprocess.CompletedProcess(["dotnet"], 2, "", "restore failed"),
    )

    with pytest.raises(release_forensics.ReleaseForensicsError, match="restore failed"):
        release_forensics._run_checked(["dotnet", "restore"], tmp_path)  # noqa: SLF001


def test_analysis_requires_gnu_time_for_isolated_memory_measurement(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    original_is_file = Path.is_file
    monkeypatch.setattr(
        Path,
        "is_file",
        lambda path: False if path == Path("/usr/bin/time") else original_is_file(path),
    )

    with pytest.raises(release_forensics_analysis.ReleaseForensicsError, match="GNU time is required"):
        release_forensics_analysis._run_with_peak_memory(["candidate"], tmp_path)  # noqa: SLF001


@pytest.mark.parametrize(
    ("record", "message"),
    [
        ("", "did not emit its required"),
        ("History timings (ms): malformed", "malformed timing data"),
        ("History timings (ms): ingestion_calls=nope", "invalid ingestion count"),
        ("History timings (ms): ingestion_calls=-1", "negative ingestion count"),
        ("History timings (ms): ingestion_calls=1; ingestion=nan", "invalid phase duration"),
        ("History timings (ms): ingestion_calls=2", "exactly one history ingestion"),
        (
            "History timings (ms): ingestion_calls=1; ingestion=1; scoring=2; policy=3; json_render=4; markdown_render=5; output=6; enrichment=0",
            "enrichment as n/a",
        ),
        (
            "History timings (ms): ingestion_calls=1; ingestion=1; scoring=2; policy=3; json_render=4; markdown_render=5; enrichment=n/a",
            "missing output",
        ),
    ],
)
def test_timing_parser_rejects_invalid_or_incomplete_candidate_records(record: str, message: str) -> None:
    with pytest.raises(release_forensics_analysis.ReleaseForensicsError, match=message):
        release_forensics_analysis._parse_timings(record)  # noqa: SLF001


@pytest.mark.parametrize(
    ("contents", "message"),
    [
        (b"not json", "valid UTF-8 JSON"),
        (b"\xff", "valid UTF-8 JSON"),
        (b"[]", "root must be a JSON object"),
    ],
)
def test_analysis_report_reader_rejects_invalid_json_roots(
    tmp_path: Path, contents: bytes, message: str
) -> None:
    json_path = tmp_path / "report.json"
    markdown_path = tmp_path / "report.md"
    json_path.write_bytes(contents)
    markdown_path.write_text("# report\n", encoding="utf-8")

    with pytest.raises(release_forensics_analysis.ReleaseForensicsError, match=message):
        release_forensics_analysis._read_report(json_path, markdown_path)  # noqa: SLF001


@pytest.mark.parametrize(
    ("report", "message"),
    [
        ({}, "valid analysis object"),
        ({"analysis": {}}, "valid resolved range"),
        (
            {
                "schemaVersion": 2,
                "kind": "release-architecture-forensics",
                "toolVersion": "0.9.0",
                "historySemanticsVersion": "v1",
                "analysis": {"range": {}, "analyzedCommitCount": 0, "historyAnalysisConfiguration": {}},
            },
            "schema or kind",
        ),
        (
            {
                "schemaVersion": 1,
                "kind": "release-architecture-forensics",
                "toolVersion": "0.9.0",
                "historySemanticsVersion": "v1",
                "analysis": {
                    "range": {"resolvedFrom": "d" * 40, "resolvedTo": "a" * 40},
                    "analyzedCommitCount": True,
                    "historyAnalysisConfiguration": {},
                },
            },
            "valid analyzed commit count",
        ),
    ],
)
def test_analysis_report_validator_rejects_invalid_candidate_evidence(
    report: dict, message: str
) -> None:
    history_range = ReleaseHistoryRange(
        status="applicable",
        reason=None,
        candidate_version="0.9.0",
        target_tag="v0.9.0",
        candidate_sha="a" * 40,
        candidate_tree="b" * 40,
        series_kind="stable",
        series_identity="stable",
        base_tag="v0.8.0",
        base_sha="d" * 40,
    )

    with pytest.raises(release_forensics_analysis.ReleaseForensicsError, match=message):
        release_forensics_analysis._validate_report(report, history_range)  # noqa: SLF001


def _fake_tool(path: Path, expected_from: str, expected_to: str, calls_path: Path) -> Path:
    script = path / "arch-linter-net"
    script.parent.mkdir(parents=True, exist_ok=True)
    script.write_text(
        "#!/usr/bin/env python3\n"
        "import json, pathlib, sys\n"
        "args = sys.argv[1:]\n"
        f"open({str(calls_path)!r}, 'a', encoding='utf-8').write('called\\n')\n"
        "from_ref = args[args.index('--from') + 1]\n"
        "to_ref = args[args.index('--to') + 1]\n"
        f"assert from_ref == {expected_from!r} and to_ref == {expected_to!r}\n"
        "assert '--enrich-dotnet' not in args\n"
        "configuration = {'builtInExtractors': ['issue'], 'ignore': [], 'paths': {}, 'weights': {}}\n"
        "report = {'schemaVersion': 1, 'kind': 'release-architecture-forensics', 'historySemanticsVersion': 'v1', 'toolVersion': '0.9.0', 'analysis': {'range': {'resolvedFrom': from_ref, 'resolvedTo': to_ref}, 'analyzedCommitCount': 2, 'historyAnalysisConfiguration': configuration}}\n"
        "for item in args:\n"
        "    if item.startswith('json='):\n"
        "        pathlib.Path(item[5:]).write_text(json.dumps(report, indent=2) + '\\n', encoding='utf-8')\n"
        "    elif item.startswith('markdown='):\n"
        "        pathlib.Path(item[9:]).write_text('# Canonical report\\n', encoding='utf-8')\n"
        "print('History timings (ms): ingestion=1.000; scoring=2.000; policy=0.500; enrichment=n/a; json_render=0.300; markdown_render=0.400; output=0.500; ingestion_calls=1', file=sys.stderr)\n",
        encoding="utf-8",
    )
    script.chmod(0o755)
    return script


def test_bundle_runs_one_candidate_cli_analysis_and_keeps_timings_out_of_report(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repository = _repository(tmp_path)
    _commit(repository, 1)
    base_sha = _git(repository, "rev-parse", "HEAD")
    _tag(repository, "v0.8.0")
    candidate_sha = _commit(repository, 2)
    candidate_tree = _git(repository, "rev-parse", "HEAD^{tree}")
    policy = repository / "architecture" / "dependencies.arch.yml"
    policy.parent.mkdir()
    policy.write_text("history_analysis: {}\n", encoding="utf-8")
    package_directory = tmp_path / "candidate"
    package_directory.mkdir()
    cli_package = package_directory / "ArchLinterNet.Cli.0.9.0.nupkg"
    cli_package.write_bytes(b"verified cli package")
    bundle_directory = tmp_path / "bundle"
    calls_path = tmp_path / "calls.txt"
    fake_command = _fake_tool(tmp_path / "tool", _git(repository, "rev-parse", "v0.8.0^{commit}"), candidate_sha, calls_path)

    monkeypatch.setattr(
        release_forensics,
        "_verify_candidate_package",
        lambda *_: ({}, release_forensics._sha256_file(cli_package)),  # noqa: SLF001
    )
    monkeypatch.setattr(release_forensics, "_install_candidate_tool", lambda *_: fake_command)

    class _Clock:
        current = 0.0

        def perf_counter(self) -> float:
            value = self.current
            self.current += 0.001
            return value

        def advance(self, seconds: float) -> None:
            self.current += seconds

    clock = _Clock()
    original_analyze = release_forensics.analyze

    def analyze_with_separate_process_wall(*args: object) -> tuple[dict, dict]:
        result = original_analyze(*args)
        clock.advance(0.75)
        return result

    monkeypatch.setattr(release_forensics, "time", clock)
    monkeypatch.setattr(release_forensics, "analyze", analyze_with_separate_process_wall)

    def run_without_platform_timer(arguments: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(arguments, cwd=cwd, text=True, capture_output=True, check=False)
        return subprocess.CompletedProcess(
            arguments,
            result.returncode,
            result.stdout,
            f"{result.stderr}release_forensics_peak_rss_kb=4096\n",
        )

    monkeypatch.setattr(release_forensics_analysis, "_run_with_peak_memory", run_without_platform_timer)
    arguments = Namespace(
        candidate_version="0.9.0",
        target_tag="v0.9.0",
        candidate_sha=candidate_sha,
        candidate_tree=candidate_tree,
        repository_name="example/project",
        run_id="12345",
        run_attempt="1",
    )
    paths = release_forensics.ReleaseForensicsPaths(
        repository=repository,
        package_directory=package_directory,
        tool_directory=tmp_path / "unused-tool-path",
        bundle_directory=bundle_directory,
        policy_path=policy,
    )

    release_forensics.generate_bundle(arguments, paths)

    assert calls_path.read_text(encoding="utf-8").splitlines() == ["called"]
    report_bytes = (bundle_directory / "release-forensics.json").read_bytes()
    report = json.loads(report_bytes)
    assert report["analysis"]["range"] == {"resolvedFrom": base_sha, "resolvedTo": candidate_sha}
    manifest = json.loads((bundle_directory / "release-forensics-manifest.json").read_text(encoding="utf-8"))
    assert manifest["tool"]["package_sha256"] == release_forensics._sha256_file(cli_package)  # noqa: SLF001
    assert manifest["content"]["json"]["sha256"] == release_forensics._sha256_bytes(report_bytes)  # noqa: SLF001
    observations = json.loads((bundle_directory / "release-forensics-observations.json").read_text(encoding="utf-8"))
    assert observations["analyzer"]["phase_durations_ms"]["ingestion_calls"] == 1
    assert observations["analyzer"]["phase_durations_ms"]["json_render"] == pytest.approx(0.3)
    assert observations["analyzer"]["phase_durations_ms"]["enrichment"] == "n/a"
    assert observations["analyzer"]["process_wall_ms"] is not None
    assert observations["orchestration_ms"]["bundle_render_ms"] == pytest.approx(1.0)
    assert "analysis_process_wall_ms" not in observations["orchestration_ms"]
    assert "History timings" not in report_bytes.decode("utf-8")
    release_forensics._verify_bundle(  # noqa: SLF001
        bundle_directory, "example/project", "0.9.0", "v0.9.0", candidate_sha, candidate_tree
    )


@pytest.mark.parametrize(
    ("candidate_version", "kind", "series_identity"),
    [
        ("0.2.0-rc.1", "preview", "preview:0.2.0"),
        ("0.1.0+build.123", "stable", "stable"),
    ],
)
def test_bundle_verifier_uses_the_candidate_nuget_semver_series(
    candidate_version: str, kind: str, series_identity: str
) -> None:
    release_forensics_transport._validate_series_identity(  # noqa: SLF001
        {"version": candidate_version}, {"kind": kind, "series_identity": series_identity}
    )


def test_not_applicable_bundle_is_typed_and_does_not_run_the_cli(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    repository = _repository(tmp_path)
    candidate_sha = _commit(repository, 1)
    candidate_tree = _git(repository, "rev-parse", "HEAD^{tree}")
    policy = repository / "architecture" / "dependencies.arch.yml"
    policy.parent.mkdir()
    policy.write_text("history_analysis: {}\n", encoding="utf-8")
    package_directory = tmp_path / "candidate"
    package_directory.mkdir()
    cli_package = package_directory / "ArchLinterNet.Cli.0.1.0.nupkg"
    cli_package.write_bytes(b"verified cli package")
    bundle_directory = tmp_path / "bundle"
    monkeypatch.setattr(
        release_forensics,
        "_verify_candidate_package",
        lambda *_: ({}, release_forensics._sha256_file(cli_package)),  # noqa: SLF001
    )

    def unexpected_install(*_args: object) -> Path:
        raise AssertionError("no analysis runs without a predecessor")

    monkeypatch.setattr(
        release_forensics,
        "_install_candidate_tool",
        unexpected_install,
    )
    arguments = Namespace(
        candidate_version="0.1.0",
        target_tag="v0.1.0",
        candidate_sha=candidate_sha,
        candidate_tree=candidate_tree,
        repository_name="example/project",
        run_id="12345",
        run_attempt="1",
    )
    paths = release_forensics.ReleaseForensicsPaths(
        repository=repository,
        package_directory=package_directory,
        tool_directory=tmp_path / "unused-tool-path",
        bundle_directory=bundle_directory,
        policy_path=policy,
    )

    manifest = release_forensics.generate_bundle(arguments, paths)

    report = json.loads((bundle_directory / "release-forensics.json").read_text(encoding="utf-8"))
    assert report["status"] == "not-applicable"
    assert report["reason"] == "no_previous_release"
    assert report["analysis"] is None
    assert manifest["history"]["kind"] == "release-forensics-not-applicable/v1"

    workspace = tmp_path / "verify-workspace"
    published_bundle = workspace / "artifacts" / "release-forensics"
    published_bundle.parent.mkdir(parents=True)
    shutil.copytree(bundle_directory, published_bundle)
    monkeypatch.chdir(workspace)
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "release_forensics.py",
            "verify",
            "--repository-name",
            "example/project",
            "--candidate-version",
            "0.1.0",
            "--target-tag",
            "v0.1.0",
            "--candidate-sha",
            candidate_sha,
            "--candidate-tree",
            candidate_tree,
        ],
    )
    assert release_forensics.main() == 0


def test_bundle_verification_rejects_changed_published_report(tmp_path: Path) -> None:
    bundle = tmp_path / "bundle"
    bundle.mkdir()
    configuration = {"builtInExtractors": ["issue"], "ignore": [], "paths": {}, "weights": {}}
    report = {
        "schemaVersion": 1,
        "kind": "release-architecture-forensics",
        "historySemanticsVersion": "v1",
        "toolVersion": "0.9.0",
        "analysis": {
            "range": {"resolvedFrom": "d" * 40, "resolvedTo": "a" * 40},
            "historyAnalysisConfiguration": configuration,
        },
    }
    observations = {
        "schema": "release-forensics-operational-observations/v1",
        "candidate": {"version": "0.9.0", "sha": "a" * 40, "tree": "b" * 40},
    }
    files = {
        "release-forensics.json": release_forensics._canonical_json(report),  # noqa: SLF001
        "release-forensics.md": b"# report\n",
        "release-forensics-observations.json": release_forensics._canonical_json(observations),  # noqa: SLF001
    }
    for name, content in files.items():
        (bundle / name).write_bytes(content)
    manifest = {
        "schema": release_forensics.BUNDLE_SCHEMA,
        "status": "applicable",
        "reason": None,
        "repository": "example/project",
        "candidate": {"version": "0.9.0", "target_tag": "v0.9.0", "sha": "a" * 40, "tree": "b" * 40},
        "base": {"tag": "v0.8.0", "sha": "d" * 40},
        "range": {
            "kind": "stable",
            "series_identity": "stable",
            "semantics": "exclusive_base_inclusive_candidate",
        },
        "tool": {
            "package_id": "ArchLinterNet.Cli",
            "package_version": "0.9.0",
            "package_file": "ArchLinterNet.Cli.0.9.0.nupkg",
            "package_sha256": "c" * 64,
        },
        "history": {
            "schema_version": 1,
            "kind": "release-architecture-forensics",
            "semantics_version": "v1",
            "tool_version": "0.9.0",
        },
        "policy": {
            "input_path": "architecture/dependencies.arch.yml",
            "input_sha256": "e" * 64,
            "effective_history_configuration_sha256": release_forensics._sha256_bytes(  # noqa: SLF001
                release_forensics._canonical_json(configuration)  # noqa: SLF001
            ),
        },
        "content": {
            "json": release_forensics._file_record(bundle / "release-forensics.json"),  # noqa: SLF001
            "markdown": release_forensics._file_record(bundle / "release-forensics.md"),  # noqa: SLF001
        },
    }
    (bundle / "release-forensics-manifest.json").write_bytes(release_forensics._canonical_json(manifest))  # noqa: SLF001
    checksum_names = [*files, "release-forensics-manifest.json"]
    (bundle / "release-forensics-checksums.txt").write_text(
        "".join(
            f"{release_forensics._sha256_file(bundle / name)}  {name}\n"  # noqa: SLF001
            for name in checksum_names
        ),
        encoding="utf-8",
    )

    release_forensics._verify_bundle(bundle)  # noqa: SLF001
    (bundle / "release-forensics.json").write_text("tampered\n", encoding="utf-8")
    with pytest.raises(ValueError, match="digest mismatch"):
        release_forensics._verify_bundle(bundle)  # noqa: SLF001


@pytest.mark.parametrize("status", ["applicable", "not_applicable"])
def test_actions_summary_records_candidate_range_or_not_applicable_reason(
    tmp_path: Path, status: str
) -> None:
    summary_path = tmp_path / "summary.md"
    history_range = ReleaseHistoryRange(
        status=status,
        reason=None if status == "applicable" else "no_previous_release",
        candidate_version="0.9.0",
        target_tag="v0.9.0",
        candidate_sha="a" * 40,
        candidate_tree="b" * 40,
        series_kind="stable",
        series_identity="stable",
        base_tag="v0.8.0" if status == "applicable" else None,
        base_sha="c" * 40 if status == "applicable" else None,
    )
    report = {"analysis": {"analyzedCommitCount": 7}} if status == "applicable" else {}

    release_forensics_transport.write_summary(
        summary_path, "example/project", "12345", history_range, report
    )

    summary = summary_path.read_text(encoding="utf-8")
    assert "Candidate: `0.9.0`" in summary
    assert "open run" in summary
    if status == "applicable":
        assert "Analyzed 7 commits" in summary
        assert f"`{'c' * 40}..{'a' * 40}`" in summary
    else:
        assert "Not applicable: no previous release" in summary
        assert "No analysis range" in summary

    with pytest.raises(release_forensics_analysis.ReleaseForensicsError, match="run identity is invalid"):
        release_forensics_transport.write_summary(summary_path, "bad//repo", "12345", history_range, report)
