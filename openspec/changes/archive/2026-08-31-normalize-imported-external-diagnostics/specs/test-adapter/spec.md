## ADDED Requirements

### Requirement: Testing results include imported normalized findings
The Testing adapter SHALL expose projected imported diagnostics in its existing normalized finding
collection with the same canonical identity, strict/audit semantics, source evidence, and trust
provenance as Core output. It SHALL not create a testing-only imported result collection.

#### Scenario: Audit imported diagnostic is observable without failing strict
- **WHEN** a projected imported diagnostic is mapped to audit and native strict findings are absent
- **THEN** Testing exposes the audit finding while strict pass/fail state remains unchanged
