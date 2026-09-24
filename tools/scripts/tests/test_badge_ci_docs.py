"""Check onboarding routing and execute documentation shell examples offline.

These checks validate workflow structure and failure propagation, not a real
CLI evaluation, GitHub provenance verifier, or Cloudflare deployment.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
from pathlib import Path

import pytest
import yaml

ROOT = Path(__file__).resolve().parents[3]
CI = ROOT / "docs/guides/ci-integration.md"
DIRECT = ROOT / "docs/guides/badge-direct-hosting.md"
BASH = shutil.which("bash")


def blocks(path: Path, language: str) -> list[str]:
    return re.findall(
        rf"^```{re.escape(language)}\n(.*?)^```\s*$",
        path.read_text(encoding="utf-8"),
        re.MULTILINE | re.DOTALL,
    )


def test_onboarding_routes_do_not_require_experimental_relay() -> None:
    nav = yaml.safe_load((ROOT / "mkdocs.yml").read_text(encoding="utf-8"))["nav"]
    getting_started = next(item["Getting Started"] for item in nav if "Getting Started" in item)
    destinations = [value for entry in getting_started for value in entry.values()]
    assert "guides/ci-integration.md" in destinations
    assert "guides/badge-adoption.md" in destinations
    assert "guides/badge-setup.md" not in destinations
    guides = next(item["Guides"] for item in nav if "Guides" in item)
    assert any("guides/badge-direct-hosting.md" in item.values() for item in guides)
    relay = next(item["Experimental Relay"] for item in guides if "Experimental Relay" in item)
    assert any("guides/badge-setup.md" in item.values() for item in relay)


def test_starter_workflow_keeps_strict_required_and_artifacts_attempt_bound() -> None:
    # BaseLoader preserves the GitHub Actions "on" key instead of YAML 1.1 bool coercion.
    workflow = yaml.load(blocks(CI, "yaml")[0], Loader=yaml.BaseLoader)
    assert set(workflow["on"]) == {"pull_request"}
    assert workflow["permissions"] == {"contents": "read"}
    job = workflow["jobs"]["architecture"]
    assert "continue-on-error" not in job
    steps = job["steps"]
    validation = next(step for step in steps if step.get("name") == "Validate architecture")
    assert "continue-on-error" not in validation
    assert "--mode strict " in validation["run"]
    assert "|| true" not in validation["run"]
    assert "secrets." not in blocks(CI, "yaml")[0]
    checkout = next(step for step in steps if step.get("uses", "").startswith("actions/checkout@"))
    assert checkout["with"]["persist-credentials"] == "false"
    upload = next(step for step in steps if step.get("uses", "").startswith("actions/upload-artifact@"))
    assert upload["if"] == "always()"
    assert "github.run_id" in upload["with"]["name"]
    assert "github.run_attempt" in upload["with"]["name"]
    for step in steps:
        if "uses" in step:
            assert re.fullmatch(r"[^@]+@[0-9a-f]{40}", step["uses"])


def test_advisory_audit_does_not_become_a_required_gate() -> None:
    step = yaml.safe_load(blocks(CI, "yaml")[1])[0]
    assert step["continue-on-error"] is True
    assert "--mode audit " in step["run"]


def scripts() -> list[str]:
    result: list[str] = []
    for path in (CI, DIRECT):
        result.extend(blocks(path, "bash"))
        for text in blocks(path, "yaml"):
            value = yaml.safe_load(text)
            candidates = value if isinstance(value, list) else value.get("steps", [])
            if isinstance(value, dict):
                for job in value.get("jobs", {}).values():
                    candidates.extend(job.get("steps", []))
            result.extend(step["run"] for step in candidates if "run" in step)
            result.extend(step["script"] for step in candidates if "script" in step)
    return result


@pytest.mark.skipif(BASH is None, reason="Bash is required to check the documented POSIX recipes")
def test_shell_examples_have_valid_syntax() -> None:
    assert scripts()
    for script in scripts():
        result = subprocess.run([BASH, "-n"], input=script, text=True, capture_output=True, check=False)
        assert result.returncode == 0, result.stderr


def environment(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> dict[str, str]:
    """Supply a curl stand-in; no example in these tests can reach the network."""
    bindir = tmp_path / "bin"
    bindir.mkdir()
    curl = bindir / "curl"
    curl.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        "if [ \"${CURL_FAIL:-0}\" = 1 ]; then exit 22; fi\n"
        "output=''\nurl=''\n"
        "while [ $# -gt 0 ]; do\n"
        "  case \"$1\" in\n"
        "    --output) output=$2; shift 2;;\n"
        "    https://*) url=$1; shift;;\n"
        "    *) shift;;\n"
        "  esac\n"
        "done\n"
        "case \"$url\" in\n"
        "  https://badge.example/badge.json) printf '%s' \"$JSON_RESPONSE\" > \"$output\";;\n"
        "  https://badge.example/badge.svg) printf '%s' \"$SVG_RESPONSE\" > \"$output\";;\n"
        "  https://api.cloudflare.com/*) printf '%s' \"$API_RESPONSE\" > \"$output\";;\n"
        "  *) exit 64;;\n"
        "esac\n",
        encoding="utf-8",
    )
    curl.chmod(0o755)
    publication = tmp_path / "publication"
    publication.mkdir()
    (publication / "worker.js").write_text("// Offline test fixture; never deployed.\n", encoding="utf-8")
    (publication / "architecture-health-badge.json").write_text("{}", encoding="utf-8")
    (publication / "architecture-health-badge.svg").write_text("<svg/>", encoding="utf-8")
    for key, value in {
        "PATH": str(bindir) + os.pathsep + os.environ["PATH"],
        "RUNNER_TEMP": str(tmp_path),
        "BADGE_JSON_URL": "https://badge.example/badge.json",
        "BADGE_SVG_URL": "https://badge.example/badge.svg",
        "JSON_RESPONSE": "{}",
        "SVG_RESPONSE": "<svg/>",
        "API_RESPONSE": '{"success":true}',
        "CF_API_TOKEN": "offline-test-token",
        "CLOUDFLARE_ACCOUNT_ID": "offline-account",
        "CLOUDFLARE_WORKER_NAME": "offline-worker",
        "CURL_FAIL": "0",
    }.items():
        monkeypatch.setenv(key, value)
    return dict(os.environ)


@pytest.mark.skipif(BASH is None, reason="Bash is required to execute the documented POSIX recipes")
@pytest.mark.parametrize("json_ok,svg_ok,http_ok", [(True, True, True), (False, True, True), (True, False, True), (True, True, False)])
def test_public_readback_rejects_wrong_bytes_and_http_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, json_ok: bool, svg_ok: bool, http_ok: bool
) -> None:
    env = environment(tmp_path, monkeypatch)
    env.pop("CF_API_TOKEN")  # Anonymous readback must not need the publishing secret.
    env["JSON_RESPONSE"] = "{}" if json_ok else "wrong-json"
    env["SVG_RESPONSE"] = "<svg/>" if svg_ok else "wrong-svg"
    env["CURL_FAIL"] = "0" if http_ok else "1"
    result = subprocess.run([BASH], input=blocks(DIRECT, "bash")[0], text=True, cwd=tmp_path, env=env, capture_output=True, check=False)
    assert (result.returncode == 0) is (json_ok and svg_ok and http_ok)


@pytest.mark.skipif(BASH is None, reason="Bash is required to execute the documented POSIX recipes")
@pytest.mark.parametrize("response,expected", [('{"success":true}', True), ('{"success":false}', False), ('{}', False), ('not-json', False)])
def test_upload_checks_the_provider_success_envelope(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, response: str, expected: bool
) -> None:
    env = environment(tmp_path, monkeypatch)
    env["API_RESPONSE"] = response
    step = yaml.safe_load(blocks(DIRECT, "yaml")[0])[0]
    script = step["run"]
    result = subprocess.run([BASH], input=script, text=True, cwd=tmp_path, env=env, capture_output=True, check=False)
    assert (result.returncode == 0) is expected
    assert env["CF_API_TOKEN"] not in result.stdout + result.stderr
