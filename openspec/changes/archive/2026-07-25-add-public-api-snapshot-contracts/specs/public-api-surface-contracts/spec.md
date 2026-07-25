## ADDED Requirements

### Requirement: Declare the exported surface from a reviewed snapshot file

The system SHALL allow a `public_api_surface` contract to declare `api_snapshot`, a repository-local path to a public API snapshot file. The snapshot SHALL be resolved and parsed at policy load time, and its entries SHALL be unioned with any inline `declared_api` entries to form the contract's declared surface.

#### Scenario: Snapshot entries satisfy the declaration
- **WHEN** a contract declares `api_snapshot` pointing at a snapshot containing an exported member's normalized signature
- **THEN** validation SHALL NOT report that member as undeclared, exactly as if it had been listed in `declared_api`

#### Scenario: Inline and snapshot declarations combine
- **WHEN** a contract declares both `declared_api` entries and an `api_snapshot`
- **THEN** the declared surface SHALL be the union of both, and neither source alone SHALL cause an otherwise declared member to be reported

#### Scenario: Missing snapshot file fails policy loading
- **WHEN** a contract declares an `api_snapshot` path that does not exist
- **THEN** policy loading SHALL fail with an error identifying the contract and the missing path, rather than treating the contract as declaring nothing

#### Scenario: Unparsable snapshot fails policy loading
- **WHEN** a contract's `api_snapshot` file cannot be parsed
- **THEN** policy loading SHALL fail with the parse error and the contract name

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
