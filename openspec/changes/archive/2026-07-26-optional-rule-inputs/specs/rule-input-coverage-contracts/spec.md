## ADDED Requirements

### Requirement: Rule-input coverage supports exact optional-empty inputs
Rule-input coverage contracts SHALL support exact `optional_inputs` declarations in addition to contract-level exclusions. An optional input SHALL suppress only its own `empty-input` finding and SHALL not suppress `unresolved` findings or findings for other inputs in the selected contract.

#### Scenario: Optional target does not suppress a dangling source
- **WHEN** one declared input is optional-empty and another selected input references an undeclared layer
- **THEN** coverage reports the undeclared input as `unresolved` while reporting the planned input as optional-empty
