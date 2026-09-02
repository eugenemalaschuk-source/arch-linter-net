# architecture-coverage-ci-reporting Specification

## Purpose
Operationalize architecture coverage as a CI quality signal: publish strict/audit JSON artifacts
and generate a standalone Markdown coverage report (including new-code coverage), while the
separate unified architecture PR publisher owns the repository sticky comment.
## Requirements
### Requirement: Strict and audit coverage artifacts are published
The read-only architecture report producer job in `.github/workflows/ci.yml` SHALL run
ArchLinterNet against the repository's own policy in both `strict` and `audit` JSON modes and
upload `architecture-strict.json` and `architecture-audit.json` as pull-request build artifacts.
It SHALL preserve the standalone coverage-report artifacts independently of unified PR report
publication.

#### Scenario: Strict and audit artifacts are uploaded when results are available
- **WHEN** the architecture report producer runs for a pull request
- **THEN** it uploads `architecture-strict.json` and `architecture-audit.json` when each has been
  materialized
- **AND** it retains only read permissions while doing so

#### Scenario: Audit artifact is uploaded even when strict fails
- **WHEN** the strict run reports violations or new non-baselined coverage findings
- **THEN** the audit run still completes and `architecture-audit.json` is still uploaded

### Requirement: Strict mode fails the job closed
The `validate` job SHALL report an overall failed conclusion when the strict run reports any violation or any new non-baselined coverage finding.

#### Scenario: Strict violation fails the gate
- **WHEN** the strict JSON output has `passed: false`
- **THEN** the `validate` job's conclusion is `failure`

#### Scenario: Baselined debt does not fail the gate
- **WHEN** all strict findings are matched by entries in the architecture baseline
- **THEN** the `validate` job's conclusion is not `failure` due to those findings

### Requirement: Markdown coverage report generation
A native `coverage report` CLI command SHALL convert strict JSON's
`coverage_summary` and coverage findings into a Markdown report containing
overall status (pass/fail) and covered/excluded/uncovered/stale/unknown counts.

#### Scenario: Zero findings produces a clean pass report
- **WHEN** the CLI report command receives strict JSON with empty
  `coverage_findings` and all `coverage_summary` counts at zero
- **THEN** the generated Markdown reports a passing status and all counts as
  `0`

#### Scenario: Failed gate is reflected in the report
- **WHEN** the strict JSON has `passed: false`
- **THEN** the generated Markdown reports a failing status

### Requirement: Covered units are derived from positive evidence
Each coverage contract's JSON output SHALL include a `covered_items` list (alongside `excluded_items`/`uncovered_items`/`stale_items`/`unknown_items`) naming the specific units found covered, so downstream tooling can classify a unit as covered from explicit evidence rather than from the absence of a problem entry.

#### Scenario: Covered assembly/namespace/project units are named explicitly
- **WHEN** a coverage contract's analysis finds a unit covered by a declared layer
- **THEN** that unit appears in the contract's `covered_items` list with supporting evidence

#### Scenario: Report generator does not infer covered from absent evidence
- **WHEN** a changed file's namespace/project/assembly does not appear in any of a contract's `covered_items`, `excluded_items`, `uncovered_items`, `stale_items`, or `unknown_items`
- **THEN** the report generator classifies that unit as `unknown`, not `covered`

### Requirement: New-code coverage section
The report generator SHALL include a "New-code coverage" section that maps PR-changed first-party files to coverage units (namespace, project, or assembly) and reports only changed units that are uncovered, stale, unknown, or excluded.

#### Scenario: Changed file maps to a known coverage unit
- **WHEN** a changed file's namespace, project, or assembly matches a problem entry (excluded/uncovered/stale/unknown) in `coverage_summary`
- **THEN** the New-code coverage section lists the unit with its coverage state

#### Scenario: Changed file cannot be reliably mapped
- **WHEN** a changed file's namespace cannot be determined, or no `covered_items`/`excluded_items`/`uncovered_items`/`stale_items`/`unknown_items` entry matches it in any configured coverage contract
- **THEN** the New-code coverage section reports that file's unit as `unknown` rather than `covered`

#### Scenario: Changed file maps to a unit named in covered_items
- **WHEN** a changed file maps to a coverage unit present in a contract's `covered_items`
- **THEN** the New-code coverage section does not list that unit as a problem

### Requirement: New-code coverage is restricted to configured scopes
The report generator SHALL only classify a changed file against a coverage scope (namespace, project, or assembly) that has at least one coverage contract configured in `coverage_summary`. A scope with no configured contract SHALL NOT be reported as `unknown` for that file.

#### Scenario: Unconfigured scope produces no finding
- **WHEN** the policy defines a namespace-scope coverage contract but no project-scope coverage contract
- **AND** a changed `.cs` file resolves to a `.csproj`
- **THEN** the New-code coverage section reports the file's namespace classification but does not report a project-scope finding for that file

#### Scenario: Multiple configured scopes are each evaluated independently
- **WHEN** the policy configures both namespace-scope and project-scope coverage contracts
- **AND** a changed file's namespace is covered while its containing project is uncovered
- **THEN** the New-code coverage section reports both the covered namespace and the uncovered project, rather than stopping at the first matching scope

### Requirement: Diff failures are reported explicitly, not as zero changed files
When the CI step that computes the changed-files diff fails (e.g. a `git diff`/fetch error), the report generator SHALL render an explicit "diff unavailable" message in the New-code coverage section instead of silently treating the failure as zero changed files.

#### Scenario: Failed diff renders as unavailable
- **WHEN** the changed-files diff step fails
- **THEN** the generated Markdown's New-code coverage section states the diff is unavailable
- **AND** it does not state `Changed first-party files: 0`

#### Scenario: Diff step failure does not silently pass
- **WHEN** the `Collect changed first-party files` step in `ci.yml` fails
- **THEN** the step's own outcome reflects failure (it is not suppressed with `|| true`)
- **AND** that failed outcome is passed to the report generator as `--diff-status failed`

### Requirement: Report generator is tested
The native CLI report renderer SHALL have NUnit tests covering JSON parsing,
Markdown generation, zero-findings output, failed-gate output, unknown-mapping
behavior, and deriving `covered` from real `covered_items` evidence.

#### Scenario: Test suite covers required cases
- **WHEN** the CLI test suite is run
- **THEN** it includes passing tests for JSON parsing, Markdown generation, the zero-findings case, the failed-gate case, the unknown-mapping case, and the covered-from-evidence case
