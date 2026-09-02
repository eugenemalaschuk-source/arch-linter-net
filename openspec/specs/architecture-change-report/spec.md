# architecture-change-report Specification

## Purpose
Provide a deterministic, machine-readable architecture delta between complete analysis snapshots for branch and pull-request workflows.

## Requirements

### Requirement: Complete architecture analysis can be persisted as a change snapshot
The system SHALL provide a versioned `architecture-change-snapshot/v2` artifact that is built from a complete authoritative architecture analysis and contains stable entries for observed namespaces, projects, assemblies, semantic roles and contexts, dependency edges, coverage blind spots, normalized findings, and baseline-debt identities. Normalized findings SHALL include every applicable contract family's findings whose configuration is not disabled, including dependency, coverage, policy-consistency, and unmatched-ignored-violation findings; a finding from a contract family whose configuration is not disabled SHALL NOT be silently omitted from the snapshot. Every entry's and every finding's identity SHALL be stable and unique: two distinct entries or findings (including two coverage blind-spot entries for different rule inputs on the same contract, and two findings of the same policy-consistency check kind under one contract) SHALL never be assigned the same identity, no identity SHALL be empty, and an identity SHALL depend only on the entry's or finding's own semantic content, never on its position within a list the policy author could reorder without changing meaning. Repeated observations of the same logical entry, including semantic-role or semantic-context facts produced for equivalent types in separate assemblies, SHALL be represented by one entry before snapshot validation; this projection SHALL NOT change the per-assembly classification facts available to analysis/runtime consumers. Core SHALL own the deterministic projection of those authoritative analysis facts and canonical identities into the snapshot contract; CLI hosts SHALL delegate that projection while retaining command orchestration and I/O responsibilities. The artifact SHALL identify its analysis mode and condition-set scope and SHALL use deterministic ordering. A snapshot document missing any required authority metadata or a required entries, findings, or baseline-debt collection SHALL be rejected rather than treated as an empty collection.

#### Scenario: Snapshot retains complete analysis facts
- **WHEN** a user creates a snapshot for a policy in the supported analysis mode
- **THEN** the artifact contains its versioned mode and condition-set scope metadata and sorted architecture surfaces
- **AND THEN** it does not claim to represent only changed files or projects

#### Scenario: Surface kinds remain distinct
- **WHEN** a namespace and assembly share the same textual name
- **THEN** the snapshot assigns them identities in distinct surface kinds

#### Scenario: CLI delegates canonical projection to Core
- **WHEN** a CLI host creates a snapshot from validation, graph, and frozen-baseline results
- **THEN** Core produces the canonical snapshot entries, findings, and baseline-debt identities
- **AND THEN** the CLI host retains option validation, runtime orchestration, artifact I/O, and error presentation

#### Scenario: Policy-consistency and unmatched-ignore findings are included
- **WHEN** an analysis produces policy-consistency findings or unmatched-ignored-violation findings whose contract-family configuration is not disabled
- **THEN** the snapshot's findings include those findings with a stable canonical identity

#### Scenario: Multiple findings of the same policy-consistency check kind get distinct identities
- **WHEN** an analysis produces two or more policy-consistency findings of the same check kind under the same contract (for example two unmatched layer-exclusion findings on different layers)
- **THEN** each finding is assigned a distinct, non-empty identity
- **AND THEN** snapshot creation succeeds rather than being rejected for duplicate or empty finding identities

#### Scenario: Policy-consistency findings against contracts sharing a duplicate id get distinct identities
- **WHEN** two policy-consistency findings of the same check kind share identical layers and identical conflicting-contract ids because the conflicting contracts have a duplicate id, but differ in their conflicting-contract names
- **THEN** each finding is assigned a distinct identity
- **AND THEN** the conflicting-contract names are not discarded merely because conflicting-contract ids are also present

#### Scenario: Disabled contract family is excluded from the snapshot
- **WHEN** a contract family's configuration is set to disabled ("off")
- **THEN** that family's findings do not appear in the snapshot's findings

#### Scenario: Coverage blind-spot entries for different rule inputs on the same contract get distinct identities
- **WHEN** a rule-input coverage contract reports two different rule inputs (for example its source layer and one of its forbidden layers) as stale or unknown
- **THEN** each is assigned a distinct, non-empty coverage blind-spot entry identity
- **AND THEN** snapshot creation succeeds rather than being rejected for duplicate or empty entry identities

