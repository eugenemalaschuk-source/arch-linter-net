# architecture-coverage-ci-reporting Specification

## Purpose
Operationalize architecture coverage as a CI quality signal: publish strict/audit JSON artifacts, generate a Markdown coverage report (including new-code coverage), and post it as a pull request comment with minimal write-permission exposure.

## Requirements
### Requirement: Strict and audit coverage artifacts are published
The `validate` job in `.github/workflows/ci.yml` SHALL run ArchLinterNet against the repository's own policy in both `strict` and `audit` JSON modes and upload the resulting JSON as build artifacts on every pull request and `main` push.

#### Scenario: Strict and audit artifacts are uploaded on success
- **WHEN** the `validate` job runs for a pull request or push to `main`
- **THEN** it uploads `architecture-strict.json` and `architecture-audit.json` as build artifacts

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
A report generator (`tools/scripts/architecture_coverage_report.py`) SHALL convert the strict JSON's `coverage_summary` and `coverage_findings` into a Markdown report containing overall status (pass/fail) and covered/excluded/uncovered/stale/unknown counts.

#### Scenario: Zero findings produces a clean pass report
- **WHEN** the strict JSON has empty `coverage_findings` and all `coverage_summary` counts at zero
- **THEN** the generated Markdown reports a passing status and all counts as `0`

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

### Requirement: Sticky PR comment with minimal write permission
A dedicated `comment` job in `.github/workflows/ci.yml`, separate from the `validate` job that runs build/test/lint code, SHALL post the generated Markdown report as a pull request comment on `pull_request` events, identifying its own prior comment via a hidden marker and updating it in place instead of creating a new comment on subsequent pushes. Only the `comment` job SHALL be granted `pull-requests: write`; the `validate` job SHALL remain `contents: read`.

#### Scenario: First push creates a comment
- **WHEN** the `comment` job runs for a pull request with no prior bot comment containing the marker
- **THEN** it creates a new comment containing the marker and the report

#### Scenario: Subsequent push updates the existing comment
- **WHEN** the `comment` job runs again for the same pull request and a comment containing the marker already exists
- **THEN** it updates that existing comment instead of creating a new one

#### Scenario: Build/test/lint steps run without pull-requests: write
- **WHEN** the `validate` job runs
- **THEN** it does not have `pull-requests: write` permission

### Requirement: Report generator is tested
The report generator SHALL have script-level tests covering JSON parsing, Markdown generation, zero-findings output, failed-gate output, unknown-mapping behavior, and deriving `covered` from real `covered_items` evidence.

#### Scenario: Test suite covers required cases
- **WHEN** the report generator's test suite is run
- **THEN** it includes passing tests for JSON parsing, Markdown generation, the zero-findings case, the failed-gate case, the unknown-mapping case, and the covered-from-evidence case
