## ADDED Requirements

### Requirement: Remediation providers are completeness-protected alongside detail projection
The reporting layer SHALL register one intentional remediation-hint provider for every sealed non-abstract `ArchitectureDiagnostic` subtype, in addition to the existing detail projector. A provider MAY deliberately return no hint, but an unregistered subtype SHALL fail loudly and be detected by a completeness test.

#### Scenario: New diagnostic type cannot silently omit remediation handling
- **WHEN** a new sealed `ArchitectureDiagnostic` subtype is added without a remediation provider registration
- **THEN** the registry completeness test fails and identifies the unregistered type

#### Scenario: Intentional no-hint fallback is preserved
- **WHEN** a registered provider determines that its diagnostic lacks safe evidence
- **THEN** it returns no specialized hint or `review_contract` explicitly, without changing the diagnostic's detail projection
