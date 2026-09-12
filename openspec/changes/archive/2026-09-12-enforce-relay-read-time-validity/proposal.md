## Why

The Relay currently persists a verified publication but deliberately rejects
public reads. Consequently, a ready projection can neither be rendered as the
safe default README image nor be made unavailable by the origin once its lease
expires. This v0.8.x correctness/stabilization work closes that gap without
giving the transport authority to recompute Architecture Health or disclose
private provenance.

## What Changes

- Add public Relay routes for exact canonical JSON compatibility output and a
  fixed, freshness-stamped SVG rendering.
- Evaluate publication validity at every GET, HEAD, conditional request, and
  renderer selection using the trusted Relay clock; serve ready only while
  `now < valid_until`.
- Define cache, ETag, and unavailable-response behavior that cannot preserve a
  ready representation after expiry, revocation, corruption, or storage
  uncertainty.
- Document the default Relay README rendering as a bounded-current origin view,
  while retaining `headline-only/v1` JSON as an explicit snapshot-compatible
  representation and explaining third-party-cache limits.

## Capabilities

### New Capabilities

- `badge-relay-public-read`: fail-closed Relay public reads, fixed SVG
  rendering, and bounded cache/conditional-request behavior for approved badge
  publications.

### Modified Capabilities

- `architecture-health-publication-evidence`: make the product-owned validity
  horizon an enforced public Relay read boundary rather than publication-only
  evidence.

## Impact

Affected systems are the standalone Cloudflare Worker/Durable Object Relay,
its TypeScript integration and contract tests, its fixture vectors, the Relay
ADR, and the root README. There is no Core/CLI Health semantic change, no
private-source access, and no new public provenance, authentication, or
workflow contract.
