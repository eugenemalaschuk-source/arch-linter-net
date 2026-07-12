## MODIFIED Requirements

### Requirement: Classification vocabulary is defined
The semantic classification model SHALL define exactly ten classification terms with non-overlapping meanings: `role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, and `uncovered semantic fact`.

#### Scenario: Uncovered semantic fact is distinguished from an unclassified type
- **WHEN** a type receives a role/metadata assignment from some classification source
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model classifies this as an `uncovered semantic fact`, a status distinct from a type that received no role at all

#### Scenario: An override-assigned role does not by itself exempt a type from coverage
- **WHEN** a type's role/metadata was assigned by a `classification.overrides` entry
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model SHALL still classify this as an `uncovered semantic fact` — `override` is a classification source, not a coverage-exemption mechanism, and only `exclusion` removes a type from coverage consideration

#### Scenario: Contextual dependency and allow-only contracts are coverage-participating consumers
- **WHEN** a `strict_context_dependencies`/`audit_context_dependencies`/`strict_context_allow_only`/`audit_context_allow_only` contract's `source`, `forbidden`, `allowed`, or `exclude` selector references a discovered role/metadata value directly, without that role/metadata also being matched by a `layers.<name>.selector`
- **THEN** the model registers that selector's `(role, metadata key)` reference as coverage-participating consumption identically to a `layers.<name>.selector` match — a type consumed only by such a direct contextual-contract reference SHALL NOT be classified as an `uncovered semantic fact`

#### Scenario: Stale selector is distinguished from an unmatched namespace layer
- **WHEN** a `layers.<name>.selector`'s `role`/`metadata` criteria match zero types classified by the model
- **THEN** the model classifies this as a `stale selector`, a status distinct from any existing namespace-layer diagnostic
