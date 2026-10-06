## MODIFIED Requirements

### Requirement: The catalog defines a bounded first-wave role vocabulary

The product documentation SHALL define roles covering layered/clean architecture, DDD, CQRS/Event Sourcing, web/API, MVVM/MVP/MVC and desktop/mobile UI, Unity/client, infrastructure/integration, and cross-cutting concerns. Each role SHALL include a short definition, intended static detection sources, typical metadata keys, example use cases, the existing catalog support tier, and exactly one v0.10 annotation disposition.

#### Scenario: Reviewer can classify a role across the supported families

- **WHEN** a reviewer selects any role in the first-wave catalog
- **THEN** the catalog identifies its family, meaning, static evidence sources, metadata examples, use case, existing support tier, and v0.10 annotation disposition

#### Scenario: Ambiguous framework-specific roles are not canonical by accident

- **WHEN** a role depends on runtime framework behavior or has materially ambiguous semantics
- **THEN** the catalog marks it custom-mapping-only or deferred and documents why it is not a v0.10 built-in default

### Requirement: Support tiers preserve YAML-first customization

The catalog SHALL retain its vocabulary support tiers—canonical vocabulary, optional annotation candidate, examples-only, custom-mapping expected, or deferred—and SHALL separately classify each role's v0.10 annotation disposition as exactly one of: first-class annotation, custom-mapping-only, or deferred. User-defined attributes mapped by full type name and deterministic YAML conventions SHALL remain supported with or without the optional package.

#### Scenario: A custom attribute can represent a catalog role

- **WHEN** a project uses `MyCompany.Architecture.DomainLayerAttribute`
- **THEN** the documentation provides a full-type-name YAML mapping to `DomainLayer` without requiring the ArchLinterNet package

#### Scenario: Every catalog role has one annotation disposition

- **WHEN** a reviewer consults the catalog for any listed role
- **THEN** exactly one v0.10 annotation disposition identifies it as first-class, custom-mapping-only, or deferred

### Requirement: Worked examples remain static-analysis-only

The catalog SHALL include one modular-monolith example covering Sales, Inventory, and SharedKernel, one Unity/client example, one custom-attribute mapping, and one assembly-level metadata example. Examples SHALL use narrow selectors and SHALL state that classification and selector evaluation are static semantic facts consumed by policy, not runtime behavior inference.

#### Scenario: AI policy guidance avoids broad unsafe selectors

- **WHEN** an author uses the catalog to write a policy
- **THEN** the guidance recommends explicit role/metadata criteria, narrow mappings, and reviewable exclusions, and does not encourage always-true selectors or broad exclusions

### Requirement: The catalog preserves the product boundary

The catalog SHALL state that ArchLinterNet remains YAML-first and static-analysis-only, does not prescribe one architecture style, does not require production projects to use the optional package, and does not imply a runtime annotation assembly, runtime DI analysis, framework behavior validation, unrestricted plugin execution, or silent policy generation.

#### Scenario: Existing projects can adopt vocabulary incrementally

- **WHEN** a project has no ArchLinterNet annotations and uses custom YAML conventions
- **THEN** the catalog treats that adoption path as supported and does not require adding the optional package or a runtime dependency

### Requirement: The catalog explains single-role classification semantics

The catalog SHALL state that a type has at most one effective primary role. Multiple canonical role annotations or conflicting same-tier canonical/custom role evidence SHALL fail closed without an effective role; equivalent evidence SHALL coalesce. The six approved context keys SHALL enrich that role without creating accumulated role tags, and lower-precedence role metadata SHALL NOT be merged into a winning higher-precedence role.

#### Scenario: Related roles do not accumulate

- **WHEN** an author considers classifying one type as both `DomainLayer` and `AggregateRoot`
- **THEN** the catalog explains that the type has one primary role and that independent context is represented through bounded metadata or existing namespace/layer policy

#### Scenario: Conflicting canonical roles fail closed

- **WHEN** a type carries two canonical role annotations with different roles
- **THEN** the classifier reports a deterministic conflict and the type has no effective role for selectors

### Requirement: Current classification sources are distinguished from future evidence

