# architecture-metric-baseline-gates Specification

## Purpose
Define reviewed metric-value baselines that let repositories ratchet supported
architecture metrics without conflating scalar metric control with finding debt.

## Requirements

### Requirement: Metric baselines use a separate versioned canonical identity
The baseline file SHALL support a `version: 3` document with a top-level
`metric_baselines` collection separate from its finding-level `baseline`
collection. Each metric baseline entry SHALL carry a metric-identity version,
metric ID, metric kind, native subject, unit when applicable, effective scope,
and a non-negative reviewed value. Its identity SHALL be derived only from
canonical metric/subject fields, never from display text, a contributor label,
or a finding-level violation identity. Exactly one entry SHALL be allowed for
each metric ID in a baseline document.

Metric baselines SHALL NOT create ignored violations, suppress a normal finding,
or participate in #121 finding-debt matching. Existing version-1 and version-2
finding baseline behavior SHALL remain unchanged.

#### Scenario: A version-3 metric baseline is distinct from finding debt
- **WHEN** a version-3 baseline records a reviewed value for metric `app-outgoing`
  and also contains a finding-level ignored violation
- **THEN** the metric value is used only by a relative metric gate and the
  ignored violation retains its existing exact finding-identity behavior

#### Scenario: Duplicate metric baseline IDs are rejected
- **WHEN** a version-3 baseline contains two `metric_baselines` entries for the
  same metric ID
- **THEN** baseline loading fails as configuration rather than selecting an
  entry by YAML order

### Requirement: Relative gates fail closed on unavailable reviewed values
A selected relative metric budget SHALL compare its evaluable current metric
only with one matching reviewed metric baseline entry. A missing entry, an
unknown metric ID, an unsupported metric-identity version, or a changed metric
kind, subject, unit, or effective scope SHALL be explicit unassessable baseline
evidence for the owning budget. It SHALL not be treated as a pass, a zero value,
a finding-level baseline match, or an automatic baseline refresh.

#### Scenario: Missing baseline cannot disable a strict gate
- **WHEN** a strict relative metric budget has an evaluable current metric but
  its supplied baseline contains no matching metric baseline
- **THEN** validation emits explicit unassessable evidence for that budget and
  the strict assessment does not pass

#### Scenario: Metric definition change invalidates the reviewed value
- **WHEN** a baseline entry has the same metric ID as the current policy but a
  different canonical kind, subject, unit, or effective scope
- **THEN** validation reports the entry as stale baseline evidence and does not
  compare its value to the changed metric definition

### Requirement: Metric baseline capture is explicit and does not update values
An explicit `baseline generate` operation for a policy that selects one or more
relative metric budgets SHALL write a deterministic version-3 baseline. It
SHALL include one current scalar entry for each unique, complete referenced
metric and retain the normal structured finding-baseline entries. An incomplete
metric measurement SHALL not be captured as a scalar baseline value.

Baseline update and prune operations SHALL preserve existing metric baseline
entries unchanged; neither those operations nor ordinary validation SHALL add,
replace, or recalculate a reviewed metric value. A changed value requires an
explicit reviewed baseline-generation or manual baseline edit.

#### Scenario: Generation captures a complete shared metric once
- **WHEN** strict and audit relative budgets both reference the same complete
  metric and the user runs `baseline generate`
- **THEN** the generated version-3 file contains exactly one metric baseline
  entry with that metric's deterministic current value

#### Scenario: Update preserves a reviewed metric value
- **WHEN** a version-3 baseline contains a metric value of five, current code
  measures six, and the user runs `baseline update` for ordinary finding debt
- **THEN** the output retains the metric baseline value five rather than
  replacing it with six
