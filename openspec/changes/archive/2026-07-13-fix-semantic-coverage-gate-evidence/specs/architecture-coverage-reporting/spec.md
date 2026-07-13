## ADDED Requirements

### Requirement: Strict semantic diagnostics participate in coverage validation
Selected strict semantic-role coverage contracts SHALL report stale selectors, classification conflicts, and metadata extraction failures as coverage findings as well as summary evidence.

#### Scenario: Stale selector fails strict semantic coverage
- **WHEN** a non-external semantic selector matches no classified type
- **THEN** strict semantic coverage returns a coverage finding for the stale selector

### Requirement: Semantic evidence explains classification and governance
Semantic coverage summary evidence SHALL identify the matching layer or contextual consumer for covered facts, role and metadata for excluded facts, and source plus metadata details for conflicts; its ordering and value formatting SHALL be deterministic and culture-invariant.

#### Scenario: Covered and excluded evidence identifies the mechanism
- **WHEN** a semantic fact is covered by a selector layer and another is excluded by role and metadata
- **THEN** summary evidence identifies the matching layer for the covered fact and the role/metadata for the excluded fact
