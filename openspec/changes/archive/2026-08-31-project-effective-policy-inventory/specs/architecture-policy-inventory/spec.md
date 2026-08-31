## ADDED Requirements

### Requirement: Effective policy control inventory is canonical and deterministic
The system SHALL expose a versioned `architecture-policy-inventory/v1`
projection for an analyzed effective policy context. Its
`effective_rule_count` SHALL count one configured control per stable effective
contract identity after normal policy composition, condition resolution,
selection, and source-set expansion. A source-set-expanded authored contract
SHALL count once regardless of its executable alias fan-out. Findings, matched
subjects, files, types, edges, baseline entries, and waiver entries SHALL NOT
increase the rule count.

The inventory SHALL expose a deterministic partition of the headline count into
non-coverage `strict`, non-coverage `audit`, and `coverage` controls. Disabled
or optional-empty controls that do not participate in the effective analyzed
scope SHALL NOT be reported as effective controls.

#### Scenario: Source-set aliases count as one control
- **WHEN** one authored dependency contract expands to multiple source-set
  execution aliases in the selected validation scope
- **THEN** the inventory counts that contract once and repeated findings from
  any alias do not change `effective_rule_count`

#### Scenario: Composed strict audit and coverage controls have a stable partition
- **WHEN** a composed effective policy contains strict, audit, and coverage
  controls across imported fragments
- **THEN** the headline count and the strict/audit/coverage breakdown are
  deterministic and the three breakdown counts sum to the headline

#### Scenario: Selected scope does not imply unrelated controls
- **WHEN** validation selects only a subset of effective contract IDs
- **THEN** the inventory describes that exact selected analyzed scope and does
  not count unselected controls

### Requirement: Explicit waiver debt consumes the canonical lifecycle
The policy inventory SHALL expose an `ignore_debt` summary over the canonical
current manual waiver lifecycle records produced by the waiver-lifecycle
authority. It SHALL include `total`, `active`, `stale`, `expired`,
`metadata_incomplete`, and `invalid` counts plus stable lifecycle records for
drill-down. Each configured waiver record SHALL contribute exactly once to
`total` and to the one state selected by the authoritative lifecycle
precedence; the inventory SHALL NOT re-parse waiver metadata, match findings,
or evaluate expiry.

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

### Requirement: Core CLI and Testing use the same inventory projection
Validation SHALL attach the canonical policy inventory to its Core outcome and
preserve it through cached-result reconstruction and the NUnit Testing adapter.
CLI human output SHALL render compact policy-control and waiver-debt lines, and
CLI JSON output SHALL expose the versioned inventory object. These projections
SHALL consume the Core inventory object and SHALL NOT parse policy YAML or
recount controls or waivers independently.

Missing inventory evidence from a compatibility result or older cache payload
SHALL remain absent; no projection SHALL manufacture zero rules or zero waiver
debt.

#### Scenario: CLI and Testing agree on an active waiver inventory
- **WHEN** validation produces an inventory for a policy containing an active
  structured waiver
- **THEN** CLI JSON, human output, and the Testing result expose the same
  effective-rule count and waiver-debt totals

#### Scenario: Missing cache-era inventory is not rendered as zero
- **WHEN** a reconstructed compatibility result lacks policy-inventory evidence
- **THEN** its CLI and Testing projections leave the inventory absent rather
  than emitting an inventory with zero controls and zero waiver debt