#### Scenario: Coverage scopes whose item already identifies the fact keep their existing identity
- **WHEN** a coverage scope other than rule input (for example project or semantic coverage) reports a stale or unknown item whose item value already identifies the fact uniquely
- **THEN** that entry's identity is derived from the item value alone and is unchanged from previous snapshots
- **AND THEN** an unchanged coverage fact is not reported as both removed and new

#### Scenario: Identity is independent of list position
- **WHEN** a policy author reorders elements of a list a finding's identity is derived from (for example a layer's exclude entries) without changing what any element means
- **THEN** the finding produced for a given element keeps the same identity it had before the reorder

#### Scenario: Equivalent semantic facts across assemblies are collapsed in a snapshot
- **WHEN** multiple distinct CLR type instances from separate assemblies produce semantic-role facts with the same stable subject, role, and metadata, including context metadata
- **THEN** the snapshot contains one `semantic_role` entry and one entry for each distinct `semantic_context` identity
- **AND THEN** snapshot creation succeeds without changing the original per-assembly classification facts

#### Scenario: Different logical entries remain distinct
- **WHEN** observed surfaces differ in kind or stable identity
- **THEN** the snapshot retains a separate entry for each surface

### Requirement: Architecture snapshots are compared by stable identity
The system SHALL compare a base and current `architecture-change-snapshot/v2` artifact by typed, stable identity and SHALL report added and removed namespaces, projects, assemblies, semantic roles, contexts, dependency edges, and coverage blind spots. The result SHALL be deterministically ordered and SHALL reject incompatible, incomplete, or unsupported snapshot inputs.

#### Scenario: New dependency edge is reported
- **WHEN** the current snapshot contains a dependency edge absent from the base snapshot
- **THEN** the report lists that edge as added with its source and target identities

#### Scenario: Removed namespace is reported
- **WHEN** a base namespace entry does not occur in the current snapshot
- **THEN** the report lists that namespace as removed

#### Scenario: Incompatible input fails closed
- **WHEN** either input lacks a supported snapshot version, required collections or authority metadata, or the snapshots use different analysis modes or condition-set scopes
- **THEN** report creation fails with an actionable input error

### Requirement: New drift is distinct from known baseline debt
The report SHALL classify current normalized findings and coverage blind spots independently of surface changes. A finding absent from the base SHALL be reported as new; an identity present in both snapshots SHALL be reported as existing; and a current baseline-debt identity SHALL be reported separately from new findings.

#### Scenario: Existing baseline debt is not reported as new drift
- **WHEN** the current snapshot contains a finding or baseline-debt identity also present in the base
- **THEN** the report does not classify that identity as a new violation

#### Scenario: New coverage blind spot is visible
- **WHEN** a current snapshot contains an uncovered coverage item absent from the base snapshot
- **THEN** the report lists it as a new coverage blind spot

### Requirement: CLI emits human and JSON architecture change reports
The CLI SHALL expose `change snapshot` to write a complete snapshot and `change report` to compare `--base` and `--current` snapshots. `change snapshot` SHALL accept `--ensure-built`, `--no-restore`, `--configuration`, `--framework`, `--platform`, and `--runtime` as explicit build-state options. When a build-state option is selected, the CLI SHALL apply one effective output context to validation, namespace and assembly graph projection, and optional baseline-debt comparison before persisting the snapshot. When `--ensure-built` is selected, validation SHALL prepare its selected project graph exactly once and every later contributor SHALL consume validation's exact receipt-backed artifact selection through the supported fresh isolated post-build analysis path. A snapshot SHALL be written only when every requested contributor succeeds; it SHALL preserve canonical finding identity, requested mode, condition-set scope, deterministic ordering, and complete-result semantics. Ordinary snapshot invocation without those options SHALL remain non-building. `change report` SHALL support deterministic `human` and `json` output, leave existing validation behavior unchanged when it is not invoked, and return success for a completed report regardless of whether the report contains drift.

#### Scenario: Snapshot accepts a complete post-build analysis request
- **WHEN** a policy opts into `Microsoft.AspNetCore.App` and a user runs `change snapshot --ensure-built --configuration Debug --framework net10.0`
- **THEN** validation, both graph projections, and optional baseline-debt collection use the same prepared post-build output context
- **AND THEN** the CLI writes a complete snapshot without requiring a consumer runtimeconfig, NuGet-cache DLL lookup, or `dotnet exec` workaround

#### Scenario: A prepared snapshot builds the selected graph once
- **WHEN** a user creates an ensure-built snapshot with namespace, assembly, and baseline contributors
- **THEN** the snapshot invokes the graph build exactly once
- **AND THEN** each later contributor re-verifies and consumes that prepared output without invoking another restore or build

