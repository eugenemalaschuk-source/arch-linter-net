#!/usr/bin/env python3
"""Mechanically verify the ArchLinterNet.Core.Tests unit-shard partition defined in make/test.mk.

Two things this deliberately does NOT do, and why:
  - It does not use `dotnet test --list-tests`: that command silently ignores `--filter` and
    returns the full, unfiltered discovery list regardless of what filter is passed, so it cannot
    validate shard membership. `dotnet vstest <dll> --ListFullyQualifiedTests` is the reliable
    authoritative-discovery source (confirmed by direct experiment - see
    docs/internal/core-unit-shard-inventory.md).
  - It does not re-derive the shard/E2E/packed-artifact token lists anywhere: it parses them
    straight out of make/test.mk (the single-sourced FullyQualifiedName filter fragments already
    documented there), so there is exactly one authored list, never two kept in sync by hand.

Failure modes checked:
  - a shard-1 token matches zero discovered tests (dead/typo'd token - the shard would silently
    cover less than intended, with no test ever failing to reveal it);
  - a shard-1 token's substring match also matches a test already assigned to the E2E or
    packed-artifact bucket (leak - VSTest FullyQualifiedName filters use substring matching, so a
    new shard-1 token could collide with an existing bucket's fixture class name).
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from pathlib import Path

TOKEN_PATTERN = re.compile(r"FullyQualifiedName~([A-Za-z0-9_.]+)")

RED = "\033[31m"
GREEN = "\033[32m"
RESET = "\033[0m"


def extract_tokens(test_mk_text: str, variable: str) -> list[str]:
    match = re.search(rf"^{re.escape(variable)}\s*:=\s*(.+)$", test_mk_text, re.MULTILINE)
    if not match:
        raise ValueError(f"{variable} not found in test.mk")
    return TOKEN_PATTERN.findall(match.group(1))


def discover_fully_qualified_tests(dll: Path) -> list[str]:
    if not dll.exists():
        raise FileNotFoundError(
            f"Core.Tests assembly not found at {dll}. Build ArchLinterNet.Core.Tests before "
            "running this check (the `lint-test-shard-membership` Make target does this for you)."
        )

    with tempfile.TemporaryDirectory() as tmp_dir:
        out_path = Path(tmp_dir) / "core-unit-shard-fqns.txt"
        result = subprocess.run(
            [
                "dotnet",
                "vstest",
                str(dll),
                "--ListFullyQualifiedTests",
                f"--ListTestsTargetPath:{out_path}",
            ],
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            raise RuntimeError(
                "dotnet vstest --ListFullyQualifiedTests failed:\n"
                f"{result.stdout}\n{result.stderr}"
            )
        return [line.strip() for line in out_path.read_text(encoding="utf-8").splitlines() if line.strip()]


def any_token_matches(tokens: list[str], fqn: str) -> bool:
    return any(token in fqn for token in tokens)


def evaluate_shard_membership(
    all_fqns: list[str],
    e2e_tokens: list[str],
    packed_tokens: list[str],
    shard1_tokens: list[str],
) -> tuple[list[str], dict[str, int]]:
    """Pure classification/validation over an already-discovered FQN list.

    Returns (errors, summary_counts). Kept separate from discover_fully_qualified_tests() (which
    shells out to `dotnet vstest`) so this logic is unit-testable against a small fixture FQN list.
    """
    errors: list[str] = []

    dead_tokens = [token for token in shard1_tokens if not any(token in fqn for fqn in all_fqns)]
    for token in dead_tokens:
        errors.append(
            f"shard-1 token '{token}' matches zero discovered tests "
            "(dead or typo'd token - was the fixture class renamed or removed?)"
        )

    leaked = [
        fqn
        for fqn in all_fqns
        if any_token_matches(shard1_tokens, fqn)
        and (any_token_matches(e2e_tokens, fqn) or any_token_matches(packed_tokens, fqn))
    ]
    for fqn in leaked:
        bucket = "E2E" if any_token_matches(e2e_tokens, fqn) else "packed-artifact"
        errors.append(f"'{fqn}' matches a shard-1 token but is already assigned to the {bucket} bucket")

    unit_fqns = [
        fqn
        for fqn in all_fqns
        if not any_token_matches(e2e_tokens, fqn) and not any_token_matches(packed_tokens, fqn)
    ]
    shard1_fqns = [fqn for fqn in unit_fqns if any_token_matches(shard1_tokens, fqn)]
    shard2_fqns = [fqn for fqn in unit_fqns if fqn not in shard1_fqns]

    summary = {
        "discovered": len(all_fqns),
        "e2e": sum(1 for fqn in all_fqns if any_token_matches(e2e_tokens, fqn)),
        "packed_artifact": sum(1 for fqn in all_fqns if any_token_matches(packed_tokens, fqn)),
        "unit": len(unit_fqns),
        "shard1": len(shard1_fqns),
        "shard2": len(shard2_fqns),
    }
    return errors, summary


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Verify the ArchLinterNet.Core.Tests unit-shard partition is complete and leak-free."
    )
    parser.add_argument(
        "--test-mk",
        default="make/test.mk",
        help="Path to make/test.mk (default: make/test.mk, relative to the current directory).",
    )
    parser.add_argument(
        "--dll",
        default=None,
        help=(
            "Path to the built ArchLinterNet.Core.Tests.dll. Defaults to "
            "tests/ArchLinterNet.Core.Tests/bin/Debug/net10.0/ArchLinterNet.Core.Tests.dll "
            "relative to the repository root inferred from --test-mk."
        ),
    )
    args = parser.parse_args()

    test_mk_path = Path(args.test_mk).resolve()
    repo_root = test_mk_path.parent.parent
    test_mk_text = test_mk_path.read_text(encoding="utf-8")

    dll = (
        Path(args.dll)
        if args.dll
        else repo_root
        / "tests"
        / "ArchLinterNet.Core.Tests"
        / "bin"
        / "Debug"
        / "net10.0"
        / "ArchLinterNet.Core.Tests.dll"
    )

    try:
        e2e_tokens = extract_tokens(test_mk_text, "TEST_E2E_FIXTURES")
        packed_tokens = extract_tokens(test_mk_text, "TEST_PACKED_ARTIFACT_FILTER")
        shard1_tokens = extract_tokens(test_mk_text, "TEST_CORE_UNIT_SHARD_1_FIXTURES")
    except ValueError as exc:
        print(f"{RED}ERROR{RESET} {exc}")
        return 1

    try:
        all_fqns = discover_fully_qualified_tests(dll)
    except (FileNotFoundError, RuntimeError) as exc:
        print(f"{RED}ERROR{RESET} {exc}")
        return 1

    errors, summary = evaluate_shard_membership(all_fqns, e2e_tokens, packed_tokens, shard1_tokens)

    if errors:
        print(f"{RED}Core unit shard membership check failed:{RESET}")
        for error in errors:
            print(f"  ERROR {error}")
        return 1

    print(f"{GREEN}Core unit shard membership check passed.{RESET}")
    print(f"  discovered tests:        {summary['discovered']}")
    print(f"  E2E bucket:               {summary['e2e']}")
    print(f"  packed-artifact bucket:   {summary['packed_artifact']}")
    print(f"  unit bucket:              {summary['unit']}")
    print(f"    shard 1:                {summary['shard1']}")
    print(f"    shard 2 (remainder):    {summary['shard2']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
