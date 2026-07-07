# attribute-usage-contracts Specification

## Purpose
TBD - created by archiving change add-attribute-usage-contracts. Update Purpose after archive.
## Requirements
### Requirement: Declare attribute usage contracts
The system SHALL allow `contracts.strict_attribute_usage` and `contracts.audit_attribute_usage` entries, each declaring at least one attribute selector (`attributes` and/or `attribute_prefixes`, non-empty) and at least one location expectation (a non-empty `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies` allow-list, or a non-empty `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies` deny-list).

#### Scenario: Policy declares an attribute usage contract
- **WHEN** a policy declares `contracts.strict_attribute_usage` with `attributes: [Microsoft.AspNetCore.Mvc.RouteAttribute]` and `allowed_only_in_layers: [api]`
- **THEN** the policy loader SHALL expose a `strict_attribute_usage` contract restricting `RouteAttribute` usage to the `api` layer

#### Scenario: Contract with no attribute selector is rejected
- **WHEN** an `attribute_usage` contract declares an empty or missing `attributes` and an empty or missing `attribute_prefixes`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no location expectation is rejected
- **WHEN** an `attribute_usage` contract declares an attribute selector but none of `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies` and none of `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies`
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Match attribute usage across every declared member regardless of visibility
The system SHALL scan, for every loaded type, the type itself and every declared member (constructors, methods excluding property/event accessor methods, properties, fields, events) for attributes matching a contract's `attributes` (exact fully-qualified type name) or `attribute_prefixes` (prefix match against the attribute type's fully-qualified name), without filtering by member visibility.

#### Scenario: Public type-level attribute match is detected
- **WHEN** a type carries an attribute whose fully-qualified name is listed in a contract's `attributes`
- **THEN** the system SHALL treat the type as a match for that attribute

#### Scenario: Private field attribute match is detected
- **WHEN** a `private` field carries an attribute matching a contract's `attributes` or `attribute_prefixes`
- **THEN** the system SHALL treat the field as a match for that attribute, identical in kind to a public member match

#### Scenario: Method, property, and event attribute matches are detected
- **WHEN** a method, property, or event carries a matching attribute
- **THEN** the system SHALL treat that member as a match for that attribute

#### Scenario: Attribute prefix matching detects namespace-scoped markers
- **WHEN** a contract declares `attribute_prefixes` and a member carries an attribute whose fully-qualified name starts with one of those prefixes
- **THEN** the system SHALL treat that member as a match, even if the exact attribute type name is not listed in `attributes`

#### Scenario: A member with two matching attributes yields two matches
- **WHEN** a member carries two attributes that each independently match a contract's `attributes`/`attribute_prefixes`
- **THEN** the system SHALL treat the member as two separate matches, one per matched attribute

### Requirement: Detect attribute usage outside a declared allow-list
The system SHALL allow an `attribute_usage` contract to declare `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies`, and SHALL report a "misplaced" violation for any matched attribute usage whose enclosing type's namespace and assembly do not resolve to any declared allowed location.

#### Scenario: Attribute found outside every allowed location is a violation
- **WHEN** a contract declares `allowed_only_in_layers: [api]` and a matched attribute's enclosing type resides outside the `api` layer and outside every other declared allowed location
- **THEN** strict validation SHALL return an architecture violation identifying the source member, the matched attribute, and that the violation kind is "misplaced"

#### Scenario: Attribute found inside an allowed location passes
- **WHEN** a matched attribute's enclosing type resides in a declared `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies` location
- **THEN** strict validation SHALL NOT report a violation for that match

### Requirement: Detect attribute usage inside a declared deny-list
The system SHALL allow an `attribute_usage` contract to declare `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies`, and SHALL report a "forbidden" violation for any matched attribute usage whose enclosing type's namespace and assembly resolves to a declared forbidden location.

#### Scenario: Attribute found inside a forbidden layer is a violation
- **WHEN** a contract declares `forbidden_in_layers: [domain]` and a matched attribute's enclosing type resides in the `domain` layer
- **THEN** strict validation SHALL return an architecture violation identifying the source member, the matched attribute, and that the violation kind is "forbidden"

#### Scenario: Attribute found outside every forbidden location passes
- **WHEN** a matched attribute's enclosing type does not reside in any declared forbidden location
- **THEN** strict validation SHALL NOT report a violation for that match on the deny-list check

#### Scenario: A match failing both the allow-list and deny-list checks yields one violation
- **WHEN** a contract declares both an allow-list and a deny-list, and a single matched attribute usage fails both (outside every allowed location and inside a forbidden location)
- **THEN** the system SHALL report exactly one violation for that match, describing its kind as "forbidden"

### Requirement: Evaluate audit attribute usage contracts
The system SHALL allow `contracts.audit_attribute_usage` entries to report misplaced/forbidden attribute usage without affecting strict validation.

#### Scenario: Audit attribute usage violation is reported in audit mode
- **WHEN** an audit attribute usage contract detects a misplaced or forbidden attribute match
- **THEN** audit validation SHALL report an architecture violation for it

#### Scenario: Audit attribute usage violation does not fail strict validation
- **WHEN** a policy contains only an `audit_attribute_usage` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Support ignored violations
The system SHALL allow `ignored_violations` entries on an `attribute_usage` contract using the same shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching attribute usage violation
- **WHEN** an `attribute_usage` contract declares an `ignored_violations` entry matching a violating member and attribute
- **THEN** strict validation SHALL NOT report a violation for that entry

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** an `attribute_usage` contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

### Requirement: Emit deterministic diagnostics identifying the matched attribute and location mismatch
The system SHALL emit, for each attribute usage violation, a diagnostic identifying the source member, the matched attribute's fully-qualified name, whether the violation is "misplaced" or "forbidden", and the actual (and, for misplaced violations, expected) location.

#### Scenario: Diagnostic identifies a misplaced attribute
- **WHEN** a matched attribute usage is outside every declared allowed location
- **THEN** the emitted diagnostic SHALL include the source member, the matched attribute, `attribute_usage_kind: misplaced`, the actual location, and the expected (allowed-only) location description

#### Scenario: Diagnostic identifies a forbidden attribute
- **WHEN** a matched attribute usage is inside a declared forbidden location
- **THEN** the emitted diagnostic SHALL include the source member, the matched attribute, `attribute_usage_kind: forbidden`, and the actual location

### Requirement: Attribute usage validation is not a security or required-marker check
The system SHALL treat `attribute_usage` contracts strictly as static marker placement validation: it SHALL NOT evaluate attribute constructor arguments or named properties for authorization/security correctness, and it SHALL NOT report a violation for the *absence* of a required attribute on a type or member.

#### Scenario: Contract cannot express a required-marker rule
- **WHEN** a policy author wants to enforce "every controller action must carry `[Authorize]` or `[AllowAnonymous]`"
- **THEN** the `attribute_usage` contract family SHALL NOT provide a mechanism for this, and documentation SHALL identify required-marker enforcement as a deferred follow-up capability

