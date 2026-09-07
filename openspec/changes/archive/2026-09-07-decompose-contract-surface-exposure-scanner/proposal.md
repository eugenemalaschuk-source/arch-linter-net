## Why

`ArchitectureContractSurfaceExposureScanner` and its nested `Walker` became a partial aggregate
while establishing recursive visible-contract evidence. The scanner needs cohesive internal
responsibilities without changing the evidence consumed by contract-surface and version-isolation
governance.

## What Changes

- Reduce the scanner to one non-partial internal entry facade.
- Extract shared scan state plus focused recursive traversal, member/accessor, and compiled
  attribute-metadata collaborators.
- Remove the two reviewed partial-declaration exceptions for the scanner and its former walker.

## Capabilities

No externally observable capability or contract changes. This behavior-preserving internal
refactor intentionally skips delta specs; the active `decompose-god-classes` change remains the
source of truth for the broader aggregate-cleanup architecture policy.

### New Capabilities

None.

### Modified Capabilities

None.

## Impact

Core Scanning implementation internals, existing focused Core exposure and consumer tests, the
reviewed self-policy partial-declaration inventory, and the active architecture-cleanup change.
No public API, policy schema, CLI, cache, or consumer behavior changes are intended.
