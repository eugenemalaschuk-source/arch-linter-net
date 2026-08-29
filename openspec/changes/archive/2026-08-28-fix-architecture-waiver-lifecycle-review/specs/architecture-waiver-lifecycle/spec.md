## MODIFIED Requirements

### Requirement: Structured architecture waivers are accountable and exact
The system SHALL support a structured `ignored_violations` waiver with a unique
stable `id` per authored composed-policy declaration, the existing governed
contract identity, an exact lowercase `sha256:` target fingerprint derived from
the versioned canonical finding identity, a non-empty reason, owner token,
remediation issue/reference, introduced date, and expiry date. Its canonical
record SHALL retain the composed source fragment and policy location. Display
text alone SHALL NOT identify a target. Source-set expansion SHALL NOT create
duplicate declarations or lifecycle records for one authored waiver.

#### Scenario: Complete structured waiver suppresses its exact finding
- **WHEN** a strict policy contains a complete structured waiver whose
  fingerprint equals one governed finding's canonical identity fingerprint
- **THEN** that finding is suppressed and the canonical waiver record retains
  its ID, contract, target, remediation metadata, and policy provenance

#### Scenario: Duplicate IDs across fragments are rejected
- **WHEN** composed policy fragments declare two distinct structured waivers
  with the same ID
- **THEN** policy loading fails with both authored waiver locations

#### Scenario: Source-set aliases share one authored waiver
- **WHEN** one authored contract with a structured waiver expands into multiple
  source-set instances and the waiver matches a finding from one instance
- **THEN** validation accepts the single declaration and produces one matching
  canonical waiver record rather than a duplicate or stale record

#### Scenario: Noncanonical fingerprint is rejected
- **WHEN** a structured waiver uses uppercase hexadecimal in its SHA-256 target
  fingerprint
- **THEN** policy validation fails before matching with an actionable canonical
  fingerprint diagnostic

### Requirement: Waiver lifecycle is deterministic and visible
The system SHALL return one lifecycle record for every configured authored
manual waiver using the states `active`, `stale`, `expired`,
`metadata_incomplete`, and `invalid`. Evaluation SHALL aggregate all selected
expanded-contract aliases of one authored waiver before calculating whether it
matches. Evaluation SHALL use a date-only `yyyy-MM-dd` evaluation date and
include that date in canonical evidence. State precedence SHALL be invalid,
expired, stale, metadata-incomplete, then active, so an expired waiver remains
expired even when its governed finding is gone. Malformed manual waiver
metadata or identity SHALL produce canonical `invalid` evidence and fail
closed rather than disappearing into an unstructured failure.

#### Scenario: Expired matched waiver is blocking
- **WHEN** a complete strict-profile waiver still matches its governed finding
  and its expiry is before the supplied evaluation date
- **THEN** its state is `expired` and the strict validation outcome fails

#### Scenario: Expired waiver whose finding is gone remains expired
- **WHEN** a complete waiver expires before the supplied evaluation date and no
  longer matches a governed finding
- **THEN** its state is `expired`, with stale-match evidence retained for human
  explanation, rather than disappearing or becoming active

#### Scenario: Invalid waiver remains canonical evidence
- **WHEN** a manual waiver has malformed required structured metadata or target
  identity
- **THEN** validation fails closed and exposes one `invalid` lifecycle record
  with its available authored declaration and policy provenance

#### Scenario: Explicit dates agree across environments
- **WHEN** local and CI validation use the same policy and explicit evaluation
  date
- **THEN** they produce the same lifecycle states and canonical records

### Requirement: Canonical lifecycle evidence is projected consistently
Core validation, CLI Human/JSON output, and the Testing adapter SHALL project
the same waiver IDs, states, target identities, metadata, evaluation date, and
policy provenance without parsing raw YAML. Human diagnostics SHALL identify
the waiver ID, rule, target fingerprint, reason, owner, issue, and expiry.

#### Scenario: Adapter and CLI agree on expired waiver evidence
- **WHEN** validation detects an expired structured waiver
- **THEN** the Testing result and CLI JSON expose the same canonical waiver ID,
  state, evaluation date, target fingerprint, and remediation metadata

#### Scenario: Human diagnostics expose structured review fields
- **WHEN** human output includes a structured waiver lifecycle record
- **THEN** it renders that record's target fingerprint and reason alongside its
  ID, contract, owner, issue, and expiry
