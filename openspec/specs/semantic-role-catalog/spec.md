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
The catalog SHALL document optional type-level annotations and role-bearing assembly-level equivalents where they are statically meaningful, while preserving the same single-role and winning-source metadata semantics as YAML mappings. Every `classification.assembly_attributes` mapping example SHALL include an `attribute` and a `role`. The catalog SHALL explicitly defer metadata-only assembly context such as `[assembly: BoundedContext("Billing")]` until a separate semantic-classification-model change defines its schema and cross-source metadata merge behavior.

#### Scenario: Role-bearing assembly classification is mapped
- **WHEN** an assembly declares `[assembly: SharedKernel("Billing")]`
- **THEN** the catalog documents an equivalent full-name YAML mapping with `role: SharedKernel` and `boundedContext` metadata that applies only when the assembly-attribute source wins for a type

#### Scenario: Metadata-only assembly context is deferred
- **WHEN** an author wants `[assembly: BoundedContext("Billing")]` to add context to types with higher-precedence type roles
- **THEN** the catalog states that the current model cannot merge that metadata and directs the author to a future model/schema change

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

### Requirement: The catalog explains single-role classification semantics
The catalog SHALL state that a classified type has exactly one winning role and the metadata produced by that winning source. It SHALL describe related roles as alternative classifications rather than accumulated tags, and SHALL direct authors to metadata or existing namespace layers when they need contextual or layering distinctions.

#### Scenario: Related roles do not accumulate
- **WHEN** an author considers classifying one type as both `DomainLayer` and `AggregateRoot`
- **THEN** the catalog explains that the model retains one role and recommends preserving the other distinction through metadata, namespace layers, or a separately reviewed model extension

### Requirement: Current classification sources are distinguished from future evidence
The catalog SHALL identify `asmdef` and package-reference facts as future discovery guidance, not current classification sources, unless a separate semantic-classification-model change adopts them.

#### Scenario: Unity assembly evidence is not implied to be active
- **WHEN** the catalog references an asmdef while discussing Unity/client roles
- **THEN** it states that the current six-source classification model does not consume asmdef facts automatically

### Requirement: The first wave approves no built-in annotation types
The semantic role catalog's first wave SHALL approve no ArchLinterNet-provided annotation types or annotation package. Annotation names in the catalog SHALL be candidates/examples only, and user-defined attributes mapped by full type name in YAML SHALL remain the supported adoption path. Issue #108 resolves the packaging decision: ArchLinterNet SHALL ship no binary annotation package and no source-only annotation package in this wave; user-defined attributes mapped by full type name in YAML remain the sole supported adoption path until a future, separately-decided change introduces an optional package.

#### Scenario: A reader evaluates an annotation example
- **WHEN** the catalog shows an annotation such as `[DomainLayer("Sales")]`
- **THEN** it identifies the annotation as a candidate/example rather than a shipped ArchLinterNet type and points to custom YAML mapping as the current supported path

#### Scenario: A reader looks for an annotation package
- **WHEN** a reader searches the catalog or policy-format documentation for an installable ArchLinterNet annotation package
- **THEN** the documentation states that no binary or source-only package exists in this wave, explains the trade-offs of user-owned attributes versus a future package, and confirms the reviewed YAML mapping shape supports user-defined attributes mapped by full type name today — while noting that runtime extraction against scanned code is not yet implemented (tracked by #109)

### Requirement: The catalog defines port and anti-corruption vocabulary
The semantic role catalog SHALL define `Port`, `Adapter`, `PrimaryPort`,
`SecondaryPort`, and `AntiCorruptionLayer`, including their support tiers,
static evidence guidance, and reviewed metadata keys. `ExternalSystem`,
`IntegrationAdapter`, `PersistenceAdapter`, and direct-database examples SHALL
be marked examples-only or custom-mapping expected unless independently
promoted by the catalog.

#### Scenario: Policy author maps a project-owned port attribute
- **WHEN** a project uses a user-owned attribute for a named secondary port
- **THEN** the catalog SHALL show a YAML mapping and selector metadata without
  implying that ArchLinterNet supplies that attribute

