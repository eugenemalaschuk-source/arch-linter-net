from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import main_build as main_build_module  # noqa: E402
import package_manifest as manifest  # noqa: E402
from main_build import (  # noqa: E402
    PACKAGE_IDS,
    create_retention_plan,
    create_retention_plan_from_directory,
    format_main_version,
    read_development_version,
)

_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def _write_props(path: Path, value: str = "0.8.0") -> None:
    path.write_text(
        "<Project><PropertyGroup>"
        f"<ArchLinterDevelopmentVersion>{value}</ArchLinterDevelopmentVersion>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )


def test_main_version_uses_explicit_development_version(tmp_path: Path) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props)

    assert read_development_version(props) == "0.8.0"
    assert format_main_version("0.8.0", 421) == "0.8.0-main.421"


def test_repository_main_version_authority_is_explicit_and_source_build_decoupled() -> None:
    props = _REPOSITORY_ROOT / "Directory.Build.props"
    root = ET.parse(props).getroot()

    development_version = root.findtext(".//ArchLinterDevelopmentVersion")
    source_version_prefix = root.findtext(".//VersionPrefix")

    assert development_version == "0.8.0"
    assert source_version_prefix
    assert source_version_prefix != "$(ArchLinterDevelopmentVersion)"
    assert format_main_version(read_development_version(props), 421) == "0.8.0-main.421"


def test_main_version_is_manifest_verified_as_a_complete_package_set(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.chdir(tmp_path)
    version = "0.8.0-main.421"
    source_commit = "a" * 40
    packages = tmp_path / "packages"
    packages.mkdir()

    for package_id in manifest._PACKAGE_IDS:
        for kind in manifest._SUBJECT_KINDS:
            path = packages / manifest._expected_filename(package_id, version, kind)
            path.write_bytes(f"{package_id}/{version}/{kind}".encode())

    output = packages / "package-manifest.json"
    manifest._create(
        argparse.Namespace(
            packages_dir=packages,
            version=version,
            source_commit=source_commit,
            output=output,
        )
    )
    manifest._verify(
        argparse.Namespace(
            packages_dir=packages,
            manifest=output,
            version=version,
            source_commit=source_commit,
            allow_v1=False,
        )
    )

    value = json.loads(output.read_text(encoding="utf-8"))
    assert value["version"] == version
    assert value["source_commit"] == source_commit
    assert [record["id"] for record in value["packages"]] == list(manifest._PACKAGE_IDS)


@pytest.mark.parametrize(
    ("base_version", "build_number"),
    [
        ("0.8.0-preview.1", 1),
        ("0.8", 1),
        ("v0.8.0", 1),
        ("0.8.0", 0),
        ("0.8.0", -1),
        ("0.8.0", True),
    ],
)
def test_main_version_rejects_inferred_or_non_monotonic_shapes(
    base_version: str,
    build_number: int,
) -> None:
    with pytest.raises(ValueError):
        format_main_version(base_version, build_number)


def test_development_version_rejects_unreadable_or_malformed_props(tmp_path: Path) -> None:
    missing = tmp_path / "missing.props"
    with pytest.raises(ValueError, match="Cannot read development version"):
        read_development_version(missing)

    malformed = tmp_path / "malformed.props"
    malformed.write_text("<Project>", encoding="utf-8")
    with pytest.raises(ValueError, match="Cannot read development version"):
        read_development_version(malformed)


@pytest.mark.parametrize(
    ("xml", "found"),
    [
        ("<Project><PropertyGroup /></Project>", 0),
        (
            "<Project><PropertyGroup>"
            "<ArchLinterDevelopmentVersion>0.8.0</ArchLinterDevelopmentVersion>"
            "<ArchLinterDevelopmentVersion>0.8.1</ArchLinterDevelopmentVersion>"
            "</PropertyGroup></Project>",
            2,
        ),
    ],
)
def test_development_version_requires_exactly_one_nonempty_property(
    tmp_path: Path,
    xml: str,
    found: int,
) -> None:
    props = tmp_path / "Directory.Build.props"
    props.write_text(xml, encoding="utf-8")

    with pytest.raises(ValueError, match=rf"found {found}"):
        read_development_version(props)


def test_development_version_rejects_non_stable_semver(tmp_path: Path) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props, "0.8.0-preview.1")

    with pytest.raises(ValueError, match="stable SemVer core"):
        read_development_version(props)


def _inventories(*versions: str) -> dict[str, dict[str, int]]:
    return {
        package_id: {
            version: package_index * 1000 + version_index
            for version_index, version in enumerate(versions, start=1)
        }
        for package_index, package_id in enumerate(PACKAGE_IDS, start=1)
    }


def _write_inventory_files(directory: Path, *versions: str) -> None:
    inventories = _inventories(*versions)
    directory.mkdir(parents=True, exist_ok=True)
    for package_id, package_versions in inventories.items():
        records = [
            {"id": version_id, "name": version}
            for version, version_id in package_versions.items()
        ]
        (directory / f"{package_id}.json").write_text(
            json.dumps([records]),
            encoding="utf-8",
        )


