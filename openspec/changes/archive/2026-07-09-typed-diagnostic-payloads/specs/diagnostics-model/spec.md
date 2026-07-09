## MODIFIED Requirements

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

## ADDED Requirements

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
