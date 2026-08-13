# diagnostic-detail-projection-registry Specification

## Purpose
Define the type-keyed static registry through which each diagnostic family owns its structured
CI/JSON detail projection, so adding a diagnostic family's projection is an additive registry entry
rather than an edit to a central switch enumerating every diagnostic kind.
## Requirements
### Requirement: Structured detail projection dispatches through a type-keyed registry
`ArchitectureDiagnosticFormatter` SHALL build each diagnostic's structured CI/JSON detail fields by looking up a projector delegate registered against that diagnostic's exact CLR type in `DiagnosticDetailProjectionRegistry`, and invoking it, rather than by extending a central switch statement enumerating every diagnostic kind.

#### Scenario: A registered diagnostic type produces its structured fields
- **WHEN** `ArchitectureDiagnosticFormatter` builds CI/JSON output for a diagnostic whose exact type has a registered projector
- **THEN** the projector is invoked with the diagnostic and the target field dictionary, and its structured fields appear in the output exactly as they did before this change

#### Scenario: An unregistered diagnostic type fails loudly
- **WHEN** `ArchitectureDiagnosticFormatter` builds CI/JSON output for a diagnostic whose exact type has no registered projector
- **THEN** the formatter throws `InvalidOperationException` instead of silently omitting structured output

### Requirement: The registry has exactly one entry per supported diagnostic type
`DiagnosticDetailProjectionRegistry.All` SHALL contain exactly one entry for every sealed, non-abstract subtype of `ArchitectureDiagnostic` defined in the Core assembly, with no duplicate or missing entries.

#### Scenario: Every diagnostic subtype is covered
- **WHEN** the set of sealed, non-abstract `ArchitectureDiagnostic` subtypes in the Core assembly is enumerated via reflection
- **THEN** `DiagnosticDetailProjectionRegistry.All` contains exactly one entry keyed by each of those types, with no type missing and no type repeated

#### Scenario: A new diagnostic subtype without a registered projector fails a test
- **WHEN** a new sealed `ArchitectureDiagnostic` subtype is added to the Core assembly without adding a corresponding entry to `DiagnosticDetailProjectionRegistry.All`
- **THEN** the reflection-based completeness test fails, identifying the unregistered type

### Requirement: Adding a diagnostic family's structured projection requires no edit to a shared switch
A new diagnostic family's structured CI/JSON detail projection SHALL be addable by adding one entry to `DiagnosticDetailProjectionRegistry.All` referencing that family's own projector method, without modifying any other family's projector or a shared dispatch switch.

#### Scenario: Adding a family does not touch unrelated projectors
- **WHEN** a new diagnostic family's projector entry is added to the registry
- **THEN** no existing family's `Apply<Kind>CiFields` method, partial file, or registry entry is modified

