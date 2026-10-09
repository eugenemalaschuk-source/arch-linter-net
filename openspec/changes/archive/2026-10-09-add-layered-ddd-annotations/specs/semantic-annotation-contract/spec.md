## ADDED Requirements

### Requirement: Layered and DDD role attributes follow the reviewed catalog

The source-only annotation set SHALL provide internal sealed role attributes for every `layered-clean` and `ddd` role in `architecture/semantic-annotations.v1.json`. Each attribute SHALL use the manifest FQN, `AllowMultiple=false`, and `Inherited=false`; it SHALL expose the six optional public string metadata properties defined by the role attribute contract. Its valid attribute targets SHALL match the manifest exactly.

#### Scenario: Layered and clean roles use approved targets

- **WHEN** a project compiles the layered/clean annotation source set
- **THEN** `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, and `SharedKernel` are assembly-only attributes, while `CompositionRoot` supports classes, structs, interfaces, and assemblies

#### Scenario: DDD roles use type targets

- **WHEN** a project compiles the DDD annotation source set
- **THEN** `Entity`, `AggregateRoot`, `ValueObject`, `DomainService`, `DomainEvent`, `Repository`, `Specification`, and `Factory` support classes, structs, and interfaces

#### Scenario: Role attribute definitions match the manifest contract

- **WHEN** the source definitions are inspected
- **THEN** each of the 17 roles has its exact manifest FQN, is internal and sealed, has a public parameterless constructor, and exposes the six optional public string metadata properties
