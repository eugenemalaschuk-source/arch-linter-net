## MODIFIED Requirements

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
