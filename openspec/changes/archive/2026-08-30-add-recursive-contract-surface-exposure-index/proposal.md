## Why

Architecture contracts can presently identify direct dependency and marker-placement concerns, but cannot explain how a visible API contract exposes a type through nested signatures or compiled attribute metadata. The v0.8 contract-surface rules need one deterministic, reusable evidence layer before they can govern those exposure paths.

## What Changes

- Add a session-scoped Core index that records recursive visible-contract exposure facts for exported type/member signatures and visible custom-attribute metadata.
- Preserve assembly-qualified referenced-type identities and stable, explainable paths through members, generic shapes, inheritance relationships, nested types, and metadata sites.
- Represent unavailable required first-party signature facts explicitly so future exposure contracts cannot mistake incomplete evidence for a safe, shortened graph.
- Consume the existing effective reviewed public-API selection where a caller supplies it; do not create another API-membership, semantic-role, or policy-decision model.
- Add focused fixture coverage for recursion, cycles, duplicate type names, custom attributes and typed attribute arguments.

## Capabilities

### New Capabilities

- `contract-surface-exposure-index`: Deterministic, reusable recursive exposure evidence for visible .NET contract signatures and compiled metadata.

### Modified Capabilities

- None.

## Impact

Affected area: `ArchLinterNet.Core` session/indexing and reflection-scanning internals, with Core NUnit fixtures. This is an internal reusable fact layer for future #513/#514 policy families; it adds no policy syntax, public-API snapshot grammar, semantic-role behavior, or runtime analysis.
