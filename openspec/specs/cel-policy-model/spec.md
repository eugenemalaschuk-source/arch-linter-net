# cel-policy-model Specification

## Purpose
Defines the explicit CEL-compatible policy expression model for ArchLinterNet:
the allowed `when` locations, fixed typed Core fact contexts, fail-closed
compilation and evaluation behavior, reporting expectations, and
backward-compatible rollout boundary before runtime implementation lands.
## Requirements
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

The shared subject shape SHALL consist of exactly the following members and
types:

- identity facts: `fullName: String`, `simpleName: String`,
  `namespace: String`, `assemblyName: String`, `projectName: String`
- classification facts: `role: String`, `metadataText: Map[String]`,
  `metadataBool: Map[Bool]`
- type facts: `kind: String`, `isAbstract: Bool`, `isSealed: Bool`,
  `baseTypeNames: List[String]`, `interfaceTypeNames: List[String]`,
  `attributeTypeNames: List[String]`
- path facts: `sourcePaths: List[String]`,
  `sourceDirectoryPrefixes: List[String]` (every repository-relative ancestor
  directory of every source path, `/`-separated, without a trailing slash)

Numeric metadata SHALL NOT be exposed in the first-wave subject shape: the
canonical `decimal` metadata domain has no lossless mapping onto CEL profile v1
`Int`/`Float`, and numeric metadata remains matchable only through literal
`metadata` selectors.

The `dependency` shape SHALL consist of exactly the following deterministic
edge facts owned by Core: `kind: String`, `viaMethodBody: Bool`,
`sourceMemberName: String`, `targetMemberName: String`. It SHALL NOT expose
host services, reflection objects, processes, environment variables, network
access, or user-defined functions.

Both context schemas are closed catalogs: adding, removing, or retyping any
member SHALL require a reviewed change to this specification.

**`dependency` is schema-declared but reserved (rejected at policy load) in
this wave.** The scanning path the runtime uses for contextual dependency/
allow-only matching reports type-level reference existence only; it does not
yet track which member produced an edge or whether it is reachable only via
a method body. Populating `dependency` with the same fixed values for every
candidate — rather than real per-edge facts — would let a `when` referencing
it compile successfully and then silently never behave as the predicate
implies, regardless of the actual edge. Until either real per-edge facts are
implemented or `ArchLinterNet.CEL` exposes a public API letting Core
determine which identifiers a compiled predicate references (neither of
which this wave provides), policy loading SHALL reject any `when` at a
`forbidden[*]`/`allowed[*]`/`exclude[*]` location that references the word
`dependency` anywhere in its source text, including inside a string literal
or comment. This rejection is deliberately unconditional (a whole-string
match, not a syntax-aware one) because ArchLinterNet.CEL's lexical grammar
(raw-string escaping, comments, quoting) is not something Core can safely
reimplement piecemeal without a real bypass risk — a syntax-aware attempt at
this exact check found two before this unconditional form was adopted.

#### Scenario: A `when` referencing `dependency` is rejected at policy load

- **WHEN** a `forbidden[*]`/`allowed[*]`/`exclude[*]` selector declares
  `when: dependency.viaMethodBody == false` or any other expression
  containing the word `dependency`
- **THEN** policy loading fails with an actionable error explaining that
  `dependency` facts are not populated with real per-edge data in this
  release, rather than compiling successfully into a predicate that would
  silently never behave as written

#### Scenario: A `when` mentioning "dependency" only in an unrelated string is still rejected

- **WHEN** a `forbidden[*]`/`allowed[*]`/`exclude[*]` selector's `when`
  contains the word `dependency` only inside a string literal or comment,
  unrelated to the `dependency` root variable
- **THEN** policy loading still rejects the expression, since precisely
  distinguishing this case from a real reference requires replicating CEL's
  lexical grammar, which this rejection deliberately does not attempt

#### Scenario: Layer selector reads typed subject facts

- **WHEN** a selector predicate references `subject.role`,
  `subject.metadataText.containsKey("domain")`, or
  `"Assets/Game/Client" in subject.sourceDirectoryPrefixes`
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

