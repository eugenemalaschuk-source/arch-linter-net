# contextual-dependency-contracts Specification

## Purpose
Evaluates strict and audit contextual dependency contracts that forbid a source type from referencing a target type based on discovered semantic role/metadata selectors, without requiring either side to be declared as a `layers.<name>`.
## Requirements
### Requirement: Contextual dependency contract family exists with strict and audit variants
The reviewed schema and runtime SHALL support `contracts.strict_context_dependencies` and `contracts.audit_context_dependencies` as a new contract family, registered identically to every other family in the contract-family registry (catalog metadata, checker delegate, owned contract type). Each contract entry SHALL declare `name`, optional `id`, a `source` selector, a `forbidden` list of selectors, an optional `exclude` list of selectors, optional `ignored_violations`, and `reason`.

#### Scenario: Strict contextual dependency contract fails the build on violation
- **WHEN** a `strict_context_dependencies` contract's `source` selector matches a type that references a type matching a `forbidden` selector, with no matching `exclude` selector
- **THEN** the analysis reports a build-failing violation

#### Scenario: Audit contextual dependency contract reports without failing the build
- **WHEN** the same violation condition is declared under `audit_context_dependencies` instead of `strict_context_dependencies`
- **THEN** the analysis reports the finding without failing the build, consistent with existing `audit_*` family semantics

### Requirement: Contextual selectors match on discovered role and metadata directly
A contextual selector SHALL declare a non-empty `role` and MAY declare `metadata` key/value constraints. A type matches a contextual selector when the type's role, as resolved by the semantic role index, equals the selector's `role` exactly, and every declared metadata constraint matches the type's resolved metadata under the constraint's operator. A type with no resolved role or missing a constrained metadata key SHALL NOT match.

#### Scenario: Selector without matching role does not match
- **WHEN** a contextual selector declares `role: DomainLayer` and a candidate type's resolved role is `ApplicationLayer` or the type has no resolved role
- **THEN** the type does not match the selector

#### Scenario: Missing constrained metadata key does not match
- **WHEN** a contextual selector declares a metadata constraint for key `domain` and a candidate type's resolved role matches but its resolved metadata has no `domain` entry
- **THEN** the type does not match the selector

### Requirement: Contextual metadata constraints support four deterministic operators
A contextual selector's metadata value SHALL be interpreted using exactly one of four forms, checked in this fixed order: a YAML sequence of literal scalars (`in` — matches if the type's resolved value equals any entry, using the same cross-domain equality as existing selector metadata comparison), the literal string `"*"` (`any` — matches any resolved value for that key), a string matching the pattern `!{source.metadata.<key>}` (`not-equal-to-source` — matches when the candidate's resolved value for that key differs from the *current match's source type's* own resolved value for `<key>`), or any other literal scalar (`exact` — matches using the same string/boolean/decimal cross-representation equality as existing selector metadata comparison). No regex or open-ended expression syntax SHALL be introduced beyond these four forms.

#### Scenario: in operator matches any listed value
- **WHEN** a selector declares `domain: [Sales, Inventory]`
- **THEN** a candidate type whose resolved `domain` metadata is `Sales` or `Inventory` matches, and any other value does not

#### Scenario: any operator matches every present value
- **WHEN** a selector declares `domain: "*"`
- **THEN** a candidate type matches regardless of its resolved `domain` value, provided the key is present

#### Scenario: not-equal-to-source operator compares against the current source type's own metadata
- **WHEN** a `forbidden` selector declares `domain: "!{source.metadata.domain}"` and the contract is currently evaluating a source type whose resolved `domain` metadata is `Sales`
- **THEN** a candidate target type matches only if its resolved `domain` metadata is present and not equal to `Sales`

#### Scenario: not-equal-to-source operator does not match when the source lacks the referenced key
- **WHEN** a `forbidden` selector declares `domain: "!{source.metadata.domain}"` and the current source type has no resolved `domain` metadata
- **THEN** no candidate target type matches this constraint, since there is no source value to be unequal to

#### Scenario: exact operator matches literal scalars only
- **WHEN** a selector declares `domain: Sales`
- **THEN** only a candidate type whose resolved `domain` metadata equals `Sales` (using cross-domain string/boolean/decimal equality) matches

### Requirement: Exclude selectors suppress candidate targets before violation evaluation
A `forbidden`/`allowed` list entry that also matches any selector in the contract's `exclude` list SHALL be removed from violation consideration entirely, distinct from and evaluated prior to `ignored_violations` post-match suppression.

#### Scenario: Excluded target produces no violation
- **WHEN** a candidate target type matches a `forbidden` selector and also matches an `exclude` selector
- **THEN** the analysis reports no violation for that source/target pair

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

### Requirement: Ignored violations apply to contextual dependency contracts identically to existing contracts
A `strict_context_dependencies`/`audit_context_dependencies` contract's `ignored_violations` list SHALL suppress matching violations using the same `SourceType`/`ForbiddenReference`/`reason` matching behavior as the existing `dependency` family, including unmatched-ignore reporting.

#### Scenario: Ignored violation suppresses a matching contextual finding
- **WHEN** a contextual dependency contract declares an `ignored_violations` entry matching a specific source/target type pair that would otherwise violate
- **THEN** that specific violation is suppressed and not reported

### Requirement: Contextual contracts are documented and schema-validated
The reviewed JSON schema SHALL validate `strict_context_dependencies`/`audit_context_dependencies` contract shapes, including selector `role`/`metadata` structure and the four metadata operator forms. Documentation and at least one example policy SHALL demonstrate the family.

#### Scenario: Schema rejects a contextual selector without a role
- **WHEN** a `context_dependencies` contract's `source`, `forbidden`, or `exclude` entry omits `role`
- **THEN** schema validation rejects the document as invalid

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

