## ADDED Requirements

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
The system SHALL provide a mapper that converts existing legacy checker result types (`ArchitectureViolation`, raw cycle path collections, `ArchitectureUnmatchedIgnoredViolation`) into `ArchitectureDiagnostic` instances, without requiring changes to checker output types.

#### Scenario: Mapping a legacy violation
- **WHEN** the mapper receives an `ArchitectureViolation` with no configuration or external-dependency fields set
- **THEN** it returns a `DependencyDiagnostic` preserving `ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, and `ForbiddenReferences`

#### Scenario: Mapping a legacy violation with external dependency group set
- **WHEN** the mapper receives an `ArchitectureViolation` with `ForbiddenExternalGroup` populated
- **THEN** it returns an `ExternalDependencyDiagnostic` preserving the forbidden external group

#### Scenario: Mapping a legacy violation with configuration fields set
- **WHEN** the mapper receives an `ArchitectureViolation` with `TemplateName`, `ContainerNamespace`, `DependencyPaths`, or `MatchedNamespacePrefixes` populated
- **THEN** it returns a `ConfigurationDiagnostic` preserving those fields

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
