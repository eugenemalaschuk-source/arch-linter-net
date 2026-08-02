## Context

`ArchitectureAnalysisSnapshotCounters` (`src/ArchLinterNet.Core/Validation/ArchitectureAnalysisSnapshotCounters.cs`) already tracks `PolicyCompositions`, `ProjectGraphEvaluations`, `AssemblyLoads`, `ModesEvaluated` for #363, and its own doc comment defers "full profiling/timing counters" to this issue. `ValidationTiming` (`src/ArchLinterNet.Core/Reporting/ValidationTiming.cs`) already measures phase durations and per-contract-family counts, but only renders a human text table via `--timings`, gated by `openspec/specs/cli-timing/spec.md`, with no JSON/SARIF shape. `ReportCoordinator` (`src/ArchLinterNet.Cli/Commands/Validate/ReportCoordinator.cs`) already proves the #364 "one analysis, N sinks" invariant operationally but does not expose sink counts anywhere machine-readable. Three other specs already forward-reference this issue by number: `analysis-snapshot`, `cli-timing`, `cooperative-cancellation`, and `packaged-schema-registry`.

## Goals / Non-Goals

**Goals:**
- One versioned (`analysis-profile/v1`), machine-readable model that a human/JSON renderer, the CLI, and the Testing API all share.
- Deterministic counters kept strictly separate from environment-dependent measurements (never mixed in a way that could make timing affect identity).
- Explicit reserved fields for cache (#365) and concurrency (#408) so they can add real values later without renaming or restructuring the schema.
- A typed completion/cancellation status field, without taking on #418's atomic-publication work for the profile artifact itself.
- A real, repeatable benchmark harness and real (not fabricated) pre-optimization evidence, checked in.

**Non-Goals:**
- Implementing persistent cache or parallel scanning (#365/#408) — only reserving fields for them.
- Cancellation-safe atomic publication of the profile artifact itself (#418).
- Registering the new schema in `schema/0.5.1/compatibility-manifest.json` (#410).
- Post-optimization evidence collection (#409) or hardware-independent duration SLAs.

## Decisions

**Model location and shape.** Add `src/ArchLinterNet.Core/Profiling/` with `AnalysisProfileId` (mirrors `CelProfileId`'s versioned-string pattern), `AnalysisProfile` (top-level record: `SchemaId`, `CompletionStatus`, `CancellationObserved`, `Counters`, `Phases`, `Measurements`), `AnalysisProfileCounters` (deterministic — extends the existing snapshot counters plus contract-family/render/output/fact-index counts, plus reserved `Cache`/`Concurrency` sub-records whose fields are `0`/`NotApplicable` today), `AnalysisProfilePhaseMeasurement` (deterministic `Name`/`Indent`/`Ordinal`/`Count` plus nullable environment `ElapsedMs`), and `AnalysisProfileMeasurements` (nullable `PeakWorkingSetBytes`/`AllocatedBytesTotal`). Building it in Core (not Cli) is what lets `ArchLinterNet.Testing` share identical semantics, per the architecture rule that CLI and Testing depend only on Core.
- *Alternative rejected*: putting the model in Cli and having Testing duplicate it — rejected because it would violate "CLI and Testing depend only on Core" and risks the two drifting.

**Builder, not a bigger `ValidationTiming`.** Add `AnalysisProfileBuilder` in Core that assembles an `AnalysisProfile` from an existing `ValidationTiming?`, `ArchitectureAnalysisSnapshotCounters`, sink render/output counts supplied by the caller, and a completion/cancellation status supplied by the caller. `ValidationTiming` itself is not changed — it keeps rendering its existing human report unchanged (protects `cli-timing`'s existing guaranteed shape).
- *Alternative rejected*: extending `ValidationTiming.WriteReport` to also emit JSON — rejected because it would couple the profile schema's evolution to the timing-report's internal `Entry` shape and blur "deterministic counters vs. environment measurement" inside one class.

**CLI surface: a sibling flag, not a new `--report` format.** Add `--profile <stdout|stderr|<file-path>>` as its own option on `validate`, analogous to `--timings`, rather than adding `"profile"` as a new value to the `--report <format>=<destination>` sink list. This keeps `multi-sink-output`'s enumerated format set and spec untouched, and keeps this change's blast radius local to the new capability. The profile still *reports on* sink counts (it observes how many sinks `ReportCoordinator` rendered/wrote), it just isn't routed through `ReportCoordinator` as a sink itself.
- *Alternative rejected*: adding profile as a `--report` sink format — rejected because it would require editing `multi-sink-output/spec.md`'s normative format enumeration for a feature that has no file-overwrite-protection or partial-output semantics of its own yet (those come with #418).

**No atomic-publication machinery for the profile file in this change.** When `--profile <file-path>` is used, the file is written directly (open, write, close) — not staged-then-renamed like `ReportCoordinator`'s file sinks. `cooperative-cancellation/spec.md` already documents, by name, that #374 has no implementation surface for it to add cancellation checks to yet, and that extending coverage once the surface exists is #418. Building atomic publication here would silently absorb #418's scope contrary to the "do not hide unfinished issue scope" rule — instead, `CompletionStatus.Cancelled` records that cancellation was *observed*, without claiming safe-publication guarantees #374 was never asked to build.

**Testing API mirror.** Add `ArchitectureValidationBuilder.WithProfile()` (enables profile collection, implies timing collection internally) and `ArchitectureValidationResult.Profile`, following the existing `WithTimings()`/`.Timing` pattern exactly.

**Schema without registry registration.** Add `schema/0.5.1/analysis-profile.schema.json` and a test (`AnalysisProfileSchemaValidationTests`) that runs a real `validate --profile` against a real fixture and validates the JSON output against the schema file directly (not via `PackagedSchemaRegistry`). `packaged-schema-registry/spec.md` line 7 is explicit that analysis-profile schemas are not published in the immutable registry until #410; this change satisfies the "real generated-profile validation suitable for packaging by #410" acceptance criterion without touching the registry manifest.

**Corpus extension, not a new fixture system.** Add one new fixture, `large-multi-host`, under `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/`, and one entry in `CheckpointAScenarioManifest.json`'s fixture list, per the corpus's own documented extension rule (`docs/internal/adoption-acceptance-corpus.md`, "Extension rule"). Sized at 8 synthetic host projects (versus the existing 2-host `same-named-multi-host` fixture) plus 2 shared library projects, which is enough to produce a measurable, non-trivial policy-composition/project-evaluation workload without ballooning `dotnet build` time in CI-adjacent runs.

**Benchmark harness kept out of the correctness gate.** A new NUnit fixture, `AnalysisProfileBenchmarkHarness`, under `tests/ArchLinterNet.Core.Tests/AnalysisProfile/`, tagged `[Explicit]` (matching the existing pattern of excluding hardware-sensitive suites from `make test`/`make acceptance`, as `benchmarks/ArchLinterNet.CEL.Benchmarks` already does for CEL). It shells out to the built CLI (`dotnet <cli-dll> validate ...`) against the `large-multi-host` fixture for each of the 7 scenarios named in the issue, 10 runs each, computing median/p95 wall time and separating a `dotnet build` (restore/build) phase from the `validate` (analysis) phase. Running it once, by hand, on this development machine produces the checked-in evidence doc; it is not re-run automatically by CI, matching "correctness tests gate deterministic counters and invariants, never hardware-specific duration limits."
- *Alternative considered*: a BenchmarkDotNet project sibling to `benchmarks/ArchLinterNet.CEL.Benchmarks`. Rejected for this change because BenchmarkDotNet's job model measures steady-state in-process throughput, not the issue's explicit "cold process," "separate legacy-style processes," and "restore/build vs. analysis time" scenarios, which are inherently process-level.

**Evidence doc.** `docs/internal/analysis-profile-pre-optimization-baseline.md`, mirroring `docs/internal/checkpoint-a-evidence.md`'s shape (scope, observed environment, scenarios exercised, non-release statement) plus the required median/p95 table per scenario.

**Phase/counter dictionary.** `docs/internal/analysis-profile-dictionary.md` documents every phase name and counter's semantics as a stability contract for #409's post-optimization diff.

## Risks / Trade-offs

- [Risk] A new `--profile` flag could be seen as redundant with `--timings`. → Mitigation: `--timings` stays human/stderr-only and unchanged; `--profile` is the machine-readable superset used by tooling, and the design doc records this so a future reader doesn't try to merge them without another issue-scoped change.
- [Risk] Hand-run benchmark evidence on one developer machine is not statistically strong. → Mitigation: the evidence doc explicitly disclaims universal applicability (matching `checkpoint-a-evidence.md`'s existing "non-release statement" pattern) and states the exact observed environment.
- [Risk] Adding 8 synthetic host projects could slow down `dotnet build` for anyone who builds the whole fixture tree. → Mitigation: the fixture lives under `AdoptionAcceptance/Fixtures/large-multi-host/` and is only built by the new `[Explicit]` harness and its own acceptance test, not by the main solution build.

## Migration Plan

Purely additive: new files, new opt-in CLI flag, new opt-in Testing API method. No existing command, flag, exit code, or JSON/SARIF shape changes. No rollback beyond reverting the change.

## Open Questions

None blocking — remaining detail (exact JSON field names) is finalized in the spec and implementation.
