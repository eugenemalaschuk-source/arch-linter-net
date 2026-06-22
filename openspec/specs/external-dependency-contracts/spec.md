# External Dependency Contracts Specification

## Purpose
Lets policies declare named external dependency groups and evaluate contracts that forbid referencing them.

## Requirements

### Requirement: Declare external dependency groups
The system SHALL allow policies to declare named external dependency groups in a top-level `external_dependencies` section. Each group SHALL support `namespace_prefixes` and `type_prefixes` lists.

#### Scenario: Policy declares external groups
- **WHEN** a policy declares `external_dependencies.unity_runtime.namespace_prefixes` with `UnityEngine`
- **THEN** the policy loader SHALL expose an external dependency group named `unity_runtime` with `UnityEngine` as a namespace prefix

#### Scenario: Policy declares type prefixes
- **WHEN** a policy declares `external_dependencies.infrastructure_sdks.type_prefixes` with `Stripe.StripeClient`
- **THEN** the policy loader SHALL expose `Stripe.StripeClient` as a type prefix for the `infrastructure_sdks` group

### Requirement: Evaluate strict external dependency contracts
The system SHALL allow `contracts.strict_external` entries to forbid references from a source layer to one or more declared external dependency groups.

#### Scenario: Strict external violation found
- **WHEN** a source type in the contract source layer directly references a type that matches a forbidden external dependency group
- **THEN** strict validation SHALL return an architecture violation for that source type and forbidden external group

#### Scenario: Strict external contract passes
- **WHEN** source types in the contract source layer do not directly reference types matching any forbidden external dependency group
- **THEN** strict validation SHALL return no violations for that external contract

### Requirement: Evaluate audit external dependency contracts
The system SHALL allow `contracts.audit_external` entries to report forbidden external dependency references without affecting strict validation.

#### Scenario: Audit external violation is reported in audit mode
- **WHEN** an audit external contract source type directly references a type matching a forbidden external dependency group
- **THEN** audit validation SHALL report an architecture violation for that source type and forbidden external group

#### Scenario: Audit external violation does not fail strict validation
- **WHEN** a policy contains only an `audit_external` violation and no strict violations
- **THEN** strict validation SHALL pass

### Requirement: Match external references by namespace prefix
The system SHALL match external dependency references when a referenced type namespace equals a configured namespace prefix or is a child namespace of that prefix.

#### Scenario: Namespace exact match
- **WHEN** an external group has namespace prefix `UnityEngine` and a source type references `UnityEngine.Vector3`
- **THEN** the reference SHALL match that external group

#### Scenario: Namespace child match
- **WHEN** an external group has namespace prefix `UnityEngine` and a source type references `UnityEngine.Rendering.RenderPipeline`
- **THEN** the reference SHALL match that external group

#### Scenario: Namespace sibling does not match
- **WHEN** an external group has namespace prefix `UnityEngine` and a source type references `UnityEngineering.Tools.Widget`
- **THEN** the reference SHALL NOT match that external group

### Requirement: Match external references by type prefix
The system SHALL match external dependency references when a referenced type full name equals or starts with a configured type prefix.

#### Scenario: Type prefix exact match
- **WHEN** an external group has type prefix `Stripe.StripeClient` and a source type references `Stripe.StripeClient`
- **THEN** the reference SHALL match that external group

#### Scenario: Type prefix child match
- **WHEN** an external group has type prefix `Amazon.S3` and a source type references `Amazon.S3.AmazonS3Client`
- **THEN** the reference SHALL match that external group

### Requirement: Use referenced type metadata visible from project types
The system SHALL detect external dependency leakage through referenced type metadata available from project types through the current reference scanner path, including supported base types, interfaces, fields, properties, method parameters, return types, and generic arguments. The system SHALL ALSO detect external dependency references that appear exclusively inside method bodies by scanning IL bytecode for metadata token references and matching them against external dependency groups.

#### Scenario: Signature reference is detected
- **WHEN** a source type exposes a forbidden external type through a field, property, parameter, return type, base type, interface, or generic argument observed by the scanner
- **THEN** the external dependency contract SHALL evaluate that referenced type against forbidden external groups

#### Scenario: Method-body-only reference is detected
- **WHEN** a source type uses a forbidden external dependency only inside a method body (e.g., a method call, constructor call, or type reference in IL) and that usage is not visible through type-level metadata
- **THEN** the external dependency contract SHALL detect that reference and report an architecture violation

#### Scenario: Method-body reference includes member context
- **WHEN** a forbidden external dependency is found inside a method body
- **THEN** the violation SHALL identify the source type, the containing method or constructor name, the forbidden external group, and the referenced external member or type

#### Scenario: Method-body strict violation fails validation
- **WHEN** a strict external dependency contract contains a method-body-only violation
- **THEN** strict validation SHALL fail

#### Scenario: Method-body audit violation reports without failing strict
- **WHEN** an audit external dependency contract contains a method-body-only violation
- **THEN** audit validation SHALL report the violation and strict validation SHALL NOT fail

#### Scenario: Unresolved external metadata is not guaranteed
- **WHEN** an external assembly is unavailable or unresolved enough that referenced type metadata cannot be observed by the scanner
- **THEN** the external dependency contract SHALL NOT be required to report references from that unavailable metadata

### Requirement: Avoid third-party internal analysis
The system SHALL NOT statically analyze third-party package internals when evaluating external dependency contracts.

#### Scenario: External group references are checked from first-party types only
- **WHEN** evaluating an external dependency contract
- **THEN** the system SHALL inspect references exposed by first-party target assembly types and SHALL NOT traverse or lint the internal dependency graph of the external package itself

### Requirement: Report external dependency diagnostics
External dependency violations SHALL identify the contract name, optional contract ID, source type, forbidden external dependency group, and matched forbidden references.

#### Scenario: Diagnostic identifies source and group
- **WHEN** `MyApp.Core.PlayerModel` references `UnityEngine.Vector3` through a `core-no-unity` contract forbidding `unity_runtime`
- **THEN** the violation SHALL identify `MyApp.Core.PlayerModel` as the source type, `unity_runtime` as the forbidden external group, and `UnityEngine.Vector3` as a forbidden reference

### Requirement: Preserve external layer compatibility
The system SHALL preserve existing `external: true` layer behavior while documenting `external_dependencies` as the preferred model for new vendor/framework dependency controls.

#### Scenario: Existing external layer policy remains valid
- **WHEN** an existing policy uses a layer with `external: true`
- **THEN** that policy SHALL continue to load and evaluate according to existing external layer semantics
