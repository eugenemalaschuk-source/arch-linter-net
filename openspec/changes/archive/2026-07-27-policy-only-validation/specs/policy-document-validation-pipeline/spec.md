## ADDED Requirements

### Requirement: Static policy validation pass
The policy document validation pipeline SHALL execute each assembly-independent root schema, import, composition, contract cross-reference, selector syntax/binding, declaration, path-format, baseline-reference, and API-snapshot-reference validation once for policy check.

#### Scenario: Duplicate contract ID
- **WHEN** root and imported fragments compose duplicate contract IDs
- **THEN** the pipeline reports one typed configuration failure with provenance

#### Scenario: Import cycle or repository escape
- **WHEN** an import creates a cycle or escapes the repository boundary
- **THEN** the pipeline rejects the policy before any fact-dependent validation is attempted
