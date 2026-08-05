## MODIFIED Requirements

### Requirement: One snapshot serves multiple mode evaluations
The system SHALL let `ArchitectureAnalysisSnapshot.Evaluate(string mode, ValidationTiming?)` be called for `strict` and/or `audit` against the same immutable preparation plan without re-running policy composition, project discovery, or artifact planning. Each evaluation SHALL perform its cache lookup before runner/session materialization. Cache-only outcomes SHALL not create a session; when any evaluation misses, the snapshot SHALL materialize one runner/session and reuse it for later misses without re-running policy composition, project discovery, artifact planning, or target-assembly loading.

#### Scenario: Strict and audit evaluated from one snapshot
- **WHEN** a caller calls `Evaluate("strict")` followed by `Evaluate("audit")` on the same snapshot
- **THEN** either call may return from cache without a session, and if either call misses, both miss-path evaluations use the one lazily materialized `ArchitectureAnalysisSession` without a second project discovery, artifact plan, or assembly load

#### Scenario: Combined execution matches separate runs
- **WHEN** `Evaluate("strict")` and `Evaluate("audit")` are called on one snapshot for a policy and target assemblies
- **THEN** each mode's `ValidationOutcome` (violations, cycles, coverage findings, unmatched-ignored findings, policy-consistency findings, classification facts) is identical to the `ValidationOutcome` produced by calling the existing single-mode `Validate` independently for that mode against the same inputs
