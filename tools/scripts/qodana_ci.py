"""Tokenless, advisory Qodana runner. Python stdlib only; Docker is required to scan."""

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


def configured_image(project: Path) -> str:
    """Read our deliberately plain, single root linter declaration, not arbitrary YAML."""
    text = (project / "qodana.yaml").read_text(encoding="utf-8")
    declarations = re.findall(r"^linter:\s*(\S+)\s*$", text, re.MULTILINE)
    if len(declarations) != 1 or not re.fullmatch(
        r"jetbrains/qodana-cdnet:\d{4}\.\d+(?:\.\d+)?"
        r"(?:@sha256:[0-9a-f]{64})?", declarations[0]
    ):
        raise ValueError("qodana.yaml must declare one version-pinned Community .NET image")
    return declarations[0]


def inventory(document: dict) -> dict:
    """Validate useful SARIF structure and fingerprint findings, not volatile scan metadata."""
    if document.get("version") != "2.1.0" or not document.get("runs"):
        raise ValueError("Expected SARIF 2.1.0 with at least one analysis run")
    findings = []
    for run in document["runs"]:
        driver = run["tool"]["driver"]
        if not isinstance(driver.get("name"), str) or not driver["name"]:
            raise ValueError("SARIF has no scanner identity")
        for invocation in run.get("invocations", []):
            if invocation.get("executionSuccessful") is False:
                raise ValueError("SARIF records unsuccessful analysis")
            for key in ("toolExecutionNotifications", "toolConfigurationNotifications"):
                if any(n.get("level") == "error" for n in invocation.get(key, [])):
                    raise ValueError("SARIF records an analysis infrastructure error")
        results = run.get("results")
        if not isinstance(results, list):
            raise ValueError("SARIF run has no results inventory")
        for result in results:
            if result.get("baselineState") == "absent":
                continue
            rule = result.get("ruleId")
            if not rule:
                index = result.get("ruleIndex")
                if type(index) is not int or index < 0:
                    raise ValueError("SARIF result has no inspection identity")
                rule = driver["rules"][index]["id"]
            if not isinstance(rule, str) or not rule:
                raise ValueError("Invalid inspection identity")
            message = result.get("message", {})
            text = message.get("text", message.get("markdown"))
            if not isinstance(text, str):
                raise ValueError("SARIF result has no diagnostic message")
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
            findings.append({"rule": rule, "level": result.get("level", "warning"),
                             "message": text, "locations": locations})
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
                       max_total_bytes: int) -> None:
    """Copy regular files from source into destination, skipping symlinks and oversized
    files, up to a total byte budget. Missing source is a no-op, not a failure."""
    if source.is_symlink() or not source.is_dir():
        return
    remaining = max_total_bytes
    for path in sorted(source.rglob("*")):
        if path.is_symlink() or not path.is_file():
            continue
        size = path.stat().st_size
        if size > max_file_bytes or size > remaining:
            continue
        target = destination / path.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(path.read_bytes())
        remaining -= size


