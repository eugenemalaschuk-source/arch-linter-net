## Why

Issue #363 (one immutable snapshot) and #364 (multi-sink output) shipped minimal typed counters and a human-only `--timings` text report, but there is still no machine-readable, versioned contract that downstream cache (#365) and parallel-scanning (#408) work can measure against, and no repeatable pre-optimization baseline. `ArchitectureAnalysisSnapshotCounters` and `openspec/specs/cli-timing/spec.md` both explicitly defer "full profiling/timing counters" and any machine-readable shape to this issue. Without it, #365/#408 would have to invent ad hoc measurement and #409 would have nothing stable to diff post-optimization results against.

## What Changes

- Add a new `analysis-profile/v1` model in `ArchLinterNet.Core` that extends the existing `ArchitectureAnalysisSnapshotCounters`/`ValidationTiming` data with a full deterministic-counter set (policy composition, project evaluation, assembly loads, contract-family executions, render/output sink counts, fact-index materializations) plus optional environment-dependent measurements (elapsed/processor time per phase, peak working set, allocated bytes), a typed completion status (`Success`/`ValidationFailure`/`PreparationFailure`/`Cancelled`), and explicit reserved-not-applicable cache/concurrency fields for #365/#408 to fill in later.
- Add a CLI `--profile <stdout|stderr|file-path>` output option on `validate` (sibling to the existing `--timings` flag) that renders the profile as deterministic JSON, and mirror the same mechanism in `ArchLinterNet.Testing` (`ArchitectureValidationBuilder.WithProfile()` / `ArchitectureValidationResult.Profile`) so tests and library consumers get identical semantics to the CLI.
- Add a JSON Schema for the profile document and a test that validates a real generated profile against it, without registering the schema in the immutable packaged-schema registry (`schema/0.5.1/compatibility-manifest.json`) — registration is explicitly owned by #410 per `openspec/specs/packaged-schema-registry/spec.md`.
- Extend the existing `#403` AdoptionAcceptance corpus (`tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/`) with a new synthetic large multi-host fixture, per the corpus's own extension rule naming #374 as a reuser.
- Add a repeatable, non-gated benchmark harness that runs the required scenario matrix at least ten times each against the large multi-host fixture, separates restore/build time from analysis time, and computes median/p95 elapsed time.
- Check in the resulting pre-optimization baseline evidence (with environment metadata) under `docs/internal/`, mirroring the existing `checkpoint-a-evidence.md` shape, and a documented phase/counter dictionary for #409 to diff against.

## Capabilities

### New Capabilities
- `analysis-profile`: versioned, machine-readable `analysis-profile/v1` contract (model, builder, CLI/Testing exposure, schema, and the documented phase/counter dictionary) built on top of the existing `analysis-snapshot` counters and `cli-timing` phase measurements.

### Modified Capabilities
(none — `analysis-snapshot`, `cli-timing`, `multi-sink-output`, and `cooperative-cancellation` are extended by composition, not by changing their existing requirements; `adoption-acceptance-corpus` gains a fixture entry under its already-documented extension rule, not a requirement change)

## Impact

- New code under `src/ArchLinterNet.Core/Profiling/`, a new `--profile` option in `src/ArchLinterNet.Cli/Commands/Validate/`, and a `WithProfile()`/`Profile` addition in `src/ArchLinterNet.Testing/`.
- New JSON Schema file `schema/0.5.1/analysis-profile.schema.json` (not registered in the packaged manifest yet).
- New fixture under `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/` and an updated `CheckpointAScenarioManifest.json`.
- New benchmark harness and checked-in evidence docs under `docs/internal/`.
- No breaking changes to existing `--timings`, `--report`, or exit-code behavior; profile output is purely additive and opt-in.
