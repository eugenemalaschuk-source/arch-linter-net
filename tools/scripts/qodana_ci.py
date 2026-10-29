"""Tokenless, read-only Qodana scanner. Python stdlib only; Docker is required to scan."""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import uuid

MAX_SARIF_BYTES = 64 * 1024 * 1024
MAX_LOG_FILE_BYTES = 4 * 1024 * 1024
MAX_LOG_TOTAL_BYTES = 8 * 1024 * 1024
PROBE_RULE = "ConditionIsAlwaysTrueOrFalse"
QODANA_CONFIG = "qodana.yaml"


def configured_image(project: Path) -> str:
    """Read our deliberately plain, single root linter declaration, not arbitrary YAML."""
    text = (project / QODANA_CONFIG).read_text(encoding="utf-8")
    declarations = re.findall(r"^linter:\s*(\S+)\s*$", text, re.MULTILINE)
    if len(declarations) != 1 or not re.fullmatch(
        r"jetbrains/qodana-cdnet:\d{4}\.\d+(?:\.\d+)?"
        r"(?:@sha256:[0-9a-f]{64})?", declarations[0]
    ):
        raise ValueError("qodana.yaml must declare one version-pinned Community .NET image")
    return declarations[0]


def configured_profile(project: Path) -> str:
    text = (project / QODANA_CONFIG).read_text(encoding="utf-8")
    declarations = re.findall(r"^\s+name:\s*(\S+)\s*$", text, re.MULTILINE)
    if len(declarations) != 1:
        raise ValueError("qodana.yaml must declare one named analysis profile")
    return declarations[0]


def configured_solution(project: Path) -> str:
    text = (project / QODANA_CONFIG).read_text(encoding="utf-8")
    declarations = re.findall(r"^\s+solution:\s*(\S+)\s*$", text, re.MULTILINE)
    if len(declarations) != 1:
        raise ValueError("qodana.yaml must declare one canonical solution")
    return declarations[0]


def locked_dotnet_sdk_root(project: Path) -> tuple[Path, str]:
    """Resolve the host-installed SDK that the scanner container will use."""
    root_value = os.environ.get("QODANA_DOTNET_ROOT", "").strip()
    if not root_value:
        raise ValueError("QODANA_DOTNET_ROOT is required for the pinned scanner SDK")
    root = Path(root_value).resolve()
    try:
        global_config = json.loads((project / "global.json").read_text(encoding="utf-8"))
        version = global_config["sdk"]["version"]
    except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        raise ValueError("global.json does not declare a usable .NET SDK version") from error
    if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("global.json does not declare a pinned .NET SDK version")
    if not (root / "sdk" / version).is_dir():
        raise ValueError(f"QODANA_DOTNET_ROOT does not contain the pinned .NET SDK {version}")
    return root, version


def _check_invocations(run: dict) -> None:
    for invocation in run.get("invocations", []):
        if invocation.get("executionSuccessful") is False:
            raise ValueError("SARIF records unsuccessful analysis")
        for key in ("toolExecutionNotifications", "toolConfigurationNotifications"):
            if any(n.get("level") == "error" for n in invocation.get(key, [])):
                raise ValueError("SARIF records an analysis infrastructure error")


def _result_rule(result: dict, driver: dict) -> str:
    rule = result.get("ruleId")
    if not rule:
        index = result.get("ruleIndex")
        if type(index) is not int or index < 0:
            raise ValueError("SARIF result has no inspection identity")
        rule = driver["rules"][index]["id"]
    if not isinstance(rule, str) or not rule:
        raise ValueError("Invalid inspection identity")
    return rule


def _result_message(result: dict) -> str:
    message = result.get("message", {})
    text = message.get("text", message.get("markdown"))
    if not isinstance(text, str):
        raise ValueError("SARIF result has no diagnostic message")
    return text


def _result_locations(result: dict, run: dict) -> list[dict]:
    locations = []
    for location in result.get("locations", []):
        physical = location.get("physicalLocation", {})
        artifact = physical.get("artifactLocation", {})
        if "index" in artifact and "uri" not in artifact:
            index = artifact["index"]
            if type(index) is not int or index < 0:
                raise ValueError("Invalid SARIF artifact index")
            artifact = run["artifacts"][index]["location"]
        locations.append({"uri": artifact.get("uri", ""),
                          "region": physical.get("region", {})})
    return locations


