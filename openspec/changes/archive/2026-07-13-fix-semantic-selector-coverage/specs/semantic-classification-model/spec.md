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

#### Scenario: Contextual dependency and allow-only contracts register executable coverage consumption
- **WHEN** a contextual dependency or allow-only contract declares a source, target, or exclusion selector
- **THEN** the model records the selector's complete role, metadata values, operators, and source-target relationship as coverage-participating consumption
- **AND** semantic coverage evaluates that record with the same matching semantics as the contextual contract

#### Scenario: Stale selector is distinguished from an unmatched namespace layer
- **WHEN** a `layers.<name>.selector`'s `role`/`metadata` criteria match zero types classified by the model
- **THEN** the model classifies this as a `stale selector`, a status distinct from any existing namespace-layer diagnostic
