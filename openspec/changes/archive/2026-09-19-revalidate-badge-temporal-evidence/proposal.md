## Why

The architecture-health badge publisher can receive a valid, exact-tree Health result whose finite semantic horizon expires between pull-request validation and squash publication. The current promotion path then reports `semantic_horizon_expired` even though the merged tree is unchanged and the time-sensitive evidence can be safely revalidated. This creates a false unavailable result at the first-party badge boundary after a UTC-day transition.

The fix must preserve the original Health result and its authoritative payload while giving promotion a canonical, bounded way to refresh only temporal evidence for the same tree and policy evidence. The publisher must not become a second architecture evaluator, and all unverifiable or still-invalid evidence must remain fail-closed.

## What Changes

- Add a Core temporal publication-evidence revalidation capability that consumes serialized Health report evidence, applies the existing Core waiver/evidence semantics at an explicitly supplied UTC evaluation date, and emits a schema-versioned receipt bound to the source evidence, exact merged tree, and badge payload identity.
- Expose the capability through a read-only CLI subcommand suitable for the trusted publisher. It must not rerun assembly analysis, recompute Health, or alter the original Health or badge payload.
- Update raw and Relay promotion/renewal to invoke and validate the canonical receipt when the frozen temporal horizon has elapsed, then use only the receipt’s bounded horizon. Preserve exact-tree provenance, payload bytes, fail-closed reason codes, and provider-specific authorization checks.
- Add focused Core, CLI, publisher, Relay, and workflow-contract regression coverage for the cross-midnight path and the required expired, stale, invalid, mismatched, and unavailable cases.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-publication-evidence`: define exact-tree temporal revalidation and its bound receipt.
- `architecture-health-badge-promotion`: allow promotion to consume a canonical refreshed temporal receipt while preserving immutable Health/payload and provenance contracts.
- `architecture-policy-badge-cli`: define the trusted CLI entry point and fail-closed output contract for temporal revalidation.

## Impact

The change affects the Core validation/evidence model, the CLI health command surface, the Python badge-promotion resolver, the raw/Relay promotion path, and their tests. It adds no new cloud credential or storage requirement and does not change the canonical Health/Gate/counts/payload result. Existing publication remains unavailable whenever the temporal receipt is missing, malformed, mismatched, expired, stale, invalid, or otherwise not assessable.
