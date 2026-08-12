# public-api-surface-contracts Specification

## Purpose
TBD - created by archiving change add-public-api-surface-contracts. Update Purpose after archive.
## Requirements
### Requirement: Declare public API surface contracts
The system SHALL allow `contracts.strict_public_api_surface` and `contracts.audit_public_api_surface` entries, each declaring a non-empty `assemblies` list (target assembly names) and a `declared_api` list of normalized signature strings representing the intended exported API surface.

#### Scenario: Policy declares a public API surface contract
- **WHEN** a policy declares `contracts.strict_public_api_surface` with `assemblies: [MyApp.Core]` and a `declared_api` list of signature strings
- **THEN** the policy loader SHALL expose a `strict_public_api_surface` contract restricting `MyApp.Core`'s exported surface to the declared signatures

#### Scenario: Contract with no assemblies is rejected
- **WHEN** a policy declares a `public_api_surface` contract with an empty or missing `assemblies` list
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

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

### Requirement: Normalize exported signatures deterministically
The system SHALL normalize each exported type or member into a signature string of the form `<kind> <FullyQualifiedName>[(<parameter types>)][: <member type>]`, where `kind` identifies the declaration category (`class`, `interface`, `struct`, `enum`, `delegate`, `const`, `field`, `property`, `event`, `method`, or `ctor` — records reflect as an ordinary `class`/`struct`, since reflection has no reliable, non-heuristic way to distinguish a record from a hand-written type), such that identical declarations always normalize to the same string.

#### Scenario: Same declaration normalizes identically across runs
- **WHEN** the same exported type or member is scanned in two separate validation runs without any source change
- **THEN** the normalized signature string SHALL be identical both times

### Requirement: Forbid public constants unless explicitly declared or allowed
The system SHALL allow a `public_api_surface` contract to set `forbid_public_constants_unless_declared: true`, in which case an exported `const` field is a violation unless its fully-qualified member name is present in the contract's `allowed_public_constants` list, even when the constant's full signature is present in `declared_api`.

#### Scenario: Public constant is forbidden by default behavior
- **WHEN** a `public_api_surface` contract does not set `forbid_public_constants_unless_declared` (default `false`) and an exported `const` field's signature is not in `declared_api`
- **THEN** strict validation SHALL report an undeclared-member violation for that constant, the same as any other undeclared member

#### Scenario: Declared constant still forbidden when the flag is enabled and not explicitly allowed
- **WHEN** `forbid_public_constants_unless_declared` is `true`, an exported `const` field's signature is present in `declared_api`, but its fully-qualified member name is not present in `allowed_public_constants`
- **THEN** strict validation SHALL report a forbidden-public-constant violation for that constant

#### Scenario: Explicitly allowed constant passes when the flag is enabled
- **WHEN** `forbid_public_constants_unless_declared` is `true` and an exported `const` field's fully-qualified member name is present in `allowed_public_constants`
- **THEN** strict validation SHALL NOT report a forbidden-public-constant violation for that constant

### Requirement: Evaluate audit public API surface contracts
The system SHALL allow `contracts.audit_public_api_surface` entries to report undeclared exported surface and forbidden-constant violations without affecting strict validation.

#### Scenario: Audit public API surface violation is reported in audit mode
- **WHEN** an audit public API surface contract detects an undeclared exported type, member, or forbidden constant
- **THEN** audit validation SHALL report an architecture violation for it

#### Scenario: Audit public API surface violation does not fail strict validation
- **WHEN** a policy contains only an `audit_public_api_surface` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations
The system SHALL allow `ignored_violations` entries on a `public_api_surface` contract using the same shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching public API surface violation
- **WHEN** a `public_api_surface` contract declares an `ignored_violations` entry matching a violating type or member
- **THEN** strict validation SHALL NOT report a violation for that entry

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** a `public_api_surface` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic diagnostics identifying assembly, type, member, and declaration mismatch
The system SHALL emit, for each public API surface violation, a diagnostic identifying the assembly, the declaring type, the member signature, its visibility, and whether the violation is an undeclared-surface or forbidden-constant mismatch, in a stable, deterministic order.

#### Scenario: Diagnostic identifies undeclared exported surface
- **WHEN** an exported type or member is not present in `declared_api`
- **THEN** the emitted diagnostic SHALL include the assembly name, the declaring type's full name, the normalized member signature, and its visibility

#### Scenario: Diagnostic identifies a forbidden public constant
- **WHEN** an exported constant fails the `forbid_public_constants_unless_declared` check
- **THEN** the emitted diagnostic SHALL identify the constant's declaring type, its fully-qualified name, and that the violation reason is a forbidden public constant

### Requirement: Declare the exported surface from a reviewed snapshot file

The system SHALL allow a `public_api_surface` contract to declare `api_snapshot`, a repository-local path to a public API snapshot file. The snapshot SHALL be resolved and parsed at policy load time, and its entries SHALL be unioned with any inline `declared_api` entries to form the contract's declared surface.

