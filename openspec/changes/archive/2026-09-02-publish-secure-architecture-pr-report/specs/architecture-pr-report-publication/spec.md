## ADDED Requirements

### Requirement: A read-only CI producer emits the canonical unified report artifact
The pull-request CI workflow SHALL run the existing ArchLinterNet CLI to create compatible
current/base architecture-change snapshots, an `architecture-health/v1` artifact, and the complete
`architecture-pr-report.md` Markdown projection. It SHALL upload the rendered Markdown and a
fixed `architecture-pr-report.manifest.json` as one named artifact. The producer SHALL retain only
read repository permissions and SHALL not create, update, or delete pull-request comments.

The workflow SHALL use one non-empty execution context for the Health and change artifacts. The
manifest SHALL declare a closed publication schema, report artifact kind, marker version, fixed
Markdown file path, SHA-256, byte count, repository, PR number, head SHA, producer workflow run
ID, and producer run attempt. Workflow glue SHALL not render Markdown, derive Architecture Health,
recount evidence, or implement remediation/business rules.

#### Scenario: Report rendering precedes publication
- **WHEN** the required canonical producer artifacts are complete for a pull request
- **THEN** the producer invokes `arch-linter-net report pr` before it uploads the report artifact
- **AND** the uploaded report bytes are the deterministic CLI-rendered Markdown without
  workflow-owned report composition

#### Scenario: A failed strict architecture result is still rendered canonically
- **WHEN** Architecture Health has a valid failing or unassessable gate result that the CLI can
  serialize
- **THEN** the producer preserves that canonical artifact and renders the corresponding report
- **AND** no workflow expression or script substitutes a clean result or recomputes the gate

### Requirement: The privileged publisher validates untrusted artifact transport before writing
The repository SHALL publish the unified report only from a dedicated completed-CI publisher that
uses the trusted default-branch workflow definition, performs no checkout, and has the only
`pull-requests: write` permission in the report path. Before it reads or posts a report, the
publisher SHALL validate the producer workflow/repository identity, exactly one associated PR,
current PR head SHA, producer run ID and attempt, one fixed non-expired artifact ID, bounded
artifact/report/manifest sizes, expected fixed file names and regular-file shape, manifest schema,
report kind, marker version, byte count, and SHA-256.

The publisher SHALL treat the manifest and report as inert data. It SHALL parse bounded JSON and
compute a content hash, but SHALL not check out, source, evaluate, execute, or interpolate artifact
content as shell, workflow, JavaScript, paths, or report semantics.

#### Scenario: Stale head evidence is rejected
- **WHEN** the completed producer run or its manifest names a head SHA different from the current
  pull-request head
- **THEN** the publisher rejects that evidence and does not publish it as the current report
- **AND** a late stale run does not overwrite a newer verified current-head comment

#### Scenario: Malformed or oversized transport evidence is rejected
- **WHEN** the producer artifact is missing, duplicated, expired, oversized, has an unexpected
  file shape, or has mismatched manifest schema, kind, marker, context, byte count, or hash
- **THEN** the publisher fails the integration path without parsing the Markdown as architecture
  semantics or posting it

### Requirement: One authoritative sticky comment is maintained without competing coverage output
The publisher SHALL identify one bot-authored unified comment with the stable hidden
`arch-linter-net-pr-report:v1` marker and update it in place on later pushes and reruns. Where the
old repository-owned Architecture Coverage marker is the sole prior bot comment, the publisher
SHALL replace that comment in place with the unified report. It SHALL fail closed on ambiguous
duplicate repository-owned markers rather than selecting an arbitrary comment.

The comment body SHALL consist of fixed publisher metadata/markers plus the exact validated
CLI-rendered Markdown. The publisher SHALL not construct report sections, alter the Markdown,
or add generic CI, build, test, quality-service, or security-service status.

#### Scenario: A rerun updates one comment
- **WHEN** a second valid producer run binds to the same current pull-request head
- **THEN** the publisher updates the existing unified comment rather than creating another
- **AND** the report body remains the validated CLI artifact for that run

#### Scenario: Producer failure does not preserve stale green evidence
- **WHEN** a producer fails, is cancelled, or cannot provide a valid report artifact for the
  current head
- **THEN** the publisher fails closed and may show only a bounded fixed transport/integration
  unavailable state
- **AND** it does not fabricate an architecture verdict or leave stale report evidence marked as
  current

### Requirement: Fork publication preserves the trust boundary
The repository SHALL allow untrusted fork and Dependabot producer workflows to run only with
read-only permissions. The privileged publisher SHALL never execute fork-controlled source,
workflow changes, generated files, or artifact content. If GitHub cannot grant the dedicated
publisher sufficient permission to publish safely, the workflow SHALL fail or degrade publication
safely without granting write authority to the producer.

#### Scenario: A fork report remains inert
- **WHEN** a fork pull request uploads a report artifact with arbitrary bytes
- **THEN** the publisher uses only the fixed bounded transport-validation protocol
- **AND** it neither checks out the fork nor executes or evaluates artifact bytes
