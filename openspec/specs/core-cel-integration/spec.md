# core-cel-integration Specification

## Purpose
TBD - created by archiving change core-cel-integration. Update Purpose after archive.
## Requirements
### Requirement: Core compiles every `when` field at policy-load time

`ArchLinterNet.Core` SHALL compile every declared `when` field through the public
`ArchLinterNet.CEL` API during `ArchitecturePolicyDocumentLoader.Load`, before any
architecture contract executes. Compilation SHALL force a boolean predicate result.
A `when` field that is absent or empty SHALL NOT construct or invoke a CEL environment.

#### Scenario: Valid selector expression compiles during load

- **WHEN** a policy declares `layers.sales.selector.when` with a well-typed boolean
  CEL expression referencing only `subject` members from the closed shared shape
- **THEN** `ArchitecturePolicyDocumentLoader.Load` succeeds and the loaded document's
  layer selector carries a compiled predicate

#### Scenario: Literal-only policy never constructs the CEL engine

- **WHEN** a policy declares no `when` field anywhere
- **THEN** loading that policy does not construct a `CelEnvironment` or compile any
  expression

### Requirement: Compile failures block policy loading with an actionable message

`ArchitecturePolicyDocumentLoader.Load` SHALL throw before returning a document when any `when` field fails compilation (syntax error, unresolved identifier or member, unsupported feature, non-boolean result type). The thrown exception's message SHALL deterministically reflect the underlying `CelDiagnostic`'s code and message.

#### Scenario: Unknown member fails policy load

- **WHEN** a `when` field references an undeclared member such as
  `subject.metadata.domain` (not `subject.metadataText`)
- **THEN** `ArchitecturePolicyDocumentLoader.Load` throws before returning a document

#### Scenario: Non-boolean expression fails policy load

- **WHEN** a `when` field is syntactically valid but evaluates to a non-boolean type
- **THEN** `ArchitecturePolicyDocumentLoader.Load` throws before returning a document

### Requirement: Each expression location compiles against its documented closed context schema

A selector-backed layer predicate SHALL compile only against a `subject` root
variable. A contextual source predicate SHALL compile only against a `source` root
variable. A contextual target or exclusion predicate SHALL compile only against
`source`, `target`, and `dependency` root variables. A predicate referencing a root
variable not available in its own location's schema SHALL fail to compile.

#### Scenario: Source-only predicate cannot reference target

- **WHEN** a contextual dependency contract's `source.when` references `target`
- **THEN** policy loading fails because `target` is not declared in the contextual
  source predicate's schema

#### Scenario: Contextual target predicate can compare source and target

- **WHEN** a contextual dependency contract's `forbidden[*].when` compares
  `source.metadataText["domain"]` against `target.metadataText["domain"]`
- **THEN** the expression compiles successfully because both `source` and `target`
  are declared in the contextual target predicate's schema

### Requirement: `when` acceptance is scoped to the documented first-wave locations only

The shared `ArchitectureContextSelector` type SHALL accept `when` only as part of a `strict_context_dependencies`, `audit_context_dependencies`, `strict_context_allow_only`, or `audit_context_allow_only` contract's `source`, target-list (`forbidden`/`allowed`), or `exclude` selector. `when` declared on a port-boundary contract's `source`, `allowed_seams`, `forbidden`, `exclude`, or an adapter binding's `adapter`, `expected_port`, or `allowed_contexts` selector SHALL be rejected as an unknown property, exactly like any other undeclared key.

#### Scenario: Contextual dependency selector accepts `when`

- **WHEN** a `strict_context_dependencies` contract's `forbidden[*]` selector
  declares `when`
- **THEN** policy loading accepts the field and compiles it

#### Scenario: Port-boundary selector rejects `when`

- **WHEN** a `strict_port_boundaries` contract's `source` or `allowed_seams`
  selector declares `when`
- **THEN** policy loading fails with an unknown-property error, identical to any
  other undeclared key on that selector

### Requirement: Compiled predicates are cached on their owning selector node

Once a `when` field compiles successfully, the resulting compiled predicate SHALL be
retained on the selector node that declared it for the lifetime of the loaded
document, so that repeated evaluation of that predicate never re-parses or
re-type-checks the expression. Two separate `Load()` calls, including two calls for
byte-identical policy text, SHALL produce independently cached predicates with no
shared mutable state between them.

#### Scenario: Compiled predicate is reused, not re-parsed

- **WHEN** a policy with a `when` field loads successfully
- **THEN** the owning selector's compiled predicate is available for evaluation
  without any further compilation step

#### Scenario: Two policy sessions do not share cached predicates

- **WHEN** the same policy file is loaded twice, producing two independent
  `ArchitectureContractDocument` instances
- **THEN** the compiled predicates attached to each document's selectors are
  independent instances with no shared static cache backing them

### Requirement: Typed context factories map Core architecture facts to immutable CEL values

`ArchLinterNet.Core` SHALL provide typed context-factory functions that map
Core-owned architecture-fact records — shaped exactly like `cel-policy-model`'s
closed `subject`/`source`/`target` and `dependency` member lists — into immutable
`ArchLinterNet.CEL` values and evaluation contexts, using only the public
`ArchLinterNet.CEL` API. These factories SHALL NOT accept arbitrary CLR objects,
reflection metadata, or host services as input.

#### Scenario: Subject facts map to a schema-valid CEL object value

- **WHEN** a Core-owned subject-facts record with all 16 documented members is passed
  to the subject context factory
- **THEN** the factory produces a `CelValue` of kind `Object` that validates against
  the shared subject/source/target object schema

#### Scenario: Dependency facts map to a schema-valid CEL object value

- **WHEN** a Core-owned dependency-facts record with all 4 documented members is
  passed to the dependency context factory
- **THEN** the factory produces a `CelValue` of kind `Object` that validates against
  the dependency object schema

### Requirement: Core-to-CEL is the only allowed dependency direction

`ArchLinterNet.Core` SHALL reference `ArchLinterNet.CEL` directly. `ArchLinterNet.CEL`
SHALL NOT reference `ArchLinterNet.Core` or any other ArchLinterNet product assembly,
and Core SHALL NOT use `InternalsVisibleTo`, reflection, linked source files, or
duplicated parser/evaluator logic to reach `ArchLinterNet.CEL` internals.

#### Scenario: Self-policy enforces the dependency direction

- **WHEN** this repository's own `architecture/dependencies.arch.yml` strict
  contracts run against the built solution
- **THEN** the existing `cel-must-not-depend-on-core` contract continues to pass and
  no new contract is required to express `Core → CEL` as an allowed edge

