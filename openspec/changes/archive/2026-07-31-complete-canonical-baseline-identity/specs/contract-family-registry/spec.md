## ADDED Requirements

### Requirement: Registered family baseline identity inventory is exhaustive
The contract-family registry SHALL classify every registered family as baseline-capable with its
canonical identity dimensions, intentionally non-baseline-capable with a reason, or a
lifecycle/configuration result with a separate stable result identity. Automated tests SHALL fail
when a registered family has no classification or a baseline-capable family can fall back to only
generic display identity.

#### Scenario: A newly registered family lacks identity classification
- **WHEN** a contract family is registered without an identity inventory classification
- **THEN** the registry validation test SHALL fail with the family name and required classification.