def _run_findings(run: dict) -> list[dict]:
    driver = run["tool"]["driver"]
    if not isinstance(driver.get("name"), str) or not driver["name"]:
        raise ValueError("SARIF has no scanner identity")
    _check_invocations(run)
    results = run.get("results")
    if not isinstance(results, list):
        raise ValueError("SARIF run has no results inventory")
    findings = []
    for result in results:
        if result.get("baselineState") == "absent":
            continue
        rule = _result_rule(result, driver)
        text = _result_message(result)
        locations = _result_locations(result, run)
        findings.append({"rule": rule, "level": result.get("level", "warning"),
                         "message": text, "locations": locations})
    return findings


def inventory(document: dict) -> dict:
    """Validate useful SARIF structure and fingerprint findings, not volatile scan metadata."""
    if document.get("version") != "2.1.0" or not document.get("runs"):
        raise ValueError("Expected SARIF 2.1.0 with at least one analysis run")
    findings = []
    for run in document["runs"]:
        findings.extend(_run_findings(run))
    normalized = sorted(json.dumps(f, sort_keys=True, ensure_ascii=True) for f in findings)
    fingerprint = hashlib.sha256("\n".join(normalized).encode()).hexdigest()
    return {"findings": len(findings),
            "by_rule": dict(sorted(Counter(f["rule"] for f in findings).items())),
            "fingerprint": fingerprint}


def safe_regular_file(path: Path) -> bool:
    return not path.is_symlink() and path.is_file() and path.stat().st_size <= MAX_SARIF_BYTES


def read_inventory(path: Path) -> dict:
    if not safe_regular_file(path):
        raise ValueError("Missing, symlinked or oversized qodana.sarif.json")
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
        return inventory(document)
    except (AttributeError, IndexError, KeyError, TypeError, UnicodeError) as error:
        raise ValueError("Malformed SARIF inventory") from error


def command(arguments: list[str], log: Path, timeout: int) -> int:
    # Scanner/project output is untrusted. Never stream it as Actions workflow commands.
    with log.open("a", encoding="utf-8") as output:
        try:
            return subprocess.run(arguments, stdout=output, stderr=subprocess.STDOUT,
                                  timeout=timeout, check=False).returncode
        except subprocess.TimeoutExpired:
            output.write(f"\nCommand exceeded {timeout} seconds.\n")
            return 124
        except OSError as error:
            output.write(f"\nUnable to execute command: {error}\n")
            return 127


def cache_bytes(cache: Path) -> int:
    return sum(path.stat().st_size for path in cache.rglob("*")
               if not path.is_symlink() and path.is_file())


def copy_bounded_tree(source: Path, destination: Path, max_file_bytes: int,
                       max_total_bytes: int) -> list[str]:
    """Copy regular files from source into destination, skipping symlinks and oversized
    files, up to a total byte budget. Missing source is a no-op, not a failure. A single
    unreadable file is recorded and skipped rather than losing the rest of the copy."""
    errors: list[str] = []
    try:
        if source.is_symlink() or not source.is_dir():
            return errors
        entries = sorted(source.rglob("*"))
    except OSError as error:
        errors.append(f"{source}: {error}")
        return errors
    remaining = max_total_bytes
    for path in entries:
        try:
            if path.is_symlink() or not path.is_file():
                continue
            size = path.stat().st_size
            if size > max_file_bytes or size > remaining:
                continue
            target = destination / path.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(path.read_bytes())
            remaining -= size
        except OSError as error:
            errors.append(f"{path}: {error}")
    return errors


