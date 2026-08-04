## Why

`analysis-profile/v1` has a checked-in pre-optimization baseline, while the
verified cache and bounded parallel scanning changes now lack the comparable
post-optimization evidence required before the 0.5.1 release gate. Release
documentation needs reproducible reference measurements and deterministic
correctness evidence without presenting local timings as a universal contract.

## What Changes

- Reuse the synthetic `large-multi-host` benchmark harness and its
  `analysis-profile/v1` phase boundaries for the required post-cache and
  post-parallel matrix.
- Record at least ten valid samples per applicable scenario, including raw or
  deterministic summarized machine-readable data and declared environment,
  source identity, and configuration.
- Add deterministic evidence for cache equivalence and avoided work,
  sequential/parallel equivalence and bounded concurrency, one-session
  strict/audit execution, and output-sink isolation.
- Publish a pre/post report which separates preparation from analysis and
  limits all timing claims to the measured reference environment.
- Exclude cancelled, partial, and failed executions from successful timing
  samples while preserving their counter and cleanup evidence.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-profile`: Require reproducible final post-cache/post-parallel
  release evidence using the established profile contract and benchmark
  scenario semantics.

## Impact

- Benchmark harness and its NUnit coverage in `ArchLinterNet.Core.Tests`.
- Checked-in internal release-evidence Markdown and machine-readable dataset.
- `analysis-profile/v1` OpenSpec and its phase/counter documentation.
- No public runtime API, cache authorization model, or parallel scheduling
  semantics change.
