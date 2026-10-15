## Why

Badge promotion can run after the producer's configured one-hour freshness lease has elapsed while the product-owned semantic horizon is still valid. The resolver currently revalidates only after that semantic horizon, so the decision rejects otherwise current evidence before it can use a trusted receipt to start a new bounded transport lease.

## What Changes

- Trigger trusted temporal revalidation when either the producer freshness lease or the product-owned semantic horizon has elapsed.
- Keep the revalidation bound to the exact source Health bytes, payload bytes, merged tree, and producer identity; keep unassessable temporal facts fail-closed.

## Capabilities

### New Capabilities

### Modified Capabilities
- `architecture-health-badge-promotion`: revalidation also renews the bounded transport lease after producer freshness expires, even when the semantic horizon remains in the future.

## Impact

Updates the Python badge-promotion resolver and its tests, plus the promotion capability spec. It does not change the CLI receipt schema, public API, or canonical Health and badge bytes.
