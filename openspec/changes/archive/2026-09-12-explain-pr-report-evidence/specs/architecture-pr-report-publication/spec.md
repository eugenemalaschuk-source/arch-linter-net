## MODIFIED Requirements

### Requirement: A read-only CI producer emits the canonical unified report artifact
The pull-request CI workflow SHALL run the existing ArchLinterNet CLI to create compatible current/base architecture-change snapshots, an `architecture-health/v1` artifact, and the complete `architecture-pr-report.md` Markdown projection. It SHALL upload the rendered Markdown and a fixed `architecture-pr-report.manifest.json` as one named artifact. The producer SHALL retain only read repository permissions and SHALL not create, update, or delete pull-request comments.

The workflow SHALL use one non-empty execution context for the Health and change artifacts. The manifest SHALL declare a closed publication schema, report artifact kind, marker version, fixed Markdown file path, SHA-256, byte count, repository, PR number, head SHA, producer workflow run ID, and producer run attempt. The producer MAY pass an explicitly constructed, repository/run/head-bound GitHub Actions artifact-bundle URL as transport navigation context to the CLI. Workflow glue SHALL not render Markdown, derive Architecture Health, recount evidence, or implement remediation/business rules.

#### Scenario: Report rendering precedes publication
- **WHEN** the required canonical producer artifacts are complete for a pull request
- **THEN** the producer invokes `arch-linter-net report pr` before it uploads the report artifact
- **AND** the uploaded report bytes are the deterministic CLI-rendered Markdown without workflow-owned report composition

#### Scenario: Bounded report links are run-bound transport context
- **WHEN** the producer provides a report-bundle navigation URL to the CLI
- **THEN** it constructs that URL only from the trusted repository identifier, current head SHA, and producer run context
- **AND** the publisher continues to validate and publish only the manifest-bound Markdown bytes without interpreting the URL as governance evidence

#### Scenario: A failed strict architecture result is still rendered canonically
- **WHEN** Architecture Health has a valid failing or unassessable gate result that the CLI can serialize
- **THEN** the producer preserves that canonical artifact and renders the corresponding report
- **AND** no workflow expression or script substitutes a clean result or recomputes the gate
