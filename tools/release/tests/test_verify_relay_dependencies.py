from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import verify_relay_dependencies as dependencies  # noqa: E402


ROOT = Path(__file__).resolve().parents[3]
INVENTORY_PATH = ROOT / ".github" / "badge-promotion" / "release-inventory.json"


@pytest.fixture(autouse=True)
def _release_workspace(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)


def _fixture(tmp_path: Path) -> tuple[Path, dict]:
    source = tmp_path / "source"
    relay = source / "relay"
    relay.mkdir(parents=True)
    for name in ("package.json", "package-lock.json", "THIRD-PARTY-NOTICES.txt"):
        shutil.copy2(ROOT / "relay" / name, relay / name)
    inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
    inventory_path = tmp_path / "inventory.json"
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
    return source, inventory


def test_offline_check_accepts_lockfile_and_audits_runtime_closure_only(tmp_path: Path) -> None:
    source, inventory = _fixture(tmp_path)
    report = dependencies.verify_relay_dependencies(source, inventory)

    assert report["lock_package_count"] > 0
    assert report["audited_runtime_packages"] == ["node_modules/jose"]
    assert all("vitest" not in name and "wrangler" not in name for name in report["audited_runtime_packages"])


def test_offline_check_rejects_stale_notice_for_locked_runtime_version(tmp_path: Path) -> None:
    source, inventory = _fixture(tmp_path)
    package_json_path = source / "relay" / "package.json"
    package_lock_path = source / "relay" / "package-lock.json"
    package_json = json.loads(package_json_path.read_text(encoding="utf-8"))
    package_lock = json.loads(package_lock_path.read_text(encoding="utf-8"))
    package_json["dependencies"]["jose"] = "6.2.13"
    package_lock["packages"][""]["dependencies"]["jose"] = "6.2.13"
    package_lock["packages"]["node_modules/jose"]["version"] = "6.2.13"
    package_json_path.write_text(json.dumps(package_json), encoding="utf-8")
    package_lock_path.write_text(json.dumps(package_lock), encoding="utf-8")

    with pytest.raises(ValueError, match=r"license notice.*6\.2\.13"):
        dependencies.verify_relay_dependencies(source, inventory)


@pytest.mark.parametrize(
    "mutation",
    ["lock-version", "runtime-map", "runtime-version", "missing-integrity", "missing-license", "missing-notice"],
)
def test_offline_check_rejects_dependency_or_license_failures(tmp_path: Path, mutation: str) -> None:
    source, inventory = _fixture(tmp_path)
    package_lock = source / "relay" / "package-lock.json"
    package_json = source / "relay" / "package.json"
    if mutation == "lock-version":
        value = json.loads(package_lock.read_text(encoding="utf-8"))
        value["lockfileVersion"] = 2
        package_lock.write_text(json.dumps(value), encoding="utf-8")
    elif mutation == "runtime-map":
        value = json.loads(package_json.read_text(encoding="utf-8"))
        value["dependencies"]["jose"] = "6.2.11"
        package_json.write_text(json.dumps(value), encoding="utf-8")
    elif mutation == "runtime-version":
        value = json.loads(package_lock.read_text(encoding="utf-8"))
        value["packages"]["node_modules/jose"]["version"] = "6.2.11"
        package_lock.write_text(json.dumps(value), encoding="utf-8")
    elif mutation in {"missing-integrity", "missing-license"}:
        value = json.loads(package_lock.read_text(encoding="utf-8"))
        entry = value["packages"]["node_modules/jose"]
        entry.pop("integrity" if mutation == "missing-integrity" else "license")
        package_lock.write_text(json.dumps(value), encoding="utf-8")
    else:
        (source / "relay" / "THIRD-PARTY-NOTICES.txt").unlink()

    with pytest.raises(ValueError):
        dependencies.verify_relay_dependencies(source, inventory)


def test_dependency_cli_returns_nonzero_on_failure(tmp_path: Path) -> None:
    source, inventory = _fixture(tmp_path)
    (source / "relay" / "package-lock.json").write_text("{}", encoding="utf-8")
    inventory_path = tmp_path / "inventory.json"
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
    completed = subprocess.run(
        [sys.executable, str(ROOT / "tools" / "release" / "verify_relay_dependencies.py"), "--source-root", str(source), "--inventory", str(inventory_path)],
        check=False,
        capture_output=True,
        text=True,
    )
    assert completed.returncode != 0
    assert "failed" in completed.stderr.lower()
