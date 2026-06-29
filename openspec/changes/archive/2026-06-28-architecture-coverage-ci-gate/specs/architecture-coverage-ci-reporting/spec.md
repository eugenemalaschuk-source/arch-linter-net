## ADDED Requirements

### Requirement: Strict and audit coverage artifacts are published
The architecture-coverage workflow SHALL run ArchLinterNet against the repository's own policy in both `strict` and `audit` JSON modes and upload the resulting JSON as build artifacts on every pull request and `main` push.

#### Scenario: Strict and audit artifacts are uploaded on success
- **WHEN** the architecture-coverage workflow runs for a pull request or push to `main`
- **THEN** it uploads `architecture-strict.json` and `architecture-audit.json` as build artifacts

#### Scenario: Audit artifact is uploaded even when strict fails
- **WHEN** the strict run reports violations or new non-baselined coverage findings
- **THEN** the audit run still completes and `architecture-audit.json` is still uploaded

### Requirement: Strict mode fails the workflow closed
The architecture-coverage workflow SHALL report an overall failed conclusion when the strict run reports any violation or any new non-baselined coverage finding.

#### Scenario: Strict violation fails the gate
- **WHEN** the strict JSON output has `passed: false`
- **THEN** the architecture-coverage workflow's conclusion is `failure`

#### Scenario: Baselined debt does not fail the gate
- **WHEN** all strict findings are matched by entries in the architecture baseline
- **THEN** the architecture-coverage workflow's conclusion is not `failure` due to those findings

### Requirement: Markdown coverage report generation
A report generator SHALL convert the strict JSON's `coverage_summary` and `coverage_findings` into a Markdown report containing overall status (pass/fail) and covered/excluded/uncovered/stale/unknown counts.

#### Scenario: Zero findings produces a clean pass report
- **WHEN** the strict JSON has empty `coverage_findings` and all `coverage_summary` counts at zero
- **THEN** the generated Markdown reports `Status: pass` and all counts as `0`

#### Scenario: Failed gate is reflected in the report
- **WHEN** the strict JSON has `passed: false`
- **THEN** the generated Markdown reports `Status: fail`

### Requirement: New-code coverage section
The report generator SHALL include a "New-code coverage" section that maps PR-changed first-party files to coverage units (namespace, project, or assembly) and reports only changed units that are uncovered, stale, unknown, or excluded.

#### Scenario: Changed file maps to a known coverage unit
- **WHEN** a changed file's namespace, project, or assembly matches an entry in `coverage_summary`
- **AND** that entry is not covered
- **THEN** the New-code coverage section lists the unit with its coverage state

#### Scenario: Changed file cannot be reliably mapped
- **WHEN** a changed file's namespace cannot be determined and no enclosing project/assembly match is found in `coverage_summary`
- **THEN** the New-code coverage section reports that file's unit as `unknown` rather than `covered`

#### Scenario: Changed file maps to a fully covered unit
- **WHEN** a changed file maps to a coverage unit that is fully covered
- **THEN** the New-code coverage section does not list that unit as a problem

### Requirement: Sticky PR comment
The architecture-coverage workflow SHALL post the generated Markdown report as a pull request comment on `pull_request` events, identifying its own prior comment via a hidden marker and updating it in place instead of creating a new comment on subsequent pushes.

#### Scenario: First push creates a comment
- **WHEN** the architecture-coverage workflow runs for a pull request with no prior bot comment containing the marker
- **THEN** it creates a new comment containing the marker and the report

#### Scenario: Subsequent push updates the existing comment
- **WHEN** the architecture-coverage workflow runs again for the same pull request and a comment containing the marker already exists
- **THEN** it updates that existing comment instead of creating a new one

### Requirement: Report generator is tested
The report generator SHALL have script-level tests covering JSON parsing, Markdown generation, zero-findings output, failed-gate output, and unknown-mapping behavior.

#### Scenario: Test suite covers required cases
- **WHEN** the report generator's test suite is run
- **THEN** it includes passing tests for JSON parsing, Markdown generation, the zero-findings case, the failed-gate case, and the unknown-mapping case
