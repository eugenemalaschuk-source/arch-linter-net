# Design

## Decision 1: shard by scenario ownership, not by random test selection

Checkpoint B is decomposed into nine explicit NUnit methods:

1. package and entrypoints;
2. core adopter runtime/parity;
3. extended adopter runtime/cache;
4. consumer-cleanup policy foundation;
5. consumer-cleanup configuration and identity;
6. consumer-cleanup source-set authoring;
7. public-API surface-selector snapshot and role preservation;
8. public-API surface-selector delta and membership lifecycle;
9. public-API surface-selector fail-closed enforcement and Testing-adapter parity.

The complete fixture remains non-parallel locally and uses one `OneTimeSetUp` candidate. CI filters one method per isolated runner. This keeps the scenario partition reviewable and deterministic.

## Decision 2: one immutable candidate, many isolated consumers

PR CI prepares one ephemeral prerelease candidate (`0.6.1-ci.<run-id>`) on Ubuntu, writes the existing candidate manifest, and uploads it once. Every Windows/macOS shard downloads exactly that artifact. The release workflow already owns an immutable candidate preparation stage and reuses that same artifact across all release shards.

Each shard still gets its own temporary NuGet caches, tool install path, fixtures, and checkout, so mutable state is never shared across concurrent runners.

## Decision 3: partial execution evidence is not release evidence

A shard writes `checkpoint-b-platform-shard-evidence/v1`. The merge tool requires exactly the nine named shards, validates common candidate/platform metadata, rejects duplicate/overlapping/missing/unexpected scenario IDs, requires policy-shape evidence from the consumer-cleanup policy-foundation shard, and emits the existing `checkpoint-b-platform-evidence/v1` record.

The existing final release aggregator remains the authority over required platforms, complete scenario inventory, policy shape, repository gates, release scope, and publication authorization.

## Decision 4: preserve required PR check contexts

The active `Main` ruleset requires `Packed Artifact Test Suite (Windows)` and `Packed Artifact Test Suite (Apple Silicon macOS)`. Those names remain as small fan-in jobs that validate canonical merged evidence after producer shards finish. The producer jobs expose detailed failure localization without changing branch-protection contexts.

## Decision 5: repository gates and packed-candidate gates are separate proofs

`make acceptance` remains the full local convenience gate. A new `make acceptance-repository` runs lint + unit + ordinary E2E only. Release candidate preparation executes that repository gate plus strict OpenSpec once, then builds the release-version binaries immediately before `--no-build` packing; this matters because repository acceptance recompiles the ordinary development-version outputs. It records the passed gates after the candidate manifest exists and does not rerun Checkpoint B through generic acceptance. The final evidence job consumes the recorded repository-gate artifact instead of repeating acceptance.

## Decision 6: timeout must bound descendants

The common process runner reads stdout/stderr asynchronously and awaits process exit with the current NUnit cancellation token. Cancellation kills the entire process tree before propagating `OperationCanceledException`. A focused cross-platform regression starts a real descendant process and proves it is gone after cancellation.
