from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import main_quality_coverage as coverage  # noqa: E402


_SHA = "a" * 40
_OTHER_SHA = "b" * 40


@pytest.fixture(autouse=True)
def _release_workspace(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)


def _xml(report_format: str, marker: str = "same") -> str:
    if report_format == "opencover":
        return f"<?xml version='1.0'?><CoverageSession><!--{marker}--><Summary /></CoverageSession>"
    return f"<?xml version='1.0'?><coverage><!--{marker}--><packages /></coverage>"


def _write_raw_pair(root: Path, report_format: str, marker: str = "same") -> None:
    filename = coverage._REPORT_FORMATS[report_format][0]
    for relative in (Path("11111111-1111-1111-1111-111111111111") / filename, Path("In") / "runner" / filename):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(_xml(report_format, marker), encoding="utf-8")


def _raw_shard(tmp_path: Path, shard_id: str) -> Path:
    root = tmp_path / "raw" / shard_id
    producers = coverage._SHARDS[shard_id]
    for producer in producers:
        producer_root = root / producer.relative_root
        for report_format in coverage._REPORT_FORMATS:
            _write_raw_pair(producer_root, report_format, f"{shard_id}-{producer.id}-{report_format}")
    return root


def _canonicalize(tmp_path: Path, shard_id: str, source_sha: str = _SHA) -> Path:
    raw = _raw_shard(tmp_path, shard_id)
    output = tmp_path / "shards" / shard_id
    coverage._canonicalize_shard(
        argparse.Namespace(
            shard=shard_id,
            source_sha=source_sha,
            coverage_root=raw,
            output_root=output,
        )
    )
    return output / shard_id


def _artifact_root(tmp_path: Path, source_sha: str = _SHA, shards: tuple[str, ...] = coverage._SHARD_IDS) -> Path:
    artifacts = tmp_path / "artifacts"
    for shard_id in shards:
        canonical = _canonicalize(tmp_path / shard_id, shard_id, source_sha)
        destination = artifacts / f"main-dotnet-coverage-{shard_id}" / shard_id
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(canonical, destination)
    return artifacts


def _assemble(tmp_path: Path, artifacts: Path, expected_sha: str = _SHA) -> Path:
    output = tmp_path / "canonical"
    github_output = tmp_path / "github-output.txt"
    coverage._assemble(
        argparse.Namespace(
            artifacts_root=artifacts,
            expected_sha=expected_sha,
            output_root=output,
            github_output=github_output,
        )
    )
    return output


def test_duplicate_collector_layout_is_canonicalized_once_per_producer_and_format(tmp_path: Path) -> None:
    shard = _canonicalize(tmp_path, "core-1")
    manifest = json.loads((shard / "shard-manifest.json").read_text(encoding="utf-8"))

    assert len(manifest["reports"]) == 2
    assert {record["candidate_count"] for record in manifest["reports"]} == {2}
    assert sorted(path.relative_to(shard).as_posix() for path in shard.rglob("coverage.*.xml")) == [
        "core/coverage.cobertura.xml",
        "core/coverage.opencover.xml",
    ]


def test_canonicalization_rejects_ambiguous_duplicate_content(tmp_path: Path) -> None:
    raw = _raw_shard(tmp_path, "core-1")
    duplicate = raw / "In" / "runner" / "coverage.opencover.xml"
    duplicate.write_text(_xml("opencover", "different"), encoding="utf-8")

    with pytest.raises(ValueError, match="Ambiguous opencover coverage evidence"):
        coverage._canonicalize_shard(
            argparse.Namespace(
                shard="core-1",
                source_sha=_SHA,
                coverage_root=raw,
                output_root=tmp_path / "output",
            )
        )


@pytest.mark.parametrize("content", ["", "<CoverageSession>"])
def test_canonicalization_rejects_empty_or_corrupt_report(tmp_path: Path, content: str) -> None:
    raw = _raw_shard(tmp_path, "core-2")
    target = raw / "11111111-1111-1111-1111-111111111111" / "coverage.opencover.xml"
    duplicate = raw / "In" / "runner" / "coverage.opencover.xml"
    target.write_text(content, encoding="utf-8")
    duplicate.write_text(content, encoding="utf-8")

    with pytest.raises(ValueError, match="missing or empty|not parseable XML"):
        coverage._canonicalize_shard(
            argparse.Namespace(
                shard="core-2",
                source_sha=_SHA,
                coverage_root=raw,
                output_root=tmp_path / "output",
            )
        )


def test_assemble_fails_closed_when_any_required_shard_is_missing(tmp_path: Path) -> None:
    artifacts = _artifact_root(tmp_path, shards=("core-1", "other"))

    with pytest.raises(ValueError, match=r"observed=2/3.*core-2"):
        _assemble(tmp_path, artifacts)


def test_assemble_rejects_stale_or_wrong_sha(tmp_path: Path) -> None:
    artifacts = _artifact_root(tmp_path, source_sha=_OTHER_SHA)

    with pytest.raises(ValueError, match="stale/wrong"):
        _assemble(tmp_path, artifacts, expected_sha=_SHA)


