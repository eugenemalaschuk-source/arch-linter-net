## Why

#374 measured the pre-cache phase/counter baseline. #406 delivered a complete-or-ineligible evaluated build-input manifest (`EvaluatedBuildInputManifestV1`/`CacheEligibility`) but its current static collector always reports `CacheIneligible` for real MSBuild evidence — intentional, per its own design.md, until a safe evaluated-MSBuild collector exists. #375 delivered cancellation-safe I/O/publication semantics and moved `CancellationToken` to the last parameter across build/report/validation paths. #365 must add the persistent cache itself, consuming these three contracts rather than redefining them, while staying disabled by default and never producing a false success from untrusted bytes.

## What Changes

- Add a new `ArchLinterNet.Core.Caching` capability: a versioned `analysis-cache/v1` on-disk envelope, a reuse-authorization key (workspace/policy/mode/contract/configuration/TFM/platform/RID), a project-manifest-gated entry (`AnalysisCacheProjectManifest` wrapping #406's `EvaluatedBuildInputManifestV1`), and a store (`AnalysisCacheStore`) providing bounded reads, atomic staged writes, full typed miss/reject-reason authorization, `Inspect`, and `Clear`.
- Add `--cache <disabled|auto|path>` to the CLI `validate` command (disabled by default; `auto` resolves to the platform user-cache namespace `ArchLinterNet/0.5.1/analysis-cache/v1`; any other value is a caller-selected path validated for canonical containment/safety) and a `cache inspect`/`cache clear` command pair.
- Add `ArchitectureValidationBuilder.WithCache(AnalysisCacheOptions)` to `ArchLinterNet.Testing`, sharing the same `AnalysisCachePopulation`/`AnalysisCacheStore` engine the CLI uses (one implementation, not two).
- Extend `analysis-profile/v1`'s already-reserved `Counters.Cache` section (see `analysis-profile/spec.md`, "Cache and concurrency fields are explicitly reserved") with real lookup/write/reject/byte/reason-count fields and an `Active` status value, without renaming or restructuring the existing `Status`/`Lookups`/`Hits` fields.
- Populate a cache entry after every completed, non-cancelled run whose discovered projects are all #406 `VerifiedCacheEligible` — which, given the current collector, means population is real and tested end-to-end (against hand-constructed eligible manifests and via `AnalysisCachePopulation` against a real project) but never actually persists anything for this repository's own self-lint today, and this is by design: an incomplete build-input manifest must never authorize reuse.

## Capabilities

### New Capabilities
- `analysis-cache`: the persistent `analysis-cache/v1` storage/authorization engine, its CLI/Testing configuration surface, and its `analysis-profile/v1` instrumentation extension.

### Modified Capabilities
- `analysis-profile`: `Counters.Cache`'s reserved fields gain real values and an `Active` status (see that capability's own "Cache and concurrency fields are explicitly reserved" requirement, which explicitly anticipates this).

## Impact

- New code under `src/ArchLinterNet.Core/Caching/`.
- New `src/ArchLinterNet.Cli/Commands/Cache/` command module and a new `--cache` option + population wiring in `src/ArchLinterNet.Cli/Commands/Validate/`.
- New `WithCache()` in `src/ArchLinterNet.Testing/ArchitectureValidationBuilder.cs`.
- Extended `AnalysisProfileCacheCounters`/`AnalysisProfileReservedFieldStatus` in `src/ArchLinterNet.Core/Profiling/`, and `schema/0.5.1/analysis-profile.schema.json`.
- New tests across `tests/ArchLinterNet.Core.Tests/` and `tests/ArchLinterNet.Cli.Tests/`.
- No breaking changes: `--cache` is opt-in, defaults preserve all existing CLI/Testing behavior exactly.
- **Known deferred scope** (see design.md): this change does not wire a cache hit into the CLI/Testing execution path to actually skip project evaluation/scanning/contract execution on a second run. It delivers the complete storage/authorization/instrumentation engine and population, not yet a live pipeline short-circuit — see design.md's "Deferred: live pipeline short-circuit" for the reason and the follow-up this leaves for a later change.
