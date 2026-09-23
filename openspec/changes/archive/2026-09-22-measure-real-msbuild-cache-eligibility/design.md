## Context

The existing `DeferredHotPathBenchmarkHarness` already proves that #502 can materialize synthetic staged and real-MSBuild workloads, run the CLI with `analysis-profile/v1`, and compare canonical result identities. Its cache probe only records an ineligible population and a repeat attempt, however, and its evidence model is owned by #655. The existing `analysis-cache/v1` contract intentionally keeps ordinary real-MSBuild manifests fail-closed, while #991 still gates any new Core optimization.

This change therefore adds a separate explicit benchmark/evidence seam. It must be runnable manually on a built CLI, must not execute in normal test/acceptance runs, and must write only synthetic/anonymized evidence under `docs/internal`.

## Goals / Non-Goals

**Goals:**

- Reuse #502's workload generator/materializer for real-MSBuild small/medium/large fixtures.
- Measure disabled, cache-enabled cold/miss/population, and equivalent repeat modes while retaining typed eligibility reasons, cache counters, phase timings, deterministic work counters, resource observations, and canonical output identity.
- Measure a separately labelled eligibility-control path where the existing cache contract permits it; if artifact authorization keeps that control unavailable, retain that fact and use the measured targeted phase only as a conservative upper bound, never pretending an ineligible real project hit.
- Record the pre-implementation effect estimate and derive its local/end-to-end upper bound, amortized reuse assumptions, break-even, success threshold, and kill criterion from explicit data.
- Record the exact-request reference/base-side measurement disposition separately from candidate prepared-state reuse.
- Make the A/B/C decision guardrails executable and fail the evidence test when a report claims an outcome without satisfying the issue's evidence contract or when #991 is not acknowledged as a Phase 2 gate.

**Non-Goals:**

- No change to `EvaluatedBuildInputManifestCollector`, `VerifiedCacheEligible`, cache keys, cache storage, CLI behavior, or public APIs.
- No real-MSBuild eligibility expansion before the #991 gate and normalized remeasurement.
- No prepared-analysis persistence, remote/shared cache, changed-file authority, or duplicate #502 benchmark corpus.
- No wall-clock-only performance claim or private adopter evidence.

## Decisions

1. **Add a dedicated #675 harness and evidence model instead of extending #655's report.** The current cache probe is useful routing evidence but its schema is designed for deferred hot paths and cannot express effect estimates, gate status, reference-side disposition, or an issue-level outcome without making #655 own #675. A dedicated explicit fixture keeps ownership and report semantics clear.

2. **Use deterministic counters as the decision basis and Stopwatch timings as labelled supporting evidence.** The report will distinguish preparation/authorization overhead from mode-specific work that a verified hit could avoid. Amdahl's upper bound is calculated from the measured targeted phase share; measured wall time is environment-labelled and never presented as a hardware-independent guarantee.

3. **Represent real-MSBuild and eligible-control observations in one report with an explicit eligibility boundary.** Real-MSBuild rows must show the actual `CacheIneligible` outcome and sorted reasons. Eligible-control rows are used only to calibrate the existing verified-hit path and are never counted as real-MSBuild eligibility success.

4. **Treat the decision as a typed A/B/C result with a conservative default.** The harness can record A only when the normalized-gate prerequisite, complete effect estimate, canonical equivalence, stale-input rejection, and material/amortized threshold are all demonstrated. Otherwise it records B or C with a routing explanation and no Phase 2 implementation claim. Current #991 state is retained in the report metadata.

5. **Keep reference/base-side exact-request reuse separate from candidate prepared-state reuse.** The report has an explicit disposition field and measurement rows for repeated immutable reference/base requests. Its formulas exclude any work already attributed to #492/#493 and cannot silently count candidate prepared-state savings twice.

6. **Validate the evidence contract with ordinary NUnit tests; keep execution manual.** Small model/guardrail tests run in the affected Core test project. The hardware-sensitive matrix remains `[Explicit]` and writes the checked-in evidence only when a maintainer intentionally runs it.

## Risks / Trade-offs

- **[Risk]** Real-MSBuild build and evaluation cost makes the matrix slow or unstable on developer machines. → Keep the harness explicit, bound fixture sizes/timeouts, use deterministic counters for decisions, and label environment/timing completeness.
- **[Risk]** An eligible staged control could be mistaken for a real-MSBuild hit. → Require a `real-msbuild`/`eligible-control` boundary in every row and render the distinction in the report.
- **[Risk]** Cache authorization changes during the run. → Capture initial and completion manifests/receipts through the existing CLI path, compare canonical results, and treat missing or changed evidence as a failed/invalid measurement rather than a success.
- **[Risk]** The pre-normalization consumer evidence overstates product value. → Record #991 as an explicit gate, rerun the matrix after normalization before any Phase 2 conclusion, and preserve the kill criterion.
- **[Risk]** Resource measurements are unavailable on a host. → Record explicit unavailable reasons and prevent a complete effect claim from silently substituting zeros.