def test_complete_three_shard_inventory_has_four_reports_per_format_and_current_sha(tmp_path: Path) -> None:
    artifacts = _artifact_root(tmp_path)
    output = _assemble(tmp_path, artifacts)
    inventory = json.loads((output / "coverage-inventory.json").read_text(encoding="utf-8"))

    assert inventory["source_sha"] == _SHA
    assert inventory["expected_shards"] == ["core-1", "core-2", "other"]
    assert inventory["observed_shards"] == ["core-1", "core-2", "other"]
    assert len([record for record in inventory["reports"] if record["format"] == "opencover"]) == 4
    assert len([record for record in inventory["reports"] if record["format"] == "cobertura"]) == 4
    assert [record["test_project"] for record in inventory["reports"] if record["format"] == "opencover"] == [
        "ArchLinterNet.Core.Tests",
        "ArchLinterNet.Core.Tests",
        "ArchLinterNet.CEL.Tests",
        "ArchLinterNet.Cli.Tests",
    ]


def test_sonar_verification_requires_all_canonical_reports_and_current_revision(tmp_path: Path) -> None:
    artifacts = _artifact_root(tmp_path)
    output = _assemble(tmp_path, artifacts)
    inventory = json.loads((output / "coverage-inventory.json").read_text(encoding="utf-8"))
    reports = [
        (output / record["path"]).as_posix()
        for record in inventory["reports"]
        if record["format"] == "opencover"
    ]
    log = tmp_path / "sonar.log"
    log.write_text(
        "\n".join(
            [f"INFO: Parsing the OpenCover report {Path(report).resolve()}" for report in reports]
            + ["INFO: Coverage Report Statistics: 42 files, 40 main files, 39 main files with coverage, 2 test files, 0 project excluded files, 0 other language files."]
        ),
        encoding="utf-8",
    )
    analysis_json = tmp_path / "analysis.json"
    analysis_json.write_text(json.dumps({"analyses": [{"revision": _SHA}]}), encoding="utf-8")
    github_output = tmp_path / "sonar-output.txt"

    coverage._verify_sonar(
        argparse.Namespace(
            inventory_root=output,
            expected_sha=_SHA,
            scanner_log=log,
            analysis_json=analysis_json,
            github_output=github_output,
        )
    )

    rendered = github_output.read_text(encoding="utf-8")
    assert f"analysis_revision={_SHA}" in rendered
    assert "coverage_import_status=4/4 canonical OpenCover reports parsed" in rendered
    assert "covered_main_files=39" in rendered


def test_cli_verify_inventory_round_trip(tmp_path: Path, monkeypatch) -> None:
    artifacts = _artifact_root(tmp_path)
    output = _assemble(tmp_path, artifacts)
    github_output = tmp_path / "cli-output.txt"
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "main_quality_coverage.py",
            "verify-inventory",
            "--inventory-root",
            str(output),
            "--expected-sha",
            _SHA,
            "--github-output",
            str(github_output),
        ],
    )

    assert coverage.main() == 0
    rendered = github_output.read_text(encoding="utf-8")
    assert "shard_count=3" in rendered
    assert "opencover_count=4" in rendered
    assert "cobertura_count=4" in rendered


def _sonar_log_for(output: Path, inventory: dict[str, object], covered_main_files: int) -> str:
    reports = [
        (output / record["path"]).resolve()
        for record in inventory["reports"]  # type: ignore[index]
        if record["format"] == "opencover"  # type: ignore[index]
    ]
    return "\n".join(
        [f"INFO: Parsing the OpenCover report {report}" for report in reports]
        + [
            "INFO: Coverage Report Statistics: 42 files, 40 main files, "
            f"{covered_main_files} main files with coverage, 2 test files, "
            "0 project excluded files, 0 other language files."
        ]
    )


def test_sonar_verification_rejects_stale_analysis_revision(tmp_path: Path) -> None:
    output = _assemble(tmp_path, _artifact_root(tmp_path))
    inventory = json.loads((output / "coverage-inventory.json").read_text(encoding="utf-8"))
    log = tmp_path / "sonar.log"
    log.write_text(_sonar_log_for(output, inventory, 39), encoding="utf-8")
    analysis_json = tmp_path / "analysis.json"
    analysis_json.write_text(json.dumps({"analyses": [{"revision": _OTHER_SHA}]}), encoding="utf-8")

    with pytest.raises(ValueError, match="Sonar analysis revision is stale/wrong"):
        coverage._verify_sonar(
            argparse.Namespace(
                inventory_root=output,
                expected_sha=_SHA,
                scanner_log=log,
                analysis_json=analysis_json,
                github_output=None,
            )
        )


def test_sonar_verification_rejects_zero_imported_main_coverage(tmp_path: Path) -> None:
    output = _assemble(tmp_path, _artifact_root(tmp_path))
    inventory = json.loads((output / "coverage-inventory.json").read_text(encoding="utf-8"))
    log = tmp_path / "sonar.log"
    log.write_text(_sonar_log_for(output, inventory, 0), encoding="utf-8")
    analysis_json = tmp_path / "analysis.json"
    analysis_json.write_text(json.dumps({"analyses": [{"revision": _SHA}]}), encoding="utf-8")

    with pytest.raises(ValueError, match="did not report any covered main .NET files"):
        coverage._verify_sonar(
            argparse.Namespace(
                inventory_root=output,
                expected_sha=_SHA,
                scanner_log=log,
                analysis_json=analysis_json,
                github_output=None,
            )
        )
