## Why

The current cache hit occurs only after CLR assembly loading and session setup, so it does not avoid the expensive work that persistent caching is meant to skip. The post-optimization evidence also accepted an inactive parallel path and did not retain enough provenance or pre/post statistics to substantiate the release claim.

## What Changes

- Split validation into a metadata-only preparation plan and lazy CLR/session materialization, allowing each evaluated mode to perform an authorized cache lookup before any assembly is loaded.
- Bind cache authorization to artifact selection and captured identity evidence computed independently of a cache entry; fail closed when preparation evidence is incomplete or becomes stale.
- Add explicit avoided-work cache counters and require real bounded-parallel fact work in the release benchmark fixture.
- Preserve strict and audit profiles in paired samples, publish wall-clock/resource distributions and an attributable #374 baseline-to-post comparison.
- Restore the cache-boundary work to an active OpenSpec change and remove the obsolete limitation on cache authorization scope.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-snapshot`: Make setup lazy from an immutable preparation plan while retaining shared-snapshot semantics.
- `analysis-cache`: Authorize pre-materialization lookup from independently selected artifact evidence and report avoided work.
- `assembly-resolution`: Separate metadata-only artifact planning from CLR loading and verify artifact bytes at materialization.
- `bounded-parallel-scanning`: Require observable active work and deterministic merge evidence for a parallel activation claim.
- `analysis-profile`: Publish complete, attributable post-optimization comparison evidence.
- `analysis-build-state-fingerprints`: Fail closed for incomplete ancestor build-file and nested-import evidence.

## Impact

Core validation, assembly resolution, build-state/cache authorization, profile schema/counters, the large multi-host benchmark fixture, checked-in evidence, and their NUnit/OpenSpec tests are affected. Existing public validation outcomes remain semantically unchanged; profile evidence gains fields.
