# contextual-allow-only-contracts Specification

## Purpose
Evaluates strict and audit contextual allow-only contracts that restrict a source type's dependencies to an explicit allow-list of semantic role/metadata selectors, without requiring either side to be declared as a `layers.<name>`.

## Requirements
### Requirement: Contextual allow-only contract family exists with strict and audit variants
The reviewed schema and runtime SHALL support `contracts.strict_context_allow_only` and `contracts.audit_context_allow_only` as a new contract family, registered identically to every other family in the contract-family registry. Each contract entry SHALL declare `name`, optional `id`, a `source` selector, an `allowed` list of selectors, an optional `exclude` list of selectors, optional `ignored_violations`, and `reason`.

#### Scenario: Strict contextual allow-only contract fails the build when a referenced target matches no allowed selector
- **WHEN** a `strict_context_allow_only` contract's `source` selector matches a type that references a type matching none of the `allowed` selectors and no `exclude` selector
- **THEN** the analysis reports a build-failing violation

#### Scenario: Audit contextual allow-only contract reports without failing the build
- **WHEN** the same violation condition is declared under `audit_context_allow_only` instead of `strict_context_allow_only`
- **THEN** the analysis reports the finding without failing the build, consistent with existing `audit_*` family semantics

#### Scenario: Reference to an allowed target produces no violation
- **WHEN** a `source`-matching type references a target type matching any `allowed` selector
- **THEN** the analysis reports no violation for that reference

### Requirement: Contextual allow-only selectors and operators are identical to the contextual dependency family
`source`, `allowed`, and `exclude` selectors SHALL use the same selector shape and the same four metadata operators (`exact`, `any`, `in`, `not-equal-to-source`) defined for `context_dependencies`, including `not-equal-to-source` resolving against the current source type's own resolved metadata.

#### Scenario: not-equal-to-source restricts allowed targets to the same context
- **WHEN** an `allowed` selector declares `domain: "{source.metadata.domain}"`-equivalent same-context matching is expressed by omitting a forbidding constraint and instead declaring `domain: "*"` scoped by a same-role allowed selector, or by an author-declared exact/`in` list matching the source's own known domains
- **THEN** the contract restricts allowed targets according to the declared operator, with no additional implicit same-context inference beyond what the four defined operators express

### Requirement: Exclude selectors suppress candidate targets before violation evaluation
An allow-only candidate target matching any selector in the contract's `exclude` list SHALL be removed from violation consideration entirely, before `allowed`-list evaluation and before `ignored_violations` post-match suppression.

#### Scenario: Excluded target produces no violation even when not allowed
- **WHEN** a candidate target type matches no `allowed` selector but matches an `exclude` selector
- **THEN** the analysis reports no violation for that source/target pair

### Requirement: Contextual allow-only diagnostics carry source/target role, metadata, and selector evidence
Every contextual allow-only violation SHALL produce a diagnostic distinguishable from existing namespace/layer allow-only diagnostics, including the source type's resolved role and relevant metadata, the target type's resolved role and relevant metadata, and that no `allowed` selector matched. This evidence SHALL be present in both JSON and human-readable output.

#### Scenario: JSON diagnostic includes role and metadata evidence
- **WHEN** a contextual allow-only violation is reported in JSON output
- **THEN** the diagnostic entry includes the source role, source metadata, target role, and target metadata

### Requirement: Ignored violations apply to contextual allow-only contracts identically to existing contracts
A `strict_context_allow_only`/`audit_context_allow_only` contract's `ignored_violations` list SHALL suppress matching violations using the same `SourceType`/`ForbiddenReference`/`reason` matching behavior as the existing `allow_only` family, including unmatched-ignore reporting.

#### Scenario: Ignored violation suppresses a matching contextual finding
- **WHEN** a contextual allow-only contract declares an `ignored_violations` entry matching a specific source/target type pair that would otherwise violate
- **THEN** that specific violation is suppressed and not reported

### Requirement: Contextual allow-only contracts are documented and schema-validated
The reviewed JSON schema SHALL validate `strict_context_allow_only`/`audit_context_allow_only` contract shapes, including selector `role`/`metadata` structure and the four metadata operator forms. Documentation and at least one example policy SHALL demonstrate the family.

#### Scenario: Schema rejects a contextual selector without a role
- **WHEN** a `context_allow_only` contract's `source`, `allowed`, or `exclude` entry omits `role`
- **THEN** schema validation rejects the document as invalid

