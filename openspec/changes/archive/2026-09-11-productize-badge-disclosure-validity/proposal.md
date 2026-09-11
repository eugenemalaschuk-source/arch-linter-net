## Why

The accepted Badge Relay contract already defines closed public payload profiles and
bounded semantic freshness, but the product has no Core/CLI implementation that
produces the required validity evidence or that lets a transport verify canonical
bytes without re-deriving Architecture Health facts. Issue #827 closes that gap
while retaining the existing badge headline, color, and exit-code semantics.

## What Changes

- Add a Core-owned, deterministic public-projection/evidence contract that derives
  a finite semantic validity horizon from canonical waiver, evaluation-date,
  inventory, and external-evidence receipts, and fails closed when it cannot do so.
- Add canonical CLI representations for the approved `headline-only/v1` and
  `headline-plus-freshness/v1` profiles, including exact UTF-8 bytes, digest,
  closed-field validation, actionable failures, and profile/schema compatibility
  handling.
- Package the schema and golden/adversarial fixtures with the consumer-facing
  product surface, and prove the behavior from a freshly packed CLI rather than a
  source-project reference.
- Preserve the legacy `badge architecture-policy` path and the established
  Architecture Health Gate/Health/count/color/exit semantics.

## Capabilities

### New Capabilities

- `architecture-health-publication-evidence`: Canonical, fail-closed derivation
  of a semantic validity horizon for trusted Architecture Health publication.

### Modified Capabilities

- `architecture-health-badge-relay-contract`: Make the existing disclosure and
  freshness contract executable at the product/transport boundary.
- `architecture-policy-badge-cli`: Add the versioned canonical Architecture
  Health disclosure-profile output and validation behavior.

## Impact

- Core Architecture Health reporting/evidence models and their NUnit coverage.
- Badge CLI application/command surface, package content, and CLI integration
  coverage.
- Relay contract schemas, conformance fixtures, and the corresponding OpenSpec
  specifications; no Relay runtime, cloud provider workflow, or README/adopter
  deployment work is introduced.
