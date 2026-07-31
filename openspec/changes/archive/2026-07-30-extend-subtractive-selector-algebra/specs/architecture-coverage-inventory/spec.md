## ADDED Requirements

### Requirement: Coverage inventories effective selector participation
The system SHALL expose typed inclusion, exclusion, and stale-exclusion evidence for compatible selector families whose fact universe is available.

#### Scenario: Stale exclusion is visible
- **WHEN** a compatible exclusion matches no included fact
- **THEN** coverage inventory SHALL record it as unmatched rather than silently expanding or changing the analysis graph

