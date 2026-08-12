## ADDED Requirements

### Requirement: Restrict the governed surface with an optional intentional-surface selector

The system SHALL allow a `public_api_surface` contract to declare an optional `surface_selector`, restricting the contract's governed exported surface to types the selector matches. `surface_selector` SHALL support the same structural matcher fields as `type_placement.types_matching` (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`) plus a `role` field selecting by existing semantic-role facts, combining every populated field with AND semantics. When a contract declares no `surface_selector`, every exported type and member in its `assemblies` remains governed exactly as before this capability existed.

#### Scenario: Selector restricts the governed surface

- **WHEN** a `public_api_surface` contract declares `surface_selector.has_attribute: MyApp.Architecture.PublicApiContractAttribute` and only some exported types in the contract's assemblies carry that attribute
- **THEN** strict validation SHALL govern only the types carrying that attribute (and their members), and SHALL NOT report undeclared-surface violations for exported types that do not match the selector

#### Scenario: Selector matches by semantic role

- **WHEN** a `public_api_surface` contract declares `surface_selector.role: ApiContract` and a type's existing single winning semantic role is `ApiContract`
- **THEN** that type SHALL be part of the governed surface

#### Scenario: Multiple populated selector fields combine with AND

- **WHEN** a contract declares both `surface_selector.has_attribute: MyApp.Architecture.PublicApiContractAttribute` and `surface_selector.namespace: MyApp.Module.Public`
- **THEN** the system SHALL govern only types that both carry the attribute and reside in that namespace

#### Scenario: No selector preserves assembly-wide behavior

- **WHEN** a `public_api_surface` contract declares no `surface_selector`
- **THEN** every exported type and member in the contract's `assemblies` SHALL remain governed, identically to contracts authored before this capability existed

#### Scenario: Selecting a type does not change its semantic role

- **WHEN** a type is selected into the governed surface via `surface_selector` (through `has_attribute`, `implements_interface`, `base_type`, `namespace`, `layer`, or name matching)
- **THEN** the system SHALL NOT change, require changing, or otherwise write to that type's existing single winning semantic role, and existing semantic/contextual governance of that type SHALL remain equivalent

### Requirement: Empty selector is a policy-load configuration error

The system SHALL require a declared `surface_selector` to have at least one populated field. A `surface_selector` with no populated field SHALL fail policy loading with a configuration error identifying the contract.

#### Scenario: Selector with no populated fields is rejected

- **WHEN** a `public_api_surface` contract declares `surface_selector: {}` (or an equivalent selector with every field empty)
- **THEN** policy loading SHALL fail with an error identifying the contract and stating that at least one selector criterion is required

### Requirement: Zero-match selector fails closed

The system SHALL treat a configured `surface_selector` that matches zero exported types across the contract's resolved assemblies as a violation, reported at validation/capture time rather than at policy load, so a required selector can never silently produce a false-green strict/audit result or a silently near-empty capture.

#### Scenario: Selector matching nothing fails strict validation

- **WHEN** a `public_api_surface` contract's `surface_selector` matches zero exported types in the contract's resolved assemblies
- **THEN** strict validation SHALL report a violation identifying the contract and stating that the selector matched no governed types, rather than passing with an empty effective surface

#### Scenario: Selector matching nothing fails a capture/diff/update/migrate operation

- **WHEN** `public-api capture`, `public-api diff`, `public-api update`, or `public-api migrate` resolves a contract whose `surface_selector` matches zero exported types
- **THEN** the operation SHALL fail rather than silently producing an empty or near-empty snapshot

### Requirement: Selected surface fails closed on an unselected first-party dependency

The system SHALL fail closed when a selected member's normalized signature references an exported type declared in one of the contract's own `assemblies` (a first-party type) that is not itself part of the selected surface. The system SHALL NOT silently include the unselected first-party type to make the check pass, and SHALL NOT require local API-membership evidence for referenced types that are not declared in the contract's own `assemblies` (BCL/external types).

#### Scenario: Selected member referencing an unselected first-party type fails closed

- **WHEN** a selected type's exported member signature references another exported type declared in the same contract's `assemblies`, and that referenced type is not itself selected by `surface_selector`
- **THEN** strict validation SHALL report a violation identifying the selected member and the unselected first-party type it depends on

#### Scenario: BCL/external referenced types do not require selection

- **WHEN** a selected type's exported member signature references a type from the base class library or another assembly outside the contract's own `assemblies`
- **THEN** the system SHALL NOT report a first-party-dependency violation for that reference

## MODIFIED Requirements

### Requirement: Detect exported types and members not present in the declaration

The system SHALL enumerate, for each assembly in a `public_api_surface` contract's `assemblies` list, every exported type and member (types that are `public`; `protected`, `protected internal`, or `public` members declared directly on an exported type) that also matches the contract's `surface_selector` when one is declared, and report any whose normalized signature is not present in `declared_api`. When no `surface_selector` is declared, every exported type and member is in scope, unchanged from prior behavior.

#### Scenario: Accidental public type is a violation

- **WHEN** a target assembly contains a `public` type whose normalized signature is not present in `declared_api`, and the type is in scope (matches `surface_selector` when one is declared, or no selector is declared)
- **THEN** strict validation SHALL return an architecture violation identifying the assembly, the type, and its normalized signature

#### Scenario: Accidental public member is a violation

- **WHEN** an in-scope exported type in a target assembly declares a `public` method, property, field, or event whose normalized signature is not present in `declared_api`
- **THEN** strict validation SHALL return an architecture violation identifying the declaring type, the member, and its normalized signature

#### Scenario: Protected member is treated as exported

- **WHEN** an in-scope exported type declares a `protected` or `protected internal` member whose normalized signature is not present in `declared_api`
- **THEN** strict validation SHALL report a violation for that member, identical in kind to an undeclared public member violation

#### Scenario: Declared exported member passes

- **WHEN** an in-scope exported type or member's normalized signature is present in `declared_api`
- **THEN** strict validation SHALL NOT report a violation for that type or member

#### Scenario: Nested type visibility follows the enclosing type chain

- **WHEN** a type is declared `public` but its enclosing type is `internal` (not itself exported)
- **THEN** the nested type SHALL NOT be treated as part of the exported surface, and its members SHALL NOT be reported even if undeclared

#### Scenario: Nested public type inside an exported type is in scope

- **WHEN** a `public` or `protected` nested type is declared inside an already-exported enclosing type and matches `surface_selector` when one is declared
- **THEN** the nested type SHALL be treated as part of the exported surface and its undeclared signature SHALL be reported

#### Scenario: Member inherited from a base type is not re-reported on the derived type

- **WHEN** an exported type inherits a public member from a base type but does not redeclare it
- **THEN** the system SHALL NOT report a violation for that inherited member against the derived type

#### Scenario: Generic type surface is detected deterministically

- **WHEN** a target assembly declares a generic `public` type or a generic method on an in-scope exported type
- **THEN** the system SHALL normalize its signature using positional type-parameter naming and report it if not present in `declared_api`, consistently across runs

#### Scenario: A type outside the selected surface is excluded from enumeration entirely

- **WHEN** a `public_api_surface` contract declares a `surface_selector` and an exported type does not match it
- **THEN** the system SHALL NOT enumerate that type or its members for this contract, and SHALL NOT report undeclared-surface violations for them
