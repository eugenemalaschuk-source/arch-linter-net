## ADDED Requirements

### Requirement: Path-typed CLI arguments are confined to the release workspace

Every script under `tools/release/` that accepts a `Path`-typed CLI argument SHALL pass it through
the shared `_release_workspace._safe_path` confinement check before using it to read or write on the
filesystem. `_safe_path` SHALL accept a path only when it resolves inside the current working
directory or the repository root, and SHALL raise `ValueError` naming the offending argument
otherwise. A script SHALL surface that `ValueError` as a CLI error with a non-zero exit code rather
than letting it propagate as an unhandled traceback, wherever the script already wraps its own
argument handling in an error-reporting boundary.

#### Scenario: A path inside the working tree is accepted

- **WHEN** `main_quality_coverage.py` or `verify_restored_main_packages.py` is invoked with a
  `Path`-typed argument that resolves inside the current working directory or the repository root
- **THEN** the script proceeds to read or write at that path exactly as before this requirement was
  enforced

#### Scenario: A path outside the release workspace is rejected

- **WHEN** `main_quality_coverage.py` or `verify_restored_main_packages.py` is invoked with a
  `Path`-typed argument that resolves outside both the current working directory and the repository
  root
- **THEN** the script rejects it with a `ValueError` identifying the argument by description, before
  any filesystem read or write is attempted at that path

#### Scenario: Rejection surfaces as a clean CLI error, not a traceback

- **WHEN** `verify_restored_main_packages.py`'s `main()` receives a `--assets` path that `_safe_path`
  rejects
- **THEN** the CLI prints `Error: ...` to stderr and exits with status `2`, matching how the same
  entry point already reports other `ValueError`s from `verify_restored_main_packages()`
