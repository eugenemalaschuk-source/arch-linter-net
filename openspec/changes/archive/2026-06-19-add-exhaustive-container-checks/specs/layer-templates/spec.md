## ADDED Requirements

### Requirement: Exhaustive container coverage option

Layer template contracts SHALL accept an optional `exhaustive` boolean field (default `false`). When `exhaustive: true`, the system SHALL detect sibling namespaces under each container that are not mapped into any declared layer.

#### Scenario: Exhaustive template with all children mapped

- **WHEN** a template has `exhaustive: true` with container `MyApp.Features.Fishing` and layers `[Presentation, Application, Domain]`
- **WHEN** all types under `MyApp.Features.Fishing` reside in namespaces matching one of the declared layers
- **THEN** no exhaustive violation is reported

#### Scenario: Exhaustive template with unmapped sibling containing types

- **WHEN** a template has `exhaustive: true` with container `MyApp.Features.Fishing` and layers `[Presentation, Application, Domain]`
- **WHEN** types exist under `MyApp.Features.Fishing.Payments` which does not match any declared layer
- **THEN** a violation is reported for the unmapped sibling namespace `MyApp.Features.Fishing.Payments`
- **THEN** the violation identifies the container namespace and the unmapped namespace

#### Scenario: Exhaustive template with unmapped sibling without types

- **WHEN** a template has `exhaustive: true` with container `MyApp.Features.Fishing` and layers `[Presentation, Application, Domain]`
- **WHEN** no types exist under `MyApp.Features.Fishing.Shared` (empty namespace)
- **THEN** no exhaustive violation is reported for the empty namespace

#### Scenario: Non-exhaustive template (default) does not check coverage

- **WHEN** a template has `exhaustive: false` (or omits the field) with container `MyApp.Features.Fishing`
- **WHEN** types exist under a namespace not matching any declared layer
- **THEN** no exhaustive violation is reported

### Requirement: Exhaustive violations in strict and audit modes

Exhaustive violations SHALL respect the contract mode. Strict exhaustive violations SHALL fail validation. Audit exhaustive violations SHALL be reported without failing strict validation.

#### Scenario: Strict exhaustive violation fails validation

- **WHEN** a `strict_layer_templates` contract has `exhaustive: true`
- **WHEN** an unmapped sibling namespace with types is detected
- **THEN** the validation result is `false` (fail)

#### Scenario: Audit exhaustive violation does not fail strict validation

- **WHEN** an `audit_layer_templates` contract has `exhaustive: true`
- **WHEN** an unmapped sibling namespace with types is detected
- **THEN** the violation is reported in the output
- **THEN** the violation does not cause strict mode to fail

### Requirement: Exhaustive violation diagnostics

Exhaustive violations SHALL include the template name and container namespace as diagnostic metadata, consistent with other template violations.

#### Scenario: Exhaustive violation includes metadata

- **WHEN** a violation occurs for unmapped namespace `MyApp.Features.Fishing.Payments` in template `feature-clean-architecture`
- **THEN** the JSON output includes `"template_name": "feature-clean-architecture"` and `"container_namespace": "MyApp.Features.Fishing"`
- **THEN** the human-readable output includes the template and container context
