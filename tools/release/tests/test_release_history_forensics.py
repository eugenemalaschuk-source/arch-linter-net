from __future__ import annotations

import json
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
from resolve_release_history_range import (  # noqa: E402
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
        repository=str(repository),
        package_directory=str(package_directory),
        tool_directory=str(tmp_path / "unused-tool-path"),
        bundle_directory=str(bundle_directory),
        policy="architecture/dependencies.arch.yml",
        candidate_version="0.9.0",
        target_tag="v0.9.0",
        candidate_sha=candidate_sha,
        candidate_tree=candidate_tree,
        repository_name="example/project",
        run_id="12345",
        run_attempt="1",
        summary_file=None,
    )

    release_forensics.generate_bundle(arguments)

    assert calls_path.read_text(encoding="utf-8").splitlines() == ["called"]
    report_bytes = (bundle_directory / "release-forensics.json").read_bytes()
    report = json.loads(report_bytes)
    assert report["analysis"]["range"] == {"resolvedFrom": base_sha, "resolvedTo": candidate_sha}
    manifest = json.loads((bundle_directory / "release-forensics-manifest.json").read_text(encoding="utf-8"))
    assert manifest["tool"]["package_sha256"] == release_forensics._sha256_file(cli_package)  # noqa: SLF001
    assert manifest["content"]["json"]["sha256"] == release_forensics._sha256_bytes(report_bytes)  # noqa: SLF001
    observations = json.loads((bundle_directory / "release-forensics-observations.json").read_text(encoding="utf-8"))
    assert observations["analyzer"]["phase_durations_ms"]["ingestion_calls"] == 1
    assert observations["analyzer"]["phase_durations_ms"]["json_render"] == 0.3
    assert observations["analyzer"]["phase_durations_ms"]["enrichment"] == "n/a"
    assert "History timings" not in report_bytes.decode("utf-8")
    release_forensics._verify_bundle(  # noqa: SLF001
        bundle_directory, "example/project", "0.9.0", "v0.9.0", candidate_sha, candidate_tree
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
    monkeypatch.setattr(
        release_forensics,
        "_install_candidate_tool",
        lambda *_: (_ for _ in ()).throw(AssertionError("no analysis runs without a predecessor")),
    )
    arguments = Namespace(
        repository=str(repository),
        package_directory=str(package_directory),
        tool_directory=str(tmp_path / "unused-tool-path"),
        bundle_directory=str(bundle_directory),
        policy="architecture/dependencies.arch.yml",
        candidate_version="0.1.0",
        target_tag="v0.1.0",
        candidate_sha=candidate_sha,
        candidate_tree=candidate_tree,
        repository_name="example/project",
        run_id="12345",
        run_attempt="1",
        summary_file=None,
    )

    manifest = release_forensics.generate_bundle(arguments)

    report = json.loads((bundle_directory / "release-forensics.json").read_text(encoding="utf-8"))
    assert report["status"] == "not-applicable"
    assert report["reason"] == "no_previous_release"
    assert report["analysis"] is None
    assert manifest["history"]["kind"] == "release-forensics-not-applicable/v1"


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
