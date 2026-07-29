## Why

The safety-critical adoption work is implemented in isolated component tests, but it lacks one reusable, external-adopter-shaped corpus that proves those pieces work together. Checkpoint A needs durable internal evidence without being mistaken for a release authorization.

## What Changes

- Add a synthetic adoption acceptance corpus with small, conventional multi-project, multi-host, migration, and clean-checkout fixture shapes.
- Add a deterministic scenario manifest, fixture ownership documentation, executable Checkpoint A entrypoint, and checked-in platform/scenario evidence.
- Exercise equivalent CLI and `ArchLinterNet.Testing` behavior plus human, JSON, and SARIF projections from the shared fixtures.
- Record explicitly that Checkpoint A is implementation evidence only and cannot publish or authorize version 0.5.1.

## Capabilities

### New Capabilities

- `adoption-acceptance-corpus`: Reusable synthetic adopter fixtures, deterministic scenario inventory, and executable internal Checkpoint A evidence.

### Modified Capabilities

- `adoption-stabilization-compatibility`: Make the reusable Checkpoint A corpus and its non-release status an explicit compatibility requirement.

## Impact

Affected areas include new acceptance-test fixtures and harnesses, internal corpus/evidence documentation, the test project entrypoints, and the adoption-stabilization OpenSpec contract. No package publishing, release automation, or public release-version behavior changes.
