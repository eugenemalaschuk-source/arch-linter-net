## Context

The existing `BuildStateCanonicalHasher` recursively hashes a small extension allowlist below the project directory plus a few ancestor imports. Its receipt proves only that coarse digest and the PE digest. That model remains useful for current ordinary preflight, but it cannot prove that a cache consumer has represented every evaluated MSBuild, compiler, analyzer, reference, output, and context input.

## Goals / Non-Goals

**Goals:**

- Define one `analysis-build-state/v1` manifest owned by `Core.BuildState`.
- Make cache authorization explicit, deterministic, bounded, portable, and fail-closed.
- Preserve ordinary preflight semantics and the distinct build versus policy/session identities.
- Give future #365 one typed result instead of another equality model.

**Non-Goals:**

- Implementing the persistent cache, executing arbitrary MSBuild targets, replacing MSBuild evaluation, or accepting unverifiable projects for convenience.

## Decisions

### Manifest is a typed, canonical data model

`EvaluatedBuildInputManifestV1` captures a selected project/output context, portable logical paths and SHA-256 digests for trusted repository inputs, context values, deterministic references, and verified artifact bytes. Its canonical digest is produced by the existing build-state hasher, so no second cache key format exists.

### Eligibility is independent from ordinary preflight currentness

`CacheEligibility` has exactly `VerifiedCacheEligible` and `CacheIneligible`; the latter carries sorted, stable reason codes. Ordinary build-state preflight remains able to classify a receipt-backed artifact as current. A later persistent-cache reader must require the former outcome and recompute otherwise.

This avoids treating conservative build-state verification as cache authorization while preserving existing no-cache behavior.

### Safe static collection plus explicit refusal

The collector reads only repository-contained, resolved regular files and project XML. It records known safe inputs such as project/import/include paths, linked source files under configured containment, package/project/framework/assembly references and target context. It does not invoke MSBuild, execute targets, follow repository escapes, or infer missing generated/analyzer inputs. Unsupported constructs, missing/ambiguous files, external paths, wildcards that cannot be bounded, generator/analyzer identity gaps, and output verification gaps add cache-ineligible reason codes.

Executing `dotnet msbuild` was rejected because untrusted repository input can invoke arbitrary project functions/tasks and it would create a competing build/evaluation authority.

### Receipt binds the manifest and every required output artifact

The receipt records the evaluated-manifest digest, cache eligibility/reasons, selected configuration/TFM/platform/RID, and hashes for PE, optional PDB, `.deps.json`, and `.runtimeconfig.json`. Verification re-hashes these bytes and re-collects the manifest before reporting a cache-eligible result. A change in the observed inputs or artifact bytes results in a new digest or an ineligible outcome; it cannot authorize reuse.

### TOCTOU protection is snapshot-time verification

The collector captures file identities with their bytes and exposes a `VerifyUnchanged` step. Receipt creation and cache-eligibility publication compare a second collection with the first, reporting an ineligible `input-changed-during-verification` result rather than publishing a candidate.

## Risks / Trade-offs

- [Complex MSBuild features are not statically provable] → return a stable `cache-ineligible` reason instead of guessing.
- [Manifest expansion increases hashing cost] → keep arrays sorted and deduplicated, impose input/count and byte bounds, and expose profiling counters.
- [Receipt schema evolves] → add optional v1 fields with deterministic validation and require their presence only for cache eligibility; legacy receipts remain ordinary-preflight compatible but cache-ineligible.
- [Platform path differences] → use normalized repository-relative coordinates, reject ambiguous containment/case aliases, and keep absolute paths as evidence only.

## Migration Plan

1. Add manifest/eligibility DTOs and collector with unit tests.
2. Extend receipt writing and verification while accepting legacy receipts for ordinary preflight.
3. Surface the result through Core/CLI/Testing projections and profile counters.
4. Archive the OpenSpec change after validation. No cache data exists to migrate or roll back.

## Open Questions

None. Inputs that cannot be represented safely are deliberately cache-ineligible until a later versioned extension supports them.
