## Context

Three prerequisite contracts already exist and must be consumed, not redefined:

- `analysis-build-state-fingerprints/spec.md`'s "Evaluated build-input manifest has an explicit cache-authorization outcome" requirement defines `EvaluatedBuildInputManifestV1`/`CacheEligibility` (`src/ArchLinterNet.Core/BuildState/EvaluatedBuildInputManifestV1.cs`). Its collector is deliberately fail-closed: it always adds the `"evaluated-msbuild-evidence-incomplete"` reason and therefore always returns `CacheIneligible` for real MSBuild evidence today (see `openspec/changes/archive/2026-08-03-harden-evaluated-manifest-review/design.md`, "more projects are ineligible until a safe evaluated-MSBuild collector exists → this is intentional").
- `analysis-profile/spec.md`'s "Cache and concurrency fields are explicitly reserved" requirement already ships `AnalysisProfileCacheCounters` with `Status`/`Lookups`/`Hits`, explicitly stating these "SHALL use names and shapes stable enough for #365 ... to populate with real values later without renaming or restructuring them."
- `cooperative-cancellation`/#375's cancellation-safe I/O and last-parameter `CancellationToken` convention (see commit 1e15e67) is the pattern this change's `AnalysisCacheStore.Put` follows for staged-write-then-cancel-check-then-publish.

## Goals / Non-Goals

**Goals:**
- A real, secure, tested `analysis-cache/v1` storage/authorization engine: canonical envelope, content-digest integrity, no polymorphic deserialization, atomic per-entry publish, typed miss/reject reasons, safe `Inspect`/`Clear`.
- Disabled-by-default `--cache`/`WithCache()` configuration with `auto`/explicit-path resolution and containment/safety validation, shared between CLI and Testing via one Core implementation.
- Real `analysis-profile/v1` instrumentation for whatever the cache actually did this run.
- Honest test coverage of hit/miss/reject/corruption/cancellation/path-safety, including proof that today's real `EvaluatedBuildInputManifestCollector` output is always ineligible (so population never persists unproven facts).

