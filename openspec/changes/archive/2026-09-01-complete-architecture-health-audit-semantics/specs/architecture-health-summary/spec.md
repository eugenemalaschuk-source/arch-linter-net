## MODIFIED Requirements

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
