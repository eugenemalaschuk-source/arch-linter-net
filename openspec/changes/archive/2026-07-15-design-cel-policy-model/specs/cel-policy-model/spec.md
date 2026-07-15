## ADDED Requirements

### Requirement: CEL-backed predicates use explicit additive `when` fields

The policy expression model SHALL use explicit optional `when` fields and SHALL
NOT implicitly interpret any existing literal scalar field as a CEL expression.
Literal policies remain the default authoring path. A `when` field SHALL compile
as a boolean predicate under ArchLinter CEL Profile v1.

#### Scenario: Layer selector uses an explicit predicate field

- **WHEN** a layer selector declares `role`, optional literal `metadata`, and
  `when`
- **THEN** the product treats `when` as the only expression-bearing field on
  that selector node

#### Scenario: Literal strings remain literal

- **WHEN** a policy author writes `role: DomainLayer` or
  `namespace: MyApp.Domain`
- **THEN** those values are interpreted only as literal schema fields and are
  never parsed as CEL

#### Scenario: Non-boolean expression is rejected at policy load

- **WHEN** a `when` field compiles to `String`, `Int`, `Float`, `List`, `Map`,
  or `Object` rather than `Bool`
- **THEN** policy loading fails with a structured compilation diagnostic for an
  incompatible predicate result type

### Requirement: First-wave expression locations are closed and selector-scoped

The first-wave model SHALL allow `when` only at these YAML locations:

- `layers.<name>.selector.when`
- contextual dependency contract `source.when`
- contextual dependency contract `forbidden[*].when`
- contextual dependency contract `exclude[*].when`
- contextual allow-only contract `source.when`
- contextual allow-only contract `allowed[*].when`
- contextual allow-only contract `exclude[*].when`

Every other YAML location SHALL forbid expressions, including `imports`,
analysis settings, coverage contracts, baseline files, classification mapping
entries, literal selector fields, contract IDs/names/reasons, and ordinary
list/scalar members in non-contextual contract families.

#### Scenario: Expression refines a selector-backed layer

- **WHEN** `layers.sales.selector.when` is present
- **THEN** the selector remains valid only because `when` is attached to the
  selector node itself

#### Scenario: Contextual exclude has one unambiguous decision

- **WHEN** a contextual `exclude` selector declares `when`
- **THEN** the field is allowed and evaluated under the same typed target
  context as its surrounding contextual contract

#### Scenario: Expression in an unsupported location is rejected

- **WHEN** a policy author adds an expression-shaped value under
  `analysis.target_assemblies`, `classification.attributes[*].metadata`,
  `contracts.strict_coverage[*]`, or `imports[*]`
- **THEN** policy loading fails rather than silently accepting or ignoring that
  expression

### Requirement: Selector predicates compile against fixed typed contexts

Every expression location SHALL compile against a fixed public context schema
owned by `ArchLinterNet.Core` and built only from public `ArchLinterNet.CEL`
APIs. A selector-backed layer predicate SHALL expose one root variable named
`subject`. A contextual source predicate SHALL expose one root variable named
`source`. A contextual target or exclusion predicate SHALL expose `source`,
`target`, and `dependency`.

The shared subject shape SHALL include, at minimum:

- identity facts: `fullName`, `simpleName`, `namespace`, `assemblyName`,
  `projectName`
- classification facts: `role`, `metadataText`, `metadataBool`,
  `metadataNumber`
- type facts: `kind`, `isAbstract`, `isSealed`, `baseTypeNames`,
  `interfaceTypeNames`, `attributeTypeNames`
- path facts: `sourcePaths`

The `dependency` shape SHALL be limited to deterministic edge facts owned by
Core and SHALL NOT expose host services, reflection objects, processes,
environment variables, network access, or user-defined functions.

#### Scenario: Layer selector reads typed subject facts

- **WHEN** a selector predicate references `subject.role`,
  `subject.metadataText.containsKey("domain")`, or `"Sales" in subject.sourcePaths`
- **THEN** those members resolve through the fixed `subject` schema rather than
  through reflection or dynamic member access

#### Scenario: Contextual predicate compares source and target facts

- **WHEN** a contextual target predicate compares
  `target.metadataText["domain"]` with `source.metadataText["domain"]`
