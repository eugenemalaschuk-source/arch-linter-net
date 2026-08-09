## ADDED Requirements

### Requirement: Effective-schema diagnostics select actionable applicable failures
When the composed effective policy violates the full policy schema, the system SHALL project a deterministic diagnostic set that prioritizes concrete failures from applicable schema alternatives. It SHALL suppress an alternative only when the evaluated instance proves that the alternative's required discriminator is absent or its constant discriminator selects another variant. It SHALL retain failures from alternatives whose applicability cannot be determined and SHALL not change the policy's validation result.

#### Scenario: Namespace-only layer excludes unrelated variants
- **WHEN** a namespace-only layer is invalid because its namespace value has the wrong scalar type
- **THEN** the effective-schema diagnostic identifies that namespace value and does not report missing selector, namespace-suffix, or exclusion fields from inapplicable layer alternatives

#### Scenario: Discriminated contract alternative is inapplicable
- **WHEN** a contract selects one scope or contract variant and contains a defect within that selected variant
- **THEN** the diagnostic reports the selected variant's defect without required-property failures from alternatives with a missing or conflicting discriminator

#### Scenario: Nested composite failure has one actionable path
- **WHEN** an invalid effective policy reaches nested `anyOf`, `oneOf`, or conditional schema branches
- **THEN** the primary policy location identifies the deepest applicable failure where provenance is available

#### Scenario: Independent failures remain visible deterministically
- **WHEN** an effective policy contains multiple independently applicable schema defects
- **THEN** the diagnostic retains each defect in stable evaluator encounter order and human and structured projections remain deterministic

#### Scenario: Valid policies retain their validity
- **WHEN** a direct or imported policy was valid before diagnostic selection
- **THEN** it remains valid after diagnostic selection
