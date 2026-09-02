## Why

The contract-surface, marker-placement, version-isolation, and selected-public-API features have shipped independently, but adopters do not yet have one realistic declarative reference policy that demonstrates their intended composition. Without that evidence, an author can accidentally substitute ordinary dependency rules for visible-signature governance or conflate an API-membership marker with a type's semantic role.

## What Changes

- Add a synthetic, fully declarative reference-policy test scenario that composes existing public-API selection, attribute-usage, recursive contract-surface exposure, and versioned-isolation contract families.
- Cover strict and audit findings, recursive path evidence, format and Testing projections, selected-surface snapshot lifecycle behavior, and backward-compatible whole-assembly selection.
- Document the recommended contract composition with realistic server and library/runtime-editor examples, including the distinction between marker placement, API membership, and visible-surface exposure.

## Capabilities

### New Capabilities

None. This change adds adoption evidence and authoring guidance for capabilities that are already implemented.

### Modified Capabilities

None. The existing policy semantics and public API remain unchanged.

## Impact

The Core NUnit adoption/reference-policy tests and contract authoring documentation change. No production code, policy schema, public API, package dependency, or analyzer behavior changes.
