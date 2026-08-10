## ADDED Requirements

### Requirement: Explain projects deferred source-set provenance
The `explain` command SHALL report authored contract, source-set, source-set reference, selector,
and resolved project provenance for project metadata contracts whose project sets were bound from
solution discovery.

#### Scenario: Explain reports an imported project selector
- **WHEN** an imported policy fragment contributes a solution-derived project set to a metadata
  contract
- **THEN** explain identifies the fragment location and the resolved project path for the selector