def scan(project: Path, work: Path, artifacts: Path, image_id: str,
         label: str, cache: Path, timeout: int = 1200) -> dict:
    results = work / label / "results"
    results.mkdir(parents=True)
    cache.mkdir(parents=True, exist_ok=True)
    name = "qodana-" + uuid.uuid4().hex
    log = artifacts / f"{label}.log"
    args = ["docker", "run", "--rm", "--name", name, "--pull", "never",
            "--user", f"{os.getuid()}:{os.getgid()}",
            "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
            "--volume", f"{project}:/data/project",
            "--volume", f"{results}:/data/results",
            "--volume", f"{cache}:/data/cache", image_id,
            "--results-dir", "/data/results", "--cache-dir", "/data/cache"]
    started = time.monotonic()
    try:
        exit_code = command(args, log, timeout)
    finally:
        # Killing the docker client on timeout does not kill the daemon-side container.
        command(["docker", "rm", "--force", name], log, 30)
    # Preserve whatever diagnostic evidence exists before any later step (including the
    # cache-size metric below) has a chance to raise and lose an already-finished scan.
    sarif = results / "qodana.sarif.json"
    if safe_regular_file(sarif):
        # A bounded, non-symlink regular file is safe to publish even when it later turns
        # out to describe a failed/partial analysis.
        (artifacts / f"{label}.sarif.json").write_bytes(sarif.read_bytes())
    copy_bounded_tree(results / "log", artifacts / f"{label}-log",
                      MAX_LOG_FILE_BYTES, MAX_LOG_TOTAL_BYTES)

    evidence = {"label": label, "exit_code": exit_code,
                "seconds": round(time.monotonic() - started, 3), "status": "failed"}
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
    (directory / "qodana.yaml").write_text(
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
    lines = ["## Qodana Community .NET — advisory", "",
             "This is not a required gate or a reviewed adoption decision.", "",
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


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True,
                        help="New/empty directory outside the analyzed repository")
    parser.add_argument("--burn-in", action="store_true",
                        help="Also run same-image warm analysis and positive/negative probes")
    args = parser.parse_args(argv)
    project, output = args.project.resolve(), args.output.resolve()
    if output == project or project in output.parents:
        parser.error("--output must be outside the project to avoid scanning generated evidence")
    if output.exists() and any(output.iterdir()):
        parser.error("--output must be empty; refusing stale or overwritten evidence")
    artifacts, work = output / "artifacts", output / "work"
    artifacts.mkdir(parents=True)
    work.mkdir()
    evidence = {"schema_version": 1, "status": "failed", "scans": [],
                "burn_in_requested": args.burn_in, "cross_run_cache": False,
                "run_id": os.environ.get("GITHUB_RUN_ID"),
                "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
                "analyzed_sha": os.environ.get("QODANA_ANALYZED_SHA"),
                "event": os.environ.get("GITHUB_EVENT_NAME")}
    started = time.monotonic()
    try:
        if not (project / "ArchLinterNet.slnx").is_file():
            raise ValueError("The canonical ArchLinterNet.slnx solution is missing")
        image = configured_image(project)
        evidence["configured_image"] = image
        pull_start = time.monotonic()
        code = command(["docker", "pull", image], artifacts / "image.log", 300)
        evidence["pull_seconds"] = round(time.monotonic() - pull_start, 3)
        evidence["pull_exit_code"] = code
        if code != 0:
            raise ValueError("Community image pull failed; see image.log (Docker/network/registry)")
        # Pin all scans in this invocation to the exact pulled image, not a mutable tag lookup.
        identity = subprocess.run(["docker", "image", "inspect", image, "--format", "{{.Id}}"],
                                  capture_output=True, text=True, check=True, timeout=30).stdout.strip()
        if not re.fullmatch(r"sha256:[0-9a-f]{64}", identity):
            raise ValueError("Docker did not return an immutable image identity")
        evidence["image_id"] = identity
        cache = work / "repository-cache"
        cold = scan(project, work, artifacts, identity, "cold", cache)
        evidence["scans"].append(cold)
        # Persist completed phases even if a later validation phase is interrupted.
        write_evidence(artifacts, evidence, publish_summary=False)
        if cold["status"] != "completed":
            raise ValueError("Canonical analysis failed; inspect cold.log and cold.sarif.json")
        if args.burn_in:
            warm = scan(project, work, artifacts, identity, "warm", cache)
            evidence["scans"].append(warm)
            write_evidence(artifacts, evidence, publish_summary=False)
            for label, defective in (("positive", True), ("negative", False)):
                probe = work / f"{label}-project"
                create_probe(probe, image, defective)
                evidence["scans"].append(scan(probe, work, artifacts, identity, label,
                                              work / f"{label}-cache", timeout=180))
                write_evidence(artifacts, evidence, publish_summary=False)
            evidence["burn_in_checks_passed"] = burn_in_passed(evidence["scans"])
            if not evidence["burn_in_checks_passed"]:
                raise ValueError("Cold/warm inventory differs or positive/negative probe failed")
        evidence["status"] = "completed"
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        evidence["error"] = str(error)
    finally:
        evidence["wall_seconds"] = round(time.monotonic() - started, 3)
        write_evidence(artifacts, evidence)
    print(f"Qodana evidence collection: {evidence['status']}; inspect the qodana-evidence artifact.")
    return 0 if evidence["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
