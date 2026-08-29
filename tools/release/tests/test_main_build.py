from __future__ import annotations

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from main_build import (  # noqa: E402
    PACKAGE_IDS,
    create_retention_plan,
    create_retention_plan_from_directory,
    format_main_version,
    read_development_version,
)

_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def test_main_version_uses_explicit_development_version(tmp_path: Path) -> None:
    props = tmp_path / "Directory.Build.props"
    props.write_text(
        "<Project><PropertyGroup>"
        "<ArchLinterDevelopmentVersion>0.8.0</ArchLinterDevelopmentVersion>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )

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


@pytest.mark.parametrize(
    ("base_version", "build_number"),
    [
        ("0.8.0-preview.1", 1),
        ("0.8", 1),
        ("v0.8.0", 1),
        ("0.8.0", 0),
        ("0.8.0", -1),
    ],
)
def test_main_version_rejects_inferred_or_non_monotonic_shapes(
    base_version: str,
    build_number: int,
) -> None:
    with pytest.raises(ValueError):
        format_main_version(base_version, build_number)


def _inventories(*versions: str) -> dict[str, dict[str, int]]:
    return {
        package_id: {
            version: package_index * 1000 + version_index
            for version_index, version in enumerate(versions, start=1)
        }
        for package_index, package_id in enumerate(PACKAGE_IDS, start=1)
    }


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
