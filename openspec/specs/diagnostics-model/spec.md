# diagnostics-model Specification

## Purpose
TBD - created by archiving change split-diagnostics-model. Update Purpose after archive.
## Requirements
### Requirement: Typed diagnostic envelope
The system SHALL represent each architecture diagnostic as a sealed subtype of an abstract `ArchitectureDiagnostic` base, discriminated by an `ArchitectureDiagnosticKind` value, with one subtype per kind: dependency violation, cycle, unmatched ignore, configuration violation, and external dependency violation.

#### Scenario: Dependency violation has its own subtype
- **WHEN** a layer, allow-only, method-body, asmdef, independence, or protected-surface contract produces a violation
- **THEN** the diagnostic model represents it as a `DependencyDiagnostic` with `Kind == ArchitectureDiagnosticKind.Dependency`

#### Scenario: Cycle has its own subtype
- **WHEN** cycle detection finds a dependency cycle
- **THEN** the diagnostic model represents it as a `CycleDiagnostic` with `Kind == ArchitectureDiagnosticKind.Cycle`

#### Scenario: Unmatched ignore has its own subtype
- **WHEN** an ignore rule does not match any violation
- **THEN** the diagnostic model represents it as an `UnmatchedIgnoreDiagnostic` with `Kind == ArchitectureDiagnosticKind.UnmatchedIgnore`

#### Scenario: Configuration violation has its own subtype
- **WHEN** a contract produces a configuration-shaped violation (template/container-namespace/dependency-path/namespace-prefix fields populated)
- **THEN** the diagnostic model represents it as a `ConfigurationDiagnostic` with `Kind == ArchitectureDiagnosticKind.Configuration`

#### Scenario: External dependency violation has its own subtype
- **WHEN** a contract produces a violation against a forbidden external dependency group
- **THEN** the diagnostic model represents it as an `ExternalDependencyDiagnostic` with `Kind == ArchitectureDiagnosticKind.ExternalDependency`

### Requirement: Diagnostic subtypes carry only kind-relevant fields
Each `ArchitectureDiagnostic` subtype SHALL declare only the fields meaningful to its own kind; it SHALL NOT expose optional fields belonging to other kinds.

#### Scenario: Dependency diagnostic has no configuration-only fields
- **WHEN** a `DependencyDiagnostic` is constructed
- **THEN** it exposes `SourceLayer`, `TargetLayer`, and `AllowedImporters` but has no `TemplateName`, `ContainerNamespace`, or `DependencyPaths` members

#### Scenario: Matched namespace prefixes are available on any diagnostic kind
- **WHEN** a violation carries both layer/importer information and matched namespace prefixes (e.g. a protected-layer violation)
- **THEN** the resulting `DependencyDiagnostic` preserves both the layer/importer fields and `MatchedNamespacePrefixes` without losing either

#### Scenario: Cycle diagnostic carries the cycle path
- **WHEN** a `CycleDiagnostic` is constructed
- **THEN** it exposes the contract name, contract id, and the ordered cycle path, with no dependency- or configuration-specific fields

### Requirement: Adapter converts legacy checker results to the diagnostic model
The system SHALL provide a mapper that converts checker result types (`ArchitectureViolation`, raw cycle path collections, `ArchitectureUnmatchedIgnoredViolation`) into `ArchitectureDiagnostic` instances. For `ArchitectureViolation`, the mapper SHALL dispatch to the violation's `IArchitectureDiagnosticPayload` (via `Payload.ToDiagnostic(violation)`) when one is set, and SHALL NOT infer diagnostic kind by inspecting which of a shared set of nullable fields on `ArchitectureViolation` is populated. A violation with no payload SHALL map to a plain `DependencyDiagnostic`.

#### Scenario: Mapping a violation with no payload
- **WHEN** the mapper receives an `ArchitectureViolation` with `Payload` unset
- **THEN** it returns a `DependencyDiagnostic` preserving `ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`, and `MatchedNamespacePrefixes`

