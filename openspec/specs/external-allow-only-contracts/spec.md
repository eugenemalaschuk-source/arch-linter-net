# external-allow-only-contracts Specification

## Purpose
TBD - created by archiving change add-external-allow-only-contracts. Update Purpose after archive.
## Requirements
### Requirement: Declare external allow-only contracts
The system SHALL allow `contracts.strict_external_allow_only` and `contracts.audit_external_allow_only` entries, each declaring a `source` layer and an `allowed` list of external dependency group names, restricting the source layer to referencing only the declared external dependency groups in `allowed`.

#### Scenario: Policy declares an external allow-only contract
- **WHEN** a policy declares `contracts.strict_external_allow_only` with `source: domain` and `allowed: [bcl_only]`
- **THEN** the policy loader SHALL expose a `strict_external_allow_only` contract restricting the `domain` layer to the `bcl_only` external dependency group

### Requirement: Evaluate strict external allow-only contracts
The system SHALL allow `contracts.strict_external_allow_only` entries to forbid references from a source layer to any declared external dependency group that is not present in `allowed`.

#### Scenario: Reference to a non-allowed declared group is a violation
- **WHEN** a source type in the contract source layer directly references a type that matches a declared external dependency group not present in `allowed`
- **THEN** strict validation SHALL return an architecture violation identifying the source type and the disallowed external dependency group

#### Scenario: Reference to an allowed declared group passes
- **WHEN** a source type in the contract source layer directly references a type that matches a declared external dependency group present in `allowed`
- **THEN** strict validation SHALL NOT return a violation for that reference

#### Scenario: No external references at all passes
- **WHEN** source types in the contract source layer reference no types matching any declared external dependency group
- **THEN** strict validation SHALL return no violations for that external allow-only contract

#### Scenario: Multiple non-allowed groups are each reported
- **WHEN** a source type references types matching two different declared external dependency groups, neither present in `allowed`
- **THEN** strict validation SHALL return violations identifying both disallowed external dependency groups

### Requirement: Evaluate audit external allow-only contracts
The system SHALL allow `contracts.audit_external_allow_only` entries to report disallowed external dependency references without affecting strict validation.

#### Scenario: Audit external allow-only violation is reported in audit mode
- **WHEN** an audit external allow-only contract source type directly references a type matching a declared external dependency group not present in `allowed`
- **THEN** audit validation SHALL report an architecture violation for that source type and disallowed external dependency group

#### Scenario: Audit external allow-only violation does not fail strict validation
- **WHEN** a policy contains only an `audit_external_allow_only` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Restrict allow-only violations to declared external dependency groups
The system SHALL only evaluate references against external dependency groups declared in the policy's top-level `external_dependencies` section. A reference that does not match any declared external dependency group SHALL NOT be reported as a violation, regardless of whether it is a BCL/system reference, a first-party reference, or any other undeclared reference.

#### Scenario: BCL reference is not flagged when no matching group is declared
- **WHEN** a source type in the contract source layer references a BCL/system type (e.g. `System.String`) and no declared external dependency group's namespace or type prefixes match that type
- **THEN** strict validation SHALL NOT report a violation for that reference

#### Scenario: BCL reference is flagged when explicitly captured by a declared, non-allowed group
- **WHEN** a policy declares an external dependency group whose namespace prefix matches a BCL namespace (e.g. `System.Net.Http`), that group is not present in a contract's `allowed` list, and a source type references a type under that namespace
- **THEN** strict validation SHALL report a violation for that reference identifying the declared group

#### Scenario: Undeclared or misspelled allowed group name has no relaxing effect
- **WHEN** a contract's `allowed` list contains a name that does not match any group declared in `external_dependencies`
- **THEN** that name SHALL have no effect on which declared groups are excluded from evaluation, and every actually-declared external dependency group not equal to that name SHALL still be evaluated as disallowed

### Requirement: Support allowed type exceptions
The system SHALL allow `allowed_types` on an external allow-only contract as a list of exact full type names that are exempt from producing a violation even when they match a disallowed external dependency group.

#### Scenario: Allowed type exception suppresses an otherwise-disallowed reference
- **WHEN** a source type references a type that matches a disallowed external dependency group, and that referenced type's full name is present in the contract's `allowed_types` list
- **THEN** strict validation SHALL NOT report a violation for that specific reference

#### Scenario: Allowed type exception does not suppress other references in the same disallowed group
- **WHEN** a source type references two distinct types matching the same disallowed external dependency group and only one of them is present in `allowed_types`
- **THEN** strict validation SHALL report a violation containing only the reference not present in `allowed_types`

### Requirement: Support ignored violations
The system SHALL allow `ignored_violations` entries on an external allow-only contract using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, suppressing matching violations and tracking unmatched ignore entries.

#### Scenario: Ignored violation suppresses a matching disallowed reference
- **WHEN** an external allow-only contract declares an `ignored_violations` entry matching a source type and disallowed external reference
- **THEN** strict validation SHALL NOT report a violation for that source type and reference pair

#### Scenario: Unmatched ignored violation is tracked
- **WHEN** an external allow-only contract declares an `ignored_violations` entry that does not match any actual violation
- **THEN** the system SHALL record that entry as an unmatched ignored violation