def test_inventory_flattening_accepts_paginated_slurp_shape() -> None:
    raw = [
        [{"id": 1, "name": "0.8.0-main.1"}],
        [{"id": 2, "name": "0.8.0-main.2"}],
    ]

    assert main_build_module._flatten_inventory(raw, PACKAGE_IDS[0]) == [
        {"id": 1, "name": "0.8.0-main.1"},
        {"id": 2, "name": "0.8.0-main.2"},
    ]


@pytest.mark.parametrize("raw", [{}, [["invalid"]]])
def test_inventory_flattening_rejects_invalid_json_shapes(raw: object) -> None:
    with pytest.raises(ValueError, match="JSON array|invalid record"):
        main_build_module._flatten_inventory(raw, PACKAGE_IDS[0])


@pytest.mark.parametrize(
    "record",
    [
        {"id": True, "name": "0.8.0-main.1"},
        {"id": 1, "name": 123},
        {"id": "1", "name": "0.8.0-main.1"},
    ],
)
def test_inventory_loading_rejects_invalid_id_or_name_records(
    tmp_path: Path,
    record: dict[str, object],
) -> None:
    path = tmp_path / "inventory.json"
    path.write_text(json.dumps([record]), encoding="utf-8")

    with pytest.raises(ValueError, match="invalid id/name"):
        main_build_module._load_package_inventory(path, PACKAGE_IDS[0])


def test_inventory_loading_rejects_missing_or_invalid_json(tmp_path: Path) -> None:
    missing = tmp_path / "missing.json"
    with pytest.raises(ValueError, match="Cannot read GitHub Packages inventory"):
        main_build_module._load_package_inventory(missing, PACKAGE_IDS[0])

    malformed = tmp_path / "malformed.json"
    malformed.write_text("{", encoding="utf-8")
    with pytest.raises(ValueError, match="Cannot read GitHub Packages inventory"):
        main_build_module._load_package_inventory(malformed, PACKAGE_IDS[0])


def test_retention_keeps_latest_five_complete_sets_only() -> None:
    versions = tuple(f"0.8.0-main.{number}" for number in range(1, 8))
    inventories = _inventories(*versions)
    for package_index, package_id in enumerate(PACKAGE_IDS, start=1):
        inventories[package_id]["0.7.4"] = package_index * 1000 + 90
        inventories[package_id]["0.8.0-rc.1"] = package_index * 1000 + 91

    plan = create_retention_plan(inventories, "0.8.0-main.7", keep=5)

    assert plan["retained_versions"] == [
        "0.8.0-main.7",
        "0.8.0-main.6",
        "0.8.0-main.5",
        "0.8.0-main.4",
        "0.8.0-main.3",
    ]
    assert {record["version"] for record in plan["delete"]} == {
        "0.8.0-main.1",
        "0.8.0-main.2",
    }
    assert len(plan["delete"]) == 8
    assert all(record["version"] not in {"0.7.4", "0.8.0-rc.1"} for record in plan["delete"])


def test_retention_does_not_delete_or_count_partial_main_builds() -> None:
    inventories = _inventories(
        "0.8.0-main.1",
        "0.8.0-main.2",
        "0.8.0-main.3",
        "0.8.0-main.4",
        "0.8.0-main.5",
        "0.8.0-main.6",
    )
    inventories[PACKAGE_IDS[0]]["0.8.0-main.99"] = 99999

    plan = create_retention_plan(inventories, "0.8.0-main.6", keep=5)

    assert plan["partial_versions"] == ["0.8.0-main.99"]
    assert all(record["version"] != "0.8.0-main.99" for record in plan["delete"])
    assert {record["version"] for record in plan["delete"]} == {"0.8.0-main.1"}


def test_retention_never_deletes_current_build_during_out_of_order_cleanup() -> None:
    inventories = _inventories(*(f"0.8.0-main.{number}" for number in range(1, 8)))

    plan = create_retention_plan(inventories, "0.8.0-main.1", keep=5)

    assert plan["current_retention_deferred"] is True
    assert "0.8.0-main.1" in plan["retained_versions"]
    assert all(record["version"] != "0.8.0-main.1" for record in plan["delete"])


@pytest.mark.parametrize(
    ("inventories", "current_version", "keep", "message"),
    [
        (_inventories("0.8.0-main.1"), "0.8.0-main.1", 0, "Retention count"),
        (_inventories("0.8.0-main.1"), "0.8.0", 5, "not a main build"),
        (
            {package_id: {} for package_id in PACKAGE_IDS[:-1]},
            "0.8.0-main.1",
            5,
            "inventory set mismatch",
        ),
        (
            {**_inventories("0.8.0-main.1"), "Unexpected.Package": {}},
            "0.8.0-main.1",
            5,
            "inventory set mismatch",
        ),
    ],
)
def test_retention_rejects_invalid_configuration(
    inventories: dict[str, dict[str, int]],
    current_version: str,
    keep: int,
    message: str,
) -> None:
    with pytest.raises(ValueError, match=message):
        create_retention_plan(inventories, current_version, keep)


