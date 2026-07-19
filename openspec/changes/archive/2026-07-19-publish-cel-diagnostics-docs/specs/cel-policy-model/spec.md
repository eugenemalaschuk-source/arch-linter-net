## MODIFIED Requirements

### Requirement: Reporting preserves provenance and expression explainability

Expression-bearing nodes SHALL preserve their composed YAML provenance exactly
like existing selector/contract nodes. Implemented predicates SHALL expose the
expression source text, the YAML location, the selector or contract identity,
the context kind, and whether the predicate produced `true`, `false`, or an
evaluation failure through diagnostic output — configuration diagnostics,
context-dependency/allow-only violation diagnostics, semantic coverage
diagnostics, SARIF related locations, and the `explain` CLI verb's path
output for hops attributed to a `when`-bearing selector.

Compilation and evaluation errors SHALL remain outside baseline suppression.
Imported expression-bearing nodes SHALL keep fragment provenance after policy
composition. JSON, human-readable, SARIF, and coverage diagnostics SHALL treat
expressions as properties of the owning selector/contract node rather than as
free-floating source strings.

#### Scenario: Imported predicate retains fragment provenance

- **WHEN** an imported fragment contributes `layers.sales.selector.when`
- **THEN** diagnostics identify the fragment path and YAML property path of
  that exact `when` field

#### Scenario: Baseline does not suppress expression infrastructure errors

- **WHEN** a policy baseline exists and a predicate fails to compile or evaluate
- **THEN** the error is still surfaced and the baseline does not hide it

#### Scenario: Diagnostics name the evaluated predicate

- **WHEN** a configuration, context-dependency, context-allow-only, or semantic
  coverage diagnostic reports why a selector matched, did not match, or failed
- **THEN** it includes the owning YAML location and the exact `when` source
  text for that selector

#### Scenario: SARIF and explain surface expression provenance

- **WHEN** a context-dependency or context-allow-only diagnostic with a
  `when`-bearing selector is rendered as SARIF, or is on a path resolved by the
  `explain` CLI verb
- **THEN** the SARIF related locations and the `explain` output respectively
  include the expression's source text, YAML location, and match result
