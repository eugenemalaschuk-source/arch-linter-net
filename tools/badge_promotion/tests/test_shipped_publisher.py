"""Exercise the immutable publisher, not merely today's working-tree resolver."""
from __future__ import annotations

import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[3]
REGISTRY = ".github/badge-promotion/registry.json"
CONTRACTS = ("test_setup_registry.py", "test_cli_contracts.py")


def test_shipped_publisher_satisfies_current_consumer_contracts(tmp_path: Path) -> None:
    inventory = json.loads((ROOT / ".github/badge-promotion/release-inventory.json").read_text())
    pin = inventory["compatibility"]["publisher_commit"]
    # Coverage CI retains full history. A missing pin is a failure, never a
    # fallback to working-tree code that would hide a stale shipped publisher.
    exported = subprocess.run(
        ["git", "-C", str(ROOT), "archive", "--format=tar", pin,
         "tools/badge_promotion", REGISTRY,
         "architecture/architecture-health-badge-unavailable.json"],
        check=True, capture_output=True, timeout=30,
    ).stdout
    isolated = tmp_path / "pinned-publisher"
    isolated.mkdir()
    with tarfile.open(fileobj=io.BytesIO(exported), mode="r:") as archive:
        for member in archive:
            path = PurePosixPath(member.name)
            assert not path.is_absolute() and ".." not in path.parts
            assert member.isdir() or member.isfile(), member.name
            if member.isfile():
                target = isolated.joinpath(*path.parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                source = archive.extractfile(member)
                assert source is not None
                with source:
                    target.write_bytes(source.read())

    # Current contract assertions run against archived runtime modules in a
    # separate interpreter. Only tests/fixtures are overlaid, never product code.
    tests = isolated / "tools/badge_promotion/tests"
    for name in CONTRACTS:
        shutil.copyfile(Path(__file__).parent / name, tests / name)
    shutil.copytree(Path(__file__).parent / "fixtures", tests / "fixtures", dirs_exist_ok=True)
    environment = {
        key: value for key, value in os.environ.items()
        if not key.startswith(("GITHUB_", "COV_CORE", "COVERAGE_", "PYTEST_"))
        and key not in {"GH_TOKEN", "CF_API_TOKEN", "CLOUDFLARE_API_TOKEN", "PYTHONPATH"}
    }
    environment["PYTEST_DISABLE_PLUGIN_AUTOLOAD"] = "1"
    environment["PYTHONPATH"] = str(isolated)
    result = subprocess.run(
        [sys.executable, "-m", "pytest", "-q", "-p", "no:cacheprovider",
         *(str(tests / name) for name in CONTRACTS)],
        cwd=isolated, env=environment, text=True, capture_output=True, timeout=60,
    )
    assert result.returncode == 0, (
        f"Approved publisher {pin} fails current consumer registry/attempt contracts.\n"
        "Review and rotate the shipped pin; do not substitute local runtime code.\n"
        + result.stdout + result.stderr
    )
