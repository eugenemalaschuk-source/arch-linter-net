# composition-contracts Specification

## Purpose
Define the composition contract family that confines dependency-injection registration, service-locator resolution, and container Resolve/Register API calls to explicitly declared composition boundaries using static IL call-site detection.
## Requirements
### Requirement: Declare composition contracts
The system SHALL allow `contracts.strict_composition` and `contracts.audit_composition` entries, each declaring at least one forbidden API selector (a non-empty `forbidden_apis`) and at least one allowed composition boundary selector (a non-empty `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, `allowed_only_in_assemblies`, and/or `allowed_only_in_types`).

#### Scenario: Policy declares a composition contract
- **WHEN** a policy declares `contracts.strict_composition` with `allowed_only_in_layers: [composition]` and `forbidden_apis: [System.IServiceProvider.GetService]`
- **THEN** the policy loader SHALL expose a `strict_composition` contract forbidding `System.IServiceProvider.GetService` calls outside the `composition` layer

#### Scenario: Contract with no forbidden API selector is rejected
- **WHEN** a `composition` contract declares empty or missing `forbidden_apis`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no allowed-boundary selector is rejected
- **WHEN** a `composition` contract declares `forbidden_apis` but empty or missing `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, `allowed_only_in_assemblies`, and `allowed_only_in_types`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: allowed_only_in_types entry missing assembly or type is rejected
- **WHEN** a `composition` contract declares an `allowed_only_in_types` entry with an empty or missing `assembly` or `type`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Detect forbidden composition API usage outside the allowed boundary
The system SHALL, for every loaded type whose location (namespace and assembly) does not satisfy at least one entry across the contract's `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, `allowed_only_in_assemblies`, and `allowed_only_in_types`, scan that type's methods and constructors and report a violation when a call resolves to a member matching an entry in `forbidden_apis`. Forbidden API patterns SHALL be matched using the same call-pattern vocabulary as method-body contracts: exact member names, `Type.Member` names, fully qualified members, and namespace/type prefixes.

#### Scenario: Forbidden API call outside the composition boundary is a violation
- **WHEN** a type outside every declared allowed composition boundary calls a member matching a `forbidden_apis` entry
- **THEN** strict validation SHALL return an architecture violation identifying the type and the matched forbidden API

#### Scenario: Forbidden API call inside the composition boundary is allowed
- **WHEN** a type whose location satisfies an `allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, or `allowed_only_in_assemblies` entry, or whose exact assembly+type pair matches an `allowed_only_in_types` entry, calls a member matching a `forbidden_apis` entry
- **THEN** the system SHALL NOT report a violation for that call

### Requirement: allowed_only_in_types selects a single type by direct assembly and type identity
The system SHALL allow `allowed_only_in_types` entries, each declaring an `assembly` and a `type` (the type's fully-qualified name), matched by exact string equality against a candidate type's declaring assembly name and fully-qualified name — with no globbing, no semantic-role classification, and no attribute-based matching. A type SHALL be considered inside the composition boundary if its exact assembly+type pair matches any `allowed_only_in_types` entry, independent of whether its assembly, namespace, or project would otherwise be inside or outside the boundary.

#### Scenario: A single named type is exempted without exempting its assembly or namespace
- **WHEN** a composition contract declares `allowed_only_in_types: [{assembly: Host.Api, type: Host.Api.Program}]` with no other boundary selector, and `Host.Api` contains both `Host.Api.Program` and a sibling type `Host.Api.OtherType` not named by any entry
- **THEN** `Host.Api.Program` calling a forbidden API SHALL NOT be reported as a violation, and `Host.Api.OtherType` calling the same forbidden API SHALL be reported as a violation

#### Scenario: A same-named type in a different assembly is not exempted
- **WHEN** a composition contract declares `allowed_only_in_types: [{assembly: Host.Api, type: Product.Program}]`, and a different assembly `Host.Admin` also contains a type with the same fully-qualified name `Product.Program`
- **THEN** `Product.Program` in `Host.Admin` calling a forbidden API SHALL still be reported as a violation, since the entry's assembly (`Host.Api`) does not match

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

The system SHALL emit, for each composition violation, a diagnostic identifying the violating type, the violating source member, the matched forbidden API's fully-qualified name, the contract, and the expected composition boundary, with violations ordered deterministically by the violating type's fully-qualified name, then by matched API (ordinal), then by source member (ordinal), and at most one violation per (type, source member, matched API) tuple.

Each composition violation's baseline/ignore identity (`ArchitectureViolationIdentity`) SHALL be qualified with the violating type's declaring assembly (`SourceAssembly`), the violating source member (`SourceMember`), and the matched forbidden API (`TargetMember`), and SHALL classify as `Kind: "call"`. Baselining or `ignored_violations` matching SHALL therefore distinguish two same-named types with the same simple name and namespace in different assemblies, and SHALL distinguish two distinct forbidden-call occurrences — whether from different source members of the same type or from two separate call sites to the same forbidden API within the same source member. Occurrence discrimination SHALL be evaluated against every raw call site independently (never pre-deduplicated before the ignore/occurrence check), so that baselining one occurrence cannot silently suppress a second, distinct call site that happens to share the same matched API and source member.

The violating type's declaring assembly SHALL also be included as `source_assembly` in human-readable, `--json`, `--explain`, and SARIF diagnostic output for composition violations, so that two same-named types in different assemblies are visibly distinguishable in every output format, not only distinguishable at baseline-matching time. SARIF SHALL expose `source_assembly`, `source_member`, `matched_forbidden_api`, and `expected_composition_boundary` as `properties` on the result entry.

#### Scenario: Diagnostic identifies the forbidden composition API usage
- **WHEN** a type outside the allowed composition boundary calls a forbidden API
- **THEN** the emitted diagnostic SHALL include the violating type's fully-qualified name, the violating type's declaring assembly, the violating source member, the matched forbidden API, and the expected composition boundary description

#### Scenario: SARIF output exposes the same structured evidence
- **WHEN** a composition violation is formatted as SARIF
- **THEN** the result's `properties` SHALL include `source_assembly`, `source_member`, `matched_forbidden_api`, and `expected_composition_boundary`

#### Scenario: Repeated runs produce identical ordering
- **WHEN** the same policy is validated twice against the same assemblies with multiple composition violations
- **THEN** the reported violations SHALL appear in the same order both times

#### Scenario: Same-named types in different assemblies do not collide
- **WHEN** two different assemblies each contain a type with the same simple name and namespace outside the composition boundary (e.g. two `Program` types), both calling the same forbidden API, and one occurrence is baselined
- **THEN** the baseline entry SHALL suppress only the violation from its own source assembly; the same-named violation in the other assembly SHALL still be reported as new debt by `validate --baseline`

#### Scenario: Distinct occurrences in one type each get a distinct identity
- **WHEN** a single type outside the composition boundary calls two distinct forbidden APIs from two different source members, and only one occurrence's baseline entry is applied
- **THEN** the other occurrence SHALL still be reported as a violation; baselining one SHALL NOT suppress the other

#### Scenario: Two calls to the same forbidden API within one source member remain distinct occurrences
- **WHEN** a single source member outside the composition boundary calls the same forbidden API from two distinct IL call sites, and only the first occurrence's baseline entry is applied
- **THEN** the second call site SHALL still be reported as a violation; baselining the first occurrence SHALL NOT suppress the second

### Requirement: Composition contracts do not validate runtime dependency injection
The system SHALL restrict composition contracts to static detection of forbidden API call sites. The system SHALL NOT attempt to resolve, simulate, or verify runtime service registration or resolution correctness.

#### Scenario: Documentation states the runtime limitation
- **WHEN** a user reads the composition contract documentation
- **THEN** it SHALL state that runtime DI resolution correctness is not validated by this family

