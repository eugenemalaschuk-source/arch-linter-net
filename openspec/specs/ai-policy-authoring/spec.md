## ADDED Requirements

### Requirement: AI authoring guide
ArchLinterNet SHALL provide AI-facing documentation that explains how agents create and revise architecture policies from real repository facts rather than idealized architecture names.

#### Scenario: Agent starts from real code
- **WHEN** an agent reads the AI authoring guide before changing a policy
- **THEN** the guide instructs the agent to inspect actual assemblies, namespaces, project references, and existing policy files before proposing layers or contracts

#### Scenario: Agent chooses strict or audit mode
- **WHEN** an agent needs to model a future-state boundary that is not currently enforceable
- **THEN** the guide instructs the agent to place the rule in an audit contract group instead of a strict contract group

#### Scenario: Agent avoids unsupported YAML
- **WHEN** an agent writes policy YAML
- **THEN** the guide explicitly warns the agent not to invent unsupported fields, contract families, matching modes, or broad ignored violations

#### Scenario: Agent considers conditional compilation
- **WHEN** an agent inspects a repository with `#if UNITY_EDITOR` or `#if DEBUG` blocks
- **THEN** the guide instructs the agent to consider whether method-body contracts need `analysis.condition_sets` to avoid false positives or missed violations under different symbol configurations

### Requirement: Capability and limit documentation
ArchLinterNet SHALL document the linter capabilities and limits that matter to AI policy authors.

#### Scenario: Agent reviews supported contract families
- **WHEN** an agent checks the AI capability documentation
- **THEN** the documentation lists supported dependency, layer order, allow-only, cycle, method-body, asmdef, independence contracts with their strict and audit variants, and condition sets under the `analysis` section

#### Scenario: Agent reviews unsupported capabilities
- **WHEN** an agent checks the AI capability documentation
- **THEN** the documentation identifies capabilities the engine cannot validate yet, including rules that would require source ownership, runtime behavior, security analysis, data flow analysis, or unsupported YAML fields

### Requirement: Policy recipes
ArchLinterNet SHALL provide reusable policy recipe samples for common architecture shapes.

#### Scenario: Clean architecture recipe exists
- **WHEN** a user or agent looks for a clean architecture example
- **THEN** a standalone sample policy shows application, domain, infrastructure, and UI-style boundaries using supported YAML fields

#### Scenario: Modular monolith recipe exists
- **WHEN** a user or agent looks for a modular monolith example
- **THEN** a standalone sample policy shows module independence and shared kernel or public contract patterns using supported YAML fields

#### Scenario: Unity asmdef recipe exists
- **WHEN** Unity support is documented
- **THEN** a standalone sample policy shows runtime/editor `.asmdef` boundary checks using supported YAML fields

### Requirement: AI policy review checklist
ArchLinterNet SHALL provide a checklist for reviewing AI-generated policy changes before they are proposed or merged.

#### Scenario: Agent reviews a policy PR
- **WHEN** an agent prepares a policy change for review
- **THEN** the checklist asks whether layers map to real namespaces or assemblies, strict rules pass today, future-state rules are audit-only, ignores are narrow and reasoned, package names are real, samples/docs match executable policy, and validation was run locally

#### Scenario: Agent reviews ignored violations
- **WHEN** an agent adds or changes `ignored_violations`
- **THEN** the checklist requires each ignore to be narrow, justified, and tied to migration debt rather than used to silence broad classes of new violations
