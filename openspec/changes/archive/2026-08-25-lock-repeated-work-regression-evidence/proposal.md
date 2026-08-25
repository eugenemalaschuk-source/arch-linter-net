## Why

Issues #652 and #653 now remove session-local metadata and exported-surface
reconstruction, but their focused unit tests prove the paths independently.
This change closes the consumer-shaped regression gap without creating the
broader large-solution benchmark framework reserved for #502.

## What Changes

- Add one deterministic, synthetic multi-project and repeated-contract Core
  regression fixture that exercises the metadata-index and public-API reuse
  paths together.
- Assert each covered lookup family's own index/surface materialization path
  remains bounded by immutable session facts rather than contract fan-out.
- Lock a non-empty ordered canonical findings projection with a checked-in
  checksum and assert strict/audit results through the Testing API and CLI.
- Record the fixture's purpose, scope boundary, and optional
  hardware-sensitive measurement guidance in internal performance evidence.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This change adds regression evidence for existing session-index
requirements; it does not alter product behavior or its OpenSpec contract.

## Impact

Core test-only fixture and internal evidence documentation. No production
API, policy schema, profile schema, cache lifecycle, or cross-process state
changes.
