## Purpose

Defines the deterministic, product-owned validity evidence needed to safely
reuse canonical Architecture Health output in bounded public publication.

## ADDED Requirements

### Requirement: Canonical Health evidence carries a finite semantic reuse horizon
The system SHALL derive a semantic validity horizon from the complete canonical
Architecture Health evidence used for publication. The horizon SHALL be the
earliest applicable expiry among evaluated waivers, evaluation-date-bound facts,
required external evidence, and other required context evidence. It SHALL be
deterministic for identical complete input evidence and SHALL not use the
publisher's wall-clock time to change the Architecture Health identity.

#### Scenario: Earliest applicable evidence limits reuse
- **WHEN** complete canonical evidence contains several applicable expiries
- **THEN** its validity receipt reports the earliest expiry as the reuse horizon
- **AND** publication cannot claim a later semantic validity time

#### Scenario: Identical input evidence is deterministic
- **WHEN** the same complete canonical evidence is projected more than once
- **THEN** the resulting validity evidence and canonical headline projection are
  byte-for-byte equivalent
- **AND** no current system time is embedded in the deterministic Health identity

### Requirement: Incomplete validity evidence fails closed
The system SHALL produce an explicit unassessable publication-evidence result
when a required validity fact is missing, unknown, unsupported, inconsistent,
or already expired at the supplied evaluation context. It SHALL not infer an
infinite horizon from an unchanged tree, a publication timestamp, or absent
evidence.

#### Scenario: Unknown horizon is not treated as infinite
- **WHEN** required canonical evidence cannot establish a finite reuse horizon
- **THEN** the product reports an actionable unassessable publication-evidence
  result
- **AND** no ready publication payload or renewal evidence is produced

#### Scenario: Expired evidence cannot be renewed by publication time
- **WHEN** the supplied evaluation context is later than an applicable waiver
  or required external-evidence expiry
- **THEN** publication evidence is unassessable
- **AND** a newer publisher verification time does not make it ready
