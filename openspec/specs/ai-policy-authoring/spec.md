# AI Policy Authoring Specification

## Purpose
Documents how AI agents author and validate architecture policies, including capability and limit guidance.
## Requirements
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

### Requirement: AI agents decompose policies by concern with minimal edits
The AI policy-authoring guidance SHALL instruct agents to keep exactly one selected root, prefer focused fragments organized by architecture concern or bounded context, keep small shared settings inline when clearer, preserve globally unique contract IDs within each family and mode, and avoid editing unrelated fragments merely to reduce file count.

#### Scenario: Agent adds one bounded-context rule
- **WHEN** an AI agent adds or revises a rule owned by one bounded context
- **THEN** the guidance directs it to edit the owning focused fragment and the root import list only when necessary

#### Scenario: Agent reviews a fragmented policy
- **WHEN** an AI agent prepares a policy change for review
- **THEN** the checklist verifies graph roles, explicit schema fit, global conflict safety, narrow fragment scope, and validation through the selected root

### Requirement: AI CEL authoring guidance
ArchLinterNet SHALL provide AI-facing guidance for authoring CEL `when` predicates that uses the same public terminology, profile version, and examples as the human-facing CEL guide, with no hidden AI-only conventions.

#### Scenario: Agent is told expression fields contain standard CEL
- **WHEN** an agent reads the AI CEL authoring guidance
- **THEN** it states that `when` fields contain standard CEL under ArchLinter CEL Profile v1, links the official CEL spec and the public CEL guide, and does not describe the expression language as a proprietary ArchLinterNet DSL

#### Scenario: Agent is warned against inventing syntax
- **WHEN** an agent writes or edits a `when` expression
- **THEN** the guidance explicitly instructs it not to invent operators, functions, or syntax beyond what the public support matrix documents

#### Scenario: Agent is warned against policy-weakening
- **WHEN** an agent is tempted to broaden a `when` predicate or a selector to make generated code pass validation
- **THEN** the guidance explicitly instructs it not to weaken policy merely to pass generated code, and to prefer fixing the code or narrowing the predicate instead

#### Scenario: Agent is guided toward literal selectors when clearer
- **WHEN** an agent considers adding a `when` predicate
- **THEN** the guidance instructs it to prefer a literal role/metadata selector when the rule can be expressed that way, reserving `when` for cases literal selectors cannot express

#### Scenario: Guidance provides self-contained context
- **WHEN** an agent needs the allowed variables/types/functions for a `when` location
- **THEN** the guidance provides or links a self-contained reference (the public CEL guide's authoring reference) rather than requiring undocumented project-specific knowledge

