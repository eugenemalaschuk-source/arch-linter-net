# external-sarif-evidence Specification

## Purpose
Define deterministic, bounded trust validation for local SARIF evidence before any
external diagnostic can influence ArchLinterNet governance.

## Requirements

### Requirement: Policy declares logical external-evidence requirements
Policy SHALL declare each external-evidence input by a unique logical identity, supported format,
requiredness, expected producer/run identity, and which repository, revision, and scope bindings
are required. A declaration SHALL reject unknown formats, duplicate identities, blank required
identities, and incomplete required producer/run selectors during policy validation.

#### Scenario: A required SARIF input is declared
- **WHEN** a policy declares a unique logical evidence key with the SARIF format, an expected tool
  and run selector, and revision binding enabled
- **THEN** the effective policy exposes a typed requirement that a caller can bind to an artifact
  and explicit analysis context

#### Scenario: A declaration is ambiguous
- **WHEN** a policy repeats a logical evidence key or declares an unsupported evidence format
- **THEN** policy loading fails with an actionable configuration diagnostic rather than selecting
  an artifact by filename or declaration order

### Requirement: Evidence artifacts are local, contained, and bounded
The system SHALL read an evidence artifact only from a regular repository-local path supplied for
its logical requirement. It SHALL reject an absolute/out-of-repository, missing, unsafe, or
over-limit artifact before treating it as a completed analysis. It SHALL enforce deterministic
limits for artifact bytes, SARIF runs, and results per selected run.

#### Scenario: Required artifact is missing
- **WHEN** a required logical evidence input has no readable artifact at its supplied local path
- **THEN** its trust outcome is `missing_required_input`, not a valid SARIF run with zero results

#### Scenario: Artifact escapes the repository
- **WHEN** an evidence path resolves outside the configured repository root or crosses an unsafe
  filesystem indirection
- **THEN** the trust outcome rejects the path without opening an analyzer, network connection, or
  unrelated local file

#### Scenario: Artifact exceeds the configured bound
- **WHEN** the artifact, its run collection, or its selected result collection exceeds the declared
  input limit
- **THEN** the trust outcome is unassessable with a stable resource-bound reason and no partial
  successful result is emitted

### Requirement: SARIF shape and producer execution are validated
The system SHALL accept only SARIF 2.1.0 documents with one unambiguous run that matches the
configured producer and run selectors. The selected run SHALL contain explicit successful
execution metadata. Malformed JSON, unsupported SARIF shape/version, an absent expected run/tool,
or unsuccessful/incomplete execution SHALL remain distinguishable, actionable trust outcomes.

#### Scenario: Successful zero-result run is valid evidence
- **WHEN** a bounded SARIF 2.1.0 artifact contains one matching successfully executed run with an
  empty results collection
- **THEN** its trust outcome is valid and records zero results without reclassifying it as absent
  or failed evidence

#### Scenario: Expected producer run is absent
- **WHEN** a structurally valid artifact contains no run matching the configured tool and run
  selectors
- **THEN** its trust outcome identifies the missing expected run rather than guessing from another
  run or artifact filename

#### Scenario: Producer execution was unsuccessful
- **WHEN** the matching SARIF run has absent, false, or otherwise incomplete execution-success
  metadata
- **THEN** its trust outcome is unassessable and cannot become a clean zero-result analysis

### Requirement: Evidence is explicitly bound to its analysis context
For each configured binding dimension, the system SHALL compare the logical evidence key,
repository identity, source revision, and scope supplied by the producer/CI context with the
configured current assessment context. Standard SARIF run metadata and explicit supplied context
are valid vendor-neutral transports; conflicting or absent required metadata SHALL be
unassessable. The system SHALL NOT infer currentness from artifact name, filesystem time, artifact
ordering, or workflow/job name.

#### Scenario: Same producer but wrong revision is rejected
- **WHEN** a successful matching SARIF run is bound to a source revision different from the
  current required revision
- **THEN** the trust outcome is `wrong_external_revision` and the artifact cannot satisfy current
  governance

#### Scenario: Required context is absent
- **WHEN** repository, revision, scope, or logical evidence-key binding is required but neither
  compatible SARIF metadata nor explicit producer/CI context supplies it
- **THEN** the trust outcome is unassessable rather than guessing that the artifact is current

#### Scenario: Logical key or scope differs
- **WHEN** an artifact is supplied with a logical evidence key or scope that differs from the
  configured requirement
- **THEN** the trust outcome identifies the mismatched identity or scope even if the artifact bytes
  and source revision otherwise match

### Requirement: Trust provenance is deterministic and reusable
Every completed artifact read SHALL expose the configured logical evidence identity, selected
producer/run identity, normalized repository-relative artifact path, deterministic lowercase
SHA-256 content hash, result count, and validated context bindings. Equivalent bytes and context
SHALL produce equivalent provenance regardless of host path separators or read order. The reader
SHALL preserve these facts for later filtering and normalized-finding work without invoking a
producer-specific service API.

#### Scenario: Identical artifact bytes have a stable hash
- **WHEN** identical SARIF bytes are read in separate local or CI assessments with equivalent
  explicit context
- **THEN** both trust outcomes expose the same artifact content hash and canonical provenance

#### Scenario: Optional artifact is deliberately absent
- **WHEN** an explicitly optional logical evidence input has no supplied artifact
- **THEN** the outcome is explicitly optional/not-configured and is distinct from required missing
  evidence or a valid successful zero-result run
