## ADDED Requirements

### Requirement: Reusable source sets implement the 0.5.1 policy-expression design slice
The 0.5.1 compatibility contract SHALL include reusable source sets and deterministic contract expansion as an additive policy-expression capability. Policies that declare exact single sources SHALL remain valid and unchanged, and expansion SHALL NOT extend analysis beyond the declared `analysis` inputs.

#### Scenario: Existing exact-source policy is unchanged
- **WHEN** a 0.5.0 policy declares only exact `source` values
- **THEN** it loads, expands to nothing, and produces identical contract identities and findings
