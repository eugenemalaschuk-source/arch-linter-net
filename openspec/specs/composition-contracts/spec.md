# composition-contracts Specification

## Purpose
TBD - created by archiving change composition-root-service-locator-contracts. Update Purpose after archive.
## Requirements
### Requirement: Declare composition contracts
The system SHALL allow `contracts.strict_composition` and `contracts.audit_composition` entries, each declaring at least one forbidden API selector (a non-empty `forbidden_apis`) and at least one allowed composition boundary selector (a non-empty `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, and/or `allowed_only_in_assemblies`).

#### Scenario: Policy declares a composition contract
- **WHEN** a policy declares `contracts.strict_composition` with `allowed_only_in_layers: [composition]` and `forbidden_apis: [System.IServiceProvider.GetService]`
- **THEN** the policy loader SHALL expose a `strict_composition` contract forbidding `System.IServiceProvider.GetService` calls outside the `composition` layer

#### Scenario: Contract with no forbidden API selector is rejected
- **WHEN** a `composition` contract declares empty or missing `forbidden_apis`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no allowed-boundary selector is rejected
- **WHEN** a `composition` contract declares `forbidden_apis` but empty or missing `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, and `allowed_only_in_assemblies`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Detect forbidden composition API usage outside the allowed boundary
The system SHALL, for every loaded type whose location (namespace and assembly) does not satisfy at least one entry across the contract's `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, and `allowed_only_in_assemblies`, scan that type's methods and constructors and report a violation when a call resolves to a member matching an entry in `forbidden_apis`. Forbidden API patterns SHALL be matched using the same call-pattern vocabulary as method-body contracts: exact member names, `Type.Member` names, fully qualified members, and namespace/type prefixes.

#### Scenario: Forbidden API call outside the composition boundary is a violation
- **WHEN** a type outside every declared allowed composition boundary calls a member matching a `forbidden_apis` entry
- **THEN** strict validation SHALL return an architecture violation identifying the type and the matched forbidden API

#### Scenario: Forbidden API call inside the composition boundary is allowed
- **WHEN** a type whose location satisfies an `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, or `allowed_only_in_assemblies` entry calls a member matching a `forbidden_apis` entry
- **THEN** the system SHALL NOT report a violation for that call

#### Scenario: Namespace prefix pattern matches forbidden API usage
- **WHEN** a contract declares a `forbidden_apis` entry ending in `.` (a namespace/type prefix) and a type outside the composition boundary calls a member whose fully qualified name starts with that prefix
- **THEN** strict validation SHALL return an architecture violation for that type

#### Scenario: Type with no forbidden API calls passes
- **WHEN** a type outside every declared allowed composition boundary calls no member matching any `forbidden_apis` entry
- **THEN** the system SHALL NOT report a violation for that type

### Requirement: Evaluate audit composition contracts
The system SHALL allow `contracts.audit_composition` entries to report forbidden composition API usage without affecting strict validation.

#### Scenario: Audit composition violation is reported in audit mode
- **WHEN** an audit composition contract detects a type outside the allowed boundary calling a forbidden API
- **THEN** audit validation SHALL report an architecture violation for it

#### Scenario: Audit composition violation does not fail strict validation
- **WHEN** a policy contains only an `audit_composition` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations on composition contracts
The system SHALL allow `ignored_violations` entries on a `composition` contract using the same shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching composition violation
- **WHEN** a `composition` contract declares an `ignored_violations` entry whose `source_type` matches the violating type and whose `forbidden_reference` matches the matched forbidden API
- **THEN** strict validation SHALL NOT report a violation for that entry

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** a `composition` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic composition diagnostics
The system SHALL emit, for each composition violation, a diagnostic identifying the violating type, the matched forbidden API's fully-qualified name, the contract, and the expected composition boundary, with violations ordered deterministically by the violating type's fully-qualified name and then by matched API (ordinal), and at most one violation per (type, matched API) pair.

#### Scenario: Diagnostic identifies the forbidden composition API usage
- **WHEN** a type outside the allowed composition boundary calls a forbidden API
- **THEN** the emitted diagnostic SHALL include the violating type's fully-qualified name, the matched forbidden API, and the expected composition boundary description

#### Scenario: Repeated runs produce identical ordering
- **WHEN** the same policy is validated twice against the same assemblies with multiple composition violations
- **THEN** the reported violations SHALL appear in the same order both times

### Requirement: Composition contracts do not validate runtime dependency injection
The system SHALL restrict composition contracts to static detection of forbidden API call sites. The system SHALL NOT attempt to resolve, simulate, or verify runtime service registration or resolution correctness.

#### Scenario: Documentation states the runtime limitation
- **WHEN** a user reads the composition contract documentation
- **THEN** it SHALL state that runtime DI resolution correctness is not validated by this family

