## ADDED Requirements

### Requirement: Health retains authority conformance and provenance

The Health summary SHALL treat canonical applicability records only as
evaluability evidence. It SHALL derive the conformance of configured topology,
metric, and imported-external dimensions from their owner’s existing typed
finding or evidence receipt. An evaluable record SHALL NOT by itself produce a
passing Health dimension. A zero-finding canonical receipt SHALL produce
`pass`; a blocking canonical receipt SHALL produce `fail`; and an unassessable
receipt SHALL produce `unassessable`.

Every Health reason derived from canonical applicability, finding, lifecycle,
or evidence data SHALL retain available family, control identity, policy
identity, and bounded evidence identity in the machine-readable result. A
Health dimension name SHALL NOT replace canonical provenance.

#### Scenario: Metric receipt has a breach
- **WHEN** a metric authority is evaluable and its canonical receipt contains
  an absolute or baseline-relative metric-budget breach
- **THEN** the metrics dimension reports `fail` with its canonical control and
  evidence reference, regardless of the evaluability record state

#### Scenario: External receipt has no findings
- **WHEN** a configured imported-external authority has trusted, selected,
  zero-finding evidence
- **THEN** the external-evidence dimension reports `pass`

#### Scenario: External receipt has a blocking finding
- **WHEN** a configured imported-external authority has a blocking normalized
  finding
- **THEN** the external-evidence dimension reports `fail` and JSON retains the
  canonical reason provenance

#### Scenario: Topology receipt has a failure
- **WHEN** declared topology is evaluable and its canonical receipt has an
  unmapped, ambiguous, or otherwise blocking topology finding
- **THEN** the topology dimension reports `fail` rather than `pass`

### Requirement: Health preserves lifecycle and baseline-change semantics

The Health waiver-debt dimension SHALL project canonical lifecycle records,
not only aggregate inventory totals. It SHALL retain active, stale, expired,
metadata-incomplete, and invalid states as deterministic lifecycle reasons and
shall preserve the selected profile’s blocking state. A blocking stale,
expired, metadata-incomplete, or invalid lifecycle record SHALL NOT be
classified as ordinary reviewed debt.

Persistent baseline comparison SHALL reuse the Health evaluation’s canonical
analysis state and candidate collection rather than initiate a second
repository-analysis path. Resolved-only baseline entries SHALL remain visible
as baseline hygiene but SHALL NOT produce `new_architecture_debt=degrading`.

#### Scenario: Active waiver remains reviewed debt
- **WHEN** the canonical waiver lifecycle contains only active records
- **THEN** the waiver-debt dimension reports `debt` separately from finding
  debt

#### Scenario: Stale lifecycle remains blocking
- **WHEN** the selected waiver profile treats a stale or metadata-incomplete
  lifecycle record as blocking
- **THEN** the waiver-debt dimension reports `fail` with the record identity
  and lifecycle state

#### Scenario: Resolved baseline entry is not new debt
- **WHEN** the persistent-baseline receipt contains resolved entries and no
  new entries
- **THEN** the health result retains the hygiene evidence without reporting
  new architecture debt or degradation

#### Scenario: Health shares analysis with baseline verification
- **WHEN** one Health evaluation requests current validation and persistent
  baseline debt
- **THEN** it uses one canonical analysis state and candidate collection for
  both authorities
