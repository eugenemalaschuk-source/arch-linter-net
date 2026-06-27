## ADDED Requirements

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
