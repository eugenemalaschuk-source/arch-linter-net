## ADDED Requirements

### Requirement: SARIF results expose CEL expression provenance as related locations
When a diagnostic rendered as a SARIF result has CEL expression participation (per the `violation-reporting` capability's `when_expressions` array), the SARIF formatter SHALL add one related location per expression entry whose message includes the expression's source text and result, in addition to any existing physical/logical locations and policy-origin related locations. SARIF results for diagnostics without expression participation SHALL be unaffected.

#### Scenario: SARIF result includes expression-provenance related location
- **WHEN** a context-dependency violation with a `when`-bearing forbidden selector is rendered as SARIF
- **THEN** the result's `relatedLocations` includes an entry whose `message.text` states the evaluated expression's source text and that it matched

#### Scenario: Existing policy-origin related locations are preserved
- **WHEN** a diagnostic has both policy-origin related locations and CEL expression participation
- **THEN** both sets of related locations are present, and neither replaces the other

#### Scenario: Non-CEL SARIF results are unaffected
- **WHEN** a diagnostic has no `when`-bearing selector
- **THEN** its SARIF result is identical to the output produced before this change
