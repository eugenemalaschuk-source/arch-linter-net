## ADDED Requirements

### Requirement: Applicability diagnostics preserve assessment evidence
The normalized diagnostic model SHALL represent projected unassessable
applicability evidence as a typed diagnostic family. Its typed details SHALL
preserve the canonical effective-control identity, family, expected membership,
valid produced state when present, collection-integrity outcome, ordered reason
codes, and canonical provenance. The diagnostic's canonical identity SHALL be
derived from those stable semantic values and SHALL NOT depend on display text,
runtime enumeration order, local paths, timestamps, or an independently
reconstructed policy identity.

The normalized diagnostic family SHALL participate in the same deterministic
ordering and every concrete-subtype formatter projection as other normalized
diagnostics. It SHALL be additive to existing diagnostic kinds and leave
policies without applicability opt-in unchanged.

#### Scenario: Integrity evidence remains typed
- **WHEN** produced applicability records contain an identity absent from the
  expected-membership collection
- **THEN** the normalized diagnostic identifies that control and provenance
  with the `unknown_applicability_record_identity` reason rather than a generic
  string-only error

#### Scenario: Equivalent assessment evidence has one canonical identity
- **WHEN** the same applicability assessment is projected twice with different
  runtime enumeration order or Human message wording
- **THEN** the normalized diagnostics have equal canonical identities and
  deterministic ordering
