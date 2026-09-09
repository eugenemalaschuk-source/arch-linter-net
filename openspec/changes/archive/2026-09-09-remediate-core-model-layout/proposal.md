## Why

The frozen post-v0.8 self-architecture audit records 24 advisory layout diagnostics for model types declared under `src/ArchLinterNet.Core/Model/`. The repository already defines `Models` as the conforming source-directory convention, so these files need a focused physical-layout repair without changing their namespaces, public contracts, or runtime behavior.

## What Changes

- Move the six source files that contain the 24 owned model classes, enums, and records from `Core/Model` to `Core/Models`.
- Preserve all type names, namespaces, serialization shapes, and behavior.
- Add focused self-policy evidence that the moved types conform while a deliberately misplaced model remains detectable.
- Record the exact baseline identities, responsibility-to-path map, and before-to-after reconciliation in the change artifacts and PR.

## Capabilities

### New Capabilities

None. This is a physical source-layout refactor using an existing convention contract.

### Modified Capabilities

None. The existing `layout-convention-contracts` requirements are unchanged; the production source tree is being brought into conformance.

## Impact

Only `ArchLinterNet.Core` source locations and focused self-policy test evidence change. Public APIs, namespaces, policy semantics, baselines, exclusions, and audit severity remain unchanged.
