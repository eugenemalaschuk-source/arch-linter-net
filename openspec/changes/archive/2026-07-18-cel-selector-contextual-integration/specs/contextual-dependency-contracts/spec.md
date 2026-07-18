## ADDED Requirements

### Requirement: Contextual selectors additionally evaluate an optional `when` predicate

A `source`, `forbidden`, or `exclude` selector MAY declare `when`, compiled under the typed contextual source/target context defined by `cel-policy-model`, and when present it SHALL be evaluated as an additional AND-ed condition alongside the selector's literal `role`/`metadata` constraints: a candidate matches only if both the literal constraints match and the compiled `when` evaluates to `true` against that candidate's typed context. A selector with no `when` SHALL behave exactly as before this requirement — it never constructs a CEL context and never touches the CEL engine.

A `when` evaluation failure (as opposed to a well-typed `false` result) SHALL be reported as a blocking policy/configuration error for the run, identical in severity to a compilation error, for both `strict_context_dependencies` and `audit_context_dependencies` contracts. It SHALL NOT be treated as a non-match, SHALL NOT be downgraded by audit contract's normally non-blocking behavior, and SHALL NOT be suppressed by baseline.

#### Scenario: `when` refines a literal role/metadata match

- **WHEN** a `forbidden` selector declares `role: DomainLayer` and
  `when: "target.metadataText[\"domain\"] == source.metadataText[\"domain\"]"`
- **THEN** a candidate target type matches only if its resolved role is
  `DomainLayer` and the compiled predicate evaluates to `true` for that
  source/target pair

#### Scenario: Well-typed false is an ordinary non-match

- **WHEN** a selector's `when` evaluates to `false` for a candidate that
  otherwise matches the selector's literal criteria
- **THEN** the candidate is excluded from that selector's match set and no
  expression error is reported

#### Scenario: Evaluation failure blocks a strict contextual dependency run

- **WHEN** a `strict_context_dependencies` contract's `forbidden[*].when`
  evaluation fails (e.g. a referenced metadata key is absent)
- **THEN** the run fails with a reported expression evaluation error, not an
  ordinary violation and not a silent non-match

#### Scenario: Evaluation failure blocks an audit contextual dependency run

- **WHEN** an `audit_context_dependencies` contract's `source.when` evaluation
  fails
- **THEN** the run fails as a policy/configuration error even though audit
  contract violations would otherwise be non-blocking

#### Scenario: Selector without `when` is unaffected

- **WHEN** a contextual selector declares only `role`/`metadata` with no
  `when`
- **THEN** matching behaves identically to its pre-#164 behavior, with no CEL
  context constructed for that selector

## MODIFIED Requirements

### Requirement: Contextual dependency diagnostics carry source/target role, metadata, and selector evidence

Every contextual dependency violation SHALL produce a diagnostic
distinguishable from existing namespace/layer dependency diagnostics,
including the source type's resolved role and relevant metadata, the target
type's resolved role and relevant metadata, and which selector (`forbidden`)
produced the match. When the matching or excluding selector declared `when`,
the diagnostic SHALL also carry the expression source text, its owning YAML
location, and whether it contributed a match (`true`) — an evaluation failure
is reported through the separate expression-evaluation-error path defined by
the "Contextual selectors additionally evaluate an optional `when` predicate"
requirement, not through this violation diagnostic shape. This evidence SHALL
be present in both JSON and human-readable output.

#### Scenario: JSON diagnostic includes role and metadata evidence

- **WHEN** a contextual dependency violation is reported in JSON output
- **THEN** the diagnostic entry includes the source role, source metadata,
  target role, target metadata, and the matched selector kind

#### Scenario: Human-readable diagnostic is distinguishable from a namespace/layer dependency violation

- **WHEN** a contextual dependency violation and a namespace/layer dependency
  violation are both reported in human-readable output
- **THEN** a reader can determine from the output alone which finding
  originated from a contextual contract and which from a namespace/layer
  contract

#### Scenario: Diagnostic identifies the participating `when` expression

- **WHEN** a contextual dependency violation's matching `forbidden` selector
  declared `when`
- **THEN** the JSON and human-readable diagnostic both include that
  expression's source text and YAML location alongside the existing role and
  metadata evidence
