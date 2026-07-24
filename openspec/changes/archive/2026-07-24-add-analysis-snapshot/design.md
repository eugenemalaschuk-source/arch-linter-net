## Context

`ArchitectureValidationApplicationService.Validate(ValidationRequest)` currently does, per call: load/merge policy YAML, resolve condition set, discover projects, resolve/load target assemblies, construct one `ArchitectureAnalysisContext` and one `ArchitectureContractRunner` (which owns one `ArchitectureAnalysisSession` with its lazy type/role/source-file indexes — see `openspec/specs/analysis-session-indexes/spec.md`), run build-state preflight, then execute contracts for exactly one mode (`strict` or `audit`). Coverage is not a separate mode; `strict_coverage`/`audit_coverage` contract families already execute inside whichever mode is running, driven by `IArchitectureContractExecutor.Execute(session, mode, ...)`.

`ArchitectureRunnerSetupService.BuildRunner` already accepts an optional `mode` used only to decide whether assembly resolution can be skipped (`ShouldResolveAssemblyOutputs`); when `mode` is `null`, it unions strict+audit contracts for that decision, i.e. it already produces a runner/session usable for both modes. `IArchitectureContractExecutor.Execute` takes `mode` per call and reads `session.Catalog.ContractsFor(mode, family)` — mode selection happens at execution time against one session, not at session construction time. This means one composed session can already serve multiple mode evaluations; the missing piece is an explicit object that owns that session and exposes multi-mode evaluation instead of every caller invoking `Validate` once per mode.

The analysis-build-state blueprint (`docs/internal/analysis-build-state-blueprint.md`, "Snapshot ownership" section) assigns exactly this slice to #363: "One immutable completed snapshot, session identity publication, ownership/disposal/reuse." Full fingerprint/digest/receipt machinery is already implemented (#362/#387/#388) and is out of scope here; this change adds the snapshot object itself on top of that existing foundation.

## Goals / Non-Goals

**Goals**
- One composed policy + one project-graph evaluation + one assembly load serves every requested `strict`/`audit` view (coverage included) for one session.
- Existing single-mode `Validate` behavior, results, and performance are unchanged (implemented on top of the new primitive, not alongside it).
- Explicit, simple ownership/disposal model for CLI (one snapshot per command) and Testing API (opt-in shared snapshot across assertions).
- Typed counters (`ArchitectureAnalysisSnapshotCounters`) record composition/evaluation counts without printing infrastructure.
- Invalid policy, invalid build state (preflight blocked), or a disposed snapshot are all non-reusable failure states — no partial success.

