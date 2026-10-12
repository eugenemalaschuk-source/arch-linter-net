from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import verify_restored_main_packages as verifier  # noqa: E402


def _write_assets(path: Path, *libraries: str) -> None:
    path.write_text(
        json.dumps({"version": 3, "libraries": {library: {} for library in libraries}}),
        encoding="utf-8",
    )


def test_exact_main_library_package_set_is_accepted(tmp_path: Path) -> None:
    version = "0.8.0-main.11"
    assets = tmp_path / "project.assets.json"
    _write_assets(
        assets,
        "archlinternet.cel/0.8.0-main.11",
        "ArchLinterNet.Core/0.8.0-main.11",
        "ARCHLINTERNET.TESTING/0.8.0-main.11",
        "YamlDotNet/16.3.0",
    )

    verifier.verify_restored_main_packages(assets, version)


def test_missing_or_wrong_main_library_version_is_rejected(tmp_path: Path) -> None:
    version = "0.8.0-main.11"
    assets = tmp_path / "project.assets.json"
    _write_assets(
        assets,
        f"ArchLinterNet.CEL/{version}",
        "ArchLinterNet.Core/0.8.0-main.10",
        f"ArchLinterNet.Testing/{version}",
    )

    with pytest.raises(ValueError, match="not restored"):
        verifier.verify_restored_main_packages(assets, version)


def test_unreadable_or_invalid_assets_are_rejected(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="Cannot read NuGet assets"):
        verifier.verify_restored_main_packages(tmp_path / "missing.json", "0.8.0-main.11")

    invalid = tmp_path / "invalid.json"
    invalid.write_text(json.dumps({"libraries": []}), encoding="utf-8")
    with pytest.raises(ValueError, match="libraries"):
        verifier.verify_restored_main_packages(invalid, "0.8.0-main.11")


def test_cli_accepts_assets_under_runner_temp(tmp_path: Path, monkeypatch, capsys) -> None:
    version = "0.8.0-main.11"
    runner_temp = tmp_path / "runner-temp"
    workspace = runner_temp / "arch-linter-main-package-smoke" / "library"
    assets = workspace / "obj" / "project.assets.json"
    assets.parent.mkdir(parents=True)
    _write_assets(
        assets,
        f"ArchLinterNet.CEL/{version}",
        f"ArchLinterNet.Core/{version}",
        f"ArchLinterNet.Testing/{version}",
    )
    monkeypatch.setenv("RUNNER_TEMP", str(runner_temp))
    monkeypatch.chdir(workspace)
    monkeypatch.setattr(
        sys,
        "argv",
        ["verify_restored_main_packages.py", "--assets", "obj/project.assets.json", "--version", version],
    )

    verifier.main()

    assert f"Verified restored ArchLinterNet library package set {version}." in capsys.readouterr().out


def test_cli_rejects_assets_outside_runner_temp_before_read(tmp_path: Path, monkeypatch, capsys) -> None:
    version = "0.8.0-main.11"
    runner_temp = tmp_path / "runner-temp"
    runner_temp.mkdir()
    outside_assets = tmp_path / "outside-assets.json"
    _write_assets(outside_assets, f"ArchLinterNet.CEL/{version}")
    monkeypatch.setenv("RUNNER_TEMP", str(runner_temp))
    monkeypatch.setattr(
        sys,
        "argv",
        ["verify_restored_main_packages.py", "--assets", str(outside_assets), "--version", version],
    )

    with pytest.raises(SystemExit) as error:
        verifier.main()

    assert error.value.code == 2
    assert "outside the runner-provided RUNNER_TEMP directory" in capsys.readouterr().err
