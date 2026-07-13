## ADDED Requirements

### Requirement: Semantic role coverage is an opt-in coverage scope
The coverage contract family SHALL accept `scope: semantic_role` for strict and audit coverage contracts, without changing validation or execution of any existing coverage scope.

#### Scenario: Semantic coverage contract is accepted
- **WHEN** a policy declares a valid `strict_coverage` or `audit_coverage` contract with `scope: semantic_role`
- **THEN** the policy loads and the contract is evaluated by the coverage engine

#### Scenario: Existing coverage scopes remain unchanged
- **WHEN** a policy declares namespace, project, assembly, dependency-edge, or rule-input coverage
- **THEN** its validation and findings remain identical to the pre-semantic integration behavior

### Requirement: Semantic coverage classifies discovered facts and stale selectors
Semantic role coverage SHALL classify each in-scope resolved role/metadata fact as `covered`, `excluded`, or `uncovered`, SHALL classify a selector or contextual semantic reference with no current match as `stale`, and SHALL classify unresolved role references or classification conflicts as `unknown` or `conflicting` evidence without treating them as dependency violations.

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
- **WHEN** a semantic layer or contextual selector is valid but matches no current classified fact
- **THEN** coverage reports a stale semantic selector with selector evidence

### Requirement: Semantic exclusions are explicit and documented
Every semantic-role coverage exclusion SHALL include a non-empty reason and SHALL be applied using exact role and metadata matching; excluded facts SHALL be reported with the reason and SHALL not also appear as uncovered.

#### Scenario: Reasoned semantic exclusion suppresses a finding
- **WHEN** a classified fact matches a semantic exclusion with a non-empty reason
- **THEN** the fact appears in the excluded coverage bucket with that reason and no uncovered finding is emitted

#### Scenario: Missing semantic exclusion reason is rejected
- **WHEN** a semantic-role coverage exclusion omits or blanks its reason
- **THEN** policy validation rejects the contract with an actionable configuration diagnostic