The catalog SHALL identify `asmdef` and package-reference facts as future discovery guidance, not current classification sources, unless a separate semantic-classification-model change adopts them.

#### Scenario: Unity assembly evidence is not implied to be active

- **WHEN** the catalog references an asmdef while discussing Unity/client roles
- **THEN** it states that the current classification model does not consume asmdef facts automatically

### Requirement: The catalog defines port and anti-corruption vocabulary

The semantic role catalog SHALL define `Port`, `Adapter`, `PrimaryPort`, `SecondaryPort`, and `AntiCorruptionLayer`, including their support tiers, static evidence guidance, reviewed metadata keys, and exactly one v0.10 annotation disposition. `ExternalSystem`, `IntegrationAdapter`, `PersistenceAdapter`, and direct-database examples SHALL be marked custom-mapping-only or deferred unless independently promoted by the reviewed v0.10 catalog.

#### Scenario: Policy author maps a project-owned port attribute

- **WHEN** a project uses a user-owned attribute for a named secondary port
- **THEN** the catalog shows a YAML mapping and selector metadata without implying that ArchLinterNet supplies the attribute

## REMOVED Requirements

### Requirement: Type-level and assembly-level annotation use cases are documented

**Reason**: The v0.10 model now distinguishes role-bearing annotations from metadata-only context attributes and defines their separate scope, precedence, and conflict behavior.

**Migration**: Use the canonical FQNs and target matrix in the versioned v0.10 contract. Role-bearing assembly evidence still participates at assembly precedence; metadata-only context can enrich a higher-precedence type role under the bounded composition rules.

### Requirement: The first wave approves no built-in annotation types

**Reason**: Issue #566 supersedes the earlier no-package decision for the v0.10 capability wave and approves a bounded optional source-only package.

**Migration**: Treat the package as planned contract until it is implemented and released. Existing releases continue to support user-owned attributes mapped by exact full type name.

## ADDED Requirements

### Requirement: Canonical annotation identities, targets, and context composition are documented

The catalog SHALL document the exact canonical identities and approved type or assembly targets, while preserving one primary role and the canonical context metadata composition rules. Role-bearing assembly annotations SHALL act as assembly-source evidence and SHALL NOT merge their role metadata into a higher-precedence type role. The six canonical metadata-only annotations MAY add context independently of the primary role using the reviewed type/assembly precedence rules. Every custom `classification.assembly_attributes` mapping example SHALL include an `attribute` and a `role`.

#### Scenario: A role-bearing assembly annotation classifies types without a type role

- **WHEN** an assembly declares an approved assembly-targeted canonical role annotation
- **THEN** the assembly role applies only to types for which the assembly source wins and carries that role annotation's own metadata

#### Scenario: Type role metadata and assembly context metadata compose

- **WHEN** a type has a higher-precedence role annotation and its assembly has canonical metadata-only context annotations
- **THEN** the type keeps its primary role and missing metadata keys may be filled from the assembly defaults under the catalog's documented conflict rules

### Requirement: The v0.10 annotation decision is finite, optional, and versioned

The v0.10 semantic annotation wave SHALL approve the optional source-only `ArchLinterNet.Annotations` package with the finite canonical role set and metadata vocabulary recorded in the versioned catalog manifest. Injected annotation types SHALL be internal and SHALL NOT require a separate runtime annotation assembly. The package SHALL remain optional, and user-defined attributes mapped by full type name in YAML SHALL remain a first-class adoption path. Documentation SHALL distinguish this approved future contract from package behavior available in already-released tools.

#### Scenario: A reader evaluates an annotation example

- **WHEN** the catalog shows a canonical annotation such as `[AggregateRoot]`
- **THEN** it identifies the exact FQN, role, targets, and metadata contract and states that existing released tools do not yet recognize the planned package

#### Scenario: A reader looks for an annotation package

- **WHEN** a reader searches the catalog or policy-format documentation for an installable ArchLinterNet annotation package
- **THEN** the documentation states that the v0.10 package is planned but not available in already-released tools, explains the optional source-only form, and confirms that custom full-name YAML mappings remain supported today
