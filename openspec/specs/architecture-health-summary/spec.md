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
metric-budget, and imported-external-diagnostic evidence. Each configured
dimension SHALL retain its typed state and deterministic reason/provenance
references. The summary SHALL distinguish `not_configured` and
`not_applicable` from assessable zero or clean evidence.

The summary SHALL not load policy YAML, rescan assemblies, reread SARIF,
recount policy controls, recompute waiver lifecycle, reclassify baseline
entries, or derive a second metric engine. Optional history or forensics
context, when available, SHALL remain advisory and SHALL not change the gate
or health state.

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
weakening, a new or broadened waiver, and metric regression SHALL remain
separate from reviewed debt and yield at least `health=degrading`; any source
authority that treats that state as blocking SHALL remain visible through the
independent gate result.

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