#### Scenario: CLI output overrides win over policy output defaults
- **WHEN** a policy defaults to Debug output and a user runs an ensure-built snapshot with `--configuration Release`, a framework override, or a runtime identifier
- **THEN** validation, graph projections, and baseline-debt collection consume the same receipt-verified Release/framework/RID artifact selection
- **AND THEN** no contributor rediscovers an artifact from the policy-default output path

#### Scenario: A required baseline contributor is blocked
- **WHEN** a snapshot requests `--baseline` and baseline-debt collection returns a blocked preflight result
- **THEN** the CLI returns a non-zero exit code, reports its available diagnostics, and does not write a snapshot artifact

#### Scenario: Ordinary snapshot remains non-building
- **WHEN** a user runs `change snapshot` without build-state options
- **THEN** the command does not restore or build implicitly

#### Scenario: JSON output is usable by CI
- **WHEN** `change report --format json` completes
- **THEN** stdout contains exactly one valid JSON document with ordered delta and debt sections

#### Scenario: Report does not perform partial analysis
- **WHEN** a user invokes `change report` with two snapshot paths
- **THEN** the command compares only the supplied complete snapshot artifacts
- **AND THEN** it does not select or analyze a changed-file or changed-project subset

### Requirement: Semantic snapshot observations are deduplicated without suppressing identity collisions
The system SHALL collapse repeated semantic-role observations only when their subject, role, and complete metadata key/value set are structurally equivalent, independent of metadata enumeration order. It SHALL collapse repeated semantic-context observations only when their subject, metadata key, and typed metadata value are structurally equivalent. This projection SHALL occur before snapshot validation and SHALL NOT suppress, rewrite, or merge any remaining duplicate `(Kind, Identity)` pair produced by structurally different entries; such an identity collision SHALL continue to be rejected as an invalid snapshot.

#### Scenario: Equivalent linked-type observations are collapsed
- **WHEN** distinct CLR type instances from separate assemblies produce semantic-role observations with the same subject, role, and metadata values
- **THEN** the snapshot contains one semantic-role entry and one entry for each structurally distinct semantic context
- **AND THEN** the classification result still contains the per-assembly observations

#### Scenario: Metadata enumeration order does not create an extra semantic role surface
- **WHEN** two semantic-role observations have the same subject, role, and metadata key/value pairs in different enumeration orders
- **THEN** the snapshot contains one semantic-role entry for those observations

#### Scenario: Structurally different facts with a serialized identity collision fail closed
- **WHEN** two structurally different semantic-role observations serialize to the same `(Kind, Identity)` pair because their legacy identity encoding is ambiguous
- **THEN** snapshot serialization rejects the snapshot with the duplicate-or-empty entry-identity error
- **AND THEN** neither observation is silently removed or merged

### Requirement: Change report retains resolved findings for downstream projections
The deterministic architecture-change report SHALL retain the stable normalized findings present in the base snapshot but absent from the current snapshot as `resolved_findings`, separately from added, existing, and baseline-debt findings. The report SHALL preserve the same compatible-mode and condition-set validation, ordering, and complete-snapshot-only comparison rules as its other delta sections.

#### Scenario: Resolved finding is retained without a second comparison
- **WHEN** a finding exists in a compatible base snapshot and is absent from the compatible current snapshot
- **THEN** the canonical change report contains that finding once in its ordered resolved-findings section
- **AND** downstream consumers can disclose the resolution without reopening or recomparing either snapshot

#### Scenario: Existing and new findings remain distinct from resolutions
- **WHEN** a compatible current snapshot contains one base-known finding and one finding absent from the base while another base finding is absent from current
- **THEN** the report retains the three findings in existing, new, and resolved sections respectively
- **AND** no finding is counted in more than one section

### Requirement: Persisted change reports carry compatible execution context
The versioned machine-readable architecture-change report SHALL retain the
mode and condition-set scope validated from its input snapshots and a
non-empty execution identifier supplied by the report workflow.  Consumers
SHALL reject a report whose context is absent, malformed, or unsupported.

#### Scenario: Change report retains report workflow identity
- **WHEN** a workflow compares compatible strict snapshots with an execution
  identifier
- **THEN** its JSON report retains that identifier, strict mode, and
  condition-set scope alongside the ordered delta sections

#### Scenario: Context-less report is unsupported
- **WHEN** a consumer reads a persisted architecture-change report without
  required execution context
- **THEN** it rejects the report rather than treating it as compatible with a
  Health artifact
