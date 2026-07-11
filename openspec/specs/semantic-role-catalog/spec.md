# semantic-role-catalog Specification

## Purpose
TBD - created by archiving change define-semantic-role-catalog. Update Purpose after archive.
## Requirements
### Requirement: The catalog defines a bounded first-wave role vocabulary
The product documentation SHALL define roles covering layered/clean architecture, DDD, CQRS/Event Sourcing, web/API, MVVM/MVP/MVC and desktop/mobile UI, Unity/client, infrastructure/integration, and cross-cutting concerns. Each role SHALL include a short definition, intended static detection sources, typical metadata keys, example use cases, and a support tier.

#### Scenario: Reviewer can classify a role across the supported families
- **WHEN** a reviewer selects any role in the first-wave catalog
- **THEN** the catalog identifies its family, meaning, static evidence sources, metadata examples, use case, and support tier

#### Scenario: Ambiguous framework-specific roles are not canonical by accident
- **WHEN** a role depends on runtime framework behavior or has materially ambiguous semantics
- **THEN** the catalog marks it examples-only or deferred and documents why it is not a built-in default

### Requirement: Support tiers preserve YAML-first customization
The catalog SHALL classify roles as canonical vocabulary, optional annotation candidate, examples-only, custom-mapping expected, or deferred, and SHALL state that user-defined attributes and deterministic YAML conventions remain supported without an ArchLinterNet annotation binary.

#### Scenario: A custom attribute can represent a catalog role
- **WHEN** a project uses `MyCompany.Architecture.DomainLayerAttribute`
- **THEN** the documentation provides a full-type-name YAML mapping to `DomainLayer` without requiring a package reference to ArchLinterNet annotations

### Requirement: Metadata keys have explicit semantics and usage boundaries
The catalog SHALL define the meaning, value intent, and example usage for `domain`, `boundedContext`, `module`, `feature`, `layer`, `subsystem`, `platform`, `runtime`, `adapter`, `direction`, and `stability`, and SHALL identify whether each key is suitable for contextual policy contracts, documentation only, discouraged, or deferred. `owner` SHALL be included only with an explicit policy-use qualification.

#### Scenario: Context metadata is distinguishable from role identity
- **WHEN** a policy assigns `role: Entity` with `boundedContext: Sales` and `module: Orders`
- **THEN** the catalog explains that the role remains `Entity` while metadata supplies contextual selectors

### Requirement: Type-level and assembly-level annotation use cases are documented
The catalog SHALL document optional annotation naming conventions and assembly-level equivalents where they are statically meaningful, while preserving the same role and metadata semantics as YAML mappings.

#### Scenario: Assembly metadata supplies a shared context
- **WHEN** an assembly declares `[assembly: BoundedContext("Billing")]`
- **THEN** the catalog documents the corresponding metadata intent and an equivalent full-name YAML mapping approach

### Requirement: Worked examples remain static-analysis-only
The catalog SHALL include one modular-monolith example covering Sales, Inventory, and SharedKernel, one Unity/client example, one custom-attribute mapping, and one assembly-level metadata example. Examples SHALL use narrow selectors and SHALL state that classification and selector evaluation are future consumers of the documented model.

#### Scenario: AI policy guidance avoids broad unsafe selectors
- **WHEN** an author uses the catalog to write a policy
- **THEN** the guidance recommends explicit role/metadata criteria, narrow mappings, and reviewable exclusions, and does not encourage always-true selectors or broad exclusions

### Requirement: The catalog preserves the product boundary
The catalog SHALL state that ArchLinterNet remains YAML-first and static-analysis-only, does not prescribe one architecture style, does not require production projects to reference a binary annotation assembly, and does not imply runtime DI analysis, framework behavior validation, unrestricted plugin execution, or silent policy generation.

#### Scenario: Existing projects can adopt vocabulary incrementally
- **WHEN** a project has no ArchLinterNet annotations and uses custom YAML conventions
- **THEN** the catalog treats that adoption path as supported and does not require adding runtime dependencies

