## Why

Apple Silicon CI can report a freshly built Release artifact as stale when its filesystem timestamp
is a few milliseconds older than the source timestamp. The post-`--ensure-built` metadata
rediscovery currently reaches that timestamp heuristic before it can verify the receipt and digest
published by the successful graph build, so a valid build is incorrectly blocked.

## What Changes

- Refresh digest evidence for the already selected artifact closure after a successful
  `--ensure-built` graph build instead of rediscovering the project output by timestamp.
- Run ordinary post-build receipt verification against that refreshed closure before materializing
  or loading any selected assembly.
- Preserve the ordinary discovery timestamp check for validation paths that have not just completed
  a successful receipt-producing build.
- Cover the post-build path and retain the policy-selected Release regression.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: Successful `--ensure-built` verification uses the published
  receipt and artifact digests as the post-build freshness proof without weakening ordinary stale
  output detection.

## Impact

Affected code is limited to Core snapshot orchestration and its focused tests. No public API,
policy schema, build command, or CI topology changes are required.