**Non-Goals (explicitly deferred, not silently dropped):**
- Wiring a cache hit into `ValidateCommandHandler`/`ArchitectureValidationBuilder` to skip project evaluation, source scanning, or contract execution on a second run. See "Deferred: live pipeline short-circuit" below.
- Improving `EvaluatedBuildInputManifestCollector` to ever report `VerifiedCacheEligible` for real MSBuild evidence — that is #406's own explicitly-scoped future work, not this change's.
- Caching full `ValidationOutcome`/`ArchitectureViolation` finding detail. `ArchitectureViolation.Payload` is `IArchitectureDiagnosticPayload`, a closed but currently-unenumerated-for-JSON polymorphic interface with ~17 concrete payload records; safely round-tripping it would need an explicit closed-set `[JsonDerivedType]`-style converter enumerating all of them, which is real work belonging to whichever change first needs cached finding detail (see "Cache boundary decision" below).
- Distributed/remote cache, replacing MSBuild/compiler caches, requiring cache for correctness, parallel scanning (#408), post-optimization evidence (#409).

## Decisions

**Cache boundary: project/output facts, not final findings.** The issue's own "Cache boundary" section lists, in order, "verified project/output facts," "assembly metadata/type/IL/source indexes," "deterministic project/reference metadata," and "reusable normalized fact sets ... independent of report rendering," and separately warns "Do not cache a bare final `passed` result as authority." This change caches `AnalysisCacheProjectManifest` (the #406 manifest digest + eligibility, per project/context) as the reuse-authorization unit, plus a small `AnalysisCacheFactsV1` record of deterministic *counts* (violation/coverage/cycle/etc. counts, `Passed`) — never the polymorphic violation objects themselves. This is the smallest boundary the issue names that this change can implement safely end-to-end without inventing new (de)serialization surface for `IArchitectureDiagnosticPayload`.
- *Alternative rejected*: caching the full `ValidationOutcome`. Rejected for this change because of the polymorphic `Payload` risk above — attempting it under time pressure risked either an unsafe shortcut (a `$type`-discriminated converter resolving arbitrary types by name, which the issue explicitly forbids: "never deserialize arbitrary CLR/runtime types") or an incomplete/untested closed-set converter shipped without full confidence. A future change that actually needs cached finding detail should add a deliberate, tested, closed-set `[JsonDerivedType]` converter for the ~17 payload records as its own reviewed decision.

**Deferred: live pipeline short-circuit.** `ArchitectureValidationApplicationService.ValidateWithCounters`/`CreateSnapshot` (`src/ArchLinterNet.Core/Validation/`) do not currently expose a seam to accept a pre-computed outcome instead of running policy composition → project evaluation → assembly loading → source scanning → contract execution. Building that seam safely (so a hit reproduces byte-identical findings/ordering/exit-category to an uncached run, per the issue's acceptance criteria) is itself a nontrivial, reviewable change to `Core.Validation`/`Core.Execution` internals. This change instead:
1. Always runs the real pipeline unchanged (cache never alters correctness — satisfies "Requiring cache for correctness" as an explicit non-goal, trivially).
2. After a completed, non-cancelled run, derives each discovered project's #406 manifest and calls `AnalysisCacheStore.Put` — real population, gated on eligibility.
3. Ships `AnalysisCacheStore.TryGet`/`AnalysisCachePopulation.TryLookup` as complete, independently tested read-side authorization logic, exercised directly (hand-built manifests, and via `AnalysisCachePopulation` against a real temp project) rather than through a CLI-level "second run is faster" scenario.

This means the issue's acceptance scenario "first eligible run populates; second unchanged run avoids the measured safe work" is proven at the `AnalysisCacheStore`/`AnalysisCachePopulation` unit level (Put-then-TryGet is a Hit; the same inputs recomputed produce the same digest without needing to re-run analysis) but is **not yet observable as a wall-clock CLI speedup**, since nothing in the CLI/Testing execution path branches on a `Hit` yet. The follow-up work — adding that branch to `ArchitectureValidationApplicationService`/`ArchitectureAnalysisSnapshot` and reconstructing (or safely re-deserializing) a `ValidationOutcome` from a hit — is left for a dedicated future change once the `Payload` closed-set question above is resolved, and is called out explicitly in the PR rather than left implicit.

**Location resolution.** `AnalysisCacheLocationResolver.Resolve` never reads policy/fragment/baseline/snapshot/receipt/cache content — only `AnalysisCacheOptions` (Disabled/Auto/ExplicitPath), matching "Cache location defaults are opt-in and never authored by content." `auto` uses `%LOCALAPPDATA%`/`$XDG_CACHE_HOME`\`~/.cache` + `ArchLinterNet/0.5.1/analysis-cache/v1`. An explicit path is rejected if it resolves to a filesystem root, an existing file, or a symlink/reparse-point directory — mirroring `EvaluatedBuildInputManifestCollector`'s own symlink/containment checks.

**Envelope and integrity.** `AnalysisCacheEntryV1` is a closed set of concrete record types (no polymorphic fields) serialized via `System.Text.Json` with only a `JsonStringEnumConverter` — never a `$type`-based or `TypeNameHandling`-style converter, so a corrupted/foreign file can never construct or execute an arbitrary CLR type. `ContentDigest` is computed the same way `BuildStateCanonicalHasher`/`EvaluatedBuildInputManifestCollector` compute their own digests: an explicit ordinal canonical string join, not reliance on JSON key ordering.

**Publication and cancellation.** `AnalysisCacheStore.Put` writes to a same-directory uniquely-named temp file (`FileMode.CreateNew`, `FileShare.None`), checks `cancellationToken.IsCancellationRequested` immediately before the final `File.Move(..., overwrite: true)`, and deletes the temp file instead of publishing when cancelled — so a cancelled populate attempt can never expose a reusable entry, matching #375's cancellation-before-publication requirement and the `CancellationToken`-last-parameter convention.

**Instrumentation.** `AnalysisProfileCacheCounters` keeps `Status`/`Lookups`/`Hits` (unrenamed) and adds `Misses`, `Rejects`, `Writes`, `BytesRead`, `BytesWritten`, `IneligibleUnitCount`, `CorruptionEvents`, `CancelledBeforePublish`, `Mode` (`"disabled"`/`"auto"`/`"path"`, never an absolute path), and `RejectReasonCounts`. `AnalysisProfileReservedFieldStatus` gains `Active` (in addition to `NotApplicable`), exactly as that enum's own doc comment anticipated ("so #365/#408 can add real status values ... without renaming or restructuring this field later").

## Risks / Trade-offs

- [Risk] The deferred pipeline short-circuit means this change alone cannot demonstrate a wall-clock cache speedup. → Mitigation: called out explicitly in proposal.md/design.md and the PR description, with the exact seam (`ArchitectureValidationApplicationService`) named for the follow-up.
- [Risk] `EvaluatedBuildInputManifestCollector` always returning `CacheIneligible` today means population never actually persists an entry against this repository's own real projects. → Mitigation: this is #406's own documented, intentional state, not a defect of this change; tests prove the gate correctly rejects (`AnalysisCachePopulationTests.TryPopulate_RealProject_IsIneligibleBuildInputToday`) rather than silently accepting.
- [Risk] A future change extending the cached payload to full findings must handle `IArchitectureDiagnosticPayload`'s closed set safely. → Mitigation: documented here as the explicit reason full-outcome caching was not attempted now.

## Migration Plan

Purely additive: new namespace, new opt-in CLI option and command, new opt-in Testing API method, extended (never renamed) profile counters. No existing command, flag, exit code, or JSON/SARIF shape changes when `--cache`/`WithCache()` is not used.

## Open Questions

- Whether the follow-up pipeline short-circuit reconstructs `ValidationOutcome` from a richer cached payload (requiring the `IArchitectureDiagnosticPayload` closed-set converter) or re-runs only the parts of the pipeline not covered by the cached facts. Left to that future change.
