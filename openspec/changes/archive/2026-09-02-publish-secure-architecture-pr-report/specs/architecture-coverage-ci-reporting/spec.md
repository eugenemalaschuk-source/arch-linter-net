## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Sticky PR comment as a stage of the validate job

**Reason**: The standalone Architecture Coverage comment competes with the Core/CLI-owned unified
architecture PR report and grants comment authority to a pull-request execution job.

**Migration**: Keep the standalone `coverage report` command and coverage artifacts, and use the
manifest-bound unified publisher defined by `architecture-pr-report-publication` for the one
authoritative sticky comment.