def scan(project: Path, work: Path, artifacts: Path, image_id: str,
         label: str, cache: Path, timeout: int = 1200,
         dotnet_root: Path | None = None) -> dict:
    results = work / label / "results"
    results.mkdir(parents=True)
    cache.mkdir(parents=True, exist_ok=True)
    name = "qodana-" + uuid.uuid4().hex
    log = artifacts / f"{label}.log"
    args = ["docker", "run", "--rm", "--name", name, "--pull", "never",
            "--user", f"{os.getuid()}:{os.getgid()}",
            "--cap-drop", "ALL", "--security-opt", "no-new-privileges"]
    if dotnet_root is not None:
        args.extend([
            "--volume", f"{dotnet_root.resolve()}:/usr/share/dotnet:ro",
            "--env", "DOTNET_ROOT=/usr/share/dotnet",
            "--env", "DOTNET_ROOT_X64=/usr/share/dotnet",
        ])
    args.extend([
        "--volume", f"{project}:/data/project",
        "--volume", f"{results}:/data/results",
        "--volume", f"{cache}:/data/cache", image_id,
        "--results-dir", "/data/results", "--cache-dir", "/data/cache",
    ])
    started = time.monotonic()
    try:
        exit_code = command(args, log, timeout)
    finally:
        # Killing the docker client on timeout does not kill the daemon-side container.
        command(["docker", "rm", "--force", name], log, 30)
    # Build the evidence record before any evidence-preservation step below can raise;
    # exit_code/seconds/status must survive even a file-level failure while collecting
    # the rest of the diagnostics for an already-finished scan.
    evidence = {"label": label, "exit_code": exit_code,
                "seconds": round(time.monotonic() - started, 3), "status": "failed"}
    sarif = results / "qodana.sarif.json"
    try:
        sarif_is_safe = safe_regular_file(sarif)
    except OSError as error:
        sarif_is_safe = False
        evidence["sarif_copy_error"] = str(error)
    if sarif_is_safe:
        # A bounded, non-symlink regular file is safe to publish even when it later turns
        # out to describe a failed/partial analysis.
        try:
            sarif_bytes = sarif.read_bytes()
            (artifacts / f"{label}.sarif.json").write_bytes(sarif_bytes)
            evidence["sarif_sha256"] = hashlib.sha256(sarif_bytes).hexdigest()
            evidence["sarif_bytes"] = len(sarif_bytes)
        except OSError as error:
            evidence["sarif_copy_error"] = str(error)
    log_errors = copy_bounded_tree(results / "log", artifacts / f"{label}-log",
                                   MAX_LOG_FILE_BYTES, MAX_LOG_TOTAL_BYTES)
    if log_errors:
        evidence["log_errors"] = log_errors
    try:
        evidence["cache_bytes"] = cache_bytes(cache)
    except OSError as error:
        evidence["cache_bytes"] = None
        evidence["cache_error"] = str(error)
    try:
        evidence.update(read_inventory(sarif))
        if exit_code == 0:
            evidence["status"] = "completed"
        else:
            evidence["error"] = "Scanner failed; partial SARIF is evidence, not a clean scan"
    except (OSError, ValueError) as error:
        evidence["error"] = str(error)
    return evidence


def create_probe(directory: Path, image: str, defective: bool) -> None:
    directory.mkdir(parents=True)
    (directory / "Probe.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n'
        '    <TargetFramework>net10.0</TargetFramework>\n'
        '    <Nullable>disable</Nullable>\n'
        '    <EnableNETAnalyzers>false</EnableNETAnalyzers>\n'
        '  </PropertyGroup>\n</Project>\n', encoding="utf-8")
    expression = "value != null" if defective else "true"
    (directory / "Probe.cs").write_text(
        'public static class InspectionProbe\n{\n'
        '    public static bool Check(object value)\n    {\n'
        '        if (value == null)\n        {\n            return false;\n        }\n'
        f'        return {expression};\n    }}\n}}\n', encoding="utf-8")
    (directory / QODANA_CONFIG).write_text(
        f'version: "1.0"\nlinter: {image}\nprofile:\n  name: qodana.recommended\n'
        f'include:\n  - name: {PROBE_RULE}\ndotnet:\n  project: Probe.csproj\n'
        '  configuration: Release\n', encoding="utf-8")


def burn_in_passed(scans: list[dict]) -> bool:
    if len(scans) != 4 or any(s["status"] != "completed" for s in scans):
        return False
    cold, warm, positive, negative = scans
    return (cold["fingerprint"] == warm["fingerprint"]
            and positive["by_rule"].get(PROBE_RULE, 0) > 0
            and negative["by_rule"].get(PROBE_RULE, 0) == 0)


