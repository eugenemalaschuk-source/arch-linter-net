# Test Adapter Specification

## Purpose
Provides a fluent NUnit-friendly test adapter that loads a policy by path or repository root and asserts strict/audit validation results.
## Requirements
### Requirement: Test adapter loads policy from path
`ArchitectureAssertions.FromPolicy(path)` SHALL return an `ArchitectureValidationBuilder` configured to load the YAML policy from the given file path.

#### Scenario: FromPolicy returns valid builder
- **WHEN** `FromPolicy("architecture/dependencies.arch.yml")` is called
- **THEN** a non-null `ArchitectureValidationBuilder` SHALL be returned

### Requirement: Test adapter loads policy from repository root
`ArchitectureAssertions.FromRepositoryRoot(root)` SHALL resolve the policy at `{root}/architecture/dependencies.arch.yml` and return a configured builder.

#### Scenario: FromRepositoryRoot with valid root
- **WHEN** `FromRepositoryRoot("/some/repo")` is called
- **THEN** the builder SHALL resolve to `/some/repo/architecture/dependencies.arch.yml`

### Requirement: Test adapter supports strict validation
`ArchitectureValidationBuilder.ValidateStrict()` SHALL run strict-mode contracts and return an `ArchitectureValidationResult`.

#### Scenario: ValidateStrict passes with clean policy
- **WHEN** `ValidateStrict()` is called on a builder with a policy that has no strict violations
- **THEN** `Passed` SHALL be `true` and `Violations` SHALL be empty

#### Scenario: ValidateStrict catches violations
- **WHEN** `ValidateStrict()` is called on a builder with a policy that has known strict violations
- **THEN** `Passed` SHALL be `false` and `Violations` SHALL contain the expected violations

### Requirement: Test adapter supports audit validation
`ArchitectureValidationBuilder.ValidateAudit()` SHALL run audit-mode contracts and return an `ArchitectureValidationResult`.

#### Scenario: ValidateAudit returns result
- **WHEN** `ValidateAudit()` is called
- **THEN** an `ArchitectureValidationResult` SHALL be returned

### Requirement: Test adapter throws on failure
`ArchitectureValidationResult.ShouldPass()` SHALL throw `InvalidOperationException` with formatted diagnostic details when validation fails. It SHALL NOT throw when validation passes.

#### Scenario: ShouldPass with passing result
- **WHEN** `ShouldPass()` is called on a result where `Passed` is `true`
- **THEN** no exception SHALL be thrown

#### Scenario: ShouldPass with failing result
- **WHEN** `ShouldPass()` is called on a result where `Passed` is `false`
- **THEN** `InvalidOperationException` SHALL be thrown with messages containing violation and/or cycle details

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
`ArchitectureValidationBuilder.WithUnmatchedIgnoredViolationsPolicy(bool enforce = true)` SHALL control whether the subsequent validation run enforces the policy's `analysis.unmatched_ignored_violations` (`error`/`warn`/`off`) configuration, mirroring `ValidationRequest.EnforceUnmatchedIgnoredViolationsPolicy`. When not called, behavior SHALL remain unchanged from today (not enforced).

#### Scenario: Enforcement toggled on fails the run
- **WHEN** `WithUnmatchedIgnoredViolationsPolicy()` is called on a builder for a policy with `analysis.unmatched_ignored_violations: error` and unmatched ignored violations exist, but no other violations or cycles
- **THEN** `ValidateStrict()` SHALL return `Passed = false`

#### Scenario: Default behavior unchanged when not called
- **WHEN** `WithUnmatchedIgnoredViolationsPolicy()` is never called, against the same policy as above
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

### Requirement: Test adapter exposes baseline comparison outcomes

`ArchitectureValidationBuilder` SHALL provide typed operations for baseline
`diff`, `verify`, and `migrate` comparisons. Each operation SHALL use the same
Core comparison semantics as the corresponding CLI command and return public
comparison entries with structured identity and status suitable for assertions.

#### Scenario: Tests assert a diff status
- **WHEN** a test runs a baseline diff through the Testing adapter against a new
  current finding
- **THEN** the returned comparison outcome contains that entry with status `new`
  and its canonical identity

#### Scenario: Tests assert a verify status
- **WHEN** a test runs baseline verification through the Testing adapter against a
  stale or ambiguous baseline entry
- **THEN** the returned outcome exposes the corresponding structured status and
  the verification gate result

#### Scenario: Tests assert migration status
- **WHEN** a test runs a dry-run baseline migration through the Testing adapter
- **THEN** the returned outcome exposes matched, stale, and ambiguous migration
  entries without writing a baseline file

### Requirement: Testing API policy-check parity
The Testing API SHALL expose the same policy-only completed, deferred, and typed-failure semantics as the CLI without requiring assemblies or project evaluation.

#### Scenario: Testing API validates a decomposed policy
- **WHEN** a test invokes policy-only validation for a valid decomposed policy
- **THEN** it receives completed checks and deferred records equivalent to CLI machine-readable output

### Requirement: Testing API exposes normalized findings
The Testing API SHALL expose normalized findings and baseline lifecycle status directly so test assertions do not parse formatted output.

#### Scenario: Baseline lifecycle assertion
- **WHEN** baseline verification reports a stale entry through the Testing API
- **THEN** the returned normalized finding exposes `baseline_state` of `stale` and its canonical identity

### Requirement: Test adapter exposes the typed architecture-debt gate
The Testing adapter SHALL expose a gate operation using the builder's configured policy and explicit baseline, with optional explicit policy-context artifacts. It SHALL return the typed Core-equivalent gate result so assertions can inspect evaluation, persistent-debt lifecycle, weakening findings, and the final decision without parsing formatted output.

#### Scenario: Adapter observes separate gate causes
- **WHEN** a test executes the gate with a matched baseline finding and an error-severity policy-weakening finding
- **THEN** the returned result distinguishes matched debt from weakening and reports a failed overall gate decision

