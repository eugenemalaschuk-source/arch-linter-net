## Why

Issue #675 needs an evidence-backed Phase 1 decision about whether ordinary real-MSBuild consumer projects should become eligible for the opt-in `analysis-cache/v1`. The repository already has a reusable #502 benchmark corpus and a fail-closed cache implementation, but the current real-MSBuild probe is only a routing note and does not record the required S/M/L effect model, reference/base-side disposition, or explicit A/B/C outcome. The #991 adopter-normalization gate is still open, so Phase 2 eligibility changes are not authorized.

## What Changes

- Extend the existing #502 benchmark/evidence path with a focused real-MSBuild cache-eligibility matrix at representative small, medium, and large solution sizes.
- Capture typed cache eligibility reasons, miss/population/hit counters, avoided-work counters, authorization and cache I/O overhead, canonical-result equivalence, and relevant build/reference/base identity observations.
- Add a deterministic pre-implementation effect model with targeted-phase share, Amdahl upper bound, cold/miss versus warm-hit costs, realistic reuse assumptions, memory/disk/I/O trade-offs, break-even, success threshold, and kill criterion.
- Record an anonymized internal evidence artifact and markdown report that explicitly selects outcome A, B, or C and routes any Phase 2 work through the #991 gate.
- Preserve the existing fail-closed authorization contract, keep `analysis-cache/v1` distinct from `prepared-analysis/v1`, and add focused tests for the evidence model and decision guardrails.

## Capabilities

### New Capabilities

None. This is internal benchmark/evidence tooling and repository documentation; it does not introduce a new user-facing or runtime capability.

### Modified Capabilities

None. Existing `analysis-cache/v1` and `analysis-profile/v1` runtime requirements remain unchanged.

## Impact

- Affected areas: `tests/ArchLinterNet.Core.Tests` benchmark/evidence harnesses, `docs/internal` performance evidence, and OpenSpec planning artifacts.
- No production assemblies, public APIs, cache authorization rules, schemas, or package behavior are changed.
- The generated evidence must contain only synthetic/anonymized consumer shapes and must not identify private adopters or include raw private CI logs.
- Full Phase 2 implementation remains blocked until #991 completes and normalized workflows are remeasured; an outcome other than A closes/defer-routes the eligibility expansion without runtime changes.