#### Scenario: Mapping a violation with an external dependency payload
- **WHEN** the mapper receives an `ArchitectureViolation` whose `Payload` is an `ExternalDependencyPayload`
- **THEN** it returns an `ExternalDependencyDiagnostic` preserving the forbidden external group carried on the payload

#### Scenario: Mapping a violation with a configuration payload
- **WHEN** the mapper receives an `ArchitectureViolation` whose `Payload` is a `ConfigurationPayload`
- **THEN** it returns a `ConfigurationDiagnostic` preserving the template name, container namespace, and dependency paths carried on the payload; `MatchedNamespacePrefixes`, if also set on the violation, is preserved on the shared base and does not affect kind classification

#### Scenario: Mapping a cycle path
- **WHEN** the mapper receives a cycle path collection, contract name, and contract id
- **THEN** it returns a `CycleDiagnostic` preserving the path and contract identifiers

#### Scenario: Mapping an unmatched ignore
- **WHEN** the mapper receives an `ArchitectureUnmatchedIgnoredViolation`
- **THEN** it returns an `UnmatchedIgnoreDiagnostic` preserving `ContractName`, `ContractId`, `IgnoreIndex`, `SourceType`, `ForbiddenReference`, and `Reason`

### Requirement: Formatters consume the diagnostic model without checker-specific knowledge
`ArchitectureDiagnosticFormatter` SHALL render human-readable and CI JSON output by pattern-matching on `ArchitectureDiagnostic` kind, and SHALL NOT inspect optional fields of legacy checker result types directly.

#### Scenario: Existing human and JSON output remain unchanged
- **WHEN** the same set of legacy checker results that previously produced a given human-readable or JSON output is formatted through the new model and adapter
- **THEN** the formatted output is identical to the output produced before this change

### Requirement: Policy consistency diagnostic has its own subtype
The system SHALL represent each policy-consistency finding as a `PolicyConsistencyDiagnostic` with `Kind == ArchitectureDiagnosticKind.PolicyConsistency`, carrying `CheckKind` (a string discriminant such as `"duplicate-id"`, `"allow-forbid-conflict"`, `"independence-conflict"`, `"protected-importer-conflict"`, `"layer-overlap"`, or `"unreachable-contract"`), `Reason` (human-readable text), `ConflictingContractIds`, `ConflictingContractNames`, `Layers`, and an optional `RepresentativeType`.

#### Scenario: Duplicate-ID finding maps to the diagnostic
- **WHEN** the policy-consistency check finds two contracts sharing an ID
- **THEN** it produces a `PolicyConsistencyDiagnostic` with `Kind == ArchitectureDiagnosticKind.PolicyConsistency`, `CheckKind == "duplicate-id"`, and `ConflictingContractIds` containing both contracts' IDs

#### Scenario: Layer-overlap finding carries a representative type
- **WHEN** the policy-consistency check finds two layers matching the same concrete type
- **THEN** the resulting `PolicyConsistencyDiagnostic` has `CheckKind == "layer-overlap"`, `Layers` containing both layer names, and `RepresentativeType` set to that type's full name

### Requirement: Formatters render policy consistency diagnostics
`ArchitectureDiagnosticFormatter` SHALL render `PolicyConsistencyDiagnostic` instances in both human-readable and CI JSON output, including the `CheckKind`, `Reason`, conflicting contract identifiers, and layers.

#### Scenario: Human-readable output includes the reason
- **WHEN** a `PolicyConsistencyDiagnostic` is formatted for human-readable output
- **THEN** the output includes the `CheckKind`, `Reason` text, and the conflicting contract names

#### Scenario: CI JSON output includes structured fields
- **WHEN** a `PolicyConsistencyDiagnostic` is formatted as CI JSON
- **THEN** the JSON includes `kind`, `checkKind`, `reason`, `conflictingContractIds`, `conflictingContractNames`, `layers`, and `representativeType` (when present)

