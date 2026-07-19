# cel-policy-guide Specification

## Purpose
TBD - created by archiving change publish-cel-diagnostics-docs. Update Purpose after archive.
## Requirements
### Requirement: Public CEL guide exists and is published
The repository SHALL contain a public documentation page at `docs/policy-format/cel-expressions.md`, included in the MkDocs navigation under Policy Authoring, that serves as the canonical CEL policy expression guide.

#### Scenario: Guide page exists and builds
- **WHEN** `make docs-build` (or `make lint-docs`) runs
- **THEN** `docs/policy-format/cel-expressions.md` is present, included in `mkdocs.yml` nav, and builds without warnings or broken links

### Requirement: Guide separates standard CEL, the ArchLinter profile, and product context
The guide SHALL present three distinct layers: (1) standard CEL language syntax/semantics with a link to the official `cel-expr/cel-spec` definition, (2) ArchLinter CEL Profile v1 as an explicitly named safe subset with its own support matrix, and (3) ArchLinterNet policy context (YAML locations, variables, types).

#### Scenario: Reader distinguishes language from profile from product context
- **WHEN** a reader or AI agent reads the guide's overview section
- **THEN** it explicitly states that CEL is the standard language (linking the spec), that ArchLinter CEL Profile v1 is a safe subset (not a proprietary DSL and not full conformance), and that variables/types are ArchLinterNet-specific product context

#### Scenario: AI-first rationale is stated
- **WHEN** a reader reads the guide's overview section
- **THEN** it explains that AI coding agents can reuse existing CEL knowledge instead of an undocumented DSL, as the rationale for choosing standard CEL

### Requirement: Guide contains a complete typed authoring reference
The guide SHALL document, for every YAML location where `when` is accepted (`layers.<name>.selector.when`, contextual dependency `source.when`/`forbidden[*].when`/`exclude[*].when`, contextual allow-only `source.when`/`allowed[*].when`/`exclude[*].when`, `strict_layout_conventions[*].files_matching.when`, `audit_layout_conventions[*].files_matching.when`): the expected boolean result type, the available root variable(s) (`subject`, or `source`/`target`/`dependency`), and every member's name and type. It SHALL also list YAML locations that remain literal-only and explicitly forbid `when`.

#### Scenario: Every expression location is documented
- **WHEN** a reader looks up which YAML fields accept CEL
- **THEN** the guide lists all currently-supported `when` locations with their root variable(s) and confirms the result type must be boolean

#### Scenario: Forbidden locations are documented
- **WHEN** a reader checks whether `when` is allowed on `imports`, `analysis.*`, coverage contracts, or non-contextual contract families
- **THEN** the guide states these locations reject `when` at policy load

#### Scenario: Context schema is fully typed
- **WHEN** a reader looks up the `subject` variable
- **THEN** the guide lists every member (`fullName`, `simpleName`, `namespace`, `assemblyName`, `projectName`, `role`, `metadataText`, `metadataBool`, `kind`, `isAbstract`, `isSealed`, `baseTypeNames`, `interfaceTypeNames`, `attributeTypeNames`, `sourcePaths`, `sourceDirectoryPrefixes`) with its type, and notes that numeric metadata is not exposed

### Requirement: Guide contains a Profile v1 support matrix
The guide SHALL document supported CEL types, operators, and functions under ArchLinter CEL Profile v1, structural/evaluation limits, and explicitly-unsupported standard CEL capabilities, framed as profile limitations rather than proprietary shorthand.

#### Scenario: Reader checks whether a CEL feature is supported
- **WHEN** a reader looks up whether a given CEL operator or function is available
- **THEN** the support matrix states whether it is supported, deferred, or prohibited under Profile v1

### Requirement: Guide contains worked examples reusing shipped fixtures
The guide SHALL include a minimal first expression, a semantic-role selector example, a source/target contextual dependency example, a modular-monolith example, a layout-convention example, and an equivalent literal-policy example where literals are preferable — all reusing YAML already validated by shipped fixtures (the modular-monolith samples and layout-convention/port-boundary CLI test fixtures) rather than newly invented, unvalidated snippets.

#### Scenario: Examples match shipped fixtures
- **WHEN** a reviewer checks a copy-pastable YAML example in the guide
- **THEN** the example is equivalent to (or copied from) an existing sample policy or test fixture already exercised by the test suite

#### Scenario: Anti-pattern examples are included
- **WHEN** a reader looks for guidance on avoiding broad expressions
- **THEN** the guide shows at least one broad/policy-weakening anti-pattern (e.g. an always-true predicate or a stale map-key access) alongside a corrected, narrower alternative

### Requirement: Guide documents diagnostics and troubleshooting
The guide SHALL explain the distinction between parse, unsupported-feature, type/binding, and evaluation errors; describe source-span and YAML-location reporting; and give guidance for narrowing overly broad predicates.

#### Scenario: Reader distinguishes error categories
- **WHEN** a reader encounters a CEL error message
- **THEN** the guide's troubleshooting section explains which category (parse, unsupported feature, binding/type, evaluation) it falls into and what to check

### Requirement: Guide is reachable from README and cross-linked from related public docs
The guide SHALL be linked from `README.md`'s Documentation section, `docs/policy-format/supported-capabilities.md`, `docs/policy-format/index.md`, and `docs/reference/yaml-schema.md`'s `when` field callout.

#### Scenario: User discovers the guide from README
- **WHEN** a user reads `README.md`'s Documentation section
- **THEN** a link to the CEL guide is present

#### Scenario: Supported-capabilities page reframes CEL as supported-but-scoped
- **WHEN** a reader reads `docs/policy-format/supported-capabilities.md`
- **THEN** CEL `when` predicates are described as supported within a closed set of locations, with a link to the full guide, rather than listed only as an unsupported capability

