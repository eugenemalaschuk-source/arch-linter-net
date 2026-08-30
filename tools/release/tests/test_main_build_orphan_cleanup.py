from __future__ import annotations

import sys
from datetime import datetime, timezone
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from main_build import PACKAGE_IDS, PackageVersionInfo, create_retention_plan  # noqa: E402

_CUTOFF = datetime(2026, 8, 30, 13, 0, tzinfo=timezone.utc)
_OLD = datetime(2026, 8, 30, 11, 0, tzinfo=timezone.utc)
_RECENT = datetime(2026, 8, 30, 13, 30, tzinfo=timezone.utc)


def _inventories(*versions: str) -> dict[str, dict[str, int | PackageVersionInfo]]:
    return {
        package_id: {
            version: package_index * 1000 + version_index
            for version_index, version in enumerate(versions, start=1)
        }
        for package_index, package_id in enumerate(PACKAGE_IDS, start=1)
    }


def test_explicit_orphan_cleanup_prunes_only_old_partial_records() -> None:
    inventories = _inventories(
        "0.8.0-main.2",
        "0.8.0-main.3",
        "0.8.0-main.4",
        "0.8.0-main.5",
        "0.8.0-main.6",
    )
    inventories[PACKAGE_IDS[0]]["0.8.0-main.1"] = PackageVersionInfo(10001, _OLD)
    inventories[PACKAGE_IDS[2]]["0.8.0-main.1"] = PackageVersionInfo(30001, _OLD)
    inventories[PACKAGE_IDS[0]]["0.7.4"] = 10002
    inventories[PACKAGE_IDS[1]]["0.8.0-rc.1"] = 20002

    plan = create_retention_plan(
        inventories,
        "0.8.0-main.6",
        keep=5,
        prune_stale_partials=True,
        stale_partial_before=_CUTOFF,
    )

    assert plan["schema"] == "arch-linter-main-package-retention/v2"
    assert plan["orphan_cleanup_enabled"] is True
    assert plan["stale_partial_before"] == "2026-08-30T13:00:00Z"
    assert plan["stale_partial_versions"] == ["0.8.0-main.1"]
    assert plan["protected_partial_versions"] == []
    assert plan["delete"] == []
    assert {
        (record["package_id"], record["version"], record["version_id"], record["reason"])
        for record in plan["delete_orphans"]
    } == {
        (PACKAGE_IDS[0], "0.8.0-main.1", 10001, "stale_partial"),
        (PACKAGE_IDS[2], "0.8.0-main.1", 30001, "stale_partial"),
    }
    assert all(
        record["version"] not in {"0.7.4", "0.8.0-rc.1"}
        for record in plan["delete_orphans"]
    )


def test_orphan_cleanup_preserves_recent_older_and_newer_partial_versions() -> None:
    inventories = _inventories("0.8.0-main.3")
    inventories[PACKAGE_IDS[0]]["0.8.0-main.1"] = PackageVersionInfo(10001, _OLD)
    inventories[PACKAGE_IDS[1]]["0.8.0-main.1"] = PackageVersionInfo(20001, _RECENT)
    inventories[PACKAGE_IDS[0]]["0.8.0-main.4"] = PackageVersionInfo(10004, _OLD)
    inventories[PACKAGE_IDS[1]]["0.9.0-main.1"] = PackageVersionInfo(20009, _OLD)

    plan = create_retention_plan(
        inventories,
        "0.8.0-main.3",
        keep=5,
        prune_stale_partials=True,
        stale_partial_before=_CUTOFF,
    )

    assert plan["stale_partial_versions"] == []
    assert plan["protected_partial_versions"] == [
        "0.9.0-main.1",
        "0.8.0-main.4",
        "0.8.0-main.1",
    ]
    assert plan["delete_orphans"] == []


def test_orphan_cleanup_is_opt_in_and_requires_explicit_cutoff() -> None:
    inventories = _inventories("0.8.0-main.2")
    inventories[PACKAGE_IDS[0]]["0.8.0-main.1"] = PackageVersionInfo(10001, _OLD)

    plan = create_retention_plan(inventories, "0.8.0-main.2", keep=5)

    assert plan["orphan_cleanup_enabled"] is False
    assert plan["stale_partial_versions"] == []
    assert plan["protected_partial_versions"] == ["0.8.0-main.1"]
    assert plan["delete"] == []
    assert plan["delete_orphans"] == []

    with pytest.raises(ValueError, match="explicit UTC cutoff"):
        create_retention_plan(
            inventories,
            "0.8.0-main.2",
            keep=5,
            prune_stale_partials=True,
        )


def test_orphan_cleanup_preserves_partial_records_without_trusted_age() -> None:
    inventories = _inventories("0.8.0-main.2")
    inventories[PACKAGE_IDS[0]]["0.8.0-main.1"] = PackageVersionInfo(10001, None)
    inventories[PACKAGE_IDS[1]]["0.8.0-main.1"] = PackageVersionInfo(20001, _OLD)

    plan = create_retention_plan(
        inventories,
        "0.8.0-main.2",
        keep=5,
        prune_stale_partials=True,
        stale_partial_before=_CUTOFF,
    )

    assert plan["stale_partial_versions"] == []
    assert plan["protected_partial_versions"] == ["0.8.0-main.1"]
    assert plan["delete_orphans"] == []
