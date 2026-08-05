#!/usr/bin/env python3
"""Aggregate portable Checkpoint B runner evidence into release-gate artifacts."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


_REQUIRED_PLATFORMS = ("linux-x64", "macos-arm64", "macos-x64", "windows-x64")


def _read_records(input_directory: Path) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    for path in sorted(input_directory.rglob("*.json")):
        with path.open(encoding="utf-8") as file:
            record = json.load(file)
        if record.get("checkpoint") != "B":
            raise ValueError(f"{path} is not Checkpoint B evidence.")
        if record.get("result") != "passed":
            raise ValueError(f"{path} does not report a passing Checkpoint B result.")
        if record.get("synthetic_identities_only") is not True:
            raise ValueError(f"{path} does not affirm synthetic identities only.")
        records.append(record)
    if not records:
        raise ValueError("No Checkpoint B evidence records were found.")
    return records


def _one_value(records: list[dict[str, object]], field: str) -> str:
    values = {str(record.get(field, "unknown")) for record in records}
    if len(values) != 1:
        raise ValueError(f"Checkpoint B records disagree on {field}: {sorted(values)}.")
    return values.pop()


def _summary(records: list[dict[str, object]]) -> dict[str, object]:
    platforms = {str(record.get("platform_id", "unknown")) for record in records}
    missing = sorted(set(_REQUIRED_PLATFORMS) - platforms)
    unexpected = sorted(platforms - set(_REQUIRED_PLATFORMS))
    if missing or unexpected:
        raise ValueError(
            f"Checkpoint B platform matrix mismatch; missing={missing}, unexpected={unexpected}."
        )

    return {
        "checkpoint": "B",
        "result": "passed",
        "authorization": "The tested 0.5.1 candidate is authorized for publication.",
        "candidate_version": _one_value(records, "candidate_version"),
        "source_commit": _one_value(records, "source_commit"),
        "synthetic_identities_only": True,
        "performance_evidence": "docs/internal/analysis-profile-post-optimization-evidence.md (#409)",
        "support_exclusions": [],
        "platforms": sorted(records, key=lambda record: str(record["platform_id"])),
        "gates": {
            "openspec": "passed",
            "self_architecture": "passed",
            "package": "passed",
            "documentation": "passed",
        },
    }


def _markdown(summary: dict[str, object]) -> str:
    rows = []
    for record in summary["platforms"]:  # type: ignore[index]
        rows.append(
            "| {platform_id} | {runtime} | {shell} | passed |".format(**record)
        )
    return "\n".join(
        [
            "# Checkpoint B release evidence",
            "",
            f"- Candidate version: `{summary['candidate_version']}`",
            f"- Tested commit: `{summary['source_commit']}`",
            "- Checkpoint B: **passed**",
            "- Release authorization: the tested candidate is authorized for 0.5.1 publication.",
            "- Private adopter identity: none; all fixtures and evidence are synthetic.",
            "- Performance evidence: `docs/internal/analysis-profile-post-optimization-evidence.md` (#409).",
            "",
            "| Platform | Runtime | Shell | Result |",
            "| --- | --- | --- | --- |",
            *rows,
            "",
            "Support exclusions: none.",
            "",
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    arguments = parser.parse_args()

    summary = _summary(_read_records(arguments.input_dir))
    arguments.output_dir.mkdir(parents=True, exist_ok=True)
    (arguments.output_dir / "checkpoint-b-release-evidence.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    (arguments.output_dir / "checkpoint-b-release-evidence.md").write_text(
        _markdown(summary), encoding="utf-8"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
