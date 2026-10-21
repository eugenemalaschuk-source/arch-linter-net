## ADDED Requirements

### Requirement: Public guidance supports safe adoption and migration of semantic annotations

Public documentation SHALL explain the optional source-only annotation path from package reference through canonical semantic facts to independently authored YAML selectors, contracts, and optional semantic-role coverage. It SHALL identify the compatible package/tool boundary and explain that annotations provide evidence, not permissions. User-owned attributes mapped through `classification.attributes` or `classification.assembly_attributes` SHALL remain a prominent, first-class alternative, with guidance on when that path is preferable. Migration instructions SHALL preserve behavior and context intentionally, allow adopters to retain working mappings, require fact parity before removing redundant mappings, and warn against combining migration with policy weakening.

#### Scenario: New adopter uses canonical annotations

- **WHEN** an adopter follows the annotation-first guide with a compatible package and tool
- **THEN** the guide shows how to opt in, author representative roles and context, inspect discovered facts, select those facts in YAML, and optionally use semantic-role coverage without implying that the attributes enforce architecture

#### Scenario: Adopter chooses custom attributes

- **WHEN** an adopter has organization-specific vocabulary, a domain role outside the canonical catalog, additional metadata, or a repository policy against an ArchLinterNet package reference
- **THEN** the documentation shows user-owned type and assembly attributes mapped by exact full name and identifies custom mapping as a supported first-class path

#### Scenario: Adopter migrates an existing mapping

- **WHEN** an adopter considers replacing a working custom mapping with a canonical annotation
- **THEN** the guide says migration is optional, compares classification facts before and after, preserves primary roles and contextual metadata intentionally, and removes redundant YAML only after parity is proven

#### Scenario: Semantic role and API membership remain distinct

- **WHEN** a `ValueObject` or other primary role is intentionally selected into a reviewed public API surface
- **THEN** the guide keeps its semantic role unchanged and explains that CLR visibility and authored #525 API membership selection are separate facts

#### Scenario: AI authoring preserves the semantic contract

- **WHEN** an AI agent authors or reviews policy for an annotated project
- **THEN** the AI guidance prefers a canonical annotation only when it exactly matches intent, preserves custom mappings otherwise, uses context metadata instead of multiple or overloaded roles, and does not infer permission or reviewed API membership from annotations

#### Scenario: Examples stay within the implemented vocabulary

- **WHEN** users follow the public examples
- **THEN** synthetic examples cover layered/DDD modules, CQRS/API roles, infrastructure adapters, Unity/client roles where applicable, custom user-owned attributes, context metadata conflict, and reviewed API membership composition without claiming unsupported runtime behavior

### Requirement: Public annotation catalog remains aligned with the reviewed manifest

The public canonical annotation reference SHALL list every first-class role and metadata-only context attribute recorded in `architecture/semantic-annotations.v1.json`, and clearly distinguish the assembly compatibility marker from semantic evidence. It SHALL identify each exact FQN, canonical role or metadata key, valid attribute targets, supported metadata properties or marker fields, assembly-level support, package/tool compatibility, and a representative selector or policy use where applicable. Automated generation or parity validation SHALL make changes to those reviewed manifest projections fail validation when the reference is stale.

#### Scenario: Catalog describes every canonical semantic annotation

- **WHEN** a role or context attribute is added, removed, or changed in the canonical manifest
- **THEN** the public reference reports any corresponding missing or stale FQN, role/key, target, property, assembly support, compatibility value, or policy example during validation

#### Scenario: Catalog explains compatibility metadata without classifying it

- **WHEN** an adopter inspects the assembly catalog identity marker
- **THEN** the reference identifies its generation and package version values as compatibility metadata and states that it contributes no role or context evidence

#### Scenario: Catalog preserves metadata-only identity boundaries

- **WHEN** an adopter reads the entry for `ArchLinterNet.Annotations.FeatureAttribute`
- **THEN** it is identified as the `feature` context attribute and not as a canonical `Feature` role annotation
