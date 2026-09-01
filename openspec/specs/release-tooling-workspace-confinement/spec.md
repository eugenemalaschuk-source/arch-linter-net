# release-tooling-workspace-confinement Specification

## Purpose
TBD - created by archiving change fix-sonarcloud-new-code-debt. Update Purpose after archive.
## Requirements
### Requirement: Path-typed CLI arguments are confined to the release workspace

Every script under `tools/release/` that accepts a `Path`-typed CLI argument SHALL pass it through
the shared `_release_workspace._safe_path` confinement check before using it to read or write on the
filesystem, **except** an argument that carries a GitHub Actions runner command-file transport path
(for example `--github-output`/`$GITHUB_OUTPUT` or a sibling `--github-env`/`$GITHUB_ENV`
argument), or an explicitly bounded read-only GitHub runner-temp verification input, which SHALL
instead be validated through the relevant GitHub runner trust boundary defined below. `_safe_path`
SHALL accept a path only when it resolves inside the current working directory or the repository
root, and SHALL raise `ValueError` naming the offending argument otherwise. A script SHALL surface
that `ValueError` as a CLI error with a non-zero exit code rather than letting it propagate as an
unhandled traceback, wherever the script already wraps its own argument handling in an
error-reporting boundary.

#### Scenario: A path inside the working tree is accepted

- **WHEN** `main_quality_coverage.py` or `verify_restored_main_packages.py` is invoked with a
  `Path`-typed argument that resolves inside the current working directory or the repository root
- **THEN** the script proceeds to read or write at that path exactly as before this requirement was
  enforced

#### Scenario: A path outside the release workspace is rejected

- **WHEN** `main_quality_coverage.py` or `verify_restored_main_packages.py` is invoked with a
  `Path`-typed argument, other than a GitHub runner command-file transport argument or explicitly
  bounded read-only GitHub runner-temp verification input, that resolves outside both the current
  working directory and the repository root
- **THEN** the script rejects it with a `ValueError` identifying the argument by description, before
  any filesystem read or write is attempted at that path

#### Scenario: Rejection surfaces as a clean CLI error, not a traceback

- **WHEN** `verify_restored_main_packages.py`'s `main()` receives a `--assets` path that `_safe_path`
  rejects
- **THEN** the CLI prints `Error: ...` to stderr and exits with status `2`, matching how the same
  entry point already reports other `ValueError`s from `verify_restored_main_packages()`

### Requirement: GitHub runner command-file transport arguments use an environment-anchored trust boundary

A release script MUST use an environment-anchored trust boundary for a GitHub Actions runner
command-file transport argument such as `--github-output` or `--github-env`. Such an argument in a
`tools/release/` script SHALL NOT be confined via `_safe_path`'s repository-workspace check, because
the runner legitimately creates these files outside the checkout (under its own temp directory).
Instead, the script SHALL validate the argument via
`_release_workspace._github_command_file_path(value, description, env_var)`, which SHALL accept the
path only when it resolves to exactly the value of the named environment variable (`env_var`, e.g.
`GITHUB_OUTPUT`) currently set in the process environment, and SHALL raise `ValueError` naming the
argument by description otherwise — including when `env_var` is unset. A path merely labelled as a
runner command-file argument on the command line SHALL NOT be trusted on that basis alone.

#### Scenario: The runner-provided transport path is accepted

- **WHEN** a script is invoked with `--github-output <path>` where `<path>` resolves to exactly the
  value of the `GITHUB_OUTPUT` environment variable set in the process environment, even when that
  path resolves outside the current working directory and the repository root
- **THEN** the script accepts the path and writes its `key=value` outputs to it

#### Scenario: An arbitrary path is rejected even when labelled as a runner command file

- **WHEN** a script is invoked with `--github-output <path>` where `<path>` does not resolve to the
  current value of the `GITHUB_OUTPUT` environment variable (including when `GITHUB_OUTPUT` is
  unset)
- **THEN** the script rejects it with a `ValueError` identifying the argument by description, before
  any filesystem read or write is attempted at that path

#### Scenario: Tests can inject a deterministic synthetic runner command path

- **WHEN** a test sets the `GITHUB_OUTPUT` environment variable to a synthetic path (including one
  outside the working tree) and passes that same path as `--github-output`
- **THEN** `_github_command_file_path` accepts it, without weakening the production check for any
  path that does not match the environment variable

### Requirement: Read-only runner-temp verification inputs use a root-anchored trust boundary

A read-only GitHub Actions runner-temp verification input SHALL NOT be accepted through `_safe_path`
when it is outside the checkout. Instead, a release script SHALL validate it through a dedicated
environment-anchored helper that resolves both the candidate path and the current `RUNNER_TEMP`
value, accepts the candidate only when it remains contained by that resolved root, and raises a
`ValueError` naming the argument otherwise. The boundary SHALL not accept paths solely because they
share a filename with a workflow log, and it SHALL remain limited to explicitly designated
read-only verification inputs. Sonar verification SHALL treat both the scanner log and the Sonar
project analyses response as designated inputs under this boundary.

#### Scenario: The workflow-owned Sonar scanner log is accepted

- **WHEN** `main_quality_coverage.py verify-sonar` receives a scanner log inside the current resolved
  `RUNNER_TEMP` root
- **THEN** it reads that log for canonical coverage-import proof and current-SHA verification without
  treating the log as a release-workspace path

#### Scenario: The workflow-owned Sonar analyses response is accepted

- **WHEN** `main_quality_coverage.py verify-sonar` receives `sonar-project-analyses.json` inside the
  current resolved `RUNNER_TEMP` root, even when that root is outside the release workspace
- **THEN** it reads the response for current-revision verification without treating it as a
  release-workspace path

#### Scenario: Arbitrary external Sonar verification inputs are rejected

- **WHEN** `main_quality_coverage.py verify-sonar` receives either a scanner-log path or an analyses
  response path outside the current resolved `RUNNER_TEMP` root, including a temporary,
  home-directory, sibling-workspace, traversal, or symlink-resolved external path
- **THEN** it raises a `ValueError` before reading that input

#### Scenario: A missing or mismatched runner context fails closed

- **WHEN** `RUNNER_TEMP` is unset or either Sonar verification input is not contained by its
  resolved value
- **THEN** `main_quality_coverage.py verify-sonar` rejects the input with a `ValueError` and does
  not read it
