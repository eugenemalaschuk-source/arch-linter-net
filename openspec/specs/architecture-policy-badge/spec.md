# architecture-policy-badge Specification

## Purpose
Expose the repository's strict self-policy result through a stable badge source.
## Requirements
### Requirement: Dynamic strict self-policy badge
The repository SHALL expose an ArchLinterNet-specific README badge sourced from
the latest `main` run of a dedicated GitHub Actions workflow that performs the
authoritative strict self-policy validation. The badge SHALL communicate
`architecture policy` status and SHALL not represent test coverage or
architecture-coverage percentage.

#### Scenario: Default branch strict self-policy passes
- **WHEN** the dedicated architecture-policy workflow completes successfully on
  `main`
- **THEN** the README badge renders the workflow's passing status for `main`
- **AND** the linked workflow shows that `badge architecture-policy` passed

#### Scenario: Strict self-policy fails
- **WHEN** `badge architecture-policy` fails in a `main` workflow run
- **THEN** the workflow has a failing conclusion
- **AND** its `main` status badge does not report a passing architecture-policy
  result

### Requirement: Architecture-policy badge remains publication-free
The architecture-policy badge source SHALL be refreshed by GitHub Actions
workflow status and SHALL NOT require a generated README commit, a committed
badge payload, GitHub Pages deployment from CI, package publication, or release
creation.

#### Scenario: Main workflow refreshes the badge source
- **WHEN** code is pushed to `main`
- **THEN** the architecture-policy workflow runs strict self-policy validation
- **AND** GitHub updates the workflow-status badge source without a repository
  content write

### Requirement: Badge payload is available from the standard CLI
The repository's architecture-policy workflow SHALL use the native
`badge architecture-policy` CLI command to project the strict result produced
by central CI. The command SHALL be usable by other repositories without copying
a Python script or triggering another analysis.

#### Scenario: Workflow produces the payload through CLI
- **WHEN** the dedicated architecture-policy workflow runs
- **THEN** it invokes `badge architecture-policy` with the strict JSON artifact
- **AND** the workflow status and the command's payload represent the same
  strict-policy outcome
