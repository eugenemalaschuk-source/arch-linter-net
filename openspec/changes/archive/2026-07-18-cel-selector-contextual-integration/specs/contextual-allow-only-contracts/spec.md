## ADDED Requirements

### Requirement: Contextual allow-only selectors additionally evaluate an optional `when` predicate

A `source`, `allowed`, or `exclude` selector MAY declare `when`, and it SHALL be evaluated identically to the contextual dependency family's `when` support: additive to literal `role`/`metadata` matching (both must match), evaluated against the typed contextual source/target/dependency context, with a well-typed `false` treated as an ordinary non-match. Any evaluation failure SHALL be reported as a blocking policy/configuration error for both `strict_context_allow_only` and `audit_context_allow_only` contracts, SHALL NOT be suppressed by baseline, and SHALL NOT be downgraded by audit's normally non-blocking behavior. A selector with no `when` SHALL behave exactly as before this requirement.

#### Scenario: `when` refines an allowed-target match

- **WHEN** an `allowed` selector declares `role: DomainLayer` and
  `when: "target.metadataText[\"domain\"] == source.metadataText[\"domain\"]"`
- **THEN** a candidate target type is treated as allowed only if its resolved
  role is `DomainLayer` and the compiled predicate evaluates to `true`

#### Scenario: Evaluation failure blocks a strict contextual allow-only run

- **WHEN** a `strict_context_allow_only` contract's `allowed[*].when`
  evaluation fails
- **THEN** the run fails with a reported expression evaluation error, not an
  ordinary violation and not a silent non-match

#### Scenario: Evaluation failure blocks an audit contextual allow-only run

- **WHEN** an `audit_context_allow_only` contract's `source.when` evaluation
  fails
- **THEN** the run fails as a policy/configuration error even though audit
  contract violations would otherwise be non-blocking

#### Scenario: Selector without `when` is unaffected

- **WHEN** a contextual allow-only selector declares only `role`/`metadata`
  with no `when`
- **THEN** matching behaves identically to its pre-#164 behavior

## MODIFIED Requirements

### Requirement: Contextual allow-only diagnostics carry source/target role, metadata, and selector evidence

Every contextual allow-only violation SHALL produce a diagnostic
distinguishable from existing namespace/layer allow-only diagnostics,
including the source type's resolved role and relevant metadata, the target
type's resolved role and relevant metadata, and that no `allowed` selector
matched. When an `allowed` or `exclude` selector considered for the violation
declared `when`, the diagnostic SHALL also carry that expression's source text
and owning YAML location — an evaluation failure is reported through the
separate expression-evaluation-error path, not through this violation
diagnostic shape. This evidence SHALL be present in both JSON and
human-readable output.

#### Scenario: JSON diagnostic includes role and metadata evidence

- **WHEN** a contextual allow-only violation is reported in JSON output
- **THEN** the diagnostic entry includes the source role, source metadata,
  target role, and target metadata

#### Scenario: Diagnostic identifies a participating `when` expression

- **WHEN** a contextual allow-only violation's nearest-matching `allowed`
  selector declared `when` that evaluated `false` for the candidate target
- **THEN** the JSON and human-readable diagnostic both include that
  expression's source text and YAML location alongside the existing role and
  metadata evidence
