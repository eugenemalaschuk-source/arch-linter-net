#!/usr/bin/env python3
"""Require a successful SonarCloud Quality Gate for trusted pull requests."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

from _release_workspace import _github_command_file_path

_GITHUB_OUTPUT_DESCRIPTION = "GitHub output file"
_PASSED_PATTERN = re.compile(r"QUALITY GATE STATUS: (?:PASSED|OK)\b")


def _quality_gate_status(scanner_log: str) -> str:
    if "QUALITY GATE STATUS: FAILED" in scanner_log:
        return "failed"
    if _PASSED_PATTERN.search(scanner_log):
        return "passed"
    return "unknown"


def _require_success(scanner_log: str, scanner_exit_code: int) -> str:
    status = _quality_gate_status(scanner_log)
    if scanner_exit_code != 0:
        raise ValueError(
            f"SonarScanner exited with code {scanner_exit_code}; Quality Gate status is {status}."
        )
    if status != "passed":
        raise ValueError(f"SonarCloud Quality Gate status is {status}; refusing a successful PR check.")
    return status


def _write_quality_gate_output(output_path: Path | None, status: str) -> None:
    if output_path is None:
        return
    output_path = _github_command_file_path(output_path, _GITHUB_OUTPUT_DESCRIPTION, "GITHUB_OUTPUT")
    with output_path.open("a", encoding="utf-8") as output:
        output.write(f"quality_gate={status}\n")


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scanner-exit-code", type=int, required=True)
    parser.add_argument("--github-output", type=Path)
    return parser


def main() -> int:
    arguments = _parser().parse_args()
    scanner_log = sys.stdin.read()
    status = _quality_gate_status(scanner_log)
    try:
        _write_quality_gate_output(arguments.github_output, status)
        _require_success(scanner_log, arguments.scanner_exit_code)
    except ValueError as error:
        print(f"Sonar Quality Gate verification failed: {error}", file=sys.stderr)
        return 1

    print("Verified SonarCloud Quality Gate: passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
