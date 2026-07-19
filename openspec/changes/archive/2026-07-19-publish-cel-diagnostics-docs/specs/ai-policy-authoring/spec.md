## ADDED Requirements

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
