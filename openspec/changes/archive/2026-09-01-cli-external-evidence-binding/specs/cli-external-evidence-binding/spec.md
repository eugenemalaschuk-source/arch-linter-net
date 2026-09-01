## ADDED Requirements

### Requirement: CLI binds repository-local SARIF artifacts to declared external evidence
The packed `arch-linter-net` validate command SHALL accept a repeatable structured
`--external-evidence` option that binds one declared `external_evidence` requirement's logical id to
a repository-local SARIF artifact path, plus optional producer repository/revision/scope context, and
SHALL accept `--evidence-repository`/`--evidence-revision`/`--evidence-scope` options that supply the
single current assessment context shared by every binding in the invocation. The CLI SHALL delegate
all trust, selection, normalization, and applicability evaluation for each binding to the existing
Core protocol without reimplementing that semantics.

#### Scenario: One required current SARIF artifact with findings
- **WHEN** a policy declares one required `external_evidence` entry with a `diagnostic_filter` and the
  invocation supplies `--external-evidence id=<id>,path=<path>` plus matching assessment/producer
  context for a SARIF artifact containing selectable results
- **THEN** the validate command's Human/JSON/SARIF/Testing output includes the resulting imported
  findings with their governance mode and evidence provenance, exactly as the direct Core call chain
  (`SarifEvidenceReader` → `SarifExternalDiagnosticSelector` → `ArchitectureImportedDiagnosticProjector`)
  would produce

#### Scenario: One required current SARIF artifact with zero findings
- **WHEN** the bound SARIF artifact is valid, matches the required tool/run and every required
  binding, and its selected run contains zero results
- **THEN** the validate command reports the requirement as evaluable with zero imported findings, and
  this is distinguishable in output from a missing or unassessable requirement

#### Scenario: Two independent required logical evidence inputs
- **WHEN** a policy declares two required `external_evidence` entries and the invocation supplies two
  `--external-evidence` occurrences, each carrying a distinct `id=`
- **THEN** each binding is matched to its declared requirement by logical id rather than by the order
  the options were supplied, and reordering the two `--external-evidence` occurrences produces an
  equivalent canonical result

#### Scenario: Optional evidence deliberately absent
- **WHEN** a policy declares an `external_evidence` entry with `required: false` and the invocation
  supplies no `--external-evidence` binding for its logical id
- **THEN** the validate command reports that entry as explicitly optional/not-configured rather than
  as missing required evidence or a clean zero-result run

#### Scenario: Required artifact absent
- **WHEN** a policy declares a required `external_evidence` entry and the invocation supplies no
  `--external-evidence` binding for its logical id
- **THEN** the requirement is unassessable and does not become a clean zero-result run

#### Scenario: Wrong revision, scope, or logical key
- **WHEN** a `--external-evidence` binding's supplied producer context, or the bound artifact's own
  SARIF metadata, disagrees with the current assessment context or the declared requirement's logical
  id for a binding dimension the requirement marks as required
- **THEN** the requirement is unassessable with the same typed reason the direct Core call chain would
  produce, and the artifact's results never become trusted current findings

#### Scenario: Copied previous-commit artifact
- **WHEN** a bound SARIF artifact is structurally valid and was produced for a different source
  revision than the current `--evidence-revision`, for a requirement with revision binding required
- **THEN** the requirement is unassessable rather than reusable as current evidence

#### Scenario: Required binding metadata missing
- **WHEN** a requirement marks a binding dimension (repository, revision, or scope) as required and
  neither the bound artifact's SARIF metadata nor the invocation's supplied context provides it
- **THEN** the requirement is unassessable rather than guessed as current

#### Scenario: Malformed binding syntax
- **WHEN** a `--external-evidence` value does not parse as a well-formed `id=...,path=...` binding
  (missing `id`/`path`, an unrecognized key, or a duplicate key within one binding)
- **THEN** the invocation fails as invalid invocation/configuration before contract execution, distinct
  from a valid assessment with unassessable evidence

#### Scenario: Unsafe or out-of-repository artifact path
- **WHEN** a syntactically well-formed `--external-evidence` binding's `path=` value resolves outside
  the repository root, is absolute, or crosses an unsafe filesystem indirection
- **THEN** the bound requirement is unassessable through the same Core trust boundary a direct Core
  caller would hit, not treated as invalid CLI invocation syntax

### Requirement: Multiple evidence bindings are order-independent and duplicate-safe
The CLI SHALL treat `--external-evidence` bindings as a set keyed by logical id, not a positional
sequence, and SHALL reject invalid binding syntax as invalid invocation rather than silently ignoring
or misassigning it.

#### Scenario: Duplicate binding id
- **WHEN** two `--external-evidence` occurrences supply the same `id=` value
- **THEN** the invocation fails as invalid invocation/configuration before contract execution

#### Scenario: Unknown binding id
- **WHEN** a `--external-evidence` occurrence supplies an `id=` value that does not match any
  `external_evidence` requirement declared by the loaded policy
- **THEN** the invocation fails as invalid invocation/configuration rather than silently discarding the
  binding

### Requirement: External evidence integrates with the existing authoritative exit-code contract
The system SHALL make the merged applicability evidence from CLI-bound external evidence participate
in the existing canonical PASS/FAIL/UNASSESSABLE-to-0/1/2 exit-code mapping without introducing a new
exit-code category, when the validate command executes a requested authoritative governance gate. The
system SHALL NOT let missing, stale, or wrong-context required evidence produce exit code 0.

#### Scenario: Authoritative gate with unassessable required evidence
- **WHEN** a requested validate invocation has otherwise-passing native conformance but a required
  `external_evidence` entry is missing, stale, or wrong-context
- **THEN** the invocation exits with code `2` and a typed reason distinguishing valid-but-unassessable
  evidence from invalid invocation/configuration

#### Scenario: Authoritative gate with trusted blocking imported findings
- **WHEN** a bound SARIF artifact is fully trusted and its policy-selected diagnostics include at least
  one strict-mode imported finding
- **THEN** the invocation's effective pass state is false and the exit code reflects a governance
  failure, exactly as a native strict violation would

### Requirement: External evidence binding is never persisted into the analysis cache
The CLI's persistent analysis cache (`--cache`) SHALL never store CLI-bound external-evidence imported
diagnostics or the applicability entries/records they contribute, so a cache hit always re-reads and
re-evaluates external evidence fresh from disk.

#### Scenario: Cache hit re-evaluates external evidence fresh
- **WHEN** a validate invocation with `--cache` populates a cache entry, the bound SARIF artifact's
  content is then changed on disk, and an equivalent invocation runs again with the same `--cache`
  destination
- **THEN** the second invocation's reported external-evidence findings and applicability state reflect
  the changed artifact bytes, not the first invocation's cached result

#### Scenario: Repeated cache-populating runs do not accumulate duplicate applicability entries
- **WHEN** the same validate invocation with `--cache` and a bound external-evidence artifact runs
  repeatedly
- **THEN** each run reports exactly one applicability record per declared logical evidence id, with no
  growth across repeated invocations

### Requirement: Public documentation describes the CLI binding surface
`docs/policy-format/external-evidence.md` SHALL describe the packed-CLI binding flow, including its
exact flag syntax and a copy-paste-ready local and CI example, alongside the existing Core API
description.

#### Scenario: Documentation shows a runnable CLI example
- **WHEN** a reader follows the external-evidence guide's CLI section
- **THEN** the documented `--external-evidence`/`--evidence-repository`/`--evidence-revision`/
  `--evidence-scope` invocation is runnable against a policy declaring a matching `external_evidence`
  requirement without writing any custom .NET host code
