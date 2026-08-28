# architecture-waiver-lifecycle Specification

## Purpose
TBD - created by archiving change add-architecture-waiver-lifecycle. Update Purpose after archive.
## Requirements
### Requirement: Structured architecture waivers are accountable and exact
The system SHALL support a structured `ignored_violations` waiver with a unique
stable `id`, the existing governed contract identity, an exact
`target.fingerprint` derived from the versioned canonical finding identity, a
non-empty reason, owner token, remediation issue/reference, introduced date,
and expiry date. Its canonical record SHALL retain the composed source fragment
and policy location. Display text alone SHALL NOT identify a target.

#### Scenario: Complete structured waiver suppresses its exact finding
- **WHEN** a strict policy contains a complete structured waiver whose
  fingerprint equals one governed finding's canonical identity fingerprint
- **THEN** that finding is suppressed and the canonical waiver record retains
  its ID, contract, target, remediation metadata, and policy provenance

#### Scenario: Duplicate IDs across fragments are rejected
- **WHEN** composed policy fragments declare two structured waivers with the
  same ID
- **THEN** policy loading fails with both authored waiver locations

### Requirement: Waiver lifecycle is deterministic and visible
The system SHALL return one lifecycle record for every configured manual waiver
using the states `active`, `stale`, `expired`, `metadata_incomplete`, and
`invalid`. Evaluation SHALL use a date-only `yyyy-MM-dd` evaluation date and
include that date in canonical evidence. State precedence SHALL be invalid,
expired, stale, metadata-incomplete, then active, so an expired waiver remains
expired even when its governed finding is gone.

#### Scenario: Expired matched waiver is blocking
- **WHEN** a complete strict-profile waiver still matches its governed finding
  and its expiry is before the supplied evaluation date
- **THEN** its state is `expired` and the strict validation outcome fails

#### Scenario: Expired waiver whose finding is gone remains expired
- **WHEN** a complete waiver expires before the supplied evaluation date and no
  longer matches a governed finding
- **THEN** its state is `expired`, with stale-match evidence retained for human
  explanation, rather than disappearing or becoming active

#### Scenario: Explicit dates agree across environments
- **WHEN** local and CI validation use the same policy and explicit evaluation
  date
- **THEN** they produce the same lifecycle states and canonical records

### Requirement: Legacy policy compatibility is explicit
Version-1 policy defaults to the `compatibility` waiver profile and SHALL retain
legacy glob ignore matching. Legacy entries SHALL be represented as
`metadata_incomplete` waiver debt when lifecycle evidence is requested; absent
metadata SHALL NOT mean active or healthy. Version-2 policy defaults to the
`strict` profile, and a policy MAY explicitly select either supported profile
through schema-backed analysis configuration.

#### Scenario: Existing legacy policy remains valid
- **WHEN** an otherwise valid version-1 policy contains an existing legacy
  ignore entry without structured metadata
- **THEN** it retains legacy suppression behavior and exposes
  `metadata_incomplete` lifecycle evidence without a schema-breaking failure

#### Scenario: Strict profile rejects a partial structured waiver
- **WHEN** a version-2 or explicitly strict policy authors an entry with any
  structured waiver field but missing required accountability metadata or a
  valid target fingerprint
- **THEN** policy validation fails closed with an actionable waiver diagnostic

### Requirement: Strict governance enforces waiver hygiene without a new gate
The strict profile SHALL fail closed for invalid or expired waivers and SHALL
fail policy hygiene for stale waivers. It SHALL expose compatibility-profile
metadata-incomplete debt without representing it as healthy. Adding or
broadening a structured waiver SHALL remain change-time evidence for the
existing policy-weakening and debt-gate authorities; the lifecycle evaluator
SHALL NOT implement a second ratchet or baseline lifecycle.

#### Scenario: Stale structured waiver is removable blocking debt
- **WHEN** a strict-profile structured waiver no longer suppresses its governed
  finding and is not expired or invalid
- **THEN** it is reported as `stale` and validation fails policy hygiene

#### Scenario: Compatibility downgrade remains visible
- **WHEN** a policy explicitly selects the compatibility profile for a legacy
  waiver
- **THEN** its metadata-incomplete lifecycle record remains machine-readable
  and is not reclassified as active or healthy

### Requirement: Canonical lifecycle evidence is projected consistently
Core validation, CLI Human/JSON output, and the Testing adapter SHALL project
the same waiver IDs, states, target identities, metadata, evaluation date, and
policy provenance without parsing raw YAML. Human diagnostics SHALL identify
the waiver ID, rule, target, reason, owner, issue, and expiry.

#### Scenario: Adapter and CLI agree on expired waiver evidence
- **WHEN** validation detects an expired structured waiver
- **THEN** the Testing result and CLI JSON expose the same canonical waiver ID,
  state, evaluation date, target fingerprint, and remediation metadata