**Non-Goals**
- Persistent cross-process cache (#365).
- Multi-file output routing.
- Changed-file-only strict validation.
- Full profiling/timing counters (#374) — only minimal typed composition/evaluation counters are added here.
- Cancellation-token plumbing (#375).
- New fingerprint/digest/receipt types — reuse what #362/#387/#388 already provide.

## Decisions

### Decision: `AnalysisSnapshotRequest` is `ValidationRequest` minus `Mode`
`ValidationRequest` bundles per-mode concerns (`Mode`) with session-level concerns (policy path, condition set, preprocessor symbols, contract-id filter, baseline, preparation mode, etc.). `AnalysisSnapshotRequest` carries only the session-level fields and exposes `ForMode(string mode)` to produce a `ValidationRequest` for the existing single-mode APIs, so `ValidationRequest` itself does not change and nothing depending on it breaks.

Alternative considered: reuse `ValidationRequest` with a nullable `Mode` and a separate `Modes` list. Rejected — it weakens the existing required-`Mode` single-mode contract and complicates `Validate`'s existing validation of `request.Mode`.

### Decision: `ArchitectureAnalysisSnapshot` owns setup once; `Evaluate(mode)` is memoized
`CreateSnapshot` runs exactly the composition/discovery/resolution/session-construction/preflight sequence `LoadAndSetup` + `RunBuildStatePreflight` already perform today (unchanged logic, just extracted so it runs once and is retained on the snapshot instead of being recomputed). `Evaluate(mode)` runs the per-mode sequence (configuration check, policy-consistency check, contract execution, unmatched-ignored resolution, coverage filtering, classification checks) — extracted verbatim from `Validate`'s current mode-specific tail — and caches the `ValidationOutcome` per mode so a repeated `Evaluate("strict")` on the same snapshot does not re-run contract execution.

`Validate(ValidationRequest)` becomes `using var snapshot = CreateSnapshot(AnalysisSnapshotRequest.From(request)); return snapshot.Evaluate(request.Mode, timing);` — the existing `--ensure-built` re-setup-after-build behavior stays inside `CreateSnapshot` (it's part of one session's setup, not mode-specific), so single-mode timing and results are unchanged.

### Decision: blocked preflight snapshots short-circuit every mode
When build-state preflight blocks (as today), `CreateSnapshot` stores the blocked `BuildStatePreflightResult` and `Evaluate(mode)` returns the same blocked `ValidationOutcome` shape the current code returns for any requested mode, without touching contract execution — matching the "invalid build state is a failed session" requirement without introducing new failure types.

### Decision: `IDisposable` with a disposed flag, no unmanaged resources
Nothing the snapshot owns today (session, indexes, runner) implements `IDisposable` itself. `ArchitectureAnalysisSnapshot.Dispose()` sets an internal flag and clears cached outcomes; `Evaluate` after disposal throws `ObjectDisposedException`. This matches the `ArchitectureEngine` precedent (`src/ArchLinterNet.Core/Composition/ArchitectureEngine.cs`) of implementing `IDisposable` for explicit lifetime even when the underlying resources are managed-only, and keeps the door open for future unmanaged/pooled resources without a breaking API change.

### Decision: Testing API adds an explicit shared-snapshot entry point, existing methods unchanged
`ArchitectureValidationBuilder.ValidateStrict()`/`ValidateAudit()` keep their current independent-run behavior (backward compatible — the issue's "ordinary single-mode validation stays simple" requirement). A new method returns a small disposable wrapper bound to one `ArchitectureAnalysisSnapshot`, exposing `ValidateStrict()`/`ValidateAudit()` that evaluate against the shared snapshot. Callers who want the sharing benefit opt in explicitly with a `using` block; callers who don't, pay no API-surface cost.

### Decision: CLI accepts a comma-separated `--mode` list
`ValidateCommandHandler`'s mode check currently rejects anything other than exactly `"strict"` or `"audit"`. It is extended to split on `,`, validate each entry against the same allowed set, build one snapshot, and evaluate/report each mode in the given order, combining exit-code failure across all requested modes. Single `--mode strict` (or `--mode audit`) keeps its current single-value behavior and output exactly as today.

## Risks / Trade-offs

- **Risk:** extracting the per-mode tail of `Validate` into a reusable method could subtly change control flow (e.g. which `document`/`runner` instance is read after the `--ensure-built` re-setup). Mitigation: the extraction is verbatim — the same local variables (`loadAndSetup`, `runner`, configs) are captured by the snapshot at the end of `CreateSnapshot`, and `Evaluate` reads only from the snapshot's stored setup, never re-derives it.
- **Risk:** file-size lint (`ArchitectureAnalysisSession.cs` main file is already 795/800 lines; several partials are near 800). Mitigation: all new logic lives in new files (`ArchitectureAnalysisSnapshot.cs`, `AnalysisSnapshotRequest.cs`, `ArchitectureAnalysisSnapshotCounters.cs`); no session partial grows.
- **Trade-off:** counters are intentionally minimal (composition/evaluation counts only, no timings) — full profiling is explicitly deferred to #374 per the blueprint's downstream map, so counters here are typed but coarse.

## Migration Plan

Purely additive at the public-API level (`IArchitectureValidationApplicationService.CreateSnapshot` is a new member; `Validate` keeps its exact signature and behavior). No consumer changes are required. CLI `--mode` gains new accepted syntax (comma-separated) without changing the meaning of existing single-value invocations.

## Open Questions

None — scope is fixed to the #363 slice per `docs/internal/analysis-build-state-blueprint.md`'s downstream implementation map.
