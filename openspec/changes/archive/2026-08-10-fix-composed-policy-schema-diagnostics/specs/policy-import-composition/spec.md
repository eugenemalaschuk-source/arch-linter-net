## MODIFIED Requirements

### Requirement: Effective-schema diagnostics select actionable applicable failures
When the composed effective policy violates the full policy schema, the system SHALL project a deterministic diagnostic set that prioritizes concrete failures from applicable schema alternatives. It SHALL suppress an alternative when the evaluated instance proves that the alternative's required discriminator is absent, that its constant discriminator selects another variant, that its declared type cannot accept the failing value, or that the failure arose beneath an `if` discriminator. It SHALL treat an alternative the instance already satisfies as applicable. It SHALL retain failures from alternatives whose applicability cannot be determined and SHALL not change the policy's validation result. The reported policy location SHALL describe the reported message, including when a specialized diagnostic replaces the generic schema failures.

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

#### Scenario: A specialized message keeps its own location
- **WHEN** a specialized diagnostic replaces the generic schema failures for an imported-fragment defect
- **THEN** the reported policy location identifies that fragment and the YAML path the message names

#### Scenario: A satisfied alternative is not reported as missing
- **WHEN** an `anyOf` is satisfied through one alternative and an unrelated defect exists elsewhere in the document
- **THEN** the unsatisfied alternatives of that `anyOf` are not reported as missing requirements

#### Scenario: A type-incompatible alternative is suppressed
- **WHEN** a scalar value fails a type-discriminated `anyOf` because its own JSON type matches only one alternative
- **THEN** the diagnostic reports that value once instead of once per rejected declared type
