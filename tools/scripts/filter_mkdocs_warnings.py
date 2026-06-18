#!/usr/bin/env python3
"""Run a command and filter out the mkdocs-material MkDocs 2.0 advisory banner from stderr."""
import subprocess
import sys

ADVISORY_LINE = "mkdocs-material - MkDocs 2.0 requires a configuration change"
COLOR_RESET = "\x1b[0m"

def filter_stderr(line: str) -> bool:
    if "Warning" in line and ADVISORY_LINE in line:
        return True
    if COLOR_RESET in line or "\x1b[31m" in line:
        return True
    return False

def main() -> None:
    if len(sys.argv) < 2:
        print("Usage: filter_mkdocs_warnings.py -- <command> [args...]", file=sys.stderr)
        sys.exit(1)

    # Support -- separator (everything after --), or just pass args directly
    if sys.argv[1] == "--":
        cmd = sys.argv[2:]
    else:
        cmd = sys.argv[1:]

    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    stdout, stderr = proc.communicate()

    # Write stdout as-is
    sys.stdout.write(stdout)

    # Filter stderr, preserving non-advisory warnings
    for line in stderr.splitlines(keepends=True):
        if not filter_stderr(line.strip()):
            sys.stderr.write(line)

    sys.exit(proc.returncode)

if __name__ == "__main__":
    main()
