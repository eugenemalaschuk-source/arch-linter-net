## Why

The topology review workflow currently violates Core's protected Execution boundary, can overwrite trusted inputs through filesystem aliases, and claims lifecycle evidence that is not automated. These defects leave the feature's public contract and acceptance guarantees unproven while its CI is red.

## What Changes

- Project topology observations into a neutral Core seam so the `Topology` application surface never imports Execution-owned types.
- Complete the reviewed public-API approval surface and keep implementation-only topology services internal.
- Make real .NET and Unity-style fixture capture, diff, and verify flows deterministic automated acceptance evidence.
- Correct nested topology command usage hints and make output publishing collision-safe for aliases and failed writes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `topology-review-workflow`: strengthen isolation, lifecycle-evidence, command-diagnostic, and trusted-input safety requirements.

## Impact

Changes affect Core topology/validation seams, CLI topology command handling and file publication, the reviewed Core API baselines, topology fixtures and NUnit acceptance tests. No new dependencies or network-facing behavior are introduced.
