## Context

See proposal.md for the failure this addresses. The promotion decision already bounds readiness by the earlier of the product semantic horizon and the configured lease duration measured from producer verification. The resolver currently starts temporal revalidation only when the semantic horizon elapses.

## Goals / Non-Goals

**Goals:**
- Make the resolver refresh temporal evidence when the producer anchored lease expires, matching the freshness boundary used by the promotion decision.
- Preserve the existing identity binding, product-owned horizon, and fail-closed behavior.

**Non-Goals:**
- Change the temporal receipt schema, canonical Health evidence, or badge payload.
- Recompute Architecture Health during promotion.

## Decisions

- Compare the current UTC time with both the semantic horizon and `verified_at + max_lease_seconds`. Start revalidation when either boundary has elapsed; this is equivalent to refreshing at the earlier effective validity boundary used by the decision.
- Keep the producer run timestamp as provenance. Only a successfully validated temporal receipt supplies a new publisher verification time and transport lease anchor.
- Continue using the trusted CLI revalidator for evidence semantics. If it is unavailable, returns an unassessable result, or produces a stale or mismatched receipt, promotion remains unavailable.
- Leave fresh publications metadata-only: restoration and building the trusted revalidator occur only after one of the validity boundaries has elapsed.

## Risks / Trade-offs

- A queued publication that outlives the producer lease now requires NuGet restore/build for trusted revalidation; if those dependencies are unavailable, publication remains fail-closed.
- Revalidation still cannot revive expired or incomplete waiver/external evidence because the Core receipt remains authoritative.
