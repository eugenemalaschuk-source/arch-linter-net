## ADDED Requirements

### Requirement: Preflight exposes evaluated-manifest eligibility consistently
The build-state preflight result, CLI diagnostics, and Testing API SHALL expose the same per-analysis-unit evaluated-manifest eligibility and sorted invalidation reasons. A cache-ineligible outcome SHALL not be presented as a cache hit or authorization, and it SHALL not redefine the existing primary ordinary-preflight state categories.

#### Scenario: Legacy receipt lacks evaluated evidence
- **WHEN** an otherwise current legacy receipt lacks the evaluated manifest and required artifact verification evidence
- **THEN** ordinary preflight retains its existing result while the cache eligibility is `cache-ineligible` with an explicit reason

#### Scenario: Machine-readable consumer observes the result
- **WHEN** CLI or Testing API emits build-state/profile diagnostics for a selected project
- **THEN** both projections contain the same eligibility value and invalidation reasons