def write_evidence(artifacts: Path, evidence: dict, publish_summary: bool = True) -> None:
    (artifacts / "evidence.json").write_text(
        json.dumps(evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    lines = ["## Qodana Community .NET — scan", "",
             "Finding-level results are evaluated by the trusted `qodana` gate after this scan completes.", "",
             f"Collection status: **{evidence['status']}**. See `evidence.json` and scan logs.", "",
             "| Scan | Status | Exit | Seconds | Findings | Cache bytes |",
             "| --- | --- | --- | --- | --- | --- |"]
    for item in evidence["scans"]:
        lines.append(f"| {item['label']} | {item['status']} | {item['exit_code']} | "
                     f"{item['seconds']} | {item.get('findings', 'unknown')} | "
                     f"{item['cache_bytes']} |")
    lines += ["", "Durations are measured scanner phases, not GitHub-billed minutes.",
              "Review raw SARIF for diagnostics. Missing reports do not mean zero findings.", ""]
    summary = "\n".join(lines)
    (artifacts / "summary.md").write_text(summary, encoding="utf-8")
    if publish_summary and os.environ.get("GITHUB_STEP_SUMMARY"):
        with Path(os.environ["GITHUB_STEP_SUMMARY"]).open("a", encoding="utf-8") as output:
            output.write(summary)


def _scan_arguments(argv: list[str] | None) -> tuple[argparse.Namespace, Path, Path | None, Path]:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path.cwd())
    parser.add_argument("--base-project", type=Path,
                        help="Separate checkout of the exact pull request base")
    parser.add_argument("--output", type=Path, required=True,
                        help="New/empty directory outside the analyzed repository")
    parser.add_argument("--burn-in", action="store_true",
                        help="Also run same-image warm analysis and positive/negative probes")
    args = parser.parse_args(argv)
    project, output = args.project.resolve(), args.output.resolve()
    base_project = args.base_project.resolve() if args.base_project else None
    if base_project is not None and (base_project == project or project in base_project.parents
                                     or base_project in project.parents or args.burn_in):
        parser.error("--base-project must be a separate checkout and cannot be combined with --burn-in")
    if output == project or project in output.parents:
        parser.error("--output must be outside the project to avoid scanning generated evidence")
    if base_project is not None and (output == base_project or base_project in output.parents):
        parser.error("--output must be outside the base checkout")
    if output.exists() and any(output.iterdir()):
        parser.error("--output must be empty; refusing stale or overwritten evidence")
    return args, project, base_project, output


def _initial_evidence(burn_in: bool) -> dict:
    return {"schema_version": 1, "status": "failed", "scans": [],
            "burn_in_requested": burn_in, "cross_run_cache": False,
            "run_id": os.environ.get("GITHUB_RUN_ID"),
            "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
            "analyzed_sha": os.environ.get("QODANA_ANALYZED_SHA"),
            "repository": os.environ.get("GITHUB_REPOSITORY"),
            "pull_request_number": _optional_positive_int(os.environ.get("QODANA_PULL_REQUEST_NUMBER")),
            "head_sha": _optional_text(os.environ.get("QODANA_HEAD_SHA")),
            "base_sha": _optional_text(os.environ.get("QODANA_BASE_SHA")),
            "base_analyzed_sha": _optional_text(os.environ.get("QODANA_BASE_ANALYZED_SHA")),
            "event": os.environ.get("GITHUB_EVENT_NAME")}


def _configure_scan(project: Path, base_project: Path | None, evidence: dict) -> tuple[Path, str]:
    if not (project / "ArchLinterNet.slnx").is_file():
        raise ValueError("The canonical ArchLinterNet.slnx solution is missing")
    dotnet_root, sdk_version = locked_dotnet_sdk_root(project)
    evidence["dotnet_sdk_version"] = sdk_version
    image = configured_image(project)
    evidence["configured_image"] = image
    evidence["configured_profile"] = configured_profile(project)
    evidence["configured_solution"] = configured_solution(project)
    if base_project is not None:
        if (evidence["base_analyzed_sha"] != evidence["base_sha"]
                or not re.fullmatch(r"[0-9a-f]{40}", evidence["base_sha"] or "")):
            raise ValueError("Base checkout does not match the pull request base SHA")
        _, base_sdk_version = locked_dotnet_sdk_root(base_project)
        if (configured_image(base_project) != image
                or configured_profile(base_project) != evidence["configured_profile"]
                or configured_solution(base_project) != evidence["configured_solution"]
                or base_sdk_version != sdk_version):
            raise ValueError("Base and head scanner configurations do not match")
    return dotnet_root, image


def _pull_image(image: str, artifacts: Path, evidence: dict) -> str:
    pull_start = time.monotonic()
    code = command(["docker", "pull", image], artifacts / "image.log", 300)
    evidence["pull_seconds"] = round(time.monotonic() - pull_start, 3)
    evidence["pull_exit_code"] = code
    if code != 0:
        raise ValueError("Community image pull failed; see image.log (Docker/network/registry)")
    identity = subprocess.run(["docker", "image", "inspect", image, "--format", "{{.Id}}"],
                              capture_output=True, text=True, check=True, timeout=30).stdout.strip()
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", identity):
        raise ValueError("Docker did not return an immutable image identity")
    evidence["image_id"] = identity
    return identity


def _run_repository_scans(project: Path, base_project: Path | None, work: Path, artifacts: Path,
                          identity: str, dotnet_root: Path, evidence: dict) -> Path:
    if base_project is not None:
        base_scan = scan(base_project, work, artifacts, identity, "base", work / "base-cache",
                         dotnet_root=dotnet_root)
        evidence["scans"].append(base_scan)
        write_evidence(artifacts, evidence, publish_summary=False)
        if base_scan["status"] != "completed":
            raise ValueError("Base analysis failed; inspect base.log and base.sarif.json")
    cache = work / "repository-cache"
    cold = scan(project, work, artifacts, identity, "cold", cache, dotnet_root=dotnet_root)
    evidence["scans"].append(cold)
    write_evidence(artifacts, evidence, publish_summary=False)
    if cold["status"] != "completed":
        raise ValueError("Canonical analysis failed; inspect cold.log and cold.sarif.json")
    return cache


def _run_burn_in(project: Path, work: Path, artifacts: Path, image: str, identity: str,
                 dotnet_root: Path, cache: Path, evidence: dict) -> None:
    warm = scan(project, work, artifacts, identity, "warm", cache, dotnet_root=dotnet_root)
    evidence["scans"].append(warm)
    write_evidence(artifacts, evidence, publish_summary=False)
    for label, defective in (("positive", True), ("negative", False)):
        probe = work / f"{label}-project"
        create_probe(probe, image, defective)
        evidence["scans"].append(scan(probe, work, artifacts, identity, label,
                                      work / f"{label}-cache", timeout=180,
                                      dotnet_root=dotnet_root))
        write_evidence(artifacts, evidence, publish_summary=False)
    evidence["burn_in_checks_passed"] = burn_in_passed(evidence["scans"])
    if not evidence["burn_in_checks_passed"]:
        raise ValueError("Cold/warm inventory differs or positive/negative probe failed")


def main(argv: list[str] | None = None) -> int:
    args, project, base_project, output = _scan_arguments(argv)
    artifacts, work = output / "artifacts", output / "work"
    artifacts.mkdir(parents=True)
    work.mkdir()
    evidence = _initial_evidence(args.burn_in)
    started = time.monotonic()
    try:
        dotnet_root, image = _configure_scan(project, base_project, evidence)
        identity = _pull_image(image, artifacts, evidence)
        cache = _run_repository_scans(project, base_project, work, artifacts, identity, dotnet_root, evidence)
        if args.burn_in:
            _run_burn_in(project, work, artifacts, image, identity, dotnet_root, cache, evidence)
        evidence["status"] = "completed"
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        evidence["error"] = str(error)
    finally:
        evidence["wall_seconds"] = round(time.monotonic() - started, 3)
        write_evidence(artifacts, evidence)
    print(f"Qodana evidence collection: {evidence['status']}; inspect the qodana-evidence artifact.")
    return 0 if evidence["status"] == "completed" else 1


def _optional_positive_int(value: str | None) -> int | None:
    if value is None or value == "":
        return None
    if not re.fullmatch(r"[1-9]\d*", value):
        return None
    return int(value)


def _optional_text(value: str | None) -> str | None:
    return value if value else None


if __name__ == "__main__":
    raise SystemExit(main())