#### Scenario: Snapshot entries satisfy the declaration
- **WHEN** a contract declares `api_snapshot` pointing at a snapshot containing an exported member's normalized signature
- **THEN** validation SHALL NOT report that member as undeclared, exactly as if it had been listed in `declared_api`

#### Scenario: Inline and snapshot declarations combine
- **WHEN** a contract declares both `declared_api` entries and an `api_snapshot`
- **THEN** the declared surface SHALL be the union of both, and neither source alone SHALL cause an otherwise declared member to be reported

#### Scenario: Missing snapshot is reported at validation, not at load
- **WHEN** a contract declares an `api_snapshot` path that does not exist
- **THEN** policy loading SHALL succeed and strict validation SHALL report a violation identifying the contract and the missing path, rather than treating the contract as declaring nothing

#### Scenario: Unparsable snapshot is reported at validation, not at load
- **WHEN** a contract's `api_snapshot` file cannot be parsed
- **THEN** policy loading SHALL succeed and strict validation SHALL report a violation carrying the parse error and the contract name

#### Scenario: An unusable snapshot does not report the whole surface as undeclared
- **WHEN** a contract's snapshot is missing, unparsable, or owned by another contract
- **THEN** validation SHALL report exactly one violation for the unusable snapshot, and SHALL NOT additionally report every exported member as undeclared

#### Scenario: Non-repository-local snapshot path fails policy loading
- **WHEN** a contract declares an `api_snapshot` path that is absolute or escapes the policy boundary
- **THEN** policy loading SHALL fail with an error identifying the contract and the rejected path

### Requirement: Exact public API comparison detects removals and changed signatures

The system SHALL allow a `public_api_surface` contract to set `api_comparison: exact` (default `additions_only`). In exact mode, validation SHALL report a violation for every declared signature absent from the actual exported surface, and for every declared member whose normalized signature changed, in addition to undeclared additions.

#### Scenario: Additions-only mode ignores removals
- **WHEN** a contract uses the default `api_comparison: additions_only` and a declared signature no longer exists in the assembly
- **THEN** validation SHALL NOT report a violation for the removal

#### Scenario: Exact mode reports a removal
- **WHEN** a contract sets `api_comparison: exact` and a declared signature is absent from the actual exported surface
- **THEN** validation SHALL report a violation whose delta kind identifies it as a removal

#### Scenario: Exact mode reports a changed signature
- **WHEN** a contract sets `api_comparison: exact` and a declared member's parameter or return type changed
- **THEN** validation SHALL report exactly one violation whose delta kind identifies it as a signature change and which carries both the previous and the current normalized signature

#### Scenario: Exact mode still reports additions
- **WHEN** a contract sets `api_comparison: exact` and an undeclared exported member exists
- **THEN** validation SHALL report a violation whose delta kind identifies it as an addition

#### Scenario: Exact mode with an in-sync surface passes
- **WHEN** a contract sets `api_comparison: exact` and the declared surface matches the actual exported surface exactly
- **THEN** validation SHALL report no public API surface violations

#### Scenario: Exact mode does not fabricate removals for an unresolved assembly
- **WHEN** a contract sets `api_comparison: exact` and at least one of its declared assemblies could not be resolved
- **THEN** validation SHALL NOT report removals or changed signatures for that contract, because every member of the unresolved assembly would otherwise appear to have been removed

#### Scenario: Invalid comparison mode fails policy loading
- **WHEN** a contract declares an `api_comparison` value other than `additions_only` or `exact`
- **THEN** policy loading SHALL fail with an error identifying the contract and the accepted values

### Requirement: Expose normalized API delta records in every output format

The system SHALL expose, for every public API surface violation that represents a surface delta, a normalized delta record identifying the delta kind (`added`, `removed`, or `changed`) and, where applicable, the previous normalized signature. Human-readable output, the JSON CI artifact, and SARIF SHALL expose the same delta records. A forbidden-public-constant violation is not a surface delta and carries no delta kind.

#### Scenario: Human output names the delta kind
- **WHEN** a public API surface violation is formatted for humans
- **THEN** the output SHALL include the delta kind alongside the assembly, visibility, and normalized signature

#### Scenario: JSON output carries the delta record
- **WHEN** a public API surface violation is serialized into the JSON CI artifact
- **THEN** the entry SHALL carry the delta kind and, for a changed signature, the previous normalized signature

#### Scenario: SARIF output carries the delta record
- **WHEN** a public API surface violation is serialized into SARIF
- **THEN** the result SHALL carry the same delta kind and previous normalized signature in its properties

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

#### Scenario: Unselected first-party dependency fails a capture/diff/update/migrate operation

- **WHEN** `public-api capture`, `public-api diff`, `public-api update`, or `public-api migrate` resolves a contract whose selected surface has a member depending on an unselected first-party type
- **THEN** the operation SHALL fail rather than producing a snapshot the same policy's strict validation could never pass against

