## MODIFIED Requirements

### Requirement: Composed effective policy is validated before semantic loading
After all sources pass role/shape validation and are composed, the system SHALL
validate the composed document against the full effective-policy schema before
fallback contract ID assignment and before the ordered
`IArchitecturePolicyDocumentValidator` pipeline. The full schema SHALL require
`version`, `name`, `layers`, `analysis`, and `contracts`, matching the current
production policy schema. The effective-policy schema SHALL validate the composed
document rather than any individual source.

#### Scenario: Fragments complete the root
- **WHEN** a root source omits `layers`, `analysis`, and `contracts` but its
  fragments compose all three sections
- **THEN** the full effective-policy schema accepts the composed document before
  fallback IDs and semantic validators run

#### Scenario: Required effective section absent from graph
- **WHEN** no source contributes one of `layers`, `analysis`, or `contracts`
- **THEN** loading fails against the effective-policy schema before fallback ID
  assignment and family-specific semantic validation

#### Scenario: Composed discovery-wide coverage contracts omit roots
- **WHEN** imported fragments compose project- or assembly-scope coverage contracts
  without `roots`
- **THEN** the full effective-policy schema accepts the effective document and the
  runtime validators apply the same scope semantics as for a direct policy
