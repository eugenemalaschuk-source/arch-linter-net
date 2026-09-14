# sonarcloud-debt-inventory Specification

## Purpose
TBD - created by archiving change reduce-post-v0-8-sonar-debt. Update Purpose after archive.

## Requirements

### Requirement: Revision-bound reproducible debt inventory

The repository SHALL provide a tool that produces a deterministic, fully paginated inventory of the
project's open SonarCloud findings, security hotspots, measures and quality-gate status for a
caller-specified analysis revision. The inventory SHALL record the analysis date, project key,
branch, analyzed revision, quality-gate status and conditions, and the analyzed/excluded area
separately from remediated code. The tool SHALL fail with a non-zero exit code rather than present a
baseline when the requested revision does not match a completed analysis of the default branch.

#### Scenario: Inventory is bound to the analysed revision

- **WHEN** the tool runs against a project whose latest completed analysis revision differs from the
  requested revision
- **THEN** it reports the mismatch and exits non-zero instead of presenting a baseline

#### Scenario: Full pagination is honoured

- **WHEN** the project has more findings than one API page
- **THEN** the tool retrieves every page and the inventory total equals the total the project reports
  for the same branch and resolution filter

#### Scenario: Analysis identity is captured, not inferred

- **WHEN** the inventory is written
- **THEN** it contains the analysis date, revision, branch, quality-gate status and each
  quality-gate condition's metric, comparator, threshold and actual value

### Requirement: Triage report separates fixes, dispositions and exclusions

The inventory report SHALL classify every finding by rule key, component, line, type, severity,
quality/impact and estimated debt, and SHALL record a disposition for each finding. Dispositions SHALL
distinguish remediated code from individually reviewed false-positive/accepted decisions and from
analyzed exclusions, and SHALL NOT treat an unexplained suppression, a disabled rule, a weaker
profile/gate or an exclusion as a fix.

#### Scenario: No anonymous residual

- **WHEN** a finding has neither a code remediation nor a reviewed disposition
- **THEN** the report marks it as untriaged rather than omitting or silently accepting it

#### Scenario: Reviewed disposition names its evidence

- **WHEN** a finding is recorded as a reviewed false positive
- **THEN** the report records its rule key, component, line and the specific guard or reasoning that
  justifies the disposition, and does not cover other findings by the same rule
