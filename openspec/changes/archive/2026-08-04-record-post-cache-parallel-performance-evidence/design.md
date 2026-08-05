## Context

The repository already has a manual `AnalysisProfileBenchmarkHarness`, the
synthetic ten-project `large-multi-host` corpus, and checked-in #374
pre-optimization evidence. `analysis-cache/v1` and bounded parallel scanning
are now implemented and expose the cache and concurrency counters that #409
must exercise. The benchmark remains explicitly excluded from ordinary CI
because real subprocess timings are hardware-sensitive.

## Goals / Non-Goals

**Goals:**

- Reuse the profile envelope, phase boundaries, fixture shape, statistic
  calculation, and sample-validation rules from the pre-optimization harness.
- Record ten successful samples for every applicable timing scenario and retain
  complete raw profiles together with deterministic comparison summaries.
- Prove required semantic invariants from canonical result projections and
  profile counters before interpreting timing data.
- Make the final report independently consumable by release documentation.

**Non-Goals:**

- Change cache authorization, scheduling, CLI options, profile schema shape,
  or normal correctness acceptance thresholds.
- Claim a speedup independent of the declared reference hardware and build.
- Turn the hardware-sensitive matrix into a mandatory CI test.

## Decisions

### A dedicated post-optimization harness reuses the #374 measurement model

The post harness will invoke the real CLI in isolated fixture copies and
produce a separate checked-in result document. It will share the #374 boundary
definitions: preflight is reported separately, analysis excludes preflight and
output phases, and command total retains all work. This prevents a convenient
but incomparable pre/post measurement change.

Alternatives considered:

- Hand-time commands or copy table values: rejected because phase attribution,
  completion checks, and raw profiles would not be reproducible.
- Change the pre-optimization artifact in place: rejected because it destroys
  the explicit historical baseline.

### The cache matrix uses a temporary explicit cache path

Each cache scenario will own a temporary cache root. It will record disabled,
first-population, and verified-warm-hit behavior without using a user cache or
leaking an absolute path into artifacts. A post-run profile must show a hit and
positive avoided work before it is classified as a warm-hit sample.

Alternatives considered:

- `--cache auto`: rejected because it depends on external user state and is
  harder to clean up deterministically.

### Correctness comparisons are evidence gates, not timing inferences

The harness will compare canonical findings and their order between cached and
uncached runs and between `--max-parallelism 1` and bounded parallel runs. It
will assert one-session counters for combined strict/audit and render-only
counter changes for extra sinks. Cancellation/failure exercises will retain
cleanup/counter evidence but are never included in timing summaries.

### Evidence has a raw artifact and a concise reference report

The machine-readable post artifact will retain profiles, environment metadata,
scenario configuration, and deterministic invariant evidence. The Markdown
report will compare the matching #374 baseline and post rows, disclose the
reference environment and commit identity, and explicitly limit performance
claims to that measurement.

## Risks / Trade-offs

- [Long local execution] → Keep the matrix `Explicit` and document its exact
  command; ordinary acceptance remains fast and deterministic.
- [Cache hit is not achieved due to a regression] → Fail the benchmark instead
  of silently publishing a non-hit as performance evidence.
- [Environment changes invalidate direct timing comparison] → Capture OS,
  hardware, runtime, configuration, harness/source identity, and state the
  comparison limitation in the report.
- [Platform metrics are unavailable] → Preserve the profile's explicit null or
  availability status rather than substituting zero.

## Migration Plan

1. Add focused tests for evidence validation and scenario configuration.
2. Run the manual post matrix from a clean, built checkout and commit its JSON
   artifact and Markdown summary without altering #374 evidence.
3. Archive the OpenSpec change; no runtime migration or rollback is needed.

## Open Questions

- None. The exact schema and phase semantics are owned by the existing
  `analysis-profile/v1` contract.
