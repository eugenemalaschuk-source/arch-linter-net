## ADDED Requirements

### Requirement: CQRS/messaging and web/API role attributes follow the reviewed catalog

The source-only annotation set SHALL provide role attributes for every `cqrs-messages` and `web-api` first-class role in `architecture/semantic-annotations.v1.json`: `Command`, `CommandHandler`, `Query`, `QueryHandler`, `Event`, `EventHandler`, `IntegrationEvent`, `ReadModel`, `Controller`, `Endpoint`, `RequestDto`, `ResponseDto`, `ApiContract`, and `Validator`. Each attribute SHALL use its exact manifest FQN and targets, be internal and sealed, reject multiple uses and inheritance, have a public parameterless constructor, and expose only the six bounded string metadata properties defined by the role-attribute contract.

#### Scenario: The approved role family source set matches the manifest
- **WHEN** a consumer compiles the annotation source set
- **THEN** all 14 approved CQRS/messaging and web/API role identities are available on classes, structs, and interfaces with the exact manifest FQNs and no role identity is added for deferred or custom-mapping-only catalog entries

#### Scenario: Canonical role evidence uses the existing classifier
- **WHEN** a consumer type uses one approved role attribute and its assembly has a supported catalog identity
- **THEN** the existing semantic classifier resolves the exact FQN to the manifest role without a YAML mapping and exposes its bounded metadata through the existing role fact and index

#### Scenario: Custom mappings remain equivalent semantic evidence
- **WHEN** a user-owned attribute is explicitly mapped to the same role and metadata as an approved canonical annotation
- **THEN** both inputs produce the same effective role and metadata through the existing semantic model while retaining their respective evidence provenance

#### Scenario: CQRS and API facts compose through existing selectors
- **WHEN** an application marks a command and its handler in the same bounded context, a query and read model, an integration-event boundary, request and response DTOs, or a controller and endpoint
- **THEN** those distinct types resolve to their approved single roles and the existing selectors, contextual contracts, semantic coverage, and explain projections consume those facts without inferring runtime behavior

#### Scenario: ApiContract remains separate from reviewed API membership
- **WHEN** a type has the primary semantic role `ApiContract`, or a `ValueObject` is selected into a reviewed API surface by an authored #525 selector
- **THEN** the role annotation alone does not select reviewed API membership and the selected `ValueObject` retains its primary semantic role
