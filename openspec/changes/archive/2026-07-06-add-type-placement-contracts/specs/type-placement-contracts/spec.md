## ADDED Requirements

### Requirement: Declare type placement contracts
The system SHALL allow `contracts.strict_type_placement` and `contracts.audit_type_placement` entries, each declaring a `types_matching` selector and at least one of a placement expectation (`must_reside_in_layers`, `must_reside_in_namespaces`, `must_reside_in_projects`, `must_reside_in_assemblies`) or a naming expectation (`required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, `forbidden_name_prefix`).

#### Scenario: Policy declares a type placement contract
- **WHEN** a policy declares `contracts.strict_type_placement` with `types_matching.name_suffix: Controller` and `must_reside_in_layers: [api]`
- **THEN** the policy loader SHALL expose a `strict_type_placement` contract restricting types whose name ends with `Controller` to the `api` layer

#### Scenario: Selector with no placement or naming expectation is rejected
- **WHEN** a policy declares a `type_placement` contract with a `types_matching` selector but no `must_reside_in_*` list and no naming field populated
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Match types by constrained selectors
The system SHALL select candidate types for a `type_placement` contract using only the declared `types_matching` fields (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`), combining all populated fields with AND semantics, using exact/prefix/suffix/full-name string comparisons only (no regex or expression language).

#### Scenario: Name suffix selector matches types by simple name
- **WHEN** a contract declares `types_matching.name_suffix: Controller`
- **THEN** the system SHALL select every type whose simple name ends with `Controller` and no others

#### Scenario: Namespace selector matches types by namespace prefix
- **WHEN** a contract declares `types_matching.namespace: MyApp.Domain`
- **THEN** the system SHALL select every type whose namespace is `MyApp.Domain` or a child namespace of it

#### Scenario: Base type selector matches types by inheritance
- **WHEN** a contract declares `types_matching.base_type: MyApp.Unity.MonoBehaviourBase`
- **THEN** the system SHALL select every type whose base type chain includes a type with that full name

#### Scenario: Interface selector matches types by implemented interface
- **WHEN** a contract declares `types_matching.implements_interface: MyApp.Application.ICommandHandler`
- **THEN** the system SHALL select every type that implements an interface with that full name

#### Scenario: Attribute selector matches types by applied attribute
- **WHEN** a contract declares `types_matching.has_attribute: MyApp.Infrastructure.GeneratedCodeMarkerAttribute`
- **THEN** the system SHALL select every type carrying a custom attribute with that full name

#### Scenario: Layer selector matches types already resolved to a declared layer
- **WHEN** a contract declares `types_matching.layer: application`
- **THEN** the system SHALL select every type whose namespace resolves to the `application` layer

#### Scenario: Multiple selector fields combine with AND
- **WHEN** a contract declares both `types_matching.name_suffix: Controller` and `types_matching.layer: api`
- **THEN** the system SHALL select only types that both end with `Controller` and resolve to the `api` layer

### Requirement: Evaluate strict placement expectations
The system SHALL allow `contracts.strict_type_placement` entries to require that every selected type's actual location (layer, namespace, project-resolved assembly, or assembly) matches at least one entry across the contract's combined `must_reside_in_layers`/`must_reside_in_namespaces`/`must_reside_in_projects`/`must_reside_in_assemblies` lists.

#### Scenario: Selected type outside every declared allowed location is a violation
- **WHEN** a selected type's namespace does not resolve to any layer in `must_reside_in_layers`, does not match any prefix in `must_reside_in_namespaces`, and its assembly name matches neither `must_reside_in_assemblies` nor any project-resolved assembly name in `must_reside_in_projects`
- **THEN** strict validation SHALL return an architecture violation identifying the type, its actual location, and the expected location

#### Scenario: Selected type inside a declared allowed location passes
- **WHEN** a selected type's namespace resolves to a layer present in `must_reside_in_layers`
- **THEN** strict validation SHALL NOT report a placement violation for that type

#### Scenario: Project residency resolves to assembly-name matching
- **WHEN** a contract declares `must_reside_in_projects: [MyApp.Api]` and project discovery resolves project `MyApp.Api` to assembly name `MyApp.Api`
- **THEN** a selected type whose declaring assembly name is `MyApp.Api` SHALL satisfy the placement expectation

### Requirement: Evaluate naming expectations
The system SHALL allow `type_placement` contracts to require or forbid a declared suffix/prefix on every selected type's simple name via `required_name_suffix`, `required_name_prefix`, `forbidden_name_suffix`, and `forbidden_name_prefix`.

#### Scenario: Selected type missing a required suffix is a violation
- **WHEN** a contract declares `required_name_suffix: Controller` and a selected type's simple name does not end with `Controller`
- **THEN** strict validation SHALL return an architecture violation identifying the type, its actual name, and the required suffix

#### Scenario: Selected type carrying a forbidden suffix is a violation
- **WHEN** a contract declares `forbidden_name_suffix: Impl` and a selected type's simple name ends with `Impl`
- **THEN** strict validation SHALL return an architecture violation identifying the type, its actual name, and the forbidden suffix

#### Scenario: Selected type satisfying naming expectations passes
- **WHEN** a contract declares `required_name_suffix: Controller` and a selected type's simple name ends with `Controller`
- **THEN** strict validation SHALL NOT report a naming violation for that type

### Requirement: Evaluate audit type placement contracts
The system SHALL allow `contracts.audit_type_placement` entries to report placement and naming violations without affecting strict validation.

#### Scenario: Audit type placement violation is reported in audit mode
- **WHEN** an audit type placement contract selects a type that fails its placement or naming expectation
- **THEN** audit validation SHALL report an architecture violation for that type

#### Scenario: Audit type placement violation does not fail strict validation
- **WHEN** a policy contains only an `audit_type_placement` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations
The system SHALL allow `ignored_violations` entries on a `type_placement` contract using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching type placement violation
- **WHEN** a `type_placement` contract declares an `ignored_violations` entry matching a violating type
- **THEN** strict validation SHALL NOT report a violation for that type

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** a `type_placement` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic diagnostics identifying rule, type, and location/name
The system SHALL emit, for each type placement or naming violation, a diagnostic identifying the matched type, the contract name/id, and whichever of expected/actual location or expected/actual name applied, in a stable, deterministic order.

#### Scenario: Diagnostic identifies expected and actual location for a placement violation
- **WHEN** a selected type fails a placement expectation
- **THEN** the emitted diagnostic SHALL include the type's full name, the expected location, and the type's actual location

#### Scenario: Diagnostic identifies expected and actual name for a naming violation
- **WHEN** a selected type fails a naming expectation
- **THEN** the emitted diagnostic SHALL include the type's full name, the expected naming pattern, and the type's actual simple name

#### Scenario: Type failing both placement and naming reports both in one diagnostic
- **WHEN** a selected type fails both its placement expectation and its naming expectation under the same contract
- **THEN** the system SHALL emit a single violation for that type carrying both the location and name expectation/actual details
