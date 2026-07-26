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
The system SHALL enumerate, for each assembly in a `public_api_surface` contract's `assemblies` list, every exported type and member (types that are `public`; `protected`, `protected internal`, or `public` members declared directly on an exported type) and report any whose normalized signature is not present in `declared_api`.

#### Scenario: Accidental public type is a violation
- **WHEN** a target assembly contains a `public` type whose normalized signature is not present in `declared_api`
- **THEN** strict validation SHALL return an architecture violation identifying the assembly, the type, and its normalized signature

#### Scenario: Accidental public member is a violation
- **WHEN** an exported type in a target assembly declares a `public` method, property, field, or event whose normalized signature is not present in `declared_api`
- **THEN** strict validation SHALL return an architecture violation identifying the declaring type, the member, and its normalized signature

#### Scenario: Protected member is treated as exported
- **WHEN** an exported type declares a `protected` or `protected internal` member whose normalized signature is not present in `declared_api`
- **THEN** strict validation SHALL return an architecture violation for that member, identical in kind to an undeclared public member violation

#### Scenario: Declared exported member passes
- **WHEN** an exported type or member's normalized signature is present in `declared_api`
- **THEN** strict validation SHALL NOT report a violation for that type or member

#### Scenario: Nested type visibility follows the enclosing type chain
- **WHEN** a type is declared `public` but its enclosing type is `internal` (not itself exported)
- **THEN** the nested type SHALL NOT be treated as part of the exported surface, and its members SHALL NOT be reported even if undeclared

#### Scenario: Nested public type inside an exported type is in scope
- **WHEN** a `public` or `protected` nested type is declared inside an already-exported enclosing type
- **THEN** the nested type SHALL be treated as part of the exported surface and its undeclared signature SHALL be reported

#### Scenario: Member inherited from a base type is not re-reported on the derived type
- **WHEN** an exported type inherits a public member from a base type but does not redeclare it
- **THEN** the system SHALL NOT report a violation for that inherited member against the derived type

#### Scenario: Generic type surface is detected deterministically
- **WHEN** a target assembly declares a generic `public` type or a generic method on an exported type
- **THEN** the system SHALL normalize its signature using positional type-parameter naming and report it if not present in `declared_api`, consistently across runs

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

