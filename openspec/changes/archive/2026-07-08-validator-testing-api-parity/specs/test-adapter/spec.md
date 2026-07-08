## ADDED Requirements

### Requirement: Test adapter supports contract-ID selection
`ArchitectureValidationBuilder.WithContracts(IEnumerable<string> contractIds)` (and a `params string[]` overload) SHALL restrict the subsequent `ValidateStrict()`/`ValidateAudit()` run to only the given contract IDs, matching CLI `--contract` semantics.

#### Scenario: Selected contract runs, others are skipped
- **WHEN** `WithContracts("layer-order")` is set on a builder for a policy with multiple contracts, one of which (not `layer-order`) would otherwise fail
- **THEN** `ValidateStrict()` SHALL return `Passed = true`, since only the selected contract executes

### Requirement: Test adapter supports baseline merge
`ArchitectureValidationBuilder.WithBaseline(string baselinePath)` SHALL merge the given baseline file's ignored entries into the policy before validation, matching CLI `validate --baseline` semantics.

#### Scenario: Baseline suppresses a previously-known violation
- **WHEN** `WithBaseline(path)` is set to a baseline file that records an existing violation, and `ValidateStrict()` is called against a policy that would otherwise report that same violation
- **THEN** the violation SHALL NOT appear in `ArchitectureValidationResult.Violations` and SHALL NOT affect `Passed`

### Requirement: Test adapter supports enforcing unmatched-ignored-violations policy
`ArchitectureValidationBuilder.EnforceUnmatchedIgnoredViolationsPolicy(bool enforce = true)` SHALL control whether the subsequent validation run enforces the policy's `analysis.unmatched_ignored_violations` (`error`/`warn`/`off`) configuration, mirroring `ValidationRequest.EnforceUnmatchedIgnoredViolationsPolicy`. When not called, behavior SHALL remain unchanged from today (not enforced).

#### Scenario: Enforcement toggled on fails the run
- **WHEN** `EnforceUnmatchedIgnoredViolationsPolicy()` is called on a builder for a policy with `analysis.unmatched_ignored_violations: error` and unmatched ignored violations exist, but no other violations or cycles
- **THEN** `ValidateStrict()` SHALL return `Passed = false`

#### Scenario: Default behavior unchanged when not called
- **WHEN** `EnforceUnmatchedIgnoredViolationsPolicy()` is never called, against the same policy as above
- **THEN** `ValidateStrict()` SHALL return `Passed = true`, matching current (pre-change) behavior

### Requirement: Test adapter supports collecting validation timings
`ArchitectureValidationBuilder.WithTimings()` SHALL enable phase timing collection for the subsequent validation run and SHALL populate `ArchitectureValidationResult.Timing` with the resulting `ValidationTiming` instance. When not called, `ArchitectureValidationResult.Timing` SHALL be `null`.

#### Scenario: Timings populated when requested
- **WHEN** `WithTimings()` is set and `ValidateStrict()` is called
- **THEN** `ArchitectureValidationResult.Timing` SHALL be non-null and SHALL contain at least a `total` phase entry

### Requirement: Test adapter result exposes the full validation outcome
`ArchitectureValidationResult` SHALL carry `CoverageFindings`, `CoverageConfig`, `UnmatchedIgnoredViolations`, `UnmatchedIgnoredViolationsConfig`, and `CoverageSummaries` in addition to its existing `Passed`, `Violations`, `Cycles`, `PolicyConsistencyFindings`, and `PolicyConsistencyConfig` members, so that no distinct `ValidationOutcome` data is dropped when wrapped for test consumption.

#### Scenario: Coverage findings surfaced
- **WHEN** `ValidateStrict()` runs against a policy with a coverage contract that finds an uncovered namespace
- **THEN** `ArchitectureValidationResult.CoverageFindings` SHALL contain the corresponding finding and `ArchitectureValidationResult.CoverageSummaries` SHALL contain its summary counts

### Requirement: Test adapter failure message includes coverage and unmatched-ignored detail
`ArchitectureValidationResult.ShouldPass()` SHALL include formatted coverage findings (via `ArchitectureDiagnosticFormatter.FormatCoverageForHumans`) and formatted unmatched-ignored violations (via `FormatUnmatchedForHumans`) in the thrown `InvalidOperationException` message when those collections are non-empty, in addition to the violation, cycle, and policy-consistency detail it already includes.

#### Scenario: Failure message includes unmatched-ignored detail
- **WHEN** `ShouldPass()` is called on a failing result whose `UnmatchedIgnoredViolations` collection is non-empty
- **THEN** the thrown exception's message SHALL contain the formatted unmatched-ignored detail
