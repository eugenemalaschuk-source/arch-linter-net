## Context

See `proposal.md` for the motivation and contract scope. The current Relay
security seam keeps one in-memory map of keys for the fixed GitHub JWKS URL but
refreshes immediately for every unknown `kid`. The Worker process is the right
scope for this cache because the existing implementation is process-local and
the JWKS provider is shared across aliases; Durable Object state is not needed
for a short-lived provider-work circuit.

## Goals / Non-Goals

**Goals:**

- Make unknown-key provider work bounded in any protection window.
- Ensure concurrent misses share one fixed-endpoint fetch.
- Keep valid cached keys usable without a fetch.
- Let provider rotation recover automatically after the protection window.
- Keep memory bounded without retaining attacker-controlled key IDs.

**Non-Goals:**

- Changing the GitHub issuer, JWKS URL, JOSE algorithm, claims, registry pins,
  or publication state machine.
- Accepting stale or unknown keys during provider failure.
- Persisting JWKS state in Durable Object storage or introducing another JOSE
  implementation.
- Adding a per-`kid` negative cache.

## Decisions

1. **Use one fixed-endpoint cache entry with a short cooldown.**
   The entry stores the validated provider key map, the time of the most recent
   refresh attempt, and an optional in-flight refresh promise. A five-second
   cooldown is long enough to collapse attack bursts while keeping rotation
   recovery bounded; the value is named and testable. The timestamp advances on
   both success and failure, so an outage cannot be turned into one provider
   request per invalid token.

2. **Coalesce before applying the cooldown result.**
   A miss that observes an existing in-flight refresh awaits it. Only a miss
   with no in-flight operation and an expired cooldown may start a fetch. This
   avoids a race where concurrent requests each pass a stale timestamp.

3. **Retain only provider-bounded data.**
   The provider response remains limited to 32 supported keys and 128-byte key
   IDs, as it is today. The cache has one fixed endpoint entry and never stores
   a requested `kid`, negative result, token, or claim. This makes the memory
   bound independent of invalid traffic volume.

4. **Fail closed while preserving safe cached-key behavior.**
   A required refresh failure rejects the requested token with the existing
   authorization failure. A previously cached key remains available for tokens
   that select it; unknown keys are never accepted from stale or partial data.
   A successful later refresh replaces the bounded key set and can discover a
   rotated key.

5. **Test through the existing security and Relay seams.**
   Focused tests will use the existing token/JWKS fixtures and fake clock to
   prove cache hits, coalesced concurrent misses, cooldown suppression,
   post-window rotation, provider failure, and bounded state. Existing claim,
   issuer, audience, algorithm, registry, replay, and lifecycle tests remain
   unchanged and provide regression coverage for the trust boundary.

## Risks / Trade-offs

- [Risk] A newly rotated provider key may be rejected briefly during the
  cooldown. → Mitigation: the window is short and the next miss after it always
  has a permitted fixed-endpoint refresh path.
- [Risk] A provider outage delays retry attempts for the duration of the
  cooldown. → Mitigation: the Relay remains fail closed and retries after a
  bounded interval rather than accepting stale/unknown keys.
- [Risk] Process-local caches are not shared across Worker isolates. →
  Mitigation: each isolate independently enforces the same per-process bound;
  the issue requires bounded work, not global provider rate limiting.

## Migration Plan

No data or configuration migration is required. Deploying the new bundle starts
with an empty bounded cache; rollback restores the prior code path but should
be treated as reverting the security hardening and therefore requires the
existing release review.
