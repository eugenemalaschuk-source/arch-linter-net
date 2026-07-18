# architecture-coverage-model Specification

## Purpose
Define the architecture coverage vocabulary, strict/audit YAML contract shape, scope rules, exclusion/severity behavior, and diagnostic identity that future coverage contract families (#97-#103) implement against. This capability is design-only: no coverage engine, checker, or diagnostic kind is implemented by it.
## Requirements
### Requirement: Coverage vocabulary is defined
The architecture coverage model SHALL define exactly six classification terms for first-party units (namespace, project, assembly, or dependency edge): `covered`, `excluded`, `uncovered`, `unknown`, `stale`, and `empty-input`, each with a distinct meaning that does not overlap another term.

#### Scenario: Uncovered is distinguished from a forbidden-dependency violation
- **WHEN** a namespace matches no declared layer, glob layer, layer-template container, or explicit coverage exclusion
- **THEN** the model classifies it as `uncovered`, a status distinct from any `ArchitectureDiagnosticKind.Dependency` violation, which requires a matched layer to exist

#### Scenario: Being listed under a coverage contract's roots does not by itself make a unit covered
- **WHEN** a unit matches a coverage contract's `roots` (the scan boundary the contract classifies) but matches no declared layer and no explicit `exclude` entry
- **THEN** the model classifies it as `uncovered`, not `covered` — `roots` membership determines what is classified, not the classification result

#### Scenario: Unknown is distinguished from uncovered
- **WHEN** a project or assembly coverage unit cannot be classified because required discovery input is unavailable or ambiguous
- **THEN** the model classifies it as `unknown`, not `uncovered`

#### Scenario: Stale is distinguished from empty-input
- **WHEN** a coverage or rule-input contract's declared pattern matches zero current first-party units that previously existed or could exist
- **THEN** the model classifies the contract's input as `stale`
- **WHEN** a coverage contract's classification was given an empty unit set before classification ran
- **THEN** the model classifies that run as `empty-input`, a distinct status from `stale`

### Requirement: Coverage contracts are a separate strict/audit family
The reviewed schema SHALL define `strict_coverage` and `audit_coverage` contract lists, structurally parallel to the existing eleven strict/audit contract-family pairs in `ArchitectureContractGroups`, each entry sharing `name`, optional `id`, and `reason` fields with existing contract families.

#### Scenario: Coverage contract lists exist independently of other families
- **WHEN** a policy declares `contracts.strict_coverage` or `contracts.audit_coverage`
- **THEN** these entries are validated as a distinct family and do not alter the behavior of `strict`, `strict_layers`, `strict_independence`, `strict_protected`, or any other existing contract family

### Requirement: Coverage scope is declared via a discriminant field
Each coverage contract SHALL declare exactly one `scope` value among `namespace`, `project`, `assembly`, `dependency_edge`, or `rule_input`, with scope-specific fields (`roots` for namespace/project/assembly, `between` for dependency_edge, `contract_ids` for rule_input) populated only for the declared scope.

#### Scenario: Namespace-scope contract declares roots
- **WHEN** a coverage contract declares `scope: namespace`
- **THEN** it SHALL declare `roots` using the same `namespace`/`namespace_suffix` glob syntax already accepted by layer definitions

#### Scenario: Dependency-edge-scope contract declares layer pairs
- **WHEN** a coverage contract declares `scope: dependency_edge`
- **THEN** it SHALL declare `between` as a list of declared-layer-name pairs

### Requirement: Coverage matching reuses existing layer pattern matchers
Coverage scope resolution SHALL use the same `namespace` and `namespace_suffix` glob matching already validated for layer definitions and SHALL NOT introduce unrestricted regex-based matching.

#### Scenario: Coverage roots use layer glob syntax
- **WHEN** a namespace-scope coverage contract's `roots` or `exclude` entries are declared
- **THEN** they use the same glob syntax accepted by `layers.<name>.namespace` and `layers.<name>.namespace_suffix`

### Requirement: Coverage interacts with exhaustive layer templates without duplicate diagnostics
When a namespace-scope coverage contract's `roots` overlaps a layer template's `ContainerNamespace` declared with `exhaustive: true`, the coverage model SHALL treat the template's expanded layers as coverage for that subtree and SHALL NOT require a separate coverage exclusion for namespaces the template already classifies.

#### Scenario: Exhaustive template subtree counts as covered
- **WHEN** a namespace-scope coverage contract's root namespace contains an exhaustive layer template's container namespace
- **THEN** namespaces classified by the template's expansion are treated as `covered` by the coverage model without an additional explicit exclusion

### Requirement: Coverage exclusions require a reason
Every `exclude` entry under a coverage contract SHALL require a non-empty `reason` field.

#### Scenario: Exclusion without reason is rejected
- **WHEN** a coverage contract's `exclude` entry omits `reason`
- **THEN** the reviewed schema SHALL reject the document as invalid

### Requirement: Coverage severity is configurable and defaults to error
The reviewed schema SHALL define `analysis.coverage` with values `error`, `warn`, or `off`, defaulting to `error` when unset, following the same configuration pattern and default rationale as `analysis.policy_consistency`. The default is `error` (not `off`) so that a policy author who explicitly declares a `strict_coverage`/`audit_coverage` contract gets a failing/visible signal by default; backward compatibility for policies that declare no coverage contracts is guaranteed structurally (see "Existing policies remain unaffected" below), not by defaulting severity to `off`.

#### Scenario: Coverage is opt-in by the absence of coverage contracts, not by default severity
- **WHEN** a policy declares no `strict_coverage` or `audit_coverage` entries and does not set `analysis.coverage`
- **THEN** the policy's validation behavior is unaffected by the existence of the coverage model, because no coverage contract exists to produce a finding

#### Scenario: An explicitly declared strict coverage contract fails by default
- **WHEN** a policy declares a `strict_coverage` contract that finds an `uncovered` unit and does not set `analysis.coverage`
- **THEN** validation SHALL fail, exactly as an explicitly declared `strict` dependency contract would

#### Scenario: Coverage severity values mirror existing severity settings
- **WHEN** `analysis.coverage` is set to a value other than `error`, `warn`, or `off`
- **THEN** the reviewed design requires this to be rejected the same way invalid `analysis.policy_consistency` values are rejected

### Requirement: Coverage diagnostic identity is reviewed
The reviewed design SHALL define a `Coverage` diagnostic kind and a diagnostic shape carrying `Scope`, `Status` (one of `uncovered`, `stale`, `empty-input`, `unknown`), `RepresentativeUnit`, and an optional `Reason`, following the same one-kind/one-record pattern used for `PolicyConsistencyDiagnostic`.

#### Scenario: Uncovered diagnostics carry representative evidence
- **WHEN** a coverage finding has `Status == "uncovered"` or `Status == "stale"`
- **THEN** the reviewed diagnostic shape requires at least one concrete `RepresentativeUnit` value (a namespace, project path, assembly identity, or dependency-edge pair)

### Requirement: Existing policies remain unaffected
A policy with no `strict_coverage`/`audit_coverage` entries SHALL behave identically to its behavior before the coverage model existed, regardless of `analysis.coverage`.

#### Scenario: Policy without coverage contracts is unaffected
- **WHEN** a policy declares no coverage contracts
- **THEN** no coverage diagnostic of any status can be produced for that policy

### Requirement: A declared coverage contract cannot be silently ignored before the engine exists
Until the coverage engine (#97-#103) is implemented, the system SHALL reject — rather than silently accept and drop — any policy that declares a `strict_coverage` or `audit_coverage` contract, so a schema-valid policy can never diverge from what is actually enforced.

#### Scenario: A declared coverage contract fails validation with an actionable message
- **WHEN** a policy declares at least one `strict_coverage` or `audit_coverage` contract and the coverage engine has not yet been implemented
- **THEN** the system SHALL throw an error identifying that coverage contracts are reserved by the schema and not yet enforceable, rather than passing validation with the contract silently unenforced

### Requirement: Semantic role coverage is an opt-in coverage scope
The coverage contract family SHALL accept `scope: semantic_role` for strict and audit coverage contracts, without changing validation or execution of any existing coverage scope.

#### Scenario: Semantic coverage contract is accepted
- **WHEN** a policy declares a valid `strict_coverage` or `audit_coverage` contract with `scope: semantic_role`
- **THEN** the policy loads and the contract is evaluated by the coverage engine

#### Scenario: Existing coverage scopes remain unchanged
- **WHEN** a policy declares namespace, project, assembly, dependency-edge, or rule-input coverage
- **THEN** its validation and findings remain identical to the pre-semantic integration behavior

### Requirement: Semantic coverage classifies discovered facts and stale selectors

Semantic role coverage SHALL classify each in-scope resolved role/metadata fact as `covered`, `excluded`, or `uncovered`, SHALL classify a selector or contextual semantic reference with no current match as `stale` — accounting for the selector's `when` expression when one is declared, not literal `role`/`metadata` criteria alone — and SHALL classify unresolved role references or classification conflicts as `unknown` or `conflicting` evidence without treating them as dependency violations. An expression evaluation failure encountered while determining a selector's match set SHALL be reported through the expression-evaluation-error path defined by `semantic-classification-model`/`contextual-dependency-contracts`/`contextual-allow-only-contracts`, and SHALL NOT be silently classified as `stale` or `uncovered`.

#### Scenario: Unclassified first-party code is visible when semantic coverage is enabled
- **WHEN** semantic classification is enabled and a first-party type in the contract roots has no resolved role
- **THEN** the coverage result reports it as an uncovered semantic fact with representative type evidence

#### Scenario: Discovered role is governed by a selector
- **WHEN** a resolved role/metadata fact matches a selector-backed layer
- **THEN** the fact is classified as covered

#### Scenario: Discovered role is governed by a contextual contract
- **WHEN** a resolved role/metadata fact is referenced by a contextual contract selector
- **THEN** the fact is classified as covered even if no layer selector matches it

#### Scenario: Empty semantic selector is stale
- **WHEN** a semantic layer or contextual selector is valid but its combined literal-and-`when` match set contains no current classified fact
- **THEN** coverage reports a stale semantic selector with selector evidence, including the `when` expression source text when one is declared

#### Scenario: A broad `when` expression is visible as coverage evidence, not a silent pass
- **WHEN** a selector's `when` expression matches a broad set of classified facts (e.g. an expression that is trivially `true` for most candidates)
- **THEN** semantic coverage continues to classify matched facts as `covered` using the same evidence path as a literal match — broad matching remains visible through ordinary coverage/stale-selector reporting rather than being hidden by expression evaluation succeeding

#### Scenario: Expression evaluation failure is not misclassified as stale or uncovered
- **WHEN** determining a selector's match set requires evaluating a `when` expression that fails to evaluate for some candidate
- **THEN** the coverage engine does not classify the affected selector as `stale` or the affected fact as `uncovered`; the run instead fails with the reported expression evaluation error

### Requirement: Semantic exclusions are explicit and documented
Every semantic-role coverage exclusion SHALL include a non-empty reason and SHALL be applied using exact role and metadata matching; excluded facts SHALL be reported with the reason and SHALL not also appear as uncovered.

#### Scenario: Reasoned semantic exclusion suppresses a finding
- **WHEN** a classified fact matches a semantic exclusion with a non-empty reason
- **THEN** the fact appears in the excluded coverage bucket with that reason and no uncovered finding is emitted

#### Scenario: Missing semantic exclusion reason is rejected
- **WHEN** a semantic-role coverage exclusion omits or blanks its reason
- **THEN** policy validation rejects the contract with an actionable configuration diagnostic

### Requirement: Semantic coverage preserves complete layer constraints
Semantic-role coverage SHALL evaluate a selector-backed layer against the concrete type using the same namespace-and-selector matching semantics as ordinary layer resolution.

#### Scenario: Combined layer does not cover a different namespace
- **WHEN** a layer declares both a namespace and a selector and a type matches only the selector
- **THEN** semantic coverage SHALL not classify that type as governed by the layer

### Requirement: Semantic roots use namespace-root syntax
Semantic-role coverage roots, when declared, SHALL use only the existing namespace and optional namespace-suffix matcher shape.

#### Scenario: Invalid semantic root is rejected
- **WHEN** a semantic-role coverage root omits namespace or uses discovery include/exclude fields
- **THEN** policy validation SHALL reject the contract with an actionable error

