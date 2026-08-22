## ADDED Requirements

### Requirement: Human and JSON output project normalized remediation guidance
Human and JSON violation output SHALL project the optional normalized remediation hint with equivalent category, summary, canonical identity, evidence, expected seam or direction, caveat, and review semantics. Human output SHALL remain concise and omit the remediation clause when no hint is available; JSON SHALL expose the structured hint without requiring consumers to parse prose.

#### Scenario: Human output displays a safe port hint
- **WHEN** a normalized finding has a `use_declared_port` remediation hint
- **THEN** human output includes its deterministic remediation summary and JSON includes the same category and expected seam as structured fields

#### Scenario: JSON preserves no-hint compatibility
- **WHEN** a normalized finding has no remediation hint
- **THEN** JSON contains no misleading fabricated remediation object and existing diagnostic fields retain their semantics