- **THEN** both operands resolve through their declared schemas and compile
  successfully only when the expression is well-typed under CEL profile v1

#### Scenario: Unknown member fails during policy load

- **WHEN** a predicate references `subject.metadata.domain`,
  `source.runtimeServices`, or another undeclared variable/member
- **THEN** policy loading fails with a structured binding or schema diagnostic

### Requirement: Predicate semantics are fail-closed and do not weaken policy

Predicate compilation SHALL happen during policy loading. Unknown variables,
members, functions, unsupported operators, type mismatches, and invalid result
types SHALL fail the policy load for both strict and audit contracts.

At validation time:

- a predicate result of `true` means the candidate matches;
- a predicate result of `false` means the candidate does not match;
- an evaluation failure means the policy is invalid for that run and SHALL be
  reported as a configuration/evaluation error rather than treated as a
  non-match or silently ignored.

Contract strictness SHALL affect only ordinary violation pass/fail behavior; it
SHALL NOT downgrade compilation or evaluation errors.

#### Scenario: Missing key during evaluation is not treated as false

- **WHEN** a predicate evaluates
  `subject.metadataText["domain"] == "Sales"` and the `domain` key is absent
- **THEN** validation reports an expression evaluation failure rather than
  treating the selector as unmatched

#### Scenario: Audit contract expression failure still blocks the run

- **WHEN** an audit contextual contract contains a predicate that evaluates with
  an error
- **THEN** the run fails as a policy/configuration error even though audit
  contract violations would otherwise be non-blocking

#### Scenario: Plain false remains an ordinary non-match

- **WHEN** a well-typed predicate evaluates to `false`
- **THEN** the candidate is simply excluded from that selector match set and no
  expression error is emitted

### Requirement: Reporting preserves provenance and expression explainability

Expression-bearing nodes SHALL preserve their composed YAML provenance exactly
like existing selector/contract nodes. Future explainable output for
implemented predicates SHALL expose the expression source text, the YAML
location, the selector or contract identity, the context kind, and whether the
predicate produced `true`, `false`, or an evaluation failure.

Compilation and evaluation errors SHALL remain outside baseline suppression.
Imported expression-bearing nodes SHALL keep fragment provenance after policy
composition. Explain, JSON, coverage, and future SARIF integrations SHALL treat
expressions as properties of the owning selector/contract node rather than as
free-floating source strings.

#### Scenario: Imported predicate retains fragment provenance

- **WHEN** an imported fragment contributes `layers.sales.selector.when`
- **THEN** future diagnostics and explain output identify the fragment path and
  YAML property path of that exact `when` field

#### Scenario: Baseline does not suppress expression infrastructure errors

- **WHEN** a policy baseline exists and a predicate fails to compile or evaluate
- **THEN** the error is still surfaced and the baseline does not hide it

#### Scenario: Explain output names the evaluated predicate

- **WHEN** a future explain command reports why a selector matched or failed
- **THEN** it includes the owning YAML location and the exact `when` source text
  for that selector

### Requirement: Compatibility is preserved and runtime rollout remains fail-closed

Existing literal-only policies SHALL remain valid and unchanged. The policy
schema version SHALL remain `1`; expression support is additive through
optional `when` fields rather than a new versioned root shape. Until issue #163
implements Core compilation and evaluation, live runtime/schema acceptance of
`when` SHALL remain fail-closed rather than partially enabled.

The documented model SHALL include worked examples for modular monolith and
Unity/client scenarios and SHALL include negative examples covering broad,
stale, invalid, and policy-weakening predicates.

#### Scenario: Existing monolithic policy remains unchanged

- **WHEN** a repository uses a current literal-only policy with no `when`
  fields
- **THEN** the policy continues to load and validate with no behavior change

#### Scenario: Pre-implementation runtime rejects `when`

- **WHEN** a user adds a documented `when` field before #163 lands
- **THEN** the live product rejects that policy rather than silently accepting
  or ignoring the field

#### Scenario: Design includes reviewed positive and negative examples

- **WHEN** the durable CEL policy model documentation is read
- **THEN** it contains modular-monolith and Unity/client examples and also
  negative examples for stale map access, unsupported regex/functions, broad
  `true` predicates, and policy-weakening exclusions
