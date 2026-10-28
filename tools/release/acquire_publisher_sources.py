#!/usr/bin/env python3
"""Fetch the independently reviewed publisher workflow and action commits."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

import release_distribution


def acquire(repository_root: Path | None = None) -> None:
    """Fetch the approved commits into this checkout's shallow object database."""
    root = repository_root or Path(__file__).resolve().parents[2]
    subprocess.run(
        [
            "git",
            "fetch",
            "--no-tags",
            "--depth=1",
            "origin",
            release_distribution._APPROVED_PUBLISHER_COMMIT,
            release_distribution._APPROVED_PUBLISHER_ACTION_COMMIT,
        ],
        cwd=root,
        check=True,
    )


def main() -> int:
    argparse.ArgumentParser(description=__doc__).parse_args()
    try:
        acquire()
    except (OSError, subprocess.CalledProcessError) as error:
        print(f"publisher source acquisition failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
