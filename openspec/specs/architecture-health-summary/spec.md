# architecture-health-summary Specification

## Purpose
Provide a deterministic, versioned Architecture Health summary that reports
the assessability and state of repository architecture governance without
recomputing any owning authority or producing a compensating score.

## Requirements

### Requirement: Health summary projects canonical governance authorities

The system SHALL expose a versioned `architecture-health/v1` summary composed
only from canonical current-validation, applicability/completion, coverage,
topology, baseline-debt, waiver-lifecycle/inventory, policy-weakening,
metric-budget, imported-external-diagnostic, and audit-validation evidence.
Each configured dimension SHALL retain its typed state and deterministic
reason/provenance references. The summary SHALL distinguish `not_configured`
and `not_applicable` from assessable zero or clean evidence.

The summary SHALL not load policy YAML, rescan assemblies, reread SARIF,
recount policy controls, recompute waiver lifecycle, reclassify baseline
entries, or derive a second metric engine. Optional history or forensics
context, when available, SHALL remain advisory and SHALL not change the gate
or health state.

#### Scenario: Audit evidence remains observable without strict failure
- **WHEN** a Health evaluation includes an audit receipt with diagnostics but
  every configured strict-blocking authority passes
- **THEN** the summary reports those diagnostics in a non-blocking audit
  evidence dimension and preserves `gate=pass`

#### Scenario: Clean complete governance is healthy
- **WHEN** all required canonical evidence is assessable, the current
  governance outcome passes, and no reviewed finding debt, waiver debt, new
  debt, weakening, or metric regression exists
- **THEN** the summary reports `gate=pass` and `health=healthy`

#### Scenario: Missing inventory is not zero inventory
- **WHEN** a compatibility or incomplete result does not provide the canonical
  effective-policy inventory
- **THEN** the summary reports that dimension as unassessable and does not
  fabricate zero effective controls or zero waiver debt

### Requirement: Gate and health precedence are deterministic and non-compensating

The summary SHALL expose `gate` as `pass`, `fail`, or `unassessable` and
`health` as `healthy`, `debt`, `degrading`, `failing`, or `unassessable`.
Health precedence SHALL be deterministic and non-compensating:
`unassessable` > `failing` > `degrading` > `debt` > `healthy`.

Missing, malformed, incomplete, stale, ambiguous, untrusted, or wrong-context
required evidence SHALL yield `gate=unassessable` and
`health=unassessable`. A failing configured authoritative dimension SHALL
yield `gate=fail`; current strict governance failure, including invalid or
expired waiver state, SHALL yield at least `health=failing`. Exact reviewed
finding debt and valid active reviewed-waiver debt MAY preserve `gate=pass`
but SHALL yield at least `health=debt`. New architecture debt, semantic policy
weakening, a new or broadened waiver, reportable warning coverage, audit-only
diagnostics, and metric regression SHALL remain separate from reviewed debt and
yield at least `health=degrading`; any source authority that treats that state
as blocking SHALL remain visible through the independent gate result.

#### Scenario: Warning coverage does not become a gate failure
- **WHEN** the canonical coverage receipt contains findings and
  `analysis.coverage` is `warn`
- **THEN** the coverage dimension retains those findings as non-blocking
  evidence and the Health gate remains `pass` when other blocking authorities
  pass

#### Scenario: Error coverage remains an authority failure
- **WHEN** the canonical coverage receipt contains findings and
  `analysis.coverage` is `error`
- **THEN** the coverage dimension is `fail` and the Health gate is `fail`

#### Scenario: Reviewed finding debt does not become new debt
- **WHEN** a current finding exactly matches the reviewed baseline and all
  other required evidence is assessable
- **THEN** the summary reports `gate=pass`, `health=debt`, and distinct
  reviewed-finding-debt evidence

#### Scenario: Current strict violation is failing
- **WHEN** the canonical current strict validation outcome fails
- **THEN** the summary reports `gate=fail` and `health=failing` without
  allowing clean dimensions to compensate

#### Scenario: Unassessable evidence dominates a current failure
- **WHEN** a configured authoritative dimension fails and another required
  dimension is unassessable
- **THEN** the summary reports `gate=unassessable` and
  `health=unassessable` while retaining both dimensions for drill-down

### Requirement: Applicability, debt, waiver, topology, metric, and external evidence retain their meanings

The summary SHALL preserve applicability-required controls, evaluable controls,
and unassessable controls independently from the effective-policy control
count. Missing required applicability records SHALL remain in the required
denominator and SHALL not improve an evaluability ratio. Finding debt and
explicit waiver debt SHALL remain separate. When declared topology is
configured, topology mapping evidence SHALL distinguish complete mapping from
unmapped or ambiguous required subjects. Required external evidence SHALL be
assessable only from its canonical trust, selection, and normalized finding
evidence; valid evidence for another repository, revision, or scope SHALL be
unassessable rather than clean.

#### Scenario: Missing applicability evidence cannot shrink the denominator
- **WHEN** the effective inventory has controls and one required applicability
  record is missing
- **THEN** the summary retains the required control in applicability evidence,
  reports that dimension as unassessable, and does not infer completeness from
  the inventory count or zero findings

#### Scenario: Valid active waiver debt remains distinct
- **WHEN** the policy inventory contains a valid active reviewed waiver and no
  new or invalid waiver state
- **THEN** the summary reports explicit waiver debt separately from finding
  debt and reports at least `health=debt`

#### Scenario: Wrong-context external evidence is unassessable
- **WHEN** imported external evidence is validly parsed but is bound to a
  different revision or scope than the current analysis
- **THEN** the summary reports external-evidence unassessability rather than
  a zero-clean external result

### Requirement: Human, JSON, and Testing projections share one Core result

Core, CLI human output, CLI JSON output, and the NUnit Testing surface SHALL
project the same canonical health result and dimension states. JSON SHALL use
the `architecture-health/v1` schema identifier, stable ordering, and
machine-readable gate/health reasons. The CLI SHALL use the established gate
exit contract: `pass` exits 0, `fail` exits 1, and `unassessable` exits 2.
It SHALL distinguish valid-but-unassessable health evidence from invalid
arguments, policy, or runtime failures that also exit 2.

#### Scenario: Projections agree on external unassessability
- **WHEN** required external evidence is missing or bound to the wrong revision
- **THEN** Core, human output, JSON, and the Testing surface expose the same
  `unassessable` gate and health states with the same dimension reasons

#### Scenario: Audit-only diagnostics do not become a gate failure
- **WHEN** the current analysis contains only audit diagnostics and every
  configured blocking authority passes
- **THEN** the summary retains the audit evidence without converting it into a
  failing gate state

#### Scenario: Repeated projection is deterministic
- **WHEN** equivalent canonical authority inputs are projected more than once
- **THEN** the resulting summary, dimension ordering, human output, and JSON
  output are equivalent

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
The Health result SHALL expose the existing immutable analysis snapshot
counters so consumers can verify this reuse without initiating another
analysis.

#### Scenario: Health shares one measurable analysis with baseline verification
- **WHEN** one Health evaluation requests current validation and persistent
  baseline debt
- **THEN** its snapshot counters report one policy composition, one project
  graph evaluation, one snapshot materialization, and the selected assembly
  load set while the debt-gate receipt reports snapshot reuse

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