### Requirement: Family-specific diagnostic evidence lives on a payload type, not the shared violation record
`ArchitectureViolation` SHALL expose only fields common to every diagnostic family (`ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`, `MatchedNamespacePrefixes`) plus a single optional `Payload` of type `IArchitectureDiagnosticPayload`. Evidence specific to one diagnostic family (e.g. a forbidden base type, an API signature, a project metadata key) SHALL be declared on that family's own payload type, not as an additional field on `ArchitectureViolation`.

#### Scenario: A family's evidence is not visible on the shared violation type
- **WHEN** a checker produces a violation for a family such as inheritance or public API surface
- **THEN** the family-specific evidence (e.g. `ForbiddenBaseType`, `UndeclaredApiSignature`) is available only through `violation.Payload` cast to that family's payload type, not as a member of `ArchitectureViolation` itself

### Requirement: Adding a diagnostic family requires no edits to shared types
A new diagnostic family SHALL be addable by introducing a new type implementing `IArchitectureDiagnosticPayload` and constructing it at the relevant checker/finder call site, without modifying `ArchitectureViolation` or `ArchitectureDiagnosticMapper`.

#### Scenario: New family payload dispatches without a mapper edit
- **WHEN** a violation carries a `Payload` implementing `IArchitectureDiagnosticPayload` that the mapper has never seen before
- **THEN** `ArchitectureDiagnosticMapper.FromViolation` returns the diagnostic produced by that payload's own `ToDiagnostic` method, with no change required in the mapper's source

### Requirement: Diagnostics carry typed policy-origin metadata
Every architecture diagnostic type SHALL be able to carry one optional typed policy
location and an ordered collection of related typed policy locations as common
diagnostic metadata. A policy location SHALL use named fields for portable source path,
root/fragment role, YAML path, source ordinal, and contract family/ID where applicable;
it SHALL NOT use a generic nullable property bag or expose host-absolute paths.

#### Scenario: Contract violation retains owning fragment
- **WHEN** a violation is produced by a contract loaded from a fragment
- **THEN** the mapped typed diagnostic carries that contract's fragment location without adding family-specific provenance fields to its payload

#### Scenario: Policy consistency conflict retains both owners
- **WHEN** a policy-consistency finding involves contracts or layers from two sources
- **THEN** its primary and related policy locations identify the involved source nodes in deterministic composed order

#### Scenario: Ignored violation retains nested YAML path
- **WHEN** an unmatched ignored violation originated in an imported contract
- **THEN** its diagnostic location points to the corresponding `ignored_violations` entry in that fragment

### Requirement: Human and CI JSON formatters expose policy origin additively
Human-readable diagnostics SHALL append a compact portable policy location when one is
available. CI JSON diagnostics SHALL add `policy_location` and, when non-empty,
`related_policy_locations` objects while preserving all existing fields, diagnostic
kinds, ordering, and compatibility for diagnostics without policy provenance.

#### Scenario: Human fragment diagnostic is actionable
- **WHEN** a diagnostic owns a fragment location
- **THEN** human output includes the fragment path and YAML path while preserving the existing diagnostic message

#### Scenario: CI JSON location is structured and portable
- **WHEN** the same diagnostic is rendered as CI JSON on different machines
- **THEN** `policy_location` contains the same `/`-separated source path, role, YAML path, source ordinal, and contract metadata on each machine

#### Scenario: Existing consumer ignores additive metadata
- **WHEN** a consumer reads only fields that existed before policy provenance
- **THEN** those fields retain their previous names and values

### Requirement: Every diagnostic subtype is recognized at every concrete-subtype dispatch point

Formatting adapters that read shared display fields (`SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`)
off a diagnostic by pattern-matching on its concrete `ArchitectureDiagnostic` subtype SHALL include every
subtype that declares those fields. Introducing a new diagnostic subtype without extending every such
dispatch point SHALL NOT silently degrade to an empty/generic value in any adapter.

#### Scenario: A subtype present in one adapter is present in all
- **WHEN** a diagnostic subtype is recognized by the SARIF formatter's field-extraction switch
- **THEN** the same subtype is also recognized by the Human/JSON formatter's equivalent switches, so no
  adapter alone determines whether a finding's evidence is visible

