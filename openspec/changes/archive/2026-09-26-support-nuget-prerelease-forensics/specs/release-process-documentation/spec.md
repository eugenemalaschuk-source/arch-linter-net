## MODIFIED Requirements

### Requirement: Release history-forensics procedure is documented

The maintainer release-process documentation SHALL describe the candidate-bound history-forensics job, stable and prerelease range semantics, the typed no-predecessor result, dry-run workflow artifacts, durable GitHub Release assets, candidate identity and digest verification, and a command for reproducing the analysis from the packed candidate package. The documentation SHALL state that accepted `alpha`, `beta`, `rc`, and `preview` versions share a prerelease series for the same `X.Y.Z` line, that SemVer prerelease precedence selects the prior boundary, and that build metadata does not affect ordering.

#### Scenario: Maintainer reviews a dry-run bundle

- **WHEN** a maintainer follows the release-process documentation for a dry run
- **THEN** they can locate the JSON, Markdown, manifest, checksums, and operational observations in the workflow run and understand which fields establish candidate identity

#### Scenario: Maintainer reproduces release history evidence

- **WHEN** a maintainer follows the documented reproduction procedure
- **THEN** they use the exact candidate CLI package and pinned full-SHA range with the supported one-analysis JSON/Markdown command
- **AND** they can distinguish analyzer phase measurements from orchestration observations

#### Scenario: Maintainer understands prerelease range selection

- **WHEN** a maintainer prepares an accepted `alpha`, `beta`, `rc`, or `preview` candidate
- **THEN** the documentation explains how the same `X.Y.Z` prerelease line and SemVer precedence select its history boundary
- **AND** the documentation explains that build metadata identifies the candidate but does not alter version ordering
