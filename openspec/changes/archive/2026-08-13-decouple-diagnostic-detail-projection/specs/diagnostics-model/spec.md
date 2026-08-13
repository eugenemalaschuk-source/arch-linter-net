## MODIFIED Requirements

### Requirement: Formatters consume the diagnostic model without checker-specific knowledge
`ArchitectureDiagnosticFormatter` SHALL render human-readable output and shared CI JSON display fields by pattern-matching on `ArchitectureDiagnostic` kind, SHALL render each diagnostic's structured CI/JSON detail fields by dispatching through a per-family projector registered in `DiagnosticDetailProjectionRegistry` (see the `diagnostic-detail-projection-registry` capability) rather than a central switch enumerating every diagnostic kind, and SHALL NOT inspect optional fields of legacy checker result types directly.

#### Scenario: Existing human and JSON output remain unchanged
- **WHEN** the same set of legacy checker results that previously produced a given human-readable or JSON output is formatted through the new model and adapter
- **THEN** the formatted output is identical to the output produced before this change

#### Scenario: Structured detail projection does not require a central switch edit
- **WHEN** a diagnostic family's structured CI/JSON detail fields are produced
- **THEN** they are produced by that family's own registered projector, and no diagnostic family's structured detail projection requires adding a case to a shared all-kinds switch statement
