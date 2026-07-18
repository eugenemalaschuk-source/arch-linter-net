## MODIFIED Requirements

### Requirement: Semantic coverage classifies discovered facts and stale selectors

Semantic role coverage SHALL classify each in-scope resolved role/metadata fact as `covered`, `excluded`, or `uncovered`, SHALL classify a selector or contextual semantic reference with no current match as `stale` — accounting for the selector's `when` expression when one is declared, not literal `role`/`metadata` criteria alone — and SHALL classify unresolved role references or classification conflicts as `unknown` or `conflicting` evidence without treating them as dependency violations. An expression evaluation failure encountered while determining a selector's match set SHALL be reported through the expression-evaluation-error path defined by `semantic-classification-model`/`contextual-dependency-contracts`/`contextual-allow-only-contracts`, and SHALL NOT be silently classified as `stale` or `uncovered`.

#### Scenario: Unclassified first-party code is visible when semantic coverage is enabled
- **WHEN** semantic classification is enabled and a first-party type in the contract roots has no resolved role
- **THEN** the coverage result reports it as an uncovered semantic fact with representative type evidence

#### Scenario: Discovered role is governed by a selector
- **WHEN** a resolved role/metadata fact matches a selector-backed layer
- **THEN** the fact is classified as covered

#### Scenario: Discovered role is governed by a contextual contract
- **WHEN** a resolved role/metadata fact is referenced by a contextual contract selector
- **THEN** the fact is classified as covered even if no layer selector matches it

#### Scenario: Empty semantic selector is stale
- **WHEN** a semantic layer or contextual selector is valid but its combined literal-and-`when` match set contains no current classified fact
- **THEN** coverage reports a stale semantic selector with selector evidence, including the `when` expression source text when one is declared

#### Scenario: A broad `when` expression is visible as coverage evidence, not a silent pass
- **WHEN** a selector's `when` expression matches a broad set of classified facts (e.g. an expression that is trivially `true` for most candidates)
- **THEN** semantic coverage continues to classify matched facts as `covered` using the same evidence path as a literal match — broad matching remains visible through ordinary coverage/stale-selector reporting rather than being hidden by expression evaluation succeeding

#### Scenario: Expression evaluation failure is not misclassified as stale or uncovered
- **WHEN** determining a selector's match set requires evaluating a `when` expression that fails to evaluate for some candidate
- **THEN** the coverage engine does not classify the affected selector as `stale` or the affected fact as `uncovered`; the run instead fails with the reported expression evaluation error
