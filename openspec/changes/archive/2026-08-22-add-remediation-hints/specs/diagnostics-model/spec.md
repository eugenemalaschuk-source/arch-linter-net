## ADDED Requirements

### Requirement: The typed finding envelope carries optional remediation metadata
`ArchitectureFinding` SHALL expose an optional typed remediation-hint property without changing its canonical identity, typed diagnostic details, schema version, or behavior when no safe hint exists.

#### Scenario: Finding identity is unchanged by guidance
- **WHEN** a finding receives a remediation hint
- **THEN** its `CanonicalIdentity` and structured `ArchitectureViolationIdentity` are identical to the values produced for the same diagnostic without hint projection

#### Scenario: Existing finding remains compatible without guidance
- **WHEN** a diagnostic has no safe deterministic remediation hint
- **THEN** the normalized finding remains valid with an absent remediation-hint property and retains all prior diagnostic evidence
