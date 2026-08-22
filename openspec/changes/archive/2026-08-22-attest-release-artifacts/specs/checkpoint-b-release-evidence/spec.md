## ADDED Requirements

### Requirement: Checkpoint B precedes pre-publication provenance authority
Checkpoint B acceptance SHALL remain a required prerequisite for GitHub build
provenance generation. Its successful candidate authorization SHALL hand the
same immutable package manifest and derived checksum evidence to the separate
provenance authority gate; it SHALL NOT permit NuGet publication or GitHub
Release attachment until that gate independently verifies every attestation.

#### Scenario: Checkpoint B fails
- **WHEN** Checkpoint B does not authorize the immutable candidate
- **THEN** the provenance-producing job and every publication handoff do not
  run

#### Scenario: Checkpoint B passes
- **WHEN** Checkpoint B authorizes the immutable candidate
- **THEN** the workflow re-verifies that same frozen candidate and its outer
  evidence before provenance authority can pass
