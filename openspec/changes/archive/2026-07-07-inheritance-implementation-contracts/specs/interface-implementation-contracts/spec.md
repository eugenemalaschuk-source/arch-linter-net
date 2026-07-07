# interface-implementation-contracts Delta

## ADDED Requirements

### Requirement: Declare interface implementation contracts
The system SHALL allow `contracts.strict_interface_implementation` and `contracts.audit_interface_implementation` entries, each declaring at least one interface selector (`interfaces` and/or `interface_prefixes`, non-empty) and at least one location expectation (a non-empty `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies` allow-list, or a non-empty `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies` deny-list).

#### Scenario: Policy declares an interface implementation contract
- **WHEN** a policy declares `contracts.strict_interface_implementation` with `interfaces: [App.Ports.IPaymentGateway]` and `allowed_only_in_layers: [infrastructure]`
- **THEN** the policy loader SHALL expose a `strict_interface_implementation` contract restricting `IPaymentGateway` implementations to the `infrastructure` layer

#### Scenario: Contract with no interface selector is rejected
- **WHEN** an `interface_implementation` contract declares empty or missing `interfaces` and empty or missing `interface_prefixes`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no location expectation is rejected
- **WHEN** an `interface_implementation` contract declares an interface selector but no allow-list and no deny-list entries
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Match interface implementations including inherited and generic interfaces
The system SHALL treat every loaded non-interface type as an implementation of each distinct interface in its full interface set (including interfaces inherited through base types) whose fully-qualified name equals an entry in `interfaces` or starts with an entry in `interface_prefixes`. Constructed generic interfaces SHALL be matched by their generic type definition's fully-qualified name. Interface types themselves SHALL NOT be treated as implementations.

#### Scenario: Direct implementation is matched
- **WHEN** a class directly implements an interface listed in `interfaces`
- **THEN** the system SHALL treat that class as an implementation of that interface

#### Scenario: Implementation inherited through a base class is matched
- **WHEN** a class derives from a base class that implements an interface listed in `interfaces`
- **THEN** the system SHALL treat the derived class as an implementation of that interface

#### Scenario: Generic interface is matched by its generic type definition name
- **WHEN** a class implements a constructed generic interface whose generic type definition's fully-qualified name is listed in `interfaces`
- **THEN** the system SHALL treat that class as an implementation of that interface

#### Scenario: Interface prefix matching detects port namespaces
- **WHEN** a contract declares `interface_prefixes` and a class implements an interface whose fully-qualified name starts with one of those prefixes
- **THEN** the system SHALL treat that class as an implementation of that interface

#### Scenario: An interface extending a selected interface is not an implementation
- **WHEN** an interface type extends an interface listed in `interfaces`
- **THEN** the system SHALL NOT report a violation for the extending interface type

### Requirement: Detect implementations outside a declared allow-list
The system SHALL allow an `interface_implementation` contract to declare `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies`, and SHALL report a "misplaced" violation for any matched implementation whose type's namespace and assembly do not resolve to any declared allowed location.

#### Scenario: Implementation outside every allowed location is a violation
- **WHEN** a contract declares `allowed_only_in_layers: [infrastructure]` and a matched implementing type resides outside the `infrastructure` layer and outside every other declared allowed location
- **THEN** strict validation SHALL return an architecture violation identifying the implementing type, the matched interface, and that the violation kind is "misplaced"

#### Scenario: Implementation inside an allowed location passes
- **WHEN** a matched implementing type resides in a declared allowed location
- **THEN** strict validation SHALL NOT report a violation for that implementation

### Requirement: Detect implementations inside a declared deny-list
The system SHALL allow an `interface_implementation` contract to declare `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies`, and SHALL report a "forbidden" violation for any matched implementation whose type's namespace and assembly resolve to a declared forbidden location. A single matched implementation failing both the allow-list and deny-list checks SHALL yield exactly one violation with kind "forbidden".

#### Scenario: Implementation inside a forbidden layer is a violation
- **WHEN** a contract declares `forbidden_in_layers: [domain]` and a matched implementing type resides in the `domain` layer
- **THEN** strict validation SHALL return an architecture violation identifying the implementing type, the matched interface, and that the violation kind is "forbidden"

#### Scenario: Implementation outside every forbidden location passes the deny-list check
- **WHEN** a matched implementing type does not reside in any declared forbidden location
- **THEN** strict validation SHALL NOT report a deny-list violation for that implementation

### Requirement: Evaluate audit interface implementation contracts
The system SHALL allow `contracts.audit_interface_implementation` entries to report misplaced/forbidden implementations without affecting strict validation.

#### Scenario: Audit violation is reported in audit mode
- **WHEN** an audit interface implementation contract detects a misplaced or forbidden implementation
- **THEN** audit validation SHALL report an architecture violation for it

#### Scenario: Audit violation does not fail strict validation
- **WHEN** a policy contains only an `audit_interface_implementation` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations on interface implementation contracts
The system SHALL allow `ignored_violations` entries on an `interface_implementation` contract using the same shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching implementation violation
- **WHEN** an `interface_implementation` contract declares an `ignored_violations` entry whose `source_type` matches the implementing type and whose `forbidden_reference` matches the matched interface
- **THEN** strict validation SHALL NOT report a violation for that entry

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** an `interface_implementation` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic interface implementation diagnostics
The system SHALL emit, for each interface implementation violation, a diagnostic identifying the implementing type, the matched interface's fully-qualified name, whether the violation is "misplaced" or "forbidden", the actual location, and, for misplaced violations, the expected (allowed-only) location description; violations SHALL be ordered deterministically by implementing type and then matched interface (ordinal), with at most one violation per (type, matched interface) pair.

#### Scenario: Diagnostic identifies a misplaced implementation
- **WHEN** a matched implementation is outside every declared allowed location
- **THEN** the emitted diagnostic SHALL include the implementing type, the matched interface, the kind "misplaced", the actual location, and the expected allowed-only location description

#### Scenario: Diagnostic identifies a forbidden implementation
- **WHEN** a matched implementation is inside a declared forbidden location
- **THEN** the emitted diagnostic SHALL include the implementing type, the matched interface, the kind "forbidden", and the actual location
