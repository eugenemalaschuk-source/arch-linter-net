## MODIFIED Requirements

### Requirement: Reporting preserves provenance and expression explainability

Expression-bearing nodes SHALL preserve their composed YAML provenance exactly
like existing selector/contract nodes. Implemented predicates SHALL expose the
expression source text, the YAML location, the selector or contract identity,
the context kind, and whether the predicate produced `true`, `false`, or an
evaluation failure through diagnostic output — configuration diagnostics,
context-dependency/allow-only violation diagnostics, and semantic coverage
diagnostics.

Compilation and evaluation errors SHALL remain outside baseline suppression.
Imported expression-bearing nodes SHALL keep fragment provenance after policy
composition. JSON, human-readable, and coverage diagnostics SHALL treat
expressions as properties of the owning selector/contract node rather than as
free-floating source strings. The `explain` CLI verb's graph-path output and
future SARIF integration are not required to carry expression provenance in
this wave; that remains separately-scoped follow-up work.

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

### Requirement: Compatibility is preserved and runtime rollout remains fail-closed

Existing literal-only policies SHALL remain valid and unchanged. The policy
schema version SHALL remain `1`; expression support is additive through
optional `when` fields rather than a new versioned root shape. With the
matcher integration delivered by issue #164, live runtime evaluation of `when`
SHALL follow the fail-closed semantics defined by the "Predicate semantics are
fail-closed and do not weaken policy" requirement — a policy declaring `when`
is compiled and evaluated, not rejected outright, and any evaluation failure
blocks the run exactly as a compilation failure would.

The documented model SHALL include worked examples for modular monolith and
Unity/client scenarios and SHALL include negative examples covering broad,
stale, invalid, and policy-weakening predicates.

#### Scenario: Existing monolithic policy remains unchanged

- **WHEN** a repository uses a current literal-only policy with no `when`
  fields
- **THEN** the policy continues to load and validate with no behavior change

#### Scenario: Runtime evaluates `when` with fail-closed semantics

- **WHEN** a user adds a documented `when` field to a selector or contextual
  contract
- **THEN** the live product compiles and evaluates it against real candidates,
  matching `true`/`false` normally and reporting any evaluation failure as a
  blocking configuration error rather than rejecting the field outright or
  silently ignoring it

#### Scenario: Design includes reviewed positive and negative examples

- **WHEN** the durable CEL policy model documentation is read
- **THEN** it contains modular-monolith and Unity/client examples and also
  negative examples for stale map access, unsupported regex/functions, broad
  `true` predicates, and policy-weakening exclusions
