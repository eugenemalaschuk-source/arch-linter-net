# architecture-pr-report-publication Specification

## Purpose
Publish the exact Core/CLI-rendered architecture pull-request report through one secure,
manifest-bound GitHub Actions sticky-comment path without granting write authority to
pull-request code.
## Requirements
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

### Requirement: Snapshot inputs and producer readiness are independent per tree and verdict

For each base and current architecture-change snapshot independently, the read-only producer SHALL
append `--baseline architecture/baseline.arch.yml` only when that exact worktree contains the file.
It SHALL not reuse a baseline path or bytes from the other tree. The publisher SHALL determine
transport readiness from exactly one successful named architecture report producer job and its
bound artifact protocol; it SHALL NOT use the overall `workflow_run.conclusion` as a
producer-integrity signal.

#### Scenario: Historical base without a baseline still produces a report

- **WHEN** the base worktree lacks `architecture/baseline.arch.yml` and the current worktree has
  or lacks it independently
- **THEN** the base snapshot runs without `--baseline`
- **AND** the current snapshot uses `--baseline` only when its own tree contains the file
- **AND** the producer can render and upload the canonical report artifact

#### Scenario: Valid report publication is independent from overall CI conclusion

- **WHEN** exactly one named architecture report producer job succeeded and its current-head
  artifact passes all transport checks
- **THEN** the publisher may publish the report even when the overall CI run failed because of a
  strict architecture gate or an unrelated job
- **AND** a missing, failed, or cancelled named producer job is rejected as an integrity failure

### Requirement: Publisher behavior has executable event and artifact regression evidence

The repository SHALL execute fixture-driven tests against the publisher's workflow JavaScript with
mocked GitHub events, REST responses, comments, and bounded artifact files. These tests SHALL
exercise first publication, same-comment update on rerun, legacy-marker migration, stale head,
bad PR/head/run binding, bad hash/schema, oversized payload, failed/cancelled producer, and a fork
artifact that remains inert.

#### Scenario: Fork fixture cannot cause code execution

- **WHEN** a fixture models a fork pull request and supplies arbitrary bounded report bytes
- **THEN** the publisher test invokes only the fixed artifact validation and comment APIs
- **AND** it does not require a checkout, evaluate the bytes as code, or add write authority to the
  producer

### Requirement: Publisher protects report integrity across reruns, races, and text decoding

The privileged publisher SHALL distinguish a latest-attempt producer that is missing from a
failed, cancelled, ambiguous, or invalid producer. When the producer is missing only because a
partial rerun did not rerun it, the publisher SHALL preserve exactly one existing bot-authored
unified comment whose context marker binds it to the current PR head. All other unavailable
producer states SHALL remain fail closed.

The publisher SHALL re-read the pull-request head immediately before writing a sticky comment and
SHALL reject a mismatched artifact without writing it. It SHALL re-read the head after a comment
write; when that read observes a newer head, it SHALL replace only the comment it just wrote with a
fixed unavailable state bound to the observed head.

Before a report is ready for publication, the publisher SHALL strictly decode the hash-validated
report bytes as UTF-8. It SHALL reject malformed bytes and SHALL publish the one validated decoded
string without re-reading or leniently decoding the report file.

#### Scenario: Partial rerun preserves verified same-head evidence

- **WHEN** run attempt 2 contains no producer job because only failed jobs were rerun
- **AND** exactly one existing unified comment is bound to the current PR head
- **THEN** the publisher preserves that comment and reports the partial-rerun state without
  replacing it with unavailable metadata

#### Scenario: A push before comment mutation rejects the old report

- **WHEN** the report passed its initial current-head binding but the pre-write PR-head read sees a
  newer commit
- **THEN** the publisher performs no comment write for the old report
- **AND** it reports a stale-head rejection

#### Scenario: A push after comment mutation is repaired conservatively

- **WHEN** the post-write PR-head read sees a newer commit
- **THEN** the publisher replaces the comment it just wrote with fixed unavailable metadata bound
  to that newer head
- **AND** it does not leave the old report marked as current

#### Scenario: Malformed UTF-8 transport bytes are rejected

- **WHEN** a manifest-bound report has valid size and SHA-256 but contains malformed UTF-8 bytes
- **THEN** the publisher rejects it before a comment write
- **AND** it never substitutes replacement characters into the published report
