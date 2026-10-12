from __future__ import annotations

from io import StringIO
import sys

import pytest

import verify_sonar_quality_gate as verifier
from verify_sonar_quality_gate import _quality_gate_status, _require_success


@pytest.mark.parametrize("status_line", ["QUALITY GATE STATUS: PASSED", "QUALITY GATE STATUS: OK"])
def test_successful_sonar_gate_is_accepted(status_line: str) -> None:
    log = f"INFO: Analysis completed\n{status_line}\n"

    assert _quality_gate_status(log) == "passed"
    assert _require_success(log, 0) == "passed"


def test_failed_sonar_gate_is_rejected() -> None:
    log = "INFO: Analysis completed\nQUALITY GATE STATUS: FAILED\n"

    assert _quality_gate_status(log) == "failed"
    with pytest.raises(ValueError, match="Quality Gate status is failed"):
        _require_success(log, 0)


def test_missing_sonar_gate_result_is_rejected() -> None:
    log = "INFO: Analysis completed without a Quality Gate result\n"

    assert _quality_gate_status(log) == "unknown"
    with pytest.raises(ValueError, match="Quality Gate status is unknown"):
        _require_success(log, 0)


def test_scanner_failure_is_rejected_even_when_gate_text_says_passed() -> None:
    log = "QUALITY GATE STATUS: PASSED\n"

    with pytest.raises(ValueError, match="SonarScanner exited with code 1"):
        _require_success(log, 1)


@pytest.mark.parametrize(
    ("scanner_log", "scanner_exit_code", "expected_status", "expected_exit_code"),
    [
        ("QUALITY GATE STATUS: PASSED\n", 0, "passed", 0),
        ("QUALITY GATE STATUS: FAILED\n", 0, "failed", 1),
        ("Analysis completed without a Gate result\n", 0, "unknown", 1),
    ],
)
def test_cli_records_gate_status_and_fails_closed(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path,
    scanner_log: str,
    scanner_exit_code: int,
    expected_status: str,
    expected_exit_code: int,
) -> None:
    runner_temp = tmp_path / "runner-temp"
    runner_temp.mkdir()
    github_output = runner_temp / "github-output.txt"
    monkeypatch.setenv("GITHUB_OUTPUT", str(github_output))
    monkeypatch.setattr(sys, "stdin", StringIO(scanner_log))
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "verify_sonar_quality_gate.py",
            "--scanner-exit-code",
            str(scanner_exit_code),
            "--github-output",
            str(github_output),
        ],
    )

    assert verifier.main() == expected_exit_code
    assert f"quality_gate={expected_status}" in github_output.read_text(encoding="utf-8")
