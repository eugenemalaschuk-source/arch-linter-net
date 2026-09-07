## Context

The existing read-only PR producer creates an inert CLI payload and a manifest bound to the validated pull-request tree. A serialized, privileged `push main` publisher verifies that evidence and atomically updates the payload and a v1 receipt on the static `architecture-health-badge` branch. The current CLI headline omits the independent Gate, and the receipt is too small to audit the entire producer-to-publisher chain.

## Goals / Non-Goals

**Goals:**

- Render Gate and Health as independent, canonical facts while retaining Health-owned color and Gate-owned exit code.
- Version the public receipt to make the exact main/tree, producer, publisher, digest, status, and timing evidence independently inspectable.
- Preserve static-only, fail-closed, atomic publication and clarify the separate freshness layers for raw GitHub, Shields, README, and Camo.

**Non-Goals:**

- Change Architecture Health, Gate, waiver, policy, baseline, or finding semantics; re-run analysis after merge; create a badge server or Pages deploy; alter the legacy strict-policy badge; or claim live merge evidence before a qualifying PR has actually merged.

## Decisions

### Keep semantic projection in the CLI

The badge message becomes `GATE · HEALTH · ignores · rules`, read solely from the canonical Health document and policy-inventory receipt. Health remains the only color authority and Gate remains the only exit-code authority. This avoids having workflow JavaScript infer Gate or recolor a non-healthy health state.

### Version the receipt rather than silently extending it

The publisher writes `architecture-health-badge-publication/v2` with explicit analyzed base/head/head-tree and merged main/main-tree identities, producer and publisher run/attempt identifiers, payload SHA-256, status/reason, and an ISO 8601 publication timestamp. An explicit schema version avoids a consumer mistaking an older v1 receipt for complete evidence. The payload stays byte-for-byte deterministic and excludes transport time.

### Retain one atomic static-branch commit

The publisher creates payload and receipt blobs in a single tree/commit and uses a guarded ref update. The current-main and compare-and-swap checks remain the ordering authority; receipt detail does not turn an out-of-order event into a valid current publication. This is preferred to independent file updates, which could expose mismatched state.

### Treat raw evidence and rendered freshness as separate layers

README points to the static receipt and documentation explains that the receipt proves source freshness, while Shields endpoint caching and GitHub README/Camo rendering have separately bounded, non-semantic delay. No cache-busting commit, payload perturbation, or artificial counter/color change is permitted.

## Risks / Trade-offs

- [A live merge is required to prove the external raw-to-rendered path] → unit/transport tests cover the protocol in the PR; after a qualifying squash merge, record real receipt, run, raw, Shields, and README evidence on #800.
- [Receipt schema consumers may expect v1] → publish v2 explicitly and document the versioned contract rather than treating a missing field as benign.
- [Publisher timing is non-deterministic] → time is receipt-only provenance; it never enters the CLI payload or its digest.
- [Shields/Camo cache propagation is outside repository control] → describe supported diagnostic ordering and do not treat unchanged semantic values as stale evidence.

## Migration Plan

1. Update CLI projections and golden tests for all Gate/Health combinations and unassessable evidence.
2. Update the publisher and transport tests for the v2 receipt, stale/rejected fallbacks, and compare-and-swap behavior.
3. Update README and CI guidance, run scoped validation, then archive the synchronized OpenSpec change before opening the PR.
4. After merge, use the v2 receipt plus real producer/publisher URLs to verify raw source and renderer freshness, recording the evidence in issue #800.
