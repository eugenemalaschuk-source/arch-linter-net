# inheritance-contracts Specification

## Purpose
TBD - created by archiving change inheritance-implementation-contracts. Update Purpose after archive.
## Requirements
### Requirement: Declare inheritance contracts
The system SHALL allow `contracts.strict_inheritance` and `contracts.audit_inheritance` entries, each declaring at least one source surface selector (a non-empty `source_layers` and/or `source_namespaces`) and at least one base type selector (a non-empty `forbidden_base_types` and/or `forbidden_base_type_prefixes`).

#### Scenario: Policy declares an inheritance contract
- **WHEN** a policy declares `contracts.strict_inheritance` with `source_layers: [domain]` and `forbidden_base_types: [UnityEngine.MonoBehaviour]`
- **THEN** the policy loader SHALL expose a `strict_inheritance` contract forbidding domain-layer types from inheriting `UnityEngine.MonoBehaviour`

#### Scenario: Contract with no source surface selector is rejected
- **WHEN** an `inheritance` contract declares empty or missing `source_layers` and empty or missing `source_namespaces`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no base type selector is rejected
- **WHEN** an `inheritance` contract declares a source surface but empty or missing `forbidden_base_types` and empty or missing `forbidden_base_type_prefixes`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Detect forbidden inheritance transitively across the base type chain
The system SHALL, for every loaded type whose namespace resolves into a declared source layer or matches a declared source namespace prefix, walk the type's full base type chain and report a violation when any base type's fully-qualified name equals an entry in `forbidden_base_types` or starts with an entry in `forbidden_base_type_prefixes`. Constructed generic base types SHALL be matched by their generic type definition's fully-qualified name. Interface implementations SHALL NOT be matched by this family.

#### Scenario: Direct inheritance from a forbidden base type is a violation
- **WHEN** a type in a declared source layer directly inherits a base type listed in `forbidden_base_types`
- **THEN** strict validation SHALL return an architecture violation identifying the type and the matched forbidden base type

#### Scenario: Transitive inheritance from a forbidden base type is a violation
- **WHEN** a type in a declared source layer inherits an intermediate class that itself derives from a base type listed in `forbidden_base_types`
- **THEN** strict validation SHALL return an architecture violation for that type identifying the matched forbidden base type

#### Scenario: Generic base type is matched by its generic type definition name
- **WHEN** a type in a declared source layer inherits a constructed generic base type whose generic type definition's fully-qualified name is listed in `forbidden_base_types`
- **THEN** strict validation SHALL return an architecture violation for that type

#### Scenario: Base type prefix matching detects framework namespaces
- **WHEN** a contract declares `forbidden_base_type_prefixes` and a source-surface type's base chain contains a type whose fully-qualified name starts with one of those prefixes
- **THEN** strict validation SHALL return an architecture violation for that type

#### Scenario: Type outside every source surface passes
- **WHEN** a type inherits a forbidden base type but resides outside every declared `source_layers` and `source_namespaces` surface
- **THEN** the system SHALL NOT report a violation for that type

#### Scenario: Nested type in a source surface is checked
- **WHEN** a nested type declared inside a source-surface type inherits a forbidden base type
- **THEN** strict validation SHALL return an architecture violation for the nested type

#### Scenario: Implementing a forbidden name as an interface is not an inheritance violation
- **WHEN** a source-surface type implements an interface whose name matches a `forbidden_base_types` entry but has no matching class in its base type chain
- **THEN** the inheritance contract SHALL NOT report a violation for that type

### Requirement: Evaluate audit inheritance contracts
The system SHALL allow `contracts.audit_inheritance` entries to report forbidden inheritance without affecting strict validation.

#### Scenario: Audit inheritance violation is reported in audit mode
- **WHEN** an audit inheritance contract detects a source-surface type inheriting a forbidden base type
- **THEN** audit validation SHALL report an architecture violation for it

#### Scenario: Audit inheritance violation does not fail strict validation
- **WHEN** a policy contains only an `audit_inheritance` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations on inheritance contracts
The system SHALL allow `ignored_violations` entries on an `inheritance` contract using the same shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching inheritance violation
- **WHEN** an `inheritance` contract declares an `ignored_violations` entry whose `source_type` matches the violating type and whose `forbidden_reference` matches the matched base type
- **THEN** strict validation SHALL NOT report a violation for that entry

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** an `inheritance` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic inheritance diagnostics
The system SHALL emit, for each inheritance violation, a diagnostic identifying the violating type, the matched forbidden base type's fully-qualified name, the contract, and the source surface expectation, with violations ordered deterministically by the violating type's fully-qualified name and then by matched base type (ordinal), and at most one violation per (type, matched base type) pair.

#### Scenario: Diagnostic identifies the forbidden inheritance relationship
- **WHEN** a source-surface type inherits a forbidden base type
- **THEN** the emitted diagnostic SHALL include the violating type's fully-qualified name and the matched forbidden base type's fully-qualified name

#### Scenario: Repeated runs produce identical ordering
- **WHEN** the same policy is validated twice against the same assemblies with multiple inheritance violations
- **THEN** the reported violations SHALL appear in the same order both times

