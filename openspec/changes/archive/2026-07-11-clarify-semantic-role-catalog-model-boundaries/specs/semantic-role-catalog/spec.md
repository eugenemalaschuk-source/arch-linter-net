## MODIFIED Requirements

### Requirement: Type-level and assembly-level annotation use cases are documented
The catalog SHALL document optional type-level annotations and role-bearing assembly-level equivalents where they are statically meaningful, while preserving the same single-role and winning-source metadata semantics as YAML mappings. Every `classification.assembly_attributes` mapping example SHALL include an `attribute` and a `role`. The catalog SHALL explicitly defer metadata-only assembly context such as `[assembly: BoundedContext("Billing")]` until a separate semantic-classification-model change defines its schema and cross-source metadata merge behavior.

#### Scenario: Role-bearing assembly classification is mapped
- **WHEN** an assembly declares `[assembly: SharedKernel("Billing")]`
- **THEN** the catalog documents an equivalent full-name YAML mapping with `role: SharedKernel` and `boundedContext` metadata that applies only when the assembly-attribute source wins for a type

#### Scenario: Metadata-only assembly context is deferred
- **WHEN** an author wants `[assembly: BoundedContext("Billing")]` to add context to types with higher-precedence type roles
- **THEN** the catalog states that the current model cannot merge that metadata and directs the author to a future model/schema change

## ADDED Requirements

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
