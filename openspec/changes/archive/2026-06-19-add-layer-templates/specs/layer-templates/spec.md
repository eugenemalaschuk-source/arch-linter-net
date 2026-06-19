## ADDED Requirements

### Requirement: Template contract YAML structure

The system SHALL support `strict_layer_templates` and `audit_layer_templates` arrays in the `contracts:` section of `dependencies.arch.yml`.

Each template entry SHALL contain:
- `name`: string identifier for the template
- `containers`: list of namespace prefix strings under which layers are resolved
- `layers`: list of layer entries, each with `name` (string) and optional `optional` (boolean, default false)
- `reason`: human-readable explanation

#### Scenario: Valid template with multiple containers

- **WHEN** a policy file defines `strict_layer_templates` with a template having 3 containers and 3 layers
- **THEN** the system parses it without error
- **THEN** each container produces an expanded layer contract

#### Scenario: Audit template variant

- **WHEN** a policy file defines `audit_layer_templates`
- **THEN** violations from expanded contracts are reported in audit mode (warnings, not errors)

#### Scenario: Empty template list

- **WHEN** `strict_layer_templates` is empty or absent
- **THEN** no expansion occurs and no behavior changes

### Requirement: Template expansion

The system SHALL expand each template into concrete `ArchitectureLayerContract` instances. For each container in a template, the expander SHALL produce one contract with fully-qualified namespace layers formed by prepending the container namespace to each layer name.

Each expanded contract SHALL receive:
- A deterministic ID: `{template-id-or-name}/{normalized-container}`
- A human-readable name: `{template-name} ({container-namespace})`
- Template and container metadata for diagnostics

#### Scenario: Template expansion produces correct contracts

- **WHEN** a template has containers `[FirstIce.Features.Fishing, FirstIce.Features.Inventory]` and layers `[Presentation, Application, Domain]`
- **THEN** two contracts are produced
- **THEN** the first contract has layers `[FirstIce.Features.Fishing.Presentation, FirstIce.Features.Fishing.Application, FirstIce.Features.Fishing.Domain]`
- **THEN** the second contract has layers `[FirstIce.Features.Inventory.Presentation, FirstIce.Features.Inventory.Application, FirstIce.Features.Inventory.Domain]`

#### Scenario: Expanded contract identity

- **WHEN** a template has `name: "feature-clean-architecture"` and `id: "fca"` with container `FirstIce.Features.Fishing`
- **THEN** the expanded contract's `Id` SHALL be `fca/firstice-features-fishing`
- **THEN** the expanded contract's `Name` SHALL be `feature-clean-architecture (FirstIce.Features.Fishing)`

### Requirement: Optional layer behavior

Optional layers SHALL NOT fail validation when absent (no types found in the resolved namespace). Required layers SHALL produce a violation when absent.

An optional layer that IS present SHALL still be validated for dependency direction — it must not reference layers at lower indices.

#### Scenario: Missing required layer produces violation

- **WHEN** a template has a required layer `Domain` and no types exist in any container's `Domain` sub-namespace
- **THEN** a violation is reported for each container indicating the required layer is empty

#### Scenario: Missing optional layer is silent

- **WHEN** a template has an optional layer `Application?` (explicit form: `optional: true`) and no types exist in the `Application` sub-namespace
- **THEN** no violation is reported for the missing layer
- **THEN** the directional check runs on remaining (present) layers only

#### Scenario: Optional layer present still enforces direction

- **WHEN** an optional layer `Application` IS present and references types in `Presentation` (lower index)
- **THEN** a violation is reported

### Requirement: Directional dependency enforcement

Expanded template contracts SHALL enforce the same directional layering as `strict_layers`: each layer at index `i` MUST NOT reference types in any layer at index `< i`.

#### Scenario: Violating dependency direction within a container

- **WHEN** a container has layers `[Presentation, Application, Domain]`
- **WHEN** a type in `Domain` references a type in `Presentation`
- **THEN** a violation is reported
- **THEN** the violation identifies the concrete container namespace

#### Scenario: No cross-contamination between containers

- **WHEN** two containers each have identical layer templates
- **THEN** a violation in one container does not affect the other
- **THEN** each violation is attributed to the correct container

### Requirement: Diagnostic metadata

Each violation from an expanded template contract SHALL carry the template name and container namespace as diagnostic metadata.

#### Scenario: Violation includes template and container

- **WHEN** a violation occurs in container `FirstIce.Features.Fishing` of template `feature-clean-architecture`
- **THEN** the JSON output includes `"template_name": "feature-clean-architecture"` and `"container_namespace": "FirstIce.Features.Fishing"`
- **THEN** the human-readable output includes the template/container context

### Requirement: Backward compatibility

Existing `strict_layers` and `audit_layers` contracts SHALL continue to work unchanged when template contracts are also present. Template contracts are additive and do not modify the semantics of direct layer contracts.

#### Scenario: Direct and template contracts coexist

- **WHEN** a policy file has both `strict_layers` and `strict_layer_templates`
- **THEN** all direct contracts are validated as before
- **THEN** all templates are expanded and validated
- **THEN** violations from both groups are reported together