def test_retention_requires_current_version_to_be_complete(tmp_path: Path) -> None:
    for package_id in PACKAGE_IDS:
        records = [[{"id": 100, "name": "0.8.0-main.5"}]]
        if package_id == PACKAGE_IDS[-1]:
            records = [[]]
        (tmp_path / f"{package_id}.json").write_text(json.dumps(records), encoding="utf-8")

    with pytest.raises(ValueError, match="not complete"):
        create_retention_plan_from_directory(tmp_path, "0.8.0-main.5", keep=5)


def test_retention_rejects_duplicate_package_version_records(tmp_path: Path) -> None:
    for package_id in PACKAGE_IDS:
        records = [[{"id": 100, "name": "0.8.0-main.5"}]]
        if package_id == PACKAGE_IDS[0]:
            records = [[
                {"id": 100, "name": "0.8.0-main.5"},
                {"id": 101, "name": "0.8.0-main.5"},
            ]]
        (tmp_path / f"{package_id}.json").write_text(json.dumps(records), encoding="utf-8")

    with pytest.raises(ValueError, match="duplicate version"):
        create_retention_plan_from_directory(tmp_path, "0.8.0-main.5", keep=5)


def test_version_cli_writes_github_environment_and_outputs(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    props = tmp_path / "Directory.Build.props"
    github_env = tmp_path / "github.env"
    github_output = tmp_path / "github.output"
    _write_props(props)
    monkeypatch.setenv("GITHUB_ENV", str(github_env))
    monkeypatch.setenv("GITHUB_OUTPUT", str(github_output))
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "main_build.py",
            "version",
            "--props",
            str(props),
            "--build-number",
            "421",
            "--github-env",
            str(github_env),
            "--github-output",
            str(github_output),
        ],
    )

    main_build_module.main()

    assert capsys.readouterr().out.strip() == "0.8.0-main.421"
    assert github_env.read_text(encoding="utf-8").splitlines() == [
        "ARCH_LINTER_DEVELOPMENT_VERSION=0.8.0",
        "PACKAGE_VERSION=0.8.0-main.421",
    ]
    assert github_output.read_text(encoding="utf-8").splitlines() == [
        "development_version=0.8.0",
        "package_version=0.8.0-main.421",
    ]


def test_version_cli_allows_omitting_github_files(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props)
    monkeypatch.setattr(
        sys,
        "argv",
        ["main_build.py", "version", "--props", str(props), "--build-number", "1"],
    )

    main_build_module.main()

    assert capsys.readouterr().out.strip() == "0.8.0-main.1"


def test_version_cli_rejects_github_output_not_matching_the_runner_env_var(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props)
    monkeypatch.setenv("GITHUB_OUTPUT", str(tmp_path / "the-real-runner-file"))
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "main_build.py",
            "version",
            "--props",
            str(props),
            "--build-number",
            "1",
            "--github-output",
            str(tmp_path / "attacker-controlled.txt"),
        ],
    )

    with pytest.raises(SystemExit) as excinfo:
        main_build_module.main()

    assert excinfo.value.code == 2
    assert "does not match the runner-provided GITHUB_OUTPUT" in capsys.readouterr().err


def test_version_cli_rejects_github_env_when_the_runner_env_var_is_unset(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props)
    monkeypatch.delenv("GITHUB_ENV", raising=False)
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "main_build.py",
            "version",
            "--props",
            str(props),
            "--build-number",
            "1",
            "--github-env",
            str(tmp_path / "github.env"),
        ],
    )

    with pytest.raises(SystemExit) as excinfo:
        main_build_module.main()

    assert excinfo.value.code == 2
    assert "GITHUB_ENV environment variable is not set" in capsys.readouterr().err


def test_retention_cli_writes_plan_and_summary(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    inventory_dir = tmp_path / "inventory"
    output = tmp_path / "nested" / "retention.json"
    _write_inventory_files(
        inventory_dir,
        *(f"0.8.0-main.{number}" for number in range(1, 7)),
    )
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "main_build.py",
            "retention-plan",
            "--inventory-dir",
            str(inventory_dir),
            "--current-version",
            "0.8.0-main.6",
            "--keep",
            "5",
            "--output",
            str(output),
        ],
    )

    main_build_module.main()

    plan = json.loads(output.read_text(encoding="utf-8"))
    assert plan["retained_versions"] == [
        "0.8.0-main.6",
        "0.8.0-main.5",
        "0.8.0-main.4",
        "0.8.0-main.3",
        "0.8.0-main.2",
    ]
    assert "delete=4 package versions" in capsys.readouterr().out


def test_cli_reports_value_errors_with_exit_code_two(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    props = tmp_path / "Directory.Build.props"
    _write_props(props, "preview")
    monkeypatch.setattr(
        sys,
        "argv",
        ["main_build.py", "version", "--props", str(props), "--build-number", "1"],
    )

    with pytest.raises(SystemExit) as error:
        main_build_module.main()

    assert error.value.code == 2
    assert "Error:" in capsys.readouterr().err
