# Machine Readable Policy Metadata Specification

## Purpose
Publishes a JSON Schema for the policy format and a capability manifest describing supported contract types.

## Requirements

### Requirement: Policy JSON Schema
ArchLinterNet SHALL provide a machine-readable JSON Schema for `dependencies.arch.yml` policy files that describes the supported policy shape for schema-aware tools and AI agents.

#### Scenario: Schema describes top-level policy structure
- **WHEN** a schema-aware tool reads the policy schema
- **THEN** it can discover the supported top-level `version`, `name`, `layers`, `legacy_runtime_layers`, `analysis`, and `contracts` fields

#### Scenario: Schema describes contract families
- **WHEN** a schema-aware tool reads the policy schema
- **THEN** it can discover the supported strict and audit contract arrays and the supported fields for each contract family

#### Scenario: Schema rejects unsupported fields
- **WHEN** a policy contains fields outside the documented schema
- **THEN** schema validation reports them as unsupported even if the current runtime loader would ignore unmatched YAML properties

### Requirement: Capability manifest
ArchLinterNet SHALL provide a machine-readable capability manifest that summarizes supported validation capabilities, contract kinds, matching semantics, validation modes, and known limits.

#### Scenario: Agent inspects supported capabilities
- **WHEN** an AI agent reads the capability manifest
- **THEN** it can determine that ArchLinterNet supports namespace dependency contracts, ordered layer contracts, allow-only contracts, cycle contracts, independence contracts, method-body forbidden API checks, Unity `.asmdef` checks, strict and audit groups, and ignored violation baselines

#### Scenario: Agent inspects limits
- **WHEN** an AI agent reads the capability manifest
- **THEN** it can determine which capabilities are not supported and avoid authoring fake rules for those capabilities

### Requirement: Metadata documentation
ArchLinterNet SHALL document where the schema and capability manifest live and how AI tools should use them.

#### Scenario: Agent follows metadata references
- **WHEN** an agent reads the AI documentation
- **THEN** the documentation points to the machine-readable schema and capability manifest as the source for supported policy structure and capabilities

#### Scenario: Samples align with metadata
- **WHEN** sample policies are added or changed
- **THEN** they use only fields described by the machine-readable metadata and current engine model
