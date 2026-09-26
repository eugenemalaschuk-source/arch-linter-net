## ADDED Requirements

### Requirement: Release history-forensics procedure is documented
The maintainer release-process documentation SHALL describe the candidate-bound history-forensics job, stable and preview range semantics, the typed no-predecessor result, dry-run workflow artifacts, durable GitHub Release assets, candidate identity and digest verification, and a command for reproducing the analysis from the packed candidate package.

#### Scenario: Maintainer reviews a dry-run bundle
- **WHEN** a maintainer follows the release-process documentation for a dry run
- **THEN** they can locate the JSON, Markdown, manifest, checksums, and operational observations in the workflow run and understand which fields establish candidate identity

#### Scenario: Maintainer reproduces release history evidence
- **WHEN** a maintainer follows the documented reproduction procedure
- **THEN** they use the exact candidate CLI package and pinned full-SHA range with the supported one-analysis JSON/Markdown command
- **AND** they can distinguish analyzer phase measurements from orchestration observations
