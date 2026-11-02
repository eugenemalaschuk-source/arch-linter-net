## Context

See `proposal.md` for the motivation and `specs/analysis-profile/spec.md` for the observable contract. The existing opt-in profile already supplies `ValidationTiming` to the analysis context and reports strict/audit cycle-family time plus whole-process allocations and peak working set. The recorder call is session-owned, and its existing optional work counter already separates deterministic SCC and candidate work. The profile schema is strict (`additionalProperties: false`) and is currently published from the 0.10.0 schema registry.

The accepted first dogfood input is ArchLinterNet's own self-analysis. It is an actual repository workflow but is explicitly not a population-representative consumer sample. No external consumer repositories, private policies, or telemetry are in scope.

## Goals / Non-Goals

**Goals:**

- Collect one bounded numeric observation per cycle recorder invocation only in explicitly profiled runs.
- Preserve the joint `(V,E,B,U,K)` distribution, strict/audit context, empty/disabled denominators, split timings, allocations, and deterministic work counts.
- Let existing profile consumers compare cumulative recorder time with the already-recorded analysis and command boundaries.
- Measure the diagnostic overhead through paired profiled/unprofiled runs and keep process-level memory semantics explicit.

**Non-Goals:**

- Selecting or shipping an adaptive production fallback.
- Adding a CLI/YAML switch, running both algorithms during ordinary analysis, or uploading observations.
- Capturing topology, names, paths, or diagnostic identities in profile observations.
- Treating self-analysis or existing synthetic matrices as evidence of consumer-population frequency.
- Changing the frozen 0.5.1 profile schema or inventing a parallel benchmark framework.

## Decisions

1. **Use an explicit profile observer as the opt-in.** A `ValidationTiming` instance can also be enabled by timing-only requests, so the profile hosts create one through `ValidationTiming.CreateForAnalysisProfile()` only for CLI `--profile` / Testing `WithProfile()`. At `ArchitectureAnalysisSession.AddCycleBaselineCandidates`, create the optional recorder work counter and capture an observation only when that profile observer is active. Keep ordinary and timing-only call paths free of new counters, timers, graph scans, or allocations.

2. **Record in a dedicated measurements section.** Add `Measurements.CycleRecorder` with a summary and an ordered list of per-invocation numeric observations. Use the execution context's bounded strict/audit contract group, not arbitrary contract IDs. Include `V`, `E`, `B`, `U`, and `K`; one high-resolution total duration; separate SCC-index-build and candidate-evaluation durations; per-call allocated bytes; and existing SCC/recorder work counts. Keep the complete-analysis and command timing boundaries in their existing profile phases. Cycle contract execution is sequential, so cumulative recorder elapsed is the sum of non-overlapping invocation intervals.

3. **Keep profiler bookkeeping outside the measured recorder interval.** Measure `_cycleBaselineCandidateRecorder.Record` separately from the extra profile bookkeeping that counts graph edges and unique evidence pairs. Store the latter as instrumentation overhead. The recorder interval still includes the optional deterministic work counter; quantify its total run-level impact by paired profiled/unprofiled measurements rather than presenting instrumented timings as uninstrumented costs.

4. **Use schema compatibility by release line.** Add the extended `analysis-profile/v1` schema to the 0.10.0 compatibility manifest and packaged registry. Preserve the existing 0.5.1 schema resource unchanged. Keep the new JSON members optional so consumers that do not inspect cycle profiling remain unaffected; run generated-profile validation against the current registry schema.

5. **Separate memory observations by boundary.** Per-invocation allocated bytes use the existing thread-local allocation counter around the recorder. Do not report per-invocation retained/peak memory as if it were attributable: retain existing process-level peak working set and total allocation in `Measurements`, and retain the explicit benchmark's documented post-GC retained delta / process peak-working-set deltas for direct calls.

6. **Use existing workload/evidence infrastructure.** Extend the #1125 direct paired harness where required and reuse the #502/#1123 profile, fixture, output-digest, and evidence conventions. First capture the approved ArchLinterNet self-analysis as internal dogfood, then keep any synthetic stress shapes in a separate report section. Compare exact outputs before interpreting time. Leave the fallback decision unresolved until the corpus and issue-specific materiality gate support a choice.

## Risks / Trade-offs

- **Profile overhead can distort a small recorder interval** → expose bookkeeping overhead and compare full analysis with profiling on/off; label instrumented recorder timings and never infer whole-analysis effect from them alone.
- **Per-invocation profile arrays can grow with policy cycle-contract count** → retain one compact record per recorder invocation with numeric fields only; do not retain evidence objects, strings, graph nodes, or per-evidence detail.
- **The initial approved corpus has one internal repository** → mark every prevalence statement as self-analysis-only and record any missing consumer workload as insufficient evidence rather than a negative result.
- **The v1 schema rejects unknown fields** → publish the updated strict schema in the current 0.10.0 manifest, leave the historical 0.5.1 resource untouched, and validate packaged registry output.

## Migration Plan

The profile extension is additive and opt-in. Existing unprofiled analysis and the 0.5.1 schema remain unchanged. Current 0.10.0 profile readers can ignore the optional `Measurements.CycleRecorder` member; consumers validating strict JSON should use the schema version shipped in the same package. No persistent state or rollback migration is needed.
