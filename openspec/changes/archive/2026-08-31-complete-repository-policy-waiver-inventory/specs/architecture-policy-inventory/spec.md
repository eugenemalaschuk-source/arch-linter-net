## MODIFIED Requirements

### Requirement: Explicit waiver debt consumes the canonical lifecycle
The policy inventory SHALL expose an `ignore_debt` summary over the canonical
current manual waiver lifecycle records produced by the waiver-lifecycle
authority. It SHALL include `total`, `active`, `stale`, `expired`,
`metadata_incomplete`, and `invalid` counts plus stable lifecycle records for
drill-down. Each configured waiver record in every selected effective strict
and audit policy mode SHALL contribute exactly once to `total` and to the one
state selected by the authoritative lifecycle precedence; the inventory SHALL
NOT re-parse waiver metadata, match findings, or evaluate expiry. Strict and
audit outcomes with the same selected effective policy SHALL expose identical
inventory waiver records and debt counts, regardless of the mode that is
currently gating validation.

Baseline finding debt, ordinary audit findings, and intended policy scope such
as selector, generated-code, test, source, coverage, or allow-list exclusions
SHALL remain outside explicit waiver debt unless #687 produced a manual waiver
lifecycle record for them.

#### Scenario: Expired unmatched waiver remains one visible debt record
- **WHEN** the waiver lifecycle reports an expired waiver whose governed
  finding no longer matches
- **THEN** `total` includes it once, `expired` includes it once, and `stale`
  is not incremented for that same record

#### Scenario: Legacy compatibility waiver remains disclosed
- **WHEN** a compatibility policy produces a `metadata_incomplete` lifecycle
  record for a legacy ignored violation
- **THEN** the inventory reports non-zero total and metadata-incomplete debt
  rather than treating the record as active or omitting it

#### Scenario: Structural exclusion is not waiver debt
- **WHEN** a policy has an `exclude_sources` or coverage exclusion but no
  manual waiver lifecycle record
- **THEN** the inventory reports no waiver debt for that exclusion

#### Scenario: Strict and audit waivers share one repository inventory
- **WHEN** the selected effective policy has one strict and one audit manual
  waiver
- **THEN** each mode's inventory contains both lifecycle records and reports a
  total of two, while each mode's validation waiver result remains mode-local
