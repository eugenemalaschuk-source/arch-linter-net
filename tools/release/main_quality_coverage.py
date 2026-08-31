#!/usr/bin/env python3
"""Build and verify commit-bound coverage evidence for main quality telemetry."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from _release_workspace import _safe_path

_SHARD_SCHEMA = "main-quality-coverage-shard/v1"
_INVENTORY_SCHEMA = "main-quality-coverage-inventory/v1"
_SHA_PATTERN = re.compile(r"[0-9a-f]{40}")
_SONAR_STATS_PATTERN = re.compile(
    r"Coverage Report Statistics: \d+ files, \d+ main files, (\d+) main files with coverage"
)
_GITHUB_OUTPUT_DESCRIPTION = "GitHub output file"


@dataclass(frozen=True)
class ProducerSpec:
    id: str
    test_project: str
    relative_root: str


_SHARDS: dict[str, tuple[ProducerSpec, ...]] = {
    "core-1": (ProducerSpec("core", "ArchLinterNet.Core.Tests", "."),),
    "core-2": (ProducerSpec("core", "ArchLinterNet.Core.Tests", "."),),
    "other": (
        ProducerSpec("cel", "ArchLinterNet.CEL.Tests", "cel"),
        ProducerSpec("cli", "ArchLinterNet.Cli.Tests", "cli"),
    ),
}
_SHARD_IDS = tuple(_SHARDS)
_REPORT_FORMATS: dict[str, tuple[str, str]] = {
    "opencover": ("coverage.opencover.xml", "CoverageSession"),
    "cobertura": ("coverage.cobertura.xml", "coverage"),
}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _validate_sha(value: str) -> None:
    if not _SHA_PATTERN.fullmatch(value):
        raise ValueError(f"Coverage evidence source SHA is invalid: {value!r}")


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _validate_report(path: Path, report_format: str) -> tuple[int, str]:
    if not path.is_file() or path.stat().st_size <= 0:
        raise ValueError(f"Coverage report is missing or empty: {path}")
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as error:
        raise ValueError(f"Coverage report is not parseable XML: {path}: {error}") from error
    expected_root = _REPORT_FORMATS[report_format][1]
    if _local_name(root.tag) != expected_root:
        raise ValueError(
            f"Coverage report has unexpected root element for {report_format}: "
            f"{path} -> {_local_name(root.tag)!r}"
        )
    return path.stat().st_size, _sha256(path)


def _candidate_rank(path: Path, producer_root: Path) -> tuple[int, int, str]:
    relative = path.relative_to(producer_root)
    parts = relative.parts
    collector_copy = 1 if "In" in parts else 0
    return collector_copy, len(parts), relative.as_posix()


def _select_canonical_report(
    producer_root: Path,
    report_format: str,
) -> tuple[Path, int, int, str]:
    filename = _REPORT_FORMATS[report_format][0]
    candidates = sorted(producer_root.rglob(filename)) if producer_root.is_dir() else []
    if not candidates:
        raise ValueError(f"Missing {report_format} report under {producer_root}")

    validated: list[tuple[Path, int, str]] = []
    for candidate in candidates:
        size, digest = _validate_report(candidate, report_format)
        validated.append((candidate, size, digest))

    digests = {digest for _, _, digest in validated}
    if len(digests) != 1:
        rendered = ", ".join(
            f"{path.relative_to(producer_root).as_posix()}={digest}"
            for path, _, digest in validated
        )
        raise ValueError(
            f"Ambiguous {report_format} coverage evidence under {producer_root}: "
            f"collector candidates have different hashes ({rendered})"
        )

    chosen, size, digest = min(validated, key=lambda item: _candidate_rank(item[0], producer_root))
    return chosen, len(candidates), size, digest


def _write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _read_json(path: Path, description: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"Cannot read {description} '{path}': {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{description.capitalize()} must be a JSON object: {path}")
    return value


def _expected_report_records(shard_id: str) -> list[tuple[ProducerSpec, str, str]]:
    return [
        (producer, report_format, _REPORT_FORMATS[report_format][0])
        for producer in _SHARDS[shard_id]
        for report_format in _REPORT_FORMATS
    ]


def _validate_shard_manifest(manifest: dict[str, Any], manifest_path: Path) -> dict[str, Any]:
    if set(manifest) != {"schema", "source_sha", "shard_id", "reports"}:
        raise ValueError(f"Coverage shard manifest fields are invalid: {manifest_path}")
    if manifest.get("schema") != _SHARD_SCHEMA:
        raise ValueError(f"Unsupported coverage shard manifest schema: {manifest_path}")
    source_sha = manifest.get("source_sha")
    if not isinstance(source_sha, str):
        raise ValueError(f"Coverage shard source SHA is invalid: {manifest_path}")
    _validate_sha(source_sha)
    shard_id = manifest.get("shard_id")
    if shard_id not in _SHARDS:
        raise ValueError(f"Coverage shard id is invalid: {manifest_path}")
    reports = manifest.get("reports")
    expected = _expected_report_records(shard_id)
    if not isinstance(reports, list) or len(reports) != len(expected):
        raise ValueError(f"Coverage shard report inventory is incomplete: {manifest_path}")

    expected_keys = [(producer.id, report_format) for producer, report_format, _ in expected]
    actual_keys: list[tuple[str, str]] = []
    for record in reports:
        if not isinstance(record, dict) or set(record) != {
            "producer_id",
            "test_project",
            "format",
            "file",
            "size",
            "sha256",
            "candidate_count",
        }:
            raise ValueError(f"Coverage shard report record is invalid: {manifest_path}")
        producer_id = record.get("producer_id")
        report_format = record.get("format")
        actual_keys.append((producer_id, report_format))
        producer = next((item for item in _SHARDS[shard_id] if item.id == producer_id), None)
        if producer is None or record.get("test_project") != producer.test_project:
            raise ValueError(f"Coverage shard producer identity is invalid: {manifest_path}")
        if report_format not in _REPORT_FORMATS:
            raise ValueError(f"Coverage shard report format is invalid: {manifest_path}")
        expected_file = f"{producer_id}/{_REPORT_FORMATS[report_format][0]}"
        if record.get("file") != expected_file:
            raise ValueError(f"Coverage shard canonical report path is invalid: {manifest_path}")
        if not isinstance(record.get("size"), int) or isinstance(record["size"], bool) or record["size"] <= 0:
            raise ValueError(f"Coverage shard report size is invalid: {manifest_path}")
        if not isinstance(record.get("sha256"), str) or not re.fullmatch(r"[0-9a-f]{64}", record["sha256"]):
            raise ValueError(f"Coverage shard report digest is invalid: {manifest_path}")
        if (
            not isinstance(record.get("candidate_count"), int)
            or isinstance(record["candidate_count"], bool)
            or record["candidate_count"] < 1
        ):
            raise ValueError(f"Coverage shard candidate count is invalid: {manifest_path}")
    if actual_keys != expected_keys:
        raise ValueError(f"Coverage shard report order/inventory is invalid: {manifest_path}")
    return manifest


def _canonicalize_shard(arguments: argparse.Namespace) -> None:
    _validate_sha(arguments.source_sha)
    if arguments.shard not in _SHARDS:
        raise ValueError(f"Unknown coverage shard: {arguments.shard}")

    coverage_root = _safe_path(arguments.coverage_root, "coverage root")
    output_root = _safe_path(arguments.output_root, "coverage shard output root")

    shard_output = output_root / arguments.shard
    if shard_output.exists():
        shutil.rmtree(shard_output)
    shard_output.mkdir(parents=True)

    reports: list[dict[str, Any]] = []
    for producer, report_format, filename in _expected_report_records(arguments.shard):
        producer_root = coverage_root / producer.relative_root
        chosen, candidate_count, size, digest = _select_canonical_report(producer_root, report_format)
        destination = shard_output / producer.id / filename
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(chosen, destination)
        copied_size, copied_digest = _validate_report(destination, report_format)
        if copied_size != size or copied_digest != digest:
            raise ValueError(f"Canonical coverage copy changed content: {destination}")
        reports.append(
            {
                "producer_id": producer.id,
                "test_project": producer.test_project,
                "format": report_format,
                "file": f"{producer.id}/{filename}",
                "size": size,
                "sha256": digest,
                "candidate_count": candidate_count,
            }
        )

    manifest = {
        "schema": _SHARD_SCHEMA,
        "source_sha": arguments.source_sha,
        "shard_id": arguments.shard,
        "reports": reports,
    }
    manifest_path = shard_output / "shard-manifest.json"
    _validate_shard_manifest(manifest, manifest_path)
    _write_json(manifest_path, manifest)
    duplicates = sum(record["candidate_count"] - 1 for record in reports)
    print(
        f"Canonicalized coverage shard {arguments.shard}: "
        f"reports={len(reports)}, collector_duplicates_ignored={duplicates}, sha={arguments.source_sha}"
    )


def _verify_shard_files(manifest_path: Path, manifest: dict[str, Any]) -> None:
    for record in manifest["reports"]:
        path = _safe_path(manifest_path.parent / record["file"], "coverage shard report")
        size, digest = _validate_report(path, record["format"])
        if size != record["size"] or digest != record["sha256"]:
            raise ValueError(f"Coverage shard report does not match its manifest: {path}")


def _inventory_outputs(root: Path, inventory: dict[str, Any]) -> dict[str, str]:
    opencover = [root / record["path"] for record in inventory["reports"] if record["format"] == "opencover"]
    cobertura = [root / record["path"] for record in inventory["reports"] if record["format"] == "cobertura"]
    return {
        "source_sha": inventory["source_sha"],
        "shard_count": str(len(inventory["observed_shards"])),
        "expected_shard_count": str(len(inventory["expected_shards"])),
        "opencover_count": str(len(opencover)),
        "cobertura_count": str(len(cobertura)),
        "opencover_files": ",".join(path.as_posix() for path in opencover),
        "cobertura_files": ",".join(path.as_posix() for path in cobertura),
        "inventory_file": (root / "coverage-inventory.json").as_posix(),
    }


def _write_github_outputs(path: Path | None, outputs: dict[str, str]) -> None:
    if path is None:
        return
    with path.open("a", encoding="utf-8") as stream:
        for key, value in outputs.items():
            stream.write(f"{key}={value}\n")


def _validate_inventory(root: Path, inventory: dict[str, Any], expected_sha: str) -> dict[str, Any]:
    _validate_sha(expected_sha)
    if set(inventory) != {"schema", "source_sha", "expected_shards", "observed_shards", "reports"}:
        raise ValueError("Coverage inventory fields are invalid.")
    if inventory.get("schema") != _INVENTORY_SCHEMA:
        raise ValueError("Unsupported coverage inventory schema.")
    if inventory.get("source_sha") != expected_sha:
        raise ValueError(
            f"Coverage inventory SHA is stale/wrong: expected {expected_sha}, observed {inventory.get('source_sha')}"
        )
    if inventory.get("expected_shards") != list(_SHARD_IDS) or inventory.get("observed_shards") != list(_SHARD_IDS):
        raise ValueError("Coverage inventory does not prove the complete 3/3 shard set.")

    reports = inventory.get("reports")
    expected_records = [
        (shard_id, producer, report_format, filename)
        for shard_id in _SHARD_IDS
        for producer, report_format, filename in _expected_report_records(shard_id)
    ]
    if not isinstance(reports, list) or len(reports) != len(expected_records):
        raise ValueError("Coverage inventory report set is incomplete.")

    actual_keys: list[tuple[str, str, str]] = []
    for record in reports:
        if not isinstance(record, dict) or set(record) != {
            "shard_id",
            "producer_id",
            "test_project",
            "format",
            "path",
            "size",
            "sha256",
            "candidate_count",
        }:
            raise ValueError("Coverage inventory report record is invalid.")
        shard_id = record.get("shard_id")
        producer_id = record.get("producer_id")
        report_format = record.get("format")
        actual_keys.append((shard_id, producer_id, report_format))
        if shard_id not in _SHARDS or report_format not in _REPORT_FORMATS:
            raise ValueError("Coverage inventory report identity is invalid.")
        producer = next((item for item in _SHARDS[shard_id] if item.id == producer_id), None)
        if producer is None or record.get("test_project") != producer.test_project:
            raise ValueError("Coverage inventory producer identity is invalid.")
        expected_path = f"{shard_id}/{producer_id}/{_REPORT_FORMATS[report_format][0]}"
        if record.get("path") != expected_path:
            raise ValueError("Coverage inventory canonical path is invalid.")
        path = root / expected_path
        size, digest = _validate_report(path, report_format)
        if size != record.get("size") or digest != record.get("sha256"):
            raise ValueError(f"Canonical coverage report does not match inventory: {path}")
        if (
            not isinstance(record.get("candidate_count"), int)
            or isinstance(record["candidate_count"], bool)
            or record["candidate_count"] < 1
        ):
            raise ValueError("Coverage inventory candidate count is invalid.")

    expected_keys = [(shard, producer.id, report_format) for shard, producer, report_format, _ in expected_records]
    if actual_keys != expected_keys:
        raise ValueError("Coverage inventory report order/inventory is invalid.")
    return inventory


def _assemble(arguments: argparse.Namespace) -> None:
    _validate_sha(arguments.expected_sha)
    artifacts_root = _safe_path(arguments.artifacts_root, "coverage artifacts root")
    output_root = _safe_path(arguments.output_root, "coverage output root")
    github_output = (
        _safe_path(arguments.github_output, _GITHUB_OUTPUT_DESCRIPTION)
        if arguments.github_output is not None
        else None
    )

    manifest_paths = sorted(artifacts_root.rglob("shard-manifest.json"))
    manifests: dict[str, tuple[Path, dict[str, Any]]] = {}
    for path in manifest_paths:
        manifest = _validate_shard_manifest(_read_json(path, "coverage shard manifest"), path)
        shard_id = manifest["shard_id"]
        if shard_id in manifests:
            raise ValueError(f"Duplicate coverage shard manifest for {shard_id}: {path}")
        if manifest["source_sha"] != arguments.expected_sha:
            raise ValueError(
                f"Coverage shard {shard_id} is stale/wrong: "
                f"expected {arguments.expected_sha}, observed {manifest['source_sha']}"
            )
        _verify_shard_files(path, manifest)
        manifests[shard_id] = (path, manifest)

    observed = [shard_id for shard_id in _SHARD_IDS if shard_id in manifests]
    missing = [shard_id for shard_id in _SHARD_IDS if shard_id not in manifests]
    unexpected = sorted(set(manifests) - set(_SHARD_IDS))
    if missing or unexpected or len(manifests) != len(_SHARD_IDS):
        raise ValueError(
            f"Coverage shard inventory is incomplete: observed={len(observed)}/{len(_SHARD_IDS)}, "
            f"missing={missing}, unexpected={unexpected}"
        )

    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True)
    reports: list[dict[str, Any]] = []
    for shard_id in _SHARD_IDS:
        manifest_path, manifest = manifests[shard_id]
        for record in manifest["reports"]:
            source = _safe_path(manifest_path.parent / record["file"], "canonical coverage report")
            relative = Path(shard_id) / record["file"]
            destination = _safe_path(output_root / relative, "coverage inventory destination")
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
            reports.append(
                {
                    "shard_id": shard_id,
                    "producer_id": record["producer_id"],
                    "test_project": record["test_project"],
                    "format": record["format"],
                    "path": relative.as_posix(),
                    "size": record["size"],
                    "sha256": record["sha256"],
                    "candidate_count": record["candidate_count"],
                }
            )

    inventory = {
        "schema": _INVENTORY_SCHEMA,
        "source_sha": arguments.expected_sha,
        "expected_shards": list(_SHARD_IDS),
        "observed_shards": list(_SHARD_IDS),
        "reports": reports,
    }
    _validate_inventory(output_root, inventory, arguments.expected_sha)
    _write_json(output_root / "coverage-inventory.json", inventory)
    outputs = _inventory_outputs(output_root, inventory)
    _write_github_outputs(github_output, outputs)
    print(
        "Coverage inventory complete: "
        f"shards={outputs['shard_count']}/{outputs['expected_shard_count']}, "
        f"opencover={outputs['opencover_count']}, cobertura={outputs['cobertura_count']}, "
        f"sha={outputs['source_sha']}"
    )


def _verify_inventory_command(arguments: argparse.Namespace) -> None:
    inventory_root = _safe_path(arguments.inventory_root, "coverage inventory root")
    github_output = (
        _safe_path(arguments.github_output, _GITHUB_OUTPUT_DESCRIPTION)
        if arguments.github_output is not None
        else None
    )

    inventory_path = inventory_root / "coverage-inventory.json"
    inventory = _read_json(inventory_path, "coverage inventory")
    _validate_inventory(inventory_root, inventory, arguments.expected_sha)
    outputs = _inventory_outputs(inventory_root, inventory)
    _write_github_outputs(github_output, outputs)
    print(
        "Verified canonical coverage inventory: "
        f"shards={outputs['shard_count']}/{outputs['expected_shard_count']}, "
        f"opencover={outputs['opencover_count']}, cobertura={outputs['cobertura_count']}, "
        f"sha={outputs['source_sha']}"
    )


def _verify_sonar(arguments: argparse.Namespace) -> None:
    inventory_root = _safe_path(arguments.inventory_root, "coverage inventory root")
    scanner_log = _safe_path(arguments.scanner_log, "Sonar scanner log")
    analysis_json = _safe_path(arguments.analysis_json, "Sonar project analyses response")
    github_output = (
        _safe_path(arguments.github_output, _GITHUB_OUTPUT_DESCRIPTION)
        if arguments.github_output is not None
        else None
    )

    inventory_path = inventory_root / "coverage-inventory.json"
    inventory = _read_json(inventory_path, "coverage inventory")
    _validate_inventory(inventory_root, inventory, arguments.expected_sha)
    expected_reports = [
        (inventory_root / record["path"]).as_posix()
        for record in inventory["reports"]
        if record["format"] == "opencover"
    ]

    try:
        log = scanner_log.read_text(encoding="utf-8", errors="replace")
    except OSError as error:
        raise ValueError(f"Cannot read Sonar scanner log: {error}") from error
    if "Could not import coverage report" in log or "doesn't contain any coverage data for the included files" in log:
        raise ValueError("Sonar scanner reported a .NET coverage import failure.")
    parsing_lines = [line for line in log.splitlines() if "Parsing the OpenCover report" in line]
    missing_reports = [
        report
        for report in expected_reports
        if not any(report in line or str(Path(report).resolve()) in line for line in parsing_lines)
    ]
    if missing_reports:
        raise ValueError(f"Sonar did not prove parsing every canonical OpenCover report: {missing_reports}")

    covered_main_files = sum(int(match.group(1)) for match in _SONAR_STATS_PATTERN.finditer(log))
    if covered_main_files <= 0:
        raise ValueError("Sonar scanner did not report any covered main .NET files.")

    analysis = _read_json(analysis_json, "Sonar project analyses response")
    analyses = analysis.get("analyses")
    if not isinstance(analyses, list):
        raise ValueError("Sonar project analyses response is missing analyses.")
    matching = [entry for entry in analyses if isinstance(entry, dict) and entry.get("revision") == arguments.expected_sha]
    if not matching:
        revisions = [entry.get("revision") for entry in analyses if isinstance(entry, dict)]
        raise ValueError(
            f"Sonar analysis revision is stale/wrong: expected {arguments.expected_sha}, observed {revisions[:5]}"
        )

    outputs = {
        "analysis_revision": arguments.expected_sha,
        "coverage_import_status": f"{len(expected_reports)}/{len(expected_reports)} canonical OpenCover reports parsed",
        "covered_main_files": str(covered_main_files),
    }
    _write_github_outputs(github_output, outputs)
    print(
        "Verified Sonar coverage import: "
        f"revision={arguments.expected_sha}, reports={len(expected_reports)}/{len(expected_reports)}, "
        f"covered_main_files={covered_main_files}"
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    canonicalize = subparsers.add_parser("canonicalize-shard")
    canonicalize.add_argument("--shard", required=True, choices=_SHARD_IDS)
    canonicalize.add_argument("--source-sha", required=True)
    canonicalize.add_argument("--coverage-root", type=Path, required=True)
    canonicalize.add_argument("--output-root", type=Path, required=True)
    canonicalize.set_defaults(func=_canonicalize_shard)

    assemble = subparsers.add_parser("assemble")
    assemble.add_argument("--artifacts-root", type=Path, required=True)
    assemble.add_argument("--expected-sha", required=True)
    assemble.add_argument("--output-root", type=Path, required=True)
    assemble.add_argument("--github-output", type=Path)
    assemble.set_defaults(func=_assemble)

    verify_inventory = subparsers.add_parser("verify-inventory")
    verify_inventory.add_argument("--inventory-root", type=Path, required=True)
    verify_inventory.add_argument("--expected-sha", required=True)
    verify_inventory.add_argument("--github-output", type=Path)
    verify_inventory.set_defaults(func=_verify_inventory_command)

    verify_sonar = subparsers.add_parser("verify-sonar")
    verify_sonar.add_argument("--inventory-root", type=Path, required=True)
    verify_sonar.add_argument("--expected-sha", required=True)
    verify_sonar.add_argument("--scanner-log", type=Path, required=True)
    verify_sonar.add_argument("--analysis-json", type=Path, required=True)
    verify_sonar.add_argument("--github-output", type=Path)
    verify_sonar.set_defaults(func=_verify_sonar)
    return parser


def main() -> int:
    arguments = _parser().parse_args()
    try:
        arguments.func(arguments)
    except ValueError as error:
        print(f"Coverage evidence error: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
